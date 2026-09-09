using System.Runtime.InteropServices;

namespace KFramework.Graphics;

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

/// <summary>纹理采样方式。</summary>
public sealed class SamplerState
{
    public readonly int MinFilter;
    public readonly int MagFilter;
    public readonly int WrapMode;

    private SamplerState(int minFilter, int magFilter, int wrapMode)
    {
        MinFilter = minFilter; MagFilter = magFilter; WrapMode = wrapMode;
    }

    /// <summary>最近邻采样，像素风游戏的首选，放大后不模糊。</summary>
    public static readonly SamplerState Point =
        new(GL.NEAREST, GL.NEAREST, GL.CLAMP_TO_EDGE);

    /// <summary>双线性采样，适合需要平滑缩放的美术风格。</summary>
    public static readonly SamplerState Linear =
        new(GL.LINEAR, GL.LINEAR, GL.CLAMP_TO_EDGE);

    /// <summary>线性采样 + 平铺，用于背景滚动。</summary>
    public static readonly SamplerState LinearWrap =
        new(GL.LINEAR, GL.LINEAR, GL.REPEAT);
}

/// <summary>混合模式。</summary>
public sealed class BlendState
{
    public readonly int SourceBlend;
    public readonly int DestinationBlend;
    public readonly int SourceAlphaBlend;
    public readonly int DestinationAlphaBlend;

    private BlendState(int src, int dst, int srcA, int dstA)
    {
        SourceBlend = src; DestinationBlend = dst;
        SourceAlphaBlend = srcA; DestinationAlphaBlend = dstA;
    }

    public static readonly BlendState AlphaBlend =
        new(GL.ONE, GL.ONE_MINUS_SRC_ALPHA, GL.ONE, GL.ONE_MINUS_SRC_ALPHA);

    public static readonly BlendState NonPremultiplied =
        new(GL.SRC_ALPHA, GL.ONE_MINUS_SRC_ALPHA, GL.SRC_ALPHA, GL.ONE_MINUS_SRC_ALPHA);

    /// <summary>叠加发光，用于粒子、爆炸、激光。</summary>
    public static readonly BlendState Additive =
        new(GL.SRC_ALPHA, GL.ONE, GL.SRC_ALPHA, GL.ONE);

    public static readonly BlendState Opaque =
        new(GL.ONE, GL.ZERO, GL.ONE, GL.ZERO);
}

/// <summary>精灵翻转方式。</summary>
[Flags]
public enum SpriteEffects
{
    None = 0,
    FlipHorizontally = 1,
    FlipVertically = 2,
}

/// <summary>批处理排序策略。</summary>
public enum SpriteSortMode
{
    /// <summary>立即绘制，不合并批次（切换状态时调用 Draw 很方便，但性能最差）。</summary>
    Immediate,
    /// <summary>延迟到 End 时统一排序绘制（默认，批处理效果最好）。</summary>
    Deferred,
    /// <summary>按纹理排序，忽略深度。</summary>
    Texture,
    /// <summary>按深度从后往前（远的先画）。</summary>
    BackToFront,
    /// <summary>按深度从前往后。</summary>
    FrontToBack,
}

/// <summary>SpriteBatch 的顶点：位置(vec2) + 纹理坐标(vec2) + 颜色(rgba8)，共 20 字节。</summary>
[StructLayout(LayoutKind.Sequential)]
public struct VertexPositionColorTexture
{
    public Vector2 Position;
    public Vector2 TexCoord;
    public Color Color;

    public const int SizeInBytes = 20;

    public VertexPositionColorTexture(Vector2 position, Vector2 texCoord, Color color)
    {
        Position = position;
        TexCoord = texCoord;
        Color = color;
    }
}
