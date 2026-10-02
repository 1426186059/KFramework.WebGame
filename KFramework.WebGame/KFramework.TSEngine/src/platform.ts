// 【依赖 C#】由 KFramework.MonoGame.JSBind_Platform 经 [JSImport(module: "platform")] 调用；产物 platform.js 由 SyncJsEngine 复制。
// 平台层：画布尺寸自适应、requestAnimationFrame 主循环、浏览器环境查询。
//
// 输入已独立成 ./input.ts（薄绑定层），由 C# 侧 JSBind_Input 单独对接模块名 "input"。

// 画布的权威来源是 html_canvas（它持有 id → 元素的注册表，且会回退到 document.getElementById）。
// 早先这里从 render_webgl20 取画布 —— 那意味着「画布尺寸自适应」依赖 WebGL 上下文已初始化：
// 纯 WebGPU 的应用从不建 WebGL 上下文，getCanvasSize 会走兜底返回 1x1，
// 画布后备缓冲永远停在 1 像素，画面全黑。现在与具体渲染后端解耦。
import { getCanvas } from './html_canvas.js';

// ---------- 画布尺寸 ----------

// 复用的整数缓冲，避免每帧 getCanvasSize 都 new Int32Array（减少 GC 抖动）。
let _int32Scratch = new Int32Array(8);

function writeInts(view: MemoryView_Span | Int32Array, values: number[]): void {
    if (_int32Scratch.length < values.length) _int32Scratch = new Int32Array(values.length);
    _int32Scratch.set(values);
    const slice = _int32Scratch.subarray(0, values.length);
    if (view instanceof Int32Array) {
        view.set(slice);
        return;
    }
    if (typeof view.set === 'function') {
        view.set(slice, 0);
        return;
    }
    const fallback = view as unknown as Record<number, number>;
    for (let i = 0; i < values.length; i++) fallback[i] = values[i];
}

export function getCanvasSize(view: MemoryView_Span | Int32Array): void {
    const canvas = getCanvas();
    if (!canvas) {
        writeInts(view, [1, 1, 1, 1, 1000]);
        return;
    }

    const dpr = Math.min(window.devicePixelRatio || 1, 2);
    const rect = canvas.getBoundingClientRect();
    const cssWidth = Math.max(1, Math.round(rect.width || canvas.clientWidth || 1));
    const cssHeight = Math.max(1, Math.round(rect.height || canvas.clientHeight || 1));
    const drawWidth = Math.max(1, Math.round(cssWidth * dpr));
    const drawHeight = Math.max(1, Math.round(cssHeight * dpr));

    if (canvas.width !== drawWidth || canvas.height !== drawHeight) {
        canvas.width = drawWidth;
        canvas.height = drawHeight;
    }

    writeInts(view, [cssWidth, cssHeight, drawWidth, drawHeight, Math.round(dpr * 1000)]);
}

// ---------- 浏览器环境 ----------

export function setTitle(title: string): void {
    document.title = title;
}

export function getQueryParameter(name: string): string {
    return new URLSearchParams(window.location.search).get(name) ?? '';
}

export function isMobile(): boolean {
    return /Android|iPhone|iPad|iPod|Mobile/i.test(navigator.userAgent);
}

export function getBaseUri(): string {
    return document.baseURI || window.location.href;
}

/**
 * 在浏览器中打开一个 URL（新标签）。用于跳转支付页、官网等外部链接。
 * 注意：浏览器通常只在「用户手势」（如点击）内允许 window.open，否则会被弹窗拦截。
 */
export function openUrl(url: string): void {
    window.open(url, "_blank");
}
