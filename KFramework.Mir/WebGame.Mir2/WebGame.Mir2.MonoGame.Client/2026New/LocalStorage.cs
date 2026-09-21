using KFramework.MonoGame;
using System;
using System.Threading.Tasks;

/// <summary>
/// 浏览器 localStorage 键值存储封装（底层走 KFramework.MonoGame.JSBind_LocalStorage，module: "localstorage"）。
/// 以异步签名暴露，供 InIReader.LoadAsync/SaveAsync 等 await 使用；localStorage 本身是同步 API，
/// 这里直接包成已完成的 Task，不额外切换线程。
/// <para>底层 getItem 对缺失键返回 null，这里归一为空字符串；需要区分「键不存在」与「值为空」时用 <see cref="HasKeyAsync"/>。</para>
/// </summary>
public static class LocalStorage
{
    /// <summary>读取字符串值；缺失返回空字符串。</summary>
    public static Task<string> GetStringAsync(string key)
    {
        try { return Task.FromResult(JSBind_LocalStorage.GetString(key) ?? string.Empty); }
        catch { return Task.FromResult(string.Empty); }
    }

    /// <summary>写入字符串值（账号/密码/设置/ini 文本等）。</summary>
    public static Task SetStringAsync(string key, string value)
    {
        try { JSBind_LocalStorage.SetString(key, value ?? string.Empty); }
        catch { }
        return Task.CompletedTask;
    }

    /// <summary>是否存在该键（区分「键不存在」与「值为空字符串」）。</summary>
    public static Task<bool> HasKeyAsync(string key)
    {
        try { return Task.FromResult(JSBind_LocalStorage.GetString(key) != null); }
        catch { return Task.FromResult(false); }
    }

    /// <summary>删除键。</summary>
    public static Task RemoveAsync(string key)
    {
        try { JSBind_LocalStorage.RemoveKey(key); }
        catch { }
        return Task.CompletedTask;
    }

    /// <summary>清空全部键。</summary>
    public static Task ClearAsync()
    {
        try { JSBind_LocalStorage.Clear(); }
        catch { }
        return Task.CompletedTask;
    }
}
