using System;
using System.Buffers.Binary;

namespace KFramework.MonoGame
{
    /// <summary>
    /// 键盘 —— 对原始输入事件的封装。
    ///
    /// <para>自己 poll 自己的事件队列（<c>input_keyboard</c> 模块），维护按键电平与
    /// 按下/抬起边沿，并对外提供查询。JS 侧只负责把原始事件入队，不做任何语义处理。</para>
    ///
    /// <para>用事件而不是"电平差分"算边沿，因此同一帧内按下又抬起也能被正确识别。</para>
    /// </summary>
    public static class Input_KeyBoard
    {
        // 事件类型（与 input_keyboard.ts 一致）
        private const int EvKeyDown = 1;
        private const int EvKeyUp = 2;
        private const int EvBlur = 10;
        private const int Stride = 2;            // 每条 2 个 i32
        private const int MaxEvents = 32;
        private static readonly byte[] _buffer = new byte[1 + MaxEvents * Stride];
        private static readonly bool[] _LastKeyState = new bool[byte.MaxValue];
        private static readonly bool[] _NewKeyState = new bool[byte.MaxValue];

        /// <summary>任意键 刚按下</summary>
        public static event Action<Keys> KeyDown;
        /// <summary>任意键 刚抬起</summary>
        public static event Action<Keys> KeyUp;
        /// <summary>任意键 持续按住</summary>
        public static event Action<Keys> KeyPress;

        /// <summary>本装置是否处于激活状态；未激活时 <see cref="Update"/> / <see cref="LateUpdate"/> 直接跳过。由 <see cref="Activate"/> / <see cref="Unbind"/> 维护。</summary>
        public static bool Active { get; private set; }

        private static byte ReadByte(int offset)
            => _buffer[offset];
        
        public static void Update()
        {
            if (!Active) return;
            JSBind_Input_Keyboard.PollKeyboard(_buffer);

            _NewKeyState.AsSpan().Clear();
            int count = ReadByte(0);
            if (count > 0)
            {
                if (count > MaxEvents)
                {
                    count = MaxEvents;
                }
                for (int i = 0; i < count; i++)
                {
                    int off = 1 + i * Stride;
                    byte keyCode = ReadByte(off);
                    byte flag = ReadByte(off + 1);
                    _NewKeyState[keyCode] = flag == EvKeyDown;
                }
            }

            for(int i = 0; i < byte.MaxValue; i++)
            {
                if (_LastKeyState[i] != _NewKeyState[i])
                {
                    if (_NewKeyState[i])
                    {
                        KeyDown?.Invoke((Keys)i);
                    }
                    else
                    {
                        KeyUp?.Invoke((Keys)i);
                    }
                }

                if (_NewKeyState[i])
                {
                    KeyPress?.Invoke((Keys)i);
                }
            }
        }

        /// <summary>清空键盘状态（失焦时由 Blur 事件触发）。</summary>
        public static void Reset()
        {
            _NewKeyState.AsSpan().Clear();
            _LastKeyState.AsSpan().Clear();
        }

        /// <summary>固定步长下，一个渲染帧可能跑多个 Update 步；在每个步结束后清空按下/抬起边沿，
        /// 确保一次按键只被识别一次（否则边沿会在多个步里重复触发，导致"按一次"的逻辑随帧时序抖动）。
        /// 下一帧 <see cref="Update"/> 时边沿重新产生。</summary>
        public static void LateUpdate()
        {
            if (!Active) return;

            for (int i = 0; i < _LastKeyState.Length; i++)
            {
                _LastKeyState[i] = _NewKeyState[i];
            }
        }

        /// <summary>解绑 JS 侧监听，并置 <see cref="Active"/> 为 false（关闭本装置采集）。</summary>
        public static void Deactivate()
        {
            JSBind_Input_Keyboard.UnbindKeyboard();
            Reset();
            Active = false;
        }

        /// <summary>激活装置：建立 / 恢复 JS 侧键盘监听（重新绑定到画布）。</summary>
        public static void Activate()
        {
            Reset();
            JSBind_Input_Keyboard.BindKeyboard();
            Active = true;
        }

        public static bool GetKey(Keys key)
        {
            return _NewKeyState[(byte)key];
        }

        public static bool GetKeyDown(Keys key)
        {
            return _NewKeyState[(byte)key] && _NewKeyState[(byte)key] != _LastKeyState[(byte)key];
        }

        public static bool GetKeyUp(Keys key)
        {
            return !_NewKeyState[(byte)key] && _NewKeyState[(byte)key] != _LastKeyState[(byte)key];
        }

        public static bool AnyKey
        {
            get
            {
                for (int i = 0; i < byte.MaxValue; i++)
                    if (GetKey((Keys)i)) return true;
                return false;
            }
        }

        public static bool AnyKeyDown
        {
            get
            {
                for (int i = 0; i < byte.MaxValue; i++)
                    if (GetKeyDown((Keys)i)) return true;
                return false;
            }
        }

        public static KPressState GetKeyState(Keys key)
        {
            if (GetKeyDown(key)) return KPressState.Down;
            if (GetKey(key)) return KPressState.Held;
            if (GetKeyUp(key)) return KPressState.Up;
            return KPressState.None;
        }

        public static bool Shift => GetKey(Keys.LeftShift) || GetKey(Keys.RightShift);
        public static bool Ctrl => GetKey(Keys.LeftControl) || GetKey(Keys.RightControl);
        public static bool Alt => GetKey(Keys.LeftAlt) || GetKey(Keys.RightAlt);

        /// <summary>WASD / 方向键组成的二维轴，Y 向下为正。</summary>
        public static Vector2 GetAxis()
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
