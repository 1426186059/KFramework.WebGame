using System;
using System.Collections.Generic;

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

    /// <summary>MovieClip JSON 核心数据（全部时间线 + res 子图矩形），唯一数据源。</summary>
    public EgretMovieClipJson MovieJson { get; private set; }

    /// <summary>时间线播放对象：把本图集与 JSON 核心数据绑起来。</summary>
    public EgretMovieClip MovieClip { get; private set; }

    /// <summary>兼容旧调用：首个时间线未展开的 res 序列。</summary>
    public List<string> PlayOrder => MovieClip?.TimelineResIds;

    protected override void OnBuilt() => MovieClip?.RebuildLayouts(this);

    protected override void ParseJson(string json)
    {
        base.ParseJson(json);   // EgretAtlasJson：静态坐标映射

        // 时序层 JSON 就是核心数据；普通图集没有 mc，TryDeserialize 返回 null。
        MovieJson = EgretMovieClipJson.TryDeserialize(json);
        if (MovieJson == null) return;

        // 与旧逻辑一致：取 mc 里的首条时间线。
        foreach (var kv in MovieJson.Mc)
        {
            if (kv.Value == null) continue;
            MovieClip = new EgretMovieClip(this, MovieJson, kv.Key, kv.Value);
            break;
        }
        // Parse 后贴图还没绑定，先用 JSON 里的 res 矩形算一版绘制矩形；
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
