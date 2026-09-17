using System.Runtime.InteropServices;
using System.Runtime.InteropServices.JavaScript;

using KFramework.MonoGame;

namespace KFramework.MonoGame
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
        private RasterizerState _rasterizerState = null!;
        private DepthStencilState _depthStencilState = null!;
        private ulong _sortingKeySource = 1;

        public Viewport Viewport { get; private set; }

        internal GraphicsMetrics _metrics;

        /// <summary>
        /// 渲染统计快照，照 MonoGame 的 GraphicsDevice.Metrics。
        /// 每帧由 Clear 重置一次（照 MonoGame 在 Present 里重置），跨所有 SpriteBatch 批次累计。
        /// </summary>
        public GraphicsMetrics Metrics { get { return _metrics; } set { _metrics = value; } }

        public int MaxTextureSize { get; }

        public string Renderer { get; }

        public BlendState BlendState => _blendState;
        public SamplerState SamplerState => _samplerState;

        /// <summary>光栅化状态（照 MonoGame 的 GraphicsDevice.RasterizerState）。setter 立即下发到 WebGL。</summary>
        public RasterizerState RasterizerState
        {
            get => _rasterizerState;
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(value));
                _rasterizerState = value;
                ApplyRasterizerState();
            }
        }

        /// <summary>深度/模板状态（照 MonoGame 的 GraphicsDevice.DepthStencilState）。setter 立即下发到 WebGL。</summary>
        public DepthStencilState DepthStencilState
        {
            get => _depthStencilState;
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(value));
                _depthStencilState = value;
                ApplyDepthStencilState();
            }
        }

        /// <summary>设备纹理槽（照 MonoGame 的 GraphicsDevice.Textures，本 2D 后端只用单元 0）。</summary>
        public TextureCollection Textures { get; } = new TextureCollection(1);

        /// <summary>设备采样器状态槽（照 MonoGame 的 GraphicsDevice.SamplerStates，本 2D 后端只用单元 0）。</summary>
        public SamplerStateCollection SamplerStates { get; } = new SamplerStateCollection(1);

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

            // 与 MonoGame 一致：正面 = 逆时针（CCW），供 3D 渲染使用。
            // 注意：本后端的正交投影会翻转 Y，2D 精灵四边形在窗口空间是顺时针绕序，
            // 若按 CCW 正面 + 背面剔除会把所有精灵判为背面而整批剔除（表现为“啥都不渲染”），
            // 因此 2D 精灵管线默认用 CullNone 关闭剔除（见 SpriteBatch）。
            JSBind_GL.FrontFace(JSBind_GL.CCW);

            _rasterizerState = RasterizerState.CullNone;
            ApplyRasterizerState();
            _depthStencilState = DepthStencilState.None;
            ApplyDepthStencilState();

            JSBind_GL.Enable(JSBind_GL.BLEND);
            JSBind_GL.BlendEquation(JSBind_GL.FUNC_ADD);
            SetBlendState(BlendState.NonPremultiplied);

            PrintTool.Log($"[KFramework.MonoGame] WebGL2 就绪 | {Renderer} | 画布 {Viewport.Width}x{Viewport.Height} | 最大纹理 {MaxTextureSize}");
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
                // 与官方 MonoGame 完全一致的三角剖分：两个三角形绕向相同（都为 +1）。
                // 三角1 = (TL, TR, BL)，三角2 = (TR, BR, BL)。
                // 注意：本仓库早期版本用过 (TL, BL, BR) 写第二个三角形，绕向与三角1相反；
                // 一旦外部代码（如 3D 渲染）开启了背面剔除而没关，绕向相反的那个三角形就会被剔掉，
                // 表现为“每个四边形缺一个三角”。这里改回与 MonoGame 一致，使两个三角形绕向相同。
                indices[o + 0] = (ushort)(v + 0);
                indices[o + 1] = (ushort)(v + 1);
                indices[o + 2] = (ushort)(v + 2);
                indices[o + 3] = (ushort)(v + 1);
                indices[o + 4] = (ushort)(v + 3);
                indices[o + 5] = (ushort)(v + 2);
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
            // 每帧清一次渲染统计，照 MonoGame 在 Present 里 _graphicsMetrics = new GraphicsMetrics()（跨所有 SpriteBatch 批次累计）。
            _metrics = new GraphicsMetrics();
            _metrics._clearCount++;
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

        /// <summary>
        /// 把当前 <see cref="RasterizerState"/> 下发给 WebGL。
        /// 本 2D 后端只用 CullCounterClockwiseFace（与 WebGL 默认 BACK 剔除一致），
        /// 故只切 Enable/Disable(CULL_FACE)，不调 glCullFace；CullNone 即关闭剔除。
        /// 每次 <see cref="SpriteBatch.Setup"/> 都会重新设置，因此绘制前状态始终被强制回 2D 设定，
        /// 不受外部 GL 状态（如 3D 渲染遗留的 CULL_FACE）影响。
        /// </summary>
        /// <summary>
        /// 把当前 <see cref="RasterizerState"/> 下发给 WebGL（照 MonoGame 的 RasterizerState.Apply）。
        /// CullMode.None 关闭剔除；否则开启 CULL_FACE 并按绕向选 FRONT/BACK，
        /// 配合构造函数里设定的 CCW 正面，CullCounterClockwiseFace 即“剔除逆时针背面”。
        /// 每次 <see cref="SpriteBatch.Setup"/> 都会重新设置，因此绘制前状态始终被强制回 2D 设定，
        /// 不受外部 GL 状态（如 3D 渲染遗留）影响。
        /// </summary>
        private void ApplyRasterizerState()
        {
            switch (_rasterizerState.CullMode)
            {
                case CullMode.None:
                    JSBind_GL.Disable(JSBind_GL.CULL_FACE);
                    break;
                case CullMode.CullClockwiseFace:
                    JSBind_GL.Enable(JSBind_GL.CULL_FACE);
                    JSBind_GL.CullFace(JSBind_GL.FRONT);
                    break;
                case CullMode.CullCounterClockwiseFace:
                    JSBind_GL.Enable(JSBind_GL.CULL_FACE);
                    JSBind_GL.CullFace(JSBind_GL.BACK);
                    break;
                default:
                    JSBind_GL.Enable(JSBind_GL.CULL_FACE);
                    JSBind_GL.CullFace(JSBind_GL.BACK);
                    break;
            }

            if (_rasterizerState.ScissorTestEnable)
                JSBind_GL.Enable(JSBind_GL.SCISSOR_TEST);
            else
                JSBind_GL.Disable(JSBind_GL.SCISSOR_TEST);
        }

        /// <summary>
        /// 把当前 <see cref="DepthStencilState"/> 下发给 WebGL。
        /// 深度测试关闭时 glDepthMask / glDepthFunc 无影响，故只切 Enable/Disable(DEPTH_TEST)；
        /// SpriteBatch 默认用 <see cref="DepthStencilState.None"/>（关闭深度测试）。
        /// 同样在每次 Setup 被强制下发，保证 2D 绘制不受外部状态干扰。
        /// </summary>
        /// <summary>
        /// 把当前 <see cref="DepthStencilState"/> 下发给 WebGL（照 MonoGame 的 DepthStencilState.Apply）。
        /// 切换 DEPTH_TEST 开关，并下发深度写入掩码与比较函数；SpriteBatch 默认用
        /// <see cref="DepthStencilState.None"/>（关闭深度测试）。
        /// 同样在每次 Setup 被强制下发，保证 2D 绘制不受外部状态干扰。
        /// </summary>
        private void ApplyDepthStencilState()
        {
            if (_depthStencilState.DepthBufferEnable)
                JSBind_GL.Enable(JSBind_GL.DEPTH_TEST);
            else
                JSBind_GL.Disable(JSBind_GL.DEPTH_TEST);

            JSBind_GL.DepthMask(_depthStencilState.DepthBufferWriteEnable);
            JSBind_GL.DepthFunc(ToGLDepthFunc(_depthStencilState.DepthBufferFunction));
        }

        private static int ToGLDepthFunc(CompareFunction func) => func switch
        {
            CompareFunction.Never => JSBind_GL.NEVER,
            CompareFunction.Less => JSBind_GL.LESS,
            CompareFunction.Equal => JSBind_GL.EQUAL,
            CompareFunction.LessEqual => JSBind_GL.LEQUAL,
            CompareFunction.Greater => JSBind_GL.GREATER,
            CompareFunction.NotEqual => JSBind_GL.NOTEQUAL,
            CompareFunction.GreaterEqual => JSBind_GL.GEQUAL,
            _ => JSBind_GL.ALWAYS,
        };

        internal void SetSamplerState(SamplerState state, Texture2D? current)
        {
            if (current is null) return;
            if (ReferenceEquals(_samplerState, state) && _samplerAppliedKey == current.SortingKey) return;
            _samplerState = state;
            _samplerAppliedKey = current.SortingKey;
            SamplerStates[0] = state;

            JSBind_GL.TexParameteri(JSBind_GL.TEXTURE_2D, JSBind_GL.TEXTURE_MIN_FILTER, state.MinFilter);
            JSBind_GL.TexParameteri(JSBind_GL.TEXTURE_2D, JSBind_GL.TEXTURE_MAG_FILTER, state.MagFilter);
            JSBind_GL.TexParameteri(JSBind_GL.TEXTURE_2D, JSBind_GL.TEXTURE_WRAP_S, state.WrapMode);
            JSBind_GL.TexParameteri(JSBind_GL.TEXTURE_2D, JSBind_GL.TEXTURE_WRAP_T, state.WrapMode);
        }

        private ulong _samplerAppliedKey;

        internal void BindTexture(Texture2D texture)
        {
            JSBind_GL.ActiveTexture(JSBind_GL.TEXTURE0);
            JSBind_GL.BindTexture(JSBind_GL.TEXTURE_2D, texture.Handle);
            Textures[0] = texture;
            _samplerAppliedKey = 0;   // 换纹理后采样参数需要重新下发
        }

        /// <summary>
        /// 把若干顶点上传到动态顶点缓冲并发起一次索引绘制（照 MonoGame 的 DrawUserIndexedPrimitives）。
        /// 索引来自初始化时建好的静态 ELEMENT_ARRAY_BUFFER（已绑进 VertexArray）。
        /// 每调用一次累加一次 DrawCount；PrimitiveCount 同步累加（每个四边形 = 2 三角形）。
        /// SpriteCount 由 SpriteBatcher.DrawBatch 整批累加一次，这里不再加。
        /// </summary>
        internal void DrawUserIndexedPrimitives(VertexPositionColorTexture[] vertices, int start, int end)
        {
            int vRun = end - start;
            if (vRun <= 0) return;

            JSBind_GL.BindVertexArray(VertexArray);
            JSBind_GL.BindBuffer(JSBind_GL.ARRAY_BUFFER, VertexBuffer);
            // 索引缓冲是[0, MaxBatchSize*4)的绝对下标：第 i 个四边形占 6 个索引，起始字节 i*6*2。
            // 因此把本批顶点（从 start 顶点起）上传到顶点缓冲的 start*SizeInBytes 处，
            // 并让 DrawElements 从 (start/4)*6*2 字节处读取索引，即可精确引用到本批顶点——
            // 多纹理切批后，后面的 run 不会再串到前一批的几何（否则会丢失/错位三角形）。
            JSBind_GL.BufferSubData(JSBind_GL.ARRAY_BUFFER, start * VertexPositionColorTexture.SizeInBytes,
                                    MemoryMarshal.AsBytes(vertices.AsSpan(start, vRun)));
            JSBind_GL.DrawElements(JSBind_GL.TRIANGLES, vRun / 4 * 6, JSBind_GL.UNSIGNED_SHORT, (start / 4) * 6 * 2);

            _metrics._drawCount++;
            _metrics._primitiveCount += vRun / 2;
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
            if (error != 0) Console.Error.WriteLine($"[KFramework.MonoGame] 纹理上传失败 0x{error:X4}（{width}x{height}，{rgba.Length} 字节）");

            var texture = new Texture2D(this, handle, width, height, ownsHandle: true);
            texture._sortingKey = _sortingKeySource++;
            return texture;
        }

        /// <summary>
        /// 用 GPU 压缩纹理字节（ASTC / BC7 / DXT 等）创建纹理：直接 compressedTexImage2D 上传到 GPU，不走 CPU 解码到 RGBA8。
        /// <paramref name="internalFormat"/> 传对应 GL 常量（如 COMPRESSED_RGBA_ASTC_4x4_KHR = 0x93B0、COMPRESSED_RGBA_BC7_EXT = 0x9093）。
        /// 压缩纹理为不可变 GPU 数据：不支持 SetData 局部更新，也不支持 generateMipmap；过滤固定为 LINEAR + CLAMP_TO_EDGE。
        /// </summary>
        public Texture2D CreateCompressedTexture(int width, int height, byte[] compressedData, int internalFormat)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
            ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
            if (width > MaxTextureSize || height > MaxTextureSize)
                throw new ArgumentOutOfRangeException(nameof(width), $"纹理尺寸超过上限 {MaxTextureSize}");
            ArgumentNullException.ThrowIfNull(compressedData);
            if (compressedData.Length == 0)
                throw new ArgumentException("压缩数据不能为空", nameof(compressedData));

            return CreateCompressedTextureInternal(width, height, compressedData, internalFormat);
        }

        /// <summary>
        /// <see cref="SurfaceFormat"/> 重载（对齐 MonoGame 写法）：用枚举表达格式而非裸 GL 常量。
        /// 会按格式校验 <paramref name="compressedData"/> 长度是否等于 宽高×压缩块大小（数据截断/越界会抛 <see cref="ArgumentException"/>）。
        /// </summary>
        public Texture2D CreateCompressedTexture(int width, int height, byte[] compressedData, SurfaceFormat format)
        {
            int expected = SurfaceFormatGL.GetExpectedCompressedBytes(format, width, height);
            if (expected > 0 && compressedData is not null && compressedData.Length != expected)
                throw new ArgumentException(
                    $"压缩数据长度 {compressedData?.Length ?? 0} 与格式 {format} 预期的 {expected} 字节不一致（宽高 {width}x{height}）。",
                    nameof(compressedData));

            return CreateCompressedTextureInternal(width, height, compressedData, (int)format);
        }

        private Texture2D CreateCompressedTextureInternal(int width, int height, byte[] compressedData, int internalFormat)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
            ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
            if (width > MaxTextureSize || height > MaxTextureSize)
                throw new ArgumentOutOfRangeException(nameof(width), $"纹理尺寸超过上限 {MaxTextureSize}");
            ArgumentNullException.ThrowIfNull(compressedData);
            if (compressedData.Length == 0)
                throw new ArgumentException("压缩数据不能为空", nameof(compressedData));

            JSObject handle = JSBind_GL.CreateTexture();
            JSBind_GL.BindTexture(JSBind_GL.TEXTURE_2D, handle);
            JSBind_GL.CompressedTexImage2D(JSBind_GL.TEXTURE_2D, 0, internalFormat, width, height, 0, compressedData);
            JSBind_GL.TexParameteri(JSBind_GL.TEXTURE_2D, JSBind_GL.TEXTURE_MIN_FILTER, JSBind_GL.LINEAR);
            JSBind_GL.TexParameteri(JSBind_GL.TEXTURE_2D, JSBind_GL.TEXTURE_MAG_FILTER, JSBind_GL.LINEAR);
            JSBind_GL.TexParameteri(JSBind_GL.TEXTURE_2D, JSBind_GL.TEXTURE_WRAP_S, JSBind_GL.CLAMP_TO_EDGE);
            JSBind_GL.TexParameteri(JSBind_GL.TEXTURE_2D, JSBind_GL.TEXTURE_WRAP_T, JSBind_GL.CLAMP_TO_EDGE);

            int error = JSBind_GL.GetError();
            if (error != 0) Console.Error.WriteLine($"[KFramework.MonoGame] 压缩纹理上传失败 0x{error:X4}（{width}x{height}，{compressedData.Length} 字节）");

            var texture = new Texture2D(this, handle, width, height, ownsHandle: true, isCompressed: true);
            texture._sortingKey = _sortingKeySource++;
            return texture;
        }

        public void Dispose()
        {
            JSBind_GL.DeleteBuffer(VertexBuffer);
            JSBind_GL.DeleteBuffer(IndexBuffer);
            Effect.Dispose();
        }
    }
}
