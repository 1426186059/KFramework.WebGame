// 【测试模块 Bench_JsReturn 的 JS 侧】C# ← JS 的“返回值能力”探测。
// 每个函数只做一件事：返回某一种类型的值，分同步（直接 return）与异步（async + return）两套。
// 接线：main.js 里 setModuleImports('bench_jsreturn', ...)。

// ---------- 标量：同步 / 异步各一套 ----------
export function syncInt() { return 42; }
export async function asyncInt() { return 42; }

export function syncDouble() { return 3.14159; }
export async function asyncDouble() { return 3.14159; }

export function syncBool() { return true; }
export async function asyncBool() { return true; }

export function syncString() { return "hello 世界"; }
export async function asyncString() { return "hello 世界"; }

// ---------- byte[]：JS 侧 new Uint8Array 交回，C# 收成 byte[]（只能同步返回）----------
export function syncBytes() { return new Uint8Array([1, 2, 3, 4, 5]); }

// ---------- JSObject：返回单个 JS 对象（对象里可夹多个字段，用来探“多值”问题）----------
export function syncObject() { return { first: 11, second: 22, name: "x" }; }
export async function asyncObject() { return { first: 11, second: 22, name: "x" }; }

// ---------- “多值”的两条出路 ----------
// ① 返回对象，C# 收成 JSObject 后逐项读 —— 一次调用拿到 first + second 两个值
export async function asyncPair() { return { first: 11, second: 22 }; }
// ② 拼成字符串过界（本工程惯例，见 BenchKit.SplitTiming）："first|second|..."，C# 端自行拆分
export async function asyncPacked() { return "11|22|hello"; }
