using KFramework.MonoGame;

// 对齐原版 System.Drawing.Font：仅作字体描述符（族名 / 字号 / 样式 / 单位），本身不含任何渲染或度量逻辑。
// SpriteFont 的创建与缓存由 TextRenderer 内部负责（对应原版 GDI 资源由 TextRenderer 管理）。
public class Font : IDisposable
{
    public string Name;
    public float Size;            // em 字号（GraphicsUnit.Point 时按 *4/3 换算像素）
    public FontStyle Style;
    public GraphicsUnit Unit;

    public int Height => (int)Math.Ceiling(Size * 4f / 3f);
    public bool Bold => (Style & FontStyle.Bold) != 0;
    public bool Italic => (Style & FontStyle.Italic) != 0;

    public Font(string familyName, float emSize)
        : this(familyName, emSize, FontStyle.Regular) { }

    public Font(string familyName, float emSize, FontStyle style)
    {
        Name = familyName; Size = emSize; Style = style; Unit = GraphicsUnit.Point;
    }

    public Font(string familyName, float emSize, GraphicsUnit unit)
    {
        Name = familyName; Size = emSize; Unit = unit;
    }

    public Font(string familyName, float emSize, FontStyle style, GraphicsUnit unit)
    {
        Name = familyName; Size = emSize; Style = style; Unit = unit;
    }

    public Font(Font prototype, FontStyle newStyle)
    {
        Name = prototype.Name; Size = prototype.Size; Unit = prototype.Unit; Style = newStyle;
    }

    public object Clone() => new Font(Name, Size, Style, Unit);

    public void Dispose() { }

    public override string ToString() => $"[Font: Name={Name}, Size={Size}, Style={Style}, Unit={Unit}]";
}
