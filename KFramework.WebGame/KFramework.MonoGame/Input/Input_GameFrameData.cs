using System;
using System.Buffers.Binary;

namespace KFramework.MonoGame
{

    /// <summary>
    /// 每帧事件流的<b>解析器</b>：C# 每帧【一次】跨界取回整条流，按 <see cref="EvType"/> 逐条分发给各输入模块。
    /// <para>
    /// 字节格式只在<b>本文件</b>解释一次 —— 各 Input_* 只收语义化参数（按下 / 抬起 / 坐标 / 相位），
    /// 不再各自解析字节流。这是"统一事件类型"的另一半：TS 侧编码一处、C# 侧解码一处。
    /// 对照 TS 的 <c>KFramework.TSEngine/src/html_event_type.ts</c>。
    /// </para>
    /// <para>
    /// 一帧的时序：<b>清理 → 取数据 → 逐条分发 → 边沿计算</b>。
    /// 清理必要先行（滚轮归零、触摸列表清空），否则上帧的残留会混进本帧。
    /// </para>
    /// </summary>
    public static class Input_GameFrameData
    {
        /// <summary>事件类型（镜像 TS 的 <c>EvType</c>）：高 4 位即模块，data 长度由类型定长。</summary>
        public static class EvType
        {
            // ---- 系统 0x0_：失焦 / 可见性只靠 type，无 data ----
            public const int SysFocusLost = 0x00;
            public const int SysFocusGained = 0x01;
            public const int SysPageHidden = 0x02;
            public const int SysPageVisible = 0x03;

            /// <summary>
            /// 指针被系统 / 浏览器接管（<c>pointercancel</c>）：mouseup 不会再派发，必须释放按住的键。
            /// 与 <see cref="SysFocusLost"/> 分开的理由见 TS 的 <c>reportPointerCancel</c>。
            /// </summary>
            public const int SysPointerCancel = 0x05;

            /// <summary>
            /// 画布尺寸变化：data 10 字节 = 5 个 i16（CSS 宽 / CSS 高 / 绘制缓冲宽 / 绘制缓冲高 / DPR×1000），
            /// 与 <c>platform.getCanvasSize</c> 的 out 布局同序（那边是 i32，这里压成 i16：画布尺寸上限远小于 short.MaxValue）。
            /// <para>
            /// 它取代了原先"每帧跨界查一次画布尺寸"的做法：尺寸只在真变化时才上报，
            /// 且数据随事件一起过来，C# 侧不必再为它跨界。
            /// </para>
            /// </summary>
            public const int CanvasResized = 0x04;

            // ---- 键盘 0x1_：data 1 字节 = Keys 序号（0..212）----
            // 不再有 KeyBlur：键盘失焦与"指针被系统接管"都并进 SysFocusLost，
            // 由 JS 侧判定后单独上报一条（见 input_window_event.ts 的 focusLostType）。
            public const int KeyDown = 0x10;
            public const int KeyUp = 0x11;

            // ---- 鼠标 0x2_ ----
            // 不再有 PointerCancel：改由收到 SysFocusLost 时调 Input_Mouse.ReleaseAll() 逐个键发起。
            public const int MouseButton = 0x20;     // 1B：低 7 位键号 | 最高位是否按下
            public const int MouseWheel = 0x21;      // 1B：按 sbyte 解读的增量
            public const int MouseMove = 0x22;       // 4B：x i16 + y i16

            // ---- 触摸 0x3_：data 5 字节 = id(1) + x i16 + y i16 ----
            public const int TouchBegin = 0x30;
            public const int TouchMove = 0x31;
            public const int TouchEnd = 0x32;
            public const int TouchCancel = 0x33;
        }

        /// <summary>焦点变化（true = 获得焦点）。触发时各输入模块已先行清空 / 复位。</summary>
        public static event Action<bool> FocusChanged;

        /// <summary>
        /// 画布尺寸变化（参数：CSS 宽、CSS 高、绘制缓冲宽、绘制缓冲高、DPR×1000）。
        /// <para>
        /// 由 <see cref="Game"/> 订阅后交给 <see cref="GraphicsDevice"/> 应用 ——
        /// 本类不直接持有设备，避免输入层与图形层互相依赖。
        /// </para>
        /// </summary>
        public static event Action<int, int, int, int, int> CanvasResized;

        // 与 TS 侧 MAX_FRAME_BYTES 对齐：触摸每触点 6B × 64 ≈ 384B，2048 留足余量。
        private const int MaxBytes = 2048;

        private static readonly byte[] _buffer = new byte[MaxBytes];   // 每帧复用，零分配
        private static bool _bound;

        /// <summary>取某类型的 data 字节数（<b>不含</b> type 那 1 字节）；与 TS 的 <c>evDataBytes</c> 一致。</summary>
        private static int DataBytes(int type)
            => type >= EvType.TouchBegin && type <= EvType.TouchCancel ? 5
             : type == EvType.MouseMove ? 4
             : type == EvType.CanvasResized ? 10
             : type <= EvType.SysPointerCancel ? 0      // 失焦 / 可见性 / 指针接管只靠 type
             : 1;

        /// <summary>
        /// 每帧调用一次（由 <see cref="Input.Update"/> 驱动），早于任何边沿计算。
        /// <para>
        /// 固定步长下一个渲染帧可能跑多个 Update 步，只有首个步能取到数据（后续步取回空流）——
        /// 这与原先"每步各 poll 一次"的行为一致。
        /// </para>
        /// </summary>
        internal static void Update()
        {
            // 懒绑定：必须早于第一次取数据，否则首帧的系统事件会漏掉
            if (!_bound)
            {
                JSBind_GameFrameData.BindFrameEvents();
                _bound = true;
            }

            // 每帧清理先行：本帧一条事件都没有也要走一次（滚轮归零、触摸列表清空）
            Input_Mouse.BeginFrame();
            Input_Touch.BeginFrame();

            int n = JSBind_GameFrameData.TakeFrameData(_buffer);
            if (n > 0) Dispatch(_buffer.AsSpan(0, n));

            // 边沿计算：差分出按下 / 抬起 / 滚动，各模块内部完成
            Input_KeyBoard.EndFrame();
            Input_Mouse.EndFrame();
            Input_Touch.EndFrame();
        }

        /// <summary>
        /// 逐条分发。布局：<c>[count(1)][ [type(1)][data] × count ]</c>。
        /// <para>
        /// TS 侧把系统事件写在流的<b>最前面</b>，所以流式处理即满足"失焦先行" ——
        /// 失焦必须早于本帧残留的按键 / 指针事件，否则"窗外松手"那一帧仍会把电平写成按下。
        /// </para>
        /// </summary>
        private static void Dispatch(ReadOnlySpan<byte> s)
        {
            int count = s[0];
            int off = 1;

            for (int i = 0; i < count; i++)
            {
                if (off >= s.Length) break;

                int type = s[off++];
                int len = DataBytes(type);
                if (off + len > s.Length) break;      // 越界即停，绝不解析半截事件

                ReadOnlySpan<byte> data = s.Slice(off, len);
                off += len;

                switch (type)
                {
                    // ---- 画布尺寸：数据随事件一起来，不必再为它跨界查一次 ----
                    case EvType.CanvasResized:
                        if (data.Length >= 10)
                        {
                            CanvasResized?.Invoke(
                                BinaryPrimitives.ReadInt16LittleEndian(data),
                                BinaryPrimitives.ReadInt16LittleEndian(data.Slice(2, 2)),
                                BinaryPrimitives.ReadInt16LittleEndian(data.Slice(4, 2)),
                                BinaryPrimitives.ReadInt16LittleEndian(data.Slice(6, 2)),
                                BinaryPrimitives.ReadInt16LittleEndian(data.Slice(8, 2)));
                        }
                        break;

                    // ---- 系统：只靠 type，没有 data ----
                    case EvType.SysFocusLost:
                        // 失焦：本帧要作废各输入模块攒下的数据。
                        // 【不动鼠标】实测右键按住拖动时浏览器会为手势把焦点拿走，
                        // document.hasFocus() 真的变 false —— 据此释放会让正在进行的右键拖拽被取消。
                        // 而 mouseup 是可靠送达的，鼠标交给 mousedown / mouseup 自己即可；
                        // 键盘 / 触摸则必须清：它们的 keyup / touchend 确实收不到了。

                        PrintTool.Log("EvType.SysFocusLost Canvas 失去焦点");
                        Input_KeyBoard.Reset();
                        Input_Touch.Reset();
                        Input_Mouse.Reset();
                        FocusChanged?.Invoke(false);
                        break;

                    case EvType.SysPageHidden:
                        // 页面切后台：鼠标事件也不会再来了，这时才需要释放按住的键
                        Input_Mouse.ReleaseAll();
                        ResetAll();
                        FocusChanged?.Invoke(false);
                        break;

                    case EvType.SysPointerCancel:
                        // 指针被系统接管：mouseup 不会再来，释放按住的键（上层据此取消进行中的交互）
                        Input_Mouse.ReleaseAll();
                        break;

                    case EvType.SysFocusGained:
                    case EvType.SysPageVisible:
                        FocusChanged?.Invoke(true);
                        break;

                    // ---- 键盘 ----
                    case EvType.KeyDown: Input_KeyBoard.OnKey(data[0], true); break;
                    case EvType.KeyUp: Input_KeyBoard.OnKey(data[0], false); break;

                    // ---- 鼠标 ----
                    case EvType.MouseButton:
                        {
                            int raw = data[0];
                            Input_Mouse.OnButton(raw & 0x7F, (raw & 0x80) != 0);   // 低 7 位键号、最高位按下
                        }
                        break;

                    case EvType.MouseWheel:
                        Input_Mouse.OnWheel((sbyte)data[0]);                       // 按 sbyte 解读增量
                        break;

                    case EvType.MouseMove:
                        Input_Mouse.OnMove(
                            BinaryPrimitives.ReadInt16LittleEndian(data),
                            BinaryPrimitives.ReadInt16LittleEndian(data.Slice(2, 2)));
                        break;

                    // ---- 触摸：data = id + x i16 + y i16 ----
                    case EvType.TouchBegin:
                    case EvType.TouchMove:
                    case EvType.TouchEnd:
                    case EvType.TouchCancel:
                        Input_Touch.OnTouch(type, data[0],
                            BinaryPrimitives.ReadInt16LittleEndian(data.Slice(1, 2)),
                            BinaryPrimitives.ReadInt16LittleEndian(data.Slice(3, 2)));
                        break;
                }
            }
        }

        /// <summary>清空全部输入装置的状态（失焦 / 页面隐藏时调用）。</summary>
        private static void ResetAll()
        {
            Input_KeyBoard.Reset();
            Input_Mouse.Reset();
            Input_Touch.Reset();
        }
    }
}
