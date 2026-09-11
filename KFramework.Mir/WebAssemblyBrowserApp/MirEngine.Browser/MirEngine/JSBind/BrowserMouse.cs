using System;
using System.Runtime.InteropServices.JavaScript;
using MirEngine;

namespace MirEngine;

/// <summary>
/// 浏览器鼠标输入封装。对应 tsengine/src/core/mouse.ts（mir.mouseAttach / mouseDetach）。
/// JS DOM 的 mousedown / mousemove / mouseup / wheel 经 mir.mouseAttach 注册后，回调本类的 [JSExport] 入口，
/// 翻译成 WinForms 风格 MouseEventArgs 并抛出 C# 事件，由游戏层订阅。
/// </summary>
public static partial class BrowserMouse
{
    [JSImport("mir.mouseAttach", "main.js")]
    private static partial void MouseAttachImpl();

    [JSImport("mir.mouseDetach", "main.js")]
    private static partial void MouseDetachImpl();

    public static void Attach() => MouseAttachImpl();
    public static void Detach() => MouseDetachImpl();

    public static event EventHandler<MouseEventArgs> MouseDown;
    public static event EventHandler<MouseEventArgs> MouseMove;
    public static event EventHandler<MouseEventArgs> MouseUp;
    public static event EventHandler<MouseEventArgs> MouseWheel;

    // DOM MouseEvent.button 是 0/1/2/3/4，而 WinForms 的 MouseButtons 是位标志
    // （Left=0x100000、Right=0x200000、Middle=0x400000），两者不能直接强转：
    // 直接强转会让左键变成 MouseButtons.None、右键变成一个未定义的枚举值 2，
    // 于是 MapControl.OnMouseClick 的 switch (e.Button) 永远匹配不到分支 —— 点击不走路。
    private static MouseButtons ConvertButton(int button) => button switch
    {
        0 => MouseButtons.Left,
        1 => MouseButtons.Middle,
        2 => MouseButtons.Right,
        3 => MouseButtons.XButton1,
        4 => MouseButtons.XButton2,
        _ => MouseButtons.None,
    };

    [JSExport]
    public static void OnMouseDown(int button, int x, int y)
        => MouseDown?.Invoke(null, new MouseEventArgs(ConvertButton(button), 1, x, y, 0));

    [JSExport]
    public static void OnMouseMove(int x, int y)
        => MouseMove?.Invoke(null, new MouseEventArgs(MouseButtons.None, 0, x, y, 0));

    [JSExport]
    public static void OnMouseUp(int button, int x, int y)
        => MouseUp?.Invoke(null, new MouseEventArgs(ConvertButton(button), 1, x, y, 0));

    [JSExport]
    public static void OnMouseWheel(int delta, int x, int y)
        => MouseWheel?.Invoke(null, new MouseEventArgs(MouseButtons.None, 0, x, y, delta));
}
