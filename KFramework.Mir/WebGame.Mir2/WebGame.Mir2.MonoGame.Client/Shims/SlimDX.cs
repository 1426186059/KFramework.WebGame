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
    // 纹理句柄：浏览器端用 int 句柄（普通贴图）或包裹 KFramework.MonoGame 的离屏渲染目标。
    public class Texture
    {
        public int Handle;
        public int Width, Height;
        public bool Disposed;

        // 离屏渲染目标（浏览器端走 KFramework.MonoGame.RenderTarget2D）：非 null 即表示这是一张可烘焙的离屏纹理。
        internal KFramework.MonoGame.RenderTarget2D? RenderTarget;

        public Texture(int handle, int width, int height)
        {
            Handle = handle; Width = width; Height = height; Disposed = false;
        }
        public bool Valid => Handle != 0 && !Disposed;

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
                Handle = -2; // 标记：RenderTarget 包裹（非普通 int 句柄）
            }
            else
            {
                // 普通贴图在浏览器端走 Texture2D 主路径，这里只给无效占位。
                Handle = -1;
            }
        }

        public Surface GetSurfaceLevel(int level) => new Surface(this);

        public void Dispose()
        {
            if (Disposed) return;
            if (RenderTarget != null) { RenderTarget.Dispose(); RenderTarget = null; }
            Disposed = true; Handle = 0;
        }
    }

    // 离屏渲染目标表面：持有其所属 Texture，SetSurface 据此取出底层 RenderTarget2D。
    public class Surface
    {
        public Texture? Owner;
        public Surface(int handle) { Owner = null; }   // 占位兼容（不被新路径使用）
        public Surface(Texture owner) { Owner = owner; }
        public bool Disposed => Owner == null || Owner.Disposed;
        public void Dispose() { Owner = null; }
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
            LastTarget = CurrentTarget;
        }
        public void Dispose() { }
        public void SetRenderState(RenderState s, object v) { }
        public void Clear(ClearFlags f, Color c, float z, int s)
        {
            // 清屏作用到当前绑定的渲染目标（离屏 RT 或默认画布），交由 KFramework.MonoGame.GraphicsDevice 下发。
            var g = Client.MirGraphics.DXManager.GDevice;
            if (g != null)
                g.Clear(new KFramework.MonoGame.Color(c.R, c.G, c.B, c.A));
        }
        public void SetRenderTarget(int i, Surface sf) { Client.MirGraphics.DXManager.SetSurface(sf); }
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
            // 线条绘制暂未接入 SpriteBatch；保留签名以兼容调用点（不影响主流程）。
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
