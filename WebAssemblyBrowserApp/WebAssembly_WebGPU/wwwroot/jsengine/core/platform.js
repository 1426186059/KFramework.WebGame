// =====================================================================
// platform：浏览器平台能力薄层（窗口尺寸 / DPR / 性能计时 / UA / 语言 / 标题）。
// 供 C# 侧 Platform.cs 通过 [JSImport] 调用。
// =====================================================================
export const platform = {
    innerWidth: () => (typeof window !== 'undefined' && window.innerWidth) || 960,
    innerHeight: () => (typeof window !== 'undefined' && window.innerHeight) || 540,
    devicePixelRatio: () => (typeof window !== 'undefined' && window.devicePixelRatio) || 1,
    now: () => (typeof performance !== 'undefined') ? performance.now() : 0,
    userAgent: () => (typeof navigator !== 'undefined') ? navigator.userAgent : '',
    language: () => (typeof navigator !== 'undefined') ? (navigator.language || '') : '',
    setTitle: (t) => { if (typeof document !== 'undefined') document.title = t },
}
