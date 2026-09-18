// 【依赖 C#】由 KFramework.MonoGame.JSBind_Net_WebSocket 经 [JSImport(module: "net_websocket")] 调用；
// 产物 net_websocket.js 由 SyncJsEngine 复制到各示例 wwwroot/jsengine。
//
// 薄薄的一层浏览器 WebSocket 桥接：只负责创建 / 发送 / 关闭连接，并把事件推回 C#。
// 真正的收发逻辑都在 C# 侧（WebSocketClient）完成，这里不做任何业务逻辑。

export interface NetHandlers {
    onOpen: (handle: number) => void;
    /** 小包：整帧直接交给 C#（由运行时封送成 byte[]）。 */
    onBinaryMessage: (handle: number, data: Uint8Array) => void;
    /** 大包：只回传 (ArrayBuffer 偏移, 长度)，数据由 C# 侧拉走（零拷贝）。 */
    onBigMessage: (handle: number, offset: number, length: number) => void;
    onClose: (handle: number, code: number) => void;
    onError: (handle: number, message: string) => void;
}

// 大包阈值（字节），必须与 C# 侧 Net_RecvBuffer.BigPacketThreshold 一致。
// 超过它的帧走零拷贝通道：整帧不再穿过 JS↔C# 边界，而是先挂在 pending 上，
// 只把 (ArrayBuffer 偏移, 长度) 交给 C#，由 C# 递一块 WASM 堆上的固定缓冲过来，
// JS 用 MemoryView.set 直接写进去 —— 整条链路上只有一次拷贝，托管侧不再分配大数组。
export const BIG_PACKET_LIMIT = 64 * 1024;

let handlers: NetHandlers | null = null;
const sockets = new Map<number, WebSocket>();
/** 正在等待 C# 拉走的大包（只保存当前这一帧，用完即删）。 */
const pending = new Map<number, Uint8Array>();
let nextId = 1;

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
        const data = typeof ev.data === 'string'
            ? new TextEncoder().encode(ev.data)
            : new Uint8Array(ev.data as ArrayBuffer);

        if (data.byteLength > BIG_PACKET_LIMIT) {
            // 大包：整帧留在 JS 侧，只把 (ArrayBuffer 偏移, 长度) 回报给 C#
            pending.set(id, data);
            try {
                handlers?.onBigMessage(id, data.byteOffset, data.byteLength);
            } finally {
                pending.delete(id);
            }
            return;
        }

        handlers?.onBinaryMessage(id, data);
    };
    ws.onclose = (ev: CloseEvent) => handlers?.onClose(id, ev.code);
    ws.onerror = () => handlers?.onError(id, 'websocket error');
    sockets.set(id, ws);
    return id;
}

/**
 * 大包通道：C# 把它的固定缓冲以 MemoryView 递过来，这里按 (offset, length)
 * 从 ArrayBuffer 直接拷进去（不产生中间数组）。target 就是 WASM 堆上的那块内存。
 */
export function netRead(handle: number, offset: number, length: number, target: MemoryView | Uint8Array): void {
    const src = pending.get(handle);
    if (!src) return;

    const chunk = new Uint8Array(src.buffer, offset, length);
    if (target instanceof Uint8Array) {
        target.set(chunk);
        return;
    }
    target.set(chunk, 0);
}

/** 发送二进制帧；连接未处于 OPEN 状态时返回 false。 */
export function netSend(handle: number, data: ArrayBuffer): boolean {
    const ws = sockets.get(handle);
    if (!ws || ws.readyState !== WebSocket.OPEN) return false;
    try {
        ws.send(data);
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
    pending.delete(handle);
}

/** 返回 WebSocket.readyState：0 CONNECTING / 1 OPEN / 2 CLOSING / 3 CLOSED。 */
export function netState(handle: number): number {
    const ws = sockets.get(handle);
    return ws ? ws.readyState : 3;
}
