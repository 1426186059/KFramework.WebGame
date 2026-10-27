using KFramework.MonoGame;
using System;

namespace KFramework.MonoGameExtend
{
    /// <summary>
    /// 输入框（参考 Unity InputField）：点击获得焦点后键入文本（基础 ASCII + 退格 + 回车提交）。
    /// 内部用 KLabel 显示文本与闪烁光标；点击框外自动失焦。空文本且未聚焦时显示占位提示。
    /// <para>裁剪说明同 KScrollView：本框架 SpriteBatch 在 KCanvas.Begin 时锁定 rasterizer，运行时 scissor 不生效，
    /// 文本超长可能溢出框边，属已知限制（逻辑上可后续加横向滚动/截断）。</para>
    /// <para>基础版为 ASCII 输入（A-Z / 0-9 / 常见标点 / 空格 / 退格 / 回车）；中文 IME 需后续接入 Input_IME。</para>
    /// <para>用法：new KInputBox(){ Size = new Vector2(240, 32) }，挂 KCanvas 下；订阅 OnTextChanged / OnSubmit 获取结果。</para>
    /// </summary>
    public class KInputBox : KImage, IClickable
    {
        private KLabel _label;
        private string _text = string.Empty;
        private bool _focused;
        private bool _registered;
        private float _caretTimer;
        private bool _caretVisible = true;

        public event EventHandler<string> OnTextChanged;
        public event EventHandler<string> OnSubmit;

        public string Text
        {
            get => _text;
            set
            {
                string v = value ?? string.Empty;
                if (_text != v) { _text = v; OnTextChanged?.Invoke(this, _text); }
            }
        }

        public Color TextColor { get; set; } = Color.White;
        public Color PlaceholderColor { get; set; } = new Color(160, 160, 160);
        public string Placeholder { get; set; } = "请输入...";
        public bool Interactable { get; set; } = true;
        public Color FocusedColor { get; set; } = new Color(60, 60, 80);
        public Color UnfocusedColor { get; set; } = new Color(40, 40, 40);

        public KInputBox()
        {
            Color = UnfocusedColor;

            _label = new KLabel();
            _label.Parent = this;
            _label.MinMaxAnchor = RectangleF.MinMax(0, 0, 1, 1);
            _label.Anchor = Vector2.Zero;
            _label.Pivot = Vector2.Zero;
            _label.AnchorOffset = AnchorRectOffset.Zero;
            _label.Color = TextColor;
            // 不显式设 Alignment：KLabel 默认按 Pivot(0,0) 推导为左上对齐

            Register();
            Input_KeyBoard.KeyDown += OnKeyBoardDown;
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
            KInputMgr.Unregister(this);
            Input_KeyBoard.KeyDown -= OnKeyBoardDown;
            _registered = false;
        }

        public override void Dispose()
        {
            Unregister();
            base.Dispose();
        }

        private void OnKeyBoardDown(Keys key)
        {
            if (!_focused || !Interactable) return;

            if (key == Keys.Enter)
            {
                OnSubmit?.Invoke(this, _text);
                return;
            }
            if (key == Keys.Backspace)
            {
                if (_text.Length > 0) Text = _text.Substring(0, _text.Length - 1);
                return;
            }
            char c = KeyToChar(key);
            if (c != '\0') Text = _text + c;
        }

        // Keys → 可见字符。KeyDown 是边沿事件（按一次只触发一次，按住不重复）。
        // 注意：本框架 Keys 是按 HTML code 顺序声明、数值连续自动编号的枚举（见 InputDefine），
        // 但 KeyA..KeyZ / Digit0..Digit9 是连续声明的，可据此映射到 a..z / 0..9。
        private static char KeyToChar(Keys key)
        {
            bool shift = Input_KeyBoard.Shift;
            if (key >= Keys.KeyA && key <= Keys.KeyZ)
            {
                char ch = (char)('a' + (key - Keys.KeyA));
                return shift ? char.ToUpperInvariant(ch) : ch;
            }
            if (key >= Keys.Digit0 && key <= Keys.Digit9)
            {
                return (char)('0' + (key - Keys.Digit0));
            }
            switch (key)
            {
                case Keys.Space: return ' ';
                case Keys.Period: return shift ? '>' : '.';
                case Keys.Comma: return shift ? '<' : ',';
                case Keys.Minus: return shift ? '_' : '-';
                case Keys.Equal: return shift ? '+' : '=';
                case Keys.Slash: return shift ? '?' : '/';
                case Keys.Quote: return shift ? '"' : '\'';
                case Keys.Semicolon: return shift ? ':' : ';';
                case Keys.BracketLeft: return shift ? '{' : '[';
                case Keys.BracketRight: return shift ? '}' : ']';
                case Keys.Backslash: return shift ? '|' : '\\';
                case Keys.Backquote: return shift ? '~' : '`';
                default: return '\0';
            }
        }

        public override void Update()
        {
            if (_focused)
            {
                _caretTimer += KTime.deltaTime;
                _caretVisible = (_caretTimer % 1.0f) < 0.5f;
                _label.Text = _text + (_caretVisible ? "|" : "");
                _label.Color = TextColor;
                Color = FocusedColor;
            }
            else
            {
                bool empty = _text.Length == 0;
                _label.Text = empty ? Placeholder : _text;
                _label.Color = empty ? PlaceholderColor : TextColor;
                Color = UnfocusedColor;
            }

            // 点击框外失焦
            if (_focused && KInputMgr.GetMouseButtonDown(MouseButton.Left))
            {
                Vector2 mp = KInputMgr.MousePosition;
                Rectangle b = Bounds;
                if (!(mp.X >= b.Left && mp.X <= b.Right && mp.Y >= b.Top && mp.Y <= b.Bottom))
                    _focused = false;
            }
        }

        // ===== IClickable =====
        public bool RaycastEnabled => Interactable;

        public Rectangle Bounds
        {
            get
            {
                Vector2 worldSize = Size * WorldScale;
                Vector2 LeftTop = WorldPosition - Pivot * worldSize;
                return new Rectangle((int)Math.Round(LeftTop.X), (int)Math.Round(LeftTop.Y), (int)Math.Round(worldSize.X), (int)Math.Round(worldSize.Y));
            }
        }

        public int Priority { get; set; } = 0;

        public bool OnPointerDown(KPointerEventArgs args)
        {
            if (!Interactable) return false;
            _focused = true;
            _caretTimer = 0;
            return true; // 消费，阻止穿透到下层
        }

        public bool OnPointerUp(KPointerEventArgs args) => false;
        public bool OnPointerClick(KPointerEventArgs args) => false;
        public void OnPointerEnter(KPointerEventArgs args) { }
        public void OnPointerExit(KPointerEventArgs args) { }
    }
}
