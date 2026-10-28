using Client.MirGraphics;
using WebGame.Mir2.MonoGame.Client;

namespace Client.MirControls
{
    // MirDropDownBox 的变体：
    //   • 箭头按钮(DropDownButton)本身是 MirButton，已自带 AtlasFrame / AtlasNameKey 等图集能力，
    //     由外部直接配置即可(无需在控件上再挂一套背景图集)。
    //   • 选中文字直接显示在箭头按钮(DropDownButton)自身的 Label 中并居中对齐(CenterText);
    //     按钮即点击区，精确按实际尺寸右对齐 + 垂直居中落在控件内，不再写死偏移。
    //   • 无滚动条：选项随 Items.Count 动态创建，下拉框整体长高容纳全部项。
    public class MirDropDownBox2 : MirControl
    {
        private MirButton _DropDownButton;
        private List<MirLabel> _Option = new List<MirLabel>();

        // 暴露下拉箭头按钮：外部直接配置它的 AtlasFrame / AtlasNameKey(及 Hover/Pressed 帧)。
        public MirButton DropDownButton { get { return _DropDownButton; } }

        public int _SelectedIndex = -1;
        public int _WantedIndex = -1;
        public int MinimumOption = 0; // 列表起始索引偏移:下拉从第 MinimumOption 项开始显示/可选(之前的项被跳过)
        public int ScrollIndex = 0;
        public int OrigHeight = 0;
        public long LastMouseLeave = 0;

        /// <summary>展开生长方向:0=向下(默认,列表在按钮/点击区正下方,点击区在顶部不动), 1=向上(列表在按钮正上方,整框上移、点击区屏幕位置不变),
        /// 2=自动(下方空间不足且上方够则改向上生长)。</summary>
        public int DropMode { get; set; } = 0;

        /// <summary>按钮内文字相对于“居中”位置的左右偏移(正=右移,负=左移)。配合 CenterText 使用。</summary>
        public int ButtonTextOffsetX
        {
            get { return _btnTextOffX; }
            set { _btnTextOffX = value; ApplyButtonTextOffset(); }
        }
        /// <summary>按钮内文字相对于“居中”位置的上下偏移(正=下移,负=上移)。配合 CenterText 使用。</summary>
        public int ButtonTextOffsetY
        {
            get { return _btnTextOffY; }
            set { _btnTextOffY = value; ApplyButtonTextOffset(); }
        }
        private int _btnTextOffX;
        private int _btnTextOffY;
        private bool _dropUp;        // 本次展开是否向上(每次 Open 时依 DropMode 计算)
        private int _collapsedY;     // 收起时的 Location.Y,向上展开后关闭时还原

        // 布局常量与状态
        private const int RowHeight = 16;  // 选项行高(同时也是行间距,行与行紧贴不重叠)
        private int _headerY;             // 当前表头(选中文字区)的 Y
        private int _headerHeight;        // 当前表头高度(折叠=OrigHeight;展开时仍为表头自身高度)

        private List<String> _Items = new List<string>();
        public event EventHandler ValueChanged;

        /// <summary>下拉展开/收起状态变化事件:状态改变后触发,上层可据此翻转箭头、改表头等。</summary>
        public event EventHandler<DropDownStateEventArgs> DropDownStateChanged;
        /// <summary>当前是否处于展开(弹出列表)状态。</summary>
        public bool IsDropDownOpen { get; private set; }

        public int SelectedIndex
        {
            get { return _SelectedIndex; }
            set
            {
                _SelectedIndex = value;
                if (_SelectedIndex >= Items.Count)
                    _SelectedIndex = -1;
            }
        }

        public List<String> Items
        {
            get { return _Items; }
            set { _Items = value; ItemsChanged(); }
        }


        protected override void OnBackColourChanged()
        {
            base.OnBackColourChanged();
        }

        // 列表项整体布局:图集异步加载完/尺寸变化时会重新摆放箭头按钮,避免偏位。
        // 取箭头按钮当前的实际矩形,列表据此对齐到按钮正下方(列表宽度/偏移与按钮完全一致)。
        private void GetButtonRect(out int btnX, out int btnW)
        {
            if (_DropDownButton == null || _DropDownButton.IsDisposed || _DropDownButton.Size.Width <= 0)
            {
                btnW = Size.Width; btnX = 0;   // 按钮尚未取到尺寸,列表回退为整宽
            }
            else
            {
                btnX = _DropDownButton.Location.X;
                btnW = _DropDownButton.Size.Width;
            }
        }

        // 列表项与表头不在同一垂直区域(按钮在表头,列表在其上/下方):
        // 列表宽度与箭头按钮一致,且 X 对齐到按钮下方,这样列表正好落在按钮正下方、不偏左。
        private void RelayoutHeader()
        {
            GetButtonRect(out int btnX, out int btnW);
            for (int i = 0; i < _Option.Count; i++)
            {
                _Option[i].Location = new Point(btnX, _Option[i].Location.Y);
                _Option[i].Size = new Size(Math.Max(0, btnW), RowHeight);
            }

            LayoutDropDownButton();
        }

        // 把箭头按钮摆到表头区并铺满控件宽度(左对齐 x=0):按钮宽度与 MirDropDownBox2 的 Size.Width 一致。
        // 关闭 AutoSize 以尊重显式尺寸;高度保留自然高度、垂直居中。
        private void LayoutDropDownButton()
        {
            if (_DropDownButton == null || _DropDownButton.IsDisposed) return;
            int headerH = _headerHeight > 0 ? _headerHeight : (OrigHeight > 0 ? OrigHeight : RowHeight);
            int btnH = _DropDownButton.Size.Height;   // 当前显示高度(图集或显式)
            _DropDownButton.AutoSize = false;
            _DropDownButton.Size = new Size(Size.Width, btnH);
            _DropDownButton.Location = new Point(0, _headerY + Math.Max(0, (headerH - _DropDownButton.Size.Height) / 2));
        }

        protected override void OnSizeChanged()
        {
            base.OnSizeChanged();
            OrigHeight = OrigHeight == 0 ? Size.Height : OrigHeight;
            _headerY = 0;
            _headerHeight = OrigHeight;
            RelayoutHeader();
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

        public MirDropDownBox2()
        {
            BackColour = Color.FromArgb(255, 6, 6, 6);
            ForeColour = Color.White;
            Enabled = false;
            // 箭头按钮(DropDownButton)本身就是点击区:选中文字直接显示在它的 Label 里(见下方 BeforeDraw),
            // 不再使用独立的左侧 Label。外部配 DropDownButton.AtlasFrame / AtlasNameKey 即可。
            _DropDownButton = new MirButton
            {
                Parent = this,
                Visible = false,
                CenterText = true,   // 选中文字在按钮内居中对齐
            };
            _DropDownButton.Click += (o, e) => DropDownClick();
            // 图集异步加载完成 / 按钮尺寸变化(如 AutoSize 取到实际图集尺寸)时,重新摆放按钮与文字偏移,避免偏位。
            _DropDownButton.AtlasLoaded += (o, e) => RelayoutHeader();
            _DropDownButton.SizeChanged += (o, e) => { RelayoutHeader(); ApplyButtonTextOffset(); };
            ApplyButtonTextOffset();   // 初始偏移(默认 0,即纯居中)
            // 选中文字显示在按钮自身的 Label 中(不再用独立的左侧 Label)
            _DropDownButton.BeforeDraw += (o, e) =>
            {
                if (Items.Count > 0 && SelectedIndex >= 0 && SelectedIndex < Items.Count)
                    _DropDownButton.Text = Items[SelectedIndex];
                else if (SelectedIndex == -1)
                    _DropDownButton.Text = " ";
                else
                    _DropDownButton.Text = "None";
            };
            BeforeDraw += MirDropDownBox2_BeforeDraw;
        }

        // 把按钮内文字 Label 从“居中”位置按偏移量平移(CenterText 已让 Label 充满按钮并居中)。
        // 设 ButtonTextOffsetX/Y 即可微调文字在按钮中的位置,避免文字压到箭头或被裁切。
        private void ApplyButtonTextOffset()
        {
            if (_DropDownButton == null || _DropDownButton.IsDisposed) return;
            var lbl = _DropDownButton.Label;
            if (lbl == null || lbl.IsDisposed) return;
            lbl.Location = new Point(_btnTextOffX, _btnTextOffY);
        }

        void MirDropDownBox2_BeforeDraw(object sender, EventArgs e)
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

        private void OnDropDownStateChanged()
        {
            DropDownStateChanged?.Invoke(this, new DropDownStateEventArgs(IsDropDownOpen));
        }

        // 按需创建选项标签,确保至少有 count 个(超出保留,收起时隐藏即可)。去掉固定 5 项上限。
        private void EnsureOptions(int count)
        {
            GetButtonRect(out int btnX, out int btnW);
            while (_Option.Count < count)
            {
                int index = _Option.Count;
                var lbl = new MirLabel
                {
                    Parent = this,
                    Visible = false,
                    Location = new Point(btnX, index * RowHeight),
                    Size = new Size(Math.Max(0, btnW), RowHeight),
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
            int listH = Items.Count * RowHeight;   // 列表区高度(随项数),不再写死 5 行

            // 生长方向:_dropUp=false 向下(默认,列表在按钮下方,点击区在顶部不动);
            // true 向上(列表在按钮上方,整框上移、点击区屏幕位置不变)。
            switch (DropMode)
            {
                case 1:
                    _dropUp = true;
                    break;
                case 2:
                    int spaceBelow = Settings.ScreenHeight - (DisplayLocation.Y + OrigHeight);
                    _dropUp = spaceBelow < listH && DisplayLocation.Y >= listH;
                    break;
                default:
                    _dropUp = false;   // 默认向下展开
                    break;
            }

            _collapsedY = Location.Y;
            Size = new Size(Size.Width, OrigHeight + listH);
            if (_dropUp)
                Location = new Point(Location.X, _collapsedY - listH);

            // 表头(点击区:选中文字 + 箭头按钮):向下在顶部(0),向上在底部(listH)。
            _headerY = _dropUp ? listH : 0;
            _headerHeight = OrigHeight;
            RelayoutHeader();   // 摆放 _DropDownButton(箭头按钮=点击区)

            ScrollIndex = 0;
            EnsureOptions(Items.Count);   // 动态补足选项标签,去掉固定 5 项上限
            for (int i = 0; i < _Option.Count; i++)
                _Option[i].Visible = i < Items.Count;

            // 选项槽位:横向对齐到按钮下方(btnX),纵向向下紧跟表头(OrigHeight + i*RowHeight)或向上在表头上方(i*RowHeight)。
            GetButtonRect(out int btnX, out int btnW);
            for (int i = 0; i < _Option.Count; i++)
                _Option[i].Location = new Point(btnX, _dropUp ? (i * RowHeight) : (OrigHeight + i * RowHeight));

            IsDropDownOpen = true;
            OnDropDownStateChanged();
        }

        public void CloseDropDown()
        {
            Size = new Size(Size.Width, OrigHeight);
            if (_dropUp)
                Location = new Point(Location.X, _collapsedY);
            _dropUp = false;

            // 还原折叠布局:收起后控件高度变回 OrigHeight,表头(点击区)回到顶部。
            _headerY = 0;
            _headerHeight = OrigHeight;
            RelayoutHeader();   // 摆放 _DropDownButton(箭头按钮=点击区)

            for (int i = 0; i < _Option.Count; i++)
                _Option[i].Visible = false;

            IsDropDownOpen = false;
            OnDropDownStateChanged();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (!disposing) return;

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

    // 下拉展开/收起状态变化事件的参数:携带当前是否展开。
    public class DropDownStateEventArgs : EventArgs
    {
        public bool IsOpen { get; }
        public DropDownStateEventArgs(bool isOpen) { IsOpen = isOpen; }
    }
}
