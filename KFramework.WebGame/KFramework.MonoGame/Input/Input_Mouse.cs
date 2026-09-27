using System;
using System.Buffers.Binary;

namespace KFramework.MonoGame
{
    /// <summary>
    /// 鼠标 —— 对原始输入事件的封装。
    ///
    /// <para>自己 poll 自己的事件队列（<c>input_mouse</c> 模块），维护位置 / 按键电平 / 滚轮增量。
    /// 状态管理完全对齐 <see cref="Input_KeyBoard"/>：两份电平缓冲（<c>_btnNew</c> / <c>_btnLast</c>），
    /// 按下 / 抬起边沿由两帧电平差分得出，不再堆零散变量，避免边沿状态算错。</para>
    /// </summary>
    public static class Input_Mouse
    {
        // 事件类型（与 input_mouse.ts 一致）
        private const int EvMousePos = 0;
        private const int EvMouseButton = 1;   // payload = button(低7位) | 按下(0x80)
        private const int EvWheel = 2;

        private const int MaxButtons = 8;
        private const int MaxEvents = MaxButtons + 2;   // 最坏：全部按键 + 位置 + 滚轮
        private const int CountSize = 1;
        private const int EvMousePos_ByteCount = 5;
        private const int EvMouseButton_ByteCount = 2;
        private const int EvWheel_ByteCount = 2;
        private const int MaxByteCount = CountSize + MaxEvents * EvMouseButton_ByteCount + EvMousePos_ByteCount;
        private static readonly byte[] _buffer = new byte[MaxByteCount];

        // 与键盘一致：两份电平缓冲 + 差分算边沿
        private static readonly bool[] _btnNew = new bool[MaxButtons];
        private static readonly bool[] _btnLast = new bool[MaxButtons];

        private static int _x, _y;
        private static int _lastX, _lastY;
        private static int _wheelDelta;     // 本帧滚轮增量
        private static int _scrollValue;    // 滚轮累计值

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
            _wheelDelta = 0;

            JSBind_Input_Mouse.PollMouse(_buffer);

            // 事件流：按类型变长跳步（EvMousePos=5B，其余=2B）
            int count = _buffer[0];
            if (count > 0)
            {
                int off = CountSize;
                for (int i = 0; i < count; i++)
                {
                    int type = _buffer[off];
                    switch (type)
                    {
                        case EvMousePos:
                            _x = BinaryPrimitives.ReadInt16LittleEndian(_buffer.AsSpan(off + 1, 2));
                            _y = BinaryPrimitives.ReadInt16LittleEndian(_buffer.AsSpan(off + 3, 2));
                            off += EvMousePos_ByteCount;
                            break;
                        case EvMouseButton:
                            {
                                int raw = _buffer[off + 1];
                                int btn = raw & 0x7F;          // 低7位 = button（DOM 序号：0左/1中/2右）
                                if (btn >= 0 && btn < MaxButtons) _btnNew[btn] = (raw & 0x80) != 0;
                                off += EvMouseButton_ByteCount;
                            }
                            break;
                        case EvWheel:
                            _wheelDelta += (sbyte)_buffer[off + 1];   // 按 sbyte 解读累计增量
                            off += EvWheel_ByteCount;
                            break;
                        default:
                            throw new NotSupportedException();
                    }
                }
            }

            // 边沿 = 本帧电平 与 上帧电平 的差分（与键盘 KeyDown/KeyUp 完全一致）
            var pos = Position;
            for (int b = 0; b < MaxButtons; b++)
            {
                if (_btnNew[b] != _btnLast[b])
                {
                    var btn = ToButton(b);
                    if (_btnNew[b])
                    {
                        ButtonDown?.Invoke(btn, pos);
                    }
                    else
                    {
                        ButtonUp?.Invoke(btn, pos);
                    }
                }

                if(_btnNew[b])
                {
                   //持续按住鼠标，暂无TOOD
                }
            }

            if (_wheelDelta != 0) ScrollWheel?.Invoke(_wheelDelta);
            _scrollValue += _wheelDelta;
        }

        /// <summary>清空鼠标状态（失焦 / 解绑时调用）。</summary>
        public static void Reset()
        {
            Array.Clear(_btnNew);
            Array.Clear(_btnLast);
            _wheelDelta = 0;
            _scrollValue = 0;
            _x = _y = _lastX = _lastY = 0;
        }

        /// <summary>固定步长下，一个渲染帧可能跑多个 Update 步；在每个步结束后把本帧电平存为“上帧”，
        /// 并刷新位置基准，确保一次点击只被识别一次（与 <see cref="Input_KeyBoard.LateUpdate"/> 一致）。</summary>
        public static void LateUpdate()
        {
            if (!Active) return;
            _btnNew.AsSpan().CopyTo(_btnLast);
            _lastX = _x;
            _lastY = _y;
        }

        /// <summary>关闭装置：解绑 JS 侧鼠标监听，并清空状态。</summary>
        public static void Deactivate()
        {
            JSBind_Input_Mouse.UnbindMouse();
            Reset();
            Active = false;
        }

        /// <summary>激活装置：建立/恢复 JS 侧鼠标监听（绑定到画布），并清空状态。与 <see cref="Input_KeyBoard.Activate"/> 一致。</summary>
        public static void Activate()
        {
            Reset();
            JSBind_Input_Mouse.BindMouse();
            Active = true;
        }

        // ===== 查询 =====

        public static Vector2 Position => new Vector2(_x, _y);

        /// <summary>本帧位移</summary>
        public static Vector2 Delta => new Vector2(_x - _lastX, _y - _lastY);

        /// <summary>本帧是否移动过</summary>
        public static bool Moved => _x != _lastX || _y != _lastY;

        public static int X => _x;
        public static int Y => _y;

        /// <summary>滚轮本帧增量</summary>
        public static int ScrollDelta => _wheelDelta;

        /// <summary>滚轮累计值</summary>
        public static int ScrollValue => _scrollValue;

        /// <summary>横向滚轮 —— 浏览器不支持，恒为 0</summary>
        public static int HorizontalScrollDelta => 0;

        public static bool GetButton(MouseButton button) => _btnNew[ToIndex(button)];

        public static bool GetButtonDown(MouseButton button)
        {
            int b = ToIndex(button);
            return _btnNew[b] && !_btnLast[b];
        }

        public static bool GetButtonUp(MouseButton button)
        {
            int b = ToIndex(button);
            return !_btnNew[b] && _btnLast[b];
        }

        public static KPressState GetButtonState(MouseButton button)
        {
            if (GetButtonDown(button)) return KPressState.Down;
            if (GetButton(button)) return KPressState.Held;
            if (GetButtonUp(button)) return KPressState.Up;
            return KPressState.None;
        }

        /// <summary>本帧按键位图（供快照用）</summary>
        public static int Buttons => ToBitmap(_btnNew);

        /// <summary>上一帧按键位图（供快照用）</summary>
        public static int PreviousButtons => ToBitmap(_btnLast);

        /// <summary>是否在指定区域内</summary>
        public static bool IsInside(int width, int height)
            => _x >= 0 && _x < width && _y >= 0 && _y < height;

        /// <summary>设置鼠标位置 —— 浏览器不允许脚本移动光标，空实现</summary>
        public static void SetPosition(int x, int y)
        {
        }

        private static int ToBitmap(bool[] src)
        {
            int v = 0;
            for (int b = 0; b < MaxButtons; b++) if (src[b]) v |= 1 << b;
            return v;
        }

        /// <summary>MouseButton 枚举 → DOM 按键序号（左=0，中=1，右=2，侧键=3/4）。
        /// 项目自定义枚举顺序为 Left/Right/Middle（Right=1，Middle=2），与浏览器
        /// MouseEvent.button（Right=2，Middle=1）相反，因此所有“枚举→内部下标”都必须走这里，
        /// 绝不能再用 (int)button，否则右键/中键会反。</summary>
        private static int ToIndex(MouseButton button)
        {
            return button switch
            {
                MouseButton.Left => 0,
                MouseButton.Middle => 1,
                MouseButton.Right => 2,
                MouseButton.XButton1 => 3,
                MouseButton.XButton2 => 4,
                _ => 0,
            };
        }

        /// <summary>DOM 按键序号 → MouseButton 枚举（0=左，1=中，2=右，3/4=侧键）。与 <see cref="ToIndex"/> 互逆。</summary>
        private static MouseButton ToButton(int index)
        {
            return index switch
            {
                0 => MouseButton.Left,
                1 => MouseButton.Middle,
                2 => MouseButton.Right,
                3 => MouseButton.XButton1,
                4 => MouseButton.XButton2,
                _ => MouseButton.Left,
            };
        }
    }
}
