using System;
using System.Collections.Generic;
using MirEngine;
using Client.MirControls;
using Client.MirGraphics;
using Client.MirNetwork;
using Client.MirScenes;

namespace Client
{
    // 浏览器端游戏主机：替代原 WinForms.Forms/CMain 窗体。
    // 保留 Mir2 代码引用的静态成员（Time/Now/MPoint/Random/DPSCounter/BytesReceived/BytesSent...）
    // 以及被 Program.Form 引用的"窗体"成员（Controls/ActiveControl/Close/...）。
    public class CMain
    {
        public static long Time;
        public static DateTime Now;
        public static Point MPoint;
        public static Random Random = new Random();
        public static int DPSCounter;
        public static KeyBindSettings InputKeys = new KeyBindSettings();
        public static long BytesReceived, BytesSent;
        public static int FPS;
        public static int TotalBytesReceived, TotalBytesSent;

        public static GraphicsStub Graphics = new GraphicsStub();

        // 被 Program.Form 引用的"窗体"成员（交互由 DOM 桥接，这里仅占位）。
        public List<object> Controls = new List<object>();
        public object ActiveControl;
        public string Text = "";
        public Size ClientSize = new Size(1024, 768);
        public Rectangle ClientRectangle => new Rectangle(0, 0, ClientSize.Width, ClientSize.Height);
        public FormBorderStyle FormBorderStyle;
        public bool TopMost;
        // 完全限定 MirEngine.Cursors：本类存在 static Cursor[] Cursors 字段，会遮蔽同名类型。
        public Cursor Cursor = MirEngine.Cursors.Default;
        public void Close() { }
        public void Focus() { }
        public void Activate() { }
        public void CenterToScreen() { }
        public Point PointToClient(Point p) => p;
        public Point PointToScreen(Point p) => p;
        public Rectangle RectangleToScreen(Rectangle r) => r;
        public void CreateScreenShot() { }

        // 原 WinForms CMain 中被逻辑代码引用的静态成员（浏览器端用占位/轻量实现）。
        public static bool Shift, Alt, Ctrl, Tilde, SpellTargetLock;
        public static Cursor[] Cursors = new Cursor[16];
        public static MirControl DebugBaseLabel, HintBaseLabel;
        public static string DebugText = "";
        public static long PingTime;
        public static long NextPing = 10000;

        static CMain()
        {
            for (int i = 0; i < Cursors.Length; i++) Cursors[i] = new Cursor();
        }

        // 原版用 Win32 .CUR 文件切换窗体光标；浏览器端无法加载 .CUR，改为切换 canvas 的 CSS cursor。
        // 之前的空实现会让游戏内光标永远停在 canvas 默认样式（攻击/NPC对话/文本等状态都不变化）。
        // 传的是语义名，具体图片/样式在 tsengine/src/core/cursor.ts 里定义（改成自己的图片只需改那里）。
        public static void SetMouseCursor(MouseCursor cursor)
        {
            string name;
            switch (cursor)
            {
                case MouseCursor.Attack:
                case MouseCursor.AttackRed:
                    name = "attack";
                    break;
                case MouseCursor.NPCTalk:
                case MouseCursor.Upgrade:
                    name = "npc";
                    break;
                case MouseCursor.TextPrompt:
                    name = "text";
                    break;
                case MouseCursor.Trash:
                    name = "trash";
                    break;
                default:
                    name = "default";
                    break;
            }

            MirEngine.BrowserCursor.Set(name);
        }
        public static void SetResolution(int width, int height) { }
        public static void ToggleFullScreen() { }
        public static bool IsKeyLocked(Keys key) => false;

        public static void CMain_KeyDown(object sender, KeyEventArgs e)
        {
            Shift = e.Shift; Alt = e.Alt; Ctrl = e.Control;
            if (!string.IsNullOrEmpty(InputKeys.GetKey(KeybindOptions.TargetSpellLockOn)))
                SpellTargetLock = e.KeyCode == (Keys)Enum.Parse(typeof(Keys), InputKeys.GetKey(KeybindOptions.TargetSpellLockOn), true);
            else SpellTargetLock = false;
            if (e.KeyCode == Keys.Oem8) Tilde = true;
            try
            {
                if (e.Alt && e.KeyCode == Keys.Enter) { ToggleFullScreen(); return; }
                if (MirScene.ActiveScene != null) MirScene.ActiveScene.OnKeyDown(e);
            }
            catch (Exception ex) { SaveError(ex.ToString()); }
        }

        public static void CMain_KeyUp(object sender, KeyEventArgs e)
        {
            Shift = e.Shift; Alt = e.Alt; Ctrl = e.Control;
            if (!string.IsNullOrEmpty(InputKeys.GetKey(KeybindOptions.TargetSpellLockOn)))
                SpellTargetLock = e.KeyCode == (Keys)Enum.Parse(typeof(Keys), InputKeys.GetKey(KeybindOptions.TargetSpellLockOn), true);
            else SpellTargetLock = false;
            if (e.KeyCode == Keys.Oem8) Tilde = false;
            foreach (KeyBind KeyCheck in CMain.InputKeys.Keylist)
            {
                if (KeyCheck.function != KeybindOptions.Screenshot) continue;
                if (KeyCheck.Key != e.KeyCode) continue;
                if ((KeyCheck.RequireAlt != 2) && (KeyCheck.RequireAlt != (Alt ? 1 : 0))) continue;
                if ((KeyCheck.RequireShift != 2) && (KeyCheck.RequireShift != (Shift ? 1 : 0))) continue;
                if ((KeyCheck.RequireCtrl != 2) && (KeyCheck.RequireCtrl != (Ctrl ? 1 : 0))) continue;
                if ((KeyCheck.RequireTilde != 2) && (KeyCheck.RequireTilde != (Tilde ? 1 : 0))) continue;
                Program.Form.CreateScreenShot();
                break;
            }
            try { if (MirScene.ActiveScene != null) MirScene.ActiveScene.OnKeyUp(e); }
            catch (Exception ex) { SaveError(ex.ToString()); }
        }

        public static void CMain_KeyPress(object sender, KeyPressEventArgs e)
        {
            try { if (MirScene.ActiveScene != null) MirScene.ActiveScene.OnKeyPress(e); }
            catch (Exception ex) { SaveError(ex.ToString()); }
        }

        public static void CMain_MouseMove(object sender, MouseEventArgs e)
        {
            MPoint = e.Location;
            try { if (MirScene.ActiveScene != null) MirScene.ActiveScene.OnMouseMove(e); }
            catch (Exception ex) { SaveError(ex.ToString()); }
        }

        public static void SaveError(string ex)
        {
            try { MirEngine.BrowserResource.Log("[Mir][Error] " + ex); }
            catch { }
        }

        private static string _lastLoopError;

        public static void Loop()
        {
            try
            {
                Time = Environment.TickCount & 0x7FFFFFFF;
                Now = DateTime.Now;

                Network.Process();

                if (MirScene.ActiveScene != null)
                {
                    MirScene.ActiveScene.Process();
                    DXManager.RenderFrame(() => MirScene.ActiveScene.Draw());
                }

                DXManager.Clean();
            }
            catch (Exception ex)
            {
                // 帧循环里的未处理异常会终止渲染进程且拿不到堆栈；这里打印完整堆栈（去重）到控制台，
                // 便于定位真正的出错行（如连接阶段的 NullReferenceException）。
                string s = ex.ToString();
                if (s != _lastLoopError)
                {
                    _lastLoopError = s;
                    SaveError(s);
                }
            }
        }
    }

    public class GraphicsStub
    {
        public float DpiX => 96f;
        public float DpiY => 96f;
    }
}
