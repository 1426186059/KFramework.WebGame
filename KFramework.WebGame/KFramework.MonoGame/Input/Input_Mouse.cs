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
        // 事件类型与字节布局统一在 Input_GameFrameData.EvType（镜像 TS 的 html_event_type），
        // 本模块不再自己解析字节流，故这里只保留"键位数"这一个与状态数组有关的常量。
        private const int MaxButtons = 8;
        // 事件数据不再由本模块自己 poll，而是每帧由 Input_GameFrameData 统一取回后按事件类型逐条分发，
        // 故这里不再需要本地缓冲与字节布局常量。

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
        /// 交互被<b>中断</b>：窗口失焦、页面切后台，或浏览器接管了指针（右键菜单 / 拖拽 / 长按手势
        /// —— 这些情况浏览器不会再派发 mouseup）。
        /// <para>
        /// 与 <see cref="ButtonUp"/> 的区别：这里根本没有"用户松手"这个动作。
        /// 上层收到它应当<b>取消</b>进行中的交互（拖动回滚、不触发点击）；
        /// 而 <see cref="ButtonUp"/> 才表示正常松手、可以确认这次交互。
        /// 因此下面会把该键电平退回抬起（避免状态卡住），但<b>不产生</b> ButtonUp 边沿。
        /// </para>
        /// <para>
        /// 触发源只有一处：<see cref="Input_GameFrameData"/> 分发 SysFocusLost / SysPageHidden 时的
        /// <see cref="ReleaseAll"/>。JS 侧那条 PointerCancel 事件已取消 ——
        /// 它原先一次只报一个键号，而"被中断的往往是整次交互"，由本侧电平统一遍历反而更全。
        /// </para>
        /// </summary>
        public static event Action<MouseButton, Vector2> PointerCancel;

        /// <summary>滚轮滚动（本帧增量）</summary>
        public static event Action<int> ScrollWheel;

        /// <summary>每帧清理（由 <see cref="Input_GameFrameData"/> 在取数据前调用）：
        /// 滚轮增量归零 —— 即便本帧一条事件都没有也要清，否则上帧的增量会残留。</summary>
        internal static void BeginFrame()
        {
            if (!Active) return;
            _wheelDelta = 0;
        }

        /// <summary>收到一次按键电平变化：btn 为 DOM 序号（0左 / 1中 / 2右）。</summary>
        internal static void OnButton(int btn, bool down)
        {
            if (!Active) return;
            if (btn < 0 || btn >= MaxButtons) return;
            _btnNew[btn] = down;
        }

        /// <summary>收到一次滚轮增量（按 sbyte 解读，同帧多条会累加）。</summary>
        internal static void OnWheel(int delta)
        {
            if (!Active) return;
            _wheelDelta += delta;
        }

        /// <summary>收到本帧的最终指针位置（backing 像素坐标）。</summary>
        internal static void OnMove(int x, int y)
        {
            if (!Active) return;
            _x = x;
            _y = y;
        }

        // 不再有 OnPointerCancel(btn)：单个键的"指针取消"事件已从流里去掉 ——
        // 手势被接管（pointercancel / 右键菜单）与键盘失焦并成同一条 SysFocusLost，
        // 由 ReleaseAll 统一处理（它遍历的是本侧电平，不需要 JS 逐个报键号）。

        /// <summary>
        /// 边沿计算：本帧电平与上帧电平的差分产生按下 / 抬起，并结算滚轮。
        /// 由 <see cref="Input_GameFrameData.Update"/> 在分发完本帧事件后调用一次。
        /// </summary>
        internal static void EndFrame()
        {
            if (!Active) return;

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

        /// <summary>
        /// 失焦 / 页面隐藏时调用：把当前按住的键逐个上报 <see cref="PointerCancel"/>，再退回抬起电平。
        /// <para>
        /// 与 <see cref="Reset"/> 的区别：Reset 只是静默清空，这里会先发事件，
        /// 让上层知道"这次交互是被中断的"（拖动要取消、点击不能算完成）。
        /// 由 <see cref="Input_GameFrameData"/> 在分发失焦事件时调用 ——
        /// JS 侧既不监听 window 的 blur、也不再单发"指针取消"，这个语义全靠它保住。
        /// </para>
        /// <para>
        /// 遍历的是<b>本侧电平</b>（<c>_btnNew</c> / <c>_btnLast</c>）而不是某个键号清单：
        /// 手势被接管时浏览器只给 pointercancel、不给键号清单，
        /// 原先靠 JS 逐个上报键号反而可能漏掉同时按住的其它键。
        /// </para>
        /// </summary>
        internal static void ReleaseAll()
        {
            if (!Active) return;

            for (int b = 0; b < MaxButtons; b++)
            {
                if (!_btnNew[b] && !_btnLast[b]) continue;

                // 两帧都置 false 使差分无边沿 —— 不被误判成一次正常松手，
                // 同时 PointerCancel 事件把"被中断"这件事单独告知上层。
                _btnNew[b] = false;
                _btnLast[b] = false;
                PointerCancel?.Invoke((MouseButton)b, Position);
            }
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
