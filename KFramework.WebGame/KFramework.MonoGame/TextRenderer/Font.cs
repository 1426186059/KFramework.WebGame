namespace KFramework.MonoGame.TextRenderer
{
    /// <summary>
    /// 字体描述符：对齐 System.Drawing.Font（族名 / em 字号 / 样式 / 单位），同时实现 <see cref="IFont"/>。
    ///
    /// 作为 IFont 时，度量与绘制委托给它解析出的真实字形资源（<see cref="SpriteFont"/>，
    /// 由 <see cref="TextRenderer.Resolve"/> 按设备缓存），因此调用方可以像使用 SpriteFont / BitmapFont
    /// 一样把本描述符直接传给 DrawText / MeasureText，无需关心字形来源。
    ///
    /// 解析需要设备：优先用 <see cref="TextRenderer.DefaultDevice"/>（引擎初始化时设置；
    /// 也可由调用方显式传设备给 TextRenderer 的 dc 参数）。若设备尚未就绪，度量返回 0、绘制为空操作。
    /// </summary>
    public sealed class Font : IFont, System.IDisposable
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

        // ---------------- IFont ----------------

        public float LineSpacing => Resolved?.LineSpacing ?? 0f;

        public Vector2 Measure(string text) => Resolved?.Measure(text) ?? Vector2.Zero;

        public Vector2 MeasureString(string text) => Resolved?.MeasureString(text) ?? Vector2.Zero;

        public void Draw(SpriteBatch batch, string text, Vector2 position, Color color,
                         float rotation, Vector2 origin, float scale, SpriteEffects effects, float layerDepth)
            => Resolved?.Draw(batch, text, position, color, rotation, origin, scale, effects, layerDepth);

        /// <summary>本描述符对应的真实字形资源（缓存）；设备未就绪时为 null。</summary>
        private SpriteFont Resolved => TextRenderer.Resolve(TextRenderer.DefaultDevice, this);

        // ---------------- 其它 ----------------

        public object Clone() => new Font(Name, Size, Style, Unit);

        public void Dispose() { }

        public override string ToString()
            => $"[Font: Name={Name}, Size={Size}, Style={Style}, Unit={Unit}]";
    }
}
