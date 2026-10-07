using System.Runtime.InteropServices;
using System.Runtime.InteropServices.JavaScript;

namespace KFramework.MonoGame
{
    internal sealed class WebGl20Backend : IGraphicsBackend
    {
        /// <summary>FBO 缓存：一组渲染目标绑定组合对应一个 FBO（照 MonoGame 的 glFramebuffers）。</summary>
        private readonly Dictionary<RenderTargetBinding[], JSObject> _glFramebuffers =
            new Dictionary<RenderTargetBinding[], JSObject>(new RenderTargetBindingArrayComparer());

        private SpriteEffect _effect = null!;
        private JSObject _vertexBuffer = default!;
        private JSObject _indexBuffer = default!;
        private JSObject _vertexArray = default!;

        public int MaxTextureSize { get; private set; }

        public string Renderer { get; private set; } = string.Empty;

        public string Name => "WebGL2";

    /// <summary>WebGL 的 FBO 与纹理原点在左下，离屏渲染需要改用 Y 向上投影来抵消（详见接口注释）。</summary>
    public bool NeedsOffscreenYFlip => true;

        /// <summary>WebGL 后端无显式收帧动作：画面由浏览器在 rAF 回调结束时自动合成。</summary>
        public void EndFrame() { }

        public Task InitializeAsync(bool antialias)
        {
            // 照 MonoGame：MSAA 属性在上下文创建之前设置
            JSBind_WEBGL20.SetAntialias(antialias);

            if (!JSBind_WEBGL20.InitContext())
                throw new InvalidOperationException("无法创建 WebGL 2.0 上下文，请使用支持 WebGL2 的浏览器。");

            MaxTextureSize = JSBind_WEBGL20.GetParameterInt(JSBind_WEBGL20.MAX_TEXTURE_SIZE);
            Renderer = JSBind_WEBGL20.GetParameterString(JSBind_WEBGL20.RENDERER);

            _effect = new SpriteEffect();

            _vertexBuffer = JSBind_WEBGL20.CreateBuffer();
            _indexBuffer = JSBind_WEBGL20.CreateBuffer();
            _vertexArray = JSBind_WEBGL20.CreateVertexArray();

            JSBind_WEBGL20.BindVertexArray(_vertexArray);

            JSBind_WEBGL20.BindBuffer(JSBind_WEBGL20.ARRAY_BUFFER, _vertexBuffer);
            JSBind_WEBGL20.BufferDataSize(JSBind_WEBGL20.ARRAY_BUFFER, GraphicsDevice.MaxBatchSize * 4 * VertexPositionColorTexture.SizeInBytes, JSBind_WEBGL20.DYNAMIC_DRAW);

            JSBind_WEBGL20.BindBuffer(JSBind_WEBGL20.ELEMENT_ARRAY_BUFFER, _indexBuffer);
            JSBind_WEBGL20.BufferData(JSBind_WEBGL20.ELEMENT_ARRAY_BUFFER, BuildQuadIndices(GraphicsDevice.MaxBatchSize), JSBind_WEBGL20.STATIC_DRAW);

            ConfigureAttributes();

            // 与 MonoGame 一致：正面 = 逆时针（CCW），供 3D 渲染使用。
            // 注意：本后端的正交投影会翻转 Y，2D 精灵四边形在窗口空间是顺时针绕序，
            // 若按 CCW 正面 + 背面剔除会把所有精灵判为背面而整批剔除（表现为“啥都不渲染”），
            // 因此 2D 精灵管线默认用 CullNone 关闭剔除（见 SpriteBatch）。
            JSBind_WEBGL20.FrontFace(JSBind_WEBGL20.CCW);

            // WebGL 初始化是同步的，故返回已完成的 Task（与 WebGPU 的异步初始化统一签名）。
            return Task.CompletedTask;
        }

        public IShaderProgram CreateShaderProgram() => _effect;

        public IShaderProgram CreateCustomShaderProgram(string vertexSource, string fragmentSource)
            => new CustomWebGlSpriteProgram(fragmentSource);

        /// <summary>WebGL2 原生支持 GPU 实例化（drawElementsInstanced + vertexAttribDivisor）。</summary>
        public bool SupportsInstancing => true;

        public ISpriteInstancer CreateInstancer(string? fragmentSource, int capacity)
            => new WebGlInstancedSpriteProgram(fragmentSource, capacity);

        private void ConfigureAttributes()
        {
            // 槽位顺序 = 顶点着色器的声明顺序（位置 → 颜色 → UV），
            // 与 VertexPositionColorTexture 的 28 字节布局（0 / 16 / 20）严格对应。
            // 三个顶点源（SpriteEffect / CustomWebGlSpriteProgram / ShaderEffect.DefaultVertexSource）必须同序声明：
            // 属性位置由链接期分配，顺序不一致就会拿到与这里配置的槽位不同的编号。
            int stride = VertexPositionColorTexture.SizeInBytes;
            if (_effect.PositionLocation >= 0)
            {
                JSBind_WEBGL20.EnableVertexAttribArray(_effect.PositionLocation);
                JSBind_WEBGL20.VertexAttribPointer(_effect.PositionLocation, 4, JSBind_WEBGL20.FLOAT, false, stride, 0);
            }
            if (_effect.ColorLocation >= 0)
            {
                JSBind_WEBGL20.EnableVertexAttribArray(_effect.ColorLocation);
                JSBind_WEBGL20.VertexAttribPointer(_effect.ColorLocation, 4, JSBind_WEBGL20.UNSIGNED_BYTE, true, stride, 16);
            }
            if (_effect.TexCoordLocation >= 0)
            {
                JSBind_WEBGL20.EnableVertexAttribArray(_effect.TexCoordLocation);
                JSBind_WEBGL20.VertexAttribPointer(_effect.TexCoordLocation, 2, JSBind_WEBGL20.FLOAT, false, stride, 20);
            }
        }

        private static byte[] BuildQuadIndices(int spriteCount)
        {
            // 每个精灵 2 个三角形：0,1,2 / 0,2,3（顶点顺序 TL,TR,BR,BL）
            byte[] data = new byte[spriteCount * 6 * sizeof(ushort)];
            Span<ushort> indices = MemoryMarshal.Cast<byte, ushort>(data.AsSpan());
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

        public int GetError() => JSBind_WEBGL20.GetError();

        public void SetViewport(int x, int y, int width, int height)
            => JSBind_WEBGL20.Viewport(x, y, width, height);

        public void SetScissor(int x, int y, int width, int height)
            => JSBind_WEBGL20.Scissor(x, y, width, height);

        public void Clear(Color color)
        {
            JSBind_WEBGL20.ClearColor(color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f);
            // 模板一并清：上下文已申请了模板缓冲（stencil:true），不清会留着上一帧的残值，
            // 一旦有页开启模板测试就会读到脏数据。
            JSBind_WEBGL20.Clear(JSBind_WEBGL20.COLOR_BUFFER_BIT | JSBind_WEBGL20.DEPTH_BUFFER_BIT | JSBind_WEBGL20.STENCIL_BUFFER_BIT);
        }

        /// <summary>读像素。WebGL 的帧缓冲原点在左下，故这里做 Y 换算（上层按左上原点传入）。</summary>
        public void ReadPixel(int x, int y, int viewportHeight, Span<byte> rgba)
            => JSBind_WEBGL20.ReadPixel(x, viewportHeight - 1 - y, rgba);

        public Task ReadPixels(int x, int y, int width, int height, byte[] rgba)
        {
            JSBind_WEBGL20.ReadPixels(x, y, width, height, rgba);
            return Task.CompletedTask;
        }

        public void SetBlendState(BlendState state)
        {
            if (state.Enabled)
            {
                // BLEND 开：下发因子 + 混合方程（RGB 与 Alpha 各一条方程）。
                JSBind_WEBGL20.Enable(JSBind_WEBGL20.BLEND);
                JSBind_WEBGL20.BlendFuncSeparate(
                    ToGLBlendMode(state.SourceColorBlendFactor),
                    ToGLBlendMode(state.DestinationColorBlendFactor),
                    ToGLBlendMode(state.SourceAlphaBlendFactor),
                    ToGLBlendMode(state.DestinationAlphaBlendFactor));
                JSBind_WEBGL20.BlendEquationSeparate(ToGLBlendOp(state.ColorBlendOperation),
                                            ToGLBlendOp(state.AlphaBlendOperation));
            }
            else
            {
                // Enabled=false（如 Opaque）：彻底关闭混合单元，整像素直接覆盖，省去一次混合开销。
                JSBind_WEBGL20.Disable(JSBind_WEBGL20.BLEND);
            }
        }

        /// <summary>中立混合因子 → GL 常量（GL 常量只应出现在后端里，公共状态类保持后端无关）。</summary>
        private static int ToGLBlendMode(BlendMode mode) => mode switch
        {
            BlendMode.Zero => JSBind_WEBGL20.ZERO,
            BlendMode.One => JSBind_WEBGL20.ONE,
            BlendMode.DstColor => JSBind_WEBGL20.DST_COLOR,
            BlendMode.SrcColor => JSBind_WEBGL20.SRC_COLOR,
            BlendMode.OneMinusDstColor => JSBind_WEBGL20.ONE_MINUS_DST_COLOR,
            BlendMode.SrcAlpha => JSBind_WEBGL20.SRC_ALPHA,
            BlendMode.OneMinusSrcColor => JSBind_WEBGL20.ONE_MINUS_SRC_COLOR,
            BlendMode.DstAlpha => JSBind_WEBGL20.DST_ALPHA,
            BlendMode.OneMinusDstAlpha => JSBind_WEBGL20.ONE_MINUS_DST_ALPHA,
            BlendMode.SrcAlphaSaturate => JSBind_WEBGL20.SRC_ALPHA_SATURATE,
            BlendMode.OneMinusSrcAlpha => JSBind_WEBGL20.ONE_MINUS_SRC_ALPHA,
            _ => JSBind_WEBGL20.ONE,
        };

        /// <summary>中立混合运算 → GL 混合方程常量。</summary>
        private static int ToGLBlendOp(BlendOp op) => op switch
        {
            BlendOp.Sub => JSBind_WEBGL20.BLEND_FUNC_SUBTRACT,
            BlendOp.RevSub => JSBind_WEBGL20.BLEND_FUNC_REVERSE_SUBTRACT,
            BlendOp.Min => JSBind_WEBGL20.BLEND_FUNC_MIN,
            BlendOp.Max => JSBind_WEBGL20.BLEND_FUNC_MAX,
            _ => JSBind_WEBGL20.BLEND_FUNC_ADD,
        };

        /// <summary>
        /// 把光栅化状态下发给 WebGL。
        /// <see cref="CullMode.Off"/> 关闭剔除；Front / Back 开启 CULL_FACE 后直接对应
        /// GL 的 FRONT / BACK —— 因为初始化时已固定 CCW 为正面，
        /// “面朝向”与 GL 的剔除面正好同名同义，无需再按绕序换算。
        /// </summary>
        public void ApplyRasterizerState(RasterizerState state)
        {
            switch (state.CullMode)
            {
                case CullMode.Off:
                    JSBind_WEBGL20.Disable(JSBind_WEBGL20.CULL_FACE);
                    break;
                case CullMode.Front:
                    JSBind_WEBGL20.Enable(JSBind_WEBGL20.CULL_FACE);
                    JSBind_WEBGL20.CullFace(JSBind_WEBGL20.FRONT);
                    break;
                case CullMode.Back:
                    JSBind_WEBGL20.Enable(JSBind_WEBGL20.CULL_FACE);
                    JSBind_WEBGL20.CullFace(JSBind_WEBGL20.BACK);
                    break;
                default:
                    JSBind_WEBGL20.Enable(JSBind_WEBGL20.CULL_FACE);
                    JSBind_WEBGL20.CullFace(JSBind_WEBGL20.BACK);
                    break;
            }

            if (state.ScissorTestEnable)
                JSBind_WEBGL20.Enable(JSBind_WEBGL20.SCISSOR_TEST);
            else
                JSBind_WEBGL20.Disable(JSBind_WEBGL20.SCISSOR_TEST);
        }

        /// <summary>
        /// 把深度 / 模板状态下发给 WebGL（照 MonoGame 的 DepthStencilState.Apply）。
        /// 切换 DEPTH_TEST 开关，并下发深度写入掩码与比较函数。
        /// </summary>
        public void ApplyDepthStencilState(DepthStencilState state)
        {
            ApplyDepth(state.Depth);
            ApplyStencil(state.Stencil);
        }

        /// <summary>
        /// 深度部分。Unity 语义：<see cref="CompareFunction.Disabled"/> 即关闭深度测试，
        /// 不再单独看一个 bool 开关。
        /// </summary>
        private void ApplyDepth(DepthState depth)
        {
            if (depth.DepthCompare != CompareFunction.Disabled)
                JSBind_WEBGL20.Enable(JSBind_WEBGL20.DEPTH_TEST);
            else
                JSBind_WEBGL20.Disable(JSBind_WEBGL20.DEPTH_TEST);

            JSBind_WEBGL20.DepthMask(depth.DepthWrite);
            JSBind_WEBGL20.DepthFunc(ToGLCompareFunction(depth.DepthCompare));
        }

        /// <summary>
        /// 模板部分。正反面各下发一组「比较函数 + 三种结果的操作」；
        /// 关闭时直接 disable(STENCIL_TEST)，不碰其余模板状态。
        /// </summary>
        private void ApplyStencil(StencilState stencil)
        {
            if (!stencil.Enabled)
            {
                JSBind_WEBGL20.Disable(JSBind_WEBGL20.STENCIL_TEST);
                return;
            }

            JSBind_WEBGL20.Enable(JSBind_WEBGL20.STENCIL_TEST);
            JSBind_WEBGL20.StencilMask(stencil.WriteMask);

            JSBind_WEBGL20.StencilFuncSeparate(JSBind_WEBGL20.FRONT,
                ToGLCompareFunction(stencil.CompareFunctionFront), stencil.Reference, stencil.ReadMask);
            JSBind_WEBGL20.StencilOpSeparate(JSBind_WEBGL20.FRONT,
                ToGLStencilOp(stencil.FailOperationFront),
                ToGLStencilOp(stencil.ZFailOperationFront),
                ToGLStencilOp(stencil.PassOperationFront));

            JSBind_WEBGL20.StencilFuncSeparate(JSBind_WEBGL20.BACK,
                ToGLCompareFunction(stencil.CompareFunctionBack), stencil.Reference, stencil.ReadMask);
            JSBind_WEBGL20.StencilOpSeparate(JSBind_WEBGL20.BACK,
                ToGLStencilOp(stencil.FailOperationBack),
                ToGLStencilOp(stencil.ZFailOperationBack),
                ToGLStencilOp(stencil.PassOperationBack));
        }

        /// <summary>中立模板操作 → GL 常量。</summary>
        private static int ToGLStencilOp(StencilOp op) => op switch
        {
            StencilOp.Zero => JSBind_WEBGL20.ZERO,
            StencilOp.Replace => JSBind_WEBGL20.STENCIL_REPLACE,
            StencilOp.IncrementSaturate => JSBind_WEBGL20.STENCIL_INCR,
            StencilOp.DecrementSaturate => JSBind_WEBGL20.STENCIL_DECR,
            StencilOp.Invert => JSBind_WEBGL20.STENCIL_INVERT,
            StencilOp.IncrementWrap => JSBind_WEBGL20.STENCIL_INCR_WRAP,
            StencilOp.DecrementWrap => JSBind_WEBGL20.STENCIL_DECR_WRAP,
            _ => JSBind_WEBGL20.STENCIL_KEEP,
        };

        private static int ToGLCompareFunction(CompareFunction func) => func switch
        {
            CompareFunction.Never => JSBind_WEBGL20.NEVER,
            CompareFunction.Less => JSBind_WEBGL20.LESS,
            CompareFunction.Equal => JSBind_WEBGL20.EQUAL,
            CompareFunction.LessEqual => JSBind_WEBGL20.LEQUAL,
            CompareFunction.Greater => JSBind_WEBGL20.GREATER,
            CompareFunction.NotEqual => JSBind_WEBGL20.NOTEQUAL,
            CompareFunction.GreaterEqual => JSBind_WEBGL20.GEQUAL,
            _ => JSBind_WEBGL20.ALWAYS,     // Always 与 Disabled
        };

        public void SetSamplerState(SamplerState state)
        {
            int minFilter = ToGLFilter(state.MinFilter);
            int magFilter = ToGLFilter(state.MagFilter);
            int wrap = ToGLAddressMode(state.WrapMode);

            //纹理缩小过滤（纹理比屏幕像素大时怎么采样）
            JSBind_WEBGL20.TexParameteri(JSBind_WEBGL20.TEXTURE_2D, JSBind_WEBGL20.TEXTURE_MIN_FILTER, minFilter);
            //纹理放大过滤（纹理比屏幕像素小时怎么采样）
            JSBind_WEBGL20.TexParameteri(JSBind_WEBGL20.TEXTURE_2D, JSBind_WEBGL20.TEXTURE_MAG_FILTER, magFilter);
            //S 方向（U / 水平）环绕方式
            JSBind_WEBGL20.TexParameteri(JSBind_WEBGL20.TEXTURE_2D, JSBind_WEBGL20.TEXTURE_WRAP_S, wrap);
            //T 方向（V / 垂直）环绕方式
            JSBind_WEBGL20.TexParameteri(JSBind_WEBGL20.TEXTURE_2D, JSBind_WEBGL20.TEXTURE_WRAP_T, wrap);
        }

        // 供材质纹理绑定复用（CustomWebGlSpriteProgram 把材质纹理绑到 1 号单元时也要下发同样的 filter/wrap）。
        internal static int ToGLFilter(TextureFilter filter)
            => filter == TextureFilter.Point ? JSBind_WEBGL20.NEAREST : JSBind_WEBGL20.LINEAR;

        internal static int ToGLAddressMode(TextureAddressMode mode) => mode switch
        {
            TextureAddressMode.Wrap => JSBind_WEBGL20.REPEAT,
            TextureAddressMode.Mirror => JSBind_WEBGL20.MIRRORED_REPEAT,
            _ => JSBind_WEBGL20.CLAMP_TO_EDGE,
        };

        public void BindTexture(Texture2D texture)
        {
            JSBind_WEBGL20.ActiveTexture(JSBind_WEBGL20.TEXTURE0);
            JSBind_WEBGL20.BindTexture(JSBind_WEBGL20.TEXTURE_2D, texture.Handle);
        }

        /// <summary>
        /// 把若干顶点上传到动态顶点缓冲并发起一次索引绘制（照 MonoGame 的 DrawUserIndexedPrimitives）。
        /// 索引来自初始化时建好的静态 ELEMENT_ARRAY_BUFFER（已绑进 VertexArray）。
        /// </summary>
        public void DrawUserIndexedPrimitives(VertexPositionColorTexture[] vertices, int start, int end)
        {
            int vRun = end - start;
            if (vRun <= 0) return;

            JSBind_WEBGL20.BindVertexArray(_vertexArray);
            JSBind_WEBGL20.BindBuffer(JSBind_WEBGL20.ARRAY_BUFFER, _vertexBuffer);
            // 索引缓冲是[0, MaxBatchSize*4)的绝对下标：第 i 个四边形占 6 个索引，起始字节 i*6*2。
            // 因此把本批顶点（从 start 顶点起）上传到顶点缓冲的 start*SizeInBytes 处，
            // 并让 DrawElements 从 (start/4)*6*2 字节处读取索引，即可精确引用到本批顶点——
            // 多纹理切批后，后面的 run 不会再串到前一批的几何（否则会丢失/错位三角形）。
            JSBind_WEBGL20.BufferSubData(JSBind_WEBGL20.ARRAY_BUFFER, start * VertexPositionColorTexture.SizeInBytes,
                                    MemoryMarshal.AsBytes(vertices.AsSpan(start, vRun)));
            JSBind_WEBGL20.DrawElements(JSBind_WEBGL20.TRIANGLES, vRun / 4 * 6, JSBind_WEBGL20.UNSIGNED_SHORT, (start / 4) * 6 * 2);
        }

        // ============ 纹理资源（原 Texture2D.Web.cs 的平台层） ============

        public void CreateTexture(Texture2D texture, int width, int height, bool mipmap, SurfaceFormat format, Texture2D.SurfaceType type)
        {
            texture.Handle = JSBind_WEBGL20.CreateTexture();
            JSBind_WEBGL20.BindTexture(JSBind_WEBGL20.TEXTURE_2D, texture.Handle);
            JSBind_WEBGL20.TexParameteri(JSBind_WEBGL20.TEXTURE_2D, JSBind_WEBGL20.TEXTURE_WRAP_S, JSBind_WEBGL20.CLAMP_TO_EDGE);
            JSBind_WEBGL20.TexParameteri(JSBind_WEBGL20.TEXTURE_2D, JSBind_WEBGL20.TEXTURE_WRAP_T, JSBind_WEBGL20.CLAMP_TO_EDGE);

            // 压缩纹理为不可变 GPU 数据，过滤固定为 LINEAR；未压缩用 NEAREST（照原 CreateTexture 行为）。
            int filter = format.IsCompressed() ? JSBind_WEBGL20.LINEAR : JSBind_WEBGL20.NEAREST;
            JSBind_WEBGL20.TexParameteri(JSBind_WEBGL20.TEXTURE_2D, JSBind_WEBGL20.TEXTURE_MIN_FILTER, filter);
            JSBind_WEBGL20.TexParameteri(JSBind_WEBGL20.TEXTURE_2D, JSBind_WEBGL20.TEXTURE_MAG_FILTER, filter);

            // 渲染目标（照 MonoGame 的 RenderTarget2D.PlatformConstruct）：内容由 GPU 绘制产生，
            // 这里只把存储分配出来（传 null 像素），否则 FBO 挂的是一张没有存储的不完整纹理。
            if (type == Texture2D.SurfaceType.RenderTarget)
            {
                if (format.IsCompressed())
                    throw new ArgumentException("渲染目标不支持压缩格式。", nameof(format));

                JSBind_WEBGL20.TexImage2DStorage(JSBind_WEBGL20.TEXTURE_2D, 0, JSBind_WEBGL20.RGBA8,
                    width, height, JSBind_WEBGL20.RGBA, JSBind_WEBGL20.UNSIGNED_BYTE);
            }
        }

        public void SetTextureData(Texture2D texture, int level, byte[] bytes)
        {
            int levelWidth = Math.Max(texture.Width >> level, 1);
            int levelHeight = Math.Max(texture.Height >> level, 1);

            JSBind_WEBGL20.BindTexture(JSBind_WEBGL20.TEXTURE_2D, texture.Handle);
            JSBind_WEBGL20.PixelStorei(JSBind_WEBGL20.UNPACK_ALIGNMENT, 1);

            if (texture.Format.IsCompressed())
                JSBind_WEBGL20.CompressedTexImage2D(JSBind_WEBGL20.TEXTURE_2D, level, SurfaceFormatGL.ToInternalFormat(texture.Format), levelWidth, levelHeight, 0, bytes.AsSpan());
            else
                JSBind_WEBGL20.TexImage2D(JSBind_WEBGL20.TEXTURE_2D, level, JSBind_WEBGL20.RGBA8, levelWidth, levelHeight, 0, JSBind_WEBGL20.RGBA, JSBind_WEBGL20.UNSIGNED_BYTE, bytes.AsSpan());
        }

        public void SetTextureData(Texture2D texture, int level, Rectangle rect, byte[] bytes)
        {
            int levelWidth = Math.Max(texture.Width >> level, 1);
            int levelHeight = Math.Max(texture.Height >> level, 1);

            JSBind_WEBGL20.BindTexture(JSBind_WEBGL20.TEXTURE_2D, texture.Handle);
            JSBind_WEBGL20.PixelStorei(JSBind_WEBGL20.UNPACK_ALIGNMENT, 1);

            if (texture.Format.IsCompressed())
            {
                if (rect.X != 0 || rect.Y != 0 || rect.Width != levelWidth || rect.Height != levelHeight)
                    throw new InvalidOperationException("压缩纹理为不可变 GPU 数据，不支持 SetData 局部更新；如需更新请整张重建。");
                JSBind_WEBGL20.CompressedTexImage2D(JSBind_WEBGL20.TEXTURE_2D, level, SurfaceFormatGL.ToInternalFormat(texture.Format), levelWidth, levelHeight, 0, bytes.AsSpan());
            }
            else if (rect.X == 0 && rect.Y == 0 && rect.Width == levelWidth && rect.Height == levelHeight)
                JSBind_WEBGL20.TexImage2D(JSBind_WEBGL20.TEXTURE_2D, level, JSBind_WEBGL20.RGBA8, levelWidth, levelHeight, 0, JSBind_WEBGL20.RGBA, JSBind_WEBGL20.UNSIGNED_BYTE, bytes.AsSpan());
            else
                JSBind_WEBGL20.TexSubImage2D(JSBind_WEBGL20.TEXTURE_2D, level, rect.X, rect.Y, rect.Width, rect.Height, JSBind_WEBGL20.RGBA, JSBind_WEBGL20.UNSIGNED_BYTE, bytes.AsSpan());
        }

        public void DeleteTexture(Texture2D texture)
            => JSBind_WEBGL20.DeleteTexture(texture.Handle);

        // ============ 渲染目标（原 GraphicsDevice 的 Platform* 系列） ============

        public void CreateRenderTarget(IRenderTarget renderTarget, int width, int height, DepthFormat depthFormat)
        {
            int internalFormat = depthFormat switch
            {
                DepthFormat.Depth16 => JSBind_WEBGL20.DEPTH_COMPONENT16,
                DepthFormat.Depth24 => JSBind_WEBGL20.DEPTH_COMPONENT24,
                DepthFormat.Depth24Stencil8 => JSBind_WEBGL20.DEPTH24_STENCIL8,
                _ => 0,
            };

            // —— 多重采样：颜色用 multisample renderbuffer，解析后写入纹理 ——
            if (renderTarget.MultiSampleCount > 0)
            {
                int samples = renderTarget.MultiSampleCount;
                int max = JSBind_WEBGL20.GetParameterInt(JSBind_WEBGL20.MAX_SAMPLES);
                if (samples > max) samples = Math.Max(1, max);
                if (samples < 1) samples = 1;

                // MSAA 颜色 renderbuffer（RGBA8）
                JSObject colorRB = JSBind_WEBGL20.CreateRenderbuffer();
                JSBind_WEBGL20.BindRenderbuffer(JSBind_WEBGL20.RENDERBUFFER, colorRB);
                JSBind_WEBGL20.RenderbufferStorageMultisample(JSBind_WEBGL20.RENDERBUFFER, samples, JSBind_WEBGL20.RGBA8, width, height);
                renderTarget.GLColorRenderbuffer = colorRB;

                // MSAA FBO：颜色挂 multisample renderbuffer
                JSObject msFbo = JSBind_WEBGL20.CreateFramebuffer();
                JSBind_WEBGL20.BindFramebuffer(JSBind_WEBGL20.FRAMEBUFFER, msFbo);
                JSBind_WEBGL20.FramebufferRenderbuffer(JSBind_WEBGL20.FRAMEBUFFER, JSBind_WEBGL20.COLOR_ATTACHMENT0, JSBind_WEBGL20.RENDERBUFFER, colorRB);

                if (internalFormat != 0)
                {
                    JSObject depthRB = JSBind_WEBGL20.CreateRenderbuffer();
                    JSBind_WEBGL20.BindRenderbuffer(JSBind_WEBGL20.RENDERBUFFER, depthRB);
                    JSBind_WEBGL20.RenderbufferStorageMultisample(JSBind_WEBGL20.RENDERBUFFER, samples, internalFormat, width, height);
                    JSBind_WEBGL20.FramebufferRenderbuffer(JSBind_WEBGL20.FRAMEBUFFER, JSBind_WEBGL20.DEPTH_ATTACHMENT, JSBind_WEBGL20.RENDERBUFFER, depthRB);
                    renderTarget.GLDepthBuffer = depthRB;
                    // 照 MonoGame：Depth24Stencil8 时 stencil 与 depth 是同一个 renderbuffer。
                    renderTarget.GLStencilBuffer = depthFormat == DepthFormat.Depth24Stencil8 ? depthRB : null;
                }

                int st = JSBind_WEBGL20.CheckFramebufferStatus(JSBind_WEBGL20.FRAMEBUFFER);
                if (st != JSBind_WEBGL20.FRAMEBUFFER_COMPLETE)
                    Console.Error.WriteLine($"[KFramework.MonoGame] MSAA 帧缓冲不完整: 0x{st:X4}");
                renderTarget.GLMultiSampleFramebuffer = msFbo;

                // 解析 FBO：把可采样纹理挂上，resolve 时 blit 进来
                JSObject resolveFbo = JSBind_WEBGL20.CreateFramebuffer();
                JSBind_WEBGL20.BindFramebuffer(JSBind_WEBGL20.FRAMEBUFFER, resolveFbo);
                JSBind_WEBGL20.FramebufferTexture2D(JSBind_WEBGL20.FRAMEBUFFER, JSBind_WEBGL20.COLOR_ATTACHMENT0,
                    JSBind_WEBGL20.TEXTURE_2D, renderTarget.GLTexture, 0);
                int st2 = JSBind_WEBGL20.CheckFramebufferStatus(JSBind_WEBGL20.FRAMEBUFFER);
                if (st2 != JSBind_WEBGL20.FRAMEBUFFER_COMPLETE)
                    Console.Error.WriteLine($"[KFramework.MonoGame] 解析帧缓冲不完整: 0x{st2:X4}");
                renderTarget.GLResolveFramebuffer = resolveFbo;

                JSBind_WEBGL20.BindFramebuffer(JSBind_WEBGL20.FRAMEBUFFER, null);
                return;
            }

            if (internalFormat == 0)
            {
                renderTarget.GLDepthBuffer = null;
                renderTarget.GLStencilBuffer = null;
                return;
            }

            JSObject depth = JSBind_WEBGL20.CreateRenderbuffer();
            JSBind_WEBGL20.BindRenderbuffer(JSBind_WEBGL20.RENDERBUFFER, depth);
            JSBind_WEBGL20.RenderbufferStorage(JSBind_WEBGL20.RENDERBUFFER, internalFormat, width, height);

            renderTarget.GLDepthBuffer = depth;
            // 照 MonoGame：Depth24Stencil8 时 stencil 与 depth 是同一个 renderbuffer（GLES 无独立 stencil 格式）。
            renderTarget.GLStencilBuffer = depthFormat == DepthFormat.Depth24Stencil8 ? depth : null;
        }

        public void DeleteRenderTarget(IRenderTarget renderTarget)
        {
            if (renderTarget.GLColorRenderbuffer is { } colorRB)
            {
                JSBind_WEBGL20.DeleteRenderbuffer(colorRB);
                renderTarget.GLColorRenderbuffer = null;
            }
            if (renderTarget.GLMultiSampleFramebuffer is { } msFbo)
            {
                JSBind_WEBGL20.DeleteFramebuffer(msFbo);
                renderTarget.GLMultiSampleFramebuffer = null;
            }
            if (renderTarget.GLResolveFramebuffer is { } resolveFbo)
            {
                JSBind_WEBGL20.DeleteFramebuffer(resolveFbo);
                renderTarget.GLResolveFramebuffer = null;
            }

            if (renderTarget.GLDepthBuffer is { } depth)
                JSBind_WEBGL20.DeleteRenderbuffer(depth);
            renderTarget.GLDepthBuffer = null;
            renderTarget.GLStencilBuffer = null;

            List<RenderTargetBinding[]>? dead = null;
            foreach (KeyValuePair<RenderTargetBinding[], JSObject> pair in _glFramebuffers)
            {
                foreach (RenderTargetBinding binding in pair.Key)
                {
                    if (ReferenceEquals(binding.RenderTarget, renderTarget))
                    {
                        (dead ??= new List<RenderTargetBinding[]>()).Add(pair.Key);
                        break;
                    }
                }
            }

            if (dead is null) return;
            foreach (RenderTargetBinding[] key in dead)
            {
                if (_glFramebuffers.TryGetValue(key, out JSObject? framebuffer) && _glFramebuffers.Remove(key))
                    JSBind_WEBGL20.DeleteFramebuffer(framebuffer);
            }
        }

        /// <summary>建 / 复用并绑定当前渲染目标组合的 FBO（照 MonoGame 的 PlatformApplyRenderTargets）。</summary>
        public IRenderTarget ApplyRenderTargets(RenderTargetBinding[] bindings, int count)
        {
            var first = (IRenderTarget)bindings[0].RenderTarget!;

            // 多重采样：颜色是 multisample renderbuffer，渲染时直接绑 MSAA FBO，不走组合缓存，
            // 解析后才通过 GLResolveFramebuffer 写入 GLTexture。
            if (first.MultiSampleCount > 0 && first.GLMultiSampleFramebuffer is { } msFbo)
            {
                JSBind_WEBGL20.BindFramebuffer(JSBind_WEBGL20.FRAMEBUFFER, msFbo);
                return first;
            }

            if (!_glFramebuffers.TryGetValue(bindings, out JSObject? framebuffer))
            {
                framebuffer = JSBind_WEBGL20.CreateFramebuffer();
                JSBind_WEBGL20.BindFramebuffer(JSBind_WEBGL20.FRAMEBUFFER, framebuffer);

                // 深度 / 模板附件（照 MonoGame：Depth24Stencil8 时二者共用同一个 renderbuffer）。
                if (first.GLDepthBuffer is { } depth)
                    JSBind_WEBGL20.FramebufferRenderbuffer(JSBind_WEBGL20.FRAMEBUFFER, JSBind_WEBGL20.DEPTH_ATTACHMENT,
                        JSBind_WEBGL20.RENDERBUFFER, depth);
                if (first.GLStencilBuffer is { } stencil)
                    JSBind_WEBGL20.FramebufferRenderbuffer(JSBind_WEBGL20.FRAMEBUFFER, JSBind_WEBGL20.STENCIL_ATTACHMENT,
                        JSBind_WEBGL20.RENDERBUFFER, stencil);

                for (int i = 0; i < count; i++)
                {
                    var target = (IRenderTarget)bindings[i].RenderTarget!;
                    JSBind_WEBGL20.FramebufferTexture2D(JSBind_WEBGL20.FRAMEBUFFER, JSBind_WEBGL20.COLOR_ATTACHMENT0 + i,
                        JSBind_WEBGL20.TEXTURE_2D, target.GLTexture, 0);
                }

                int status = JSBind_WEBGL20.CheckFramebufferStatus(JSBind_WEBGL20.FRAMEBUFFER);
                if (status != JSBind_WEBGL20.FRAMEBUFFER_COMPLETE)
                    Console.Error.WriteLine($"[KFramework.MonoGame] Framebuffer 不完整: 0x{status:X4}");

                _glFramebuffers.Add((RenderTargetBinding[])bindings.Clone(), framebuffer);
            }
            else
            {
                JSBind_WEBGL20.BindFramebuffer(JSBind_WEBGL20.FRAMEBUFFER, framebuffer);
            }

            return first;
        }

        public void ApplyDefaultRenderTarget()
            => JSBind_WEBGL20.BindFramebuffer(JSBind_WEBGL20.FRAMEBUFFER, null);

        /// <summary>把多重采样帧缓冲解析到可采样纹理（照 MonoGame 的 PlatformResolveRenderTargets）。</summary>
        public void ResolveRenderTarget(IRenderTarget rt)
        {
            if (rt.MultiSampleCount <= 0) return;
            if (rt.GLMultiSampleFramebuffer is not { } ms || rt.GLResolveFramebuffer is not { } resolve) return;

            JSBind_WEBGL20.BindFramebuffer(JSBind_WEBGL20.READ_FRAMEBUFFER, ms);
            JSBind_WEBGL20.BindFramebuffer(JSBind_WEBGL20.DRAW_FRAMEBUFFER, resolve);
            JSBind_WEBGL20.BlitFramebuffer(0, 0, rt.Width, rt.Height, 0, 0, rt.Width, rt.Height,
                JSBind_WEBGL20.COLOR_BUFFER_BIT, JSBind_WEBGL20.LINEAR);
            JSBind_WEBGL20.BindFramebuffer(JSBind_WEBGL20.READ_FRAMEBUFFER, null);
            JSBind_WEBGL20.BindFramebuffer(JSBind_WEBGL20.DRAW_FRAMEBUFFER, null);
        }

        public void Dispose()
        {
            // 渲染目标的 FBO 缓存归后端所有，随设备一起释放（单个 RT 的 Dispose 会先摘掉自己的条目）。
            foreach (JSObject framebuffer in _glFramebuffers.Values)
                JSBind_WEBGL20.DeleteFramebuffer(framebuffer);
            _glFramebuffers.Clear();

            JSBind_WEBGL20.DeleteBuffer(_vertexBuffer);
            JSBind_WEBGL20.DeleteBuffer(_indexBuffer);
            _effect.Dispose();
        }

        /// <summary>渲染目标绑定组合的比较器（照 MonoGame 的 RenderTargetBindingArrayComparer）。</summary>
        private sealed class RenderTargetBindingArrayComparer : IEqualityComparer<RenderTargetBinding[]>
        {
            public bool Equals(RenderTargetBinding[]? first, RenderTargetBinding[]? second)
            {
                if (ReferenceEquals(first, second)) return true;
                if (first is null || second is null) return false;
                if (first.Length != second.Length) return false;

                for (int i = 0; i < first.Length; i++)
                {
                    // 照 MonoGame：只比渲染目标本身与切片，槽位为 null 的两项也算相等。
                    if (!ReferenceEquals(first[i].RenderTarget, second[i].RenderTarget)) return false;
                    if (first[i].ArraySlice != second[i].ArraySlice) return false;
                }
                return true;
            }

            public int GetHashCode(RenderTargetBinding[] array)
            {
                var hashCode = new HashCode();
                foreach (RenderTargetBinding binding in array)
                {
                    hashCode.Add(binding.RenderTarget?.GetHashCode() ?? 0);
                    hashCode.Add(binding.ArraySlice);
                }
                return hashCode.ToHashCode();
            }
        }
    }

    /// <summary>
    /// WebGL2 的 GL 内部格式常量（= compressedTexImage2D 的 internalFormat），以及
    /// <see cref="SurfaceFormat"/>（后端中立枚举）→ GL 内部格式的映射表。
    /// 仅 WebGL2 后端使用；压缩块/字节数等后端无关信息见共享层的 <see cref="SurfaceFormatInfo"/>。
    /// </summary>
    internal static class SurfaceFormatGL
    {
        public const int RGBA8 = 0x8058;
        public const int COMPRESSED_RGBA_S3TC_DXT1_EXT = 0x83F0;
        public const int COMPRESSED_RGBA_S3TC_DXT3_EXT = 0x83F2;
        public const int COMPRESSED_RGBA_S3TC_DXT5_EXT = 0x83F3;
        public const int COMPRESSED_RGBA_BPTC_UNORM_EXT = 0x8E8C;   // BC7，需 EXT_texture_compression_bptc
        public const int COMPRESSED_RGBA_ASTC_4X4_KHR = 0x93B0;
        public const int COMPRESSED_RGBA_ASTC_5X5_KHR = 0x93B1;
        public const int COMPRESSED_RGBA_ASTC_6X6_KHR = 0x93B2;
        public const int COMPRESSED_RGBA_ASTC_8X8_KHR = 0x93B3;
        public const int COMPRESSED_RGBA_ASTC_10X10_KHR = 0x93B4;
        public const int COMPRESSED_RGBA_ASTC_12X12_KHR = 0x93B5;
        public const int COMPRESSED_RGB8_ETC2 = 0x9274;
        public const int COMPRESSED_RGBA8_ETC2_EAC = 0x9278;
        public const int COMPRESSED_RGBA_PVRTC_2BPPV1_IMG = 0x8C03;
        public const int COMPRESSED_RGBA_PVRTC_4BPPV1_IMG = 0x8C02;

        /// <summary>
        /// <see cref="SurfaceFormat"/>（后端中立枚举）→ GL 内部格式常量。
        /// WebGL2 的 compressedTexImage2D 的 internalFormat 即取此值；未压缩（<see cref="SurfaceFormat.Color"/>）返回 RGBA8。
        /// </summary>
        public static int ToInternalFormat(SurfaceFormat format) => format switch
        {
            SurfaceFormat.Color => RGBA8,
            SurfaceFormat.Dxt1 => COMPRESSED_RGBA_S3TC_DXT1_EXT,
            SurfaceFormat.Dxt3 => COMPRESSED_RGBA_S3TC_DXT3_EXT,
            SurfaceFormat.Dxt5 => COMPRESSED_RGBA_S3TC_DXT5_EXT,
            SurfaceFormat.Bc7 => COMPRESSED_RGBA_BPTC_UNORM_EXT,
            SurfaceFormat.Astc4X4 => COMPRESSED_RGBA_ASTC_4X4_KHR,
            SurfaceFormat.Astc5X5 => COMPRESSED_RGBA_ASTC_5X5_KHR,
            SurfaceFormat.Astc6X6 => COMPRESSED_RGBA_ASTC_6X6_KHR,
            SurfaceFormat.Astc8X8 => COMPRESSED_RGBA_ASTC_8X8_KHR,
            SurfaceFormat.Astc10X10 => COMPRESSED_RGBA_ASTC_10X10_KHR,
            SurfaceFormat.Astc12X12 => COMPRESSED_RGBA_ASTC_12X12_KHR,
            SurfaceFormat.Etc2Rgb8 => COMPRESSED_RGB8_ETC2,
            SurfaceFormat.Etc2Rgba8 => COMPRESSED_RGBA8_ETC2_EAC,
            SurfaceFormat.PvrtcRgba2Bpp => COMPRESSED_RGBA_PVRTC_2BPPV1_IMG,
            SurfaceFormat.PvrtcRgba4Bpp => COMPRESSED_RGBA_PVRTC_4BPPV1_IMG,
            _ => RGBA8,
        };
    }

}
