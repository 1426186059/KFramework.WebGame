// =====================================================================
// 本地存储（localStorage，含异常兜底）。
// =====================================================================

export const storage = {
    get(key, fallback) {
        try {
            const v = localStorage.getItem(key)
            return v === null ? fallback : v
        } catch { return fallback }
    },
    set(key, value) {
        try { localStorage.setItem(key, value) } catch { /* 忽略 */ }
    },
}
