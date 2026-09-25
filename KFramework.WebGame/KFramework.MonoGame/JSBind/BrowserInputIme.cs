using System.Runtime.InteropServices.JavaScript;

namespace MirEngine;

/// <summary>
/// DOM 文本输入覆盖层 -> 引擎的 [JSExport] 桥接。对应 tsengine/src/input_html_ime.ts 的前向调用。
/// 输入以“事件”进入引擎：控制键转发给引擎 TextBox（自行维护光标），原生编辑结果经 OnDomValue 回传，
/// 由引擎按自身光标合并文本。引擎始终是 text / 光标 / 选区的唯一真相源（对齐 UGUI InputField）。
/// </summary>
public static partial class BrowserInputIme
{
    [JSExport]
    public static void OnKeyDown(string key, bool ctrl, bool shift, bool alt)
        => KFramework.MonoGame.TextBox.ProcessKey(key, ctrl, shift, alt);

    [JSExport]
    public static void OnDomValue(string value, bool composing)
        => KFramework.MonoGame.TextBox.ProcessDomValue(value, composing);
}
