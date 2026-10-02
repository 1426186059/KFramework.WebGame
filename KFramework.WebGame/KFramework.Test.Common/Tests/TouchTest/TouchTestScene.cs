using KFramework.MonoGame;
using KFramework.MonoGameExtend;
using System.Collections.Generic;

namespace KFramework.Test.Common.Tests.TouchTest
{

    /// <summary>
    /// 触摸测试页：演示多触点状态（Began / Moved / Stationary / Ended）、位置 / Δ / 持续时长，
    /// 双指 Pinch（缩放比 / 中心点 / 间距），以及手势（Tap / LongPress / Swipe）。
    /// 直接在画布上绘制各触点的位置标记，方便在电脑上用浏览器设备模拟（Ctrl+Shift+M）验证。
    /// 事件日志由订阅 <see cref="Input_Touch"/> 的事件差分得出；触点实时状态由轮询 <see cref="Input_Touch.FrameTouches"/> 得出。
    /// </summary>
    public sealed class TouchTestScene : TestSceneBase
    {
        private readonly List<string> _log = new();

        public override string Title => "触摸测试（手机 / 平板）";

        public TouchTestScene()
        {
            Input_Touch.TouchBegan += OnBegan;
            Input_Touch.TouchEnded += OnEnded;
            Input_Touch.Tap += OnTap;
            Input_Touch.LongPress += OnLongPress;
            Input_Touch.Swipe += OnSwipe;
        }

        public override void Update()
        {
            base.Update();
            // 本页无需按钮交互，但保留基类返回逻辑（Esc / 点返回）
        }

        protected override void DrawBody(SpriteBatch batch, Vector2 origin)
        {
            float y = origin.Y;

            y += DrawSection(batch, "提示：电脑上用浏览器 DevTools 设备模拟（Ctrl+Shift+M 选手机预设）即可产生触摸事件", new Vector2(origin.X, y));

            // 一、当前触点实时状态
            y += DrawSection(batch, "一、当前触点 FrameTouches（状态由 C# 差分得出）", new Vector2(origin.X, y));
            if (Input_Touch.TouchCount == 0)
            {
                y += DrawLine(batch, null, "（无触点）在画布上点按 / 拖动试试", new Vector2(origin.X, y), new Color(150, 160, 180));
            }
            else
            {
                for (int i = 0; i < Input_Touch.TouchCount; i++)
                {
                    KTouch t = Input_Touch.GetTouch(i);
                    if (y < Device.Viewport.Height - 8)
                    {
                        batch.DrawString(Font,
                            $"#{t.Id} {StateText(t.State)}  位置:({t.Position.X:F0},{t.Position.Y:F0})  Δ:({t.Delta.X:F0},{t.Delta.Y:F0})  按住:{t.Duration:F2}s  总位移:{t.TotalDistance:F0}",
                            new Vector2(origin.X, y), StateColor(t.State));
                    }
                    y += Font.LineSpacing + 4f;
                }
            }

            // 二、双指 Pinch
            y += DrawSection(batch, "二、双指 Pinch", new Vector2(origin.X, y));
            if (Input_Touch.TouchCount >= 2)
            {
                float scale = Input_Touch.GetPinchScale();
                Vector2 center = Input_Touch.GetPinchCenter();
                float dist = Input_Touch.GetPinchDistance();
                if (y < Device.Viewport.Height - 8)
                    batch.DrawString(Font,
                        $"缩放比: {scale:F3}   中心点: ({center.X:F0},{center.Y:F0})   间距: {dist:F0}",
                        new Vector2(origin.X, y), Color.LightGray);
                y += Font.LineSpacing + 6f;
            }
            else
            {
                y += DrawLine(batch, null, "（少于两指，无法计算 Pinch；放两指再分开 / 合拢）", new Vector2(origin.X, y), new Color(150, 160, 180));
            }

            // 三、事件日志
            y += DrawSection(batch, "三、手势 / 触点事件日志（订阅事件差分得出）", new Vector2(origin.X, y));
            for (int i = 0; i < _log.Count; i++)
            {
                if (y < Device.Viewport.Height - 8)
                    batch.DrawString(Font, _log[i], new Vector2(origin.X, y), new Color(200, 210, 230));
                y += Font.LineSpacing + 3f;
            }

            // 在画布上实时绘制各触点位置标记（直接用 touch.Position，已为画布坐标）
            for (int i = 0; i < Input_Touch.TouchCount; i++)
            {
                KTouch t = Input_Touch.GetTouch(i);
                var rect = new Rectangle((int)t.Position.X - 14, (int)t.Position.Y - 14, 28, 28);
                batch.Draw(KDefaultRes.DefaultTexture2D, rect, StateColor(t.State) * 0.55f);
                Vector2 lp = t.Position - new Vector2(10f, 28f);
                if (lp.Y > 0 && lp.Y < Device.Viewport.Height)
                    batch.DrawString(Font, "#" + t.Id, lp, Color.White);
            }
        }

        private void OnBegan(KTouch t) => Log($"触点#{t.Id} 按下 ({t.Position.X:F0},{t.Position.Y:F0})");
        private void OnEnded(KTouch t) => Log($"触点#{t.Id} 抬起 (按住 {t.Duration:F2}s, 总位移 {t.TotalDistance:F0})");
        private void OnTap(KTapGesture g) => Log($"Tap #{g.Id} @({g.Position.X:F0},{g.Position.Y:F0}) 时长 {g.Duration:F2}s");
        private void OnLongPress(KTouch t) => Log($"LongPress #{t.Id} @({t.Position.X:F0},{t.Position.Y:F0})");
        private void OnSwipe(KSwipeGesture g) => Log($"Swipe #{g.Id} 方向 {DirName(g.SwipeDirection)} 距离 {g.Distance:F0} 速度 {g.Speed:F1}");

        private void Log(string s)
        {
            _log.Insert(0, s);
            if (_log.Count > 12) _log.RemoveAt(_log.Count - 1);
        }

        private static string StateText(KTouchState st)
        {
            return st switch
            {
                KTouchState.Began => "按下",
                KTouchState.Moved => "移动",
                KTouchState.Stationary => "静止",
                KTouchState.Ended => "抬起",
                KTouchState.Canceled => "取消",
                _ => "无效",
            };
        }

        private static Color StateColor(KTouchState st)
        {
            return st switch
            {
                KTouchState.Began => new Color(255, 206, 110),
                KTouchState.Moved => new Color(120, 230, 140),
                KTouchState.Stationary => new Color(150, 160, 180),
                KTouchState.Ended => new Color(230, 120, 120),
                KTouchState.Canceled => new Color(200, 120, 200),
                _ => new Color(120, 130, 150),
            };
        }

        private static string DirName(KSwipeDirection d)
        {
            return d switch
            {
                KSwipeDirection.Up => "↑上",
                KSwipeDirection.Down => "↓下",
                KSwipeDirection.Left => "←左",
                KSwipeDirection.Right => "→右",
                _ => "无",
            };
        }

        public override void Dispose()
        {
            Input_Touch.TouchBegan -= OnBegan;
            Input_Touch.TouchEnded -= OnEnded;
            Input_Touch.Tap -= OnTap;
            Input_Touch.LongPress -= OnLongPress;
            Input_Touch.Swipe -= OnSwipe;
            base.Dispose();
        }
    }

}
