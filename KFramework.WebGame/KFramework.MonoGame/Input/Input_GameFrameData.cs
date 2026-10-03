using System.Buffers.Binary;

namespace KFramework.MonoGame
{
    internal static class Input_GameFrameData
    {
        public enum EvType
        {
            None = 0,             //无效的事件

            // 窗口/Canvas 各种事件
            SysFocusLost = 1,      // 窗口失焦（data 0 字节）—— 只复位键盘 / 触摸，<b>不动鼠标</b>
            SysFocusGained = 2,    // Canvas 获得焦点
            SysPageHidden = 3,     // 页面切到后台 —— 鼠标事件也不会再来了，这时才释放按键
            SysPageVisible = 4,    // Canvas 可见性
            SysPointerCancel = 5,
            CanvasResized = 6,
            devicePixelRatio = 7,
    
            //键盘
            KeyDown = 10,
            KeyUp = 11,

            //鼠标
            MouseButton = 20,       // [button|down<<7] 1B —— 低 7 位键号、最高位是否按下
            MouseWheel = 21,        // [delta] 1B —— 按 sbyte 解读
            MouseMove = 22,         // [x i16][y i16] 4B

            //触摸
            TouchBegin = 30,
            TouchMove = 31,
            TouchEnd = 32,
            TouchCancel = 33,
        };

        public static event Action<bool> WindowFocusChanged;
        public static event Action<int, int, int, int, int> WindowSizeChanged;
        private const int MaxBytes = 2048;
        private static readonly byte[] _buffer = new byte[MaxBytes];   // 每帧复用，零分配
        private static bool _bound;

        /// <summary>
        /// 取某类型的 data 字节数（<b>不含</b> type 那 1 字节）。
        /// <para>
        /// <b>必须与 TS 的 <c>evDataBytes</c> 逐条一致</b>（html_event_type.ts）——
        /// TS 的判据是 <c>type &lt;= SysPointerCancel(5) → 0</c>，
        /// 所以 SysFocusLost / SysFocusGained / SysPageHidden / SysPageVisible / SysPointerCancel
        /// 这五条<b>都是 0 字节</b>。早先只把 SysPointerCancel 列进 0，其余落到 default 返回 1，
        /// 于是 C# 每读到一条失焦事件就多吞一个字节 —— 从那一条起整条流全部错位。
        /// </para>
        /// </summary>
        private static int DataBytes(EvType type)
        {
            switch(type)
            {
                case EvType.TouchBegin:
                case EvType.TouchMove:
                case EvType.TouchEnd:
                case EvType.TouchCancel:
                    return 5;
                case EvType.MouseMove:
                    return 4;
                case EvType.CanvasResized:
                    return 10;

                // 只靠 type、不带 data 的五条（与 TS 的 type <= SysPointerCancel 对应）
                case EvType.SysFocusLost:
                case EvType.SysFocusGained:
                case EvType.SysPageHidden:
                case EvType.SysPageVisible:
                case EvType.SysPointerCancel:
                    return 0;

                default: 
                    return 1;
            }
        }
        
        internal static void Update()
        {
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
        
        private static void Dispatch(ReadOnlySpan<byte> s)
        {
            int count = s[0];
            int off = 1;

            for (int i = 0; i < count; i++)
            {
                if (off >= s.Length) break;

                EvType type = (EvType)s[off++];
                int len = DataBytes(type);
                if (off + len > s.Length) break;      // 越界即停，绝不解析半截事件

                ReadOnlySpan<byte> data = s.Slice(off, len);
                off += len;

                PrintTool.Log("Input_GameFrameData: " + type.ToString());
                switch (type)
                {
                    case EvType.CanvasResized:
                        {
                            int cssWidth = BinaryPrimitives.ReadInt16LittleEndian(data);
                            int cssHeight = BinaryPrimitives.ReadInt16LittleEndian(data.Slice(2, 2));
                            int canvasWidth = BinaryPrimitives.ReadInt16LittleEndian(data.Slice(4, 2));
                            int canvasHeight = BinaryPrimitives.ReadInt16LittleEndian(data.Slice(6, 2));
                            int dpi = BinaryPrimitives.ReadInt16LittleEndian(data.Slice(8, 2));

                            WindowSizeChanged?.Invoke(
                                cssWidth,
                                cssHeight,
                                canvasWidth,
                                canvasHeight,
                                dpi);

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
                        WindowFocusChanged?.Invoke(false);
                        break;

                    case EvType.SysPageHidden:
                        // 页面切后台：鼠标事件也不会再来了，这时才需要释放按住的键
                        Input_Mouse.ReleaseAll();
                        ResetAll();
                        WindowFocusChanged?.Invoke(false);
                        break;

                    case EvType.SysPointerCancel:
                        // 指针被系统接管：mouseup 不会再来，释放按住的键（上层据此取消进行中的交互）
                        Input_Mouse.ReleaseAll();
                        break;

                    case EvType.SysFocusGained:
                    case EvType.SysPageVisible:
                        WindowFocusChanged?.Invoke(true);
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
