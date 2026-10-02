using KFramework.MonoGame;
using KFramework.MonoGameExtend;
using System;
using System.Collections.Generic;

namespace KFramework.Test.Common.Tests.InputTest
{

    /// <summary>
    /// 输入测试页：演示键盘 / 鼠标的三种状态（按下 Down、持续按住 Held、抬起 Up），
    /// 以及用鼠标左键拖拽物体（方块 A / B）跟随光标移动。全部走轮询查询（GetKeyState / GetButtonState），
    /// 事件日志由"本帧状态 vs 上帧状态"差分得出，不订阅静态事件，避免场景切换时的生命周期隐患。
    /// </summary>
    public sealed class InputTestScene : TestSceneBase
    {
        // 要展示的键：由 Keys 枚举自动生成（排除 None；按数值升序；别名合并显示首个名字 + 数值）
        private static readonly (Keys key, string label)[] _keys = BuildAllKeys();

        /// <summary>枚举全量生成按键表，避免手写遗漏（F1..F12 / Tab 等曾在手写表里缺失）。
        /// Shift/Ctrl/Alt 的 Left/Right 与其同名键数值相同（浏览器不区分左右），故按数值合并。</summary>
        private static (Keys, string)[] BuildAllKeys()
        {
            var names = new Dictionary<byte, List<string>>();
            foreach (string name in Enum.GetNames<Keys>())
            {
                if (name == nameof(Keys.None)) continue;
                byte v = (byte)Enum.Parse<Keys>(name);
                if (!names.TryGetValue(v, out List<string>? list))
                {
                    list = new List<string>();
                    names[v] = list;
                }
                list.Add(name);
            }

            var result = new List<(Keys, string)>();
            for (int v = 0; v <= byte.MaxValue; v++)
            {
                if (!names.TryGetValue((byte)v, out List<string>? list)) continue;
                result.Add(((Keys)v, list[0] + "(" + v + ")"));
            }
            return result.ToArray();
        }

        private static readonly MouseButton[] _buttonsList = [MouseButton.Left, MouseButton.Right, MouseButton.Middle];

        // 每个键 / 按钮的"持续按住"帧数（用于展示 Held）
        private readonly Dictionary<Keys, int> _keyFrames = new();
        private readonly Dictionary<MouseButton, int> _btnFrames = new();

        // 上一帧的按住状态，用于差分出"按下 / 抬起"事件日志
        private readonly Dictionary<Keys, bool> _prevKey = new();
        private readonly Dictionary<MouseButton, bool> _prevBtn = new();
        private readonly List<string> _log = new();

        // 两个可拖拽方块 + 拖拽状态
        private Rectangle _boxA = Rectangle.Empty;
        private Rectangle _boxB = Rectangle.Empty;
        private Vector2 _offsetA;
        private Vector2 _offsetB;
        private bool _draggingA;
        private bool _draggingB;

        // 键盘绑定模式开关：true=绑画布(需聚焦) / false=绑 window(全局)。对应 Input_KeyBoard.Activate(bUseCanvas)。
        private bool _keyboardUseCanvas = true;
        private Rectangle _bindBtn = Rectangle.Empty;

        public override string Title
        {
            get { return "输入测试（鼠标 / 键盘）"; }
        }

        public override void Update()
        {
            base.Update();

            HandleBindToggle();

            foreach ((Keys key, _) in _keys)
            {
                bool held = Input_KeyBoard.GetKey(key);
                if (held) _keyFrames[key] = _keyFrames.GetValueOrDefault(key) + 1;
                else _keyFrames[key] = 0;

                bool prev = _prevKey.GetValueOrDefault(key);
                if (held && !prev) LogEvent($"键盘按下 {key}");
                else if (!held && prev) LogEvent($"键盘抬起 {key}");
                _prevKey[key] = held;
            }

            foreach (MouseButton btn in _buttonsList)
            {
                bool held = Input_Mouse.GetButton(btn);
                if (held) _btnFrames[btn] = _btnFrames.GetValueOrDefault(btn) + 1;
                else _btnFrames[btn] = 0;

                bool prev = _prevBtn.GetValueOrDefault(btn);
                if (held && !prev) LogEvent($"鼠标按下 {btn}");
                else if (!held && prev) LogEvent($"鼠标抬起 {btn}");
                _prevBtn[btn] = held;
            }

            if (_boxA.IsEmpty)
            {
                int h = Device.Viewport.Height;
                _boxA = new Rectangle(48, h - 218, 120, 70);
                _boxB = new Rectangle(184, h - 190, 96, 96);
            }

            Drag(ref _boxA, ref _offsetA, ref _draggingA);
            Drag(ref _boxB, ref _offsetB, ref _draggingB);
        }

        private void Drag(ref Rectangle box, ref Vector2 offset, ref bool dragging)
        {
            Vector2 mp = Input_Mouse.Position;
            if (Input_Mouse.GetButtonDown(MouseButton.Left) && box.Contains(mp))
            {
                dragging = true;
                offset = new Vector2(box.X - mp.X, box.Y - mp.Y);
            }
            if (dragging && !Input_Mouse.GetButton(MouseButton.Left)) dragging = false;
            if (dragging) box = new Rectangle((int)(mp.X + offset.X), (int)(mp.Y + offset.Y), box.Width, box.Height);
        }

        private void LogEvent(string s)
        {
            _log.Insert(0, s);
            if (_log.Count > 10) _log.RemoveAt(_log.Count - 1);
        }

        protected override void DrawBody(SpriteBatch batch, Vector2 origin)
        {
            int w = Device.Viewport.Width;
            int h = Device.Viewport.Height;
            float gridRight = w - 400f;   // 键盘区右边界（右侧留给鼠标 / 日志面板）

            // ===== 左：键盘全键表（缩小字号 + 自动换行铺满，键再多也放得下） =====
            float ky = origin.Y;
            batch.DrawString(Font,
                "一、键盘全键表（枚举全量 " + _keys.Length + " 键：按下=黄 / 按住=绿 / 抬起=红 / 无=灰）",
                new Vector2(origin.X, ky), Color.LightGray);
            ky += Font.LineSpacing + 8f;

            float scale = 0.72f;
            float lineH = Font.LineSpacing * scale + 2f;
            const float gap = 8f;
            float x = origin.X;
            for (int i = 0; i < _keys.Length; i++)
            {
                (Keys key, string label) = _keys[i];
                KPressState st = Input_KeyBoard.GetKeyState(key);
                string txt = st == KPressState.None ? label : label + " " + StateText(st);
                // 按基础标签宽度 + 预留状态文本宽度来推进，按下时整行不抖动
                float wItem = Font.Measure(label).X * scale + 40f * scale + gap;
                if (x + wItem > gridRight && x > origin.X)
                {
                    x = origin.X;
                    ky += lineH;
                }
                if (ky > h - 8) break;     // 极端小窗时截断，不溢出
                batch.DrawString(Font, txt, new Vector2(x, ky), StateColor(st), 0f, Vector2.Zero, scale);
                x += wItem;
            }

            // 轴信息放在键盘区下方
            Vector2 axis = Input_KeyBoard.GetAxis();
            batch.DrawString(Font,
                $"WASD / 方向键 轴: ({axis.X:+0.##;-0.##;0}, {axis.Y:+0.##;-0.##;0})",
                new Vector2(origin.X, ky + lineH + 6f), Color.LightGray);

            DrawDragArea(batch);

            // ===== 右：鼠标 / 事件日志 =====
            float rx = w - 372f;
            float ry = Math.Max(origin.Y, 96f);
            ry += DrawSection(batch, "二、鼠标 Mouse", new Vector2(rx, ry));
            batch.DrawString(Font, $"位置: ({Input_Mouse.X}, {Input_Mouse.Y})", new Vector2(rx, ry), Color.LightGray);
            ry += Font.LineSpacing + 4f;
            batch.DrawString(Font, $"位移: ({Input_Mouse.Delta.X}, {Input_Mouse.Delta.Y}) 滚轮: {Input_Mouse.ScrollValue}", new Vector2(rx, ry), Color.LightGray);
            ry += Font.LineSpacing + 4f;
            foreach (MouseButton btn in _buttonsList)
            {
                KPressState st = Input_Mouse.GetButtonState(btn);
                int frames = _btnFrames.GetValueOrDefault(btn);
                string suffix = st == KPressState.Held ? $" (按住 {frames} 帧)" : "";
                batch.DrawString(Font, $"{btn}  {StateText(st)}{suffix}", new Vector2(rx, ry), StateColor(st));
                ry += Font.LineSpacing + 4f;
            }
            ry += 8f;
            ry += DrawSection(batch, "三、本帧事件日志", new Vector2(rx, ry));
            for (int i = 0; i < _log.Count; i++)
            {
                batch.DrawString(Font, _log[i], new Vector2(rx, ry), new Color(200, 210, 230));
                ry += Font.LineSpacing + 3f;
            }

            DrawBindToggle(batch);
        }

        /// <summary>键盘绑定模式开关（体现 <see cref="Input_KeyBoard.Activate(bool)"/> 的 bUseCanvas 参数）：
        /// 画布模式需画布聚焦才收键；窗口模式全局捕获。点击按钮实时切换并重新激活。</summary>
        private void DrawBindToggle(SpriteBatch batch)
        {
            if (_bindBtn.IsEmpty) return;
            bool hover = _bindBtn.Contains(Input_Mouse.Position);
            DrawRect(batch, _bindBtn, hover ? new Color(40, 60, 90) : new Color(28, 36, 56));
            string mode = _keyboardUseCanvas ? "画布(需聚焦)" : "窗口(全局)";
            string label = $"[点击切换] 键盘绑定: {mode}";
            float tx = _bindBtn.X + 8f;
            float ty = _bindBtn.Y + (_bindBtn.Height - Font.LineSpacing) / 2f;
            batch.DrawString(Font, label, new Vector2(tx, ty), Color.White);
            batch.DrawString(Font,
                "画布：点游戏区外再点回会暂失焦收不到键 / 窗口：始终全局捕获",
                new Vector2(_bindBtn.X, _bindBtn.Y + _bindBtn.Height + 4f), new Color(150, 160, 180));
        }

        private void HandleBindToggle()
        {
            int w = Device.Viewport.Width;
            if (_bindBtn.IsEmpty) _bindBtn = new Rectangle(w - 360, 20, 340, 30);
            if (Input_Mouse.GetButtonDown(MouseButton.Left) && _bindBtn.Contains(Input_Mouse.Position))
            {
                _keyboardUseCanvas = !_keyboardUseCanvas;
                Input_KeyBoard.Deactivate();
                Input_KeyBoard.Activate(_keyboardUseCanvas);
                LogEvent("键盘绑定 → " + (_keyboardUseCanvas ? "画布(需聚焦)" : "窗口(全局)"));
            }
        }

        private void DrawDragArea(SpriteBatch batch)
        {
            int h = Device.Viewport.Height;
            Rectangle panel = new Rectangle(28, h - 230, 360, 220);
            DrawRect(batch, panel, new Color(20, 26, 40));
            batch.DrawString(Font, "拖拽区域：左键按住方块拖动", new Vector2(panel.X + 10f, panel.Y + 8f), Color.LightGray);
            DrawBox(batch, _boxA, _draggingA, "方块 A");
            DrawBox(batch, _boxB, _draggingB, "方块 B");
        }

        private void DrawBox(SpriteBatch batch, Rectangle box, bool dragging, string label)
        {
            Color fill = dragging ? new Color(96, 190, 255) : new Color(60, 120, 200);
            DrawRect(batch, box, fill);
            float cx = box.X + box.Width / 2f - Font.Measure(label).X / 2f;
            float cy = box.Y + box.Height / 2f - Font.LineSpacing / 2f;
            batch.DrawString(Font, label, new Vector2(cx, cy), Color.White);
            if (dragging)
                batch.DrawString(Font, "拖拽中", new Vector2(box.X, box.Y - Font.LineSpacing - 2f), new Color(255, 206, 110));
        }

        private static Color StateColor(KPressState st)
        {
            if (st == KPressState.Down) return new Color(255, 206, 110);
            if (st == KPressState.Held) return new Color(120, 230, 140);
            if (st == KPressState.Up) return new Color(230, 120, 120);
            return new Color(120, 130, 150);
        }

        private static string StateText(KPressState st)
        {
            if (st == KPressState.Down) return "按下";
            if (st == KPressState.Held) return "按住";
            if (st == KPressState.Up) return "抬起";
            return "无";
        }
    }

}
