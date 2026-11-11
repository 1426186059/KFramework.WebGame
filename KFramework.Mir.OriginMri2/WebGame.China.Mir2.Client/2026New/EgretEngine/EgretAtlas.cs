using System;
using System.Collections.Generic;
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
    /// <summary>
    /// 本图集的【核心数据】：反序列化后的普通图集 JSON。
    /// 子图矩形、偏移、虚拟画布尺寸全部直接读它，运行时不再复制一份字段副本。
    /// </summary>
    public EgretAtlasJson Json { get; private set; }

    /// <summary>同目录整页贴图文件名（png），直接来自 JSON 的 file。</summary>
    public string File => Json?.File ?? "";

    /// <summary>已加载的整页贴图（Build 后可用）。</summary>
    public Texture2D Texture { get; protected set; }

    /// <summary>帧名 → KSpriteInfo：绑定整页贴图后的产物，不是 JSON 数据副本。</summary>
    public Dictionary<string, KSpriteInfo> Frames { get; protected set; }

    /// <summary>帧名 → (offX, offY)：模型帧名的运行时索引，值直接取自 JSON。</summary>
    public Dictionary<string, Vector2> Anchors { get; protected set; }
        = new Dictionary<string, Vector2>(StringComparer.OrdinalIgnoreCase);

    /// <summary>模型帧的虚拟画布尺寸，直接取 JSON 首个有效帧的 sourceW/sourceH；UI 图集为 0。</summary>
    public int SourceW => CanvasSize.W;
    public int SourceH => CanvasSize.H;

    private (int W, int H) CanvasSize
    {
        get
        {
            var ordered = Json?.OrderedFrames;
            if (ordered != null)
                foreach (var r in ordered)
                    if (r != null && r.SourceW.HasValue && r.SourceH.HasValue)
                        return (r.SourceW.Value, r.SourceH.Value);
            return (0, 0);
        }
    }

    /// <summary>本图集是否含 MovieClip 时序层。</summary>
    public virtual bool IsMovieClip => false;

    /// <summary>按帧名字典序预排序的帧名序列（模型帧名 "0","1"... 正好即播放顺序）。</summary>
    private List<string> _frameNames;

    /// <summary>
    /// 按 JSON 语义分流创建：带 "mc" 时间线 → <see cref="EgretMovieClipAtlas"/>；否则 → 普通 <see cref="EgretAtlas"/>。
    /// 判定由 <see cref="EgretMovieClipJson"/> 反序列化后是否有 mc 完成，不手写 JSON 探测。
    /// </summary>
    public static EgretAtlas Create(string json)
    {
        EgretAtlas atlas = EgretMovieClipJson.IsMovieClip(json)
            ? new EgretMovieClipAtlas()
            : new EgretAtlas();
        atlas.ParseJson(json);
        return atlas;
    }

    /// <summary>
    /// 供派生类（兼容旧类型 Mir2AtlasSheet）用自身类型解析 JSON。
    /// 只保存 DTO 引用，不做任何字段搬运；MovieClip 子类 override 后再叠加时序层。
    /// </summary>
    protected virtual void ParseJson(string json) => Json = EgretAtlasJson.Deserialize(json);

    /// <summary>
    /// 绑定整页贴图，从 JSON 核心数据直接生成每帧 KSpriteInfo（贴图绑定产物，非数据副本）。
    /// 优先级：frames 字典（UI 图集）→ frames 数组（模型图集）→ res 字典（MovieClip 图集）。
    /// </summary>
    public virtual void Build(Texture2D pageTexture)
    {
        Texture = pageTexture;
        var frames = new Dictionary<string, KSpriteInfo>(StringComparer.OrdinalIgnoreCase);
        var anchors = new Dictionary<string, Vector2>(StringComparer.OrdinalIgnoreCase);

        if (Json?.NamedFrames != null)
            foreach (var kv in Json.NamedFrames)
                AddFrame(frames, anchors, kv.Key, kv.Value, true);
        else if (Json?.OrderedFrames != null)
            for (int i = 0; i < Json.OrderedFrames.Count; i++)
                AddFrame(frames, anchors, i.ToString(), Json.OrderedFrames[i], false);
        else if (Json?.Res != null)
            foreach (var kv in Json.Res)
                AddFrame(frames, anchors, kv.Key, kv.Value, true);

        Frames = frames;
        Anchors = anchors;
        _frameNames = new List<string>(frames.Keys);
        _frameNames.Sort(StringComparer.Ordinal);
        OnBuilt();
    }

    private void AddFrame(Dictionary<string, KSpriteInfo> frames, Dictionary<string, Vector2> anchors,
                          string name, EgretRectJson r, bool named)
    {
        if (r == null) return;
        var size = named
            ? new Vector2(r.SourceW ?? r.W, r.SourceH ?? r.H)   // UI 图集：裁剪前尺寸
            : new Vector2(r.W, r.H);                              // 模型帧：子图自身尺寸
        frames[name] = new KSpriteInfo(name, Texture, new Rectangle(r.X, r.Y, r.W, r.H),
                                       size, Vector2.Zero, false);
        anchors[name] = named ? Vector2.Zero : new Vector2(r.OffX ?? 0, r.OffY ?? 0);
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

}

// MovieClip 专用图集子类见 EgretMovieClipAtlas.cs（单独文件）。
