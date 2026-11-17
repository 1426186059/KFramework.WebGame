using MirEngine;
using Client.MirGraphics;
using SlimDX;
using SlimDX.Direct3D9;

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

        private KFramework.MonoGame.TextStyle _drawFormat = KFramework.MonoGame.TextStyle.Default;
        public KFramework.MonoGame.TextStyle DrawFormat
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
                Size = Size.Empty;
            else
            {
                Size = TextRenderer.MeasureText(Text, Font, RichText);
                //Size = new Size(Size.Width, Size.Height + 5);

                if (OutLine && Size != Size.Empty)
                    Size = new Size(Size.Width + 2, Size.Height + 2);
            }
        }

        private string _text;
        public string Text
        {
            get { return _text; }
            set
            {
                if (_text == value)
                    return;

                _text = value;
                OnTextChanged(EventArgs.Empty);
            }
        }

        /// <summary>
        /// 是否启用富文本（Unity 风格标签）：&lt;color=#RRGGBB&gt;文本&lt;/color&gt;、
        /// &lt;link="url"&gt;文本&lt;/link&gt;、&lt;b&gt;&lt;i&gt;&lt;u&gt;&lt;size=20&gt; 等。
        /// 开启后 Text 里的标签不显示、只作用于对应文字；关闭时标签当普通字符原样显示。
        /// </summary>
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
            if (!RichText || !Visible || string.IsNullOrEmpty(Text))
            {
                return false;
            }

            Point loc = DisplayLocation;
            KFramework.MonoGame.TextStyle format = new KFramework.MonoGame.TextStyle(
                _drawFormat.Anchor,
                _drawFormat.HorizontalWrap,
                KFramework.MonoGame.VerticalWrapMode.Overflow);
            url = TextRenderer.GetLinkAt(Text, Font, new Rectangle(1, 1, Size.Width, Size.Height),
                                         format, x - loc.X, y - loc.Y);
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
            _outLineColour = Color.Black; 
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

            int fore = ForeColour.ToArgb();
            int outline = OutLine ? OutLineColour.ToArgb() : 0;
            int back = BackColour.ToArgb();

            // 垂直一律 Overflow：Truncate 的"放不下整行不画"会让高度略小的标签整行消失。
            KFramework.MonoGame.TextStyle format = new KFramework.MonoGame.TextStyle(
                _drawFormat.Anchor,
                _drawFormat.HorizontalWrap,
                KFramework.MonoGame.VerticalWrapMode.Overflow);

            // 目标、清屏、批三件事都由调用方管（GDI 与游戏渲染已合并为一个渲染系统）：
            // 下面最多 5 次 DrawText（4 次描边 + 1 次前景）共用一次 Begin/End → 合成 1 次 draw call；
            // 若让 TextRenderer 每次自己开收批，就是 5 段。
            var savedSurface = DXManager.CurrentSurface;
            DXManager.SetSurface(new SlimDX.Direct3D9.Surface(ControlTexture));
            DXManager.GDevice.Clear(KFramework.MonoGame.Color.FromArgb((uint)back));
            try
            {
                DXManager.BeginSprite(MirBlend.Normal);
                try
                {
                    // 描边对齐原版 MirLabel：四向各偏移 1px 先画轮廓，再画前景。
                    if (outline != 0)
                    {
                        TextRenderer.DrawText(Text, Font, new Rectangle(1, 0, Size.Width, Size.Height), outline, format, RichText, true);
                        TextRenderer.DrawText(Text, Font, new Rectangle(0, 1, Size.Width, Size.Height), outline, format, RichText, true);
                        TextRenderer.DrawText(Text, Font, new Rectangle(2, 1, Size.Width, Size.Height), outline, format, RichText, true);
                        TextRenderer.DrawText(Text, Font, new Rectangle(1, 2, Size.Width, Size.Height), outline, format, RichText, true);
                    }
                    TextRenderer.DrawText(Text, Font, new Rectangle(1, 1, Size.Width, Size.Height), fore, format, RichText, false);

                    if (_underLine)
                    {
                        int uy = Size.Height - 2;
                        if (uy > 0)
                            DXManager.DrawLines(new SlimDX.Vector2[]
                            {
                                new SlimDX.Vector2(1, uy),
                                new SlimDX.Vector2(Size.Width - 1, uy),
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
            _drawFormat = KFramework.MonoGame.TextStyle.Default;

            FontChanged = null;
            if (_font != null)
            {
                _font.Dispose();
                _font = null;
            }

            OutLineChanged = null;
            _outLine = false;

            OutLineColourChanged = null;
            _outLineColour = Color.Empty;

            TextChanged = null;
            _text = null;
            LinkClick = null;
        }

    }
}
