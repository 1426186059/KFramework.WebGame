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

        /// <summary>任意键 刚按下</summary>
        public static event Action<Keys> KeyDown;
        /// <summary>任意键 刚抬起</summary>
        public static event Action<Keys> KeyUp;
        /// <summary>任意键按住</summary>
        public static event Action<Keys> KeyPress;

        internal static bool[] Held => _held;
        internal static bool[] Pressed => _pressed;
        internal static bool[] Released => _released;

        /// <summary>本装置是否处于激活状态；未激活时 <see cref="Update"/> / <see cref="LateUpdate"/> 直接跳过。由 <see cref="Activate"/> / <see cref="Unbind"/> 维护。</summary>
        public static bool Active { get; private set; }

        private static int ReadInt(int offset)
            => BinaryPrimitives.ReadInt32LittleEndian(_buffer.AsSpan(offset, 4));

        /// <summary>每帧调用一次：取回本模块的事件队列并更新状态。</summary>
        /// <remarks>
        /// 注意：<b>不要在开头清空按下/抬起边沿</b>。边沿（<see cref="_pressed"/> / <see cref="_released"/>）
        /// 由 <see cref="LateUpdate"/> 在固定步长的每个 Update 步结束后清空，因此会跨帧保留，
        /// 直到被某个真正运行的 Update 步消费。若在此处清空，则在 <c>steps==0</c> 的帧（高刷新率或
        /// 时序抖动导致 accumulator 不足一步）里读到的按键边沿会被下一帧的 Update 抹掉，造成按键丢失——
        /// 尤其表现为“跳跃/确认”等边沿触发的操作偶发或完全失灵、相应音效不播放。
        /// </remarks>
        public static void Update()
        {
            if (!Active) return;
            JSBind_Input_Keyboard.PollKeyboard(_buffer);

            int count = ReadInt(0);
            if (count > 0) PrintTool.Log($"[DBG] Poll count={count} firstKey={(count > 0 ? ReadInt(4 + 4) : -1)}");
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
        }

        private static void SetKey(int keyCode, bool down)
        {
            int k = keyCode & 0xFF;
            if (k <= 0 || k >= KeyCount) return;
            if (k == 38) Console.WriteLine($"[DBG] SetKey 38 down={down} heldBefore={_held[38]}");

            if (down)
            {
                // 浏览器长按会连发 keydown，只有"从没按下"的那次才算本次按下；
                // 同时触发 KeyDown 事件（仅在状态跳变时，避免跨帧重复触发）。
                if (!_held[k])
                {
                    _pressed[k] = true;
                    var key = (Keys)k;
                    KeyDown?.Invoke(key);
                    // 对齐 WinForms：KeyPress 跟在 KeyDown 之后，且只对能产生字符的键触发。
                    if (HasChar(key))
                        KeyPress?.Invoke(key);
                }
                _held[k] = true;
            }
            else
            {
                if (_held[k])
                {
                    _released[k] = true;
                    KeyUp?.Invoke((Keys)k);
                }
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
        /// 下一帧 <see cref="Update"/> 时边沿重新产生。</summary>
        public static void LateUpdate()
        {
            if (!Active) return;
            Array.Clear(_pressed);
            Array.Clear(_released);
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

        // ===== 查询 =====

        public static bool GetKey(Keys key) => key != Keys.None && _held[(int)key];

        public static bool GetKeyDown(Keys key)
        {
            if ((int)key == 38) Console.WriteLine($"[DBG] GetKeyDown Up: _pressed[38]={_pressed[38]} held[38]={_held[38]}");
            return key != Keys.None && _pressed[(int)key];
        }

        public static bool GetKeyUp(Keys key) => key != Keys.None && _released[(int)key];

        /// <summary>构造当前帧的键盘快照（含电平 / 按下 / 抬起）。</summary>
        public static KeyboardState GetKeyboardState() => new(Held, Pressed, Released);

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

        // 数字键在 Shift 按下时的字符（与 US 布局一致，索引 = 键码 - Keys.D0）。
        private const string ShiftedDigits = ")!@#$%^&*(";

        /// <summary>
        /// 键码 → 字符，即 WinForms <c>KeyPressEventArgs.KeyChar</c> 的取值约定：
        /// 控制键沿用 ASCII 控制码（Backspace=8 / Tab=9 / Enter=13 / Escape=27），
        /// 字母 / 数字按当前 <see cref="Shift"/> 电平给出字面量，空格给 ' '。
        /// </summary>
        /// <returns>映射不出字符的键（方向键、Home/End、修饰键等）返回 '\0'。</returns>
        /// <remarks>
        /// 只覆盖 <see cref="Keys"/> 枚举里存在的键：浏览器 keyCode 本身不区分大小写 / 键盘布局，
        /// 真正的本地化字符输入（含 IME）由 DOM 输入层负责，不经过这里。
        /// </remarks>
        public static char ToChar(Keys key)
        {
            switch (key)
            {
                case Keys.Backspace: return '\b';
                case Keys.Tab: return '\t';
                case Keys.Enter: return '\r';
                case Keys.Escape: return (char)27;
                case Keys.Space: return ' ';
            }

            if (key >= Keys.D0 && key <= Keys.D9)
            {
                int d = (int)key - (int)Keys.D0;
                return Shift ? ShiftedDigits[d] : (char)('0' + d);
            }

            if (key >= Keys.A && key <= Keys.Z)
            {
                char c = (char)('a' + ((int)key - (int)Keys.A));
                return Shift ? char.ToUpperInvariant(c) : c;
            }

            return '\0';
        }

        /// <summary>该键是否会产生 <see cref="KeyPress"/>（即能映射出字符）。</summary>
        public static bool HasChar(Keys key) => ToChar(key) != '\0';

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
