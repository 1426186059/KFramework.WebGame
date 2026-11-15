using System;
using SlimDX;
using Client.MirSounds;
using Color = MirEngine.Color;

namespace Client.MirControls
{
    /// <summary>
    /// 皮肤化竖直滚动条（三段式）：背景轨道(九宫格) + 上下端箭头按钮 + 滑块。
    /// 四个组件（Track / UpArrow / DownArrow / Thumb）直接暴露给外部，可任意设置图集帧、九宫格边框等。
    /// <para>本控件只负责"视觉 + 命中/拖拽交互"，不持有内容数据：
    /// 宿主用 SetState(scrollable, visibleRatio, percent) 喂入状态；
    /// 用户拖动滑块/点轨道/点箭头时通过 ValueChanged(percent) / PageRequested(-1|+1) 事件把滚动意图交回宿主。</para>
    /// <para>默认图集帧取自原始传奇 y_common-sheet（bag_bar_bg / bag_bar / btn_yellow_up），
    /// 全部可覆盖。典型用法：mirScrollView.Slider.Track.AtlasNameKey = "..." 直接换肤。</para>
    /// </summary>
    public class MirSlider : MirControl
    {
        private const int Arrow = 16;       // 上下箭头高度
        private const int MinThumb = 12;    // 滑块最小高度

        private readonly MirImageControl _track;   // 背景轨道（九宫格拉伸）
        private readonly MirButton _up, _down;     // 两端箭头
        private readonly MirImageControl _thumb;   // 滑块

        // 四个组件直接暴露给外部设置（图集帧、九宫格边框、颜色、Sound 等），只读引用防止替换破坏内部事件订阅。
        public MirImageControl Track { get { return _track; } }
        public MirButton UpArrow { get { return _up; } }
        public MirButton DownArrow { get { return _down; } }
        public MirImageControl Thumb { get { return _thumb; } }

        private bool _captured;
        private int _grabOffset;
        private bool _scrollable;
        private float _visibleRatio = 1f;
        private float _percent;

        /// <summary>用户拖动滑块或点轨道后，滚动比例变化（0..1）。</summary>
        public event Action<MirSlider, float> ValueChanged;
        /// <summary>用户点上下箭头：dir = -1 上翻 / +1 下翻。</summary>
        public event Action<MirSlider, int> PageRequested;

        public MirSlider()
        {
            _track = new MirImageControl
            {
                Parent = this, NotControl = true,
                AtlasNameKey = "y_common-sheet#bag_bar_bg",
                
                DrawMode = MirImageDrawMode.NineGrid,
                BorderLeft = 3, BorderTop = 3, BorderRight = 1, BorderBottom = 1,
            };
            _up = new MirButton
            {
                Parent = this, AtlasNameKey = "y_common-sheet#bag_3",
                PressedAtlasFrame = "bag_3_0",
                Sound = SoundList.ButtonA,
            };
            _down = new MirButton
            {
                Parent = this, AtlasNameKey = "y_common-sheet#bag_31",
                PressedAtlasFrame = "bag_31_0",
                FlipY = true, Sound = SoundList.ButtonA,
            };
            _thumb = new MirImageControl
            {
                Parent = this, AtlasNameKey = "y_common-sheet#bag_bar", NotControl = false,
            };

            _up.Click += (o, e) => { if (_scrollable) PageRequested?.Invoke(this, -1); };
            _down.Click += (o, e) => { if (_scrollable) PageRequested?.Invoke(this, 1); };
            _thumb.MouseDown += OnThumbMouseDown;
            _thumb.MouseMove += OnThumbMouseMove;
            _thumb.MouseUp += OnThumbMouseUp;
        }

        /// <summary>
        /// 由宿主喂入滚动状态。
        /// scrollable=是否需要滚动条；visibleRatio=可视高度/内容高度(决定滑块长度)；percent=当前滚动比例(0..1)。
        /// </summary>
        public void SetState(bool scrollable, float visibleRatio, float percent)
        {
            _scrollable = scrollable;
            _visibleRatio = visibleRatio < 0.05f ? 0.05f : (visibleRatio > 1f ? 1f : visibleRatio);
            _percent = percent < 0f ? 0f : (percent > 1f ? 1f : percent);

            if (!scrollable)
            {
                _track.Visible = false;
                _up.Visible = false;
                _down.Visible = false;
                _thumb.Visible = false;
                return;
            }
            _track.Visible = true;
            _up.Visible = true;
            _down.Visible = true;
            Layout();
        }

        /// <summary>Size 变化后由宿主调用，重排三段。</summary>
        public void RefreshLayout()
        {
            if (_scrollable) Layout();
        }

        private void Layout()
        {
            int w = Size.Width, h = Size.Height;
            if (w <= 0 || h <= 0) return;

            _up.Location = new Point(0, 0);
            _up.Size = new Size(w, Arrow);
            _down.Location = new Point(0, h - Arrow);
            _down.Size = new Size(w, Arrow);

            int trackTop = Arrow;
            int trackH = h - Arrow * 2;
            _track.Location = new Point(0, trackTop);
            _track.Size = new Size(w, trackH);

            int thumbH = Math.Max(MinThumb, (int)(trackH * _visibleRatio));
            if (thumbH > trackH) thumbH = trackH;
            int travel = trackH - thumbH;
            _thumb.Size = new Size(w, thumbH);
            _thumb.Visible = true;
            _thumb.Location = new Point(0, trackTop + (int)(travel * _percent));
        }

        // 拖拽期间放宽命中：鼠标略微飘出滑块也能持续收到 MouseMove
        public override bool IsMouseOver(Point p) => _captured || base.IsMouseOver(p);

        private void OnThumbMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            _captured = true;
            _grabOffset = e.Y - _thumb.DisplayLocation.Y;
            _thumb.AtlasFrame = "bag_bar_0";
        }

        private void OnThumbMouseMove(object sender, MouseEventArgs e)
        {
            if (!_captured) return;
            int trackTop = Arrow;
            int trackH = Size.Height - Arrow * 2;
            int thumbH = _thumb.Size.Height;
            int travel = trackH - thumbH;
            int desiredTop = e.Y - _grabOffset - (DisplayLocation.Y + trackTop);
            float p = travel > 0 ? Math.Max(0f, Math.Min(1f, desiredTop / (float)travel)) : 0f;
            _percent = p;
            Layout();
            ValueChanged?.Invoke(this, p);
        }

        private void OnThumbMouseUp(object sender, MouseEventArgs e)
        {
            _captured = false;
            _thumb.AtlasFrame = "bag_bar";
        }

        protected override void Deactivate()
        {
            _captured = false;
            base.Deactivate();
        }
    }
}
