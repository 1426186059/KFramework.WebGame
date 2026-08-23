using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;

namespace KFramework.MonoGame
{
    /// <summary>
    /// 键盘输入设备
    /// </summary>
    public class KKeyboardInput : IKInputDevice
    {
        public string Name => "Keyboard";
        public bool Enabled { get; set; } = true;
        public bool IsAvailable => !KFramework.MonoGame.GameConst.IsMobile;

        private KeyboardState _prev;
        private KeyboardState _curr;

        /// <summary>本帧刚按下的按键</summary>
        private readonly List<Keys> _pressedThisFrame = new List<Keys>();

        /// <summary>本帧刚抬起的按键</summary>
        private readonly List<Keys> _releasedThisFrame = new List<Keys>();

        /// <summary>任意键按下事件</summary>
        public event Action<Keys> KeyDown;

        /// <summary>任意键抬起事件</summary>
        public event Action<Keys> KeyUp;

        public KeyboardState CurrentState => _curr;
        public KeyboardState PreviousState => _prev;

        public IReadOnlyList<Keys> PressedThisFrame => _pressedThisFrame;
        public IReadOnlyList<Keys> ReleasedThisFrame => _releasedThisFrame;

        public void Init()
        {
            _curr = Keyboard.GetState();
            _prev = _curr;
        }

        public void Update(GameTime gameTime)
        {
            _prev = _curr;
            _curr = Keyboard.GetState();

            _pressedThisFrame.Clear();
            _releasedThisFrame.Clear();

            // 本帧按下
            var currKeys = _curr.GetPressedKeys();
            for (int i = 0; i < currKeys.Length; i++)
            {
                if (_prev.IsKeyUp(currKeys[i]))
                {
                    _pressedThisFrame.Add(currKeys[i]);
                    KeyDown?.Invoke(currKeys[i]);
                }
            }

            // 本帧抬起
            var prevKeys = _prev.GetPressedKeys();
            for (int i = 0; i < prevKeys.Length; i++)
            {
                if (_curr.IsKeyUp(prevKeys[i]))
                {
                    _releasedThisFrame.Add(prevKeys[i]);
                    KeyUp?.Invoke(prevKeys[i]);
                }
            }
        }

        public void Reset()
        {
            _curr = Keyboard.GetState();
            _prev = _curr;
            _pressedThisFrame.Clear();
            _releasedThisFrame.Clear();
        }

        /// <summary>按键是否处于按住状态</summary>
        public bool GetKey(Keys key) => _curr.IsKeyDown(key);

        /// <summary>按键是否本帧刚按下</summary>
        public bool GetKeyDown(Keys key) => _curr.IsKeyDown(key) && _prev.IsKeyUp(key);

        /// <summary>按键是否本帧刚抬起</summary>
        public bool GetKeyUp(Keys key) => _curr.IsKeyUp(key) && _prev.IsKeyDown(key);

        /// <summary>是否有任意键按住</summary>
        public bool AnyKey => _curr.GetPressedKeyCount() > 0;

        /// <summary>是否有任意键本帧刚按下</summary>
        public bool AnyKeyDown => _pressedThisFrame.Count > 0;

        public KPressState GetKeyState(Keys key)
        {
            bool now = _curr.IsKeyDown(key);
            bool before = _prev.IsKeyDown(key);
            if (now && !before) return KPressState.Down;
            if (now) return KPressState.Held;
            if (before) return KPressState.Up;
            return KPressState.None;
        }

        /// <summary>Shift 是否按住</summary>
        public bool Shift => GetKey(Keys.LeftShift) || GetKey(Keys.RightShift);

        /// <summary>Ctrl 是否按住</summary>
        public bool Ctrl => GetKey(Keys.LeftControl) || GetKey(Keys.RightControl);

        /// <summary>Alt 是否按住</summary>
        public bool Alt => GetKey(Keys.LeftAlt) || GetKey(Keys.RightAlt);

        /// <summary>
        /// WASD / 方向键组成的二维轴，范围 [-1,1]，Y 向下为正（与屏幕坐标一致）
        /// </summary>
        public Vector2 GetAxis()
        {
            float x = 0f, y = 0f;
            if (GetKey(Keys.A) || GetKey(Keys.Left)) x -= 1f;
            if (GetKey(Keys.D) || GetKey(Keys.Right)) x += 1f;
            if (GetKey(Keys.W) || GetKey(Keys.Up)) y -= 1f;
            if (GetKey(Keys.S) || GetKey(Keys.Down)) y += 1f;
            return new Vector2(x, y);
        }
    }
}
