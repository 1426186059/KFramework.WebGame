using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using KFramework.MonoGame;
using KFramework.MonoGameExtend;

// MirEngine 全局 using 也导出了 Rectangle / Vector2；这里用别名消歧，明确使用 KFramework.MonoGame 版本。
using Rectangle = KFramework.MonoGame.Rectangle;
using Vector2 = KFramework.MonoGame.Vector2;

/// <summary>
/// 白鹭普通图集（egret.SpriteSheet / textureData）：JSON 只负责「静态坐标映射」——
/// frames(字典) 或模型 frames(数组) 给出整页 PNG 上每个子图的 {x,y,w,h}，
/// 不含帧序列、帧率、事件、标签等任何动画时序信息。
///
/// 与 MovieClip 图集的区别见 <see cref="EgretMovieClipAtlas"/>：两者底层都靠同一张 PNG
/// 存纹理，但 JSON 的语义/结构完全不同，所以分成两个类而不是一个「大杂烩」图集类。
/// </summary>
public class EgretAtlas
{
    /// <summary>同目录整页贴图文件名（png）。</summary>
    public string File = "";

    /// <summary>已加载的整页贴图（Build 后可用）。</summary>
    public Texture2D Texture { get; protected set; }

    /// <summary>帧名 → KSpriteInfo（整页贴图上的子图，Build 后可用）。</summary>
    public Dictionary<string, KSpriteInfo> Frames { get; protected set; }

    /// <summary>帧名 → (offX, offY) 注册点偏移。仅模型帧（frames 数组）格式非零。</summary>
    public Dictionary<string, Vector2> Anchors { get; protected set; }
        = new Dictionary<string, Vector2>(StringComparer.OrdinalIgnoreCase);

    /// <summary>模型帧的虚拟画布尺寸（frames 数组元素里的 sourceW/sourceH）；UI 图集为 0。</summary>
    public int SourceW, SourceH;

    /// <summary>本图集是否含 MovieClip 时序层。</summary>
    public virtual bool IsMovieClip => false;

    protected sealed class FrameDef
    {
        public string Name;
        public Rectangle SourceRect;
        public Vector2 Size;       // 原始（未裁剪）尺寸
        public int OffX, OffY;     // 模型帧注册点偏移
    }

    protected readonly List<FrameDef> Defs = new List<FrameDef>();

    /// <summary>按帧名字典序预排序的帧名序列（模型帧名 "0","1"... 正好即播放顺序）。</summary>
    private List<string> _frameNames;

    /// <summary>
    /// 按 JSON 语义分流创建：带 "mc" 段时间线 → <see cref="EgretMovieClipAtlas"/>；
    /// 否则 → 普通 <see cref="EgretAtlas"/>。只解析 JSON，贴图需再调 Build。
    /// </summary>
    public static EgretAtlas Create(string json)
    {
        JsonNode root = null;
        try { root = JsonNode.Parse(json ?? ""); }
        catch (Exception) { return new EgretAtlas(); }

        bool isMovie = root is JsonObject o && o["mc"] is JsonObject;
        EgretAtlas atlas = isMovie ? new EgretMovieClipAtlas() : new EgretAtlas();
        atlas.ParseFrom(root);
        return atlas;
    }

    /// <summary>供派生类（兼容旧类型 Mir2AtlasSheet）用自身类型解析 JSON。</summary>
    protected void ParseJson(string json)
    {
        try { ParseFrom(JsonNode.Parse(json ?? "")); }
        catch (Exception) { /* 非法 JSON 视为空图集 */ }
    }

    /// <summary>解析静态坐标映射层。子类先调 base 再解析自己的时序层。</summary>
    protected virtual void ParseFrom(JsonNode root)
    {
        if (root is not JsonObject obj) return;
        if (obj["file"] is JsonValue fv && fv.TryGetValue<string>(out var f)) File = f;

        // frames(字典) = 普通 UI 图集；res(字典) = Movie 图集的子图矩形（结构与 frames 等价）；
        // frames(数组) = 原始传奇模型独立图集（自定义格式，带 offX/offY + 虚拟画布）。
        if (obj["frames"] is JsonObject framesDict)
            ParseFrameDict(framesDict);
        else if (obj["frames"] is JsonArray framesArr)
            ParseModelFrames(framesArr);
        else if (obj["res"] is JsonObject resDict)
            ParseFrameDict(resDict);
    }

    private void ParseFrameDict(JsonObject frames)
    {
        foreach (var kv in frames)
        {
            if (kv.Value is not JsonObject o) continue;
            var (x, y, w, h) = ReadRect(o);
            Defs.Add(new FrameDef
            {
                Name = kv.Key,
                SourceRect = new Rectangle(x, y, w, h),
                Size = new Vector2(GetInt(o, "sourceW", w), GetInt(o, "sourceH", h))
            });
        }
    }

    private void ParseModelFrames(JsonArray frames)
    {
        int i = 0;
        foreach (var node in frames)
        {
            if (node is JsonObject o)
            {
                var (x, y, w, h) = ReadRect(o);
                int sw = GetInt(o, "sourceW", w), sh = GetInt(o, "sourceH", h);
                if (SourceW == 0 && sw > 0 && sh > 0) { SourceW = sw; SourceH = sh; }
                Defs.Add(new FrameDef
                {
                    Name = i.ToString(),
                    SourceRect = new Rectangle(x, y, w, h),
                    Size = new Vector2(w, h),
                    OffX = GetInt(o, "offX"),
                    OffY = GetInt(o, "offY")
                });
            }
            i++;
        }
    }

    /// <summary>绑定整页贴图，构建每帧 KSpriteInfo（pivot 默认左上、不旋转，与框架 SpriteSheetLoader 一致）。</summary>
    public virtual void Build(Texture2D pageTexture)
    {
        Texture = pageTexture;
        var frames = new Dictionary<string, KSpriteInfo>(StringComparer.OrdinalIgnoreCase);
        var anchors = new Dictionary<string, Vector2>(StringComparer.OrdinalIgnoreCase);
        foreach (var d in Defs)
        {
            frames[d.Name] = new KSpriteInfo(d.Name, pageTexture, d.SourceRect, d.Size, Vector2.Zero, false);
            anchors[d.Name] = new Vector2(d.OffX, d.OffY);
        }
        Frames = frames;
        Anchors = anchors;
        _frameNames = new List<string>(frames.Keys);
        _frameNames.Sort(StringComparer.Ordinal);
        OnBuilt();
    }

    /// <summary>Build 之后的钩子：MovieClip 图集在此用真实子图尺寸重算统一注册点。</summary>
    protected virtual void OnBuilt() { }

    /// <summary>按帧序取帧名：普通图集按帧名字典序（模型帧 "0","1"... 正好即播放顺序）。</summary>
    public virtual bool TryGetFrameName(int frameIndex, out string frameName)
    {
        frameName = null;
        if (Frames == null || Frames.Count == 0) return false;
        if (_frameNames == null)
        {
            _frameNames = new List<string>(Frames.Keys);
            _frameNames.Sort(StringComparer.Ordinal);
        }
        frameName = _frameNames[Math.Abs(frameIndex) % _frameNames.Count];
        return true;
    }

    protected static (int x, int y, int w, int h) ReadRect(JsonObject o)
        => (GetInt(o, "x"), GetInt(o, "y"), GetInt(o, "w"), GetInt(o, "h"));

    protected static int GetInt(JsonObject o, string k, int fallback = 0)
        => o[k] is JsonValue v && v.TryGetValue<int>(out var i) ? i : fallback;
}

// MovieClip 专用图集子类见 EgretMovieClipAtlas.cs（单独文件）。
