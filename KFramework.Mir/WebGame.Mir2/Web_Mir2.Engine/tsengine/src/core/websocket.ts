// mirengine/core/websocket.ts
// 手写 WebSocket 网络层（浏览器）。对应 JSBind/BrowserWebSocket.cs（mir.ws*）。
// 设计镜像 KNet.WebSocket 的 WebGL 轮询模型：
//   - JS 端维护每个实例的消息队列（二进制帧）与事件队列；
//   - C# 每帧调用 mir.wsReceive 取出一条完整消息，mir.wsGetState 轮询连接状态。
// 不主动回调 C#，避免 JS 回调线程与游戏主线程的同步问题。

/** 一个 WS 连接实例的运行时状态。 */
interface WsInstance {
    ws: WebSocket;
    /** 未取走的二进制帧队列（每条 = 一个 Mir Packet）。 */
    messages: Uint8Array[];
    open: boolean;
}

const instances: Record<number, WsInstance> = {};
let nextId = 1;

/**
 * 建立连接（mir.wsConnect）。HTTPS 页面下 ws:// 会自动升级为 wss://（浏览器禁止混合内容）。
 * @param url 服务端地址（ws:// 或 wss://）
 * @returns 实例 id；<=0 表示失败
 */
export const wsConnect = (url: string): number => {
    let id = -1;
    try {
        // HTTPS 页面下浏览器禁止混合内容（ws://），自动升级为 wss://
        if (typeof location !== 'undefined' && location.protocol === 'https:' && url.startsWith('ws://')) {
            url = 'wss://' + url.slice('ws://'.length);
        }
        const ws = new WebSocket(url);
        ws.binaryType = 'arraybuffer';
        id = nextId++;
        const inst: WsInstance = { ws, messages: [], open: false };
        ws.onopen = () => { inst.open = true; };
        ws.onmessage = (e: MessageEvent) => {
            // 一条 WS 二进制消息 = 一条 Mir Packet 的字节
            const data = (e.data instanceof ArrayBuffer) ? new Uint8Array(e.data) : new Uint8Array(0);
            inst.messages.push(data);
        };
        ws.onclose = () => { inst.open = false; };
        ws.onerror = () => { inst.open = false; };
        instances[id] = inst;
    } catch (e) {
        console.error('[ws] connect failed', e);
        id = -1;
    }
    return id;
};

/**
 * 关闭连接并清理实例（mir.wsClose）。
 * @param id 实例 id
 */
export const wsClose = (id: number): void => {
    const inst = instances[id];
    if (inst && inst.ws) {
        try { inst.ws.close(1000, 'Normal Closure'); } catch (e) { /* ignore */ }
    }
    delete instances[id];
};

/**
 * 发送一条二进制消息（mir.wsSend）。
 * @param id 实例 id
 * @param data 待发送的字节（C# byte[] 跨边界后为 Uint8Array）
 * @returns 1 成功，0 失败（未连接 / 异常）
 */
export const wsSend = (id: number, data: Uint8Array): number => {
    const inst = instances[id];
    if (inst && inst.ws && inst.ws.readyState === WebSocket.OPEN) {
        try {
            inst.ws.send(data);
            return 1;
        } catch (e) {
            return 0;
        }
    }
    return 0;
};

/**
 * 轮询连接状态（mir.wsGetState）。
 * @param id 实例 id
 * @returns WebSocket.readyState：0 CONNECTING / 1 OPEN / 2 CLOSING / 3 CLOSED
 */
export const wsGetState = (id: number): number => {
    const inst = instances[id];
    if (!inst || !inst.ws) return 3; // CLOSED
    return inst.ws.readyState;       // 0 CONNECTING, 1 OPEN, 2 CLOSING, 3 CLOSED
};

/**
 * 取出队列头的一条消息（mir.wsReceive）。
 * @param id 实例 id
 * @returns 一条完整消息的字节；无消息时返回长度 0 的数组（不返回 null）
 */
export const wsReceive = (id: number): Uint8Array => {
    const inst = instances[id];
    if (!inst || inst.messages.length === 0) return new Uint8Array(0);
    const next = inst.messages.shift();
    return next ?? new Uint8Array(0);
};
