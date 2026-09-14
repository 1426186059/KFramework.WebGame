using System.Runtime.InteropServices.JavaScript;

using KFramework.JSBind;

namespace KFramework.Graphics
{

    /// <summary>
    /// 对 WebGL 2.0 上下文的封装：负责上下文创建、视口同步、状态切换与资源分配。
    /// 所有绘制都通过 <see cref="SpriteBatch"/> 完成。
    /// </summary>
    public sealed class GraphicsDevice : IDisposable
    {
        /// <summary>单批最大精灵数，决定顶点/索引缓冲大小。</summary>
        public const int MaxBatchSize = 4096;

        internal readonly SpriteEffect Effect;
        internal readonly JSObject VertexBuffer;
        internal readonly JSObject IndexBuffer;
        internal readonly JSObject VertexArray;

        // 初值必须为 null：SetBlendState 用引用相等做短路，若初值就等于目标值，
        // 首次调用会被跳过，glBlendFunc 永远不下发（表现为画面全黑）。
        private BlendState _blendState = null!;
        private SamplerState _samplerState = null!;
        private int _batchKeySource = 1;

        public Viewport Viewport { get; private set; }

        public int MaxTextureSize { get; }

        public string Renderer { get; }

        public BlendState BlendState => _blendState;
        public SamplerState SamplerState => _samplerState;

        public GraphicsDevice(string canvasSelector = "#game")
        {
            if (!JSBind_GL.InitContext(canvasSelector))
                throw new InvalidOperationException("无法创建 WebGL 2.0 上下文，请使用支持 WebGL2 的浏览器。");

            MaxTextureSize = JSBind_GL.GetParameterInt(JSBind_GL.MAX_TEXTURE_SIZE);
            Renderer = JSBind_GL.GetParameterString(JSBind_GL.RENDERER);

            Effect = new SpriteEffect();

            VertexBuffer = JSBind_GL.CreateBuffer();
            IndexBuffer = JSBind_GL.CreateBuffer();
            VertexArray = JSBind_GL.CreateVertexArray();

            JSBind_GL.BindVertexArray(VertexArray);

            JSBind_GL.BindBuffer(JSBind_GL.ARRAY_BUFFER, VertexBuffer);
            JSBind_GL.BufferDataSize(JSBind_GL.ARRAY_BUFFER, MaxBatchSize * 4 * VertexPositionColorTexture.SizeInBytes, JSBind_GL.DYNAMIC_DRAW);

            JSBind_GL.BindBuffer(JSBind_GL.ELEMENT_ARRAY_BUFFER, IndexBuffer);
            JSBind_GL.BufferData(JSBind_GL.ELEMENT_ARRAY_BUFFER, BuildQuadIndices(MaxBatchSize), JSBind_GL.STATIC_DRAW);

            ConfigureAttributes();

            SyncCanvasSize();

            JSBind_GL.Disable(JSBind_GL.DEPTH_TEST);
            JSBind_GL.Enable(JSBind_GL.BLEND);
            JSBind_GL.BlendEquation(JSBind_GL.FUNC_ADD);
            SetBlendState(BlendState.NonPremultiplied);

            Console.WriteLine($"[KFramework] WebGL2 就绪 | {Renderer} | 画布 {Viewport.Width}x{Viewport.Height} | 最大纹理 {MaxTextureSize}");
        }

        private void ConfigureAttributes()
        {
            int stride = VertexPositionColorTexture.SizeInBytes;
            if (Effect.PositionLocation >= 0)
            {
                JSBind_GL.EnableVertexAttribArray(Effect.PositionLocation);
                JSBind_GL.VertexAttribPointer(Effect.PositionLocation, 2, JSBind_GL.FLOAT, false, stride, 0);
            }
            if (Effect.TexCoordLocation >= 0)
            {
                JSBind_GL.EnableVertexAttribArray(Effect.TexCoordLocation);
                JSBind_GL.VertexAttribPointer(Effect.TexCoordLocation, 2, JSBind_GL.FLOAT, false, stride, 8);
            }
            if (Effect.ColorLocation >= 0)
            {
                JSBind_GL.EnableVertexAttribArray(Effect.ColorLocation);
                JSBind_GL.VertexAttribPointer(Effect.ColorLocation, 4, JSBind_GL.UNSIGNED_BYTE, true, stride, 16);
            }
        }

        private static byte[] BuildQuadIndices(int spriteCount)
        {
            // 每个精灵 2 个三角形：0,1,2 / 0,2,3（顶点顺序 TL,TR,BR,BL）
            byte[] data = new byte[spriteCount * 6 * sizeof(ushort)];
            Span<ushort> indices = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, ushort>(data.AsSpan());
            for (int i = 0; i < spriteCount; i++)
            {
                int v = i * 4;
                int o = i * 6;
                indices[o + 0] = (ushort)(v + 0);
                indices[o + 1] = (ushort)(v + 1);
                indices[o + 2] = (ushort)(v + 2);
                indices[o + 3] = (ushort)(v + 0);
                indices[o + 4] = (ushort)(v + 2);
                indices[o + 5] = (ushort)(v + 3);
            }
            return data;
        }

        /// <summary>把视口同步为画布当前的绘制缓冲尺寸，返回是否发生了变化。</summary>
        public bool SyncCanvasSize()
        {
            Span<int> size = stackalloc int[5];
            JSBind_Platform.GetCanvasSize(size);
            int width = size[2];
            int height = size[3];
            if (width <= 0 || height <= 0) return false;
            if (width == Viewport.Width && height == Viewport.Height) return false;

            Viewport = new Viewport(0, 0, width, height);
            JSBind_GL.Viewport(0, 0, width, height);
            return true;
        }

        /// <summary>CSS 像素尺寸（不含设备像素比）。</summary>
        public Vector2 CssSize
        {
            get
            {
                Span<int> size = stackalloc int[5];
                JSBind_Platform.GetCanvasSize(size);
                return new Vector2(size[0], size[1]);
            }
        }

        public float DevicePixelRatio
        {
            get
            {
                Span<int> size = stackalloc int[5];
                JSBind_Platform.GetCanvasSize(size);
                return size[4] / 1000f;
            }
        }

        /// <summary>
        /// 读取画布上的一个像素。坐标以**左上角为原点**（与精灵坐标系一致），
        /// 内部会自动换算成 WebGL 的左下原点。用于截图式自检。
        /// </summary>
        public Color ReadPixel(int x, int y)
        {
            Span<byte> rgba = stackalloc byte[4];
            JSBind_GL.ReadPixel(x, Viewport.Height - 1 - y, rgba);
            return new Color(rgba[0], rgba[1], rgba[2], rgba[3]);
        }

        public void Clear(Color color)
        {
            JSBind_GL.ClearColor(color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f);
            JSBind_GL.Clear(JSBind_GL.COLOR_BUFFER_BIT | JSBind_GL.DEPTH_BUFFER_BIT);
        }

        internal void SetBlendState(BlendState state)
        {
            if (ReferenceEquals(_blendState, state)) return;
            _blendState = state;
            JSBind_GL.Enable(JSBind_GL.BLEND);
            JSBind_GL.BlendFuncSeparate(state.SourceBlend, state.DestinationBlend,
                                 state.SourceAlphaBlend, state.DestinationAlphaBlend);
        }

        internal void SetSamplerState(SamplerState state, Texture2D? current)
        {
            if (current is null) return;
            if (ReferenceEquals(_samplerState, state) && _samplerAppliedKey == current.BatchKey) return;
            _samplerState = state;
            _samplerAppliedKey = current.BatchKey;
            JSBind_GL.TexParameteri(JSBind_GL.TEXTURE_2D, JSBind_GL.TEXTURE_MIN_FILTER, state.MinFilter);
            JSBind_GL.TexParameteri(JSBind_GL.TEXTURE_2D, JSBind_GL.TEXTURE_MAG_FILTER, state.MagFilter);
            JSBind_GL.TexParameteri(JSBind_GL.TEXTURE_2D, JSBind_GL.TEXTURE_WRAP_S, state.WrapMode);
            JSBind_GL.TexParameteri(JSBind_GL.TEXTURE_2D, JSBind_GL.TEXTURE_WRAP_T, state.WrapMode);
        }

        private int _samplerAppliedKey = -1;

        internal void BindTexture(Texture2D texture)
        {
            JSBind_GL.ActiveTexture(JSBind_GL.TEXTURE0);
            JSBind_GL.BindTexture(JSBind_GL.TEXTURE_2D, texture.Handle);
            _samplerAppliedKey = -1;   // 换纹理后采样参数需要重新下发
        }

        /// <summary>创建一张空的 RGBA8 纹理。</summary>
        public Texture2D CreateTexture(int width, int height)
            => CreateTexture(width, height, new byte[width * height * 4]);

        /// <summary>用 RGBA8 像素数据创建纹理。</summary>
        public Texture2D CreateTexture(int width, int height, byte[] rgba)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
            ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
            if (width > MaxTextureSize || height > MaxTextureSize)
                throw new ArgumentOutOfRangeException(nameof(width), $"纹理尺寸超过上限 {MaxTextureSize}");

            JSObject handle = JSBind_GL.CreateTexture();
            JSBind_GL.BindTexture(JSBind_GL.TEXTURE_2D, handle);
            JSBind_GL.PixelStorei(JSBind_GL.UNPACK_ALIGNMENT, 1);
            JSBind_GL.TexImage2D(JSBind_GL.TEXTURE_2D, 0, JSBind_GL.RGBA8, width, height, 0, JSBind_GL.RGBA, JSBind_GL.UNSIGNED_BYTE, rgba);
            JSBind_GL.TexParameteri(JSBind_GL.TEXTURE_2D, JSBind_GL.TEXTURE_MIN_FILTER, JSBind_GL.NEAREST);
            JSBind_GL.TexParameteri(JSBind_GL.TEXTURE_2D, JSBind_GL.TEXTURE_MAG_FILTER, JSBind_GL.NEAREST);
            JSBind_GL.TexParameteri(JSBind_GL.TEXTURE_2D, JSBind_GL.TEXTURE_WRAP_S, JSBind_GL.CLAMP_TO_EDGE);
            JSBind_GL.TexParameteri(JSBind_GL.TEXTURE_2D, JSBind_GL.TEXTURE_WRAP_T, JSBind_GL.CLAMP_TO_EDGE);

            int error = JSBind_GL.GetError();
            if (error != 0) Console.Error.WriteLine($"[KFramework] 纹理上传失败 0x{error:X4}（{width}x{height}，{rgba.Length} 字节）");

            return new Texture2D(handle, width, height, _batchKeySource++, ownsHandle: true);
        }

        public void Dispose()
        {
            JSBind_GL.DeleteBuffer(VertexBuffer);
            JSBind_GL.DeleteBuffer(IndexBuffer);
            Effect.Dispose();
        }
    }
}
