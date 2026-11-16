using System;
using SlimDX;
using Client.MirSounds;
using Color = MirEngine.Color;

namespace Client.MirControls
{
    /// <summary>
    /// 皮肤化滚动条（三段式）：背景轨道(九宫格) + 两端箭头按钮 + 滑块。
    /// 四个组件（Track / UpArrow / DownArrow / Thumb）直接暴露给外部，可任意设置图集帧、九宫格边框等。
    /// <para>本控件只负责"视觉 + 命中/拖拽交互"，不持有内容数据：
    /// 宿主用 SetState(scrollable, percent) 喂入状态（scrollable=false 时仅隐藏滑块，轨道/箭头常驻）；
    /// 用户拖动滑块/点轨道/点箭头时通过 ValueChanged(percent) / PageRequested(-1|+1) 事件把滚动意图交回宿主。</para>
    /// <para>方向：Horizontal=true 走 LayoutHorizontal()（默认），false 走 LayoutVertical()；
    /// 两个布局方法均为 public，外部可直接调用调整。图集帧默认取原始传奇 y_common-sheet，全部可覆盖。</para>
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
        private float _percent;

        /// <summary>用户拖动滑块或点轨道后，滚动比例变化（0..1）。</summary>
        public event Action<MirSlider, float> ValueChanged;
        /// <summary>用户点上下箭头：dir = -1 上翻 / +1 下翻。</summary>
        public event Action<MirSlider, int> PageRequested;

        /// <summary>
        /// 布局方向：true=横向（默认，LayoutHorizontal），false=纵向（LayoutVertical）。
        /// 设置后立即重排；内部拖拽命中也随之按 X/Y 轴换算。
        /// </summary>
        private bool _horizontal = true;
        public bool Horizontal
        {
            get { return _horizontal; }
            set
            {
                if (_horizontal == value) return;
                _horizontal = value;
                ApplyLayout();
            }
        }

        public MirSlider()
        {
            _track = new MirImageControl
            {
                Parent = this, NotControl = true,
                AtlasNameKey = "y_common-sheet#bag_bar_bg",
                AutoSize = false,
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
            _thumb.MouseUp += OnThumbMouseUp;
        }

        /// <summary>
        /// 由宿主喂入滚动状态。
        /// scrollable=是否需要滚动条；percent=当前滚动比例(0..1)。滑块高度取图集原始尺寸，不参与缩放。
        /// </summary>
        public void SetState(bool scrollable, float percent)
        {
            _scrollable = scrollable;
            _percent = percent < 0f ? 0f : (percent > 1f ? 1f : percent);

            // 轨道与两端箭头常驻；仅滑块随「是否可滚动」显隐。
            // 布局始终执行：不可滚动时轨道/箭头也要停在正确位置，只是不显示 thumb。
            _track.Visible = true;
            _up.Visible = true;
            _down.Visible = true;
            _thumb.Visible = scrollable;
            ApplyLayout();
        }

        /// <summary>Size 变化后由宿主调用，重排三段。</summary>
        public void RefreshLayout()
        {
            ApplyLayout();
        }

        private void ApplyLayout()
        {
            if (_horizontal) LayoutHorizontal();
            else LayoutVertical();
        }

        /// <summary>横向布局（Horizontal=true 时默认使用，外部可直接调用）：左/右箭头 + 横向轨道 + 滑块横向滑动。</summary>
        public void LayoutHorizontal()
        {
            int w = Size.Width, h = Size.Height;
            if (w <= 0 || h <= 0) return;

            _up.Location = new Point(0, 0);
            _up.Size = new Size(Arrow, h);
            _down.Location = new Point(w - Arrow, 0);
            _down.Size = new Size(Arrow, h);

            int trackLeft = Arrow;
            int trackW = w - Arrow * 2;
            _track.Location = new Point(trackLeft, 0);
            _track.Size = new Size(trackW, h);

            // 滑块用图集帧原始宽度显示，不按可见占比缩放；高度纵向填满轨道。
            int thumbW = _thumb.TrueSize.Width;
            if (thumbW <= 0) thumbW = MinThumb;
            if (thumbW > trackW) thumbW = trackW;
            int travel = trackW - thumbW;
            _thumb.Size = new Size(thumbW, h);
            _thumb.Location = new Point(trackLeft + (int)(travel * _percent), 0);
        }

        /// <summary>纵向布局（Horizontal=false 时使用，外部可直接调用）：上/下箭头 + 纵向轨道 + 滑块纵向滑动。</summary>
        public void LayoutVertical()
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

            // 滑块用图集帧原始高度显示，不按可见占比缩放；宽度横向填满轨道。
            int thumbH = _thumb.TrueSize.Height;
            if (thumbH <= 0) thumbH = MinThumb;
            if (thumbH > trackH) thumbH = trackH;
            int travel = trackH - thumbH;
            _thumb.Size = new Size(w, thumbH);
            _thumb.Location = new Point(0, trackTop + (int)(travel * _percent));
        }

        // 拖拽期间放宽命中：鼠标飘出滑块/整个控件后，本控件仍被视为命中，
        // 保证场景层(按 IsMouseOver 逐层转发)持续把 MouseMove/Up 派发到这里。
        public override bool IsMouseOver(Point p) => _captured || base.IsMouseOver(p);

        // 拖拽中的 MouseMove 上提到本控件处理：不再依赖子控件 _thumb 的命中。
        // 鼠标一旦离开 thumb 实体区域，_thumb.IsMouseOver 会变 false，事件就到不了 _thumb.MouseMove；
        // 而本控件 IsMouseOver 在 _captured 时恒 true，能稳定收到事件，故在此统一换算滚动比例。
        public override void OnMouseMove(MouseEventArgs e)
        {
            if (_captured)
            {
                float p = _horizontal ? ThumbPercentFromMouseHorizontal(e) : ThumbPercentFromMouseVertical(e);
                _percent = p < 0f ? 0f : (p > 1f ? 1f : p);
                ApplyLayout();
                ValueChanged?.Invoke(this, _percent);
                return;
            }
            base.OnMouseMove(e);
        }

        // 松手兜底：拖拽中本控件 IsMouseOver 恒 true，可能被 Highlight 成 MouseControl，
        // 此时 mouseup 派发到本 OnMouseUp 而非 _thumb，必须在这一律解除捕获，
        // 否则 _captured 卡住 → 松开鼠标后滑块仍跟着鼠标动。
        public override void OnMouseUp(MouseEventArgs e)
        {
            if (_captured)
            {
                _captured = false;
                _thumb.AtlasFrame = "bag_bar";
            }
            base.OnMouseUp(e);
        }

        private void OnThumbMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            _captured = true;
            _grabOffset = _horizontal
                ? e.X - _thumb.DisplayLocation.X
                : e.Y - _thumb.DisplayLocation.Y;
            _thumb.AtlasFrame = "bag_bar_0";
        }

        /// <summary>横向拖拽映射：鼠标 X → 滚动比例。</summary>
        private float ThumbPercentFromMouseHorizontal(MouseEventArgs e)
        {
            int trackLeft = Arrow;
            int trackW = Size.Width - Arrow * 2;
            int thumbW = _thumb.Size.Width;
            int travel = trackW - thumbW;
            int desiredLeft = e.X - _grabOffset - (DisplayLocation.X + trackLeft);
            return travel > 0 ? Math.Max(0f, Math.Min(1f, desiredLeft / (float)travel)) : 0f;
        }

        /// <summary>纵向拖拽映射：鼠标 Y → 滚动比例。</summary>
        private float ThumbPercentFromMouseVertical(MouseEventArgs e)
        {
            int trackTop = Arrow;
            int trackH = Size.Height - Arrow * 2;
            int thumbH = _thumb.Size.Height;
            int travel = trackH - thumbH;
            int desiredTop = e.Y - _grabOffset - (DisplayLocation.Y + trackTop);
            return travel > 0 ? Math.Max(0f, Math.Min(1f, desiredTop / (float)travel)) : 0f;
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
