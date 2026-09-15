using System;
using System.Buffers.Binary;

namespace KFramework.MonoGame
{
    /// <summary>
    /// 输入总入口。
    ///
    /// <para>JS 侧（tsengine/src/input.ts）是【薄绑定层】：只注册监听、把原始事件入队，
    /// 不做键码映射、不维护任何状态。本类每帧一次把整队事件取回，
    /// 自己维护：按键电平 / 本帧按下 / 本帧抬起、鼠标位置与按键、触点表与阶段。</para>
    ///
    /// <para>相比"JS 维护电平、C# 差分"的旧做法，这样有三个好处：
    /// 1) 逻辑集中在 C#，可断点可单测，不用改 TS 再重新构建；
    /// 2) 事件不丢 —— 电平差分会漏掉"同一帧内按下又抬起"的按键；
    /// 3) 移植小游戏时只需替换薄绑定层，C# 逻辑一行不动。</para>
    ///
    /// <para>跨界次数仍然是每帧 1 次（pollInput 一次取走整队），
    /// 避免 mousemove / touchmove 这类高频事件逐个回调造成抖动。</para>
    /// </summary>
    public static partial class Input
    {
        /// <summary>事件类型，必须与 tsengine/src/input.ts 的 InputEventType 完全一致。</summary>
        internal enum InputEventType
        {
            KeyDown = 1,
            KeyUp = 2,
            MouseDown = 3,
            MouseUp = 4,
            MouseMove = 5,
            Wheel = 6,
            TouchStart = 7,
            TouchMove = 8,
            TouchEnd = 9,
            Blur = 10,
        }

        /// <summary>单帧最多处理的事件数（与 JS 侧 MAX_EVENTS 一致）。</summary>
        private const int MaxEvents = 128;

        /// <summary>每条事件字节数（5 个 i32，与 JS 侧 EVENT_STRIDE 一致）。</summary>
        private const int EventStride = 20;

        /// <summary>键槽数量，键码取值范围 [0, KeyCount)。</summary>
        public const int KeyCount = 256;

        /// <summary>
        /// 是否移动端平台。用 .NET 自带的判断，不跨界调用 JS，
        /// 因此可以在每帧的 IsAvailable 里放心使用。
        /// </summary>
        public static bool IsMobileDevice => OperatingSystem.IsAndroid() || OperatingSystem.IsIOS();

        /// <summary>最多同时跟踪的触点数。</summary>
        public const int MaxTouchCount = 8;

        private const int MaxMouseButtons = 8;

        /// <summary>事件缓冲大小：4 字节事件数 + 事件数组。</summary>
        internal const int BufferSize = 4 + MaxEvents * EventStride;

        private static readonly byte[] _eventBuffer = new byte[BufferSize];

        // ===== 键盘：电平 + 本帧边沿 =====
        private static readonly bool[] _keyHeld = new bool[KeyCount];
        private static readonly bool[] _keyPressed = new bool[KeyCount];
        private static readonly bool[] _keyReleased = new bool[KeyCount];

        // ===== 鼠标 =====
        private static int _mouseX, _mouseY;
        private static int _mouseButtons, _prevMouseButtons;
        private static int _wheelDelta;
        private static readonly bool[] _mousePressed = new bool[MaxMouseButtons];
        private static readonly bool[] _mouseReleased = new bool[MaxMouseButtons];

        // ===== 触摸 =====
        private static readonly List<TouchPoint> _touches = new List<TouchPoint>();
        private static readonly List<TouchPoint> _beganTouches = new List<TouchPoint>();
        private static readonly List<TouchPoint> _movedTouches = new List<TouchPoint>();
        private static readonly List<TouchPoint> _endedTouches = new List<TouchPoint>();

        /// <summary>当前留在屏上的触点。</summary>
        public static IReadOnlyList<TouchPoint> Touches => _touches;

        /// <summary>本帧新按下的触点。</summary>
        public static IReadOnlyList<TouchPoint> BeganTouches => _beganTouches;

        /// <summary>本帧位置发生变化的触点。</summary>
        public static IReadOnlyList<TouchPoint> MovedTouches => _movedTouches;

        /// <summary>本帧抬起的触点。</summary>
        public static IReadOnlyList<TouchPoint> EndedTouches => _endedTouches;

        private static int ReadInt(byte[] buffer, int offset)
            => BinaryPrimitives.ReadInt32LittleEndian(buffer.AsSpan(offset, 4));

        /// <summary>每帧由 <see cref="Game"/> 调用一次：清空边沿，取回事件队列并应用。</summary>
        internal static void Poll()
        {
            // 边沿只在当帧有效，先清
            Array.Clear(_keyPressed);
            Array.Clear(_keyReleased);
            Array.Clear(_mousePressed);
            Array.Clear(_mouseReleased);
            _beganTouches.Clear();
            _movedTouches.Clear();
            _endedTouches.Clear();
            _wheelDelta = 0;
            _prevMouseButtons = _mouseButtons;

            JSBind_Input.PollInput(_eventBuffer);

            int count = ReadInt(_eventBuffer, 0);
            if (count <= 0) return;
            if (count > MaxEvents) count = MaxEvents;

            for (int i = 0; i < count; i++)
            {
                int off = 4 + i * EventStride;
                ApplyEvent(
                    (InputEventType)ReadInt(_eventBuffer, off),
                    ReadInt(_eventBuffer, off + 4),
                    ReadInt(_eventBuffer, off + 8),
                    ReadInt(_eventBuffer, off + 12),
                    ReadInt(_eventBuffer, off + 16));
            }
        }

        private static void ApplyEvent(InputEventType type, int a, int b, int c, int d)
        {
            switch (type)
            {
                case InputEventType.KeyDown:
                    SetKey(a, true);
                    break;

                case InputEventType.KeyUp:
                    SetKey(a, false);
                    break;

                case InputEventType.MouseDown:
                    _mouseX = b; _mouseY = c;
                    SetMouseButton(a, true);
                    break;

                case InputEventType.MouseUp:
                    _mouseX = b; _mouseY = c;
                    SetMouseButton(a, false);
                    break;

                case InputEventType.MouseMove:
                    _mouseX = b; _mouseY = c;
                    break;

                case InputEventType.Wheel:
                    _mouseX = b; _mouseY = c;
                    _wheelDelta += d;
                    break;

                case InputEventType.TouchStart:
                    // 用事件而不是"还在屏上的列表"来判定阶段，抬手与按下即使发生在同一帧也不会丢
                    AddTouch(new TouchPoint(a, new Vector2(b, c)));
                    break;

                case InputEventType.TouchMove:
                    MoveTouch(a, new Vector2(b, c));
                    break;

                case InputEventType.TouchEnd:
                    RemoveTouch(a);
                    break;

                case InputEventType.Blur:
                    Reset();
                    break;
            }
        }

        private static void SetKey(int keyCode, bool down)
        {
            int k = keyCode & 0xFF;
            if (k <= 0 || k >= KeyCount) return;

            if (down)
            {
                // 浏览器长按会连发 keydown，只有"从没按下"的那次才算本帧按下
                if (!_keyHeld[k]) _keyPressed[k] = true;
                _keyHeld[k] = true;
            }
            else
            {
                if (_keyHeld[k]) _keyReleased[k] = true;
                _keyHeld[k] = false;
            }
        }

        private static void SetMouseButton(int button, bool down)
        {
            if (button < 0 || button >= MaxMouseButtons) return;

            int bit = 1 << button;
            if (down)
            {
                if ((_mouseButtons & bit) == 0) _mousePressed[button] = true;
                _mouseButtons |= bit;
            }
            else
            {
                if ((_mouseButtons & bit) != 0) _mouseReleased[button] = true;
                _mouseButtons &= ~bit;
            }
        }

        private static void AddTouch(TouchPoint point)
        {
            _touches.Add(point);
            _beganTouches.Add(point);
        }

        private static void MoveTouch(int id, Vector2 position)
        {
            for (int i = 0; i < _touches.Count; i++)
            {
                if (_touches[i].Id != id) continue;

                var moved = new TouchPoint(id, position);
                _touches[i] = moved;
                _movedTouches.Add(moved);
                return;
            }
        }

        private static void RemoveTouch(int id)
        {
            for (int i = 0; i < _touches.Count; i++)
            {
                if (_touches[i].Id != id) continue;

                _endedTouches.Add(_touches[i]);
                _touches.RemoveAt(i);
                return;
            }
        }

        /// <summary>清空全部输入状态（失焦、切场景）。</summary>
        public static void Reset()
        {
            Array.Clear(_keyHeld);
            Array.Clear(_keyPressed);
            Array.Clear(_keyReleased);
            Array.Clear(_mousePressed);
            Array.Clear(_mouseReleased);
            _mouseButtons = 0;
            _prevMouseButtons = 0;
            _wheelDelta = 0;
            _touches.Clear();
            _beganTouches.Clear();
            _movedTouches.Clear();
            _endedTouches.Clear();
        }

        /// <summary>解绑 JS 侧全部监听（切场景 / 销毁时调用，避免监听器泄漏）。</summary>
        public static void Unbind()
        {
            JSBind_Input.UnbindInput();
            Reset();
        }

        // ===== 零 GC 的单键查询（热路径用这个，避免 KeyboardState 的数组分配） =====

        public static bool IsKeyDown(Keys key) => key != Keys.None && _keyHeld[(int)key];
        public static bool IsKeyPressed(Keys key) => key != Keys.None && _keyPressed[(int)key];
        public static bool IsKeyReleased(Keys key) => key != Keys.None && _keyReleased[(int)key];

        public static bool IsMouseButtonDown(int button)
            => button >= 0 && button < MaxMouseButtons && (_mouseButtons & (1 << button)) != 0;

        public static bool IsMouseButtonPressed(int button)
            => button >= 0 && button < MaxMouseButtons && _mousePressed[button];

        public static bool IsMouseButtonReleased(int button)
            => button >= 0 && button < MaxMouseButtons && _mouseReleased[button];

        // ===== 快照 =====

        public static KeyboardState GetKeyboardState() => new(_keyHeld, _keyPressed, _keyReleased);

        public static MouseState GetMouseState()
            => new(_mouseX, _mouseY, _mouseButtons, _wheelDelta, _prevMouseButtons);

        public static TouchCollection GetTouchState() => new(_touches);

        /// <summary>触屏或鼠标当前是否按住（移动端虚拟摇杆等统一处理）。</summary>
        public static bool IsPointerDown
        {
            get
            {
                if ((_mouseButtons & 1) != 0) return true;
                return _touches.Count > 0;
            }
        }
    }
}
