// SlimDX 子集兼容壳：仅实现 Crystal/Client (Mir2) 渲染代码实际用到的类型，
// 让 95 个逻辑文件几乎不改即可编译到浏览器。真正的绘制由 RenderingCore(Canvas) 接管。
using MirEngine;

namespace SlimDX
{
    public struct Vector2
    {
        public float X, Y;
        public static readonly Vector2 Zero = new Vector2(0, 0);
        public Vector2(float x, float y) { X = x; Y = y; }
        public static Vector2 operator +(Vector2 a, Vector2 b) => new Vector2(a.X + b.X, a.Y + b.Y);
        public static Vector2 operator -(Vector2 a, Vector2 b) => new Vector2(a.X - b.X, a.Y - b.Y);
        public static Vector2 operator *(Vector2 a, float s) => new Vector2(a.X * s, a.Y * s);
        public static Vector2 operator /(Vector2 a, float s) => new Vector2(a.X / s, a.Y / s);
        public static bool operator ==(Vector2 a, Vector2 b) => a.X == b.X && a.Y == b.Y;
        public static bool operator !=(Vector2 a, Vector2 b) => !(a == b);
        public override bool Equals(object o) => o is Vector2 v && v == this;
        public override int GetHashCode() => (X.GetHashCode() * 397) ^ Y.GetHashCode();
    }

    public struct Vector3
    {
        public float X, Y, Z;
        public static readonly Vector3 Zero = new Vector3(0, 0, 0);
        public Vector3(float x, float y, float z) { X = x; Y = y; Z = z; }
        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Vector3 operator *(Vector3 a, float s) => new Vector3(a.X * s, a.Y * s, a.Z * s);
        public static Vector3 operator /(Vector3 a, float s) => new Vector3(a.X / s, a.Y / s, a.Z / s);
        public static bool operator ==(Vector3 a, Vector3 b) => a.X == b.X && a.Y == b.Y && a.Z == b.Z;
        public static bool operator !=(Vector3 a, Vector3 b) => !(a == b);
        public override bool Equals(object o) => o is Vector3 v && v == this;
        public override int GetHashCode() => (X.GetHashCode() * 397 ^ Y.GetHashCode() * 397) ^ Z.GetHashCode();
    }

    // 颜色：调用方多传 Color，需支持隐式转换。
    public struct Color4
    {
        public float Red, Green, Blue, Alpha;
        public float R { get => Red; set => Red = value; }
        public float G { get => Green; set => Green = value; }
        public float B { get => Blue; set => Blue = value; }
        public float A { get => Alpha; set => Alpha = value; }

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
    // 纹理句柄：浏览器端用 int 句柄（Image 或离屏 canvas）。
    public class Texture
    {
        public int Handle;
        public int Width, Height;
        public bool Disposed;

        public Texture(int handle, int width, int height)
        {
            Handle = handle; Width = width; Height = height; Disposed = false;
        }
        public bool Valid => Handle != 0 && !Disposed;

        public Texture(Device device, int width, int height, int levels, Usage usage, Format format, Pool pool)
        {
            Handle = BrowserCanvas.CreateOffscreen(Math.Max(1, width), Math.Max(1, height));
            Width = width; Height = height; Disposed = false;
        }

        public Surface GetSurfaceLevel(int level) => new Surface(Handle);

        public void Dispose()
        {
            if (Disposed) return;
            if (Handle != 0) MirEngine.BrowserCanvas.DisposeImage(Handle);
            Disposed = true; Handle = 0;
        }
    }

    // 离屏渲染目标表面（浏览器端由 CanvasRenderingPipeline 的 CreateRenderTarget 提供）。
    public class Surface
    {
        public int Handle;
        public Surface(int handle) { Handle = handle; }
        public bool Disposed => Handle == 0;
        public void Dispose() { Handle = 0; }
    }

    // 仅 DXManager 重写后内部用到的占位类型（供潜在残留引用编译通过）。
    public enum Blend { SourceAlpha, InverseSourceAlpha, One, BlendFactor, InverseBlendFactor, Zero, SourceColor }
    public enum Format { A8R8G8B8, X8R8G8B8 }
    public enum Usage { None, RenderTarget }
    public enum Pool { Managed, Default }
    public enum LockFlags { Discard, None }
    public enum DeviceType { Hardware, Reference }
    public enum CreateFlags { HardwareVertexProcessing, PureDevice }
    public enum PresentFlags { LockableBackBuffer, None }
    public enum SwapEffect { Discard }
    public enum SpriteFlags { None = 0, AlphaBlend = 1 }
    public enum PresentInterval { One, Immediate, Default }
    public enum RenderState { AlphaBlendEnable, SourceBlend, DestinationBlend, BlendFactor, SourceBlendAlpha }
    public enum BlendOperation { Add }
    public enum ClearFlags { Target }
    public enum DeviceCaps { HWTransformAndLight, PureDevice }
    public class PixelShader { public void Dispose() { } }
    // 浏览器端 Device：不再持有真实 D3D9 设备。CurrentTarget 即"当前渲染目标"（Unity 的 RenderTexture.active 等价物）。
    public class Device
    {
        public static int CurrentTarget = 0;   // 0 = 主画布，>0 = 离屏 canvas 句柄
        public static int LastTarget = -1;
        public static void ApplyTarget()
        {
            if (LastTarget != CurrentTarget) { BrowserCanvas.SetTarget(CurrentTarget); LastTarget = CurrentTarget; }
        }
        public void Dispose() { }
        public void SetRenderState(RenderState s, object v) { }
        public void Clear(ClearFlags f, Color c, float z, int s)
        {
            ApplyTarget();
            BrowserCanvas.Clear(c);
        }
        public void SetRenderTarget(int i, Surface sf) { CurrentTarget = sf == null ? 0 : sf.Handle; }
    }

    // 精灵/线条：浏览器端即时模式绘制，Flush/Begin/End 为无操作。
    public class Sprite
    {
        public Matrix Transform;
        public Sprite() { Transform = Matrix.Identity; }
        public void Flush() { }
        public void Begin(object flags) { }
        public void End() { }
        public void Draw(Texture texture, Rectangle? src, Vector3 position, Vector3 scaling, Color4 color) { }
    }

    public class Line
    {
        public float Width = 1F;
        public void Draw(Vector2[] points, Color color)
        {
            if (points == null || points.Length < 2) return;
            for (int i = 0; i + 1 < points.Length; i++)
                MirEngine.BrowserCanvas.DrawLine(points[i].X, points[i].Y, points[i + 1].X, points[i + 1].Y, Width, color.ToArgb());
        }
    }

    public struct Matrix
    {
        public float M11, M12, M13, M14, M21, M22, M23, M24, M31, M32, M33, M34, M41, M42, M43, M44;
        public static readonly Matrix Identity = new Matrix
        {
            M11 = 1, M22 = 1, M33 = 1, M44 = 1
        };
        public static Matrix Scaling(float x, float y, float z)
        {
            return new Matrix { M11 = x, M22 = y, M33 = z, M44 = 1 };
        }
        public static Matrix Translation(float x, float y, float z)
        {
            return new Matrix { M11 = 1, M22 = 1, M33 = 1, M44 = 1, M41 = x, M42 = y, M43 = z };
        }
    }
}
