using System.Runtime.InteropServices.JavaScript;

namespace WebAssemblyBrowserApp.Engine;

/// <summary>浏览器 localStorage 封装（JS 薄层），用于存档 / 最高分等。</summary>
public static partial class Storage
{
    [JSImport("storage.get", "main.js")]
    public static partial string Get(string key, string fallback);

    [JSImport("storage.set", "main.js")]
    public static partial void Set(string key, string value);
}
