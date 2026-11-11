using System;
using System.Collections.Generic;
using KFramework.MonoGame;

using Rectangle = KFramework.MonoGame.Rectangle;

// 白鹭(Egret) MovieClip 时序对象。
//
// 核心数据 = 两个 JSON 数据类，它们就是唯一数据源，不再复制成另一套运行时对象：
//   EgretAtlasJson        普通图集：静态坐标映射（frames / res 子图矩形）
//   EgretMovieClipJson    MovieClip 图集：mc 时间线（帧序列/帧率/标签/事件）+ res 子图矩形
// 本类只把这两份核心数据与整页贴图绑起来，派生出「播放索引」和「绘制矩形」；
// 帧率、帧序列、x/y、res 矩形一律直接读 Timeline / Json，不搬运字段。

/// <summary>
/// 白鹭 MovieClip 播放对象：把【图集 EgretAtlas】与【MovieClip JSON 核心数据】绑在一起。
/// 必须持有图集才能工作：mc 每帧只有 x/y 偏移和 res 名，子图真实 w/h 在图集里。
/// </summary>
public sealed class EgretMovieClip
{
    /// <summary>本 MovieClip 引入的图集（子图矩形 + 整页贴图）。</summary>
    public EgretAtlas Atlas { get; }

    /// <summary>MovieClip JSON 核心数据（全部时间线 + res 子图矩形）。</summary>
    public EgretMovieClipJson Json { get; }

    /// <summary>当前播放的时间线，直接就是 mc 字典里的那个值。</summary>
    public EgretMovieClipTimelineJson Timeline { get; }

    public string Name { get; }

    /// <summary>帧率，直接取 JSON 的 frameRate（缺省 24）。</summary>
    public int FrameRate => Timeline?.FrameRate > 0 ? Timeline.FrameRate.Value : 24;

    /// <summary>每帧间隔(ms)。</summary>
    public long DelayMs => FrameRate > 0 ? 1000L / FrameRate : 250L;

    /// <summary>JSON 时间线的帧序列（唯一数据源，未展开 duration）。</summary>
    public IReadOnlyList<EgretMovieClipFrameJson> Frames
    {
        get
        {
            var frames = Timeline?.Frames;
            if (frames != null) return frames;
            return Array.Empty<EgretMovieClipFrameJson>();
        }
    }

    /// <summary>展开 duration 后的播放序列 —— 运行时派生索引，不是 JSON 字段副本。</summary>
    public List<string> PlayOrder { get; } = new List<string>();

    /// <summary>未展开的 res 序列 —— 运行时派生索引，不是 JSON 字段副本。</summary>
    public List<string> TimelineResIds { get; } = new List<string>();

    /// <summary>
    /// res 名 → 该帧在【MovieClip 局部坐标系】里的完整绘制矩形 (x,y,w,h)，逻辑像素、未缩放。
    /// 与白鹭一致，直接使用 mc.frames 的原始 x/y（不做 minX/minY 归一化）：
    /// MovieClip 的 (0,0) 就是引擎挂载点，各组资源不同的 x/y 本身就是导出时算好的位置补偿。
    /// 这是需要「帧 x/y + 子图 w/h」才能算出的运行时结果。
    /// </summary>
    public Dictionary<string, Rectangle> Layouts { get; private set; }
        = new Dictionary<string, Rectangle>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 内容包围盒 (0,0,max(x+w),max(y+h))，对应 Egret 的
    /// setWidth(textureWidth + offsetPoint.x) / setHeight(textureHeight + offsetPoint.y)。
    /// </summary>
    public Rectangle Bounds { get; private set; }

    public EgretMovieClip(EgretAtlas atlas, EgretMovieClipJson json,
                          string name, EgretMovieClipTimelineJson timeline)
    {
        Atlas = atlas ?? throw new ArgumentNullException(nameof(atlas));
        Json = json ?? throw new ArgumentNullException(nameof(json));
        Timeline = timeline ?? throw new ArgumentNullException(nameof(timeline));
        Name = name ?? "";

        // duration 是白鹭 fillFramesData 的展开规则：同一 res 重复 Duration 次做定格。
        foreach (var frame in Frames)
        {
            if (frame == null || string.IsNullOrEmpty(frame.Res)) continue;
            TimelineResIds.Add(frame.Res);
            int duration = Math.Max(1, frame.Duration ?? 1);
            for (int i = 0; i < duration; i++) PlayOrder.Add(frame.Res);
        }
    }

    /// <summary>
    /// 用图集子图的真实 w/h + 时间线每帧 x/y 生成每帧绘制矩形。
    ///
    /// 白鹭引擎（js/game.min.js）不做任何「统一注册点」计算：
    ///   MovieClipData.$getOffsetByFrame()  → offsetPoint.setTo(0|frame.x, 0|frame.y)
    ///   MovieClipData.getTextureByFrame()  → SpriteSheet.createTexture(res, x,y,w,h)，尺寸即 res.w/h
    ///   MovieClip.constructFrame()         → 设置 texture + offsetPoint
    ///   MovieClip.$updateRenderNode()      → BitmapNode 用 Math.round(offsetPoint) 作绘制偏移
    ///   MovieClip.$measureContentBounds()  → bounds.setTo(offsetPoint.x, offsetPoint.y, w, h)
    /// 即每帧子图直接画在 MovieClip 局部坐标 (frame.x, frame.y)，不减 minX/minY、不拉伸。
    ///
    /// 对齐是【导出工具】的责任：它裁掉各帧透明边后再用 x/y 把裁掉的部分补回去，
    /// 所以帧高矮不一但 frame.y + h 恒定 = 脚底不动。实测 6 组 carrer*.json：
    ///   1_1/1_2 全帧恒定 348/335；2_1/2_2/3_1/3_2 仅 ±1px 的原始取整抖动。
    /// 原版 JS 里 6 组 MovieClip 都挂在同一个 (30,-60)，没有任何按职业的补偿，
    /// 而各组 minX 从 16 到 121 不等 —— 若在这里减去 minX/minY，
    /// 不同职业会在同一槽位横向错位最多 105px，且整体相对原版左移/上移。
    /// </summary>
    public void RebuildLayouts(EgretAtlas atlas)
    {
        Layouts.Clear();
        if (Frames.Count == 0) return;

        var sizes = new Dictionary<string, (int w, int h)>(StringComparer.OrdinalIgnoreCase);
        foreach (var frame in Frames)
        {
            if (frame == null || string.IsNullOrEmpty(frame.Res)) continue;
            if (atlas?.Frames != null && atlas.Frames.TryGetValue(frame.Res, out var sprite))
                sizes[frame.Res] = (sprite.SourceRectangle.Width, sprite.SourceRectangle.Height);
            // 贴图还没绑定时，直接取 JSON 核心数据里的 res 矩形先算一版。
            else if (Json.Res != null && Json.Res.TryGetValue(frame.Res, out var r) && r != null)
                sizes[frame.Res] = (r.W, r.H);
        }

        int maxR = 0, maxB = 0;
        foreach (var frame in Frames)
        {
            if (frame == null || string.IsNullOrEmpty(frame.Res)) continue;
            if (!sizes.TryGetValue(frame.Res, out var wh) || wh.w <= 0 || wh.h <= 0) continue;
            // 原样保留 mc.frames 的 x/y：MovieClip 局部原点就是引擎挂载点。
            Layouts[frame.Res] = new Rectangle(frame.X, frame.Y, wh.w, wh.h);
            maxR = Math.Max(maxR, frame.X + wh.w);
            maxB = Math.Max(maxB, frame.Y + wh.h);
        }
        if (Layouts.Count == 0) return;   // 图集还没绑定贴图，等 Build 后 OnBuilt 再算
        Bounds = new Rectangle(0, 0, maxR, maxB);
    }
}
