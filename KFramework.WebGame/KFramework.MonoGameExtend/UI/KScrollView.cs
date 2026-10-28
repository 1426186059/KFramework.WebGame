using System;
using System.Collections.Generic;
using KFramework.MonoGame;

namespace KFramework.MonoGameExtend
{
    /// <summary>
    /// 滚动视图（参考 Unity ScrollRect + Scrollbar）：把子控件放进 Content 容器，通过滚动偏移平移内容；
    /// 视口裁剪由 KCanvas 配合 Mask 的「剪刀测试（scissor 材质）」完成 —— KScrollView 重写 BatchMaterial 返回一个开启 ScissorTest 的材质，
    /// KCanvas 据此把本视图内容切到独立 SpriteBatch 段并用 Mask.ApplyScissor 裁剪（Viewport 外像素不绘制）；
    /// UpdateCull 仅用于交互剔除（视口外子项不响应点击）。支持滚轮与右侧滚动条拖拽，可选内容拖拽。
    /// <para>用法：new KScrollView(){ Size = new Vector2(300,400) }，Add(child) 加入内容；挂在 KCanvas 下即可自动 Update/Draw。</para>
    /// </summary>
    public class KScrollView : KWidget, IClickable
    {
        // 内容容器用 KTransform（非 KWidget）：滚动偏移走 LocalPosition 持久保存，且不受父 Size 变化重置；
        // KCanvas 仍会自动递归绘制其下的 KWidget 子项。
        private readonly KTransform _content;
        private readonly ScrollBarWidget _scrollBar;
        private Vector2 _scrollOffset = Vector2.Zero;
        private Vector2 _contentSize = Vector2.Zero;

        private bool _scrollBarDragging;
        private float _grabOffset;
        private bool _contentDragging;
        private Vector2 _dragLastMouse;

        /// <summary>开启后内容区按下拖动可滚动，但会吞掉内容项的点击。</summary>
        public bool EnableDragScroll { get; set; } = false;
        public bool EnableScrollBar { get; set; } = true;
        public float ScrollBarWidth { get; set; } = 8f;
        public Color BackgroundColor { get; set; } = new Color(30, 30, 30, 140);

        public int Priority { get; set; } = 0;
        public bool RaycastEnabled => true;

        /// <summary>裁剪内容用的 scissor 材质（开启 ScissorTest）。KCanvas 会因本节点 BatchMaterial 非 null 而把内容切到独立批绘制。</summary>
        private Material _scissorMaterial;
        public override Material BatchMaterial => _scissorMaterial ??= new Material
        {
            Blend = BlendState.NonPremultiplied,
            Sampler = SamplerState.PointClamp,
            DepthStencil = DepthStencilState.None,
            Rasterizer = new RasterizerState { CullMode = CullMode.Off, ScissorTestEnable = true },
        };

        public event EventHandler<KScrollView> ScrollChanged;

        public KScrollView()
        {
            _content = new KTransform();
            _content.Parent = this;

            _scrollBar = new ScrollBarWidget(this);
            _scrollBar.MinMaxAnchor = RectangleF.MinMax(0, 0, 0, 0);
            _scrollBar.Parent = this;

            KInputMgr.Register(this);
            UpdateScrollBarVisibility();
        }

        /// <summary>内容容器，Add 的子控件挂到这里（Parent=Content），会随滚动偏移整体平移。</summary>
        public KTransform Content => _content;

        public Vector2 ScrollOffset => _scrollOffset;
        public Vector2 ContentSize => _contentSize;

        public void Add(KWidget item) { item.Parent = _content; RefreshContentSize(); SetScrollOffset(_scrollOffset); }
        public void Remove(KWidget item) { item.Parent = null; RefreshContentSize(); SetScrollOffset(_scrollOffset); }
        public void Clear()
        {
            var list = new List<KTransform>(_content.ChildList);
            foreach (var c in list) c.Parent = null;
            RefreshContentSize();
        }

        private void RefreshContentSize()
        {
            float maxX = 0, maxY = 0;
            foreach (var c in _content.ChildList)
            {
                if (c == null) continue;
                Vector2 p = c.WorldPosition - _content.WorldPosition; // 抵消滚动后的相对布局位置
                Vector2 s = (c is KWidget w) ? w.Size * w.WorldScale : Vector2.Zero;
                maxX = Math.Max(maxX, p.X + s.X);
                maxY = Math.Max(maxY, p.Y + s.Y);
            }
            _contentSize = new Vector2(maxX, maxY);
        }

        public void SetScrollOffset(Vector2 offset)
        {
            float maxX = Math.Max(0, _contentSize.X - Size.X);
            float maxY = Math.Max(0, _contentSize.Y - Size.Y);
            float x = MathHelper.Clamp(offset.X, 0, maxX);
            float y = MathHelper.Clamp(offset.Y, 0, maxY);
            _scrollOffset = new Vector2(x, y);
            _content.LocalPosition = new Vector3(-x, -y, 0); // 内容整体平移
            UpdateCull();
            UpdateScrollBarVisibility();
            ScrollChanged?.Invoke(this, this);
        }

        public void ScrollBy(Vector2 d) => SetScrollOffset(_scrollOffset + d);

        // 视口外子项剔除：完全不相交则隐藏（跳过绘制）
        private void UpdateCull()
        {
            RectangleF view = GetWorldBound();
            foreach (var c in _content.ChildList)
            {
                if (c is KWidget w)
                {
                    RectangleF r = w.GetWorldBound();
                    bool visible = r.Right > view.Left && r.Left < view.Right && r.Bottom > view.Top && r.Top < view.Bottom;
                    w.activeSelf = visible;
                }
            }
        }

        private void UpdateScrollBarVisibility()
        {
            bool scrollable = _contentSize.Y > Size.Y || _contentSize.X > Size.X;
            _scrollBar.activeSelf = EnableScrollBar && scrollable;
        }

        public override void Update()
        {
            base.Update(); // 确保整棵子树 WorldMatrix 脏标记被消费（WorldPosition 为惰性派生）
            // 每帧基于最新世界矩阵重算内容尺寸 / 滚动条可见性 / 视口剔除：
            // 这样即使首次布局未定、或内容用 Add/直接 Parent 动态增删，滚动条都能正确出现。
            RefreshContentSize();
            UpdateScrollBarVisibility();
            UpdateCull();
            // 滚轮：鼠标在视口内才滚动。约定：向前/上滚看上方内容（offset 减小）；
            // 若实测方向相反，翻转下面的符号即可（不同后端的 ScrollDelta 正负约定可能不同）。
            int wheel = KInputMgr.ScrollDelta;
            if (wheel != 0)
            {
                RectangleF view = GetWorldBound();
                Vector2 mp = KInputMgr.MousePosition;
                if (mp.X >= view.Left && mp.X <= view.Right && mp.Y >= view.Top && mp.Y <= view.Bottom)
                    ScrollBy(new Vector2(0, wheel * 20f));
            }

            if (_contentDragging)
            {
                Vector2 cur = KInputMgr.MousePosition;
                ScrollBy(_dragLastMouse - cur); // 内容跟随鼠标：鼠标下移→内容上移
                _dragLastMouse = cur;
            }
            else if (_scrollBarDragging)
            {
                UpdateScrollBarDrag();
            }
        }

        public override void Draw()
        {
            if (!activeInHierarchy) return;
            // 用 Mask 的剪刀测试把内容裁到视口内（Viewport 外像素不绘制）。
            // 裁剪矩形用屏幕坐标给出：本框架 UI 的 WorldPosition 已是经 Canvas 缩放后的屏幕像素坐标。
            var device = KSceneMgr.Game.GraphicsDevice;
            RectangleF r = GetWorldBound();
            Rectangle rect = new Rectangle((int)Math.Round(r.X), (int)Math.Round(r.Y), (int)Math.Round(r.Width), (int)Math.Round(r.Height));
            Mask.ApplyScissor(device, rect);

            var batch = KSceneMgr.SpriteBatch;
            batch.DrawRect(KDefaultRes.DefaultTexture2D, rect, null, BackgroundColor, 0, Vector2.Zero, SpriteEffects.None, 0);
        }

        // ===== IClickable =====
        public Rectangle Bounds
        {
            get
            {
                RectangleF r = GetWorldBound();
                return new Rectangle((int)Math.Round(r.X), (int)Math.Round(r.Y), (int)Math.Round(r.Width), (int)Math.Round(r.Height));
            }
        }

        public bool OnPointerDown(KPointerEventArgs args)
        {
            if (EnableScrollBar && InScrollBarZone(args.Position))
            {
                _scrollBarDragging = true;
                float trackTop = GetWorldBound().Top;
                float thumbH = ScrollBarThumbHeight();
                float travel = Math.Max(0, Size.Y - thumbH);
                float maxY = Math.Max(1, _contentSize.Y - Size.Y);
                float thumbTop = travel * (_scrollOffset.Y / maxY);
                _grabOffset = args.Position.Y - (trackTop + thumbTop);
                return true;
            }
            if (EnableDragScroll)
            {
                _contentDragging = true;
                _dragLastMouse = args.Position;
                return true;
            }
            return false; // 不消费，让内容项正常接收点击
        }

        public bool OnPointerUp(KPointerEventArgs args)
        {
            _contentDragging = false;
            _scrollBarDragging = false;
            return false;
        }

        public bool OnPointerClick(KPointerEventArgs args) => false;
        public void OnPointerEnter(KPointerEventArgs args) { }
        public void OnPointerExit(KPointerEventArgs args) { }

        private bool InScrollBarZone(Vector2 screen)
        {
            if (!_scrollBar.activeSelf) return false;
            RectangleF view = GetWorldBound();
            float x = view.Right - ScrollBarWidth;
            return screen.X >= x && screen.X <= view.Right && screen.Y >= view.Top && screen.Y <= view.Bottom;
        }

        private float ScrollBarThumbHeight()
        {
            float h = Size.Y * (Size.Y / Math.Max(1, _contentSize.Y));
            return MathHelper.Clamp(h, 16, Size.Y);
        }

        private void UpdateScrollBarDrag()
        {
            float trackTop = GetWorldBound().Top;
            float thumbH = ScrollBarThumbHeight();
            float travel = Math.Max(0, Size.Y - thumbH);
            float desiredTop = (KInputMgr.MousePosition.Y - _grabOffset) - trackTop;
            float t = travel > 0 ? MathHelper.Clamp(desiredTop / travel, 0, 1) : 0;
            SetScrollOffset(new Vector2(_scrollOffset.X, t * Math.Max(0, _contentSize.Y - Size.Y)));
        }

        // 滚动条作为 ScrollView 直接子 KWidget，绘制在内容之上（ChildList 中位于 _content 之后）
        private sealed class ScrollBarWidget : KWidget
        {
            private readonly KScrollView _owner;
            public ScrollBarWidget(KScrollView owner) { _owner = owner; }

            public override void Draw()
            {
                if (!activeInHierarchy) return;
                var batch = KSceneMgr.SpriteBatch;
                RectangleF view = _owner.GetWorldBound();
                float x = view.Right - _owner.ScrollBarWidth;
                Rectangle track = new Rectangle((int)Math.Round(x), (int)Math.Round(view.Top), (int)Math.Round(_owner.ScrollBarWidth), (int)Math.Round(view.Height));
                batch.DrawRect(KDefaultRes.DefaultTexture2D, track, null, new Color(40, 46, 70), 0, Vector2.Zero, SpriteEffects.None, 0);

                float maxY = Math.Max(1, _owner.ContentSize.Y - _owner.Size.Y);
                float thumbH = _owner.ScrollBarThumbHeight();
                float travel = view.Height - thumbH;
                float thumbTop = travel * (_owner.ScrollOffset.Y / maxY);
                Rectangle thumb = new Rectangle((int)Math.Round(x), (int)Math.Round(view.Top + thumbTop), (int)Math.Round(_owner.ScrollBarWidth), (int)Math.Round(thumbH));
                batch.DrawRect(KDefaultRes.DefaultTexture2D, thumb, null, new Color(110, 150, 210), 0, Vector2.Zero, SpriteEffects.None, 0);
            }
        }
    }
}
