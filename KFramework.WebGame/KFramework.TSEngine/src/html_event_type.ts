// 【共享类型】HTML 输入事件的统一类型表：系统 / 键盘 / 鼠标 / 触摸共用这一套编号与字节布局。
// 本文件只定义"事件长什么样" —— 不注册监听、不持有状态；TS 侧按它编码，C# 侧按它解码（两边各存一份镜像常量）。
//
// 事件流布局（扁平，每条自带类型）：<c>[count(1)][ [type(1)][data(...)] × count ]</c>
// type 的<b>高 4 位即模块</b>，因此不再需要原先每条 3 字节的 module+len 包装头；
// data 长度由 type 定长决定，所以流里也不必再带长度。
//
// 字节数：离散事件（按键 / 滚轮 / 焦点）的 data 只有 1 字节；带坐标的是 4~5 字节 ——
// 屏幕坐标是整数、画布上限 16384，i16（±32767）装得下，这已是物理下限，压不动了。

/** 事件类型编号：1 字节。高 4 位 = 模块：0x0_ 系统 / 0x1_ 键盘 / 0x2_ 鼠标 / 0x3_ 触摸。 */
export const E_HTML_Event_Type = {
    // ---- 系统 0x0_ ----
    SysFocusLost: 0x00,      // 窗口失焦（data 0 字节）
    SysFocusGained: 0x01,
    SysPageHidden: 0x02,     // 页面切到后台
    SysPageVisible: 0x03,
    // 画布尺寸变化：data 10 字节 = 5 个 i16（CSS 宽 / CSS 高 / 绘制缓冲宽 / 绘制缓冲高 / DPR×1000）
    // 与 platform.getCanvasSize 的 out 布局同序（那边是 i32，这里压成 i16 —— 画布尺寸上限远小于 32767，
    // DPR×1000 也不过 2000，用 i32 是白占一倍带宽）。
    // C# 侧拿到即可直接用，不必再为它跨界取一次 —— 这正是把它从"每帧轮询"改成"事件"的目的。
    CanvasResized: 0x04,

    // ---- 键盘 0x1_：data 1 字节 = Keys 序号（0..212，见 input_keyboard 的 CODE_TO_KEYS）----
    KeyDown: 0x10,
    KeyUp: 0x11,
    KeyBlur: 0x12,           // 失焦：C# 侧清空按键状态（窗外松手收不到 keyup，不报就会卡住）

    // ---- 鼠标 0x2_ ----
    MouseButton: 0x20,       // [button|down<<7] 1B —— 低 7 位键号、最高位是否按下
    MouseWheel: 0x21,        // [delta] 1B —— 按 sbyte 解读
    MouseMove: 0x22,         // [x i16][y i16] 4B
    PointerCancel: 0x23,     // [button] 1B —— 手势被系统接管；不是抬起，冒充抬起会让上层误判为完成

    // ---- 触摸 0x3_：data 5 字节 = id(1) + x i16 + y i16 ----
    TouchBegin: 0x30,
    TouchMove: 0x31,
    TouchEnd: 0x32,
    TouchCancel: 0x33,
} as const;

/**
 * 取某类型的 data 字节数（<b>不含</b> type 那 1 字节）。
 * 定长，所以解析端按 type 查表推进即可，流里不必带长度、也不会解析错位。
 */
export function evDataBytes(type: number): number {
    if (type >= E_HTML_Event_Type.TouchBegin && type <= E_HTML_Event_Type.TouchCancel) return 5;   // id + x i16 + y i16
    if (type === E_HTML_Event_Type.MouseMove) return 4;                                  // x i16 + y i16
    if (type === E_HTML_Event_Type.CanvasResized) return 10;                             // 5 × i16
    if (type <= E_HTML_Event_Type.SysPageVisible) return 0;                              // 失焦 / 可见性只靠 type
    return 1;                                                                 // 键盘 / 鼠标按键 / 滚轮 / 取消
}

/** 本帧事件流的最大字节数：触摸每触点 6B（type+data）、最多 64 条 ≈ 384B，2048 留足余量。 */
export const MAX_FRAME_BYTES = 2048;

/** 单帧事件条数上限（count 只占 1 字节）。 */
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
