using System;
using SlimDX;
using SlimDX.Direct3D9;
using Client.MirGraphics;
using KFramework.MonoGameExtend;

namespace Client.MirControls
{
    /// <summary>
    /// 滚动视图（参考 Unity ScrollRect + Scrollbar）：把子控件放进一个"内容容器"，通过将内容渲染进一张"视口大小"的离屏纹理(RenderTarget)再贴回屏幕，
    /// <b>天然裁掉超出视口的溢出部分</b>（与后端无关，不依赖 scissor / 模板）。
    /// 滚动由 <see cref="ScrollOffset"/> 驱动（内容容器 Location = -ScrollOffset），并内置一条可拖拽的竖直滚动条 + 滚轮（事件沿父链冒泡到本控件）。
    /// <para>用法：把子控件用 <see cref="Add"/> 加进来，设置本控件的 <c>Size</c>（= 可视区大小）；滚轮 / 拖拽右侧滚动条 / 翻页 均可滚动。</para>
    /// </summary>
    public class MirScrollView : MirControl
    {
        // 滚动条布局
        public const int ScrollBarWidth = 14;
        private const int TrackPad = 2;
        private const int MinThumb = 16;

        private readonly MirControl _content;
        private readonly MirImageControl _track;
        private readonly ScrollThumb _thumb;

        public MirScrollView()
        {
            DrawControlTexture = true;       // 走离屏烘焙：把内容烘进 ControlTexture 再上屏
            _content = new MirControl();     // 内容容器（也在 Controls 里，用于命中测试 / 输入转发）
            InsertControl(0, _content);      // 设 _content.Parent = this，使其 DisplayLocation 参与滚动
            ScrollOffset = Point.Empty;
            _content.Location = Point.Empty;

            // 滚动条（直接挂在 ScrollView 下、NOT 在 _content 内，所以不被滚动/烘焙，固定显示在右缘）
            _track = new ScrollTrack(this)
            {
                Visible = false, NotControl = false,
                Texture = KDefaultRes.DefaultTexture2D, ForeColour = new MirEngine.Color(40, 46, 70),
            };
            _thumb = new ScrollThumb(this)
            {
                Visible = false, NotControl = false,
                Texture = KDefaultRes.DefaultTexture2D, ForeColour = new MirEngine.Color(110, 150, 210),
            };
            InsertControl(Controls.Count, _track);
            InsertControl(Controls.Count, _thumb);
        }

        /// <summary>内容容器（子控件加到这里）。也可直接 scrollView.Content.Controls.Add(...)。</summary>
        public MirControl Content { get { return _content; } }

        /// <summary>当前滚动偏移（像素，内容相对视口左上角的位移，>=0）。</summary>
        public Point ScrollOffset { get; private set; }

        /// <summary>滚动偏移变化（SetScrollOffset / ScrollBy / 滚轮 / 拖拽）后触发，参数为本控件。可用于做"可见窗口"优化。</summary>
        public event Action<MirScrollView> ScrollChanged;

        /// <summary>添加子控件（= 加到内容容器），并刷新内容尺寸 / 重新烘焙。</summary>
        public void Add(MirControl control)
        {
            _content.InsertControl(_content.Controls.Count, control);
            UpdateContentSize();
        }

        /// <summary>移除子控件并刷新。</summary>
        public void Remove(MirControl control)
        {
            _content.Controls.Remove(control);
            UpdateContentSize();
        }

        /// <summary>设置滚动偏移并夹紧到 [0, max]（max = 内容尺寸 - 视口尺寸）。</summary>
        public void SetScrollOffset(Point offset)
        {
            int maxX = Math.Max(0, _content.Size.Width - Size.Width);
            int maxY = Math.Max(0, _content.Size.Height - Size.Height);
            int x = Math.Max(0, Math.Min(offset.X, maxX));
            int y = Math.Max(0, Math.Min(offset.Y, maxY));
            ScrollOffset = new Point(x, y);
            _content.Location = new Point(-x, -y);
            UpdateScrollBar();
            TextureValid = false;   // 令本控件离屏纹理失效 → 下次 Draw 重新烘焙内容（基类 Redraw() 只冒泡父级、不会置 TextureValid=false）
            ScrollChanged?.Invoke(this);
        }

        /// <summary>相对当前偏移滚动。</summary>
        public void ScrollBy(Point delta)
            => SetScrollOffset(new Point(ScrollOffset.X + delta.X, ScrollOffset.Y + delta.Y));

        /// <summary>按归一化比例(0..1)设置竖直滚动位置（滚动条拖拽用）。</summary>
        internal void SetScrollNormalizedY(float t)
        {
            int maxY = Math.Max(0, _content.Size.Height - Size.Height);
            SetScrollOffset(new Point(ScrollOffset.X, (int)(t * maxY)));
        }

        /// <summary>点击轨道翻页（dir=-1 上翻，+1 下翻）。</summary>
        internal void PageScroll(int dir)
        {
            int step = Math.Max(20, (Size.Height - ScrollBarWidth) * 9 / 10);
            ScrollBy(new Point(0, dir * step));
        }

        /// <summary>滚动条 thumb 中心（屏幕 Y），供轨道翻页判断方向。</summary>
        internal int ThumbCenterY { get { return _thumb.DisplayLocation.Y + _thumb.Size.Height / 2; } }

        /// <summary>根据内容子控件重新计算内容尺寸（=_content 需要的包围盒），并重新夹紧滚动。</summary>
        public void UpdateContentSize()
        {
            int maxX = 0, maxY = 0;
            foreach (MirControl c in _content.Controls)
            {
                if (c == null) continue;
                maxX = Math.Max(maxX, c.Location.X + c.Size.Width);
                maxY = Math.Max(maxY, c.Location.Y + c.Size.Height);
            }
            _content.Size = new Size(maxX, maxY);
            SetScrollOffset(ScrollOffset);   // 尺寸变了可能要回弹夹紧
        }

        /// <summary>依据当前滚动量摆放滚动条（thumb 大小=可见占比，位置=滚动比例）。</summary>
        private void UpdateScrollBar()
        {
            int maxY = Math.Max(0, _content.Size.Height - Size.Height);
            bool scrollable = maxY > 0;
            _track.Visible = scrollable;
            _thumb.Visible = scrollable;
            if (!scrollable) return;

            int stripX = Size.Width - ScrollBarWidth;
            _track.Location = new Point(stripX, 0);
            _track.Size = new Size(ScrollBarWidth, Size.Height);

            int trackH = Size.Height - TrackPad * 2;
            int thumbH = (int)(trackH * (Size.Height / (float)Math.Max(1, _content.Size.Height)));
            thumbH = Math.Max(MinThumb, Math.Min(trackH, thumbH));
            int travel = trackH - thumbH;
            int thumbTop = travel > 0 ? (int)(travel * (ScrollOffset.Y / (float)maxY)) : 0;
            _thumb.Size = new Size(ScrollBarWidth - TrackPad * 2, thumbH);
            _thumb.Location = new Point(stripX + TrackPad, TrackPad + thumbTop);
        }

        /// <summary>
        /// 重写 Draw：跳过"屏幕上直接画子控件"（基类的 DrawChildControls 非 virtual，故在此整体接管），
        /// 子控件仅在 CreateTexture 里烘焙进离屏纹理；随后在屏幕上叠加绘制滚动条，再画边框。
        /// </summary>
        public override void Draw()
        {
            if (IsDisposed || !Visible)
                return;

            BeforeDrawControl();
            DrawControl();   // 烘焙子控件进 ControlTexture（见 CreateTexture 重写）+ 贴回屏幕；屏幕上不再单独画子控件
            if (_track.Visible) _track.Draw();
            if (_thumb.Visible) _thumb.Draw();
            DrawBorder();
            AfterDrawControl();
        }

        /// <summary>
        /// 重写 CreateTexture：切到 ControlTexture 渲染目标，用平移变换把本控件左上角(DisplayLocation)映射到 RT 原点，
        /// 绘制内容容器（其 Location = -ScrollOffset，故内容被滚动），随后恢复渲染目标与世界变换。RT 之外的子控件被裁掉。
        /// </summary>
        protected override void CreateTexture()
        {
            if (Size.Width <= 0 || Size.Height <= 0) return;
            base.CreateTexture();   // 建并清 ControlTexture（BackColour）
            if (_content == null || ControlTexture == null || ControlTexture.Disposed) return;

            var savedTransform = DXManager.RenderTransform;
            Surface savedSurface = DXManager.CurrentSurface;
            try
            {
                // 把"本控件左上角"平移到 RT 原点：RT 之外的子控件被裁掉（天然裁剪溢出）
                DXManager.RenderTransform = KFramework.MonoGame.Matrix4x4.CreateScaleTranslation(1, 1, -DisplayLocation.X, -DisplayLocation.Y);
                DXManager.SetSurface(new Surface(ControlTexture));
                _content.Draw();   // 递归把内容（含子控件）画进 RT
            }
            finally
            {
                DXManager.SetSurface(savedSurface);
                DXManager.RenderTransform = savedTransform;
            }
        }

        /// <summary>鼠标滚轮滚动：向上滚（Delta&gt;0）= 内容下移（ScrollOffset 增大）；并标记 Handled 停止冒泡。</summary>
        public override void OnMouseWheel(MouseEventArgs e)
        {
            // 本环境 e.Delta 每格为 ±1（浏览器端已归一化），不再除以 120（否则 count 恒为 0、永不滚动）。
            // 兼容桌面端 ±120：|delta|>=120 时按 /120 折算成"格数"，否则直接把 delta 当格数。
            int notches = Math.Abs(e.Delta) >= 120 ? e.Delta / 120 : e.Delta;
            if (notches != 0)
                ScrollBy(new Point(0, notches * 30));   // 上滚(delta>0)=内容下移(ScrollOffset 增大)
            e.Handled = true;
            base.OnMouseWheel(e);
        }

        // —— 滚动条轨道（点击翻页）——
        private sealed class ScrollTrack : MirImageControl
        {
            private readonly MirScrollView _owner;
            public ScrollTrack(MirScrollView owner) { _owner = owner; }
            public override void OnMouseDown(MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left)
                    _owner.PageScroll(e.Y < _owner.ThumbCenterY ? -1 : 1);
                base.OnMouseDown(e);
            }
        }

        // —— 滚动条滑块（拖拽）——
        private sealed class ScrollThumb : MirImageControl
        {
            private readonly MirScrollView _owner;
            private bool _captured;
            private int _grabOffset;   // 按下时鼠标与 thumb 顶部的偏移

            public ScrollThumb(MirScrollView owner) { _owner = owner; }

            // 拖拽期间始终视为"鼠标悬停在本控件"，保证鼠标移出滑块也能持续收到 OnMouseMove
            public override bool IsMouseOver(Point p) => _captured || base.IsMouseOver(p);

            public override void OnMouseDown(MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left)
                {
                    _captured = true;
                    _grabOffset = e.Y - DisplayLocation.Y;
                }
                base.OnMouseDown(e);
            }

            public override void OnMouseMove(MouseEventArgs e)
            {
                if (_captured)
                {
                    int trackTop = _owner.DisplayLocation.Y + TrackPad;
                    int trackH = _owner.Size.Height - TrackPad * 2;
                    int thumbH = Size.Height;
                    int travel = trackH - thumbH;
                    int desiredTop = (e.Y - _grabOffset) - trackTop;
                    float t = travel > 0 ? Math.Max(0, Math.Min(1, desiredTop / (float)travel)) : 0;
                    _owner.SetScrollNormalizedY(t);
                }
                base.OnMouseMove(e);
            }

            public override void OnMouseUp(MouseEventArgs e)
            {
                _captured = false;
                base.OnMouseUp(e);
            }

            // 关键修复：引擎每次 OnMouseUp 都会走 base.OnMouseUp → ActiveControl.Deactivate()，
            // 而按下时 Activate() 已把 ActiveControl 设为本滑块。重写 Deactivate 在此清掉 _captured，
            // 这样无论 mouse up 落在滑块本身还是其它控件（光标在拖拽中可能飘走），捕获都会被释放，
            // 避免"松手后没按键只是移动，内容却一直跟着滚动"的卡死式拖拽。
            protected override void Deactivate()
            {
                _captured = false;
                if (ActiveControl == this) ActiveControl = null;
                base.Deactivate();
            }
        }
    }
}
