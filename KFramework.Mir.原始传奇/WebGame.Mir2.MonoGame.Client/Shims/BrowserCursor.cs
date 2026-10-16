using KFramework.MonoGame;
using System;

// 浏览器端光标设置：原版用 Win32 .CUR 文件切换窗体光标；浏览器端无法加载 .CUR，
// 改为切换 default 画布（GraphicsDevice.CanvasId，默认 "game"）的 CSS cursor。
// 底层走 KFramework.MonoGame.MouseCursorFunc（进而 JSBind_Cursor → cursor.ts）；
// name 为合法 CSS cursor 值（业务侧语义名 attack/npc/text/trash/default 由 CMain.SetMouseCursor 传入）。
public static class BrowserCursor
{
    public static void Set(string name)
    {
        try { MouseCursorFunc.Set(name); }
        catch { }
    }

    // 复位为默认箭头。
    public static void Reset()
    {
        try { MouseCursorFunc.Reset(); }
        catch { }
    }
}
