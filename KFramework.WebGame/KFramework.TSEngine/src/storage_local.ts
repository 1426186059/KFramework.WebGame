//对 localStorage 的原始封装， API 与 原始一模一样

export function setItem(key: string, value: string): void
{
    localStorage.setItem(key, value ?? '');
}

export function getItem(key: string): string
{
    return localStorage.getItem(key) ?? '';
}

export function removeItem(key: string): void
{
    localStorage.removeItem(key);
}

export function clear(key: string): void
{
    localStorage.clear();
}
