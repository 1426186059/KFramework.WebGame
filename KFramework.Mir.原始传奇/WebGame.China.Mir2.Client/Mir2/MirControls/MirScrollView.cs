using System;
using SlimDX;
using SlimDX.Direct3D9;
using Client.MirGraphics;
using MirEngine;

namespace Client.MirControls
{
    /// <summary>
    /// 滚动视图：把子控件放进一个"内容容器"，通过将内容渲染进一张"视口大小"的离屏纹理(RenderTarget)再贴回屏幕，
    /// <b>天然裁掉超出视口的溢出部分</b>（与后端无关，不依赖 scissor / 模板）。
    /// 滚动由 <see cref="ScrollOffset"/> 驱动：内容容器 Location = -ScrollOffset。
    /// <para>
    /// 实现借用引擎既有的离屏烘焙范式（<c>DrawControlTexture</c> + <c>DXManager.SetSurface(ControlTexture)</c>）：
    /// 重写 <c>CreateTexture</c> 把本控件左上角平移到 RT 原点后绘制内容，RT 之外的子控件被裁掉；基类 <c>DrawControl</c> 再把 RT 贴回屏幕。
    /// </para>
    /// <para>用法：把子控件用 <see cref="Add"/> 加进来，设置本控件的 <c>Size</c>（= 可视区大小）；用滚轮或 <see cref="ScrollBy"/> / <see cref="SetScrollOffset"/> 滚动。</para>
    /// </summary>
    public class MirScrollView : MirControl
    {
        private readonly MirControl _content;

        public MirScrollView()
        {
            DrawControlTexture = true;       // 走离屏烘焙：把内容烘进 ControlTexture 再上屏
            _content = new MirControl();     // 内容容器（也在 Controls 里，用于命中测试 / 输入转发）
            InsertControl(0, _content);      // 设 _content.Parent = this，使其 DisplayLocation 参与滚动
            ScrollOffset = Point.Empty;
            _content.Location = Point.Empty;
        }

        /// <summary>内容容器（子控件加到这里）。也可直接 scrollView.Content.Controls.Add(...)。</summary>
        public MirControl Content { get { return _content; } }

        /// <summary>当前滚动偏移（像素，内容相对视口左上角的位移，>=0）。</summary>
        public Point ScrollOffset { get; private set; }

        /// <summary>滚动偏移变化（SetScrollOffset / ScrollBy / 滚轮）后触发，参数为本控件。可用于做"可见窗口"优化（只保留视口附近子控件可见）。</summary>
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
            Redraw();   // 令 TextureValid=false → 下次 Draw 重新烘焙内容
            ScrollChanged?.Invoke(this);
        }

        /// <summary>相对当前偏移滚动。</summary>
        public void ScrollBy(Point delta)
            => SetScrollOffset(new Point(ScrollOffset.X + delta.X, ScrollOffset.Y + delta.Y));

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

        /// <summary>
        /// 重写 Draw：跳过"屏幕上直接画子控件"（基类的 DrawChildControls 非 virtual，故在此整体接管），
        /// 子控件仅在 CreateTexture 里烘焙进离屏纹理，避免溢出被画到视口外。
        /// </summary>
        public override void Draw()
        {
            if (IsDisposed || !Visible)
                return;

            BeforeDrawControl();
            DrawControl();   // 烘焙子控件进 ControlTexture（见 CreateTexture 重写）+ 贴回屏幕；屏幕上不再单独画子控件
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

        /// <summary>鼠标滚轮滚动：向上滚（Delta&gt;0）= 内容上移（ScrollOffset 减小）。</summary>
        public override void OnMouseWheel(MouseEventArgs e)
        {
            int count = e.Delta / 120;   // 每格 120 = 一行
            if (count != 0)
                ScrollBy(new Point(0, (e.Delta > 0 ? -1 : 1) * count * 30));   // 30px / 行
            base.OnMouseWheel(e);
        }
    }
}
