using System;
using System.Text.Json;

/// <summary>白鹭 JSON 文件的类型：决定该用哪个 JSON 数据类去反序列化。</summary>
public enum EgretJsonKind
{
    /// <summary>不是白鹭图集 JSON（普通配置、其它资源清单、空内容、非法 JSON 等）。</summary>
    Other = 0,

    /// <summary>普通图集 JSON → <see cref="EgretAtlasJson"/>：只有静态坐标映射（frames / res）。</summary>
    Atlas,

    /// <summary>MovieClip 图集 JSON → <see cref="EgretMovieClipJson"/>：mc 时序层 + res 子图矩形。</summary>
    MovieClip,
}

/// <summary>
/// 白鹭 JSON 类型判别器：只按根节点字段判断该用哪个 JSON 数据类，不做反序列化、不绑定贴图。
///
/// 判定依据（真实资源的三种形态）：
///   · MovieClip  根有非空的 "mc" 对象 → 帧序列 / 帧率 / 标签 / 事件都在里面
///   · 普通图集   根有 "frames" 对象(UI 图集) 或 "frames" 数组(模型图集) 或 "res" 对象
///   · 其它       以上都不满足
/// mc 优先于 res —— MovieClip 图集也带 res，但那是子图矩形，不是时间线。
///
/// 只用 JsonDocument / JsonElement 这些 System.Text.Json 内建 API（不需要反射元数据），
/// 因此在 WASM AOT / 裁剪环境下同样可用。
/// </summary>
public static class EgretJsonDetector
{
    private static readonly JsonDocumentOptions ParseOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>从 JSON 文本判别类型；空内容或非法 JSON 一律为 <see cref="EgretJsonKind.Other"/>。</summary>
    public static EgretJsonKind Detect(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return EgretJsonKind.Other;
        try
        {
            using var doc = JsonDocument.Parse(json, ParseOptions);
            return Detect(doc.RootElement);
        }
        catch (JsonException) { return EgretJsonKind.Other; }
    }

    /// <summary>已经解析过根节点时用这个重载，避免重复 Parse。</summary>
    public static EgretJsonKind Detect(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object) return EgretJsonKind.Other;

        // 1) mc 段是 MovieClip 独有；必须是非空对象（空 mc 不算一条可播放的时间线）。
        if (root.TryGetProperty("mc", out var mc) && mc.ValueKind == JsonValueKind.Object)
        {
            var timelines = mc.EnumerateObject();
            if (timelines.MoveNext()) return EgretJsonKind.MovieClip;
        }

        // 2) 普通图集：frames 字典 / frames 数组 / res 字典，任一即可。
        if (root.TryGetProperty("frames", out var frames)
            && (frames.ValueKind == JsonValueKind.Object || frames.ValueKind == JsonValueKind.Array))
            return EgretJsonKind.Atlas;

        if (root.TryGetProperty("res", out var res) && res.ValueKind == JsonValueKind.Object)
            return EgretJsonKind.Atlas;

        return EgretJsonKind.Other;
    }

    public static bool IsAtlas(string json) => Detect(json) == EgretJsonKind.Atlas;

    public static bool IsMovieClip(string json) => Detect(json) == EgretJsonKind.MovieClip;
}
