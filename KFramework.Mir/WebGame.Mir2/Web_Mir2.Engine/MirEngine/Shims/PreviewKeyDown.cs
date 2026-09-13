using System;

namespace MirEngine;

/// <summary>
/// 键盘预览事件参数（原版 WinForms 的 PreviewKeyDownEventArgs）。
/// 库内 Keys / KeyEventArgs 已由 Shims\Keyboard.cs 提供，此处补齐两侧客户端仍在使用的预览事件类型。
/// </summary>
public class PreviewKeyDownEventArgs : EventArgs
{
    public PreviewKeyDownEventArgs(Keys keyData) { KeyData = keyData; }

    public Keys KeyData { get; }
    public Keys KeyCode => (Keys)((int)KeyData & 0xFFFF);
    public Keys Modifiers => KeyData & Keys.Modifiers;

    /// <summary>是否把该键当作输入键（原版用于 Tab / 方向键的焦点切换控制）。</summary>
    public bool IsInputKey { get; set; }
    public bool Alt => (KeyData & Keys.Alt) != 0;
    public bool Control => (KeyData & Keys.Control) != 0;
    public bool Shift => (KeyData & Keys.Shift) != 0;
}
