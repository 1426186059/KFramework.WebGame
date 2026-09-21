using System.Runtime.InteropServices.JavaScript;
using System.Threading.Tasks;

namespace KFramework.MonoGame
{
    /// <summary>
    /// http_func 模块绑定：通用取字节——先查 Cache Storage，未命中再 fetch 下载并写回缓存。
    /// 整条链路都在 JS 侧完成，字节只在最后一刻写进 C# 预分配的缓冲，省掉 HttpClient 方案里
    /// “下载 → 进 WASM byte[] → 再传回 JS 存 Cache”的那次来回搬运。
    /// 实际逻辑见 KFramework.TSEngine/src/http_func.ts（"http_func" 模块）；产物 http_func.js 随 jsengine 进 wwwroot。
    ///
    /// 与 C# 交换字节沿用「预分配缓冲 + 写回」模式（同 decodeImageToRgba）：C# 备好 byte[]，JS 往里写，
    /// 绕开 .NET WASM 不支持 byte[] 作为返回值的限制（SYSLIB1072）。
    /// 业务不要直接调用，请用上层封装。
    /// </summary>
    public static partial class JSBind_Http
    {
        /// <summary>
        /// 取 <paramref name="name"/>（资源的键 / URL，相对路径按 document.baseURI 解析）对应的字节，
        /// 写入 <paramref name="buffer"/>。
        /// </summary>
        /// <param name="useCache">是否启用 Cache Storage（false = 纯下载）。</param>
        /// <returns>
        /// &gt;=0 实际写入字节数；-1 失败（非 2xx / 网络错误）；
        /// &lt;=-2 缓冲不足，-(返回值) 即所需长度，本次字节已在 JS 侧暂存，
        /// 按该长度重新分配缓冲后再调一次即可立即取回（不重复下载）。
        /// </returns>
        [JSImport("loadCacheOrDownloadAsync", "http_func")]
        public static partial Task<int> LoadCacheOrDownloadAsync(string name, bool useCache, [JSMarshalAs<JSType.MemoryView>] Span<byte> returnValue);

        /// <summary>
        /// 丢弃「缓冲不足」时暂存在 JS 侧的字节（调用方放弃重试时释放内存）。
        /// </summary>
        /// <param name="name">资源键；传空字符串则清空全部暂存。</param>
        [JSImport("releasePending", "http_func")]
        public static partial void ReleasePending(string name);
    }
}
