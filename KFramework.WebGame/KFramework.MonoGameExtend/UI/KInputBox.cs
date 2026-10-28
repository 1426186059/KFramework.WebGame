using KFramework.MonoGame;
using System;

namespace KFramework.MonoGameExtend
{
    /// <summary>
    /// 输入框（参考 Unity InputField）：本质是 <see cref="TextBox"/> 的一层 UI 封装。
    /// <para>输入链路（键盘 / 控制键 / 光标 / 选区 / IME 中文）全部委托给 <see cref="TextBox"/>——
    /// 它静态订阅了 <see cref="Input_KeyBoard"/> 与 <see cref="Input_IME"/>，在桌面与 WebGL(浏览器) 下都能正确收字；
    /// 自绘文本 / 光标 / 选区 / IME 组字预览则复用 <see cref="TextBox.DrawTextBox"/>。</para>
    /// <para>本类只负责：把自身的几何 / 可见性 / 可用性同步给 TextBox（供其全局鼠标路由与 DOM 覆盖层定位），
    /// 并把 TextBox 的事件（TextChanged / 回车）转发出去（OnTextChanged / OnSubmit）。</para>
    /// <para>裁剪说明同 KScrollView / KProgressBar：本框架运行时 scissor 不生效，超长文本可能溢出框边，属已知限制。</para>
    /// </summary>
    public class KInputBox : KImage, IClickable
    {
        private readonly TextBox _tb;
        private readonly IFont _font;
        private bool _registered;

        public event EventHandler<string> OnTextChanged;
        public event EventHandler<string> OnSubmit;

        public string Text
        {
            get => _tb.Text;
            set => _tb.Text = value;
        }

        public Color TextColor { get; set; } = Color.White;
        public Color PlaceholderColor { get; set; } = new Color(160, 160, 160);
        public string Placeholder { get; set; } = "请输入...";
        public bool Interactable { get; set; } = true;
        public Color FocusedColor { get; set; } = new Color(60, 60, 80);
        public Color UnfocusedColor { get; set; } = new Color(40, 40, 40);

        /// <summary>多行（TextBox 透传，影响回车行为 / 绘制换行）</summary>
        public bool Multiline { get => _tb.Multiline; set => _tb.Multiline = value; }
        /// <summary>最大字符数（0=不限）</summary>
        public int MaxLength { get => _tb.MaxLength; set => _tb.MaxLength = value; }

        public KInputBox()
        {
            Color = UnfocusedColor;

            _font = KDefaultRes.DefaultSpriteFont;
            _tb = new TextBox
            {
                Font = _font,
                OverlayFontPx = (int)_font.LineSpacing,
                OverlayFontCss = $"{(int)_font.LineSpacing}px sans-serif",
            };

            _tb.TextChanged += (_, _) => OnTextChanged?.Invoke(this, _tb.Text);
            // 回车提交（ESC 也走 KeyPress，但仅回车触发 OnSubmit）
            _tb.KeyPress += (_, e) =>
            {
                if (e.KeyCode == Keys.Enter) OnSubmit?.Invoke(this, _tb.Text);
            };

            Register();
        }

        public void Register()
        {
            if (_registered) return;
            KInputMgr.Register(this);
            _registered = true;
        }

        public void Unregister()
        {
            if (!_registered) return;
            if (_tb.Focused) _tb.Blur(); // 隐藏页时收起 IME 覆盖层，避免残留
            KInputMgr.Unregister(this);
            _registered = false;
        }

        public override void Dispose()
        {
            Unregister();
            _tb.Dispose(); // 从 TextBox 全局实例表移除，否则已销毁框仍会被全局鼠标路由命中
            base.Dispose();
        }

        public override void Update()
        {
            // 把几何 / 状态同步给 TextBox：供其全局鼠标路由自动聚焦，以及 WebGL 下 DOM 覆盖层定位
            Vector2 ws = Size * WorldScale;
            _tb.Location = new Point((int)Math.Round(WorldPosition.X), (int)Math.Round(WorldPosition.Y));
            _tb.Size = new Size((int)Math.Round(ws.X), (int)Math.Round(ws.Y));
            _tb.Visible = activeInHierarchy;
            _tb.Enabled = Interactable;
            _tb.Font = _font;

            // 点击框外失焦（TextBox 自身只在点到"另一个框"时互斥，点到空白不会失焦）
            if (_tb.Focused && KInputMgr.GetMouseButtonDown(MouseButton.Left))
            {
                Vector2 mp = KInputMgr.MousePosition;
                Rectangle b = Bounds;
                if (!(mp.X >= b.Left && mp.X <= b.Right && mp.Y >= b.Top && mp.Y <= b.Bottom))
                    _tb.Blur();
            }
        }

        public override void Draw()
        {
            bool focused = _tb.Focused;
            Color = focused ? FocusedColor : UnfocusedColor; // 背景随焦点变色（必须在 base.Draw 之前设置）
            base.Draw(); // 画背景框（KImage）

            Rectangle b = Bounds;
            bool empty = string.IsNullOrEmpty(_tb.Text);
            bool showPlaceholder = !focused && empty;
            string drawText = showPlaceholder ? Placeholder : _tb.Text;
            Color fore = showPlaceholder ? PlaceholderColor : TextColor;

            // 复用 TextBox 的自绘：文本 + 光标闪烁 + 选区高亮 + IME 组字预览
            _tb.DrawTextBox(
                KSceneMgr.SpriteBatch,
                KSceneMgr.Game.GraphicsDevice,
                _font,
                drawText,
                b,
                fore,
                _tb.SelectionStart,
                _tb.Multiline,
                focused,
                composition: _tb.CompositionString,
                selectionStart: _tb.SelectionStart,
                selectionLength: _tb.SelectionLength);
        }

        // ===== IClickable =====

        public bool RaycastEnabled => Interactable;

        /// <summary>命中区与 KImage.Draw 的绘制矩形一致：以 WorldPosition 为左上角。</summary>
        public Rectangle Bounds
        {
            get
            {
                Vector2 ws = Size * WorldScale;
                return new Rectangle(
                    (int)Math.Round(WorldPosition.X), (int)Math.Round(WorldPosition.Y),
                    (int)Math.Round(ws.X), (int)Math.Round(ws.Y));
            }
        }

        public int Priority { get; set; } = 0;

        public bool OnPointerDown(KPointerEventArgs args)
        {
            if (!Interactable) return false;
            _tb.Focus(); // 聚焦：建立引擎焦点链并（WebGL 下）打开 DOM 输入覆盖层
            return true;  // 消费，阻止穿透到下层
        }

        public bool OnPointerUp(KPointerEventArgs args) => false;
        public bool OnPointerClick(KPointerEventArgs args) => false;
        public void OnPointerEnter(KPointerEventArgs args) { }
        public void OnPointerExit(KPointerEventArgs args) { }
    }
}
