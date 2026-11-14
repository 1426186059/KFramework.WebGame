// 【测试模块 Bench_Json 的 JS 侧】加载 2config 文件并做 JSON.parse / JSON.stringify 计时，
// 另提供 Json_GetText 供 C# 一次性取出文件文本（C# 不自己联网）。
// 接线：main.js 里 setModuleImports('bench_json', json)。
//
// 【本模块不调 C#】解析与序列化都没有跨界，计时只能在调用方（JS）侧做，
// 所以这里自己连打并回报 "最快ms|最慢ms|错误"，C# 侧用 BenchKit.FromJs 收。

// ---- 一次性把 7MB 配置拉到本地（顶层 await，runMain 之前必定完成）----
let text = null;
let parsed = null;   // JSON.parse 的产物，序列化计时复用（与 C# 预解析同思路）

try {
    const res = await fetch('./2config10.25341.1.json');
    text = await res.text();
} catch (e) {
    text = null;
}

// C# 调它一次性取文本（不在计时循环内）
export function Json_GetText() { return text; }

// ---------------------------------------------------------------- 计时
// 与 C# 侧 BenchKit.MeasureFixed 对齐：warmup 不计入 / 取最快轮 / rounds 轮。
// 用户要求只测一次：warmup=1、rounds=1（单次解析几十 ms，已远大于计时器分辨率，足够稳定）。
function timeJson(n, fn, warmup, rounds) {
    for (let i = 0; i < warmup; i++) fn(i);

    let best = Infinity, worst = 0;
    for (let r = 0; r < rounds; r++) {
        const t0 = performance.now();
        for (let i = 0; i < n; i++) fn(i);
        const dt = performance.now() - t0;
        if (dt < best) best = dt;
        if (dt > worst) worst = dt;
    }
    return best + '|' + worst + '|';
}

// ---------------------------------------------------------------- 解析
export function CallParseN(n) {
    if (!text) return '-1|-1|文本未加载（fetch 失败或文件缺失）';
    // 解析到的对象不保留（纯解析计时，与 C# JsonDocument.Parse 口径一致）
    return timeJson(n, () => { const o = JSON.parse(text); return o; }, 1, 1);
}

// ---------------------------------------------------------------- 序列化
export function CallStringifyN(n) {
    if (!text) return '-1|-1|文本未加载';
    if (!parsed) parsed = JSON.parse(text);   // 解析一次，序列化计时复用
    return timeJson(n, () => { const s = JSON.stringify(parsed); return s; }, 1, 1);
}
