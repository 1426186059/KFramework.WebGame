using WebGame.Mir2.MonoGame.Client;

namespace Client.MirControls
{
    /// <summary>
    /// 统一的"冒泡提示"：一行文字从屏幕中下方浮出、上移一段后停留 2 秒，再淡出消失。
    /// 非阻塞、不拦截鼠标（NotControl），与 MirMessageBox 的模态提示互补。
    /// 用法：MirBubbleToast.Show("文字")；多条同时出现时自动向上错开，互不覆盖。
    /// <para>每帧驱动走 AfterDraw（与 ChatNoticeDialog / TaskCompleteEffect 一致），无需改 GameScene.Process。</para>
    /// </summary>
    public sealed class MirBubbleToast : MirControl
    {
        private const int RiseDistance = 46;        // 整体向上浮动的总像素
        private const int RiseDuration = 360;       // 上浮+淡入耗时(ms)
        private const int HoldDuration = 2000;      // 停留 2 秒
        private const int FadeDuration = 420;       // 淡出耗时(ms)
        private const int TotalDuration = RiseDuration + HoldDuration + FadeDuration;

        private static readonly System.Collections.Generic.List<MirBubbleToast> Active = new System.Collections.Generic.List<MirBubbleToast>();

        private readonly MirLabel _label;
        private readonly long _startTime;
        private readonly int _baseY;                // 起点 Y（浮出前的位置），由出现时的活跃条数错开
        private bool _finished;

        private MirBubbleToast(string text, Color colour, int font)
        {
            _startTime = CMain.Time;

            // 终点（上浮完成后的停留位置）对齐屏幕正中：起点 = 屏幕中点 + 上浮距离。
            // 同时多条时，越晚出现的整体越往下错开，避免文字重叠。
            int stackIndex = 0;
            for (int i = 0; i < Active.Count; i++)
                if (Active[i]._startTime > _startTime - 1)
                    stackIndex++;
            _baseY = Settings.ScreenHeight / 2 + RiseDistance + stackIndex * 34;

            NotControl = true;                       // 纯提示，绝不吃鼠标
            Sort = true;                             // 置于同层最上方
            DrawControlTexture = false;
            Size = new Size(Settings.ScreenWidth, 40);

            _label = new MirLabel
            {
                Parent = this,
                NotControl = true,
                AutoSize = true,
                BackColour = Color.Transparent,
                ForeColour = colour,
                OutLine = true,
                OutLineColour = Color.Black,
                DrawFormat = new TextStyle(TextAnchor.MiddleCenter),
                Font = new Font(Settings.FontName, font),
                Text = text,
            };
            _label.Location = new Point((Settings.ScreenWidth - _label.Size.Width) / 2, 0);

            Location = new Point(0, _baseY);
            Opacity = 0F;

            AfterDraw += OnAfterDraw;
        }

        /// <summary>显示一行冒泡提示（白色，12 号普通字体）。</summary>
        public static void Show(string text)
        {
            Show(text, Color.White);
        }

        /// <summary>显示一行冒泡提示，可指定颜色与字号。</summary>
        public static void Show(string text, Color colour, int font = 12)
        {
            if (string.IsNullOrEmpty(text) || MirScene.ActiveScene == null)
                return;

            MirBubbleToast toast = new MirBubbleToast(text, colour, font);
            toast.Parent = MirScene.ActiveScene;
            Active.Add(toast);
            toast.TrySort();
        }

        private void OnAfterDraw(object sender, System.EventArgs e)
        {
            if (_finished)
                return;

            long elapsed = CMain.Time - _startTime;
            if (elapsed >= TotalDuration)
            {
                Finish();
                return;
            }

            if (elapsed < RiseDuration)
            {
                // 上浮 + 淡入
                float t = (float)elapsed / RiseDuration;
                Location = new Point(0, _baseY - (int)(RiseDistance * t));
                Opacity = t;
            }
            else if (elapsed < RiseDuration + HoldDuration)
            {
                // 停留
                Location = new Point(0, _baseY - RiseDistance);
                Opacity = 1F;
            }
            else
            {
                // 淡出（保持终点位置）
                float t = (float)(elapsed - RiseDuration - HoldDuration) / FadeDuration;
                Location = new Point(0, _baseY - RiseDistance);
                Opacity = 1F - t;
            }
        }

        private void Finish()
        {
            _finished = true;
            AfterDraw -= OnAfterDraw;
            Active.Remove(this);
            Dispose(true);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                AfterDraw -= OnAfterDraw;
                Active.Remove(this);
            }
            base.Dispose(disposing);
        }
    }
}
