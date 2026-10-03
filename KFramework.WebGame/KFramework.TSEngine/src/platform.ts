// 【依赖 C#】由 KFramework.MonoGame.JSBind_Platform 经 [JSImport(module: "platform")] 调用；产物 platform.js 由 SyncJsEngine 复制。
// 平台层：页面标题 / 地址栏参数 / 环境查询 / 外链打开。
//
// 画布元素与尺寸已搬到 ./html_canvas.ts（对应 C# 侧 HTML_Canvas）；
// 帧循环已搬到 ./game_update.ts（对应 C# 侧 JSBind_GameUpdate）；
// 输入已独立成 input_keyboard / input_mouse / input_touch / input_window_event。

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
