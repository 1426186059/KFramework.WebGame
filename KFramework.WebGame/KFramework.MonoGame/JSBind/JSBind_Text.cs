using System.Runtime.InteropServices.JavaScript;
using System.Threading.Tasks;

namespace KFramework.MonoGame
{

    /// <summary>
    /// text 模块绑定：借 Canvas2D 测量与栅格化文字，并提供自定义字体（ttf/otf/woff）的注册入口。
    /// 依赖 KFramework.TSEngine 项目：本类 JSImport 映射到 src/text.ts 的 "text" 模块；编译产物 text.js 由 SyncJsEngine 复制。
    /// 这里只负责跨语言调用，字形图集与排版见 <c>KFramework.MonoGame.SpriteFont</c>，位图字体见 <c>KFramework.MonoGame.BitmapFont</c>。
    /// </summary>
    internal static partial class JSBind_Text
    {
        /// <summary>测量文本尺寸，结果写入 [宽, 高, 基线以上高度(ascent), 0]。</summary>
        [JSImport("measure", "text")]
        internal static partial void Measure(string text, string font, float letterSpacing, [JSMarshalAs<JSType.MemoryView>] Span<int> result);

        /// <summary>把文本渲染成 RGBA8 像素。</summary>
        [JSImport("render", "text")]
        internal static partial void Render(string text, string font, float letterSpacing, int x, int y, int width, int height,
            [JSMarshalAs<JSType.MemoryView>] Span<byte> rgba);

        /// <summary>
        /// 注册自定义字体：从 URL 下载字体文件并加入 document.fonts，之后即可按 <paramref name="family"/> 光栅化。
        /// 用 JS 侧下载而非 HttpClient，是为了让浏览器把字体当成字体资源解析（FontFace.load），也省一次 C# 侧字节拷贝。
        /// </summary>
        [JSImport("loadFontFromUrl", "text")]
        internal static partial Task<bool> LoadFontFromUrl(string family, string url);

        /// <summary>
        /// 注册自定义字体：直接用字体文件字节（ttf / otf / woff）构造 FontFace 并加入 document.fonts。
        /// 适用于字体被打进 AssetBundle 的情形（<c>AssetBundle.LoadAsset</c> 取出字节）。
        /// </summary>
        [JSImport("loadFontFromBytes", "text")]
        internal static partial Task<bool> LoadFontFromBytes(string family, byte[] bytes);
    }
}
