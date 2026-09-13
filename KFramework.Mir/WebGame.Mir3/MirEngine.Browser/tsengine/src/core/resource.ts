// mirengine/core/resource.ts
// 资源 / 主机服务。对应 JSBind/BrowserResource.cs（mir.getBytes + mir.log）。
import { host, fetchBytes } from '../shared.js';

// 同步按需拉取 URL 字节（首次访问某资源时才发起请求并缓存）。
// 注意：返回空 Uint8Array 而非 null —— .NET WASM 的 [JSImport] 把 JS 的 null 封送为 byte[]
// 时可能触发 RuntimeError: unreachable，固定返回空数组可彻底规避该问题。
/**
 * C# 侧读取资源字节的入口（BrowserResource.GetBytes）。
 * @param url 资源地址
 * @returns 资源字节；取不到时返回长度 0 的数组（不返回 null）
 */
export const getBytes = (url: string): Uint8Array => {
    try {
        console.log('[Mir][res] (sync) ' + url);
        const data = fetchBytes(url);
        return data && data.length ? data : new Uint8Array(0);
    } catch (e) {
        console.error('[Mir] getBytes error: ' + url, e);
        return new Uint8Array(0);
    }
};

// mir.log：C# 侧 BrowserResource.Log 通过 [JSImport] 调用，必须是 Function。
// 输出到 console，便于排查资源加载（URL / 404 / 库解析失败）。
/**
 * 托管侧日志转发的入口。
 * @param msg 日志内容（空值不输出）
 */
export const log = (msg: string): void => { if (msg) console.log(msg); };
