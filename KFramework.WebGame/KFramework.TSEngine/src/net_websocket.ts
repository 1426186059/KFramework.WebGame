// 【依赖 C#】由 KFramework.MonoGame.JSBind_Net_WebSocket 经 [JSImport(module: "net_websocket")] 调用；
// 产物 net_websocket.js 由 SyncJsEngine 复制到各示例 wwwroot/jsengine。
//
// 薄薄的一层浏览器 WebSocket 桥接：只负责创建 / 发送 / 关闭连接，并把事件推回 C#。
// 真正的收发逻辑都在 C# 侧（WebSocketClient）完成，这里不做任何业务逻辑。

export interface NetHandlers {
    onOpen: (handle: number) => void;
    onBinaryMessage: (handle: number, data: Uint8Array) => void;
    onClose: (handle: number, code: number) => void;
    onError: (handle: number, message: string) => void;
}

let handlers: NetHandlers | null = null;
const sockets = new Map<number, WebSocket>();
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
        if (typeof ev.data === 'string') {
            handlers?.onBinaryMessage(id, new TextEncoder().encode(ev.data));
        } else {
            handlers?.onBinaryMessage(id, new Uint8Array(ev.data as ArrayBuffer));
        }
    };
    ws.onclose = (ev: CloseEvent) => handlers?.onClose(id, ev.code);
    ws.onerror = () => handlers?.onError(id, 'websocket error');
    sockets.set(id, ws);
    return id;
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
}

/** 返回 WebSocket.readyState：0 CONNECTING / 1 OPEN / 2 CLOSING / 3 CLOSED。 */
export function netState(handle: number): number {
    const ws = sockets.get(handle);
    return ws ? ws.readyState : 3;
}
