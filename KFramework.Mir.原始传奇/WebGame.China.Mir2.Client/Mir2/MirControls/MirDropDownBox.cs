using Client.MirGraphics;
using WebGame.Mir2.MonoGame.Client;

namespace Client.MirControls
{
    public class MirDropDownBox : MirControl
    {
        private MirLabel _label;
        private MirButton _DropDownButton;
        private List<MirLabel> _Option = new List<MirLabel>();

        // 暴露下拉箭头按钮:外部可直接配置它的图集(Library/Index 或 AtlasSheet/AtlasFrame/AtlasNameKey 等)。
        public MirButton DropDownButton { get { return _DropDownButton; } }

        public int _SelectedIndex = -1;
        public int _WantedIndex = -1;
        public int MinimumOption = 0; // 列表起始索引偏移:下拉从第 MinimumOption 项开始显示/可选(之前的项被跳过),实际索引 = ScrollIndex + 槽位 + MinimumOption
        public int ScrollIndex = 0;
        public int OrigHeight = 0;
        public long LastMouseLeave = 0;

        /// <summary>展开方向:0=向下(默认,保持原行为), 1=向上, 2=自动(按屏幕空间选择)。</summary>
        public int DropMode { get; set; } = 0;
        private bool _dropUp;        // 本次展开是否向上(每次 Open 时依 DropMode 计算)
        private int _collapsedY;     // 收起时的 Location.Y,向上展开后关闭时还原

        private List<String> _Items = new List<string>();
        public event EventHandler ValueChanged;

        public int SelectedIndex
        {
            get
            {
                return _SelectedIndex;
            }
            set
            {
                _SelectedIndex = value;
                if (_SelectedIndex >= Items.Count)
                    _SelectedIndex = -1;
            }
        }

        public List<String> Items
        {
            get
            {
                return _Items;
            }
            set
            {
                _Items = value;
                ItemsChanged();
            }
        }


        protected override void OnBackColourChanged()
        {
            base.OnBackColourChanged();
            if (_label != null && !_label.IsDisposed)
                _label.BackColour = BackColour;
        }


        protected override void OnSizeChanged()
        {
            base.OnSizeChanged();
            OrigHeight = OrigHeight == 0 ? Size.Height : OrigHeight;
            for (int i = 0; i < _Option.Count; i++)
            {
                _Option[i].Size = new Size(Size.Width - 16, 16); //Size.Width - 13, 13
            }

            if (_label != null && !_label.IsDisposed)
                _label.Size = new Size(Size.Width - 16, 15); //Size.Width - 16, 12
            if (_DropDownButton != null && !_DropDownButton.IsDisposed)
                _DropDownButton.Location = new Point(Size.Width - 18, 2);
        }


        protected override void OnEnabledChanged()
        {
            base.OnEnabledChanged();
            if (_DropDownButton != null && !_DropDownButton.IsDisposed)
            {
                _DropDownButton.Enabled = Enabled;
                if (Enabled)
                    _DropDownButton.Visible = true;
                else
                    _DropDownButton.Visible = false;
            }
        }


        public MirDropDownBox()
        {
            BackColour = Color.FromArgb(255,6,6,6);
            ForeColour = Color.White;
            Enabled = false;
            _label = new MirLabel
            {
                Parent = this,
                Location = new Point(0, 0),
                ForeColour = ForeColour,
                BackColour = BackColour,
                Font = new Font(Settings.FontName, 8F),
                Visible = true,
            };
            _label.Click += (o, e) =>
            {
                if (_DropDownButton.Enabled)
                    DropDownClick();
            };
            _label.BeforeDraw += (o, e) =>
            {
                if ((Items.Count > 0) && (Items.Count >= SelectedIndex))
                    if (SelectedIndex == -1)
                    {
                        _label.Text = " ";
                    }
                    else

                        _label.Text = Items[SelectedIndex];
                else
                    _label.Text = "None";
            };
            _DropDownButton = new MirButton
            {
                Index = 207,
                Library = Libraries.Prguse2,
                Location = new Point(Size.Width - 18, 1),
                Parent = this,
                HoverIndex = 208,
                PressedIndex = 209,
                Visible = false,
            };
            _DropDownButton.Click += (o, e) => DropDownClick();
            BeforeDraw += MirDropDownBox_BeforeDraw;
        }

        void MirDropDownBox_BeforeDraw(object sender, EventArgs e)
        {
            if (_Option.Count == 0 || !_Option[0].Visible) return;
            if (IsMouseOver(CMain.MPoint)) return;
            foreach (MirControl control in Controls)
                if (control.IsMouseOver(CMain.MPoint))
                    return;

            CloseDropDown();
        }

        protected override void OnMouseLeave()
        {
            if (!IsMouseOver(CMain.MPoint))
                CloseDropDown();
            base.OnMouseLeave();
        }

        public void SelectOption(int index)
        {
            if (ScrollIndex + index + MinimumOption < Items.Count)
            {
                _WantedIndex = ScrollIndex + index + MinimumOption;
                if (ValueChanged != null)
                    ValueChanged.Invoke(this, EventArgs.Empty);
            }
            else
                Update();
            CloseDropDown();
        }

        public void ItemsChanged()
        {
            CloseDropDown();
            SelectedIndex = -1;
        }
        public override void Update()
        {
            // Member is not implemented.
        }
        public void DropDownClick()
        {
            if (_Option.Count > 0 && _Option[0].Visible)
                CloseDropDown();
            else
                OpenDropDown();
        }
        // 按需创建选项标签,确保至少有 count 个(超出保留,收起时隐藏即可)。去掉固定 5 项上限。
        private void EnsureOptions(int count)
        {
            while (_Option.Count < count)
            {
                int index = _Option.Count;
                var lbl = new MirLabel
                {
                    Parent = this,
                    Visible = false,
                    Location = new Point(0, 15 + (index * 13)),
                    Size = new Size(Size.Width - 16, 16),
                    ForeColour = ForeColour,
                    BackColour = Color.FromArgb(255, 20, 20, 20),
                    Font = new Font(Settings.FontName, 8F)
                };
                lbl.MouseEnter += (o, e) => lbl.BackColour = Color.FromArgb(255, 140, 70, 0);
                lbl.MouseLeave += (o, e) => lbl.BackColour = Color.FromArgb(255, 20, 20, 20);
                lbl.MouseDown += (o, e) => lbl.BackColour = Color.FromArgb(255, 20, 20, 20);
                lbl.MouseUp += (o, e) => lbl.BackColour = Color.FromArgb(255, 20, 20, 20);
                lbl.Click += (o, e) => SelectOption(index);
                lbl.BeforeDraw += (o, e) =>
                {
                    if (Items.Count > (ScrollIndex + index + MinimumOption))
                        lbl.Text = _Items[ScrollIndex + index + MinimumOption];
                };
                _Option.Add(lbl);
            }
        }

        public void OpenDropDown()
        {
            int needed = Math.Max(5, Items.Count) * 15;

            // 展开方向:0=向下, 1=向上, 2=自动(下方空间不足且上方够则上拉)
            switch (DropMode)
            {
                case 0:
                    _dropUp = false;
                    break;
                case 1:
                    _dropUp = true;
                    break;
                default:
                    int spaceBelow = Settings.ScreenHeight - (DisplayLocation.Y + OrigHeight);
                    _dropUp = spaceBelow < needed && DisplayLocation.Y >= needed;
                    break;
            }

            _collapsedY = Location.Y;
            Size = new Size(Size.Width, OrigHeight + needed);
            if (_dropUp)
                Location = new Point(Location.X, _collapsedY - needed);

            // 表头:向下在顶部,向上在底部
            int headerY = _dropUp ? needed : 0;
            _label.Location = new Point(0, headerY);
            _DropDownButton.Location = new Point(Size.Width - 18, headerY + (_dropUp ? 2 : 1));

            ScrollIndex = 0;
            EnsureOptions(Items.Count);   // 动态补足选项标签,去掉固定 5 项上限
            for (int i = 0; i < _Option.Count; i++)
                _Option[i].Visible = i < Items.Count;

            // 选项槽位:向下 15+i*13;向上翻到表头上方(槽位 0 在最顶)
            for (int i = 0; i < _Option.Count; i++)
                _Option[i].Location = new Point(0, _dropUp ? (needed - _Option.Count * 13 + i * 13) : (15 + i * 13));
        }
        public void CloseDropDown()
        {
            Size = new Size(Size.Width, OrigHeight);
            if (_dropUp)
                Location = new Point(Location.X, _collapsedY);
            _dropUp = false;

            // 还原折叠布局:向上展开时表头(_label/_DropDownButton)被移到控件底部,收起后控件高度变回
            // OrigHeight,若不还原表头会落在控件之外被裁掉(字和标签背景一起消失)。
            _label.Location = new Point(0, 0);
            _DropDownButton.Location = new Point(Size.Width - 18, 1);

            for (int i = 0; i < _Option.Count; i++)
                _Option[i].Visible = false;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (!disposing) return;

            if (_label != null && !_label.IsDisposed)
                _label.Dispose();
            _label = null;

            if (_DropDownButton != null && !_DropDownButton.IsDisposed)
                _DropDownButton.Dispose();
            _DropDownButton = null;

            for (int i = 0; i < _Option.Count; i++)
            {
                if (_Option[i] != null && !_Option[i].IsDisposed)
                    _Option[i].Dispose();
                _Option[i] = null;
            }
        }
    }
}