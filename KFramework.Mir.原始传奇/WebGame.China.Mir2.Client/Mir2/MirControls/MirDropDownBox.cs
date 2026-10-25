using Client.MirGraphics;
using WebGame.Mir2.MonoGame.Client;

namespace Client.MirControls
{
    public class MirDropDownBox : MirControl
    {
        private MirLabel _label;
        private MirButton _DropDownButton;
        private MirLabel[] _Option = new MirLabel[5];
        private MirButton _ScrollUp, _ScrollDown, _ScrollPosition;

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

        // —— 可选图集底图:设了 Library+Index,控件背景就用该图(默认拉伸填满;设了四边边框则九宫格)。——
        private MLibrary _bgLibrary;
        public MLibrary Library { get { return _bgLibrary; } set { _bgLibrary = value; Redraw(); } }
        private int _bgIndex = -1;
        public int Index { get { return _bgIndex; } set { _bgIndex = value; Redraw(); } }
        public int BorderLeft, BorderTop, BorderRight, BorderBottom; // 九宫格边框(默认 0=纯拉伸)
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
            for (int i = 0; i < _Option.Length; i++)
            {
                _Option[i].Size = new Size(Size.Width - 16, 16); //Size.Width - 13, 13
            }

            if (_label != null && !_label.IsDisposed)
                _label.Size = new Size(Size.Width - 16, 15); //Size.Width - 16, 12
            if (_DropDownButton != null && !_DropDownButton.IsDisposed)
                _DropDownButton.Location = new Point(Size.Width - 18, 2);
            if (_ScrollUp != null && !_ScrollUp.IsDisposed)
                _ScrollUp.Location = new Point(Size.Width - 12, 14);
            if (_ScrollDown != null && !_ScrollDown.IsDisposed)
                _ScrollDown.Location = new Point(Size.Width - 12, 52);
            if (_ScrollPosition != null && !_ScrollPosition.IsDisposed)
                _ScrollPosition.Location = new Point(Size.Width - 11, 22);
 
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
            for (int i = 0; i < _Option.Length; i++)
            {
                _Option[i] = new MirLabel
                {
                    Parent = this,
                    Visible = false,
                    Location = new Point(0, 15 + (i * 13)),
                    ForeColour = ForeColour,
                    BackColour = Color.FromArgb(255,20,20,20),
                    Font = new Font(Settings.FontName, 8F)
                };
                int index = i;
                _Option[index].MouseEnter += (o, e) => _Option[index].BackColour = Color.FromArgb(255,140,70,0);
                _Option[index].MouseLeave += (o, e) => _Option[index].BackColour = Color.FromArgb(255, 20, 20, 20);
                _Option[index].MouseDown += (o, e) => _Option[index].BackColour = Color.FromArgb(255, 20, 20, 20);
                _Option[index].MouseUp += (o, e) => _Option[index].BackColour = Color.FromArgb(255,20,20,20);
                _Option[index].Click += (o, e) => SelectOption(index);

                _Option[index].BeforeDraw += (o, e) =>
                {
                    if (Items.Count > (ScrollIndex + index + MinimumOption))
                        _Option[index].Text = _Items[ScrollIndex + index + MinimumOption];
                };
            }
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
            _ScrollUp = new MirButton
            {
                HoverIndex = 2022,
                Index = 2021,
                Library = Libraries.Prguse,
                Location = new Point(Size.Width - 12, 14),
                Parent = this,
                PressedIndex = 2023,
                Visible = false
            };
            _ScrollUp.Click += (o, e) => ScrollUp();
            _ScrollDown = new MirButton
            {
                HoverIndex = 2025,
                Index = 2024,
                Library = Libraries.Prguse,
                Location = new Point(Size.Width - 12, 52),
                Parent = this,
                PressedIndex = 2026,
                Visible = false
            };
            _ScrollDown.Click += (o, e) => ScrollDown();
           
            _ScrollPosition = new MirButton
            {
                HoverIndex = 2016,
                Index = 2015,
                Library = Libraries.Prguse,
                Location = new Point(Size.Width - 11, 22),
                Movable = true,
                Parent = this,
                PressedIndex = 2017,
                Visible = false
            };
            _ScrollPosition.OnMoving += ScrollPosition;
            BeforeDraw += MirDropDownBox_BeforeDraw;
        }

        protected internal override void DrawControl()
        {
            base.DrawControl(); // 先画原 BackColour 底(未设图集时即为最终外观)
            if (_bgLibrary != null && _bgIndex >= 0)
            {
                Size img = _bgLibrary.GetTrueSize(_bgIndex);
                Size drawSize = Size;
                Point drawLoc = DisplayLocation;
                Color drawColor = ForeColour;

                int borders = BorderLeft + BorderTop + BorderRight + BorderBottom;
                if (borders > 0)
                {
                    MirImageControl.ForEachNineGridRect(img.Width, img.Height, drawLoc.X, drawLoc.Y, drawSize.Width, drawSize.Height,
                        BorderLeft, BorderTop, BorderRight, BorderBottom,
                        (src, dst) => _bgLibrary.Draw(_bgIndex, src, dst, drawColor, 1f));
                }
                else
                {
                    _bgLibrary.Draw(_bgIndex, drawLoc, drawSize, drawColor);
                }
            }
        }

        void MirDropDownBox_BeforeDraw(object sender, EventArgs e)
        {
            if (!_Option[0].Visible) return;
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
            if (_Option[0].Visible)
                CloseDropDown();
            else
                OpenDropDown();
        }
        public void OpenDropDown()
        {
            int needed = Math.Max(5, Items.Count) * 15;
            int vis = Math.Min(5, Items.Count);

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
            if (Items.Count > 5)
                ScrollIndex = SelectedIndex > 3 ? SelectedIndex - 2 : 0;
            for (int i = 0; i < _Option.Length; i++)
                _Option[i].Visible = Items.Count > i;

            // 选项槽位:向下 15+i*13;向上翻到表头上方(槽位 0 在最顶)
            for (int i = 0; i < _Option.Length; i++)
                _Option[i].Location = new Point(0, _dropUp ? (needed - _Option.Length * 13 + i * 13) : (15 + i * 13));

            if (Items.Count > 5)
            {
                _ScrollDown.Visible = true;
                _ScrollPosition.Visible = true;
                _ScrollUp.Visible = true;
            }
            else
            {
                _ScrollDown.Visible = false;
                _ScrollPosition.Visible = false;
                _ScrollUp.Visible = false;
            }

            // 滚动按钮:向下在右下方,向上在右上方
            _ScrollUp.Location = new Point(Size.Width - 12, _dropUp ? 2 : 14);
            _ScrollDown.Location = new Point(Size.Width - 12, _dropUp ? needed - 13 : 9 + vis * 13);
            _ScrollPosition.Location = new Point(Size.Width - 11, _dropUp ? needed / 2 : 22);
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

            for (int i = 0; i < _Option.Length; i++)
                _Option[i].Visible = false;
            _ScrollDown.Visible = false;
            _ScrollPosition.Visible = false;
            _ScrollUp.Visible = false;
        }
        public void ScrollUp()
        {
            if (ScrollIndex > 0) ScrollIndex--;
            Update();
        }
        public void ScrollDown()
        {
            if (ScrollIndex < (Items.Count - 5)) ScrollIndex++;
            Update();
        }
        void ScrollPosition(object sender, MouseEventArgs e)
        {
            int x = Size.Width - 11;
            int y = _ScrollPosition.Location.Y;
            int top = _ScrollUp.Location.Y;
            int bottom = _ScrollDown.Location.Y - 14;
            if (y > bottom) y = bottom;
            if (y < top) y = top;

            int track = _ScrollDown.Location.Y - _ScrollUp.Location.Y + 6;
            int h = (int)((y - top) / (track / (float)(Items.Count - 1)));
            if (h > Items.Count - 5) h = Math.Max(0, Items.Count - 5);
            if (h != ScrollIndex)
            {
                ScrollIndex = h;
                Update();
            }

            _ScrollPosition.Location = new Point(x, y);
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

            if (_ScrollDown != null && !_ScrollDown.IsDisposed)
                _ScrollDown.Dispose();
            _ScrollDown = null;

            if (_ScrollPosition != null && !_ScrollPosition.IsDisposed)
                _ScrollPosition.Dispose();
            _ScrollPosition = null;

            if (_ScrollUp != null && !_ScrollUp.IsDisposed)
                _ScrollUp.Dispose();
            _ScrollUp = null;
            for (int i = 0; i < _Option.Length; i++)
            {
                if (_Option[i] != null && !_Option[i].IsDisposed)
                    _Option[i].Dispose();
                _Option[i] = null;
            }
        }
    }
}