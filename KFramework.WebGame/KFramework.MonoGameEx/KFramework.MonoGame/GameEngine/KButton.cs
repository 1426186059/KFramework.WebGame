using Microsoft.Xna.Framework;
using System;
using System.Reflection.Emit;

namespace KFramework.MonoGame
{
    /// <summary>
    /// 按钮的可视状态，对应 Unity Selectable 的 SelectionState
    /// </summary>
    public enum KSelectionState
    {
        Normal,
        Highlighted,
        Pressed,
        Disabled,
    }

    public class KButton : KImage, IClickable
    {
        private KLabel _cacheLable;
        public KLabel Label
        {
            get
            {
                if (_cacheLable == null)
                {
                    _cacheLable = new KLabel();
                    InitLableDefault();
                }
                return _cacheLable;
            }
            set
            {
                _cacheLable = value;
                InitLableDefault();
            }
        }

        private void InitLableDefault()
        {
            _cacheLable.Parent = this;
            _cacheLable.MinMaxAnchor = KRectangleF.MinMax(0, 0, 1, 1);
            _cacheLable.Anchor = Vector2.One * 0.5f;
            _cacheLable.Pivot = Vector2.One * 0.5f;
            _cacheLable.AnchorOffset = KRectangleFOffset.Zero;
        }

        /// <summary>点击事件，用法：button.onClick += (s, e) => { };</summary>
        public event EventHandler<KPointerEventArgs> PointerClickEvent;

        /// <summary>按下事件</summary>
        public event EventHandler<KPointerEventArgs> PointerDownEvent;

        /// <summary>抬起事件</summary>
        public event EventHandler<KPointerEventArgs> PointerUpEvent;

        /// <summary>移入事件</summary>
        public event EventHandler<KPointerEventArgs> PointerEnterEvent;

        /// <summary>移出事件</summary>
        public event EventHandler<KPointerEventArgs> PointerExitEvent;

        // ===== 四态配色，对应 Unity 的 ColorBlock =====

        public Color NormalColor { get; set; } = Color.White;
        public Color HighlightedColor { get; set; } = new Color(245, 245, 245);
        public Color PressedColor { get; set; } = new Color(200, 200, 200);
        public Color DisabledColor { get; set; } = new Color(200, 200, 200, 128);

        /// <summary>颜色渐变速度（每秒），对应 Unity 的 Fade Duration</summary>
        public float ColorFadeSpeed { get; set; } = 8f;

        private bool _interactable = true;

        /// <summary>是否可交互，false 时变灰且不响应事件</summary>
        public bool Interactable
        {
            get => _interactable;
            set
            {
                if (_interactable == value) return;
                _interactable = value;
                if (!_interactable)
                {
                    // 失效时清掉按下/悬停状态，避免卡在按下态
                    _isPointerInside = false;
                    _isPointerDown = false;
                }
                RefreshState();
            }
        }

        /// <summary>指针是否悬停在按钮上</summary>
        public bool IsHovered => _isPointerInside;

        /// <summary>按钮是否被按住</summary>
        public bool IsPressed => _isPointerDown;

        /// <summary>当前可视状态</summary>
        public KSelectionState SelectionState { get; private set; } = KSelectionState.Normal;

        private bool _isPointerInside;
        private bool _isPointerDown;
        private bool _registered;
        private Color _targetColor;
        private bool _colorInited;

        public KButton()
        {
            _targetColor = NormalColor;
            Color = NormalColor;
            _colorInited = true;

            // 自动注册到输入系统，业务层无需手动调用
            Register();
        }

        /// <summary>注册到输入系统，开始接收指针事件</summary>
        public void Register()
        {
            if (_registered) return;
            KInputMgr.Register(this);
            _registered = true;
        }

        /// <summary>从输入系统注销，销毁按钮时务必调用，否则会泄漏</summary>
        public void Unregister()
        {
            if (!_registered) return;
            KInputMgr.Unregister(this);
            _registered = false;
            _isPointerInside = false;
            _isPointerDown = false;
        }

        public override void Update()
        {
            UpdateColorTransition();
        }

        /// <summary>按状态把颜色平滑过渡到目标色</summary>
        private void UpdateColorTransition()
        {
            if (!_colorInited)
            {
                Color = _targetColor;
                _colorInited = true;
                return;
            }

            if (Color == _targetColor) return;

            float t = KTime.deltaTime * ColorFadeSpeed;
            if (t >= 1f)
            {
                Color = _targetColor;
                return;
            }

            Color = Color.Lerp(Color, _targetColor, t);
        }

        /// <summary>根据交互状态重算目标颜色</summary>
        private void RefreshState()
        {
            KSelectionState state;
            if (!_interactable) state = KSelectionState.Disabled;
            else if (_isPointerDown) state = KSelectionState.Pressed;
            else if (_isPointerInside) state = KSelectionState.Highlighted;
            else state = KSelectionState.Normal;

            SelectionState = state;

            _targetColor = state switch
            {
                KSelectionState.Disabled => DisabledColor,
                KSelectionState.Pressed => PressedColor,
                KSelectionState.Highlighted => HighlightedColor,
                _ => NormalColor,
            };
        }

        public int GetHeight()
        {
            return Size != default ? (int)Math.Round(Size.Y) : 100;
        }

        public int GetWidth()
        {
            return Size != default ? (int)Math.Round(Size.X) : 300;
        }

        // ===== IClickable =====

        private bool _raycastEnabled = true;

        /// <summary>不可交互时自动退出射线检测，让事件穿透到下层</summary>
        public bool RaycastEnabled
        {
            get => _raycastEnabled && _interactable;
            set => _raycastEnabled = value;
        }

        /// <summary>
        /// 屏幕空间碰撞区域。必须与 KImage.Draw 使用的矩形一致：
        /// 都用 WorldPosition + Size * WorldScale，否则嵌套或缩放后点击会错位
        /// </summary>
        public Rectangle Bounds
        {
            get
            {
                Vector2 worldSize = Size * WorldScale;
                Vector2 LeftTopWorldPos = WorldPosition - Pivot * worldSize;
                return new Rectangle(
                    (int)Math.Round(LeftTopWorldPos.X),
                    (int)Math.Round(LeftTopWorldPos.Y),
                    (int)Math.Round(worldSize.X),
                    (int)Math.Round(worldSize.Y));
            }
        }

        public int Priority { get; set; } = 0;

        public bool OnPointerDown(KPointerEventArgs args)
        {
            if (!_interactable) return false;

            _isPointerDown = true;
            RefreshState();
            PointerDownEvent?.Invoke(this, args);
            return true; // 消费事件，阻止穿透到下层
        }

        public bool OnPointerUp(KPointerEventArgs args)
        {
            if (!_interactable) return false;

            _isPointerDown = false;
            RefreshState();
            PointerUpEvent?.Invoke(this, args);
            return true;
        }

        public bool OnPointerClick(KPointerEventArgs args)
        {
            if (!_interactable) return false;

            PointerClickEvent?.Invoke(this, args);
            return true;
        }

        public void OnPointerEnter(KPointerEventArgs args)
        {
            if (!_interactable) return;

            _isPointerInside = true;
            RefreshState();
            PointerEnterEvent?.Invoke(this, args);
        }

        public void OnPointerExit(KPointerEventArgs args)
        {
            _isPointerInside = false;
            // 指针移出时同步松开，避免在外面抬起后仍停留在按下态
            _isPointerDown = false;
            RefreshState();
            PointerExitEvent?.Invoke(this, args);
        }
    }
}
