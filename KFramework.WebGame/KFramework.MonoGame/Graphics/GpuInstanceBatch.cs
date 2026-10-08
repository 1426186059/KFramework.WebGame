using System.Numerics;
using System.Runtime.InteropServices;

namespace KFramework.MonoGame
{
    /// <summary>
    /// GPU 实例化绘制器：<b>一次 DrawCall 画出 N 个"同材质 + 同纹理"的精灵</b>，每个实例带自己的
    /// <b>对象→世界矩阵</b>（照 Unity 的 <c>unity_ObjectToWorld</c>）、颜色、UV 矩形与逐实例属性。
    /// <para>
    /// <b>它和 <see cref="SpriteBatch"/> 是两条互不相干的路子</b>，不要混用：
    /// <list type="table">
    ///   <item><term>SpriteBatch（CPU 合批）</term><description>几何在 CPU 侧按精灵展开（每精灵 4 顶点 × 28 字节），按纹理/属性块分批，逐批一次 drawElements。
    ///   逐物体差异靠顶点数据（颜色/UV）与 <see cref="ShaderPropertyBlock"/>（覆盖 uniform，代价是切批）。</description></item>
    ///   <item><term>GpuInstanceBatch（本类，GPU 实例化）</term><description>几何只有 4 个顶点的单位四边形（静态），
    ///   逐实例矩阵/颜色/UV矩形按实例放进第二根缓冲（<c>vertexAttribDivisor = 1</c>）；一次 draw 覆盖整批。
    ///   DrawCall 与 CPU 带宽都最省，但一次 draw 只能一张纹理、逐实例不能改渲染状态。</description></item>
    /// </list>
    /// 选型：同屏同纹理的精灵数量大 → 用本类；需要逐批换材质状态、或用属性块覆盖任意 uniform → 用 <see cref="SpriteBatch"/>。
    /// </para>
    /// <para>
    /// <b>与 Unity 的对应关系</b>：Unity 是
    /// <c>Graphics.DrawMeshInstanced(mesh, submesh, material, Matrix4x4[] matrices, count, properties)</c>，
    /// 传的是矩阵数组；本类则是 <see cref="Begin"/> → <see cref="Add(in Matrix4x4, Color, ShaderPropertyBlock?)"/> × N →
    /// <see cref="End"/>，单位四边形（(0,0)-(1,1)）由引擎内置，逐实例属性走
    /// <see cref="Material.SetGpuInstanceChannels"/> + <see cref="ShaderPropertyBlock"/>（= Unity 的实例化属性）。
    /// 位置 / 尺寸 / 旋转只是矩阵的一种填法，<see cref="Add(Vector2, Vector2, float, Color, ShaderPropertyBlock?)"/>
    /// 是它的便捷重载（等价于 <see cref="GpuInstance.CreateObjectToWorld"/>）。
    /// </para>
    /// <para>
    /// 用法：
    /// <code>
    /// var material = new Material();
    /// material.SetGpuInstanceChannels("uPhase");     // 声明哪些属性进实例通道（8 个槽）
    ///
    /// using var instances = new GpuInstanceBatch(Device, atlas, material, fragmentSource);
    /// instances.Begin();
    /// for (int i = 0; i &lt; 512; i++)
    /// {
    ///     _block.Clear();
    ///     _block.SetFloat("uPhase", i / 512f);       // 逐实例不同值
    ///     instances.Add(center, size, rotation, tint, _block);
    /// }
    /// int drawCalls = instances.End();               // 512 个精灵 → 1 次 draw
    /// </code>
    /// </para>
    /// </summary>
    public sealed class GpuInstanceBatch : IDisposable
    {
        private readonly GraphicsDevice _device;
        private readonly Texture2D _texture;
        private readonly Material _material;
        private readonly IGpuInstanceProgram? _impl;
        private readonly List<GpuInstance> _instances = new();

        /// <summary>本批实例共用的 UV 矩形（取自纹理的 <see cref="Texture2D.Bounds"/>，图集子区域视图也能取到自己那一块）。</summary>
        private readonly Vector4 _uvRect;
        private bool _begun;

        /// <summary>
        /// 创建实例化绘制器。
        /// </summary>
        /// <param name="device">图形设备。</param>
        /// <param name="texture">本批实例共用的纹理（图集子区域视图也可以，UV 自动取其 Bounds）。</param>
        /// <param name="material">材质（绑定/采样/深度/剔除状态 + 实例通道声明）；为 null 时用精灵默认状态、且不能下发逐实例属性。</param>
        /// <param name="fragmentSource">自定义片元着色器（GLSL ES 3.00）。为 null 时用默认的"纹理 × 逐实例颜色"。
        /// 自定义着色器可以读 <c>vColor</c>（逐实例颜色）、<c>vTexCoord</c>、<c>vInstanceID</c>（实例号）
        /// 与 <c>vInst0/vInst1</c>（逐实例属性槽）。</param>
        /// <param name="capacity">实例缓冲容量（超出时按容量分批绘制）。</param>
        public GpuInstanceBatch(GraphicsDevice device, Texture2D texture, Material? material = null,
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
                // 实例通道声明随材质一起带走（照 Unity：声明属于材质），否则 Add(..., block) 无法编码。
                _material.GpuInstanceChannels = material.GpuInstanceChannels;
            }
            else
            {
                _material.Blend = BlendState.NonPremultiplied;
                _material.Sampler = SamplerState.Point;
                _material.DepthStencil = DepthStencilState.None;
                _material.Rasterizer = RasterizerState.CullNone;
            }

            _impl = device.Backend.CreateGpuInstanceProgram(fragmentSource, capacity);
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

        /// <summary>当前后端是否支持 GPU 实例化（WebGL2 支持；WebGPU 尚未接入）。</summary>
        public bool IsSupported => _impl is not null;

        /// <summary>单次 DrawCall 的实例上限。</summary>
        public int Capacity => _impl?.Capacity ?? 0;

        /// <summary>本次 Begin 之后已加入的实例数。</summary>
        public int InstanceCount => _instances.Count;

        public void Begin()
        {
            if (_begun) throw new InvalidOperationException("上一次 Begin 还没有对应的 End。");
            _instances.Clear();
            _begun = true;
        }

        public void Add(Vector2 center, Vector2 size)
            => Add(center, size, 0f, Color.White);

        public void Add(Vector2 center, Vector2 size, float rotation)
            => Add(center, size, rotation, Color.White);

        public void Add(Vector2 center, Vector2 size, float rotation, Color tint)
            => Add(center, size, rotation, tint, null);

        /// <summary>
        /// 便捷重载：按「中心点 / 尺寸 / 旋转弧度 / 颜色」加入一个实例
        /// （内部用 <see cref="GpuInstance.CreateObjectToWorld"/> 组出对象→世界矩阵），
        /// 并可选地带「逐实例属性」（照 Unity 的实例化属性）。
        /// <para>
        /// <paramref name="properties"/> 里被 <see cref="Material.SetGpuInstanceChannels"/> 声明过的属性会被编码进
        /// 逐实例数据（<c>aInst0/aInst1</c> → 片元着色器里的 <c>vInst0/vInst1</c>），
        /// 于是<b>每个实例各自持有自己的属性值，而整批仍然只有一次 DrawCall</b>。
        /// 内置片元着色器不读这两个 varying，要用自定义 <c>fragmentSource</c> 读才看得见。
        /// </para>
        /// <para>
        /// 块里出现<b>没被声明</b>的属性会抛 <see cref="InvalidOperationException"/>：实例化路径没有 uniform
        /// 覆盖层，未声明的属性（矩阵 / 纹理等）装不进 8 个通道。早失败好过静默画错。
        /// </para>
        /// </summary>
        public void Add(Vector2 center, Vector2 size, float rotation, Color tint, ShaderPropertyBlock? properties = null)
            => Add(GpuInstance.CreateObjectToWorld(center, size, rotation), tint, properties);

        /// <summary>
        /// 直接给「对象→世界矩阵」（照 Unity 的 <c>Graphics.DrawMeshInstanced(..., Matrix4x4[] matrices, ...)</c>）。
        /// <para>
        /// 矩阵把<b>单位四边形 (0,0)-(1,1)</b> 变换到屏幕，约定与本引擎一致（行主序 + 行向量 p' = p × M）：
        /// 想让精灵以 <paramref name="center"/> 为中心、按 size 缩放并旋转，用
        /// <see cref="GpuInstance.CreateObjectToWorld"/>；要做斜切 / 镜像等任意仿射，自己乘出来即可。
        /// </para>
        /// </summary>
        public void Add(in Matrix4x4 objectToWorld, Color tint, ShaderPropertyBlock? properties = null)
        {
            if (!_begun) throw new InvalidOperationException("Add 必须在 Begin / End 之间调用。");

            GpuInstance instance = new GpuInstance(objectToWorld, tint, _uvRect);

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

            _instances.Add(instance);
        }

        /// <summary>提交并按容量分批发起实例化绘制，返回本次的 DrawCall 数。</summary>
        public int End()
        {
            if (!_begun) throw new InvalidOperationException("End 必须在 Begin 之后调用。");
            _begun = false;

            if (_impl is null)
                throw new InvalidOperationException($"当前后端（{_device.Backend.Name}）尚未接入 GPU 实例化。");
            if (_instances.Count == 0) return 0;

            Matrix4x4 projection = CurrentProjection();
            Span<GpuInstance> span = CollectionsMarshal.AsSpan(_instances);

            int draws = 0;
            for (int offset = 0; offset < span.Length; offset += _impl.Capacity)
            {
                int count = Math.Min(_impl.Capacity, span.Length - offset);
                _device.DrawGpuInstances(_impl, _material, projection, span.Slice(offset, count), count, _texture);
                draws++;
            }

            _instances.Clear();
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
