using System;
using Client.MirGraphics;

namespace Client.MirControls
{
    // 进度条（Unity Slider/ProgressBar 风格）：自身图像作底，内嵌一个 Fill 子控件按百分比拉伸。
    public class MirProgressBar : MirImageControl
    {
        private MirImageControl _fill;

        // 填充层（背景是控件自身）。设它的 AtlasNameKey / Index / ForeColour 即可换填充图；
        // 想要纯色条就用一张白像素帧 + ForeColour 染色（白图拉伸后染成实色）。
        public MirImageControl Fill
        {
            get { return _fill; }
            private set { _fill = value; }
        }

        private int _minimum;
        public int Minimum
        {
            get { return _minimum; }
            set
            {
                if (_minimum == value) return;
                _minimum = value;
                if (_value < _minimum) Value = _minimum;
                UpdateFill();
            }
        }

        private int _maximum = 100;
        public int Maximum
        {
            get { return _maximum; }
            set
            {
                if (_maximum == value) return;
                _maximum = value;
                if (_value > _maximum) Value = _maximum;
                UpdateFill();
            }
        }

        private int _value;
        public int Value
        {
            get { return _value; }
            set
            {
                int v = Math.Max(_minimum, Math.Min(_maximum, value));
                if (_value == v) return;
                _value = v;
                UpdateFill();
                if (ValueChanged != null) ValueChanged.Invoke(this, EventArgs.Empty);
            }
        }

        public float Percent => _maximum > _minimum ? (float)(_value - _minimum) / (_maximum - _minimum) : 0f;

        private bool _vertical;
        // true=竖直（从底部向上增长）；false=水平（从左向右）。
        public bool Vertical
        {
            get { return _vertical; }
            set { if (_vertical == value) return; _vertical = value; UpdateFill(); }
        }

        public event EventHandler ValueChanged;

        public MirProgressBar()
        {
            AutoSize = false;                 // 底图按 Size 拉伸，进度条尺寸交给布局定
            Fill = new MirImageControl
            {
                Parent = this,
                AutoSize = false,             // Fill 按百分比尺寸拉伸
                NotControl = true,            // 不拦截鼠标
                Visible = true,
            };
            UpdateFill();
        }

        private void UpdateFill()
        {
            if (_fill == null) return;
            float pct = Math.Max(0f, Math.Min(1f, Percent));

            if (!Vertical)
            {
                int w = (int)Math.Round(Size.Width * pct);
                _fill.Size = new Size(Math.Max(0, w), Math.Max(0, Size.Height));
                _fill.Location = new Point(0, 0);
            }
            else
            {
                int h = (int)Math.Round(Size.Height * pct);
                _fill.Size = new Size(Math.Max(0, Size.Width), Math.Max(0, h));
                _fill.Location = new Point(0, Size.Height - _fill.Size.Height);
            }

            _fill.Visible = pct > 0f;
            _fill.Redraw();
        }

        protected override void OnSizeChanged()
        {
            base.OnSizeChanged();
            UpdateFill();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (!disposing) return;

            ValueChanged = null;
            if (_fill != null && !_fill.IsDisposed)
                _fill.Dispose();
            _fill = null;
        }
    }
}
