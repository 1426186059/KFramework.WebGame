using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;

namespace KFramework.MonoGame
{
    /// <summary>
    /// 鼠标输入设备
    /// </summary>
    public class KMouseInput : IKInputDevice
    {
        public string Name => "Mouse";
        public bool Enabled { get; set; } = true;
        public bool IsAvailable => !KFramework.MonoGame.GameConst.IsMobile;

        private MouseState _prev;
        private MouseState _curr;

        private static readonly MouseButton[] AllButtons =
        {
            MouseButton.Left,
            MouseButton.Right,
            MouseButton.Middle,
            MouseButton.XButton1,
            MouseButton.XButton2,
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
        public int ScrollDelta => _curr.ScrollWheelValue - _prev.ScrollWheelValue;

        /// <summary>滚轮累计值</summary>
        public int ScrollValue => _curr.ScrollWheelValue;

        /// <summary>横向滚轮本帧增量</summary>
        public int HorizontalScrollDelta => _curr.HorizontalScrollWheelValue - _prev.HorizontalScrollWheelValue;

        public void Init()
        {
            _curr = Mouse.GetState();
            _prev = _curr;
        }

        public void Update(GameTime gameTime)
        {
            _prev = _curr;
            _curr = Mouse.GetState();

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
            _curr = Mouse.GetState();
            _prev = _curr;
        }

        /// <summary>按键是否按住</summary>
        public bool GetButton(MouseButton button)
            => GetState(_curr, button) == ButtonState.Pressed;

        /// <summary>按键是否本帧刚按下</summary>
        public bool GetButtonDown(MouseButton button)
            => GetState(_curr, button) == ButtonState.Pressed
            && GetState(_prev, button) == ButtonState.Released;

        /// <summary>按键是否本帧刚抬起</summary>
        public bool GetButtonUp(MouseButton button)
            => GetState(_curr, button) == ButtonState.Released
            && GetState(_prev, button) == ButtonState.Pressed;

        public KPressState GetButtonState(MouseButton button)
        {
            bool now = GetState(_curr, button) == ButtonState.Pressed;
            bool before = GetState(_prev, button) == ButtonState.Pressed;
            if (now && !before) return KPressState.Down;
            if (now) return KPressState.Held;
            if (before) return KPressState.Up;
            return KPressState.None;
        }

        /// <summary>鼠标是否在窗口内</summary>
        public bool IsInsideWindow()
        {
            var vp = KSceneMgr.Game.GraphicsDevice.Viewport;
            return _curr.X >= 0 && _curr.X < vp.Width && _curr.Y >= 0 && _curr.Y < vp.Height;
        }

        /// <summary>设置鼠标位置</summary>
        public void SetPosition(int x, int y)
        {
            Mouse.SetPosition(x, y);
            _curr = Mouse.GetState();
        }

        private static ButtonState GetState(MouseState state, MouseButton button)
        {
            return button switch
            {
                MouseButton.Left => state.LeftButton,
                MouseButton.Right => state.RightButton,
                MouseButton.Middle => state.MiddleButton,
                MouseButton.XButton1 => state.XButton1,
                MouseButton.XButton2 => state.XButton2,
                _ => ButtonState.Released,
            };
        }
    }
}
