using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

/// <summary>
/// MovieClip 专用图集（<see cref="EgretAtlas"/> 的子类，单独文件）。
///
/// 与普通图集最核心的区别在于 JSON 配置文件的语义和结构完全不同：
///   · 普通图集 (EgretAtlas)：JSON 只负责「静态坐标映射」——frames 字典/数组给出子图在整页 PNG 上的矩形。
///   · MovieClip 图集 (本类)：除了同一张 PNG 大图和 res 子图矩形外，JSON 还带 "mc" 段，
///     里面是动画时序逻辑（帧序列、帧率、事件、标签、每帧注册点偏移 x/y、duration）。
/// 因此这里继承 EgretAtlas 复用「静态坐标映射」，再叠加时序层，而不是把它塞进普通图集类。
///
/// 时序逻辑本身封装在 <see cref="EgretMovieClip"/>；本类把它与图集绑定后对外暴露。
/// </summary>
public class EgretMovieClipAtlas : EgretAtlas
{
    public override bool IsMovieClip => true;

    /// <summary>时间线播放对象：持有本图集，因此能同时看到子图 w/h 与帧 x/y 来算统一注册点。</summary>
    public EgretMovieClip MovieClip { get; private set; }

    /// <summary>兼容旧调用：首个时间线未展开的 res 序列。</summary>
    public List<string> PlayOrder => MovieClip?.TimelineResIds;

    /// <summary>兼容旧调用：底层 mc 解析工厂。</summary>
    public EgretMovieClipDataFactory MovieFactory { get; private set; }

    protected override void OnBuilt() => MovieClip?.RebuildLayouts(this);

    protected override void ParseFrom(JsonNode root)
    {
        base.ParseFrom(root);   // res / frames 静态子图矩形
        if (root is not JsonObject obj || obj["mc"] is not JsonObject) return;

        MovieFactory = EgretMovieClipDataFactory.Parse(obj);
        var data = MovieFactory.First;
        MovieClip = data != null ? new EgretMovieClip(this, data) : null;
        // Parse 后 Frames 还没 Build，先用 mc 里同步解析出的 res 矩形算一版注册点，
        // Build 绑定贴图后 OnBuilt 会用真实子图尺寸再校准一次。
        MovieClip?.RebuildLayouts(this);
    }

    public override bool TryGetFrameName(int frameIndex, out string frameName)
    {
        var order = MovieClip?.TimelineResIds;
        if (order != null && order.Count > 0)
        {
            frameName = order[Math.Abs(frameIndex) % order.Count];
            return true;
        }
        return base.TryGetFrameName(frameIndex, out frameName);
    }
}
