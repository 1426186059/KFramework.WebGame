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
    ///   （<see cref="SpriteBatch"/> / <see cref="SpriteNestedBatch"/>）、读像素、
    ///   <b>渲染目标（离屏渲染）</b>—— 目标就是另一块离屏 canvas，画完可直接当纹理采样。</description></item>
    ///   <item><description><b>不支持</b>：自定义着色器、GPU 实例化、URP（UBO）、压缩纹理、深度 / 模板、
    ///   多渲染目标（MRT）、渲染目标级 MSAA —— 前四类一律抛 <see cref="NotSupportedException"/>
    ///   （实例化与 URP 通过 <see cref="SupportsGpuInstancing"/> / <see cref="SupportsUrpBatching"/> 返回 false
    ///   提前告知上层，由已有的"后端不支持"分支给出可读的报错）；后两类只记日志并忽略（抛错会让整条离屏链路不可用）。</description></item>
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

        /// <summary>
        /// 恒为 false：Canvas2D 的坐标原点在左上、Y 向下，离屏目标（另一块 canvas）与主画布完全同构，
        /// 所以离屏与屏幕共用同一套投影（<see cref="Matrix4x4.CreateOrthographicScreen"/>），不需要翻转。
        /// </summary>
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

        /// <summary>Canvas2D 没有深度 / 模板缓冲（离屏目标也只是另一块 2D canvas，不带深度附件），故深度模板状态恒为无操作。</summary>
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
            if (format.IsCompressed())
                throw new NotSupportedException(
                    $"渲染后端「{Name}」不支持压缩纹理（{format}）：Canvas2D 无法解码 DXT / ASTC / BC7 / KTX2。");

            int id = _nextTextureId++;
            texture.Handle = id;

            // 渲染目标 = 一块离屏 canvas：它既能被画进去（bindRenderTarget），也能直接被 drawImage 采样。
            // 与普通纹理的两点差别：上下文必须带 alpha（离屏分层靠透明层叠加合成）、不参与 putImageData 上传。
            if (type == Texture2D.SurfaceType.RenderTarget)
            {
                JSBind_Canvas2D.CreateRenderTarget(id, width, height);
                return;
            }

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

        // ============ 渲染目标平台层 ============
        //
        // Canvas2D 没有 FBO / 附件那一套，但"离屏渲染"这件事天然成立：渲染目标就是<b>再要一块离屏 canvas</b>。
        // 它既能被画进去（BindRenderTarget），也能当普通纹理被 drawImage 采样（上屏或当中间图）——
        // 于是"把控件 / 图层烘焙进 RT，再合成上屏"这类架构在本后端可以直接跑（见测试工程 OffscreenScene）。

        /// <summary>
        /// 建渲染目标：那块离屏 canvas 在 <see cref="CreateTexture"/> 里就已经建好了（RT 的 <c>SurfaceType</c>
        /// 就是 RenderTarget），这里没有要分配的底层对象。
        /// <para>
        /// 两点差异：① 没有深度 / 模板附件，<paramref name="depthFormat"/> 被忽略；
        /// ② 没有 RT 级多重采样，<see cref="IRenderTarget.MultiSampleCount"/> 被忽略 —— 这里只记日志不抛错，
        /// 抛了整条离屏链路就不可用，而画质影响仅限"边缘少一点抗锯齿"。
        /// </para>
        /// </summary>
        public void CreateRenderTarget(IRenderTarget renderTarget, int width, int height, DepthFormat depthFormat)
        {
            if (renderTarget.MultiSampleCount > 0)
                PrintTool.Log($"[KFramework.MonoGame] Canvas2D 没有渲染目标级 MSAA：请求 {renderTarget.MultiSampleCount}x 已忽略");
        }

        /// <summary>释放渲染目标：把它的离屏 canvas 从目标表与纹理表一并摘掉。</summary>
        public void DeleteRenderTarget(IRenderTarget renderTarget)
            => JSBind_Canvas2D.DeleteRenderTarget(renderTarget.GLTexture);

        /// <summary>
        /// 绑定渲染目标组合，并返回首个目标（契约要求：上层用它取 <c>Width</c> / <c>Height</c> / usage，
        /// 再据此设视口与投影）。Canvas2D 没有 MRT，故只认 <paramref name="bindings"/>[0]。
        /// </summary>
        public IRenderTarget ApplyRenderTargets(RenderTargetBinding[] bindings, int count)
        {
            if (count > 1)
                PrintTool.Log($"[KFramework.MonoGame] Canvas2D 不支持多渲染目标（MRT，收到 {count} 个）：只用第一个");

            var first = (IRenderTarget)bindings[0].RenderTarget!;
            JSBind_Canvas2D.BindRenderTarget(first.GLTexture);
            return first;
        }

        /// <summary>切回默认目标（主画布）。</summary>
        public void ApplyDefaultRenderTarget() => JSBind_Canvas2D.BindRenderTarget(-1);

        /// <summary>
        /// 多重采样解析：Canvas2D 没有 MSAA，而渲染目标本身就是一块可采样的 canvas（内容即结果），
        /// 故无需动作 —— 与 WebGPU"解析由渲染通道结束自动完成"同理。
        /// </summary>
        public void ResolveRenderTarget(IRenderTarget renderTarget) { }

        public void Dispose() { }
    }
}
