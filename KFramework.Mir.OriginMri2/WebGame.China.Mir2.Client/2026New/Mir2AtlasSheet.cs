using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using KFramework.MonoGame;
using KFramework.MonoGameExtend;

// MirEngine 全局 using 也导出了 Rectangle / Vector2；这里用别名消歧，明确使用 KFramework.MonoGame 版本。
using Rectangle = KFramework.MonoGame.Rectangle;
using Vector2 = KFramework.MonoGame.Vector2;

/// <summary>
/// 原始传奇（移动版）精灵表（Egret 图集）解析：一张图集 = 一个图集 json（帧信息）+ 同目录 png（整页贴图）。
/// 解析后每帧产出框架标准的 KSpriteInfo（整页贴图 + 源矩形 + 原始尺寸），可直接交给 SpriteBatch 绘制。
/// 构建方式对齐 KFramework.MonoGameExtend.KTexturePacker.SpriteSheetLoader：pivot 默认左上、不旋转。
/// 这是网页版 Egret 图集，与桌面端 WIL/WIX（MLibrary/MImage）是两套体系。
///
/// 支持三种线上格式：
///   1) UI/通用图集 -sheet.json：{"file":"x.png","frames":{"帧名":{x,y,w,h,sourceW,sourceH}}}   ← frames 是字典
///   2) 模型独立图集 model/{id}/{srcId}.json：
///      {"count":4,"file":"4171010.png","frames":[{x,y,w,h,offX,offY,sourceW,sourceH}, ...]}   ← frames 是数组
///      帧名按数组序号 "0","1"...；offX/offY 是该帧在虚拟画布 sourceW×sourceH 里的注册点偏移，
///      绘制时须按画布中心还原（见 Anchors / SourceW / SourceH），否则人物会飘出格子。
///   3) Egret MovieClip：无 "frames"，用 "res"（子图矩形）+ "mc"（时间线帧序列），PlayOrder 取首条时间线。
/// </summary>
public sealed class Mir2AtlasSheet
{
    public string File = ""; // 同目录整页贴图文件名（png）

    /// <summary>已加载的整页贴图（Build 后可用）。</summary>
    public Texture2D Texture { get; private set; }

    /// <summary>帧名 → KSpriteInfo（整页贴图上的子图，Build 后可用）。</summary>
    public Dictionary<string, KSpriteInfo> Frames { get; private set; }

    /// <summary>MovieClip 播放顺序（取 mc 首个时间线 frames 的 res 引用序列）；普通精灵图集为空。</summary>
    public List<string> PlayOrder { get; private set; }

    /// <summary>白鹭 MovieClip 工厂（Egret Movie 图集解析结果）；普通图集为 null。</summary>
    public EgretMovieClipDataFactory MovieFactory { get; private set; }

    /// <summary>实际可播放帧数：必须与 TryGetFrameName 的取帧口径一致。
    /// MovieClip 图集的时间线长度往往不等于子图数（同一子图会被重复引用做定格），
    /// 若用 Frames.Count 当帧数，FrameIndex 会被 % 时间线长度绕到中间帧上，尸体可能停在站立帧。</summary>
    public int PlayableFrameCount => PlayOrder != null && PlayOrder.Count > 0
        ? PlayOrder.Count
        : (Frames != null ? Frames.Count : 0);

    /// <summary>帧名 → (offX, offY) 注册点偏移。仅模型帧（frames 数组）格式非零，UI 图集全为 0。</summary>
    public Dictionary<string, Vector2> Anchors { get; private set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>模型帧的虚拟画布尺寸（frames 数组元素里的 sourceW/sourceH），用于还原注册点；UI 图集为 0。</summary>
    public int SourceW, SourceH;

    private List<FrameDef> _defs = new();

    /// <summary>按帧名字典序预排序的帧名序列（模型帧名 "0","1"... 正好即播放顺序）。Build 时一次性算好，
    /// 避免 TryGetFrameName / TryGetSprite 每帧 new List + Sort 造成 GC 压力。</summary>
    private List<string> _frameNames;

    private sealed class FrameDef
    {
        public string Name;
        public Rectangle SourceRect;
        public Vector2 Size;       // 原始（未裁剪）尺寸
        public int OffX, OffY;     // 模型帧注册点偏移
    }

    /// <summary>仅解析 JSON（不含贴图）。返回的实例需再调用 Build(整页贴图) 才能得到 Frames。</summary>
    /// <remarks>
    /// 解析结构对齐白鹭 Egret（见 js/game.min.js 的 egret.MovieClipData / MovieClipDataFactory 源码）：
    ///   · 子图矩形层（egret.SpriteSheet / textureData）：普通图集的 frames(字典) 与 Movie 图集的 res(字典)
    ///     都是“整页贴图上的一组命名矩形”，结构等价（res 即 Movie 的 textureData；子图查找见
    ///     egret.MovieClipData.getTextureByResName → textureData[res]）。
    ///   · 时间线层（egret.MovieClipDataFactory）：mc 段把子图按序引用成动画；每帧带 res 引用 +
    ///     相对偏移(x,y)（egret.$getOffsetByFrame）+ 定格时长 duration（fillFramesData 按 duration 展开占位帧）。
    /// 模型图集（frames 为数组）是原始传奇自定义格式（非 Egret 标准），单独处理 offX/offY 注册点。
    /// </remarks>
    public static Mir2AtlasSheet Parse(string json)
    {
        var sheet = new Mir2AtlasSheet();
        if (JsonNode.Parse(json) is not JsonObject obj) return sheet;
        if (obj["file"] is JsonValue fv && fv.TryGetValue<string>(out var f)) sheet.File = f;

        // 子图矩形层（SpriteSheet）：frames(字典) 与 res(字典) 等价（都是命名矩形）；frames(数组) 为模型自定义格式。
        if (obj["frames"] is JsonObject framesDict)
            ParseSheetFrames(sheet, framesDict);          // 普通 UI/通用图集（Egret SpriteSheet 标准）
        else if (obj["frames"] is JsonArray framesArr)
            ParseModelFrames(sheet, framesArr);           // 模型独立图集（自定义格式）
        else if (obj["res"] is JsonObject resDict)
            ParseSheetFrames(sheet, resDict);             // Movie 图集的子图（≡ Egret textureData）

        // 时间线层（MovieClipDataFactory）：mc 段把子图按序引用成动画，与白鹭 MovieClipData 对齐。
        // 注意：frames(字典) + mc 共存的合并图集也能正确解析（普通子图 + Movie 时间线）。
        if (obj["mc"] is JsonObject)
        {
            sheet.MovieFactory = EgretMovieClipDataFactory.Parse(obj);
            sheet.PlayOrder = sheet.MovieFactory.First?.TimelineResIds ?? new List<string>();
        }
        return sheet;
    }

    // 普通 UI 图集 frames(字典) 与 Movie 图集 res(字典) 共用：子图矩形 = {x,y,w,h[,sourceW,sourceH]}。
    // sourceW/sourceH 缺省回退到 w/h（Movie 子图通常只给 x,y,w,h）。
    private static void ParseSheetFrames(Mir2AtlasSheet sheet, JsonObject frames)
    {
        foreach (var kv in frames)
        {
            if (kv.Value is not JsonObject o) continue;
            var (x, y, w, h) = ReadRect(o);
            int sw = GetInt(o, "sourceW", w), sh = GetInt(o, "sourceH", h);
            sheet._defs.Add(new FrameDef
            {
                Name = kv.Key,
                SourceRect = new Rectangle(x, y, w, h),
                Size = new Vector2(sw, sh)
            });
        }
    }

    // 模型独立图集：frames 为数组，每帧自带 offX/offY 注册点 + 虚拟画布 sourceW/sourceH（自定义格式）。
    private static void ParseModelFrames(Mir2AtlasSheet sheet, JsonArray frames)
    {
        int i = 0;
        foreach (var node in frames)
        {
            if (node is JsonObject o)
            {
                var (x, y, w, h) = ReadRect(o);
                int sw = GetInt(o, "sourceW", w), sh = GetInt(o, "sourceH", h);
                if (sheet.SourceW == 0 && sw > 0 && sh > 0) { sheet.SourceW = sw; sheet.SourceH = sh; }
                sheet._defs.Add(new FrameDef
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

    private static (int x, int y, int w, int h) ReadRect(JsonObject o)
        => (GetInt(o, "x"), GetInt(o, "y"), GetInt(o, "w"), GetInt(o, "h"));

    /// <summary>绑定整页贴图，构建每帧的 KSpriteInfo（pivot 默认左上、不旋转，与框架 SpriteSheetLoader 一致）。</summary>
    public void Build(Texture2D pageTexture)
    {
        Texture = pageTexture;
        var frames = new Dictionary<string, KSpriteInfo>(StringComparer.OrdinalIgnoreCase);
        var anchors = new Dictionary<string, Vector2>(StringComparer.OrdinalIgnoreCase);
        foreach (var d in _defs)
        {
            frames[d.Name] = new KSpriteInfo(d.Name, pageTexture, d.SourceRect, d.Size, Vector2.Zero, false);
            anchors[d.Name] = new Vector2(d.OffX, d.OffY);
        }
        Frames = frames;
        Anchors = anchors;
        _frameNames = new List<string>(frames.Keys);
        _frameNames.Sort(StringComparer.Ordinal);
    }

    /// <summary>
    /// 按帧序取帧名：MovieClip 走 PlayOrder 时间线；其余按帧名字典序（模型帧名 "0","1","2"... 正好是播放顺序）。
    /// </summary>
    public bool TryGetFrameName(int frameIndex, out string frameName)
    {
        frameName = null;
        if (Frames == null || Frames.Count == 0) return false;
        int n = Math.Abs(frameIndex);
        if (PlayOrder != null && PlayOrder.Count > 0)
        {
            frameName = PlayOrder[n % PlayOrder.Count];
            return true;
        }
        if (_frameNames == null)
        {
            _frameNames = new List<string>(Frames.Keys);
            _frameNames.Sort(StringComparer.Ordinal);
        }
        frameName = _frameNames[n % _frameNames.Count];
        return true;
    }

    private static int GetInt(JsonObject o, string k, int fallback = 0)
    { 
        return o[k] is JsonValue v && v.TryGetValue<int>(out var i) ? i : fallback;
    }

}
