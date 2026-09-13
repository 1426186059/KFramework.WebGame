// mirengine/core/storage.ts
// 本地存储（localStorage）。对应 JSBind/BrowserStorage.cs（mir.storageAppend / storageGet / storageRemove）。
/**
 * 把文本追加到指定 key（不存在则创建），常用于持续累积日志。
 * @param key localStorage 键
 * @param text 追加内容
 */
export const storageAppend = (key, text) => {
    try {
        localStorage.setItem(key, (localStorage.getItem(key) || '') + text);
    }
    catch (e) { }
};
/**
 * 读取指定 key 的文本。
 * @param key localStorage 键
 * @returns 已存内容；不存在或受限时返回空串
 */
export const storageGet = (key) => {
    try {
        return localStorage.getItem(key) || '';
    }
    catch (e) {
        return '';
    }
};
/**
 * 删除指定 key。
 * @param key localStorage 键
 */
export const storageRemove = (key) => {
    try {
        localStorage.removeItem(key);
    }
    catch (e) { }
};
