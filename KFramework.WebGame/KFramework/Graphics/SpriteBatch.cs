using System.Runtime.InteropServices;

namespace KFramework.Graphics;

/// <summary>
/// 2D 精灵批处理器。用法与 MonoGame 一致：<c>Begin()</c> → 若干 <c>Draw()</c> → <c>End()</c>。
/// 同一张底层纹理（含图集内的所有子图）会被合并为一次 draw call。
/// </summary>
public sealed class SpriteBatch
{
    private struct SpriteBatchItem
    {
        public SpriteBatchItem() => Texture = null!;

        public Texture2D Texture;
        public Vector2 Position;
        public Rectangle Source;
        public Vector2 Size;
        public Vector2 Origin;
        public float Rotation;
        public Color Color;
        public float LayerDepth;
        public SpriteEffects Effects;
    }

    private readonly GraphicsDevice _device;
    private readonly SpriteBatchItem[] _items = new SpriteBatchItem[GraphicsDevice.MaxBatchSize];
    private readonly VertexPositionColorTexture[] _vertices = new VertexPositionColorTexture[GraphicsDevice.MaxBatchSize * 4];
    private int _itemCount;

    private bool _beginCalled;
    private SpriteSortMode _sortMode;
    private BlendState _blendState = BlendState.AlphaBlend;
    private SamplerState _samplerState = SamplerState.Point;
    private Matrix4x4 _transform = Matrix4x4.Identity;
    private Matrix4x4 _projection;

    private static readonly Comparison<SpriteBatchItem> ByTexture = static (a, b) =>
    {
        int d = a.Texture.BatchKey.CompareTo(b.Texture.BatchKey);
        return d != 0 ? d : a.LayerDepth.CompareTo(b.LayerDepth);
    };

    private static readonly Comparison<SpriteBatchItem> BackToFront = static (a, b) => b.LayerDepth.CompareTo(a.LayerDepth);
    private static readonly Comparison<SpriteBatchItem> FrontToBack = static (a, b) => a.LayerDepth.CompareTo(b.LayerDepth);

    public SpriteBatch(GraphicsDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);
        _device = device;
    }

    public GraphicsDevice GraphicsDevice => _device;

    public void Begin(SpriteSortMode sortMode = SpriteSortMode.Deferred,
                      BlendState? blendState = null,
                      SamplerState? samplerState = null,
                      Matrix4x4? transformMatrix = null)
    {
        if (_beginCalled) throw new InvalidOperationException("上一次 Begin 还没有对应的 End。");

        _sortMode = sortMode;
        // 纹理数据是非预乘的，所以默认用 NonPremultiplied（SRC_ALPHA, ONE_MINUS_SRC_ALPHA）
        _blendState = blendState ?? BlendState.NonPremultiplied;
        _samplerState = samplerState ?? SamplerState.Point;
        _transform = transformMatrix ?? Matrix4x4.Identity;
        _itemCount = 0;

        var viewport = _device.Viewport;
        _projection = Matrix4x4.CreateOrthographicScreen(viewport.Width, viewport.Height);

        _beginCalled = true;
    }

    public void End()
    {
        if (!_beginCalled) throw new InvalidOperationException("End 必须在 Begin 之后调用。");
        Flush();
        _beginCalled = false;
    }

    #region Draw 重载

    public void Draw(Texture2D texture, Vector2 position, Color color)
        => Draw(texture, position, null, color, 0f, Vector2.Zero, Vector2.One, SpriteEffects.None, 0f);

    public void Draw(Texture2D texture, Vector2 position, Color color, float rotation, Vector2 origin, float scale, float layerDepth = 0f)
        => Draw(texture, position, null, color, rotation, origin, new Vector2(scale, scale), SpriteEffects.None, layerDepth);

    public void Draw(Texture2D texture, Vector2 position, Rectangle? sourceRectangle, Color color)
        => Draw(texture, position, sourceRectangle, color, 0f, Vector2.Zero, Vector2.One, SpriteEffects.None, 0f);

    public void Draw(Texture2D texture, Rectangle destination, Color color)
        => Draw(texture, destination, null, color);

    public void Draw(Texture2D texture, Rectangle destination, Rectangle? sourceRectangle, Color color)
    {
        ArgumentNullException.ThrowIfNull(texture);
        Draw(texture, new Vector2(destination.X, destination.Y), sourceRectangle, color,
             0f, Vector2.Zero,
             new Vector2(destination.Width / (float)(sourceRectangle?.Width ?? texture.Width),
                         destination.Height / (float)(sourceRectangle?.Height ?? texture.Height)),
             SpriteEffects.None, 0f);
    }

    /// <summary>完整参数的绘制。</summary>
    public void Draw(Texture2D texture, Vector2 position, Rectangle? sourceRectangle, Color color,
                     float rotation, Vector2 origin, Vector2 scale, SpriteEffects effects, float layerDepth)
    {
        ArgumentNullException.ThrowIfNull(texture);
        if (!_beginCalled) throw new InvalidOperationException("Draw 必须在 Begin / End 之间调用。");

        Rectangle source = sourceRectangle ?? new Rectangle(0, 0, texture.Width, texture.Height);

        SpriteBatchItem item = new()
        {
            Texture = texture,
            Position = position,
            Source = source,
            Size = new Vector2(source.Width * scale.X, source.Height * scale.Y),
            Origin = origin,
            Rotation = rotation,
            Color = color,
            LayerDepth = layerDepth,
            Effects = effects,
        };

        if (_itemCount >= _items.Length) Flush();
        _items[_itemCount++] = item;

        if (_sortMode == SpriteSortMode.Immediate) Flush();
    }

    /// <summary>九宫格之外的常用辅助：以中心点对齐绘制并缩放。</summary>
    public void DrawCentered(Texture2D texture, Vector2 center, Color color,
                             float rotation = 0f, float scale = 1f, float layerDepth = 0f)
        => Draw(texture, center, null, color, rotation,
                new Vector2(texture.Width / 2f, texture.Height / 2f),
                new Vector2(scale, scale), SpriteEffects.None, layerDepth);

    #endregion

    #region 文字

    public void DrawString(SpriteFont font, string text, Vector2 position, Color color)
        => font.Draw(this, text, position, color, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);

    public void DrawString(SpriteFont font, string text, Vector2 position, Color color,
                           float rotation, Vector2 origin, float scale, float layerDepth = 0f)
        => font.Draw(this, text, position, color, rotation, origin, scale, SpriteEffects.None, layerDepth);

    #endregion

    private void Flush()
    {
        if (_itemCount == 0) return;

        switch (_sortMode)
        {
            case SpriteSortMode.Texture:
                Array.Sort(_items, 0, _itemCount, Comparer<SpriteBatchItem>.Create(ByTexture));
                break;
            case SpriteSortMode.BackToFront:
                Array.Sort(_items, 0, _itemCount, Comparer<SpriteBatchItem>.Create(BackToFront));
                break;
            case SpriteSortMode.FrontToBack:
                Array.Sort(_items, 0, _itemCount, Comparer<SpriteBatchItem>.Create(FrontToBack));
                break;
        }

        _lastFlushCount = 0;
        _device.SetBlendState(_blendState);
        _device.Effect.Apply(_projection * _transform);

        GL.BindVertexArray(_device.VertexArray);
        GL.BindBuffer(GL.ARRAY_BUFFER, _device.VertexBuffer);

        int batchStart = 0;
        int currentKey = -1;

        for (int i = 0; i < _itemCount; i++)
        {
            int key = _items[i].Texture.BatchKey;
            if (key != currentKey)
            {
                if (i > batchStart) DrawRange(batchStart, i - batchStart);
                Texture2D texture = _items[i].Texture;
                _device.BindTexture(texture);
                _device.SetSamplerState(_samplerState, texture);
                batchStart = i;
                currentKey = key;
            }
        }
        DrawRange(batchStart, _itemCount - batchStart);

        ReportGlError("SpriteBatch.Flush");

        if (_flushesDiagnosed < 3)
        {
            _flushesDiagnosed++;
            Texture2D first = _items[batchStart].Texture;
            Console.WriteLine($"[SpriteBatch] 第 {_flushesDiagnosed} 次提交：{_lastFlushCount} 个精灵，底图 {first.TextureWidth}x{first.TextureHeight}");
        }

        _itemCount = 0;
    }

    private static int _flushesDiagnosed;

    /// <summary>记录本次 Flush 实际提交的精灵数（DrawRange 里累加）。</summary>
    private static int _lastFlushCount;

    private static bool _glErrorReported;

    /// <summary>只上报一次的 WebGL 错误，用来定位"画面全黑但没抛异常"这类问题。</summary>
    private static void ReportGlError(string stage)
    {
        if (_glErrorReported) return;
        int error = GL.GetError();
        if (error == 0) return;

        _glErrorReported = true;
        Console.Error.WriteLine($"[KFramework] WebGL 错误 0x{error:X4} @ {stage}");
    }

    private void DrawRange(int start, int count)
    {
        if (count <= 0) return;

        for (int i = 0; i < count; i++)
            BuildQuad(_items[start + i], _vertices.AsSpan(i * 4, 4));

        GL.BufferSubData(GL.ARRAY_BUFFER, 0, MemoryMarshal.AsBytes(_vertices.AsSpan(0, count * 4)));
        GL.DrawElements(GL.TRIANGLES, count * 6, GL.UNSIGNED_SHORT, 0);
        _lastFlushCount += count;
    }

    private static void BuildQuad(in SpriteBatchItem item, Span<VertexPositionColorTexture> destination)
    {
        Texture2D texture = item.Texture;

        float u0 = (texture.Bounds.X + item.Source.X) / (float)texture.TextureWidth;
        float v0 = (texture.Bounds.Y + item.Source.Y) / (float)texture.TextureHeight;
        float u1 = (texture.Bounds.X + item.Source.X + item.Source.Width) / (float)texture.TextureWidth;
        float v1 = (texture.Bounds.Y + item.Source.Y + item.Source.Height) / (float)texture.TextureHeight;

        if ((item.Effects & SpriteEffects.FlipHorizontally) != 0) (u0, u1) = (u1, u0);
        if ((item.Effects & SpriteEffects.FlipVertically) != 0) (v0, v1) = (v1, v0);

        float ox = item.Origin.X, oy = item.Origin.Y;
        float w = item.Size.X, h = item.Size.Y;

        Vector2 tl = new(-ox, -oy);
        Vector2 tr = new(w - ox, -oy);
        Vector2 br = new(w - ox, h - oy);
        Vector2 bl = new(-ox, h - oy);

        if (item.Rotation != 0f)
        {
            float c = MathF.Cos(item.Rotation), s = MathF.Sin(item.Rotation);
            tl = Rotate(tl, c, s); tr = Rotate(tr, c, s);
            br = Rotate(br, c, s); bl = Rotate(bl, c, s);
        }

        Vector2 p = item.Position;
        Color color = item.Color;

        destination[0] = new VertexPositionColorTexture(p + tl, new Vector2(u0, v0), color);
        destination[1] = new VertexPositionColorTexture(p + tr, new Vector2(u1, v0), color);
        destination[2] = new VertexPositionColorTexture(p + br, new Vector2(u1, v1), color);
        destination[3] = new VertexPositionColorTexture(p + bl, new Vector2(u0, v1), color);
    }

    private static Vector2 Rotate(Vector2 v, float cos, float sin)
        => new(v.X * cos - v.Y * sin, v.X * sin + v.Y * cos);
}
