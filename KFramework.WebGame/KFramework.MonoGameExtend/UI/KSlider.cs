using KFramework.MonoGame;
using System;

namespace KFramework.MonoGameExtend
{
    /// <summary>
    /// 滑块：背景（继承 KImage）+ 前景填充条 + 可拖动滑块（内部 KImage）。
    /// 实现 <see cref="IClickable"/>：在滑块区域内按下即开始拖动（点在轨道上也会跳到该位置），
    /// 拖动时实时更新 <see cref="Value"/>（水平 / 垂直方向均可）。
    /// <para>拖拽的「move」沿用框架现有做法（同 KScrollView 滚动条）：在 Update 里轮询鼠标位置，
    /// 而非依赖 OnPointerMove 事件分发——因为 KPointerDispatcher 目前只分发 Down/Up/Click/Enter/Exit。</para>
    /// </summary>
    public class KSlider : KImage, IClickable
    {
        public enum KSliderDir { Horizontal, Vertical }

        private KImage _fill;
        private KImage _thumb;
        private float _value;
        private bool _dragging;
        private bool _interactable = true;
        private bool _registered;
        private bool _dirty = true;
        private float _thumbSize = 16f;

        public KSlider()
        {
            Color = new Color(40, 40, 40); // 轨道背景

            _fill = new KImage { Color = new Color(80, 200, 120) };
            _fill.Parent = this;

            _thumb = new KImage { Color = Color.White };
            _thumb.Parent = this;

            Register(); // 自动注册到输入系统，业务层无需手动调用
        }

        public KSliderDir Direction { get; set; } = KSliderDir.Horizontal;

        public float Value
        {
            get => _value;
            set
            {
                float v = Clamp(value, MinValue, MaxValue);
                if (WholeNumbers) v = (float)Math.Round(v);
                if (_value != v) { _value = v; _dirty = true; OnValueChanged?.Invoke(this, _value); }
            }
        }

        public float MinValue { get; set; } = 0f;
        public float MaxValue { get; set; } = 1f;
        public bool WholeNumbers { get; set; } = false;

        public Color FillColor { get => _fill.Color; set => _fill.Color = value; }
        public Color ThumbColor { get => _thumb.Color; set => _thumb.Color = value; }
        public Color BackgroundColor { get => Color; set => Color = value; }

        /// <summary>滑块（thumb）的方形边长；水平方向时高度填满、宽度取此值，垂直反之。</summary>
        public float ThumbSize
        {
            get => _thumbSize;
            set { if (_thumbSize != value) { _thumbSize = value; _dirty = true; } }
        }

        public event EventHandler<float> OnValueChanged;

        public override void Update()
        {
            if (_dragging)
            {
                // 拖拽中：持续跟随鼠标；松手则结束（双保险，OnPointerUp 也会结束）
                if (KInputMgr.GetMouseButton(MouseButton.Left))
                    SetValueFromPointer(KInputMgr.MousePosition);
                else
                    _dragging = false;
            }

            if (_dirty) { Layout(); _dirty = false; }
        }

        private void Layout()
        {
            float range = MaxValue - MinValue;
            float t = range <= 0f ? 0f : (_value - MinValue) / range;
            t = Clamp(t, 0f, 1f);

            if (Direction == KSliderDir.Horizontal)
            {
                float track = Math.Max(0f, Size.X - _thumbSize);
                float thumbLeft = t * track;
                _fill.Pivot = new Vector2(0f, 0.5f);
                _fill.LocalPosition = new Vector2(0f, Size.Y * 0.5f);
                _fill.Size = new Vector2(thumbLeft + _thumbSize * 0.5f, Size.Y);

                _thumb.Pivot = new Vector2(0.5f, 0.5f);
                _thumb.LocalPosition = new Vector2(thumbLeft + _thumbSize * 0.5f, Size.Y * 0.5f);
                _thumb.Size = new Vector2(_thumbSize, Size.Y);
            }
            else
            {
                float track = Math.Max(0f, Size.Y - _thumbSize);
                float thumbTop = t * track;
                _fill.Pivot = new Vector2(0.5f, 0f);
                _fill.LocalPosition = new Vector2(Size.X * 0.5f, 0f);
                _fill.Size = new Vector2(Size.X, thumbTop + _thumbSize * 0.5f);

                _thumb.Pivot = new Vector2(0.5f, 0.5f);
                _thumb.LocalPosition = new Vector2(Size.X * 0.5f, thumbTop + _thumbSize * 0.5f);
                _thumb.Size = new Vector2(Size.X, _thumbSize);
            }
        }

        private void SetValueFromPointer(Vector2 screen)
        {
            Rectangle b = Bounds;
            if (Direction == KSliderDir.Horizontal)
            {
                float track = Math.Max(1f, b.Width - _thumbSize);
                float localX = screen.X - b.Left - _thumbSize * 0.5f;
                Value = MinValue + Clamp(localX / track, 0f, 1f) * (MaxValue - MinValue);
            }
            else
            {
                float track = Math.Max(1f, b.Height - _thumbSize);
                float localY = screen.Y - b.Top - _thumbSize * 0.5f;
                Value = MinValue + Clamp(localY / track, 0f, 1f) * (MaxValue - MinValue);
            }
        }

        private static float Clamp(float v, float a, float b) => v < a ? a : (v > b ? b : v);

        // ===== IClickable =====

        public int Priority { get; set; } = 0;

        /// <summary>是否可交互，false 时变灰且不响应事件</summary>
        public bool Interactable
        {
            get => _interactable;
            set => _interactable = value;
        }

        /// <summary>不可交互时自动退出射线检测，让事件穿透到下层</summary>
        public bool RaycastEnabled => _interactable;

        /// <summary>屏幕空间碰撞区域，与 KImage.Draw 使用的矩形一致（WorldPosition + Size*WorldScale）。</summary>
        public Rectangle Bounds
        {
            get
            {
                Vector2 worldSize = Size * WorldScale;
                Vector2 leftTop = WorldPosition - Pivot * worldSize;
                return new Rectangle(
                    (int)Math.Round(leftTop.X), (int)Math.Round(leftTop.Y),
                    (int)Math.Round(worldSize.X), (int)Math.Round(worldSize.Y));
            }
        }

        public bool OnPointerDown(KPointerEventArgs args)
        {
            if (!_interactable) return false;
            _dragging = true;
            SetValueFromPointer(args.Position);
            return true; // 消费事件
        }

        public bool OnPointerUp(KPointerEventArgs args)
        {
            _dragging = false;
            return true;
        }

        public bool OnPointerClick(KPointerEventArgs args) => false;

        public void OnPointerEnter(KPointerEventArgs args) { }

        public void OnPointerExit(KPointerEventArgs args) => _dragging = false;

        /// <summary>注册到输入系统，销毁时务必调用 Unregister 避免泄漏</summary>
        public void Register()
        {
            if (_registered) return;
            KInputMgr.Register(this);
            _registered = true;
        }

        public void Unregister()
        {
            if (!_registered) return;
            KInputMgr.Unregister(this);
            _registered = false;
            _dragging = false;
        }
    }
}
