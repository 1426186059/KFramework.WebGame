using Client.Envir;
using System;
using MirEngine;

//Cleaned
namespace Client.Controls
{
    public class DXLabel : DXControl
    {
        #region Static
        public static Size GetSize(string text, Font font, bool outline, int paddingBottom = 0)
        {
            if (string.IsNullOrEmpty(text))
            {
                return Size.Empty;
            }

            Size tempSize = RenderingPipelineManager.MeasureText(text, font);

            if (outline && tempSize.Width > 0 && tempSize.Height > 0)
            {
                tempSize.Width += 2;
                tempSize.Height += 2;
            }

            tempSize.Height += paddingBottom;

            return tempSize;
        }
        public static Size GetHeight(DXLabel label, int width)
        {
            Size tempSize = RenderingPipelineManager.MeasureText(label.Text, label.Font, new Size(width, 2000), label.DrawFormat);

            if (label.Outline && tempSize.Width > 0 && tempSize.Height > 0)
            {
                tempSize.Width += 2;
                tempSize.Height += 2;
            }

            return tempSize;
        }
        #endregion

        #region Properties

        #region AutoSize

        public bool AutoSize
        {
            get => _AutoSize;
            set
            {
                if (_AutoSize == value) return;

                bool oldValue = _AutoSize;
                _AutoSize = value;

                OnAutoSizeChanged(oldValue, value);
            }
        }
        private bool _AutoSize;
        public event EventHandler<EventArgs> AutoSizeChanged;
        public virtual void OnAutoSizeChanged(bool oValue, bool nValue)
        {
            TextureValid = false;
            CreateSize();

            AutoSizeChanged?.Invoke(this, EventArgs.Empty);
        }

        #endregion

        #region DrawFormat

        public TextFormatFlags DrawFormat
        {
            get => _DrawFormat;
            set
            {
                if (_DrawFormat == value) return;

                TextFormatFlags oldValue = _DrawFormat;
                _DrawFormat = value;

                OnDrawFormatChanged(oldValue, value);
            }
        }
        private TextFormatFlags _DrawFormat;
        public event EventHandler<EventArgs> DrawFormatChanged;
        public virtual void OnDrawFormatChanged(TextFormatFlags oValue, TextFormatFlags nValue)
        {
            TextureValid = false;

            DrawFormatChanged?.Invoke(this, EventArgs.Empty);
        }

        #endregion

        #region Font

        public Font Font
        {
            get => _Font;
            set
            {
                if (_Font == value) return;

                Font oldValue = _Font;
                _Font = value;

                OnFontChanged(oldValue, value);
            }
        }
        private Font _Font;
        public event EventHandler<EventArgs> FontChanged;
        public virtual void OnFontChanged(Font oValue, Font nValue)
        {
            FontChanged?.Invoke(this, EventArgs.Empty);

            TextureValid = false;
            CreateSize();
        }

        #endregion

        #region Outline

        public bool Outline
        {
            get => _Outline;
            set
            {
                if (_Outline == value) return;

                bool oldValue = _Outline;
                _Outline = value;

                OnOutlineChanged(oldValue, value);
            }
        }
        private bool _Outline;
        public event EventHandler<EventArgs> OutlineChanged;
        public virtual void OnOutlineChanged(bool oValue, bool nValue)
        {
            TextureValid = false;
            CreateSize();

            OutlineChanged?.Invoke(this, EventArgs.Empty);
        }

        #endregion

        #region DropShadow

        public bool DropShadow
        {
            get => _DropShadow;
            set
            {
                if (_DropShadow == value) return;

                bool oldValue = _DropShadow;
                _DropShadow = value;

                OnDropShadowChanged(oldValue, value);
            }
        }
        private bool _DropShadow;
        public event EventHandler<EventArgs> DropShadowChanged;
        public virtual void OnDropShadowChanged(bool oValue, bool nValue)
        {
            TextureValid = false;
            CreateSize();

            DropShadowChanged?.Invoke(this, EventArgs.Empty);
        }

        #endregion

        #region Gradient

        public bool Gradient
        {
            get => _Gradient;
            set
            {
                if (_Gradient == value) return;

                bool oldValue = _Gradient;
                _Gradient = value;

                OnGradientChanged(oldValue, value);
            }
        }
        private bool _Gradient;
        public event EventHandler<EventArgs> GradientChanged;
        public virtual void OnGradientChanged(bool oValue, bool nValue)
        {
            TextureValid = false;

            GradientChanged?.Invoke(this, EventArgs.Empty);
        }

        public Color GradientTopColour
        {
            get => _GradientTopColour;
            set
            {
                if (_GradientTopColour == value) return;

                Color oldValue = _GradientTopColour;
                _GradientTopColour = value;

                OnGradientTopColourChanged(oldValue, value);
            }
        }
        private Color _GradientTopColour;
        public event EventHandler<EventArgs> GradientTopColourChanged;
        public virtual void OnGradientTopColourChanged(Color oValue, Color nValue)
        {
            TextureValid = false;

            GradientTopColourChanged?.Invoke(this, EventArgs.Empty);
        }

        public Color GradientBottomColour
        {
            get => _GradientBottomColour;
            set
            {
                if (_GradientBottomColour == value) return;

                Color oldValue = _GradientBottomColour;
                _GradientBottomColour = value;

                OnGradientBottomColourChanged(oldValue, value);
            }
        }
        private Color _GradientBottomColour;
        public event EventHandler<EventArgs> GradientBottomColourChanged;
        public virtual void OnGradientBottomColourChanged(Color oValue, Color nValue)
        {
            TextureValid = false;

            GradientBottomColourChanged?.Invoke(this, EventArgs.Empty);
        }

        #endregion

        #region LabelStyle

        public DXLabelStyle LabelStyle
        {
            get => _LabelStyle;
            set
            {
                if (_LabelStyle == value) return;

                DXLabelStyle oldValue = _LabelStyle;
                _LabelStyle = value;

                OnLabelStyleChanged(oldValue, value);
            }
        }
        private DXLabelStyle _LabelStyle;
        public event EventHandler<EventArgs> LabelStyleChanged;
        public virtual void OnLabelStyleChanged(DXLabelStyle oValue, DXLabelStyle nValue)
        {
            UpdateLabelStyle();

            LabelStyleChanged?.Invoke(this, EventArgs.Empty);
        }

        #endregion

        #region OutlineColour

        public Color OutlineColour
        {
            get => _OutlineColour;
            set
            {
                if (_OutlineColour == value) return;

                Color oldValue = _OutlineColour;
                _OutlineColour = value;

                OnOutlineColourChanged(oldValue, value);
            }
        }
        private Color _OutlineColour;
        public event EventHandler<EventArgs> OutlineColourChanged;
        public virtual void OnOutlineColourChanged(Color oValue, Color nValue)
        {
            TextureValid = false;

            OutlineColourChanged?.Invoke(this, EventArgs.Empty);
        }

        #endregion


        #region PaddingBottom

        public int PaddingBottom
        {
            get => _PaddingBottom;
            set
            {
                if (_PaddingBottom == value) return;

                int oldValue = _PaddingBottom;
                _PaddingBottom = value;

                OnPaddingBottomChanged(oldValue, value);
            }
        }
        private int _PaddingBottom;
        public event EventHandler<EventArgs> PaddingBottomChanged;
        public virtual void OnPaddingBottomChanged(int oValue, int nValue)
        {
            TextureValid = false;
            CreateSize();

            PaddingBottomChanged?.Invoke(this, EventArgs.Empty);
        }

        #endregion


        public override void OnTextChanged(string oValue, string nValue)
        {
            base.OnTextChanged(oValue, nValue);

            TextureValid = false;
            CreateSize();
        }
        public override void OnForeColourChanged(Color oValue, Color nValue)
        {
            base.OnForeColourChanged(oValue, nValue);

            TextureValid = false;
        }
        #endregion

        public DXLabel()
        {
            BackColour = Color.Empty;
            DrawTexture = true;
            AutoSize = true;
            Font = new Font(Config.FontName, CEnvir.FontSize(8F));
            DrawFormat = TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix;

            Outline = true;
            ForeColour = Constants.PrimaryColour;
            OutlineColour = Color.Black;
            GradientTopColour = Color.Empty;
            GradientBottomColour = Color.Empty;
        }

        #region Methods
        private void CreateSize()
        {
            if (!AutoSize) return;

            Size = GetSize(Text, Font, Outline, PaddingBottom);
        }

        private void UpdateLabelStyle()
        {
            switch (LabelStyle)
            {
                case DXLabelStyle.Title:
                    Outline = true;
                    OutlineColour = Color.Black;
                    Gradient = true;
                    GradientTopColour = Color.FromArgb(255, 226, 113);
                    GradientBottomColour = Color.FromArgb(226, 171, 55);
                    break;
                case DXLabelStyle.GameStoreTopRank:
                    Outline = true;
                    OutlineColour = Color.Black;
                    Gradient = true;
                    GradientTopColour = Color.FromArgb(245, 248, 255);
                    GradientBottomColour = Color.FromArgb(151, 184, 255);
                    break;
                default:
                    Gradient = false;
                    GradientTopColour = Color.Empty;
                    GradientBottomColour = Color.Empty;
                    break;
            }
        }

        private RenderTexture _labelTextureHandle;

        protected override void CreateTexture()
        {
            int width = DisplayArea.Width;
            int height = DisplayArea.Height;

            if (!ControlTexture.IsValid || DisplayArea.Size != TextureSize)
            {
                DisposeTexture();
                TextureSize = DisplayArea.Size;
                _labelTextureHandle = RenderingPipelineManager.CreateTexture(TextureSize, RenderTextureFormat.A8R8G8B8, RenderTextureUsage.None, RenderTexturePool.Managed);

                ControlTexture = _labelTextureHandle;
                RenderingPipelineManager.RegisterControlCache(this);
            }

            bool gradient = Gradient && (!GradientTopColour.IsEmpty || !GradientBottomColour.IsEmpty);
            int handle = (int)_labelTextureHandle.NativeHandle;
            int fore = ForeColour.ToArgb();
            int outline = Outline ? OutlineColour.ToArgb() : -1;
            int back = BackColour.IsEmpty ? -1 : BackColour.ToArgb();
            int gTop = GradientTopColour.IsEmpty ? fore : GradientTopColour.ToArgb();
            int gBot = GradientBottomColour.IsEmpty ? fore : GradientBottomColour.ToArgb();

            // 浏览器端用 WebGL 渲染文字（见 BrowserCanvas.DrawLabel / tsengine/src/render/webgl/webgl2d.ts 的 mir.drawLabel），
            // 不再使用 GDI 的 Bitmap/Graphics/TextRenderer（WASM 无 gdiplus.dll）。
            MirEngine.BrowserCanvas.DrawLabel(handle, width, height, Text ?? string.Empty, Font.ToCss(),
                fore, outline, (int)DrawFormat, back, gTop, gBot, gradient);

            TextureValid = true;
            ExpireTime = CEnvir.Now + Config.CacheDuration;
        }


        public override void DisposeTexture()
        {
            if (_labelTextureHandle.IsValid)
            {
                RenderingPipelineManager.ReleaseTexture(_labelTextureHandle);
                _labelTextureHandle = default;
            }

            base.DisposeTexture();
        }
        protected override void DrawControl()
        {
            if (!DrawTexture)
            {
                return;
            }

            if (!TextureValid)
            {
                CreateTexture();
            }

            float oldOpacity = RenderingPipelineManager.GetOpacity();

            RenderingPipelineManager.SetOpacity(Opacity);

            PresentTexture(ControlTexture, Parent, DisplayArea, IsEnabled ? Color.White : Color.FromArgb(75, 75, 75), this);

            RenderingPipelineManager.SetOpacity(oldOpacity);

            ExpireTime = CEnvir.Now + Config.CacheDuration;
        }

        internal void DrawTextureTo(RectangleF destination)
        {
            if (!DrawTexture) return;

            if (!TextureValid)
                CreateTexture();

            if (!ControlTexture.IsValid || TextureSize.Width <= 0 || TextureSize.Height <= 0)
                return;

            RenderingPipelineManager.DrawTexture(ControlTexture, new Rectangle(Point.Empty, TextureSize), destination, IsEnabled ? Color.White : Color.FromArgb(75, 75, 75));

            ExpireTime = CEnvir.Now + Config.CacheDuration;
        }
        #endregion

        #region IDisposable
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
            {
                _AutoSize = false;
                _DrawFormat = TextFormatFlags.Default;
                _Font?.Dispose();
                _Font = null;
                _Outline = false;
                _Gradient = false;
                _LabelStyle = DXLabelStyle.None;
                _GradientTopColour = Color.Empty;
                _GradientBottomColour = Color.Empty;
                _OutlineColour = Color.Empty;

                AutoSizeChanged = null;
                DrawFormatChanged = null;
                FontChanged = null;
                OutlineChanged = null;
                GradientChanged = null;
                LabelStyleChanged = null;
                GradientTopColourChanged = null;
                GradientBottomColourChanged = null;
                OutlineColourChanged = null;
            }
        }
        #endregion
    }

    public enum DXLabelStyle
    {
        None,
        Title,
        GameStoreTopRank
    }
}
