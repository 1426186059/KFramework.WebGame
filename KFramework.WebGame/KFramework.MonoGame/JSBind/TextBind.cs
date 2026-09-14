using System.Runtime.InteropServices.JavaScript;

namespace KFramework.JSBind;

/// <summary>
/// text 模块绑定：借 Canvas2D 测量与栅格化文字。
/// 这里只负责跨语言调用，字形图集与排版见 <c>KFramework.Graphics.SpriteFont</c>。
/// </summary>
internal static partial class TextBind
{
    /// <summary>测量文本尺寸，结果写入 [宽, 高]。</summary>
    [JSImport("measure", "text")]
    internal static partial void Measure(string text, string font, [JSMarshalAs<JSType.MemoryView>] Span<int> result);

    /// <summary>把文本渲染成 RGBA8 像素。</summary>
    [JSImport("render", "text")]
    internal static partial void Render(string text, string font, int x, int y, int width, int height,
        [JSMarshalAs<JSType.MemoryView>] Span<byte> rgba);
}
