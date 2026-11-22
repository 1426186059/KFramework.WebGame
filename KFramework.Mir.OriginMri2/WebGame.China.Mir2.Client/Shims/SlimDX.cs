using Client.MirGraphics;

namespace SlimDX.Direct3D9
{
    public class Texture
    {
        public int Width, Height;
        public bool Disposed;

        internal KFramework.MonoGame.RenderTarget2D? RenderTarget;
        internal KFramework.MonoGame.Texture2D? PlainTexture;

        public Texture(Device device, int width, int height, int levels, Usage usage, Format format, Pool pool)
        {
            Width = width; Height = height; Disposed = false;
            if (usage == Usage.RenderTarget && width > 0 && height > 0)
            {
                var g = DXManager.GDevice;
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
            var g = DXManager.GDevice;
            if (g != null)
            {
                g.Clear(c);
            }
        }

        public void SetRenderTarget(int i, Surface sf) 
        { 
            DXManager.SetSurface(sf); 
        }
    }

    public class Line
    {
        public float Width = 1F;
        public void Draw(Vector2[] points, Color color)
        {
            DXManager.DrawLines(points, Width, color);
        }
    }

}
