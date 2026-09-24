namespace KFramework.MonoGame.TextRenderer
{
    /// <summary>
    /// 字体描述符：对齐 System.Drawing.Font（族名 / em 字号 / 样式 / 单位），本身不含任何渲染或度量逻辑。
    /// SpriteFont（真实字形资源）的创建与缓存由 <see cref="TextRenderer"/> 内部负责，
    /// 对应原版 WinForms 中 GDI 字体资源由 TextRenderer 管理的分工。
    /// </summary>
    public sealed class Font : System.IDisposable
    {
        public string Name;
        /// <summary>em 字号（GraphicsUnit.Point 时按 *4/3 换算像素）。</summary>
        public float Size;
        public FontStyle Style;
        public GraphicsUnit Unit;

        /// <summary>行高（像素）。</summary>
        public int Height => (int)System.Math.Ceiling(GetPixelSize() * 4f / 3f);
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

        /// <summary>按 <see cref="Unit"/> 把 em 字号换算成像素字号。</summary>
        public float GetPixelSize()
            => Unit == GraphicsUnit.Pixel ? Size : Size * 4f / 3f;

        public object Clone() => new Font(Name, Size, Style, Unit);

        public void Dispose() { }

        public override string ToString()
            => $"[Font: Name={Name}, Size={Size}, Style={Style}, Unit={Unit}]";
    }
}
