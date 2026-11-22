using Client.MirGraphics;

namespace Client.MirControls
{
    public class MirLabel : MirControl
    {
        private bool _autoSize;
        public bool AutoSize
        {
            get { return _autoSize; }
            set
            {
                if (_autoSize == value)
                    return;
                _autoSize = value;
                OnAutoSizeChanged(EventArgs.Empty);
            }
        }
        public event EventHandler AutoSizeChanged;
        private void OnAutoSizeChanged(EventArgs e)
        {
            TextureValid = false;
            GetSize();
            if (AutoSizeChanged != null)
                AutoSizeChanged.Invoke(this, e);
        }

        private TextStyle _drawFormat = TextStyle.Default;
        public TextStyle DrawFormat
        {
            get { return _drawFormat; }
            set
            {
                _drawFormat = value;
                OnDrawFormatChanged(EventArgs.Empty);
            }
        }

        public event EventHandler DrawFormatChanged;
        private void OnDrawFormatChanged(EventArgs e)
        {
            TextureValid = false;

            if (DrawFormatChanged != null)
                DrawFormatChanged.Invoke(this, e);
        }

        private Font _font;
        public Font Font
        {
            get { return _font; }
            set
            {
                _font = ScaleFont(value);
                OnFontChanged(EventArgs.Empty);
            }
        }
        public event EventHandler FontChanged;
        private void OnFontChanged(EventArgs e)
        {
            TextureValid = false;

            GetSize();

            if (FontChanged != null)
                FontChanged.Invoke(this, e);
        }

        private bool _outLine;
        public bool OutLine
        {
            get { return _outLine; }
            set
            {
                if (_outLine == value)
                    return;
                _outLine = value;
                OnOutLineChanged(EventArgs.Empty);
            }
        }
        public event EventHandler OutLineChanged;
        private void OnOutLineChanged(EventArgs e)
        {
            TextureValid = false;
            GetSize();
            
            if (OutLineChanged != null)
                OutLineChanged.Invoke(this, e);
        }

        private Color _outLineColour;
        public Color OutLineColour
        {
            get { return _outLineColour; }
            set
            {
                if (_outLineColour == value)
                    return;
                _outLineColour = value;
                OnOutLineColourChanged();
            }
        }
        public event EventHandler OutLineColourChanged;
        private void OnOutLineColourChanged()
        {
            TextureValid = false;

            if (OutLineColourChanged != null)
                OutLineColourChanged.Invoke(this, EventArgs.Empty);
        }

        // 下划线装饰（opt-in）：引擎字体走 Canvas2D，FontStyle 无 Underline，这里在文字底部画一条 1px 线。
        private bool _underLine;
        public bool UnderLine
        {
            get { return _underLine; }
            set
            {
                if (_underLine == value)
                    return;
                _underLine = value;
                TextureValid = false;
            }
        }

        private void GetSize()
        {
            if (!AutoSize)
                return;

            if (string.IsNullOrEmpty(_text))
            {
                Size = Size.Empty;
            }
            else
            {
                SyncRich();
                Size = TextRenderer.MeasureText(_rich, _drawFormat);
                if (OutLine && Size != Size.Empty)
                {
                    Size = new Size(Size.Width + 2, Size.Height + 2);
                }
            }
        }

        private string _text;
        public string Text
        {
            get { return _text; }
            set
            {
                if (_text == value)
                {
                    return;
                }

                _text = value;
                OnTextChanged(EventArgs.Empty);
            }
        }
        
        private bool _richText;
        public bool RichText
        {
            get
            {
                return _richText;
            }
            set
            {
                if (_richText == value)
                {
                    return;
                }
                _richText = value;
                OnTextChanged(EventArgs.Empty);   // 重新度量与重绘
            }
        }

        private readonly KFramework.MonoGame.RichTextObject _rich = new KFramework.MonoGame.RichTextObject();
        private void SyncRich()
        {
            _rich.SetText(Text);
            _rich.SetFont(Font);
        }

        /// <summary>命中超链接后是否自动用默认浏览器打开其 url。默认 true；
        /// 置 false 时可只订阅 <see cref="LinkClick"/> 自行处理（如走游戏内协议）。</summary>
        public bool AutoOpenLink = true;

        /// <summary>点击到 &lt;link&gt;/&lt;a href&gt; 文字时触发，Url 为标签里的地址。</summary>
        public event EventHandler<LinkClickEventArgs> LinkClick;

        public sealed class LinkClickEventArgs : EventArgs
        {
            public readonly string Url;

            public LinkClickEventArgs(string url)
            {
                Url = url;
            }
        }

        /// <summary>把 UI(逻辑)坐标的鼠标点换算到本控件局部坐标，判断是否落在某个超链接上。
        /// <paramref name="x"/>/<paramref name="y"/> 传 MouseEventArgs 的 X/Y（与 DisplayLocation 同一坐标系）。</summary>
        public bool TryGetLinkAt(int x, int y, out string url)
        {
            url = null;
            if (!Visible || string.IsNullOrEmpty(Text))
            {
                return false;
            }

            Point loc = DisplayLocation;
            SyncRich();
            // 保证 TextInfo 按当前绘制参数排版（与 CreateTexture 的绘制矩形一致）。
            _rich.Layout(new RectangleF(1, 1, Size.Width, Size.Height),
                         _drawFormat.Anchor, 
                         _drawFormat.HorizontalWrap,
                         _drawFormat.VerticalWrap);
            url = TextRenderer.FindIntersectingLink(_rich,
                new Vector2(x - loc.X, y - loc.Y));
            return url != null;
        }

        public override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);

            string url;
            if (!TryGetLinkAt(e.X, e.Y, out url) || string.IsNullOrEmpty(url))
            {
                return;
            }

            if (LinkClick != null)
            {
                LinkClick.Invoke(this, new LinkClickEventArgs(url));
            }

            if (AutoOpenLink)
            {
                BrowserHelper.OpenDefaultBrowser(url);
            }
        }

        public event EventHandler TextChanged;
        private void OnTextChanged(EventArgs e)
        {
            DrawControlTexture = !string.IsNullOrEmpty(Text);
            TextureValid = false;
            Redraw();

            GetSize();

            if (TextChanged != null)
                TextChanged.Invoke(this, e);
        }

        public MirLabel()
        {
            DrawControlTexture = true;
            _drawFormat = new KFramework.MonoGame.TextStyle(KFramework.MonoGame.TextAnchor.UpperLeft, KFramework.MonoGame.HorizontalWrapMode.Wrap);

            _font = ScaleFont(new Font(Settings.FontName, 12F));
            _outLine = true;
            _outLineColour = CommonColor.Black; 
            _text = string.Empty;
        }
        
        protected override void CreateTexture()
        {
            if (string.IsNullOrEmpty(Text))
                return;

            if (Size.Width == 0 || Size.Height == 0)
                return;

            if (TextureSize != Size)
                DisposeTexture();

            if (ControlTexture == null || ControlTexture.Disposed)
            {
                DXManager.ControlList.Add(this);
                ControlTexture = DXManager.CreateRenderTarget(Size.Width, Size.Height);
                TextureSize = Size;
            }

            Color fore = ForeColour;
            Color outline = OutLine ? OutLineColour : CommonColor.Empty;
            Color back = BackColour;

            // 垂直一律 Overflow：Truncate 的"放不下整行不画"会让高度略小的标签整行消失。
            KFramework.MonoGame.TextStyle format = _drawFormat;

            // 目标、清屏、批三件事都由调用方管（GDI 与游戏渲染已合并为一个渲染系统）：
            // 下面最多 5 次 DrawText（4 次描边 + 1 次前景）共用一次 Begin/End → 合成 1 次 draw call；
            // 若让 TextRenderer 每次自己开收批，就是 5 段。
            var savedSurface = DXManager.CurrentSurface;
            DXManager.SetSurface(new SlimDX.Direct3D9.Surface(ControlTexture));
            DXManager.GDevice.Clear(back);
            try
            {
                DXManager.BeginSprite(MirBlend.Normal);
                try
                {
                    // 描边对齐原版 MirLabel：四向各偏移 1px 先画轮廓，再画前景。
                    // 统一走 RichTextObject：无标签时内部按普通文本逐字符折行绘制，
                    // 解析与排版结果跨 4 次描边 + 1 次前景复用；描边用 outlineMode 只画去标签的纯文本。
                    SyncRich();
                    if (outline.PackedValue != 0)
                    {
                        TextRenderer.DrawText(_rich, new Rectangle(1, 0, Size.Width, Size.Height), outline, format, true);
                        TextRenderer.DrawText(_rich, new Rectangle(0, 1, Size.Width, Size.Height), outline, format, true);
                        TextRenderer.DrawText(_rich, new Rectangle(2, 1, Size.Width, Size.Height), outline, format, true);
                        TextRenderer.DrawText(_rich, new Rectangle(1, 2, Size.Width, Size.Height), outline, format, true);
                    }
                    TextRenderer.DrawText(_rich, new Rectangle(1, 1, Size.Width, Size.Height), fore, format, false);

                    if (_underLine)
                    {
                        int uy = Size.Height - 2;
                        if (uy > 0)
                            DXManager.DrawLines(new KFramework.MonoGame.Vector2[]
                            {
                                new KFramework.MonoGame.Vector2(1, uy),
                                new KFramework.MonoGame.Vector2(Size.Width - 1, uy),
                            }, 1F, ForeColour);
                    }
                }
                finally
                {
                    DXManager.EndSprite();
                }
            }
            finally
            {
                DXManager.SetSurface(savedSurface);
            }

            TextureValid = true;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (!disposing) return;
            
            AutoSizeChanged = null;
            _autoSize = false;

            DrawFormatChanged = null;
            _drawFormat = TextStyle.Default;

            FontChanged = null;
            if (_font != null)
            {
                _font.Dispose();
                _font = null;
            }

            OutLineChanged = null;
            _outLine = false;

            OutLineColourChanged = null;
            _outLineColour = CommonColor.Empty;

            TextChanged = null;
            _text = null;
            LinkClick = null;
        }

    }
}
