using System.Numerics;
using System.Runtime.InteropServices;

namespace KFramework.MonoGame
{
    /// <summary>
    /// GPU 实例化绘制器：<b>每个"同纹理段"一次 DrawCall 画出 N 个精灵</b>，每个实例带自己的
    /// <b>对象→世界矩阵</b>（照 Unity 的 <c>unity_ObjectToWorld</c>）、颜色与 UV 矩形。
    /// <para>
    /// <b>API 形态与 <see cref="SpriteBatch"/> 完全一致</b>：构造只给设备，<see cref="Begin"/> 里给
    /// 材质 / 渲染顺序 / 相机矩阵，<see cref="Draw"/> 的参数（texture / targetRectangle / sourceRectangle /
    /// color / rotation / origin / scale / effects / layerDepth）与 <see cref="SpriteBatch"/> 的「目标矩形」重载
    /// （<see cref="SpriteBatch.Draw(Texture2D, Rectangle, Rectangle?, Color, float, Vector2, SpriteEffects, float)"/>）
    /// 逐参数对应 —— 三个批处理之间换用不必改调用代码。
    /// </para>
    /// <para>
    /// <b>纹理走 <see cref="Draw"/></b>：一次 <c>drawElementsInstanced</c> 只能绑一张纹理，所以提交时会
    /// <b>按纹理切段</b>（连续同一张纹理拼成一段，段内一次实例化 draw）—— 一次 Begin 可以画任意多张纹理，
    /// 代价是 DrawCall 数 = 纹理段数（可以配合 <see cref="SpriteSortMode.Texture"/> 让同纹理的物体连在一起）。
    /// 逐实例数据里放的是 UV 矩形（<see cref="GpuInstance.UvRect"/>）而不是纹理引用。
    /// </para>
    /// <para>
    /// <b>自定义着色器走 <see cref="Material.Effect"/></b>（和 <see cref="SpriteBatch"/> 同一条路子）：
    /// <c>var effect = Device.CreateShaderEffect(fragmentSource); material.Effect = effect;</c>
    /// 引擎从 <see cref="ShaderEffect.FragmentSource"/> 取回源码，为这条路创建对应的程序
    /// （顶点输入与 SpriteBatch 不同，不能复用普通精灵程序），并按源码缓存 —— <b>同一个源码只编译一次</b>。
    /// 注意：本路的片元着色器只能用到 varyings（<c>vColor</c> / <c>vTexCoord</c> / <c>vInst0</c> / <c>vInst1</c> /
    /// <c>vInstanceID</c>）与纹理，效果的 uniform 属性在这里不生效。
    /// </para>
    /// <para>
    /// <b>三条路的对照</b>：
    /// <list type="table">
    ///   <item><term><see cref="SpriteBatch"/>（CPU 合批）</term><description>几何在 CPU 侧展开（每精灵 4 顶点 × 28 字节），DrawCall = 纹理批数。</description></item>
    ///   <item><term>GpuInstanceBatch（本类，GPU 实例化）</term><description>几何只有 4 个顶点的单位四边形，逐实例数据走
    ///   <c>vertexAttribDivisor = 1</c> 的第二根缓冲：<b>DrawCall = 纹理段数</b>，CPU 带宽最省；
    ///   逐实例数据受顶点属性槽/格式限制（所以带了 <see cref="Material.SetGpuInstanceChannels"/> 那 8 个通道）。</description></item>
    ///   <item><term><see cref="UrpBatch"/>（SRP Batcher 式）</term><description>数据走 uniform buffer，什么格式都装得下、可逐笔换纹理，
    ///   但 DrawCall = 物体数（不降），换物体的开销 ≈ 一次 <c>bindBufferRange</c>。</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>逐实例属性</b>（照 Unity 的实例化属性）：材质上先用
    /// <see cref="Material.SetGpuInstanceChannels"/> 声明若干属性进 8 个实例通道，再给 <see cref="Draw"/>
    /// 传一个 <see cref="ShaderPropertyBlock"/> —— 值会被编码进逐实例数据，
    /// <b>每个实例各自持有自己的属性值，而不打断这一段实例化绘制</b>。
    /// </para>
    /// <para>用法：</para>
    /// <code>
    /// using var instances = new GpuInstanceBatch(Device);
    /// instances.Begin(material);                       // 材质（含 Material.Effect 自定义着色器）/ 排序 / 相机矩阵
    /// for (int i = 0; i &lt; 512; i++)
    ///     instances.Draw(atlas, targetRect, sourceRect, tint, rotation, origin, scale, SpriteEffects.None, layerDepth);
    /// int drawCalls = instances.End();                 // 512 个精灵（同一张纹理）→ 1 次 draw
    /// </code>
    /// </para>
    /// </summary>
    public sealed class GpuInstanceBatch : IDisposable
    {
        private readonly GraphicsDevice _device;

        /// <summary>本批的材质快照（照 <see cref="SpriteBatch"/> 的 <c>_cache_Mat</c>：复用同一个材质，每帧零分配）。</summary>
        private readonly Material _material = new();

        /// <summary>按片元源码缓存的后端程序：同一个源码只创建（编译）一次。</summary>
        private readonly Dictionary<string, IGpuInstanceProgram?> _programs = new();

        /// <summary>本批已排队的实例（按绘制顺序；排序模式非 Deferred 时在提交前排序）。</summary>
        private readonly List<Entry> _entries = new();

        /// <summary>提交时把实例拷成连续数组（实例缓冲要连续内存）。</summary>
        private GpuInstance[] _scratch = Array.Empty<GpuInstance>();

        /// <summary>缓存比较委托，避免每次排序分配。</summary>
        private static readonly Comparison<Entry> EntryComparison = CompareEntries;

        private IGpuInstanceProgram? _impl;
        private SpriteSortMode _sortMode;
        private Matrix4x4 _transform = Matrix4x4.Identity;
        private Matrix4x4 _projection;
        private bool _begun;

        /// <summary>排队的一条绘制：逐实例数据 + 纹理 + 排序键 + 原始次序（同键时保持稳定排序）。</summary>
        private struct Entry
        {
            public GpuInstance Data;
            public Texture2D Texture;
            public float SortKey;
            public int Order;
        }

        /// <summary>创建实例化绘制器（只给设备；材质 / 排序 / 相机矩阵在 <see cref="Begin"/> 里给，纹理在 <see cref="Draw"/> 里给）。</summary>
        public GpuInstanceBatch(GraphicsDevice device)
        {
            ArgumentNullException.ThrowIfNull(device);
            _device = device;
        }

        /// <summary>当前后端是否支持 GPU 实例化（WebGL2 支持；WebGPU 尚未接入）。</summary>
        public bool IsSupported => _device.Backend.SupportsGpuInstancing;

        /// <summary>单次实例化 draw 的实例上限（Begin 之后有效）。</summary>
        public int Capacity => _impl?.Capacity ?? 0;

        /// <summary>本次 Begin 之后已加入的实例数。</summary>
        public int InstanceCount => _entries.Count;

        /// <summary>
        /// 开一批（与 <see cref="SpriteBatch.Begin(Material, SpriteSortMode, Matrix4x4?)"/> 对应）：
        /// 材质、渲染顺序与相机矩阵都在这里给。
        /// </summary>
        /// <param name="material">材质（混合 / 采样 / 深度 / 剔除状态 + 实例通道声明 +
        /// <see cref="Material.Effect"/> 自定义着色器）；为 null 时用精灵默认状态与内置片元着色器。</param>
        /// <param name="sortMode">渲染顺序（<see cref="SpriteSortMode"/>）：Deferred = 保持 Draw 调用顺序；
        /// Texture = 按纹理排序键（让同纹理的物体连在一起，减少实例化 draw 次数）；FrontToBack / BackToFront = 按 layerDepth 排序；
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
                // 实例通道声明随材质走（否则 Draw(..., block) 无法编码）。
                _material.GpuInstanceChannels = material.GpuInstanceChannels;
            }

            // 着色器：来自 Material.Effect 的片元源码（null = 内置默认）。按源码缓存，故每帧 Begin 不会重编译。
            _impl = ResolveProgram(material?.Effect?.FragmentSource);
            if (_impl is null)
                throw new NotSupportedException($"后端「{_device.Backend.Name}」尚未接入 GPU 实例化。");

            _sortMode = sortMode;
            _transform = transformMatrix ?? Matrix4x4.Identity;
            _projection = _device.CreateSpriteProjection();
            _entries.Clear();
            _begun = true;
        }

        /// <summary>取（必要时创建）指定片元源码对应的实例化程序；同一个源码只创建一次。</summary>
        private IGpuInstanceProgram? ResolveProgram(string? fragmentSource)
        {
            string key = fragmentSource ?? string.Empty;
            if (!_programs.TryGetValue(key, out IGpuInstanceProgram? program))
            {
                program = _device.Backend.CreateGpuInstanceProgram(fragmentSource, GraphicsDevice.MaxBatchSize);
                _programs.Add(key, program);
            }
            return program;
        }

        /// <summary>
        /// 加入一个实例（本类唯一的 Draw 重载）。参数语义与 <see cref="SpriteBatch"/> 的「目标矩形」重载一致：
        /// <paramref name="targetRectangle"/> 的 X/Y 是<b>锚点落点</b>（即 <paramref name="origin"/> 落在哪），
        /// Width/Height 是基准尺寸（null = 落点在原点、尺寸取源尺寸）；
        /// <paramref name="scale"/> 在矩形尺寸之上再做额外缩放（默认 (1,1) 即精确落进矩形），
        /// <paramref name="origin"/> 是源纹理上的像素锚点（按 源尺寸 → 实际尺寸 的比例放大后定位）。
        /// <para>
        /// <paramref name="properties"/>：逐实例属性（照 Unity 的实例化属性）。里面被
        /// <see cref="Material.SetGpuInstanceChannels"/> 声明过的属性会被编码进逐实例数据
        /// （<c>aInst0/aInst1</c> → 片元着色器里的 <c>vInst0/vInst1</c>），于是
        /// <b>每个实例各自持有自己的属性值，而不打断这一段实例化绘制</b>。
        /// 块里出现<b>没被声明</b>的属性会抛 <see cref="InvalidOperationException"/>
        /// —— 实例化路径没有 uniform 覆盖层，装不下的属性无处可去（早失败好过静默画错）。
        /// </para>
        /// </summary>
        public void Draw(Texture2D texture, Rectangle? targetRectangle, Rectangle? sourceRectangle, Color color,
                         float rotation, Vector2 origin, Vector2 scale, SpriteEffects effects, float layerDepth = 0f,
                         ShaderPropertyBlock? properties = null)
        {
            ArgumentNullException.ThrowIfNull(texture);
            if (!_begun) throw new InvalidOperationException("Draw 必须在 Begin / End 之间调用。");

            Rectangle source = sourceRectangle ?? new Rectangle(0, 0, texture.Width, texture.Height);

            // 目标矩形给"位置 + 基准尺寸"；scale 在其之上再做额外缩放（默认 (1,1) 即精确落进该矩形）。
            // targetRectangle 为 null 时退化成"左上角在原点、尺寸 = 源尺寸 × scale"，等价于 SpriteBatch 的 position 用法。
            Vector2 position = targetRectangle.HasValue
                ? new Vector2(targetRectangle.Value.X, targetRectangle.Value.Y)
                : Vector2.Zero;
            float w = (targetRectangle?.Width ?? source.Width) * scale.X;
            float h = (targetRectangle?.Height ?? source.Height) * scale.Y;

            // 照 SpriteBatch：origin 是"源纹理上的像素锚点"，按 源尺寸 → 实际尺寸 的比例放大后再参与定位。
            origin = new Vector2(source.Width == 0 ? 0f : origin.X * w / source.Width,
                                 source.Height == 0 ? 0f : origin.Y * h / source.Height);

            // UV（含图集 Bounds 偏移与翻转）：与 SpriteBatch 共用同一份实现。
            SpriteBatch.ComputeUv(texture, sourceRectangle, effects, out Vector2 uvTL, out Vector2 uvBR);

            GpuInstance instance = new GpuInstance(
                GpuInstance.CreateObjectToWorld(position, origin, new Vector2(w, h), rotation),
                color,
                // 翻转已体现在 uvTL / uvBR 的交换里：UV 尺寸写成负值，着色器插值方向自然就反了。
                new Vector4(uvTL.X, uvTL.Y, uvBR.X - uvTL.X, uvBR.Y - uvTL.Y));

            if (properties is not null)
            {
                if (!_material.HasGpuInstanceChannels)
                    throw new InvalidOperationException(
                        "要按实例下发属性块的值，必须先在材质上用 SetGpuInstanceChannels 声明通道" +
                        "（照 Unity 的实例化属性：shader 里要有对应的 UNITY_INSTANCING_BUFFER 字段）。");

                GpuInstanceChannelMap channels = _material.GpuInstanceChannels!;

                // 校验块里没有"装不下的属性"。走 RawProperties（原始字典）而不是 Properties（IReadOnlyDictionary）：
                // 后者 foreach 会装箱字典枚举器 → 每实例一次堆分配，而这里是每帧跑 N 次的热路径。
                Dictionary<string, ShaderProperty>? raw = properties.RawProperties;
                if (raw is not null)
                {
                    foreach (KeyValuePair<string, ShaderProperty> pair in raw)
                    {
                        if (!channels.IsDeclared(pair.Key))
                            throw new InvalidOperationException(
                                $"属性「{pair.Key}」没有声明进实例通道（{nameof(Material.SetGpuInstanceChannels)}）：" +
                                "实例化路径只能下发被声明进 8 个通道的属性，矩阵 / 纹理装不下。");
                    }
                }

                channels.Encode(properties, _material.Effect, out Vector4 inst0, out Vector4 inst1);
                instance.Inst0 = inst0;
                instance.Inst1 = inst1;
            }

            _entries.Add(new Entry
            {
                Data = instance,
                Texture = texture,
                SortKey = SortKeyFor(texture, layerDepth),
                Order = _entries.Count,
            });

            if (_sortMode == SpriteSortMode.Immediate) FlushQueued();
        }

        /// <summary>提交并按"纹理段 + 容量"分批发起实例化绘制，返回本次的 DrawCall 数。</summary>
        public int End()
        {
            if (!_begun) throw new InvalidOperationException("End 必须在 Begin 之后调用。");
            _begun = false;

            return FlushQueued();
        }

        /// <summary>
        /// 把已排队的实例提交掉：排序（非 Deferred / Immediate 时）→ 按"连续同一张纹理"切段 →
        /// 每段拷成连续数组、按容量分块 → 每块一次实例化 draw。Immediate 模式下每笔 Draw 都会调它。
        /// </summary>
        private int FlushQueued()
        {
            if (_entries.Count == 0) return 0;
            if (_impl is null) { _entries.Clear(); return 0; }

            if (_sortMode != SpriteSortMode.Deferred && _sortMode != SpriteSortMode.Immediate)
                _entries.Sort(EntryComparison);

            if (_scratch.Length < _entries.Count)
                _scratch = new GpuInstance[Math.Max(_entries.Count, GraphicsDevice.MaxBatchSize)];

            Matrix4x4 transform = _transform * _projection;
            int draws = 0;

            int start = 0;
            while (start < _entries.Count)
            {
                // 连续同一张纹理拼成一段：一次实例化 draw 只能绑一张纹理，段内才能合并成一次 draw。
                Texture2D texture = _entries[start].Texture;
                int end = start + 1;
                while (end < _entries.Count && ReferenceEquals(_entries[end].Texture, texture)) end++;

                int runLength = end - start;
                for (int i = 0; i < runLength; i++) _scratch[i] = _entries[start + i].Data;

                for (int offset = 0; offset < runLength; offset += _impl.Capacity)
                {
                    int count = Math.Min(_impl.Capacity, runLength - offset);
                    _device.DrawGpuInstances(_impl, _material, transform, _scratch.AsSpan(offset, count), count, texture);
                    draws++;
                }

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

        /// <summary>实例排序：先按排序键，键相同再按原始次序（等价于稳定排序）。</summary>
        private static int CompareEntries(Entry a, Entry b)
        {
            int byKey = a.SortKey.CompareTo(b.SortKey);
            return byKey != 0 ? byKey : a.Order.CompareTo(b.Order);
        }

        public void Dispose()
        {
            foreach (IGpuInstanceProgram? program in _programs.Values) program?.Dispose();
            _programs.Clear();
            _impl = null;
        }
    }
}
