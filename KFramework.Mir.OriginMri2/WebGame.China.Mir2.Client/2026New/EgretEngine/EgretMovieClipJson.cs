using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

// =====================================================================================
// 白鹭【MovieClip 图集】的 JSON 数据类：只负责序列化 / 反序列化，不参与任何运行时绘制。
//
// 与普通图集最核心的区别：JSON 里除了整页 PNG 和 res 子图矩形，还带 "mc" 时间线：
//   {"mc":{
//       "动作名":{
//           "frameRate":8,
//           "labels":[{"name":"1","frame":1,"end":4}],
//           "events":[{"name":"...","frame":1}],
//           "frames":[{"res":"0001","x":20,"y":103,"duration":2}]
//       }
//    },
//    "res":{"0001":{"x":..,"y":..,"w":..,"h":..}}}
//
// mc.frames 虽也叫 frames，元素却不是子图矩形，而是「引用哪个 res + 该帧显示坐标 + 停留帧数」；
// 所以它绝不能和 EgretAtlasJson.Frames（子图矩形）共用类型。这也是两个类必须分开的原因。
// 全部字段由 [JsonPropertyName] 绑定，取值交给 System.Text.Json。
// =====================================================================================

/// <summary>MovieClip 时间线里的一帧：引用一个 res 子图，并给出它相对 MovieClip 原点的显示坐标。</summary>
public sealed class EgretMovieClipFrameJson
{
    [JsonPropertyName("res")] public string Res { get; set; }

    /// <summary>导出时裁掉透明边后的补偿偏移，原样参与绘制（白鹭 offsetPoint）。</summary>
    [JsonPropertyName("x")] public int X { get; set; }
    [JsonPropertyName("y")] public int Y { get; set; }

    /// <summary>停留帧数，缺省 1；白鹭会把它展开成多个占位帧。</summary>
    [JsonPropertyName("duration")] public int? Duration { get; set; }
}

/// <summary>时间线上的命名区间标签。</summary>
public sealed class EgretMovieClipLabelJson
{
    [JsonPropertyName("name")] public string Name { get; set; }
    [JsonPropertyName("frame")] public int Frame { get; set; }
    [JsonPropertyName("end")] public int? End { get; set; }
}

/// <summary>时间线上的帧事件。</summary>
public sealed class EgretMovieClipEventJson
{
    [JsonPropertyName("name")] public string Name { get; set; }
    [JsonPropertyName("frame")] public int Frame { get; set; }
}

/// <summary>一条 MovieClip 时间线（mc 字典的一个值）。</summary>
public sealed class EgretMovieClipTimelineJson
{
    /// <summary>帧率，白鹭缺省 24。</summary>
    [JsonPropertyName("frameRate")] public int? FrameRate { get; set; }

    [JsonPropertyName("labels")] public List<EgretMovieClipLabelJson> Labels { get; set; }
    [JsonPropertyName("events")] public List<EgretMovieClipEventJson> Events { get; set; }

    /// <summary>帧序列；注意元素不是子图矩形，而是帧引用 + 显示坐标。</summary>
    [JsonPropertyName("frames")] public List<EgretMovieClipFrameJson> Frames { get; set; }
}

/// <summary>
/// 白鹭 MovieClip 图集 JSON 根对象：res 是静态坐标映射，mc 是动画时序。
/// 普通图集 JSON 没有 "mc"，<see cref="TryDeserialize"/> 会返回 null，据此分流到 EgretAtlas。
/// </summary>
public sealed class EgretMovieClipJson
{
    /// <summary>同目录整页 PNG 文件名（MovieClip 资源通常不写，由 JSON 同名推断）。</summary>
    [JsonPropertyName("file")] public string File { get; set; }

    /// <summary>动作名 → 时间线。</summary>
    [JsonPropertyName("mc")] public Dictionary<string, EgretMovieClipTimelineJson> Mc { get; set; }

    /// <summary>子图名 → 整页 PNG 上的矩形。</summary>
    [JsonPropertyName("res")] public Dictionary<string, EgretRectJson> Res { get; set; }

    public bool HasTimeline => Mc != null && Mc.Count > 0;

    /// <summary>反序列化；不含 "mc" 时间线时返回 null（即这不是 MovieClip 图集）。</summary>
    public static EgretMovieClipJson TryDeserialize(string json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        try
        {
            // AOT：传入源生成上下文产出的 JsonTypeInfo，不走反射式 JsonSerializer。
            var dto = KFramework.MonoGame.JsonTool.FromJson(json, AppJsonContext.Default.EgretMovieClipJson);
            return dto != null && dto.HasTimeline ? dto : null;
        }
        catch (JsonException) { return null; }
    }

    /// <summary>类型判定统一交给 EgretJsonDetector（只探测根字段，不做完整反序列化）。</summary>
    public static bool IsMovieClip(string json) => EgretJsonDetector.IsMovieClip(json);

    public string Serialize() =>
        KFramework.MonoGame.JsonTool.ToJson(this, AppJsonContext.Default.EgretMovieClipJson);
}
