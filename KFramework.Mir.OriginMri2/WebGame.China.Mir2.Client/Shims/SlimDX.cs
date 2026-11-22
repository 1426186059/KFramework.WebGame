// SlimDX 子集兼容壳：仅实现传奇客户端 (Mir2) 渲染代码实际用到的类型，
// 让 95 个逻辑文件几乎不改即可编译到浏览器。真正的绘制由 RenderingCore(Canvas) 接管。
using Client.MirGraphics;

namespace SlimDX
{
    // 颜色：调用方多传 Color，需支持隐式转换。
    public struct Color4
    {
        public float Red, Green, Blue, Alpha;

        public Color4(float alpha, float red, float green, float blue)
        {
            Alpha = alpha; Red = red; Green = green; Blue = blue;
        }
        public Color4(Color c)
        {
            Alpha = c.A / 255f; Red = c.R / 255f; Green = c.G / 255f; Blue = c.B / 255f;
        }
        public static implicit operator Color4(Color c) => new Color4(c);
    }
}

namespace SlimDX.Direct3D9
{
    // 纹理句柄：浏览器端用 int 句柄（普通贴图）或包裹 KFramework.MonoGame 的离屏渲染目标。
    public class Texture
    {
        public int Width, Height;
        public bool Disposed;

        // 离屏渲染目标（浏览器端走 KFramework.MonoGame.RenderTarget2D）：非 null 即表示这是一张可烘焙的离屏纹理。
        internal KFramework.MonoGame.RenderTarget2D? RenderTarget;

        // 普通贴图（浏览器端走 KFramework.MonoGame.Texture2D 主路径）：用于程序化生成的光晕等静态纹理。
        internal KFramework.MonoGame.Texture2D? PlainTexture;

        public Texture(Device device, int width, int height, int levels, Usage usage, Format format, Pool pool)
        {
            Width = width; Height = height; Disposed = false;
            if (usage == Usage.RenderTarget && width > 0 && height > 0)
            {
                // 离屏渲染目标：用 KFramework.MonoGame 的真·FBO 渲染目标承载。
                // 用 PreserveContents：嵌套合成时回绑该目标不会丢内容；目标本身的清屏由
                // ClearControlTexture / Device.Clear 显式完成（避免 ApplyRenderTargets 的 Discard 清屏）。
                var g = Client.MirGraphics.DXManager.GDevice;
                if (g != null)
                    RenderTarget = new KFramework.MonoGame.RenderTarget2D(
                        g, width, height, false, KFramework.MonoGame.SurfaceFormat.Color,
                        KFramework.MonoGame.DepthFormat.None, 0, KFramework.MonoGame.RenderTargetUsage.PreserveContents);
            }
        }

        public Surface GetSurfaceLevel(int level) => new Surface(this);

        public void Dispose()
        {
            if (Disposed) return;
            if (RenderTarget != null) { RenderTarget.Dispose(); RenderTarget = null; }
            if (PlainTexture != null) { PlainTexture.Dispose(); PlainTexture = null; }
            Disposed = true;
        }
    }

    // 离屏渲染目标表面：持有其所属 Texture，SetSurface 据此取出底层 RenderTarget2D。
    public class Surface
    {
        public Texture? Owner;
        public Surface(Texture owner) { Owner = owner; }
        public void Dispose() { Owner = null; }
    }

    public enum Format { A8R8G8B8 }
    public enum Usage { None, RenderTarget }
    public enum Pool { Managed, Default }
    public enum ClearFlags { Target }
    public class Device
    {
        public void BeginScene() { }
        public void EndScene() { }
        public void Present() { }
        public void Clear(ClearFlags f, Color c, float z, int s)
        {
            // 清屏作用到当前绑定的渲染目标（离屏 RT 或默认画布），交由 KFramework.MonoGame.GraphicsDevice 下发。
            var g = Client.MirGraphics.DXManager.GDevice;
            if (g != null)
                g.Clear(new KFramework.MonoGame.Color(c.R, c.G, c.B, c.A));
        }
        public void SetRenderTarget(int i, Surface sf) { Client.MirGraphics.DXManager.SetSurface(sf); }
    }

    public class Sprite
    {
    }

    public class Line
    {
        public float Width = 1F;
        public void Draw(KFramework.MonoGame.Vector2[] points, Color color)
        {
            // 线条绘制已接入：复用 DXManager 的 1×1 白像素沿 SpriteBatch 拉伸成细矩形（见 DXManager.DrawLines）。
            DXManager.DrawLines(points, Width, color);
        }
    }

}
