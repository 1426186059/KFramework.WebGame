using System;
using System.Buffers.Binary;

namespace KFramework.MonoGame
{
    /// <summary>
    /// 鼠标 —— 对原始输入事件的封装。
    ///
    /// 自己 poll 自己的事件队列（<c>input_mouse</c> 模块），维护位置 / 按键 / 滚轮增量。
    /// 查询返回的是值类型快照，可以安全跨帧比较。
    /// </summary>
    public static class Input_Mouse
    {
        // 事件类型（与 input_mouse.ts 一致）
        private const int EvMouseDown = 3;
        private const int EvMouseUp = 4;
        private const int EvMouseMove = 5;
        private const int EvWheel = 6;

        private const int Stride = 20;           // 每条 5 个 i32
        private const int MaxEvents = 64;
        private const int MaxButtons = 8;

        private static readonly byte[] _buffer = new byte[4 + MaxEvents * Stride];

        private static int _x, _y;
        private static int _prevX, _prevY;
        private static int _buttons, _prevButtons;
        private static int _wheelDelta;
        private static int _scrollValue;
        private static readonly bool[] _pressed = new bool[MaxButtons];
        private static readonly bool[] _released = new bool[MaxButtons];

        /// <summary>按键按下（参数：按键、坐标）</summary>
        public static event Action<MouseButton, Vector2> ButtonDown;

        /// <summary>按键抬起</summary>
        public static event Action<MouseButton, Vector2> ButtonUp;

        /// <summary>滚轮滚动（本帧增量）</summary>
        public static event Action<int> ScrollWheel;

        private static int ReadInt(int offset)
            => BinaryPrimitives.ReadInt32LittleEndian(_buffer.AsSpan(offset, 4));

        /// <summary>每帧调用一次：取回本模块的事件队列并更新状态。</summary>
        public static void Poll()
        {
            Array.Clear(_pressed);
            Array.Clear(_released);
            _wheelDelta = 0;
            _prevButtons = _buttons;
            _prevX = _x;
            _prevY = _y;

            JSBind_Input.PollMouse(_buffer);

            int count = ReadInt(0);
            if (count <= 0) return;
            if (count > MaxEvents) count = MaxEvents;

            for (int i = 0; i < count; i++)
            {
                int off = 4 + i * Stride;
                int type = ReadInt(off);
                int button = ReadInt(off + 4);
                int x = ReadInt(off + 8);
                int y = ReadInt(off + 12);
                int wheel = ReadInt(off + 16);

                _x = x; _y = y;

                switch (type)
                {
                    case EvMouseDown: SetButton(button, true); break;
                    case EvMouseUp: SetButton(button, false); break;
                    case EvWheel: _wheelDelta += wheel; break;
                }
            }

            var pos = Position;
            for (int b = 0; b < MaxButtons; b++)
            {
                var btn = ToButton(b);
                if (_pressed[b]) ButtonDown?.Invoke(btn, pos);
                else if (_released[b]) ButtonUp?.Invoke(btn, pos);
            }

            if (_wheelDelta != 0) ScrollWheel?.Invoke(_wheelDelta);
            _scrollValue += _wheelDelta;
        }

        private static void SetButton(int button, bool down)
        {
            if (button < 0 || button >= MaxButtons) return;

            int bit = 1 << button;
            if (down)
            {
                if ((_buttons & bit) == 0) _pressed[button] = true;
                _buttons |= bit;
            }
            else
            {
                if ((_buttons & bit) != 0) _released[button] = true;
                _buttons &= ~bit;
            }
        }

        public static void Reset()
        {
            _buttons = 0;
            _prevButtons = 0;
            _wheelDelta = 0;
            Array.Clear(_pressed);
            Array.Clear(_released);
        }

        /// <summary>固定步长下，一个渲染帧可能跑多个 Update 步；在每个步结束后清空按下/抬起边沿，
        /// 确保一次点击只被识别一次（否则边沿会在多个步里重复触发）。下一帧 <see cref="Poll"/> 时边沿重新产生。</summary>
        public static void ConsumeEdges()
        {
            Array.Clear(_pressed);
            Array.Clear(_released);
        }

        /// <summary>解绑 JS 侧监听。</summary>
        public static void Unbind()
        {
            JSBind_Input.UnbindMouse();
            Reset();
        }

        // ===== 查询 =====

        public static Vector2 Position => new Vector2(_x, _y);

        /// <summary>本帧位移</summary>
        public static Vector2 Delta => new Vector2(_x - _prevX, _y - _prevY);

        /// <summary>本帧是否移动过</summary>
        public static bool Moved => _x != _prevX || _y != _prevY;

        public static int X => _x;
        public static int Y => _y;

        /// <summary>滚轮本帧增量</summary>
        public static int ScrollDelta => _wheelDelta;

        /// <summary>滚轮累计值</summary>
        public static int ScrollValue => _scrollValue;

        /// <summary>横向滚轮 —— 浏览器不支持，恒为 0</summary>
        public static int HorizontalScrollDelta => 0;

        public static bool GetButton(MouseButton button) => (Buttons & (1 << (int)button)) != 0;

        public static bool GetButtonDown(MouseButton button)
        {
            int b = (int)button;
            return b >= 0 && b < MaxButtons && _pressed[b];
        }

        public static bool GetButtonUp(MouseButton button)
        {
            int b = (int)button;
            return b >= 0 && b < MaxButtons && _released[b];
        }

        public static KPressState GetButtonState(MouseButton button)
        {
            if (GetButtonDown(button)) return KPressState.Down;
            if (GetButton(button)) return KPressState.Held;
            if (GetButtonUp(button)) return KPressState.Up;
            return KPressState.None;
        }

        /// <summary>本帧按键位图（供快照用）</summary>
        public static int Buttons => _buttons;

        /// <summary>上一帧按键位图（供快照用）</summary>
        public static int PreviousButtons => _prevButtons;

        /// <summary>是否在指定区域内</summary>
        public static bool IsInside(int width, int height)
            => _x >= 0 && _x < width && _y >= 0 && _y < height;

        /// <summary>设置鼠标位置 —— 浏览器不允许脚本移动光标，空实现</summary>
        public static void SetPosition(int x, int y)
        {
        }

        private static MouseButton ToButton(int index)
        {
            return index switch
            {
                0 => MouseButton.Left,
                1 => MouseButton.Right,
                2 => MouseButton.Middle,
                3 => MouseButton.XButton1,
                4 => MouseButton.XButton2,
                _ => MouseButton.Left,
            };
        }
    }
}
