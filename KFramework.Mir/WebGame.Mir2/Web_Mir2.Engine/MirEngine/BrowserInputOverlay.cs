using System;
using System.Runtime.InteropServices.JavaScript;

namespace MirEngine;

/// <summary>
/// 浏览器原生文本输入覆盖层。对应 tsengine/src/core/input.ts（mir.inputShow / inputHide / inputSetValue / inputReposition）。
/// 替代 WinForms 风格的 TextBox 按键模拟：在 canvas 上叠加真实 &lt;input&gt;/&lt;textarea&gt;，由其原生处理光标/选区/IME，
/// 仅在值变化 / 回车 / 失焦时回调 C#（OnInput / OnEnter / OnBlur），由游戏侧 MirTextBox 把结果同步回 shim TextBox。
/// </summary>
public static partial class BrowserInputOverlay
{
    [JSImport("mir.inputShow", "main.js")]
    private static partial void InputShowImpl(double cx, double cy, double cw, double ch, double fontPx, int color, string value, bool password, int maxLength, bool multiline);

    [JSImport("mir.inputHide", "main.js")]
    private static partial void InputHideImpl();

    [JSImport("mir.inputSetValue", "main.js")]
    private static partial void InputSetValueImpl(string value);

    [JSImport("mir.inputGetValue", "main.js")]
    private static partial string InputGetValueImpl();

    [JSImport("mir.inputReposition", "main.js")]
    private static partial void InputRepositionImpl(double cx, double cy, double cw, double ch);

    public static event EventHandler<string> ValueChanged;
    public static event EventHandler Enter;
    public static event EventHandler Blur;

    public static void Show(double cx, double cy, double cw, double ch, double fontPx, int color, string value, bool password, int maxLength, bool multiline)
        => InputShowImpl(cx, cy, cw, ch, fontPx, color, value, password, maxLength, multiline);

    public static void Hide() => InputHideImpl();

    public static void SetValue(string value) => InputSetValueImpl(value);

    public static string GetValue() => InputGetValueImpl();

    public static void Reposition(double cx, double cy, double cw, double ch) => InputRepositionImpl(cx, cy, cw, ch);

    [JSExport]
    public static void OnInput(string value) => ValueChanged?.Invoke(null, value);

    [JSExport]
    public static void OnEnter() => Enter?.Invoke(null, EventArgs.Empty);

    [JSExport]
    public static void OnBlur() => Blur?.Invoke(null, EventArgs.Empty);
}
