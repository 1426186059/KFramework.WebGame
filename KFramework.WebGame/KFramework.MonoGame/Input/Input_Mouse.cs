using System;

namespace KFramework.MonoGame
{
    /// <summary>
    /// 鼠标输入设备（基石层）。
    ///
    /// <para><see cref="MouseState"/> 是纯值类型，因此可以安全地跨帧缓存来做边沿检测
    /// （与 KeyboardState 不同，后者持有 Input 的状态数组引用，不能跨帧缓存）。</para>
    ///
    /// <para>适配说明：本引擎只上报左 / 中 / 右三键，不支持 XButton；
    /// 滚轮只有本帧增量；浏览器不允许脚本移动光标，因此没有 SetPosition 能力。</para>
    /// </summary>
    public class Input_Mouse
    {
        public bool Enabled { get; set; } = true;
        public bool IsAvailable => !Input.IsMobileDevice;

        private MouseState _prev;
        private MouseState _curr;
        private int _scrollValue;

        private static readonly MouseButton[] AllButtons =
        {
            MouseButton.Left,
            MouseButton.Right,
            MouseButton.Middle,
        };

        /// <summary>按键按下（参数：按键、屏幕坐标）</summary>
        public event Action<MouseButton, Vector2> ButtonDown;

        /// <summary>按键抬起（参数：按键、屏幕坐标）</summary>
        public event Action<MouseButton, Vector2> ButtonUp;

        /// <summary>滚轮滚动（参数：本帧增量）</summary>
        public event Action<int> ScrollWheel;

        public MouseState CurrentState => _curr;
        public MouseState PreviousState => _prev;

        /// <summary>当前屏幕坐标</summary>
        public Vector2 Position => new Vector2(_curr.X, _curr.Y);

        /// <summary>上一帧屏幕坐标</summary>
        public Vector2 PreviousPosition => new Vector2(_prev.X, _prev.Y);

        /// <summary>本帧位移</summary>
        public Vector2 Delta => Position - PreviousPosition;

        /// <summary>鼠标是否移动过</summary>
        public bool Moved => _curr.X != _prev.X || _curr.Y != _prev.Y;

        /// <summary>滚轮本帧增量</summary>
        public int ScrollDelta => _curr.ScrollDelta;

        /// <summary>滚轮累计值</summary>
        public int ScrollValue => _scrollValue;

        /// <summary>横向滚轮本帧增量 —— 浏览器端不支持，恒为 0</summary>
        public int HorizontalScrollDelta => 0;

        public void Init()
        {
            _curr = Input.GetMouseState();
            _prev = _curr;
        }

        public void Update(GameTime gameTime)
        {
            _prev = _curr;
            _curr = Input.GetMouseState();
            _scrollValue += ScrollDelta;

            for (int i = 0; i < AllButtons.Length; i++)
            {
                var btn = AllButtons[i];
                if (GetButtonDown(btn)) ButtonDown?.Invoke(btn, Position);
                else if (GetButtonUp(btn)) ButtonUp?.Invoke(btn, Position);
            }

            int scroll = ScrollDelta;
            if (scroll != 0) ScrollWheel?.Invoke(scroll);
        }

        public void Reset()
        {
            _curr = Input.GetMouseState();
            _prev = _curr;
        }

        /// <summary>按键是否按住</summary>
        public bool GetButton(MouseButton button) => GetState(_curr, button);

        /// <summary>按键是否本帧刚按下</summary>
        public bool GetButtonDown(MouseButton button)
            => GetState(_curr, button) && !GetState(_prev, button);

        /// <summary>按键是否本帧刚抬起</summary>
        public bool GetButtonUp(MouseButton button)
            => !GetState(_curr, button) && GetState(_prev, button);

        public KPressState GetButtonState(MouseButton button)
        {
            bool now = GetState(_curr, button);
            bool before = GetState(_prev, button);
            if (now && !before) return KPressState.Down;
            if (now) return KPressState.Held;
            if (before) return KPressState.Up;
            return KPressState.None;
        }

        /// <summary>设置鼠标位置 —— 浏览器不允许脚本移动光标，空实现</summary>
        public void SetPosition(int x, int y)
        {
        }

        private static bool GetState(MouseState state, MouseButton button)
        {
            return button switch
            {
                MouseButton.Left => state.LeftButton,
                MouseButton.Right => state.RightButton,
                MouseButton.Middle => state.MiddleButton,
                _ => false,
            };
        }
    }
}
