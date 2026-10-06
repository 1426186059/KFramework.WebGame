// 【测试模块 Bench_JSObject 的 JS 侧】专测 JSObject 的跨边界代价：创建 / 传参 / 反向接收。
// 对应 C# 侧 JSBind_JSObject.cs。JSObject 是 WebGL 纹理句柄（Texture2D.Handle）的当前载体，
// 本模块把它和「整数句柄」摆在一起比 —— 后者对应 WebGPU 的 createTexture 返回的 int。
//
// 接线：main.js 里 setModuleImports('bench_jsobject', jsObject) 并 setCs(本模块的导出)。

// 自增整数，模拟 WebGPU 那种 int 句柄；JS 侧建 Map 把 int 映射到对象，与 int 描述层完全对称。
let _objId = 0;
const _objByInt = new Map();

const makeObj = () => {
    const o = { id: ++_objId };
    _objByInt.set(_objId, o);
    return o;                       // 返回真实对象，C# 侧收成 JSObject
};

// ================= C# → JS =================

// 创建：返回 JSObject（对应 WebGL createTexture 返回 WebGLTexture）
export function joCreateObj() { return makeObj(); }

// 创建：返回 int（对应 WebGPU createTexture 返回 int 句柄）
export function joCreateInt() { return ++_objId; }

// 传参：吃一个 JSObject（对应 bindTexture(target, texture)）
export function joTakeObj(obj) { /* 仅接收，不做任何事 */ }

// 传参：吃一个 int（对应 bindTexture(target, id)）
export function joTakeInt(id) { /* 仅接收 */ }

// ================= JS → C#（连打 C# 的导出，回报 "最快ms|最慢ms"）=================

const timeIt = (n, fn, rounds = 5) => {
    const warm = Math.min(2000, Math.max(100, n >> 3));
    for (let i = 0; i < warm; i++) fn(i);

    let best = Infinity, worst = 0;
    for (let r = 0; r < rounds; r++) {
        if (cs && typeof cs.Js_GcCollect === 'function') cs.Js_GcCollect();   // 每轮前强制回收，不计入
        const t0 = performance.now();
        for (let i = 0; i < n; i++) fn(i);
        const dt = performance.now() - t0;
        if (dt < best) best = dt;
        if (dt > worst) worst = dt;
    }
    return best + '|' + worst;
};

const timed = (n, fn) => timeIt(n, fn) + '|';

let cs = null;

/** main.js 注入 JSBind_JSObject 的导出。 */
export function setCs(exports) { cs = exports; }

// JS→C# 收 JSObject：每次新建一个对象传进去（模拟每帧传新句柄）
export function joCallTakeObjN(n) {
    if (!cs || typeof cs.Js_TakeObj !== 'function') return '-1|-1|Js_TakeObj 未导出';
    return timed(n, () => cs.Js_TakeObj(makeObj()));
}

// JS→C# 收 int
export function joCallTakeIntN(n) {
    if (!cs || typeof cs.Js_TakeInt !== 'function') return '-1|-1|Js_TakeInt 未导出';
    let id = 0;
    return timed(n, () => cs.Js_TakeInt(++id));
}
