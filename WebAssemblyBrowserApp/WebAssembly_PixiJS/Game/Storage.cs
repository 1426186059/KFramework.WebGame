using System.Runtime.InteropServices.JavaScript;

namespace PixiGame;

/// <summary>
/// 本地存储。JSImport 到 Pixi 桥 pixiApi.storage*：
/// Pixi 不提供持久化 API，底层为浏览器 localStorage，统一经 Pixi 桥暴露。
/// </summary>
public static partial class Storage
{
    [JSImport("pixiApi.storageGet", "main.js")] public static partial string Get(string key, string fallback);
    [JSImport("pixiApi.storageSet", "main.js")] public static partial void Set(string key, string value);
}
