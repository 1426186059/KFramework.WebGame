using System;
using System.Collections.Generic;
using KFramework.MonoGame;
using KFramework.MonoGameExtend;

// MirEngine 全局 using 也导出了 Rectangle / Vector2；这里用别名消歧，明确使用 KFramework.MonoGame 版本。
using Rectangle = KFramework.MonoGame.Rectangle;
using Vector2 = KFramework.MonoGame.Vector2;

/// <summary>
/// 白鹭普通图集（egret.SpriteSheet / textureData）：只负责「静态坐标映射」——
/// frames(字典) 或模型 frames(数组) 给出整页 PNG 上每个子图的 {x,y,w,h}，
/// 不含帧序列、帧率、事件、标签等任何动画时序信息。
///
/// 它是【被组合】的部件，不是基类：MovieClip 时序层由 Mir2AtlasSheet 以组合方式
/// 叠加 EgretMovieClipJson + EgretMovieClip，不存在 MovieClip 专用图集子类。
/// </summary>
public sealed class EgretAtlas
{
    /// <summary>
    /// 本图集的【核心数据】：反序列化后的普通图集 JSON。
    /// 子图矩形、偏移、虚拟画布尺寸全部直接读它，运行时不再复制一份字段副本。
    /// </summary>
    public EgretAtlasJson Json { get; private set; }

    /// <summary>同目录整页贴图文件名（png），直接来自 JSON 的 file。</summary>
    public string File => Json?.File ?? "";

    /// <summary>已加载的整页贴图（Build 后可用）。</summary>
    public Texture2D Texture { get; private set; }

    /// <summary>帧名 → KSpriteInfo：绑定整页贴图后的产物，不是 JSON 数据副本。</summary>
    public Dictionary<string, KSpriteInfo> Frames { get; private set; }

    /// <summary>帧名 → (offX, offY)：模型帧名的运行时索引，值直接取自 JSON。</summary>
    public Dictionary<string, Vector2> Anchors { get; private set; }
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

    /// <summary>按帧名字典序预排序的帧名序列（模型帧名 "0","1"... 正好即播放顺序）。</summary>
    private List<string> _frameNames;

    /// <summary>解析并保存 JSON 核心数据；整页贴图需再调 Build。</summary>
    public void ParseJson(string json) => Json = EgretAtlasJson.Deserialize(json);

    /// <summary>
    /// MovieClip 图集的 Atlas 完全由 movieJson 提供：file + res 子图矩形即足够，
    /// 不必再对整份含 "mc" 的 JSON 走一遍 EgretAtlasJson。这是 Mir2AtlasSheet 组合 MovieClip 时的入口。
    /// </summary>
    public void UseMovieClip(EgretMovieClipJson mc)
    {
        Json = new EgretAtlasJson { File = mc.File, Res = mc.Res };
    }

    /// <summary>
    /// 绑定整页贴图，从 JSON 核心数据直接生成每帧 KSpriteInfo（贴图绑定产物，非数据副本）。
    /// 优先级：frames 字典（UI 图集）→ frames 数组（模型图集）→ res 字典（MovieClip 图集）。
    /// </summary>
    public void Build(Texture2D pageTexture)
    {
        Texture = pageTexture;
        var frames = new Dictionary<string, KSpriteInfo>(StringComparer.OrdinalIgnoreCase);
        var anchors = new Dictionary<string, Vector2>(StringComparer.OrdinalIgnoreCase);

        if (Json?.NamedFrames != null)
        {
            foreach (var kv in Json.NamedFrames)
                AddFrame(frames, anchors, kv.Key, kv.Value, true);
            _frameNames = new List<string>(frames.Keys);
            SortFrameNames(_frameNames); // 命名帧无固有播放顺序，排序仅为稳定取帧
        }
        else if (Json?.OrderedFrames != null)
        {
            // 模型帧图集：frames 数组天然即播放顺序，按数组下标命名并【直接保序】，
            // 不依赖字符串排序（否则 >10 帧时字典序会把 "10"/"11" 排到 "2" 之前而错乱）。
            _frameNames = new List<string>();
            for (int i = 0; i < Json.OrderedFrames.Count; i++)
            {
                AddFrame(frames, anchors, i.ToString(), Json.OrderedFrames[i], false);
                _frameNames.Add(i.ToString());
            }
        }
        else if (Json?.Res != null)
        {
            foreach (var kv in Json.Res)
                AddFrame(frames, anchors, kv.Key, kv.Value, true);
            _frameNames = new List<string>(frames.Keys);
            SortFrameNames(_frameNames);
        }

        Frames = frames;
        Anchors = anchors;
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

    /// <summary>帧名排序：若全部可解析为整数（模型帧 "0","1"...）则按数值顺序，否则按字典序（UI 图集命名帧）。
    /// 修复原先一律字典序导致 "10"/"11" 排在 "2" 之前、>10 帧动画顺序错乱的问题。</summary>
    private static void SortFrameNames(List<string> names)
    {
        if (names == null || names.Count == 0) return;
        bool allInt = true;
        foreach (var n in names) { if (!int.TryParse(n, out _)) { allInt = false; break; } }
        if (allInt) names.Sort((a, b) => int.Parse(a).CompareTo(int.Parse(b)));
        else names.Sort(StringComparer.Ordinal);
    }

    /// <summary>按帧序取帧名：OrderedFrames（模型帧）已是数组下标顺序；NamedFrames/Res 按字典序（无固有播放顺序）。</summary>
    public bool TryGetFrameName(int frameIndex, out string frameName)
    {
        frameName = null;
        if (Frames == null || Frames.Count == 0) return false;
        if (_frameNames == null)
        {
            _frameNames = new List<string>(Frames.Keys);
            SortFrameNames(_frameNames);
        }
        frameName = _frameNames[Math.Abs(frameIndex) % _frameNames.Count];
        return true;
    }
}
