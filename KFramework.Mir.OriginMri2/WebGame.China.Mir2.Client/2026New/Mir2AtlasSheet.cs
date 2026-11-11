/// <summary>
/// 兼容壳：旧代码里到处用 Mir2AtlasSheet 这个类型名，因此保留它，但实现全部下沉到
/// 2026New/EgretEngine 下的 EgretAtlas（普通图集）/ EgretMovieClipAtlas（MovieClip 图集）。
///
/// 语义拆分：
///   · EgretAtlas          只解析静态坐标映射（frames / res 矩形 + 整页 PNG）
///   · EgretMovieClipAtlas  继承 EgretAtlas，额外带 mc 时序层（帧序列/帧率/事件/标签）
///   · EgretMovieClip       持有图集的时序对象，负责播放序列与统一注册点
///
/// 本类 = EgretMovieClipAtlas 的薄封装，只补回几个历史属性名，不再自己解析 JSON。
/// </summary>
public sealed class Mir2AtlasSheet : EgretMovieClipAtlas
{
    /// <summary>实际可播放帧数：取时间线长度（同一子图会被重复引用做定格），不用 Frames.Count。</summary>
    public int PlayableFrameCount => (PlayOrder != null && PlayOrder.Count > 0)
        ? PlayOrder.Count
        : (Frames != null ? Frames.Count : 0);

    /// <summary>兼容旧调用：解析 JSON（不含贴图），之后仍需 Build(整页贴图)。</summary>
    public static Mir2AtlasSheet Parse(string json)
    {
        var sheet = new Mir2AtlasSheet();
        sheet.ParseJson(json);
        return sheet;
    }
}
