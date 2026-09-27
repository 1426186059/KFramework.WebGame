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
        private const int EvMousePos = 5;
        private const int EvMouseDown = 3;
        private const int EvMouseUp = 4;
        private const int EvWheel = 6;

        private const int MaxEvents = 64;
        private const int MaxButtons = 8;

        // 紧凑协议（与 input_mouse.ts 对齐）：全事件驱动、变长记录。
        //   [0]        nEvents(1B)
        //   [1..]      事件流：EvMousePos=5B(type + x:short + y:short)；其余=2B(type + payload)
        private const int CountSize = 1;
        private const int PosRec = 5;
        private const int EvRec = 2;
        private static readonly byte[] _buffer = new byte[CountSize + PosRec + MaxEvents * EvRec];

        private static int _x, _y;
        private static int _prevX, _prevY;
        private static int _buttons, _prevButtons;
        private static int _wheelDelta;
        private static int _scrollValue;
        private static readonly bool[] _pressed = new bool[MaxButtons];
        private static readonly bool[] _released = new bool[MaxButtons];

        /// <summary>本装置是否处于激活状态；未激活时 <see cref="Update"/> / <see cref="LateUpdate"/> 直接跳过。由 <see cref="Activate"/> / <see cref="Deactivate"/> 维护。</summary>
        public static bool Active { get; private set; }

        /// <summary>按键按下（参数：按键、坐标）</summary>
        public static event Action<MouseButton, Vector2> ButtonDown;

        /// <summary>按键抬起</summary>
        public static event Action<MouseButton, Vector2> ButtonUp;

        /// <summary>滚轮滚动（本帧增量）</summary>
        public static event Action<int> ScrollWheel;

        /// <summary>每帧调用一次：取回本模块的事件队列并更新状态。</summary>
        public static void Update()
        {
            if (!Active) return;
            Array.Clear(_pressed);
            Array.Clear(_released);
            _wheelDelta = 0;
            _prevButtons = _buttons;
            _prevX = _x;
            _prevY = _y;

            JSBind_Input_Mouse.PollMouse(_buffer);

            // 事件流：按类型变长跳步（EvMousePos=5B，其余=2B）
            int count = _buffer[0];
            if (count > MaxEvents) count = MaxEvents;

            int off = CountSize;
            for (int i = 0; i < count; i++)
            {
                int type = _buffer[off];
                switch (type)
                {
                    case EvMousePos:
                        _x = BinaryPrimitives.ReadInt16LittleEndian(_buffer.AsSpan(off + 1, 2));
                        _y = BinaryPrimitives.ReadInt16LittleEndian(_buffer.AsSpan(off + 3, 2));
                        off += PosRec;
                        break;
                    case EvMouseDown:
                        SetButton(_buffer[off + 1], true);
                        off += EvRec;
                        break;
                    case EvMouseUp:
                        SetButton(_buffer[off + 1], false);
                        off += EvRec;
                        break;
                    case EvWheel:
                        _wheelDelta += (sbyte)_buffer[off + 1];   // 按 sbyte 解读累计增量
                        off += EvRec;
                        break;
                    default:
                        off += EvRec;   // 未知类型按最小记录跳过，避免越界死循环
                        break;
                }
            }

            var pos = Position;
            for (int b = 0; b < MaxButtons; b++)
            {
                var btn = ToButton(b);
                // 同一帧内可能同时出现 按下+抬起（快速拖甩 / 卡顿把两条事件攒进同一帧）。
                // 必须用两个独立 if：若写成 if/else if，_pressed 为真时会跳过 _released，
                // 导致 ButtonUp 永不触发，调用方（如面板拖动）的“松手”永远收不到，表现为“松手仍在拖动”。
                // 原版 WinForms 是离散事件不会批量，此 bug 是 poll/队列模型引入的。
                if (_pressed[b]) ButtonDown?.Invoke(btn, pos);
                if (_released[b]) ButtonUp?.Invoke(btn, pos);
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
        /// 确保一次点击只被识别一次（否则边沿会在多个步里重复触发）。下一帧 <see cref="Update"/> 时边沿重新产生。</summary>
        public static void LateUpdate()
        {
            if (!Active) return;
            Array.Clear(_pressed);
            Array.Clear(_released);
        }

        /// <summary>解绑 JS 侧监听。</summary>
        public static void Deactivate()
        {
            JSBind_Input_Mouse.UnbindMouse();
            Reset();
            Active = false;
        }

        /// <summary>激活装置：鼠标 JS 模块在脚本加载时即自动绑定监听，无需显式 Bind。空实现，仅置 <see cref="Active"/> 标记。</summary>
        public static void Activate() { Active = true; }

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
