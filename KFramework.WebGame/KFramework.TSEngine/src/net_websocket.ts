// 【依赖 C#】由 KFramework.MonoGame.JSBind_Net_WebSocket 经 [JSImport(module: "net_websocket")] 调用；
// 产物 net_websocket.js 由 SyncJsEngine 复制到各示例 wwwroot/jsengine。
//
// 薄薄的一层浏览器 WebSocket 桥接：只负责创建 / 发送 / 关闭连接，并把事件推回 C#。
// 真正的收发逻辑都在 C# 侧（Net_WebSocket_Client）完成，这里不做任何业务逻辑。
//
// 收发一律走 byte[] 封送：实时通信以小包为主，JS→C# 一次封送（一次拷贝 + 一个 gen0 数组）
// 足够便宜，不再为偶发大包单独做零拷贝通道，代码更干净。
//
// 分配纪律：
//   1. new Uint8Array(arrayBuffer) 是“视图”，不复制；new Uint8Array(uint8Array) 才是“按内容复制” —— 千万别写反；
//   2. 二进制帧直接建视图交给 C#（C# 的 byte[] 参数只认 Uint8Array，裸 ArrayBuffer 会被运行时断言拒绝）；
//   3. TextEncoder 复用同一个实例，别每帧 new。

export interface NetHandlers {
    onOpen: (handle: number) => void;
    /** 收到一帧：整帧交给 C#（运行时封送成 byte[]，必然有一份拷贝；必须传 Uint8Array）。 */
    onBinaryMessage: (handle: number, data: Uint8Array) => void;
    onClose: (handle: number, code: number) => void;
    onError: (handle: number, message: string) => void;
}

let handlers: NetHandlers | null = null;
const sockets = new Map<number, WebSocket>();
let nextId = 1;
/** 文本帧编码复用同一个 TextEncoder（每帧 new 一个纯属浪费）。 */
const utf8 = new TextEncoder();

/** C# 侧注册事件回调（由 main.ts 在拿到程序集导出后调用一次）。 */
export function setHandlers(h: NetHandlers): void {
    handlers = h;
}

/** 创建连接，返回句柄；连接结果通过 onOpen / onError 异步回报。 */
export function netCreate(url: string): number {
    const id = nextId++;
    let ws: WebSocket;
    try {
        ws = new WebSocket(url);
    } catch (e) {
        handlers?.onError(id, String(e));
        return id;
    }
    ws.binaryType = 'arraybuffer';
    ws.onopen = () => handlers?.onOpen(id);
    ws.onmessage = (ev: MessageEvent) => {
        const raw: unknown = ev.data;

        // 文本帧：编码成 UTF-8 字节（这一步必然产生新数组，省不掉）
        if (typeof raw === 'string') {
            handlers?.onBinaryMessage(id, utf8.encode(raw));
            return;
        }

        // 二进制帧：binaryType='arraybuffer'。C# 的 byte[] 只接受 Array / Uint8Array
        // （运行时会断言 “Value is not an Array or Uint8Array”），裸 ArrayBuffer 传过去会直接抛错。
        // 这里的 new Uint8Array(raw) 是零拷贝视图，只多一个几十字节的视图头。
        if (raw instanceof ArrayBuffer) {
            handlers?.onBinaryMessage(id, new Uint8Array(raw));
            return;
        }

        // 走到这里说明 binaryType 被改过或收到了 Blob，明确报错好过静默丢包
        handlers?.onError(id, `[net] 不支持的消息类型：${Object.prototype.toString.call(raw)}`);
    };
    ws.onclose = (ev: CloseEvent) => handlers?.onClose(id, ev.code);
    ws.onerror = () => handlers?.onError(id, 'websocket error');
    sockets.set(id, ws);
    return id;
}

/** 发送二进制帧；连接未处于 OPEN 状态时返回 false。 */
export function netSend(handle: number, data: Uint8Array | ArrayBuffer): boolean {
    const ws = sockets.get(handle);
    if (!ws || ws.readyState !== WebSocket.OPEN) return false;

    // 千万别写 new Uint8Array(data)：data 是 Uint8Array 时那是“按内容复制”（白白多一份 O(n)）。
    // 直接透传视图，或在 ArrayBuffer 上建视图 —— 两者都不复制。
    // send 是同步把数据拷进浏览器自己的发送队列的，不会持有这块内存，所以传视图是安全的。
    const payload = data instanceof Uint8Array
        ? data
        : data instanceof ArrayBuffer ? new Uint8Array(data) : null;
    if (!payload) {
        handlers?.onError(handle, `netSend: 不支持的数据类型 ${Object.prototype.toString.call(data)}`);
        return false;
    }

    try {
        ws.send(payload);
        return true;
    } catch (e) {
        handlers?.onError(handle, String(e));
        return false;
    }
}

export function netClose(handle: number): void {
    const ws = sockets.get(handle);
    if (ws) {
        try { ws.close(); } catch { /* ignore */ }
        sockets.delete(handle);
    }
}

/** 返回 WebSocket.readyState：0 CONNECTING / 1 OPEN / 2 CLOSING / 3 CLOSED。 */
export function netState(handle: number): number {
    const ws = sockets.get(handle);
    return ws ? ws.readyState : 3;
}
