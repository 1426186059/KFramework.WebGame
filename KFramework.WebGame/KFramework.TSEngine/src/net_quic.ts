// 【依赖 C#】由 KFramework.MonoGame.JSBind_Net_Quic 经 [JSImport(module: "net_quic")] 调用；
// 产物 net_quic.js 由 SyncJsEngine 复制到各示例 wwwroot/jsengine。
//
// 薄薄的一层浏览器 WebTransport(QUIC) 桥接：连接建立后用“可靠双向流”收发（有序、不丢包，语义对齐 WebSocket），
// 并把事件推回 C#。真正的协议、缓冲、分包都在 C# 侧（QuicClient）完成，这里不做任何业务逻辑。
//
// 说明：WebTransport 还提供不可靠 datagram，但游戏消息通常需要可靠有序，故本桥默认走可靠流；
// 若确需 datagram，可在 C# 侧扩展方法后在此补一个 writer。

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
// 语义同 net_websocket.ts：超阈值只回传 (偏移, 长度)，由 C# 递固定缓冲过来再写入。
export const BIG_PACKET_LIMIT = 64 * 1024;

const CONNECTING = 0;
const OPEN = 1;
const CLOSING = 2;
const CLOSED = 3;

let handlers: NetHandlers | null = null;
/** 正在等待 C# 拉走的大包（只保存当前这一帧，用完即删）。 */
const pending = new Map<number, Uint8Array>();
const sessions = new Map<number, {
    wt: WebTransport;
    state: number;
    writer: WritableStreamDefaultWriter<Uint8Array> | null;
}>();
let nextId = 1;

/** C# 侧注册事件回调（由 main.ts 在拿到程序集导出后调用一次）。 */
export function setHandlers(h: NetHandlers): void {
    handlers = h;
}

/** 创建连接，返回句柄；连接结果通过 onOpen / onError 异步回报（WebTransport 需等待 HTTP/3 握手 + 建流）。 */
export function quicCreate(url: string): number {
    const id = nextId++;
    let wt: WebTransport;
    try {
        wt = new WebTransport(url);
    } catch (e) {
        handlers?.onError(id, String(e));
        return id;
    }
    const entry = { wt, state: CONNECTING, writer: null as WritableStreamDefaultWriter<Uint8Array> | null };
    sessions.set(id, entry);

    (async () => {
        try {
            await wt.ready;
        } catch (e) {
            entry.state = CLOSED;
            handlers?.onError(id, String(e));
            return;
        }
        // 建立可靠双向流作为收发主通道；建好后再通知 C# 已可收发（保证 Send 立即可用）。
        try {
            const stream = await wt.createBidirectionalStream();
            entry.writer = stream.writable.getWriter();
            readLoop(id, stream.readable.getReader());
        } catch (e) {
            entry.state = CLOSED;
            handlers?.onError(id, String(e));
            return;
        }
        entry.state = OPEN;
        handlers?.onOpen(id);

        // 同时处理服务器主动发起的入站双向流（服务器也可开新流推送）。
        (async () => {
            try {
                const reader = wt.incomingBidirectionalStreams.getReader();
                while (true) {
                    const { value, done } = await reader.read();
                    if (done) break;
                    if (value) readLoop(id, value.readable.getReader());
                }
            } catch {
                /* 连接关闭时自然结束 */
            }
        })();
    })();

    wt.closed.then(() => {
        entry.state = CLOSED;
        handlers?.onClose(id, 0);
    }).catch((e: unknown) => {
        entry.state = CLOSED;
        handlers?.onError(id, String(e));
    });

    return id;
}

/** 单个流的入站读取循环：连接/流出错或关闭时读取自然结束。 */
function readLoop(id: number, reader: ReadableStreamDefaultReader<Uint8Array>): void {
    (async () => {
        try {
            while (true) {
                const { value, done } = await reader.read();
                if (done) break;
                if (value) deliver(id, new Uint8Array(value));
            }
        } catch {
            /* 流关闭/出错时自然结束 */
        } finally {
            reader.releaseLock();
        }
    })();
}

/** 收包分流：小包整帧交给 C#，大包只回传 (ArrayBuffer 偏移, 长度)。 */
function deliver(id: number, data: Uint8Array): void {
    if (data.byteLength > BIG_PACKET_LIMIT) {
        pending.set(id, data);
        try {
            handlers?.onBigMessage(id, data.byteOffset, data.byteLength);
        } finally {
            pending.delete(id);
        }
        return;
    }
    handlers?.onBinaryMessage(id, data);
}

/**
 * 大包通道：C# 把它的固定缓冲以 MemoryView 递过来，这里按 (offset, length)
 * 从 ArrayBuffer 直接拷进去（不产生中间数组）。target 就是 WASM 堆上的那块内存。
 */
export function quicRead(handle: number, offset: number, length: number, target: MemoryView | Uint8Array): void {
    const src = pending.get(handle);
    if (!src) return;

    const chunk = new Uint8Array(src.buffer, offset, length);
    if (target instanceof Uint8Array) {
        target.set(chunk);
        return;
    }
    target.set(chunk, 0);
}

/** 在可靠流上发送一帧；流未就绪（连接未 OPEN）时返回 false。写入前复制数据，避免底层复用缓冲。 */
export function quicSend(handle: number, data: ArrayBuffer): boolean {
    const entry = sessions.get(handle);
    if (!entry || entry.state !== OPEN || !entry.writer) return false;
    try {
        entry.writer.write(new Uint8Array(data));
        return true;
    } catch (e) {
        handlers?.onError(handle, String(e));
        return false;
    }
}

export function quicClose(handle: number): void {
    const entry = sessions.get(handle);
    if (!entry) return;
    entry.state = CLOSING;
    try { entry.wt.close(); } catch { /* ignore */ }
    sessions.delete(handle);
    pending.delete(handle);
}

/** 返回内部状态：0 CONNECTING / 1 OPEN / 2 CLOSING / 3 CLOSED。 */
export function quicState(handle: number): number {
    const entry = sessions.get(handle);
    return entry ? entry.state : CLOSED;
}
