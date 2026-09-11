using System;
using System.Runtime.InteropServices.JavaScript;

namespace MirEngine;

/// <summary>
/// 浏览器端持久化（localStorage）互操作，对应 tsengine/src/core/storage.ts（mir.storage* 函数）。
/// 用于在 WASM 下等价替代桌面端写磁盘的行为（如聊天日志持久化）。
/// </summary>
public static partial class BrowserStorage
{
    [JSImport("mir.storageAppend", "main.js")]
    private static partial void StorageAppendImpl(string key, string text);

    [JSImport("mir.storageGet", "main.js")]
    private static partial string StorageGetImpl(string key);

    [JSImport("mir.storageRemove", "main.js")]
    private static partial void StorageRemoveImpl(string key);

    /// <summary>把文本追加到指定 key（不存在则创建），用于持续累积日志。</summary>
    public static void AppendText(string key, string text) => StorageAppendImpl(key, text);

    /// <summary>读取指定 key 的文本，不存在或异常时返回空字符串。</summary>
    public static string GetText(string key) => StorageGetImpl(key) ?? string.Empty;

    /// <summary>删除指定 key。</summary>
    public static void Remove(string key) => StorageRemoveImpl(key);
}
