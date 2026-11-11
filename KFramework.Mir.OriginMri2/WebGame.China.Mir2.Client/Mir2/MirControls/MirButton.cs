using Client.MirSounds;

namespace Client.MirControls
{
    public class MirButton : MirImageControl
    {
        public virtual Color FontColour
        {
            get
            {
                if (_label != null && !_label.IsDisposed)
                    return _label.ForeColour;
                return Color.Empty;
            }
            set
            {
                if (_label != null && !_label.IsDisposed)
                    _label.ForeColour = value;
            }
        }

        private int _hoverIndex;
        public int HoverIndex
        {
            get { return _hoverIndex; }
            set
            {
                if (_hoverIndex == value)
                    return;
                _hoverIndex = value;
                OnHoverIndexChanged();
            }
        }
        public event EventHandler HoverIndexChanged;
        private void OnHoverIndexChanged()
        {
            if (HoverIndexChanged != null)
                HoverIndexChanged.Invoke(this, EventArgs.Empty);
        }

        public override int Index
        {
            get
            {
                if (!Enabled)
                    return _disabledIndex >= 0 ? _disabledIndex : base.Index;

                if (_pressedIndex >= 0 && ActiveControl == this && MouseControl == this)
                    return _pressedIndex;

                if (_hoverIndex >= 0 && MouseControl == this)
                    return _hoverIndex;

                return base.Index;
            }
            set { base.Index = value; }
        }

        // —— 图集版状态帧：与 Index 的状态选择逻辑一致，但作用于 AtlasFrame（同一图集内的不同帧名）。
        private string _hoverAtlasFrame;
        public string HoverAtlasFrame
        {
            get { return _hoverAtlasFrame; }
            set { _hoverAtlasFrame = value; Redraw(); }
        }

        private string _pressedAtlasFrame;
        public string PressedAtlasFrame
        {
            get { return _pressedAtlasFrame; }
            set { _pressedAtlasFrame = value; Redraw(); }
        }

        private string _disabledAtlasFrame;
        public string DisabledAtlasFrame
        {
            get { return _disabledAtlasFrame; }
            set { _disabledAtlasFrame = value; Redraw(); }
        }

        public override string AtlasFrame
        {
            get
            {
                if (!Enabled)
                    return !string.IsNullOrEmpty(_disabledAtlasFrame) ? _disabledAtlasFrame : base.AtlasFrame;

                if (!string.IsNullOrEmpty(_pressedAtlasFrame) && ActiveControl == this && MouseControl == this)
                    return _pressedAtlasFrame;

                if (!string.IsNullOrEmpty(_hoverAtlasFrame) && MouseControl == this)
                    return _hoverAtlasFrame;

                return base.AtlasFrame;
            }
            set { base.AtlasFrame = value; }
        }

        protected MirLabel _label;
        public MirLabel Label
        {
            get { return _label; }
        }

        protected MirImageControl _labelImage;
        public MirImageControl LabelImage
        {
            get { return _labelImage; }
            set { _labelImage = value; }
        }

        protected bool _center;
        public virtual bool CenterText
        {
            get
            {
                return _center;
            }
            set
            {
                _center = value;
                if (_center)
                {
                    _label.Size = Size;
                    _label.DrawFormat = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter;
                }
                else
                    _label.AutoSize = true;
            }
        }

        private int _pressedIndex;
        public int PressedIndex
        {
            set
            {
                if (_pressedIndex == value)
                    return;
                _pressedIndex = value;
                OnPressedIndexChanged();
            }
            get { return _pressedIndex; }
        }

        public event EventHandler PressedIndexChanged;
        private void OnPressedIndexChanged()
        {
            if (PressedIndexChanged != null)
                PressedIndexChanged.Invoke(this, EventArgs.Empty);
        }

        private int _disabledIndex;
        public int DisabledIndex
        {
            set
            {
                if (_disabledIndex == value)
                    return;
                _disabledIndex = value;
                OnDisabledIndexChanged();
            }
            get { return _disabledIndex; }
        }

        public event EventHandler DisabledIndexChanged;
        private void OnDisabledIndexChanged()
        {
            if (DisabledIndexChanged != null)
                DisabledIndexChanged.Invoke(this, EventArgs.Empty);
        }

        // —— 禁用态统一表现 ——
        // 不可点击由 MirControl.OnMouseClick 的 !Enabled 拦截，这里只管视觉置灰：
        // GrayScale 由 MirImageControl.DrawControl 应用（AtlasFrame / Library 通道）。
        // 已有专门禁用帧（DisabledIndex / DisabledAtlasFrame）或不想置灰的按钮，把开关设为 false。
        public bool AutoGrayWhenDisabled { get; set; } = true;
        public Color DisabledLabelColour { get; set; } = Color.Gray;
        private Color? _enabledLabelColour;

        protected override void OnEnabledChanged()
        {
            base.OnEnabledChanged();

            if (AutoGrayWhenDisabled)
                GrayScale = !Enabled;

            // 内建文字层是独立子控件，父级的 GrayScale 不会作用到它；
            // 记住并还原启用态颜色，避免覆盖使用方通过 FontColour 设过的自定义色。
            if (_label == null || _label.IsDisposed) return;
            if (!Enabled)
            {
                if (_enabledLabelColour == null)
                    _enabledLabelColour = _label.ForeColour;
                _label.ForeColour = DisabledLabelColour;
            }
            else if (_enabledLabelColour != null)
            {
                _label.ForeColour = _enabledLabelColour.Value;
                _enabledLabelColour = null;
            }
        }

        protected override void OnSizeChanged()
        {
            base.OnSizeChanged();

            if (_label != null && !_label.IsDisposed)
                _label.Size = Size;
        }

        public virtual string Text
        {
            set
            {
                if (_label == null || _label.IsDisposed)
                    return;
                _label.Text = value;
                _label.Visible = !string.IsNullOrEmpty(value);
            }
        }

        public bool OnlyDrawWhenActive;

        public MirButton()
        {
            HoverIndex = -1;
            PressedIndex = -1;
            DisabledIndex = -1;
            Sound = SoundList.ButtonB;

            _label = new MirLabel
                {
                    NotControl = true,
                    Parent = this,
                };

            _labelImage = new MirImageControl { Parent = this, NotControl = true, Visible = false };
        }

        protected internal override void DrawControl()
        {
            if (OnlyDrawWhenActive && ActiveControl != this && MouseControl != this)
                return;

            base.DrawControl();
        }

        protected override void Highlight()
        {
            Redraw();
            base.Highlight();
        }
        protected override void Activate()
        {
            Redraw();
            base.Activate();
        }
        protected override void Dehighlight()
        {
            Redraw();
            base.Dehighlight();
        }
        protected override void Deactivate()
        {
            Redraw();
            base.Deactivate();
        }


        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (!disposing) return;

            HoverIndexChanged = null;
            _hoverIndex = 0;

            if (_label != null && !_label.IsDisposed)
                _label.Dispose();
            _label = null;

            PressedIndexChanged = null;
            _pressedIndex = 0;

            DisabledIndexChanged = null;
            _disabledIndex = 0;
        }
    }
}
