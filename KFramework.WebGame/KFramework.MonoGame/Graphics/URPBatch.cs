using System.Numerics;

namespace KFramework.MonoGame
{
    /// <summary>
    /// SRP-Batcher 式的批处理器（照 Unity 的 URP / SRP Batcher）：<b>不减少 DrawCall</b>，
    /// 而是把"换物体"的 CPU 开销压到只剩<b>一次绑定</b>。
    /// <para>
    /// <b>API 形态与 <see cref="SpriteBatch"/> 一致</b>：构造只给设备，<see cref="Begin"/> 里给
    /// 材质 / 渲染顺序 / 相机矩阵，<see cref="Draw"/> 的参数（texture / targetRectangle / sourceRectangle / color /
    /// rotation / origin / scale / effects / layerDepth）与
    /// <see cref="SpriteBatch.Draw(Texture2D, Rectangle, Rectangle?, Color, float, Vector2, SpriteEffects, float)"/>
    /// 逐参数对应，三个批处理之间换用不必改调用代码。
    /// <b>纹理走 <see cref="Draw"/> 而不是构造</b> —— 因为每个物体本来就是一次独立 draw，
    /// 每条记录可以带自己的纹理（同纹理连着的会拼成一段，避免重复绑定）。
    /// </para>
    /// <para>
    /// <b>自定义着色器走 <see cref="Material.Effect"/></b>（和 <see cref="SpriteBatch"/> 同一条路子）：
    /// <c>var effect = Device.CreateShaderEffect(fragmentSource); material.Effect = effect;</c>
    /// 引擎从 <see cref="ShaderEffect.FragmentSource"/> 取回源码，为这条路创建对应的程序，并按源码缓存
    /// —— <b>同一个源码只编译一次</b>，所以把它放在 <see cref="Begin"/> 里不会每帧重编译。
    /// 自定义片元着色器必须声明 <c>UnityPerMaterial</c> 常量块（照 Unity 的 SRP Batcher 兼容要求）。
    /// </para>
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
    /// 它和 GPU 实例化是<b>互补</b>的两条路：实例化能把 DrawCall 压到 1（但一次 Begin 只能一张纹理、且受顶点属性槽/格式限制）；
    /// 这条路不压 DrawCall，却什么数据都装得下（矩阵、整型、任意分量），还能逐笔换纹理。
    /// </para>
    /// <para>
    /// 三条路的选型（都能"同材质画 N 个精灵"）：
    /// <list type="table">
    ///   <item><term><see cref="SpriteBatch"/>（CPU 合批）</term><description>按纹理分批，DrawCall 少；每帧重传整批顶点几何。</description></item>
    ///   <item><term><see cref="GpuInstanceBatch"/>（GPU 实例化）</term><description>DrawCall = 1/缓冲；逐物体数据走顶点属性通道（受格式与槽数限制），一次 Begin 一张纹理。</description></item>
    ///   <item><term>UrpBatch（本类，SRP Batcher 式）</term><description>DrawCall = 物体数；换物体成本 ≈ 一次 <c>bindBufferRange</c>，数据走 UBO（不受顶点格式限制），可逐笔换纹理。</description></item>
    /// </list>
    /// </para>
    /// <para>用法：</para>
    /// <code>
    /// using var urp = new UrpBatch(Device);
    /// urp.MaterialColor = new Vector4(1f, 0.9f, 0.8f, 1f);   // 材质常量：一段只传一次
    /// urp.Begin(material);                                  // 材质（含 Material.Effect 自定义着色器）/ 排序 / 相机矩阵
    /// for (int i = 0; i &lt; 512; i++)
    ///     urp.Draw(atlas, targetRect, sourceRect, tint, rotation, origin, scale, SpriteEffects.None, layerDepth);
    /// int drawCalls = urp.End();                            // = 512（不降 DrawCall），材质上传仍只有 1 次
    /// </code>
    /// </para>
    /// </summary>
    public sealed class UrpBatch : IDisposable
    {
        private readonly GraphicsDevice _device;

        /// <summary>本批的材质快照（照 <see cref="SpriteBatch"/> 的 <c>_cache_Mat</c>：复用同一个材质，每帧零分配）。</summary>
        private readonly Material _material = new();

        /// <summary>按片元源码缓存的后端程序：同一个源码只创建（编译）一次。</summary>
        private readonly Dictionary<string, IUrpProgram?> _programs = new();

        /// <summary>本批已排队的物体（按绘制顺序；排序模式非 Deferred 时在提交前排序）。</summary>
        private readonly List<Entry> _entries = new();

        /// <summary>提交时把物体拷成连续数组（逐物体常量缓冲要连续内存）。</summary>
        private UrpDrawData[] _scratch = Array.Empty<UrpDrawData>();

        /// <summary>缓存比较委托，避免每次排序分配。</summary>
        private static readonly Comparison<Entry> EntryComparison = CompareEntries;

        private IUrpProgram? _impl;
        private SpriteSortMode _sortMode;
        private Matrix4x4 _transform = Matrix4x4.Identity;
        private Matrix4x4 _projection;

        /// <summary>「世界 → 屏幕」矩阵（= 相机矩阵 × 投影）：批内不变，开批时算一次。</summary>
        private Matrix4x4 _transformProjection;

        private bool _begun;

        /// <summary>材质常量（<c>UnityPerMaterial</c> 里的 uColorScale）：改了才在下次提交时重传一次。</summary>
        public Vector4 MaterialColor = Vector4.One;

        /// <summary>累计上传材质常量的次数（诊断用：SRP Batcher 的卖点就是它远小于物体数）。</summary>
        public long MaterialUploads { get; private set; }

        private bool _materialUploaded;
        private Vector4 _uploadedColor;

        /// <summary>排队的一条绘制：逐物体常量 + 纹理 + 排序键 + 原始次序（同键时保持稳定排序）。</summary>
        private struct Entry
        {
            public UrpDrawData Data;
            public Texture2D Texture;
            public float SortKey;
            public int Order;
        }

        /// <summary>创建 SRP-Batcher 式批处理器（只给设备；材质 / 排序 / 相机矩阵在 <see cref="Begin"/> 里给）。</summary>
        public UrpBatch(GraphicsDevice device)
        {
            ArgumentNullException.ThrowIfNull(device);
            _device = device;
        }

        /// <summary>当前后端是否支持 SRP-Batcher 式的 UBO 绘制（WebGL2 支持；WebGPU 尚未接入）。</summary>
        public bool IsSupported => _device.Backend.SupportsUrpBatching;

        /// <summary>单段能画的物体上限（逐物体缓冲按需增长，Begin 之后可读当前值）。</summary>
        public int Capacity => _impl?.Capacity ?? 0;

        /// <summary>本次 Begin 之后已加入的物体数。</summary>
        public int ObjectCount => _entries.Count;

        /// <summary>
        /// 开一批（与 <see cref="SpriteBatch.Begin(Material, SpriteSortMode, Matrix4x4?)"/> 对应）：
        /// 材质、渲染顺序与相机矩阵都在这里给。
        /// </summary>
        /// <param name="material">材质（混合 / 采样 / 深度 / 剔除状态 + <see cref="Material.Effect"/> 自定义着色器）；
        /// 为 null 时用精灵默认状态与内置片元着色器。逐材质常量请用 <see cref="MaterialColor"/>（它进 <c>UnityPerMaterial</c> 常量块）。</param>
        /// <param name="sortMode">渲染顺序（<see cref="SpriteSortMode"/>）：Deferred = 保持 Draw 调用顺序；
        /// Texture = 按纹理排序（让同纹理的物体连在一起，减少绑定切换）；FrontToBack / BackToFront = 按 layerDepth 排序；
        /// Immediate = 每笔当场提交。</param>
        /// <param name="transformMatrix">相机矩阵（世界 → 屏幕的变换），null = 单位矩阵；与投影矩阵相乘后下发。</param>
        public void Begin(Material? material = null, SpriteSortMode sortMode = SpriteSortMode.Deferred,
                          Matrix4x4? transformMatrix = null)
        {
            if (_begun) throw new InvalidOperationException("上一次 Begin 还没有对应的 End。");

            // 材质快照：复用同一个 Material 实例（照 SpriteBatch 的 _cache_Mat），逐批零分配。
            _material.Reset();
            if (material is not null)
            {
                _material.Effect = material.Effect;
                _material.Blend = material.Blend;
                _material.Sampler = material.Sampler;
                _material.DepthStencil = material.DepthStencil;
                _material.Rasterizer = material.Rasterizer;
            }

            // 着色器：来自 Material.Effect 的片元源码（null = 内置默认）。按源码缓存，故每帧 Begin 不会重编译。
            _impl = ResolveProgram(material?.Effect?.FragmentSource);
            if (_impl is null)
                throw new NotSupportedException(
                    $"后端「{_device.Backend.Name}」尚未接入 SRP-Batcher 式的 UBO 绘制。");

            _sortMode = sortMode;
            _transform = transformMatrix ?? Matrix4x4.Identity;
            _projection = _device.CreateSpriteProjection();
            _transformProjection = _transform * _projection;

            // ---- 批内不变的渲染状态，在这里一次性下发 ----
            // 混合 / 深度 / 剔除 / 采样在整个 Begin/End 期间都不会变（一个批只有一个材质），
            // 所以不必每段（更不必每个物体）重复下发；提交时每段只换纹理（见 FlushQueued）。
            // 本路的着色器程序由 _impl 自己管理（它不在 Material.Effect 上），故此处的职责只是状态。
            _device.ApplyRenderStates(_material);

            _entries.Clear();
            _begun = true;
        }

        /// <summary>取（必要时创建）指定片元源码对应的 URP 程序；同一个源码只创建一次。</summary>
        private IUrpProgram? ResolveProgram(string? fragmentSource)
        {
            string key = fragmentSource ?? string.Empty;
            if (!_programs.TryGetValue(key, out IUrpProgram? program))
            {
                program = _device.Backend.CreateUrpProgram(fragmentSource);
                _programs.Add(key, program);
            }
            return program;
        }

        /// <summary>
        /// 加入一个物体（本类唯一的 Draw 重载）。参数语义与
        /// <see cref="SpriteBatch.Draw(Texture2D, Rectangle, Rectangle?, Color, float, Vector2, SpriteEffects, float)"/>
        /// 完全一致（targetRectangle 的 X/Y 是锚点落点、Width/Height 是基准尺寸，null = 落点在原点、尺寸取源尺寸；scale 在矩形尺寸之上再做额外缩放）。
        /// </summary>
        public void Draw(Texture2D texture, Rectangle? targetRectangle, Rectangle? sourceRectangle, Color color, float rotation,
                         Vector2 origin, Vector2 scale, SpriteEffects effects, float layerDepth = 0f)
        {
            ArgumentNullException.ThrowIfNull(texture);
            if (!_begun) throw new InvalidOperationException("Draw 必须在 Begin / End 之间调用。");

            Rectangle source = sourceRectangle ?? new Rectangle(0, 0, texture.Width, texture.Height);
            Vector2 position = targetRectangle.HasValue ? new Vector2(targetRectangle.Value.X, targetRectangle.Value.Y) : Vector2.Zero;
            float w = (targetRectangle?.Width ?? source.Width) * scale.X;
            float h = (targetRectangle?.Height ?? source.Height) * scale.Y;

            // 照 SpriteBatch：origin 是源纹理上的像素锚点，按 源尺寸 → 实际尺寸 的比例放大后再定位。
            origin = new Vector2(source.Width == 0 ? 0f : origin.X * w / source.Width, source.Height == 0 ? 0f : origin.Y * h / source.Height);

            // UV（含图集 Bounds 偏移与翻转）：与 SpriteBatch 共用同一份实现。
            SpriteBatch.ComputeUv(texture, sourceRectangle, effects, out Vector2 uvTL, out Vector2 uvBR);

            _entries.Add(new Entry
            {
                Data = new UrpDrawData
                {
                    ObjectToWorld = GpuInstance.CreateObjectToWorld(position, origin, new Vector2(w, h), rotation),
                    // 翻转已体现在 uvTL / uvBR 的交换里：UV 尺寸写成负值，着色器插值方向自然就反了。
                    UvRect = new Vector4(uvTL.X, uvTL.Y, uvBR.X - uvTL.X, uvBR.Y - uvTL.Y),
                    Tint = new Vector4(color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f),
                },
                Texture = texture,
                SortKey = SortKeyFor(texture, layerDepth),
                Order = _entries.Count,
            });

            if (_sortMode == SpriteSortMode.Immediate) FlushQueued();
        }

        /// <summary>
        /// 提交本批：先（必要时）上传一次材质常量，然后按"连续同一张纹理"切段，
        /// 每段整段上传逐物体常量 + 逐个 <c>drawElements</c>（每次只重绑一次 UBO 范围）。
        /// 返回本次的 DrawCall 数（= 物体数）。
        /// </summary>
        public int End()
        {
            if (!_begun) throw new InvalidOperationException("End 必须在 Begin 之后调用。");
            _begun = false;

            return FlushQueued();
        }

        /// <summary>
        /// 把已排队的物体提交掉（Immediate 模式下每笔 Draw 都会调它）。
        /// <para>
        /// <b>每段只换纹理</b>：渲染状态已在 <see cref="Begin"/> 里下发过一次（批内不变），
        /// 这里不再重复 —— 这正是"一个批一个材质"的红利。
        /// </para>
        /// </summary>
        private int FlushQueued()
        {
            if (_entries.Count == 0) return 0;
            if (_impl is null) { _entries.Clear(); return 0; }

            if (_sortMode != SpriteSortMode.Deferred && _sortMode != SpriteSortMode.Immediate)
                _entries.Sort(EntryComparison);

            // 材质常量：材质/值没变就一次都不传（这就是"每材质一份常驻常量缓冲"）。
            bool uploadMaterial = !_materialUploaded || MaterialColor != _uploadedColor;
            if (uploadMaterial)
            {
                _materialUploaded = true;
                _uploadedColor = MaterialColor;
                MaterialUploads++;
            }

            if (_scratch.Length < _entries.Count)
                _scratch = new UrpDrawData[Math.Max(_entries.Count, GraphicsDevice.MaxBatchSize)];

            Matrix4x4 transform = _transformProjection;
            Vector4 color = MaterialColor;
            int draws = 0;

            int start = 0;
            while (start < _entries.Count)
            {
                // 连续同一张纹理拼成一段：段内只需绑定一次纹理。
                Texture2D texture = _entries[start].Texture;
                int end = start + 1;
                while (end < _entries.Count && ReferenceEquals(_entries[end].Texture, texture)) end++;

                int runLength = end - start;
                for (int i = 0; i < runLength; i++) _scratch[i] = _entries[start + i].Data;

                // 逐物体缓冲在后端按需增长，所以这里不分块：一段一次性上传 + N 次 drawElements。
                draws += _device.DrawUrpSegment(_impl, transform, _scratch.AsSpan(0, runLength), runLength,
                                                texture, color, uploadMaterial);
                uploadMaterial = false;   // 材质常量整批只传一次，后续段共用同一份常驻缓冲

                start = end;
            }

            _entries.Clear();
            return draws;
        }

        /// <summary>排序键：与 <see cref="SpriteBatch"/> 一致。Texture 模式用 Texture.SortingKey，前后排序用 layerDepth。</summary>
        private float SortKeyFor(Texture2D texture, float layerDepth) => _sortMode switch
        {
            SpriteSortMode.Texture => texture.SortingKey,
            SpriteSortMode.FrontToBack => layerDepth,
            SpriteSortMode.BackToFront => -layerDepth,
            _ => 0f,
        };

        /// <summary>物体排序：先按排序键，键相同再按原始次序（等价于稳定排序）。</summary>
        private static int CompareEntries(Entry a, Entry b)
        {
            int byKey = a.SortKey.CompareTo(b.SortKey);
            return byKey != 0 ? byKey : a.Order.CompareTo(b.Order);
        }

        public void Dispose()
        {
            foreach (IUrpProgram? program in _programs.Values) program?.Dispose();
            _programs.Clear();
            _impl = null;
        }
    }
}
