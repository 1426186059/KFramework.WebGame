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

        private const int Stride = 8;            // 每条 2 个 i32
        private const int MaxEvents = 64;

        public const int KeyCount = 256;

        private static readonly byte[] _buffer = new byte[4 + MaxEvents * Stride];

        private static readonly bool[] _held = new bool[KeyCount];
        private static readonly bool[] _pressed = new bool[KeyCount];
        private static readonly bool[] _released = new bool[KeyCount];

        /// <summary>任意键按下</summary>
        public static event Action<Keys> KeyDown;

        /// <summary>任意键抬起</summary>
        public static event Action<Keys> KeyUp;

        internal static bool[] Held => _held;
        internal static bool[] Pressed => _pressed;
        internal static bool[] Released => _released;

        private static int ReadInt(int offset)
            => BinaryPrimitives.ReadInt32LittleEndian(_buffer.AsSpan(offset, 4));

        /// <summary>每帧调用一次：取回本模块的事件队列并更新状态。</summary>
        public static void Poll()
        {
            Array.Clear(_pressed);
            Array.Clear(_released);

            JSBind_Input.PollKeyboard(_buffer);

            int count = ReadInt(0);
            if (count <= 0) return;
            if (count > MaxEvents) count = MaxEvents;

            for (int i = 0; i < count; i++)
            {
                int off = 4 + i * Stride;
                int type = ReadInt(off);
                int keyCode = ReadInt(off + 4);

                switch (type)
                {
                    case EvKeyDown: SetKey(keyCode, true); break;
                    case EvKeyUp: SetKey(keyCode, false); break;
                    case EvBlur: Reset(); break;
                }
            }

            for (int k = 1; k < KeyCount; k++)
            {
                if (_pressed[k]) KeyDown?.Invoke((Keys)k);
                else if (_released[k]) KeyUp?.Invoke((Keys)k);
            }
        }

        private static void SetKey(int keyCode, bool down)
        {
            int k = keyCode & 0xFF;
            if (k <= 0 || k >= KeyCount) return;

            if (down)
            {
                // 浏览器长按会连发 keydown，只有"从没按下"的那次才算本帧按下
                if (!_held[k]) _pressed[k] = true;
                _held[k] = true;
            }
            else
            {
                if (_held[k]) _released[k] = true;
                _held[k] = false;
            }
        }

        /// <summary>清空键盘状态（失焦时由 Blur 事件触发）。</summary>
        public static void Reset()
        {
            Array.Clear(_held);
            Array.Clear(_pressed);
            Array.Clear(_released);
        }

        /// <summary>固定步长下，一个渲染帧可能跑多个 Update 步；在每个步结束后清空按下/抬起边沿，
        /// 确保一次按键只被识别一次（否则边沿会在多个步里重复触发，导致"按一次"的逻辑随帧时序抖动）。
        /// 下一帧 <see cref="Poll"/> 时边沿重新产生。</summary>
        public static void ConsumeEdges()
        {
            Array.Clear(_pressed);
            Array.Clear(_released);
        }

        /// <summary>解绑 JS 侧监听（切场景 / 销毁时调用）。</summary>
        public static void Unbind()
        {
            JSBind_Input.UnbindKeyboard();
            Reset();
        }

        // ===== 查询 =====

        public static bool GetKey(Keys key) => key != Keys.None && _held[(int)key];

        public static bool GetKeyDown(Keys key) => key != Keys.None && _pressed[(int)key];

        public static bool GetKeyUp(Keys key) => key != Keys.None && _released[(int)key];

        public static bool AnyKey
        {
            get
            {
                for (int i = 1; i < KeyCount; i++)
                    if (_held[i]) return true;
                return false;
            }
        }

        public static bool AnyKeyDown
        {
            get
            {
                for (int i = 1; i < KeyCount; i++)
                    if (_pressed[i]) return true;
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
