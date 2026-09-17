using System.Runtime.InteropServices.JavaScript;
using System.Threading.Tasks;

namespace KFramework.MonoGame
{
    /// <summary>
    /// cachestorage 模块绑定：把资源包字节（JS/CSS/图片/KTX2 纹理、.web.lib 等静态资源）持久化到
    /// 浏览器 Cache Storage。相较于 IndexedDB，Cache Storage 以 Response 形式存储二进制资源，序列化开销更小，
    /// 后续可叠加 Service Worker 拦截 fetch 直接返回缓存响应，做到“零解析开销”。
    /// 实际逻辑见 KFramework.TSEngine/src/cachestorage.ts（"cachestorage" 模块）。
    ///
    /// 与 C# 交换字节采用「预分配缓冲 + 写回」模式（同 decodeImageToRgba）：先 <see cref="GetSizeAsync"/>
    /// 探长度，C# 按长度分配 byte[] 后交给 <see cref="LoadIntoAsync"/> 写入，绕开 .NET WASM 不支持 byte[]
    /// 作为返回值的限制（SYSLIB1072）。
    /// </summary>
    internal static partial class JSBind_CacheStorage
    {
        /// <summary>返回 Cache Storage 中已存字节长度；不存在返回 -1。</summary>
        [JSImport("size", "cachestorage")]
        internal static partial Task<int> GetSizeAsync(string name);

        /// <summary>把 Cache Storage 中已存字节写入 <paramref name="buffer"/>；返回实际写入长度（缺失返回 -1）。</summary>
        [JSImport("loadInto", "cachestorage")]
        internal static partial Task<int> LoadIntoAsync(string name, byte[] buffer);

        /// <summary>把资源包字节（byte[]）以 Response 形式写入 Cache Storage（按 name 键，覆盖式）。</summary>
        [JSImport("save", "cachestorage")]
        internal static partial Task SaveAsync(string name, byte[] bytes);
    }
}
