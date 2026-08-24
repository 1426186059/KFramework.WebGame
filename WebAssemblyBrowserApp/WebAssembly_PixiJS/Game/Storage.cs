using System.Runtime.InteropServices.JavaScript;

namespace PixiGame;

/// <summary>本地存储（localStorage 封装，对应 core/storage.js）。</summary>
public static partial class Storage
{
    [JSImport("storage.get", "main.js")] public static partial string Get(string key, string fallback);
    [JSImport("storage.set", "main.js")] public static partial void Set(string key, string value);
}
