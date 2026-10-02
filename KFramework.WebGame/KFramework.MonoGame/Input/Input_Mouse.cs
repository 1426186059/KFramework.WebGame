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
        private const int EvPointerCancel = 3; // payload = button（指针被接管/失焦，并非用户松手）

        private const int MaxButtons = 8;
        private const int EvMousePos_ByteCount = 5;
        private const int EvMouseButton_ByteCount = 2;
        private const int EvWheel_ByteCount = 2;
        private const int MaxByteCount = 
            1 + 
            MaxButtons * EvMouseButton_ByteCount + 
            EvMousePos_ByteCount +
            EvWheel_ByteCount;
        // 事件数据不再由本模块自己 poll，而是每帧由 GameFrameData 统一取回后以 payload 形式转交，
        // 故这里不再需要本地缓冲。MaxByteCount 保留作为 payload 长度的文档性上限。

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

        /// <summary>
        /// 指针被<b>系统 / 浏览器接管</b>（右键手势、拖拽、长按菜单等）或窗口失焦，交互被中断。
        /// <para>
        /// 与 <see cref="ButtonUp"/> 的区别：这里根本没有"用户松手"这个动作 ——
        /// 浏览器接管指针后不会再派发 mouseup，只给 <c>pointercancel</c>。
        /// 上层收到它应当<b>取消</b>进行中的交互（拖动回滚、不触发点击）；
        /// 而 <see cref="ButtonUp"/> 才表示正常松手、可以确认这次交互。
        /// 因此下面会把该键电平退回抬起（避免状态卡住），但<b>不产生</b> ButtonUp 边沿。
        /// </para>
        /// </summary>
        public static event Action<MouseButton, Vector2> PointerCancel;

        /// <summary>滚轮滚动（本帧增量）</summary>
        public static event Action<int> ScrollWheel;

        /// <summary>每帧调用一次：取回本模块的事件队列并更新状态。</summary>
        /// <summary>
        /// 消费本帧分发的鼠标事件（由 <see cref="GameFrameData"/> 转交）。
        /// <para>
        /// payload 沿用本模块<b>原有</b>的格式（第 0 字节 = 条数，其后按类型变长跳步），
        /// 因此只是把"自己 poll"换成"收 payload"，解析逻辑一字未改 —— 鼠标模块依旧独立。
        /// </para>
        /// </summary>
        internal static void Consume(ReadOnlySpan<byte> payload)
        {
            if (!Active) return;
            _wheelDelta = 0;      // 每帧清理：即便本帧无事件也要归零，否则上帧增量会残留

            // 事件流：按类型变长跳步（EvMousePos=5B，其余=2B）
            int count = payload.Length > 0 ? payload[0] : 0;
            if (count > 0)
            {
                int off = 1;
                for (int i = 0; i < count; i++)
                {
                    if (off >= payload.Length) break;      // 越界即停，绝不解析半截事件
                    int type = payload[off];
                    switch (type)
                    {
                        case EvMousePos:
                            _x = BinaryPrimitives.ReadInt16LittleEndian(payload.Slice(off + 1, 2));
                            _y = BinaryPrimitives.ReadInt16LittleEndian(payload.Slice(off + 3, 2));
                            off += EvMousePos_ByteCount;
                            break;
                        case EvMouseButton:
                            {
                                int raw = payload[off + 1];
                                int btn = raw & 0x7F;          // 低7位 = button（DOM 序号：0左/1中/2右）
                                if (btn >= 0 && btn < MaxButtons) _btnNew[btn] = (raw & 0x80) != 0;
                                off += EvMouseButton_ByteCount;
                            }
                            break;
                        case EvWheel:
                            _wheelDelta += (sbyte)payload[off + 1];   // 按 sbyte 解读累计增量
                            off += EvWheel_ByteCount;
                            break;
                        case EvPointerCancel:
                            {
                                int btn = payload[off + 1] & 0x7F;
                                if (btn >= 0 && btn < MaxButtons)
                                {
                                    // 电平退回抬起，但【两帧都置 false】使差分无边沿 ——
                                    // 否则会被当成一次正常松手，把中断的交互错误地确认掉。
                                    _btnNew[btn] = false;
                                    _btnLast[btn] = false;
                                    PointerCancel?.Invoke((MouseButton)btn, Position);
                                }
                                off += EvMouseButton_ByteCount;
                            }
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
                    MouseButton btn = (MouseButton)(b);
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
            // 画布 id 以 GraphicsDevice.CanvasId 为准（与键盘绑定一致）；
            // 不能让输入模块去问 WebGL 后端要画布 —— 纯 WebGPU 应用下那会是 null，事件根本绑不上。
            JSBind_Input_Mouse.BindMouse(GraphicsDevice.CanvasId);
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

        public static bool GetButton(MouseButton button) => _btnNew[(int)button];

        public static bool GetButtonDown(MouseButton button)
        {
            int b = (int)(button);
            return _btnNew[b] && !_btnLast[b];
        }

        public static bool GetButtonUp(MouseButton button)
        {
            int b = (int)(button);
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
    }
}
