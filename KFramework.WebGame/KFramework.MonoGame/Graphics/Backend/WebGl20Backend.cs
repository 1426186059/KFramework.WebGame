using System.Runtime.InteropServices;
using System.Runtime.InteropServices.JavaScript;

namespace KFramework.MonoGame
{

    /// <summary>
    /// WebGL 2.0 渲染后端。
    /// <para>
    /// 本文件是 <see cref="GraphicsDevice"/> 原先平台层代码的<b>原样搬迁</b>：调用顺序、参数、
    /// 错误处理均与搬迁前一致，仅把分散在 GraphicsDevice / Texture2D.Web.cs 里的 WebGL 调用收拢到一处，
    /// 以便与 WebGPU 后端并列实现 <see cref="IGraphicsBackend"/>。
    /// </para>
    /// </summary>
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

        /// <summary>WebGL 后端无显式收帧动作：画面由浏览器在 rAF 回调结束时自动合成。</summary>
        public void EndFrame() { }

        public Task InitializeAsync(string canvasSelector, bool antialias)
        {
            // 照 MonoGame：MSAA 属性在上下文创建之前设置
            JSBind_WEBGL20.SetAntialias(antialias);

            if (!JSBind_WEBGL20.InitContext(canvasSelector))
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

            JSBind_WEBGL20.Enable(JSBind_WEBGL20.BLEND);
            JSBind_WEBGL20.BlendEquation(JSBind_WEBGL20.BLEND_FUNC_ADD);

            // WebGL 初始化是同步的，故返回已完成的 Task（与 WebGPU 的异步初始化统一签名）。
            return Task.CompletedTask;
        }

        public ISpriteProgram CreateSpriteProgram() => _effect;

        private void ConfigureAttributes()
        {
            int stride = VertexPositionColorTexture.SizeInBytes;
            if (_effect.PositionLocation >= 0)
            {
                JSBind_WEBGL20.EnableVertexAttribArray(_effect.PositionLocation);
                JSBind_WEBGL20.VertexAttribPointer(_effect.PositionLocation, 2, JSBind_WEBGL20.FLOAT, false, stride, 0);
            }
            if (_effect.TexCoordLocation >= 0)
            {
                JSBind_WEBGL20.EnableVertexAttribArray(_effect.TexCoordLocation);
                JSBind_WEBGL20.VertexAttribPointer(_effect.TexCoordLocation, 2, JSBind_WEBGL20.FLOAT, false, stride, 8);
            }
            if (_effect.ColorLocation >= 0)
            {
                JSBind_WEBGL20.EnableVertexAttribArray(_effect.ColorLocation);
                JSBind_WEBGL20.VertexAttribPointer(_effect.ColorLocation, 4, JSBind_WEBGL20.UNSIGNED_BYTE, true, stride, 16);
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
            JSBind_WEBGL20.Clear(JSBind_WEBGL20.COLOR_BUFFER_BIT | JSBind_WEBGL20.DEPTH_BUFFER_BIT);
        }

        /// <summary>读像素。WebGL 的帧缓冲原点在左下，故这里做 Y 换算（上层按左上原点传入）。</summary>
        public void ReadPixel(int x, int y, int viewportHeight, Span<byte> rgba)
            => JSBind_WEBGL20.ReadPixel(x, viewportHeight - 1 - y, rgba);

        public void SetBlendState(BlendState state)
        {
            JSBind_WEBGL20.Enable(JSBind_WEBGL20.BLEND);
            JSBind_WEBGL20.BlendFuncSeparate(state.SourceBlend, state.DestinationBlend,
                                 state.SourceAlphaBlend, state.DestinationAlphaBlend);
        }

        /// <summary>
        /// 把光栅化状态下发给 WebGL（照 MonoGame 的 RasterizerState.Apply）。
        /// CullMode.None 关闭剔除；否则开启 CULL_FACE 并按绕向选 FRONT/BACK，
        /// 配合初始化时设定的 CCW 正面，CullCounterClockwiseFace 即“剔除逆时针背面”。
        /// </summary>
        public void ApplyRasterizerState(RasterizerState state)
        {
            switch (state.CullMode)
            {
                case CullMode.None:
                    JSBind_WEBGL20.Disable(JSBind_WEBGL20.CULL_FACE);
                    break;
                case CullMode.CullClockwiseFace:
                    JSBind_WEBGL20.Enable(JSBind_WEBGL20.CULL_FACE);
                    JSBind_WEBGL20.CullFace(JSBind_WEBGL20.FRONT);
                    break;
                case CullMode.CullCounterClockwiseFace:
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
            if (state.DepthBufferEnable)
                JSBind_WEBGL20.Enable(JSBind_WEBGL20.DEPTH_TEST);
            else
                JSBind_WEBGL20.Disable(JSBind_WEBGL20.DEPTH_TEST);

            JSBind_WEBGL20.DepthMask(state.DepthBufferWriteEnable);
            JSBind_WEBGL20.DepthFunc(ToGLDepthFunc(state.DepthBufferFunction));
        }

        private static int ToGLDepthFunc(CompareFunction func) => func switch
        {
            CompareFunction.Never => JSBind_WEBGL20.NEVER,
            CompareFunction.Less => JSBind_WEBGL20.LESS,
            CompareFunction.Equal => JSBind_WEBGL20.EQUAL,
            CompareFunction.LessEqual => JSBind_WEBGL20.LEQUAL,
            CompareFunction.Greater => JSBind_WEBGL20.GREATER,
            CompareFunction.NotEqual => JSBind_WEBGL20.NOTEQUAL,
            CompareFunction.GreaterEqual => JSBind_WEBGL20.GEQUAL,
            _ => JSBind_WEBGL20.ALWAYS,
        };

        public void SetSamplerState(SamplerState state)
        {
            JSBind_WEBGL20.TexParameteri(JSBind_WEBGL20.TEXTURE_2D, JSBind_WEBGL20.TEXTURE_MIN_FILTER, state.MinFilter);
            JSBind_WEBGL20.TexParameteri(JSBind_WEBGL20.TEXTURE_2D, JSBind_WEBGL20.TEXTURE_MAG_FILTER, state.MagFilter);
            JSBind_WEBGL20.TexParameteri(JSBind_WEBGL20.TEXTURE_2D, JSBind_WEBGL20.TEXTURE_WRAP_S, state.WrapMode);
            JSBind_WEBGL20.TexParameteri(JSBind_WEBGL20.TEXTURE_2D, JSBind_WEBGL20.TEXTURE_WRAP_T, state.WrapMode);
        }

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
                JSBind_WEBGL20.CompressedTexImage2D(JSBind_WEBGL20.TEXTURE_2D, level, (int)texture.Format, levelWidth, levelHeight, 0, bytes.AsSpan());
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
                JSBind_WEBGL20.CompressedTexImage2D(JSBind_WEBGL20.TEXTURE_2D, level, (int)texture.Format, levelWidth, levelHeight, 0, bytes.AsSpan());
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

}
