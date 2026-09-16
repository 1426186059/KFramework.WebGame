namespace KFramework.MonoGame
{
    /// <summary>渲染目标区域（屏幕坐标系，左上原点）。</summary>
    public struct Viewport
    {
        public int X;
        public int Y;
        public int Width;
        public int Height;
        public float MinDepth;
        public float MaxDepth;

        public Viewport(int x, int y, int width, int height)
        {
            X = x; Y = y; Width = width; Height = height;
            MinDepth = 0f; MaxDepth = 1f;
        }

        public Rectangle Bounds => new(X, Y, Width, Height);
        public float AspectRatio => Height == 0 ? 0f : Width / (float)Height;
    }
}
