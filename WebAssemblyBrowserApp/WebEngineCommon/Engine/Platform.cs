#nullable enable
using System;
using System.Runtime.InteropServices.JavaScript;

namespace WebAssemblyBrowserApp.Engine;

/// <summary>
/// 浏览器平台能力（JS 薄层）：窗口尺寸、设备像素比、性能计时、UA、语言、标题。
/// 所有访问都带 try-catch 兜底——JS 端缺失对应导出时返回默认值，绝不抛异常。
/// </summary>
public static partial class Platform
{
    [JSImport("platform.innerWidth", "main.js")]
    internal static partial float InnerWidthRaw();

    [JSImport("platform.innerHeight", "main.js")]
    internal static partial float InnerHeightRaw();

    [JSImport("platform.devicePixelRatio", "main.js")]
    internal static partial float DevicePixelRatioRaw();

    [JSImport("platform.now", "main.js")]
    internal static partial float NowRaw();

    [JSImport("platform.userAgent", "main.js")]
    internal static partial string UserAgentRaw();

    [JSImport("platform.language", "main.js")]
    internal static partial string LanguageRaw();

    [JSImport("platform.setTitle", "main.js")]
    internal static partial void SetTitleRaw(string title);

    /// <summary>窗口逻辑宽度（CSS px）。</summary>
    public static float ViewportWidth => Safe(() => InnerWidthRaw(), 960);

    /// <summary>窗口逻辑高度（CSS px）。</summary>
    public static float ViewportHeight => Safe(() => InnerHeightRaw(), 540);

    /// <summary>设备像素比 devicePixelRatio（CSS px 与物理 px 之比）。</summary>
    public static float DevicePixelRatio => Safe(() => DevicePixelRatioRaw(), 1);

    /// <summary>performance.now()（毫秒）。</summary>
    public static float NowMs => Safe(() => NowRaw(), 0);

    /// <summary>navigator.userAgent。</summary>
    public static string UserAgent => Safe(() => UserAgentRaw(), "");

    /// <summary>navigator.language（如 "zh-CN"）。</summary>
    public static string Language => Safe(() => LanguageRaw(), "");

    /// <summary>是否为触屏设备（UA 粗判）。</summary>
    public static bool IsTouchDevice =>
        UserAgent.Contains("Android") || UserAgent.Contains("iPhone") ||
        UserAgent.Contains("iPad") || UserAgent.Contains("Touch");

    /// <summary>设置 document.title。</summary>
    public static void SetTitle(string title) { try { SetTitleRaw(title); } catch { /* 忽略 */ } }

    private static T Safe<T>(Func<T> fn, T fallback)
    {
        try { return fn(); }
        catch { return fallback; }
    }
}
