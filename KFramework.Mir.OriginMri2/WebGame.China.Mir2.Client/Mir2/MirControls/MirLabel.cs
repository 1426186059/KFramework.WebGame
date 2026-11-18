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
                // 统一走 RichTextObject：无标签时它内部按普通文本快路径度量，有标签时剥离标签后度量。
                SyncRich();
                Size = TextRenderer.MeasureText(_rich, _drawFormat);
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
        /// 兼容属性：渲染现已统一走 <see cref="KFramework.MonoGame.RichTextObject"/>，
        /// 无标签文本按普通文本绘制、有标签时标签生效，无需再切开关。
        /// 保留此属性仅为兼容旧的对象初始化器，赋值只触发一次重绘。
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

        // 富文本对象：持有文本/字体并缓存标签解析，度量/绘制/链接命中共用，
        // 避免一次重绘里 4 次描边 + 1 次前景反复解析同一段文本。
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
            _rich.Layout(new KFramework.MonoGame.RectangleF(1, 1, Size.Width, Size.Height),
                         _drawFormat.Anchor, _drawFormat.HorizontalWrap,
                         KFramework.MonoGame.VerticalWrapMode.Overflow);
            url = TextRenderer.FindIntersectingLink(_rich,
                new KFramework.MonoGame.Vector2(x - loc.X, y - loc.Y));
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
                    // 统一走 RichTextObject：无标签时内部按普通文本逐字符折行绘制，
                    // 解析与排版结果跨 4 次描边 + 1 次前景复用；描边用 outlineMode 只画去标签的纯文本。
                    SyncRich();
                    if (outline != 0)
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
