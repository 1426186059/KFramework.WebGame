// 【依赖 C#】由 KFramework.MonoGame.JSBind_Net_WebSocket 经 [JSImport(module: "net_websocket")] 调用；
// 产物 net_websocket.js 由 SyncJsEngine 复制到各示例 wwwroot/jsengine。
//
// 薄薄的一层浏览器 WebSocket 桥接：只负责创建 / 发送 / 关闭连接，并把事件推回 C#。
// 真正的收发逻辑都在 C# 侧（Net_WebSocket_Client）完成，这里不做任何业务逻辑。
//
// 【两个方向的封送方式不同，别照搬】
//   C# → JS（netSend）：参数是 Span<byte> → MemoryView，只封送一个视图；本侧经 sharedBytesOf
//     里的 _unsafe_create_view() 直接拿到【共享托管内存】的视图 —— 零拷贝，没有 memcpy。
//   JS → C#（onBinaryMessage）：只能用 byte[]，运行时按帧新建一个托管数组（一次分配 + 一次拷贝）。
//   JS 这边【给不出】MemoryView：C# 侧 [JSExport] 若声明 Span + MemoryView 会被运行时断言拒绝
//   （实测见 WebAssemblyBrowserApp.Test 的 Bench_JsToCs.JsToCs_FillSpan）。
//
// 【连接做成类】一条连接的全部状态（ws、句柄、发送兜底缓冲）都收在 NetSocket 里：
//   * 事件回调是本类的方法 —— 有名字，不必再包匿名函数，也不必从事件目标反查句柄；
//   * 句子级的表只剩一张 "句柄 → 连接"（C# 侧只拿得到句柄，这张表省不掉）。
//   模块级保留 4 个转发入口（netCreate / netSend / netClose / netState）——
//   它们对应 C# 侧 [JSImport] 的名字，形状不能改。
//
// 分配纪律：
//   1. new Uint8Array(arrayBuffer) 是“视图”，不复制；new Uint8Array(uint8Array) 才是“按内容复制” —— 千万别写反；
//   2. JS→C# 的二进制帧直接建视图交给 C#（C# 的 byte[] 参数只认 Uint8Array，裸 ArrayBuffer 会被运行时断言拒绝）；
//   3. TextEncoder 全模块共用一个实例，发送兜底缓冲每条连接各持一块，都别每次 new。
import { sharedBytesOf } from './custom_data_byte_cache.js';
let handlers = null;
let nextId = 1;
/** 文本帧编码复用同一个 TextEncoder（每帧 new 一个纯属浪费）。 */
const utf8 = new TextEncoder();
/**
 * 一条浏览器 WebSocket 连接。
 *
 * 句柄就是本实例的 id；四个事件回调是本类的实例方法（用类字段箭头函数写，
 * 天然绑定 this，赋给 ws.onXxx 时既不需要包一层匿名函数，也不需要 bind）。
 */
class NetSocket {
    /** 连接句柄（C# 侧用它寻址）。 */
    handle;
    ws;
    /** 发送兜底缓冲：只在拿不到共享视图时用（按需扩容，避免每次发送都分配一块 Uint8Array）。 */
    scratch = new Uint8Array(0);
    constructor(ws) {
        this.handle = nextId++;
        this.ws = ws;
        this.ws.binaryType = 'arraybuffer';
        this.ws.onopen = this.onOpen;
        this.ws.onmessage = this.onMessage;
        this.ws.onclose = this.onClose;
        this.ws.onerror = this.onError;
    }
    /** WebSocket.readyState：0 CONNECTING / 1 OPEN / 2 CLOSING / 3 CLOSED。 */
    get readyState() {
        return this.ws.readyState;
    }
    /**
     * 发送一帧二进制数据；连接未处于 OPEN 状态时返回 false。
     *
     * data 是 C# 侧 Span<byte> 封送过来的 MemoryView —— 【不是 TypedArray，也没有 .buffer / .byteOffset，
     * 更没有 [] 索引器】。所以 `new Uint8Array(data.buffer, data.byteOffset, data.byteLength)` 是错的
     * （前两个取到 undefined，直接抛 TypeError），`ws.send(data)` 也不行（send 不认 MemoryView）。
     * 要拿它的字节只有两条路：copyTo(target)，或 _unsafe_create_view() 给的共享内存视图。
     */
    send(data) {
        if (this.ws.readyState !== WebSocket.OPEN)
            return false;
        // 零拷贝优先：sharedBytesOf 经 _unsafe_create_view() 取出【共享托管内存】的视图，一次 memcpy 都没有。
        // ws.send 会【同步】把数据拷进浏览器自己的发送队列（返回前就已固化），所以直接喂共享视图是安全的
        // —— 与 gl.bufferData / new Blob 同类，不会踩"堆一增长、视图静默失效"那个坑。
        // 拿不到共享视图（内部方法缺失等）才退回本连接的复用缓冲：多一次 memcpy，但功能不丢。
        const payload = sharedBytesOf(data) ?? this.copyToScratch(data);
        try {
            this.ws.send(payload);
            return true;
        }
        catch (e) {
            handlers?.onError(this.handle, String(e));
            return false;
        }
    }
    /** 主动关闭（随后浏览器仍会触发一次 onclose，重复回报由 C# 侧按句柄兜住）。 */
    close() {
        try {
            this.ws.close();
        }
        catch { /* ignore */ }
    }
    /** 连接建立。 */
    onOpen = () => {
        handlers?.onOpen(this.handle);
    };
    /** 收到一帧（文本帧先编码成 UTF-8 字节，与二进制帧走同一条上报路径）。 */
    onMessage = (ev) => {
        const raw = ev.data;
        // 文本帧：编码成 UTF-8 字节（这一步必然产生新数组，省不掉）
        if (typeof raw === 'string') {
            handlers?.onBinaryMessage(this.handle, utf8.encode(raw));
            return;
        }
        // 二进制帧：binaryType='arraybuffer'。C# 的 byte[] 只接受 Array / Uint8Array
        // （运行时会断言 “Value is not an Array or Uint8Array”），裸 ArrayBuffer 传过去会直接抛错。
        // 这里的 new Uint8Array(raw) 是零拷贝视图，只多一个几十字节的视图头。
        if (raw instanceof ArrayBuffer) {
            handlers?.onBinaryMessage(this.handle, new Uint8Array(raw));
            return;
        }
        // 走到这里说明 binaryType 被改过或收到了 Blob，明确报错好过静默丢包
        handlers?.onError(this.handle, `[net] 不支持的消息类型：${Object.prototype.toString.call(raw)}`);
    };
    /** 连接关闭。 */
    onClose = (ev) => {
        handlers?.onClose(this.handle, ev.code);
    };
    /** 连接出错（浏览器的 error 事件不带原因，给固定文案）。 */
    onError = () => {
        handlers?.onError(this.handle, 'websocket error');
    };
    /** 兜底路径：把 MemoryView copyTo 进本连接的复用缓冲（1 次 memcpy，无分配）。 */
    copyToScratch(data) {
        const n = data.length;
        if (this.scratch.length < n) {
            let cap = this.scratch.length || 256;
            while (cap < n)
                cap *= 2;
            this.scratch = new Uint8Array(cap);
        }
        const payload = this.scratch.subarray(0, n); // subarray 是视图，不复制
        data.copyTo(payload);
        return payload;
    }
}
/** 句柄 → 连接。C# 侧只拿得到句柄，所以这张表是必须的。 */
const sockets = new Map();
/** C# 侧注册事件回调（由 main.ts 在拿到程序集导出后调用一次）。 */
export function setHandlers(h) {
    handlers = h;
}
// ---------------- 模块入口：形状由 C# 侧 [JSImport] 的名字决定，勿改 ----------------
/** 创建连接，返回句柄；连接结果通过 onOpen / onError 异步回报。 */
export function netCreate(url) {
    let ws;
    try {
        ws = new WebSocket(url);
    }
    catch (e) {
        // 构造就失败（非法 URL 等）：没有实例可登记，但错误同样要按句柄回报
        const id = nextId++;
        handlers?.onError(id, String(e));
        return id;
    }
    const socket = new NetSocket(ws);
    sockets.set(socket.handle, socket);
    return socket.handle;
}
/** 发送二进制帧；连接不存在或未处于 OPEN 状态时返回 false。 */
export function netSend(handle, data) {
    return sockets.get(handle)?.send(data) ?? false;
}
/** 关闭并注销连接。 */
export function netClose(handle) {
    const socket = sockets.get(handle);
    if (!socket)
        return;
    sockets.delete(handle);
    socket.close();
}
/** 返回 WebSocket.readyState：0 CONNECTING / 1 OPEN / 2 CLOSING / 3 CLOSED。 */
export function netState(handle) {
    return sockets.get(handle)?.readyState ?? 3;
}
