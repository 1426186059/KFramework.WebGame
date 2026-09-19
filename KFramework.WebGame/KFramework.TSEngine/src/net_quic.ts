// 【依赖 C#】由 KFramework.MonoGame.JSBind_Net_Quic 经 [JSImport(module: "net_quic")] 调用；
// 产物 net_quic.js 由 SyncJsEngine 复制到各示例 wwwroot/jsengine。
//
// 薄薄的一层浏览器 WebTransport(QUIC) 桥接：连接建立后用“可靠流”收发（有序、不丢包），并把事件推回 C#。
// 真正的协议、缓冲、分包都在 C# 侧（Net_Quic_Client）完成，这里不做任何业务逻辑。
//
// 【多流】WebTransport 与 WebSocket 最大的区别就是“一个连接上有多条流”，因此这里每条流都有一个
// 连接内唯一的 streamId：
//   · quicCreate 建好第一条双向流作为“主流”（DefaultStreamId），保证连接后 Send 立即可用；
//   · 服务器可以主动发起入站双向流 / 入站单向流（推送、每流一个业务通道），这里都会接管并回报 onStreamOpen；
//   · C# 可用 quicOpenStream 主动开流（双向 / 单向），用 quicSend(handle, streamId, ...) 往指定流发；
//   · 每条流可单独结束写端（quicCloseStream 发 FIN）或中止（quicAbortStream）；
//   · 入站单向流是只读的，往它发会明确报错（而不是静默丢弃）。
//
// 说明：WebTransport 还提供不可靠 datagram，但游戏消息通常需要可靠有序，故本桥走可靠流；
// 若确需 datagram，可在 C# 侧扩展方法后在此补一个 writer。
//
// 分配纪律同 net_websocket.ts：WebTransport 给的 Uint8Array 原样交给 C#
// （千万别写 new Uint8Array(value)，那是按内容复制，白白多一份 O(n)）。

/** 流类型：双向流（可读可写）。 */
export const STREAM_BIDI = 0;
/** 流类型：单向流 —— 本地创建，只能写（对端读）。 */
export const STREAM_UNI_SEND = 1;
/** 流类型：单向流 —— 对端创建，只能读（本地收）。 */
export const STREAM_UNI_RECV = 2;

export interface QuicNetHandlers {
    onOpen: (handle: number) => void;
    /** 有流可用（本地创建完成 / 对端发起）时回调；kind 见 STREAM_BIDI / STREAM_UNI_SEND / STREAM_UNI_RECV。 */
    onStreamOpen: (handle: number, streamId: number, kind: number) => void;
    /** 收到一帧：整帧交给 C#（运行时封送成 byte[]，必然有一份拷贝；必须传 Uint8Array）。 */
    onBinaryMessage: (handle: number, streamId: number, data: Uint8Array) => void;
    /** 单条流结束：code 0 = 对端正常 FIN，非 0 = 异常结束。 */
    onStreamClose: (handle: number, streamId: number, code: number) => void;
    onClose: (handle: number, code: number) => void;
    onError: (handle: number, message: string) => void;
}

const CONNECTING = 0;
const OPEN = 1;
const CLOSING = 2;
const CLOSED = 3;

/** 连接上的一条流。 */
interface StreamEntry {
    /** STREAM_BIDI / STREAM_UNI_SEND / STREAM_UNI_RECV。 */
    kind: number;
    /** 写端；入站单向流为 null（只读）。 */
    writer: WritableStreamDefaultWriter<Uint8Array> | null;
    /** 写端是否已结束（close/abort 后置 true，读端仍可继续收，即“半关”）。 */
    writeClosed: boolean;
}

interface Session {
    wt: WebTransport;
    state: number;
    streams: Map<number, StreamEntry>;
    /** 主流（连接建立时创建的第一条双向流）；Send 不带 streamId 时用它。 */
    defaultStreamId: number;
    nextStreamId: number;
}

let handlers: QuicNetHandlers | null = null;
const sessions = new Map<number, Session>();
let nextId = 1;

/** C# 侧注册事件回调（由 main.ts 在拿到程序集导出后调用一次）。 */
export function setHandlers(h: QuicNetHandlers): void {
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
    const entry: Session = {
        wt, state: CONNECTING, streams: new Map(), defaultStreamId: 0, nextStreamId: 1,
    };
    sessions.set(id, entry);

    (async () => {
        try {
            await wt.ready;
        } catch (e) {
            entry.state = CLOSED;
            handlers?.onError(id, String(e));
            return;
        }
        // 建立可靠双向流作为收发主流；建好后再通知 C# 已可收发（保证 Send 立即可用）。
        try {
            const stream = await wt.createBidirectionalStream();
            const streamId = entry.nextStreamId++;
            entry.defaultStreamId = streamId;
            entry.streams.set(streamId, {
                kind: STREAM_BIDI, writer: stream.writable.getWriter(), writeClosed: false,
            });
            handlers?.onStreamOpen(id, streamId, STREAM_BIDI);
            readLoop(id, streamId, stream.readable.getReader());
        } catch (e) {
            entry.state = CLOSED;
            handlers?.onError(id, String(e));
            return;
        }
        entry.state = OPEN;
        handlers?.onOpen(id);

        // 对端主动发起的流：每条流独立编号并接管（服务器可按流推送不同业务通道）。
        acceptIncoming(id, entry, wt.incomingBidirectionalStreams, STREAM_BIDI);
        acceptIncoming(id, entry, wt.incomingUnidirectionalStreams, STREAM_UNI_RECV);
    })();

    wt.closed.then(() => {
        entry.state = CLOSED;
        entry.streams.clear();
        handlers?.onClose(id, 0);
    }).catch((e: unknown) => {
        entry.state = CLOSED;
        entry.streams.clear();
        handlers?.onError(id, String(e));
    });

    return id;
}

/** 接管对端发起的入站流：双向流可收可发，单向流只能收。 */
function acceptIncoming(handle: number, entry: Session,
    incoming: ReadableStream<WebTransportBidirectionalStream | ReadableStream<Uint8Array>>,
    kind: number): void {
    (async () => {
        const reader = incoming.getReader();
        try {
            while (entry.state === CONNECTING || entry.state === OPEN) {
                const { value, done } = await reader.read();
                if (done) break;
                if (!value) continue;

                const streamId = entry.nextStreamId++;
                let writer: WritableStreamDefaultWriter<Uint8Array> | null = null;
                let dataReader: ReadableStreamDefaultReader<Uint8Array>;
                if (kind === STREAM_BIDI) {
                    const bi = value as WebTransportBidirectionalStream;
                    writer = bi.writable.getWriter();
                    dataReader = bi.readable.getReader();
                } else {
                    // 入站单向流本身就是一条 ReceiveStream（ReadableStream），没有 writable —— 只读
                    dataReader = (value as ReadableStream<Uint8Array>).getReader();
                }
                entry.streams.set(streamId, { kind, writer, writeClosed: false });
                handlers?.onStreamOpen(handle, streamId, kind);
                readLoop(handle, streamId, dataReader);
            }
        } catch {
            /* 连接关闭时自然结束 */
        } finally {
            try { reader.releaseLock(); } catch { /* ignore */ }
        }
    })();
}

/** 单条流的入站读取循环：流出错或关闭时读取自然结束，并回报 onStreamClose。 */
function readLoop(handle: number, streamId: number,
    reader: ReadableStreamDefaultReader<Uint8Array>): void {
    (async () => {
        try {
            while (true) {
                const { value, done } = await reader.read();
                if (done) break;
                // value 已经是 Uint8Array，原样透传；写 new Uint8Array(value) 会变成按内容复制（多一份 O(n)）
                if (value) deliver(handle, streamId, value);
            }
            sessions.get(handle)?.streams.delete(streamId);
            handlers?.onStreamClose(handle, streamId, 0); // 对端 FIN
        } catch {
            sessions.get(handle)?.streams.delete(streamId);
            handlers?.onStreamClose(handle, streamId, 1); // 异常结束
        } finally {
            try { reader.releaseLock(); } catch { /* ignore */ }
        }
    })();
}

/** 收包：整帧交给 C#（与 WebSocket 一侧一致，不再为大包单独分通道）。 */
function deliver(handle: number, streamId: number, data: Uint8Array): void {
    handlers?.onBinaryMessage(handle, streamId, data);
}

/**
 * 开一条新流（异步建立，建好后回报 onStreamOpen）。
 * @param unidirectional true = 单向流（只能写，对端收）；false = 双向流（可收可发）。
 * @returns 流号（&gt; 0）；连接未就绪时返回 0 并上报 onError。
 */
export function quicOpenStream(handle: number, unidirectional: boolean): number {
    const entry = sessions.get(handle);
    if (!entry || entry.state !== OPEN) {
        handlers?.onError(handle, 'quicOpenStream: 连接未就绪');
        return 0;
    }

    const streamId = entry.nextStreamId++;
    (async () => {
        try {
            if (unidirectional) {
                const send = await entry.wt.createUnidirectionalStream();
                entry.streams.set(streamId, {
                    kind: STREAM_UNI_SEND, writer: send.getWriter(), writeClosed: false,
                });
            } else {
                const bi = await entry.wt.createBidirectionalStream();
                entry.streams.set(streamId, {
                    kind: STREAM_BIDI, writer: bi.writable.getWriter(), writeClosed: false,
                });
                readLoop(handle, streamId, bi.readable.getReader());
            }
            handlers?.onStreamOpen(handle, streamId, unidirectional ? STREAM_UNI_SEND : STREAM_BIDI);
        } catch (e) {
            handlers?.onError(handle, `quicOpenStream: ${String(e)}`);
        }
    })();

    return streamId;
}

/** 在指定流上发送一帧；streamId &lt;= 0 表示主流。流未就绪（连接未 OPEN / 只读单向流）时返回 false。 */
export function quicSend(handle: number, streamId: number, data: Uint8Array | ArrayBuffer): boolean {
    const entry = sessions.get(handle);
    if (!entry || entry.state !== OPEN) return false;

    const id = streamId > 0 ? streamId : entry.defaultStreamId;
    const stream = entry.streams.get(id);
    if (!stream || !stream.writer || stream.writeClosed) {
        handlers?.onError(handle,
            `quicSend: 流 ${id} 不可写（未就绪 / 已关闭 / 是只读的入站单向流）`);
        return false;
    }

    const view = data instanceof Uint8Array
        ? data
        : data instanceof ArrayBuffer ? new Uint8Array(data) : null;
    if (!view) {
        handlers?.onError(handle, `quicSend: 不支持的数据类型 ${Object.prototype.toString.call(data)}`);
        return false;
    }

    try {
        // 这里必须复制一份：writer.write() 是异步的，底层可能稍后才真正读这块内存，
        // 而 C# 传来的视图指向 WASM 堆（随后可能被改写，memory.grow 后还会失效）。
        // 对比 net_websocket.ts：WebSocket.send 是同步拷贝的，那边不需要复制。
        stream.writer.write(view.slice());
        return true;
    } catch (e) {
        handlers?.onError(handle, String(e));
        return false;
    }
}

/** 结束流的写端（发 FIN，仍可继续收）；返回是否已提交。 */
export function quicCloseStream(handle: number, streamId: number): boolean {
    const stream = sessions.get(handle)?.streams.get(streamId);
    if (!stream || !stream.writer || stream.writeClosed) return false;
    stream.writeClosed = true;
    try {
        stream.writer.close();
        return true;
    } catch (e) {
        handlers?.onError(handle, String(e));
        return false;
    }
}

/** 中止流（立即重置，双方都不再收发）。 */
export function quicAbortStream(handle: number, streamId: number, code: number): boolean {
    const stream = sessions.get(handle)?.streams.get(streamId);
    if (!stream || !stream.writer || stream.writeClosed) return false;
    stream.writeClosed = true;
    try {
        stream.writer.abort(code);
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
    entry.streams.clear();
    sessions.delete(handle);
}

/** 返回内部状态：0 CONNECTING / 1 OPEN / 2 CLOSING / 3 CLOSED。 */
export function quicState(handle: number): number {
    const entry = sessions.get(handle);
    return entry ? entry.state : CLOSED;
}

/** 返回该流的类型：STREAM_BIDI / STREAM_UNI_SEND / STREAM_UNI_RECV；流不存在或已结束返回 -1。 */
export function quicStreamKind(handle: number, streamId: number): number {
    return sessions.get(handle)?.streams.get(streamId)?.kind ?? -1;
}
