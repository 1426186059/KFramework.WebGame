//对 localStorage 的原始封装， API 与 原始一模一样
export function setItem(key, value) {
    localStorage.setItem(key, value ?? '');
}
export function getItem(key) {
    return localStorage.getItem(key) ?? '';
}
export function removeItem(key) {
    localStorage.removeItem(key);
}
export function clear() {
    localStorage.clear();
}
