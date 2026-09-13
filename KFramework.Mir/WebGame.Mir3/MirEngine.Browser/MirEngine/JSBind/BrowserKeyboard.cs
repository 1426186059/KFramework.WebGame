using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.JavaScript;
using MirEngine;

namespace MirEngine;

/// <summary>
/// 浏览器键盘输入封装。对应 tsengine/src/core/keyboard.ts（mir.keyboardAttach / keyboardDetach）。
/// JS DOM 的 keydown / keyup 经 mir.keyboardAttach 注册后，回调本类的 [JSExport] 入口，
/// 翻译成 WinForms 风格 KeyEventArgs / KeyPressEventArgs 并抛出 C# 事件，由游戏层订阅。
/// </summary>
public static partial class BrowserKeyboard
{
    [JSImport("mir.keyboardAttach", "main.js")]
    private static partial void KeyboardAttachImpl();

    [JSImport("mir.keyboardDetach", "main.js")]
    private static partial void KeyboardDetachImpl();

    public static void Attach() => KeyboardAttachImpl();
    public static void Detach() => KeyboardDetachImpl();

    public static event EventHandler<KeyEventArgs> KeyDown;
    public static event EventHandler<KeyEventArgs> KeyUp;
    public static event EventHandler<KeyPressEventArgs> KeyPress;

    [JSExport]
    public static void OnKeyDown(string key)
    {
        Keys k = ToKeys(key);
        if (k == Keys.None) return;
        KeyDown?.Invoke(null, new KeyEventArgs(k));
    }

    [JSExport]
    public static void OnKeyUp(string key)
    {
        Keys k = ToKeys(key);
        if (k == Keys.None) return;
        KeyUp?.Invoke(null, new KeyEventArgs(k));
    }

    [JSExport]
    public static void OnKeyPress(string key)
    {
        if (string.IsNullOrEmpty(key)) return;
        KeyPress?.Invoke(null, new KeyPressEventArgs(key[0]));
    }

    // ---- 键名 -> Keys 枚举 ----
    private static readonly Dictionary<string, Keys> SpecialKeys = new Dictionary<string, Keys>(StringComparer.OrdinalIgnoreCase)
    {
        { "arrowup", Keys.Up }, { "arrowdown", Keys.Down },
        { "arrowleft", Keys.Left }, { "arrowright", Keys.Right },
        { "escape", Keys.Escape }, { "enter", Keys.Return },
        { "return", Keys.Return }, { "tab", Keys.Tab },
        { " " , Keys.Space }, { "spacebar", Keys.Space },
        { "backspace", Keys.Back }, { "delete", Keys.Delete },
        { "shift", Keys.ShiftKey }, { "control", Keys.ControlKey },
        { "alt", Keys.Menu },
        { "f1", Keys.F1 }, { "f2", Keys.F2 },
        { "f3", Keys.F3 }, { "f4", Keys.F4 },
        { "f5", Keys.F5 }, { "f6", Keys.F6 },
        { "f7", Keys.F7 }, { "f8", Keys.F8 },
        { "f9", Keys.F9 }, { "f10", Keys.F10 },
        { "f11", Keys.F11 }, { "f12", Keys.F12 },
    };

    private static Keys ToKeys(string key)
    {
        if (string.IsNullOrEmpty(key)) return Keys.None;
        if (SpecialKeys.TryGetValue(key, out Keys sp)) return sp;
        if (Enum.TryParse<Keys>(key, true, out Keys parsed)) return parsed;
        if (key.Length == 1) return (Keys)char.ToUpperInvariant(key[0]);
        return Keys.None;
    }
}
