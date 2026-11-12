using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

// =====================================================================================
// 白鹭【普通图集】的 JSON 数据类：只负责序列化 / 反序列化，不参与任何运行时绘制。
//
// 真实资源里「静态坐标映射」有两种形态，都归普通图集：
//   · UI 图集   {"file":"x.png","frames":{"名字":{"x":..,"y":..,"w":..,"h":..,
//                                              "offX":..,"offY":..,"sourceW":..,"sourceH":..}}}
//   · 模型图集  {"count":4,"file":"x.png","frames":[{"x":..,"y":..,"w":..,"h":..,
//                                                   "offX":..,"offY":..,"sourceW":..,"sourceH":..}]}
//   · MovieClip 图集的 "res" 段也是同构的字典，由 EgretMovieClipJson 单独描述时序层。
//
// 字段全部由 [JsonPropertyName] 绑定，取值交给 System.Text.Json，不手写 JsonValue/TryGetValue。
// =====================================================================================

/// <summary>图集里一个子图的矩形 / 裁剪信息（frames 元素与 res 元素共用）。</summary>
public sealed class EgretRectJson
{
    [JsonPropertyName("x")] public int X { get; set; }
    [JsonPropertyName("y")] public int Y { get; set; }
    [JsonPropertyName("w")] public int W { get; set; }
    [JsonPropertyName("h")] public int H { get; set; }

    /// <summary>子图相对原始画布的偏移，仅模型图集(frames 数组)与裁剪过的 UI 图集有。</summary>
    [JsonPropertyName("offX")] public int? OffX { get; set; }
    [JsonPropertyName("offY")] public int? OffY { get; set; }

    /// <summary>裁剪前的原始画布尺寸；缺省即等于 w/h。</summary>
    [JsonPropertyName("sourceW")] public int? SourceW { get; set; }
    [JsonPropertyName("sourceH")] public int? SourceH { get; set; }
}

/// <summary>
/// 白鹭普通图集 JSON 根对象。只描述「静态坐标映射」，没有任何帧序列 / 帧率 / 标签 / 事件。
/// 带 "mc" 时间线的 JSON 由 <see cref="EgretMovieClipJson"/> 描述，两者语义不同，不合并成一个类。
/// </summary>
public sealed class EgretAtlasJson
{
    /// <summary>同目录整页 PNG 文件名。</summary>
    [JsonPropertyName("file")] 
    public string File { get; set; }

    /// <summary>模型图集自带的帧数提示，运行时不依赖它。</summary>
    [JsonPropertyName("count")] 
    public int? Count { get; set; }

    /// <summary>
    /// frames 段：白鹭导出器会输出「按名字的字典」或「按帧序的数组」两种形态，
    /// 因此保留 JsonElement 原样，由下面两个方法按形态反序列化成强类型集合。
    /// 序列化时原样写回，不会把数组变成字典（或反过来）。
    /// </summary>
    [JsonPropertyName("frames")] 
    public JsonElement? Frames { get; set; }

    /// <summary>MovieClip 图集的 res 段（结构与 UI 图集的 frames 字典等价）。</summary>
    [JsonPropertyName("res")] 
    public Dictionary<string, EgretRectJson> Res { get; set; }

    /// <summary>frames 为字典形态时的强类型视图（反序列化时一次解析，之后运行时直接读）。</summary>
    [JsonIgnore] 
    public Dictionary<string, EgretRectJson> NamedFrames { get; private set; }

    /// <summary>frames 为数组形态时的强类型视图（反序列化时一次解析，之后运行时直接读）。</summary>
    [JsonIgnore] 
    public List<EgretRectJson> OrderedFrames { get; private set; }


    public static EgretAtlasJson Deserialize(string json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        try
        {
            // AOT：传入源生成上下文产出的 JsonTypeInfo，不走反射式 JsonSerializer。
            var dto = KFramework.MonoGame.JsonTool.FromJson(json, AppJsonContext.Default.EgretAtlasJson);
            dto?.ResolveFrames();
            return dto;
        }
        catch (JsonException) { return null; }
    }

    public string Serialize() =>
        KFramework.MonoGame.JsonTool.ToJson(this, AppJsonContext.Default.EgretAtlasJson);

    /// <summary>把 frames 的两种形态解析成强类型视图，只做一次；序列化仍走原始 JsonElement。</summary>
    private void ResolveFrames()
    {
        if (!Frames.HasValue) return;
        string raw = Frames.Value.GetRawText();
        if (Frames.Value.ValueKind == JsonValueKind.Object)
            NamedFrames = KFramework.MonoGame.JsonTool.FromJson(
                raw, AppJsonContext.Default.DictionaryStringEgretRectJson);
        else if (Frames.Value.ValueKind == JsonValueKind.Array)
            OrderedFrames = KFramework.MonoGame.JsonTool.FromJson(
                raw, AppJsonContext.Default.ListEgretRectJson);
    }
}
