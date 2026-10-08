using System.Numerics;
using System.Runtime.InteropServices;

namespace KFramework.MonoGame
{
    /// <summary>
    /// SRP-Batcher 式的批处理器（照 Unity 的 URP / SRP Batcher）：<b>不减少 DrawCall</b>，
    /// 而是把"换物体"的 CPU 开销压到只剩<b>一次绑定</b>。
    /// <para>
    /// <b>Unity 的 SRP Batcher 到底省什么</b>（三条硬性机制，本类一一对应）：
    /// <list type="number">
    ///   <item><description><b>材质属性集中在一份常量缓冲里</b>（Unity 的 <c>CBUFFER_START(UnityPerMaterial)</c>），
    ///   这份缓冲常驻 GPU；材质不变就一个字节都不用重传。→ 本类的 <c>UnityPerMaterial</c> 块 + <see cref="MaterialUploads"/>。</description></item>
    ///   <item><description><b>逐物体数据放在另一份缓冲里</b>（Unity 的 <c>UnityPerDraw</c> / <c>unity_ObjectToWorld</c>），
    ///   一批物体的数据一次性上传，之后每个物体只是"绑到哪一段"。→ 本类整段上传 <see cref="UrpDrawData"/> + 逐个 <c>bindBufferRange</c>。</description></item>
    ///   <item><description><b>同一 shader 变体连续绘制时不切程序、不重设状态</b>（Unity 里就是"不 SetPass"）。
    ///   要求所有材质属性都声明在同一个 CBUFFER 里 —— 用 <see cref="ShaderPropertyBlock"/> 的物体会被踢出 SRP Batcher，正是这条。</description></item>
    /// </list>
    /// 结果是：<b>DrawCall 数量不变</b>，但每物体之间的 CPU 成本从"设一堆 uniform + 可能切程序"变成"改一次绑定"。
    /// 它和 GPU 实例化是<b>互补</b>的两条路：实例化能把 DrawCall 压到 1，但受顶点属性槽/格式限制；
    /// 这条路不压 DrawCall，却什么数据都装得下（矩阵、整型、任意分量），而且不依赖实例化支持。
    /// </para>
    /// <para>
    /// 三条路的选型（都能"同材质画 N 个精灵"）：
    /// <list type="table">
    ///   <item><term><see cref="SpriteBatch"/>（CPU 合批）</term><description>按纹理分批，DrawCall 少；每帧重传整批顶点几何。</description></item>
    ///   <item><term><see cref="GpuInstanceBatch"/>（GPU 实例化）</term><description>DrawCall = 1/缓冲；逐物体数据走顶点属性通道（受格式与槽数限制）。</description></item>
    ///   <item><term>URPBatch（本类，SRP Batcher 式）</term><description>DrawCall = 物体数；换物体成本 ≈ 一次 <c>bindBufferRange</c>，数据走 UBO（不受顶点格式限制）。</description></item>
    /// </list>
    /// </para>
    /// <para>用法：</para>
    /// <code>
    /// using var urp = new URPBatch(Device, atlas, material);
    /// urp.MaterialColor = new Vector4(1f, 0.9f, 0.8f, 1f);   // 材质常量：一段只传一次
    /// urp.Begin();
    /// for (int i = 0; i &lt; 512; i++)
    ///     urp.Add(center, size, rotation, tint);            // 逐物体：进逐物体常量缓冲
    /// int drawCalls = urp.End();                            // = 512（不降 DrawCall），材质上传仍只有 1 次
    /// </code>
    /// </summary>
    public sealed class URPBatch : IDisposable
    {
        private readonly GraphicsDevice _device;
        private readonly Texture2D _texture;
        private readonly Material _material;
        private readonly IUrpProgram? _impl;
        private readonly List<UrpDrawData> _objects = new();

        /// <summary>本批物体共用的 UV 矩形（取自纹理的 <see cref="Texture2D.Bounds"/>）。</summary>
        private readonly Vector4 _uvRect;

        /// <summary>材质常量（<c>UnityPerMaterial</c> 里的 uColorScale）：改了才在下次提交时重传一次。</summary>
        public Vector4 MaterialColor = Vector4.One;

        /// <summary>累计上传材质常量的次数（诊断用：SRP Batcher 的卖点就是它远小于物体数）。</summary>
        public long MaterialUploads { get; private set; }

        private bool _materialUploaded;
        private Vector4 _uploadedColor;
        private bool _begun;

        /// <summary>
        /// 创建 SRP-Batcher 式批处理器。
        /// </summary>
        /// <param name="device">图形设备。</param>
        /// <param name="texture">本批共用的纹理（图集子区域视图也可以，UV 自动取其 Bounds）。</param>
        /// <param name="material">材质（混合 / 采样 / 深度 / 剔除状态）；为 null 时用精灵默认状态。</param>
        /// <param name="fragmentSource">自定义片元着色器（GLSL ES 3.00）。为 null 时用内置的
        /// "纹理 × 逐物体颜色 × 材质常量"。注意：它必须声明 <c>UnityPerMaterial</c> 常量块（照 Unity 的 SRP Batcher 兼容要求）。</param>
        /// <param name="capacity">单段能画的物体上限（超出自动分段）。</param>
        public URPBatch(GraphicsDevice device, Texture2D texture, Material? material = null,
                        string? fragmentSource = null, int capacity = 1024)
        {
            ArgumentNullException.ThrowIfNull(device);
            ArgumentNullException.ThrowIfNull(texture);

            _device = device;
            _texture = texture;

            _material = new Material();
            if (material is not null)
            {
                _material.Effect = material.Effect;
                _material.Blend = material.Blend;
                _material.Sampler = material.Sampler;
                _material.DepthStencil = material.DepthStencil;
                _material.Rasterizer = material.Rasterizer;
            }
            else
            {
                _material.Blend = BlendState.NonPremultiplied;
                _material.Sampler = SamplerState.Point;
                _material.DepthStencil = DepthStencilState.None;
                _material.Rasterizer = RasterizerState.CullNone;
            }

            _impl = device.Backend.CreateUrpProgram(fragmentSource, capacity);
            _uvRect = ComputeUvRect(texture);
        }

        /// <summary>纹理的 UV 矩形（归一化到 0~1）：xy = 起点，zw = 尺寸。</summary>
        private static Vector4 ComputeUvRect(Texture2D texture)
        {
            float tw = Math.Max(1, texture.TextureWidth);
            float th = Math.Max(1, texture.TextureHeight);
            Rectangle bounds = texture.Bounds;
            return new Vector4(bounds.X / tw, bounds.Y / th, bounds.Width / tw, bounds.Height / th);
        }

        /// <summary>当前后端是否支持 SRP-Batcher 式的 UBO 绘制（WebGL2 支持；WebGPU 尚未接入）。</summary>
        public bool IsSupported => _impl is not null;

        /// <summary>单段的物体上限。</summary>
        public int Capacity => _impl?.Capacity ?? 0;

        /// <summary>本次 Begin 之后已加入的物体数。</summary>
        public int ObjectCount => _objects.Count;

        /// <summary>开一段：之后 <see cref="Add"/> 的物体共用同一份材质与纹理。</summary>
        public void Begin()
        {
            if (_begun) throw new InvalidOperationException("上一次 Begin 还没有对应的 End。");
            _objects.Clear();
            _begun = true;
        }

        public void Add(Vector2 center, Vector2 size)
            => Add(center, size, 0f, Color.White);

        public void Add(Vector2 center, Vector2 size, float rotation)
            => Add(center, size, rotation, Color.White);

        /// <summary>
        /// 便捷重载：按「中心点 / 尺寸 / 旋转弧度 / 颜色」加入一个物体
        /// （内部用 <see cref="GpuInstance.CreateObjectToWorld"/> 组出对象→世界矩阵）。
        /// </summary>
        public void Add(Vector2 center, Vector2 size, float rotation, Color tint)
            => Add(GpuInstance.CreateObjectToWorld(center, size, rotation), tint);

        /// <summary>
        /// 直接给「对象→世界矩阵」（照 Unity 的 per-object <c>unity_ObjectToWorld</c>）：
        /// 矩阵把单位四边形 (0,0)-(1,1) 变换到屏幕，约定与本引擎一致（行主序 + 行向量 p' = p × M）。
        /// </summary>
        public void Add(in Matrix4x4 objectToWorld, Color tint)
        {
            if (!_begun) throw new InvalidOperationException("Add 必须在 Begin / End 之间调用。");

            _objects.Add(new UrpDrawData
            {
                ObjectToWorld = objectToWorld,
                UvRect = _uvRect,
                Tint = new Vector4(tint.R / 255f, tint.G / 255f, tint.B / 255f, tint.A / 255f),
            });
        }

        /// <summary>
        /// 提交本段：先（必要时）上传一次材质常量，再整段上传逐物体常量，
        /// 然后逐个 <c>drawElements</c>（每次只重绑一次 UBO 范围）。返回本次的 DrawCall 数（= 物体数）。
        /// </summary>
        public int End()
        {
            if (!_begun) throw new InvalidOperationException("End 必须在 Begin 之后调用。");
            _begun = false;

            if (_impl is null)
                throw new InvalidOperationException(
                    $"当前后端（{_device.Backend.Name}）尚未接入 SRP-Batcher 式的 UBO 绘制。");
            if (_objects.Count == 0) return 0;

            // 材质常量：材质/值没变就一次都不传（这就是"每材质一份常驻常量缓冲"）。
            bool uploadMaterial = !_materialUploaded || MaterialColor != _uploadedColor;
            if (uploadMaterial)
            {
                _materialUploaded = true;
                _uploadedColor = MaterialColor;
                MaterialUploads++;
            }

            Matrix4x4 projection = CurrentProjection();
            Span<UrpDrawData> span = CollectionsMarshal.AsSpan(_objects);
            Vector4 color = MaterialColor;

            int draws = 0;
            for (int offset = 0; offset < span.Length; offset += _impl.Capacity)
            {
                int count = Math.Min(_impl.Capacity, span.Length - offset);
                draws += _device.DrawUrpSegment(_impl, _material, projection, span.Slice(offset, count), count,
                                                _texture, color, uploadMaterial);
                uploadMaterial = false;   // 后续分段共用同一份常驻材质缓冲，不必再传
            }

            _objects.Clear();
            return draws;
        }

        /// <summary>
        /// 投影矩阵：与 SpriteBatch 完全一致（世界坐标就是屏幕坐标；离屏渲染时是否翻 Y 取决于后端坐标系原点）。
        /// </summary>
        private Matrix4x4 CurrentProjection()
        {
            var viewport = _device.Viewport;
            return _device.RenderTargetCount > 0 && _device.Backend.NeedsOffscreenYFlip
                ? Matrix4x4.CreateOrthographicOffCenter(0f, viewport.Width, 0f, viewport.Height, 0f, 1f)
                : Matrix4x4.CreateOrthographicScreen(viewport.Width, viewport.Height);
        }

        public void Dispose() => _impl?.Dispose();
    }
}
