using System.Runtime.InteropServices;
using System.Runtime.InteropServices.JavaScript;
using System.Threading.Tasks;

namespace KFramework.MonoGame
{

    /// <summary>
    /// text 模块绑定：借 Canvas2D 测量与栅格化文字，并提供自定义字体（ttf/otf/woff）的注册入口。
    /// 依赖 KFramework.TSEngine 项目：本类 JSImport 映射到 src/text.ts 的 "text" 模块；编译产物 text.js 由 SyncJsEngine 复制。
    /// 这里只负责跨语言调用，字形图集与排版见 <c>KFramework.MonoGame.SpriteFont</c>，位图字体见 <c>KFramework.MonoGame.BitmapFont</c>。
    /// </summary>
    public static partial class JSBind_Text
    {
        /// <summary>
        /// 测量文本尺寸（JS 侧入口）：4 项度量以小端 int16 写进 8 字节缓冲。
        /// <para>
        /// 为什么是 <c>Span&lt;byte&gt;</c> 而不是 <c>Span&lt;short&gt;</c>：.NET 的 <c>JSType.MemoryView</c> 编组
        /// 只支持 <c>byte</c> / <c>int</c> 两类元素，<c>Span&lt;short&gt;</c> 过不了 source generator
        /// （报 <c>JSMarshalerType 未包含 None</c>）。故由 <see cref="Measure"/> 负责重新解释成 short。
        /// </para>
        /// </summary>
        [JSImport("measure", "text")]
        private static partial void MeasureRaw(string text, string font, float letterSpacing, [JSMarshalAs<JSType.MemoryView>] Span<byte> result);

        /// <summary>
        /// 测量文本尺寸，结果写入 [advance(宽), 总高, 基线以上高度(ascent), 0]，四项均为 short（像素量）。
        /// JS 侧写的是 8 字节小端 int16，这里零拷贝重新解释（WASM 为小端，与 JS 侧 Int16Array 一致）。
        /// </summary>
        public static void Measure(string text, string font, float letterSpacing, Span<short> result)
        {
            Span<byte> bytes = stackalloc byte[8];
            MeasureRaw(text, font, letterSpacing, bytes);
            MemoryMarshal.Cast<byte, short>(bytes).CopyTo(result);
        }

        /// <summary>把文本渲染成 RGBA8 像素。</summary>
        [JSImport("render", "text")]
        public static partial void Render(string text, string font, float letterSpacing, int x, int y, int width, int height,
            [JSMarshalAs<JSType.MemoryView>] Span<byte> rgba);

        /// <summary>
        /// 注册自定义字体：从 URL 下载字体文件并加入 document.fonts，之后即可按 <paramref name="family"/> 光栅化。
        /// 用 JS 侧下载而非 HttpClient，是为了让浏览器把字体当成字体资源解析（FontFace.load），也省一次 C# 侧字节拷贝。
        /// </summary>
        [JSImport("loadFontFromUrl", "text")]
        public static partial Task<bool> LoadFontFromUrl(string family, string url);

        /// <summary>
        /// 注册自定义字体：直接用字体文件字节（ttf / otf / woff）构造 FontFace 并加入 document.fonts。
        /// 适用于字体被打进 AssetBundle 的情形（<c>AssetBundle.LoadAsset</c> 取出字节）。
        /// </summary>
        [JSImport("loadFontFromBytes", "text")]
        public static partial Task<bool> LoadFontFromBytes(string family, byte[] bytes);
    }
}
