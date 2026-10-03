export const E_HTML_Event_Type =
{
    None : 0,             //无效的事件

    // 窗口/Canvas 各种事件
    SysFocusLost: 1,      // 窗口失焦（data 0 字节）—— 只复位键盘 / 触摸，<b>不动鼠标</b>
    SysFocusGained: 2,    // Canvas 获得焦点
    SysPageHidden: 3,     // 页面切到后台 —— 鼠标事件也不会再来了，这时才释放按键
    SysPageVisible: 4,    // Canvas/Window 可见性
    SysPointerCancel: 5,  //取消 鼠标/触发 事件
    CanvasResized: 6,
    devicePixelRatio: 7,
    
    //键盘
    KeyDown: 10,
    KeyUp: 11,

    //鼠标
    MouseButton: 20,       // [button|down<<7] 1B —— 低 7 位键号、最高位是否按下
    MouseWheel: 21,        // [delta] 1B —— 按 sbyte 解读
    MouseMove: 22,         // [x i16][y i16] 4B

    //触摸
    TouchBegin: 30,
    TouchMove: 31,
    TouchEnd: 32,
    TouchCancel: 33,
};

export function evDataBytes(type: number): number 
{
    if (type >= E_HTML_Event_Type.TouchBegin && type <= E_HTML_Event_Type.TouchCancel) return 5;   // id + x i16 + y i16
    if (type === E_HTML_Event_Type.MouseMove) return 4;                                  // x i16 + y i16
    if (type === E_HTML_Event_Type.CanvasResized) return 10;                             // 5 × i16
    if (type <= E_HTML_Event_Type.SysPointerCancel) return 0;        // 失焦 / 可见性 / 指针接管只靠 type
    return 1;                                                                 // 键盘 / 鼠标按键 / 滚轮 / 取消
}

export const MAX_FRAME_BYTES = 2048;
export const MAX_EVENTS_PER_FRAME = 255;

export interface FrameDataStream {
    /**
     * 写入一条事件。
     * @param type 见 html_event_type 的 <c>E_HTML_Event_Type</c>
     * @param data 该事件的 data（长度由类型定长决定，见 <c>evDataBytes</c>）
     * @param len data 中实际有效的字节数（可小于 data.length）
     */
    put(type: number, data: Uint8Array, len: number): void;
}
