using Client.MirGraphics;
using SlimDX.Direct3D9;
using WebGame.Mir2.MonoGame.Client; // CMain.MPoint（与 DisplayLocation 同一坐标系）

namespace Client.MirControls
{
    /// <summary>
    /// 滚动视图（参考 Unity ScrollRect + Scrollbar）：把子控件放进一个"内容容器"，通过将内容渲染进一张"视口大小"的离屏纹理(RenderTarget)再贴回屏幕，
    /// <b>天然裁掉超出视口的溢出部分</b>（与后端无关，不依赖 scissor / 模板）。
    /// 滚动由 <see cref="ScrollOffset"/> 驱动（内容容器 Location = -ScrollOffset），内置竖直滚动条 + 滚轮（事件沿父链冒泡到本控件），
    /// 并支持像 Unity ScrollRect 一样<b>拖住内容区滚动</b>：左键按下后移动超过 <see cref="DragThreshold"/> 像素即进入拖拽，
    /// 位移增量直接驱动 ScrollOffset（水平/竖直均可），拖拽期间鼠标命中被内容容器捕获，松手不会误点列表里的按钮。
    /// <para>用法：把子控件用 <see cref="Add"/> 加进来，设置本控件的 <c>Size</c>（= 可视区大小）；滚轮 / 拖拽滚动条 / 翻页 / 拖动内容区 均可滚动。</para>
    /// </summary>
    public class MirScrollView : MirControl
    {
        // 滚动条布局
        public const int ScrollBarWidth = 14;

        private readonly MirControl _content;   // 内容容器：滚动=移动它；子控件全加进它；绝不能删

        // 三段式皮肤滚动条（构造即创建默认皮肤，所有面板自动获得；外部直接对 Slider 设图集帧换肤）
        private MirSlider _skinSlider;

        public MirScrollView()
        {
            DrawControlTexture = true;       // 走离屏烘焙：把内容烘进 ControlTexture 再上屏
            _content = new DragContent(this); // 内容容器（也在 Controls 里，并把按下/移动/抬起转发给本控件做拖拽）
            InsertControl(0, _content);      // 设 _content.Parent = this，使其 DisplayLocation 参与滚动
            ScrollOffset = Point.Zero;
            _content.Location = Point.Zero;

            // 皮肤滚动条（挂在 ScrollView 下、不在 _content 内，故不被滚动/烘焙，固定显示在右缘）
            _skinSlider = new MirSlider();
            _skinSlider.Horizontal = false;   // MirScrollView 是竖直滚动，显式设为纵向布局
            _skinSlider.ValueChanged += OnSkinSliderValueChanged;
            _skinSlider.PageRequested += OnSkinSliderPageRequested;
            InsertControl(Controls.Count, _skinSlider);
            _skinSlider.Visible = false;
        }

        /// <summary>内容容器（子控件加到这里）。也可直接 scrollView.Content.Controls.Add(...)。</summary>
        public MirControl Content { get { return _content; } }

        /// <summary>
        /// 当前皮肤滚动条（三段式：背景轨道/上下箭头/滑块），构造即带默认皮肤。
        /// 外部直接对 Slider.Track / Thumb / UpArrow / DownArrow 四个组件设图集帧换肤。
        /// 滚动数据逻辑仍由本控件持有（slider 只回报比例/翻页意图）。
        /// </summary>
        public MirSlider Slider { get { return _skinSlider; } }

        private void OnSkinSliderValueChanged(MirSlider slider, float percent) => SetScrollNormalizedY(percent);
        private void OnSkinSliderPageRequested(MirSlider slider, int dir) => PageScroll(dir);

        /// <summary>当前滚动偏移（像素，内容相对视口左上角的位移，>=0）。</summary>
        public Point ScrollOffset { get; private set; }

        /// <summary>是否显示竖直滚动条（默认 true）。设为 false 可隐藏滚动条，仅保留滚轮/拖拽滚动。</summary>
        public bool ShowScrollBar { get; set; } = true;

        /// <summary>滚动偏移变化（SetScrollOffset / ScrollBy / 滚轮 / 拖拽）后触发，参数为本控件。可用于做"可见窗口"优化。</summary>
        public event Action<MirScrollView> ScrollChanged;

        // —— 内容区拖拽（Unity ScrollRect 的手势拖动）——
        // OnMouseDown/Up 由 MirScene 只派发给 MouseControl（最深命中控件），不会沿父链上抛，
        // 因此内容容器和它的直接子控件都要把按下/抬起转发进来，否则按在内容上根本起不了拖拽。
        private const int DragThreshold = 4;
        private bool _dragPending;   // 左键已按下但还没越过阈值：此时单击行为完全不变
        private bool _dragging;      // 已进入拖拽：开始滚动 + 捕获鼠标命中
        private Point _dragAnchor;
        private Point _lastDragPoint;

        /// <summary>是否允许拖住内容区滚动（默认 true）。内容里若有需要自行处理拖放的控件可关掉。</summary>
        public bool DragToScroll { get; set; } = true;

        private void BeginDrag()
        {
            EndDrag();
            if (!DragToScroll) return;
            // 没有溢出内容就不需要拖拽。
            if (_content.Size.Width <= Size.Width && _content.Size.Height <= Size.Height) return;
            _dragPending = true;
            _dragAnchor = CMain.MPoint;
            _lastDragPoint = CMain.MPoint;
        }

        /// <summary>每帧 OnMouseMove 调用：用「上一帧 → 当前」的位移增量滚动，而不是「按下点 → 当前」的绝对差。
        /// 这样拖到顶/底被 SetScrollOffset 夹紧后反向拖动能立刻响应，不会出现要先把超出位移走完的死区。</summary>
        private void UpdateDrag()
        {
            if (!_dragPending && !_dragging) return;

            // 浏览器失焦 / 在视图外松手 / 事件丢失时，MouseUp 派发不到本视图；
            // 读引擎的左键电平兜底，避免拖拽状态卡死（电平由 Input_Mouse.OnButton 实时维护）。
            // 输入装置未激活（非浏览器端）时不做此判断，纯靠上面的事件转发结束拖拽。
            if (KFramework.MonoGame.Input_Mouse.Active &&
                !KFramework.MonoGame.Input_Mouse.GetButton(KFramework.MonoGame.MouseButton.Left))
            {
                EndDrag();
                return;
            }

            Point p = CMain.MPoint;

            if (_dragPending)
            {
                if (Math.Abs(p.X - _dragAnchor.X) < DragThreshold &&
                    Math.Abs(p.Y - _dragAnchor.Y) < DragThreshold)
                {
                    _lastDragPoint = p;
                    return;
                }
                _dragPending = false;
                _dragging = true;
            }

            int dx = _lastDragPoint.X - p.X;
            int dy = _lastDragPoint.Y - p.Y;
            _lastDragPoint = p;
            if (dx != 0 || dy != 0) ScrollBy(new Point(dx, dy));
        }

        private void EndDrag()
        {
            _dragPending = false;
            _dragging = false;
        }

        private void OnContentMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) BeginDrag();
        }

        private void OnContentMouseUp(object sender, MouseEventArgs e) => EndDrag();

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

        /// <summary>强制令本控件离屏纹理失效，下次 Draw 重新烘焙内容（子控件外观变化如高亮/选中态切换后调用）。</summary>
        public void InvalidateTexture()
        {
            TextureValid = false;
        }

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

        /// <summary>依据当前滚动量摆放皮肤滚动条（thumb 用图集原始尺寸，位置=滚动比例）。</summary>
        private void UpdateScrollBar()
        {
            int maxY = Math.Max(0, _content.Size.Height - Size.Height);
            int stripX = Size.Width - ScrollBarWidth;
            _skinSlider.Location = new Point(stripX, 0);
            _skinSlider.Size = new Size(ScrollBarWidth, Size.Height);

            // ShowScrollBar 控制整条显隐；内容是否溢出(maxY>0)只决定 thumb 显隐（轨道/箭头常驻）。
            _skinSlider.Visible = ShowScrollBar;
            float percent = maxY > 0 ? ScrollOffset.Y / (float)maxY : 0f;
            _skinSlider.SetState(maxY > 0, percent);
        }

        /// <summary>
        /// 子控件（按钮等）高亮/按下/选中态变化时会自置 TextureValid=false；这里让滚动视图跟着失效并重烘，
        /// 否则子控件在滚动视图内的选中/高亮/按下视觉不会刷新。
        /// </summary>
        protected override void BeforeDrawControl()
        {
            base.BeforeDrawControl();
            if (TextureValid)
            {
                foreach (MirControl c in _content.Controls)
                    if (!c.TextureValid) { TextureValid = false; break; }
            }
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
            // 绘制前刷新滚动条：Size 由外部（对象初始化器/resize）设置，构造时拿不到最终尺寸；
            // 且 MirSlider.Layout 在尺寸为 0 时会跳过布局。故每帧同步尺寸/位置/状态，确保首次显示与 resize 后都正确布局。
            UpdateScrollBar();
            DrawControl();   // 烘焙子控件进 ControlTexture（见 CreateTexture 重写）+ 贴回屏幕；屏幕上不再单独画子控件
            if (_skinSlider.Visible) _skinSlider.Draw();
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

        /// <summary>拖拽期间放宽命中：鼠标略微飘出视口仍能持续收到 OnMouseMove。</summary>
        public override bool IsMouseOver(Point p) => (_dragging && Visible) || base.IsMouseOver(p);

        public override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) BeginDrag();
            base.OnMouseDown(e);
        }

        public override void OnMouseMove(MouseEventArgs e)
        {
            UpdateDrag();   // 递归进 _content 之前先算一次；_content 再算时增量为 0，不会重复滚动
            base.OnMouseMove(e);
        }

        public override void OnMouseUp(MouseEventArgs e)
        {
            EndDrag();
            base.OnMouseUp(e);
        }

        protected override void Deactivate()
        {
            // 引擎每次 OnMouseUp 都会 Deactivate；无论松手落在视图内还是飘到别处，都要解除拖拽状态。
            EndDrag();
            base.Deactivate();
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

        /// <summary>内容容器：把按下/移动/抬起转发给 ScrollView，并在拖拽期间捕获鼠标命中。</summary>
        private sealed class DragContent : MirControl
        {
            private readonly MirScrollView _owner;

            public DragContent(MirScrollView owner) { _owner = owner; }

            public override bool IsMouseOver(Point p) => (_owner._dragging && Visible) || base.IsMouseOver(p);

            /// <summary>Add() 与 MirScrollView2.RefreshVisible() 都走 InsertControl，在此统一挂上转发。</summary>
            public override void InsertControl(int index, MirControl control)
            {
                base.InsertControl(index, control);
                if (control == null) return;
                control.MouseDown += _owner.OnContentMouseDown;
                control.MouseUp += _owner.OnContentMouseUp;
            }

            public override void OnMouseDown(MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left) _owner.BeginDrag();
                base.OnMouseDown(e);
            }

            public override void OnMouseMove(MouseEventArgs e)
            {
                _owner.UpdateDrag();
                if (_owner._dragging)
                {
                    // 拖拽期间不再递归子控件，MouseControl 固定为本容器：
                    // 列表里的按钮不会被高亮，松手时的 Click 也不会落到按钮上（Unity 拖动列表的同一行为）；
                    // 同时 MirScene.OnMouseUp 仍会派发到本容器，保证 EndDrag 一定被调用。
                    if (MouseControl != this)
                    {
                        Highlight();          // 让旧 MouseControl 正常走 OnMouseLeave（清掉物品提示框等）
                        // Highlight 可能被 ActiveControl 检查提前 return 并把 MouseControl 置空，
                        // 这里补回捕获，保证松手时 MirScene.OnMouseUp 仍派发到本容器 → EndDrag 一定执行。
                        if (MouseControl != this)
                        {
                            MouseControl = this;
                            OnMouseEnter();
                        }
                    }
                    return;
                }
                base.OnMouseMove(e);
            }

            public override void OnMouseUp(MouseEventArgs e)
            {
                _owner.EndDrag();
                base.OnMouseUp(e);
            }

            protected override void Deactivate()
            {
                _owner.EndDrag();
                base.Deactivate();
            }
        }
    }
}
