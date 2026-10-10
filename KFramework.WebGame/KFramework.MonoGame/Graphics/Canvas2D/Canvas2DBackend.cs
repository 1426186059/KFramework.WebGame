using System.Runtime.InteropServices;

namespace KFramework.MonoGame
{
    /// <summary>
    /// Canvas2D 渲染后端：用浏览器的 Canvas2D API 直接把精灵批次回放成 <c>drawImage</c>，
    /// 作为 WebGL2 / WebGPU 之外的第三条路。定位是<b>功能对照 / 兜底后端</b>，不是性能路径。
    /// <para>
    /// 能力边界（做不到的如实抛错，与 WebGPU 后端"未实现就抛"的约定一致）：
    /// <list type="bullet">
    ///   <item><description><b>支持</b>：清屏 / 视口 / 矩形裁剪（clip）、混合（普通 / 加色 / 正片叠底 / 不透明）、
    ///   点采样与线性采样、纹理创建与上传（含动态字形图集的局部更新）、CPU 合批的精灵与文字
    ///   （<see cref="SpriteBatch"/> / <see cref="SpriteNestedBatch"/>）、读像素。</description></item>
    ///   <item><description><b>不支持</b>：自定义着色器、GPU 实例化、URP（UBO）、渲染目标、压缩纹理、深度 / 模板 ——
    ///   这些一律抛 <see cref="NotSupportedException"/>；其中实例化与 URP 通过
    ///   <see cref="SupportsGpuInstancing"/> / <see cref="SupportsUrpBatching"/> 返回 false 提前告知上层，
    ///   由已有的"后端不支持"分支给出可读的报错。</description></item>
    ///   <item><description><b>语义差异</b>：逐绘制着色（顶点色 tint）在 Canvas2D 里没有对应能力，
    ///   由 TS 侧按"纹理 + RGB"缓存一份着色的离屏副本实现（见 render_canvas2d.ts 的 tintedSource），
    ///   故非白色着色会有额外开销。</description></item>
    /// </list>
    /// </para>
    /// </summary>
    internal sealed class Canvas2DBackend : IGraphicsBackend
    {
        private Canvas2DShaderProgram _default2D = null!;

        /// <summary>纹理句柄分配器（Canvas2D 的纹理就是一张离屏 canvas，句柄只需会话内唯一）。</summary>
        private int _nextTextureId = 1;

        /// <summary>最近一次设置的裁剪矩形（Canvas2D 的 clip 只能"整块换"，故记录后按需重建）。</summary>
        private (int X, int Y, int W, int H) _scissorRect;

        private bool _scissorEnabled;

        /// <summary>当前视口尺寸（投影换算要用，见 <see cref="Canvas2DShaderProgram.Apply"/>）。</summary>
        internal int ViewportWidth { get; private set; }
        internal int ViewportHeight { get; private set; }

        public string Name => "Canvas2D";

        /// <summary>Canvas2D 坐标原点在左上，与屏幕一致；且本后端不支持离屏渲染，故无需 Y 翻转。</summary>
        public bool NeedsOffscreenYFlip => false;

        public int MaxTextureSize { get; private set; }

        public string Renderer { get; private set; } = string.Empty;

        public Task InitializeAsync(bool antialias)
        {
            if (!JSBind_Canvas2D.Init(antialias))
                throw new InvalidOperationException(
                    "无法创建 Canvas2D 上下文（画布可能已被其它上下文类型占用）。");

            Renderer = JSBind_Canvas2D.GetRenderer();
            MaxTextureSize = JSBind_Canvas2D.GetMaxTextureSize();
            _default2D = new Canvas2DShaderProgram(this);

            // Canvas2D 的初始化是同步的，返回已完成的 Task（与 WebGPU 的异步签名统一）。
            return Task.CompletedTask;
        }

        /// <summary>Canvas2D 无需收帧：浏览器会把画布内容按 rAF 直接合成。</summary>
        public void EndFrame() => JSBind_Canvas2D.EndFrame();

        public int GetError() => JSBind_Canvas2D.GetError();

        public IShaderProgram CreateShaderProgram() => _default2D;

        /// <summary>Canvas2D 没有可编程管线，无法编译自定义着色器。</summary>
        public IShaderProgram CreateCustomShaderProgram(string vertexSource, string fragmentSource)
            => throw new NotSupportedException(
                $"渲染后端「{Name}」不支持自定义着色器（Canvas2D 没有可编程管线）：请改用 WebGL2 / WebGPU 后端。");

        public bool SupportsGpuInstancing => false;

        public IGpuInstanceProgram? CreateGpuInstanceProgram(string? fragmentSource, int capacity) => null;

        public bool SupportsUrpBatching => false;

        public IUrpProgram? CreateUrpProgram(string? fragmentSource) => null;

        // ============ 视口 / 裁剪 / 清屏 / 读像素 ============

        public void SetViewport(int x, int y, int width, int height)
        {
            ViewportWidth = width;
            ViewportHeight = height;
            JSBind_Canvas2D.SetViewport(x, y, width, height);
        }

        public void SetScissor(int x, int y, int width, int height)
        {
            _scissorRect = (x, y, width, height);

            // 只有打开了剪刀测试才真正裁剪（照 WebGL 后端的语义：ScissorTestEnable 决定开关）
            if (_scissorEnabled)
                JSBind_Canvas2D.SetScissor(x, y, width, height);
        }

        public void Clear(Color color)
            => JSBind_Canvas2D.Clear(color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f);

        /// <summary>读像素：Canvas2D 原点在左上，与上层一致，故不需要 WebGL 那样的 Y 换算。</summary>
        public void ReadPixel(int x, int y, int viewportHeight, Span<byte> rgba)
            => JSBind_Canvas2D.ReadPixel(x, y, rgba);

        public Task ReadPixels(int x, int y, int width, int height, byte[] rgba)
        {
            JSBind_Canvas2D.ReadPixels(x, y, width, height, rgba);
            return Task.CompletedTask;
        }

        // ============ 状态 ============

        /// <summary>
        /// 混合：Canvas2D 只有 globalCompositeOperation 一个开关，按"源 / 目标因子"的组合归类：
        /// <list type="bullet">
        ///   <item><description>加色（<c>SrcAlpha+One</c> / <c>One+One</c>）→ <c>lighter</c>
        ///   —— 覆盖 <see cref="BlendState.Additive"/> 与 <see cref="BlendState.AdditiveFull"/>；</description></item>
        ///   <item><description>正片叠底（<c>DstColor+Zero</c> / <c>Zero+SrcColor</c>）→ <c>multiply</c>
        ///   —— 后者是引擎内置 <see cref="BlendState.Multiply"/> 的写法（原 D3D9 固定管线，传奇的光照图用它）；</description></item>
        ///   <item><description>关闭混合（<see cref="BlendState.Opaque"/>，<c>enabled:false</c>）与其余（含默认的 <c>SrcAlpha+OneMinusSrcAlpha</c>）
        ///   → <c>source-over</c> —— Canvas2D 的 source-over 本身就是"源 alpha / 1−源 alpha"的预乘合成，与本引擎的默认混合一致。</description></item>
        /// </list>
        /// <para>
        /// 已知差异：Canvas2D 没有"忽略源 alpha"的合成算子，所以 <see cref="BlendState.Opaque"/> 下顶点 alpha 仍会被叠加
        /// （半透明精灵照旧半透明），要完全覆盖只能把顶点 alpha 给满。
        /// </para>
        /// </summary>
        public void SetBlendState(BlendState state)
        {
            int kind;
            if (!state.Enabled)
                kind = 0;
            else if (IsMultiply(state))
                kind = 3;
            else if (IsAdditive(state))
                kind = 1;
            else
                kind = 0;

            JSBind_Canvas2D.SetBlendState(kind);
        }

        /// <summary>
        /// 正片叠底：两种等价写法都认 —— <c>DstColor+Zero</c>（<c>dst*src</c>）与 <c>Zero+SrcColor</c>
        /// （<see cref="BlendState.Multiply"/>，D3D9 固定管线写法）的结果都是"目标色乘以源色"。
        /// </summary>
        private static bool IsMultiply(BlendState s)
            => (s.SourceColorBlendFactor == BlendMode.DstColor && s.DestinationColorBlendFactor == BlendMode.Zero)
            || (s.SourceColorBlendFactor == BlendMode.Zero && s.DestinationColorBlendFactor == BlendMode.SrcColor);

        /// <summary>加色：<c>SrcAlpha+One</c>（按源 alpha 加权）与 <c>One+One</c>（整亮度相加）都归到 <c>lighter</c>。</summary>
        private static bool IsAdditive(BlendState s)
            => s.DestinationColorBlendFactor == BlendMode.One
            && (s.SourceColorBlendFactor == BlendMode.SrcAlpha || s.SourceColorBlendFactor == BlendMode.One);

        /// <summary>裁剪开关在这里生效（Canvas2D 无"裁剪测试"标志，只能真的上 / 撤 clip）；剔除与 Canvas2D 无关。</summary>
        public void ApplyRasterizerState(RasterizerState state)
        {
            bool wanted = state.ScissorTestEnable;
            if (wanted == _scissorEnabled) return;

            _scissorEnabled = wanted;
            if (wanted)
                JSBind_Canvas2D.SetScissor(_scissorRect.X, _scissorRect.Y, _scissorRect.W, _scissorRect.H);
            else
                JSBind_Canvas2D.ClearScissor();
        }

        /// <summary>Canvas2D 没有深度 / 模板缓冲，本后端也不支持渲染目标，故深度模板状态恒为无操作。</summary>
        public void ApplyDepthStencilState(DepthStencilState state) { }

        /// <summary>采样方式：线性任一（放大 / 缩小）即开 imageSmoothing，否则最近邻。</summary>
        public void SetSamplerState(SamplerState state)
        {
            bool linear = state.MinFilter == TextureFilter.Linear || state.MagFilter == TextureFilter.Linear;
            JSBind_Canvas2D.SetSampler(linear);
        }

        public void BindTexture(Texture2D texture) => JSBind_Canvas2D.BindTexture(texture.Handle);

        // ============ 绘制 ============

        public void DrawUserIndexedPrimitives(VertexPositionColorTexture[] vertices, int start, int end)
        {
            if (end - start <= 0) return;
            JSBind_Canvas2D.DrawBatch(MemoryMarshal.AsBytes(vertices.AsSpan()), start, end);
        }

        // ============ 纹理 ============

        public void CreateTexture(Texture2D texture, int width, int height, bool mipmap, SurfaceFormat format,
                                  Texture2D.SurfaceType type)
        {
            if (type == Texture2D.SurfaceType.RenderTarget)
                throw new NotSupportedException(
                    $"渲染后端「{Name}」不支持渲染目标：Canvas2D 没有 FBO / 附件概念，请改用 WebGL2 / WebGPU 后端。");

            if (format.IsCompressed())
                throw new NotSupportedException(
                    $"渲染后端「{Name}」不支持压缩纹理（{format}）：Canvas2D 无法解码 DXT / ASTC / BC7 / KTX2。");

            int id = _nextTextureId++;
            texture.Handle = id;
            JSBind_Canvas2D.CreateTexture(id, width, height);
        }

        public void SetTextureData(Texture2D texture, int level, byte[] bytes)
        {
            if (level != 0)
                throw new NotSupportedException($"渲染后端「{Name}」只支持 0 级纹理上传（收到 level={level}）。");

            JSBind_Canvas2D.UploadTexture(texture.Handle, level, bytes);
        }

        public void SetTextureData(Texture2D texture, int level, Rectangle rect, byte[] bytes)
        {
            if (level != 0)
                throw new NotSupportedException($"渲染后端「{Name}」只支持 0 级纹理上传（收到 level={level}）。");

            JSBind_Canvas2D.UploadSubTexture(texture.Handle, level, rect.X, rect.Y, rect.Width, rect.Height, bytes);
        }

        public void DeleteTexture(Texture2D texture) => JSBind_Canvas2D.DeleteTexture(texture.Handle);

        // ============ 渲染目标平台层（不支持） ============

        public void CreateRenderTarget(IRenderTarget renderTarget, int width, int height, DepthFormat depthFormat)
            => throw new NotSupportedException(
                $"渲染后端「{Name}」不支持渲染目标（离屏渲染）：请改用 WebGL2 / WebGPU 后端。");

        /// <summary>释放路径上可能被调到（即使从未创建成功），故按无操作处理，不抛。</summary>
        public void DeleteRenderTarget(IRenderTarget renderTarget) { }

        public IRenderTarget ApplyRenderTargets(RenderTargetBinding[] bindings, int count)
            => throw new NotSupportedException(
                $"渲染后端「{Name}」不支持渲染目标（离屏渲染）：请改用 WebGL2 / WebGPU 后端。");

        /// <summary>切回默认目标：Canvas2D 一直在默认画布上画，无操作。</summary>
        public void ApplyDefaultRenderTarget() { }

        public void ResolveRenderTarget(IRenderTarget renderTarget)
            => throw new NotSupportedException(
                $"渲染后端「{Name}」不支持多重采样解析（没有渲染目标）：请改用 WebGL2 / WebGPU 后端。");

        public void Dispose() { }
    }
}
