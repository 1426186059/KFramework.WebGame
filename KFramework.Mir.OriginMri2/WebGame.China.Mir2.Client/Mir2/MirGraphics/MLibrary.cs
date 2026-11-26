using Client.MirObjects;

namespace Client.MirGraphics
{
    /// <summary>
    /// 经典 .Lib 图库占位类。经典 .Lib 资源已从资源包移除，资源全面改用原始传奇 CDN
    /// （Mir2OriginResource / Mir2AtlasLibrary）。本类不再加载/解析任何 .Lib 字节，
    /// 所有绘制调用统一走 DrawMissing 品红兜底，便于定位尚未迁移的资源。
    /// 保留字段和方法签名仅为兼容现存绘制调用点。
    /// </summary>
    public sealed class MLibrary
    {
        private readonly string _fileName;

        // 缺资源兜底：画 KDefaultRes 默认贴图（品红方块），一眼定位缺失资源。
        private static readonly HashSet<string> _missingLogged = new HashSet<string>();

        private void DrawMissing(int index, Point point)
        {
            DrawMissing(index, new RectangleF(point.X, point.Y, 32, 32));
        }

        private void DrawMissing(int index, int x, int y)
        {
            DrawMissing(index, new RectangleF(x, y, 32, 32));
        }

        private void DrawMissing(int index, RectangleF destRect)
        {
            if (destRect.X >= Settings.ScreenWidth || destRect.Y >= Settings.ScreenHeight ||
                destRect.Right < 0 || destRect.Bottom < 0) return;

            Color magenta = CommonColor.FromArgb(255, 255, 0, 255);
            DXManager.Draw(KFramework.MonoGameExtend.KDefaultRes.DefaultTexture2D,
                           new Rectangle(0, 0, 1, 1), destRect, magenta);

            string key = _fileName + ":" + index;
            if (_missingLogged.Add(key))
                KFramework.MonoGame.PrintTool.LogError("[缺图兜底] " + key + " -> KDefaultRes 默认图（品红块）");
        }

        public MLibrary(string filename)
        {
            _fileName = filename ?? string.Empty;
        }

        public Task InitializeAsync() => Task.CompletedTask;

        /// <summary>.Lib 已停用：返回 null，调用方用 ?? FrameSet.DefaultXxx 兜底。</summary>
        public FrameSet Frames => null;

        public MImage CheckImage(int index) => null;
        public Point GetOffSet(int index) => Point.Zero;
        public bool IsPending => false;
        public Size GetSize(int index) => Size.Empty;
        public Size GetTrueSize(int index) => Size.Empty;
        public bool VisiblePixel(int index, Point point, bool accuate) => false;

        // 以下绘制重载全部只做品红兜底，保持与调用方的签名兼容。
        public void Draw(int index, int x, int y)
        {
            if (x >= Settings.ScreenWidth || y >= Settings.ScreenHeight) return;
            DrawMissing(index, x, y);
        }

        public void Draw(int index, Point point, Color colour, bool offSet = false) => DrawMissing(index, point);

        public void Draw(int index, Point point, Color colour, bool offSet, float opacity) => DrawMissing(index, point);

        public void DrawBlend(int index, Point point, Color colour, bool offSet = false, float rate = 1) => DrawMissing(index, point);

        public void DrawBlend(int index, Point point, Size size, Color colour, float rate = 1)
            => DrawMissing(index, new RectangleF(point.X, point.Y, size.Width, size.Height));

        public void Draw(int index, Rectangle section, Point point, Color colour, bool offSet) => DrawMissing(index, point);

        public void Draw(int index, Rectangle section, Point point, Color colour, float opacity) => DrawMissing(index, point);

        public void Draw(int index, Point point, Size size, Color colour)
            => DrawMissing(index, new RectangleF(point.X, point.Y, size.Width, size.Height));

        public void Draw(int index, Rectangle section, RectangleF destRect, Color colour, float opacity = 1f)
            => DrawMissing(index, destRect);
    }
}
