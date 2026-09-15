using System;
using System.Collections.Generic;

namespace KFramework.MonoGame
{
    /// <summary>
    /// 键盘输入设备（基石层）。
    ///
    /// <para>本类只做「事件分发与查询封装」，不保存状态：
    /// 按键电平与按下/抬起边沿全部由 <see cref="Input"/> 依据浏览器事件维护。
    /// 这样即使同一帧内按下又抬起，也能被正确识别（用电平差分会漏掉）。</para>
    /// </summary>
    public class Input_KeyBoard
    {
        public string Name => "Keyboard";
        public bool Enabled { get; set; } = true;
        public bool IsAvailable => !Input.IsMobileDevice;

        /// <summary>本帧刚按下的按键</summary>
        private readonly List<Keys> _pressedThisFrame = new List<Keys>();

        /// <summary>本帧刚抬起的按键</summary>
        private readonly List<Keys> _releasedThisFrame = new List<Keys>();

        /// <summary>任意键按下事件</summary>
        public event Action<Keys> KeyDown;

        /// <summary>任意键抬起事件</summary>
        public event Action<Keys> KeyUp;

        public KeyboardState CurrentState => Input.GetKeyboardState();

        public IReadOnlyList<Keys> PressedThisFrame => _pressedThisFrame;
        public IReadOnlyList<Keys> ReleasedThisFrame => _releasedThisFrame;

        public void Init()
        {
            _pressedThisFrame.Clear();
            _releasedThisFrame.Clear();
        }

        public void Update(GameTime gameTime)
        {
            _pressedThisFrame.Clear();
            _releasedThisFrame.Clear();

            // 直接查 Input 的边沿数组，遍历 256 个键槽，零分配
            for (int i = 1; i < Input.KeyCount; i++)
            {
                Keys key = (Keys)i;

                if (Input.IsKeyPressed(key))
                {
                    _pressedThisFrame.Add(key);
                    KeyDown?.Invoke(key);
                }
                else if (Input.IsKeyReleased(key))
                {
                    _releasedThisFrame.Add(key);
                    KeyUp?.Invoke(key);
                }
            }
        }

        public void Reset()
        {
            _pressedThisFrame.Clear();
            _releasedThisFrame.Clear();
            Input.Reset();
        }

        /// <summary>按键是否处于按住状态</summary>
        public bool GetKey(Keys key) => Input.IsKeyDown(key);

        /// <summary>按键是否本帧刚按下</summary>
        public bool GetKeyDown(Keys key) => Input.IsKeyPressed(key);

        /// <summary>按键是否本帧刚抬起</summary>
        public bool GetKeyUp(Keys key) => Input.IsKeyReleased(key);

        /// <summary>是否有任意键按住</summary>
        public bool AnyKey => Input.GetKeyboardState().AnyKeyDown;

        /// <summary>是否有任意键本帧刚按下</summary>
        public bool AnyKeyDown => _pressedThisFrame.Count > 0;

        public KPressState GetKeyState(Keys key)
        {
            if (Input.IsKeyPressed(key)) return KPressState.Down;
            if (Input.IsKeyDown(key)) return KPressState.Held;
            if (Input.IsKeyReleased(key)) return KPressState.Up;
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
