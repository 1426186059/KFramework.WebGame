using System.Numerics;
using System.Runtime.InteropServices;

namespace KFramework.MonoGame
{
    /// <summary>
    /// GPU 实例化绘制器：<b>一次 DrawCall 画出 N 个"同材质 + 同纹理"的精灵</b>，每个实例可以有自己的
    /// 位置、尺寸、旋转、颜色和 8 个逐实例参数。
    /// <para>
    /// 与既有两条路的关系（三者都是"同材质"，但代价不同）：
    /// <list type="table">
    ///   <item><term>SpriteBatch + MaterialPropertyBlock（块属性映射进顶点通道）</term><description>同样 1 次 DrawCall，但几何在 CPU 侧按精灵展开（每精灵 4 顶点 × 28 字节）。</description></item>
    ///   <item><term>SpriteBatch + SpriteParams（逐顶点参数）</term><description>几何在 CPU 侧展开，按纹理分批；参数随顶点走，不打断合批。每精灵 4×28 字节。</description></item>
    ///   <item><term>SpriteInstancer（本类，GPU 实例化）</term><description>几何只有 4 个顶点的单位四边形；每实例 32 字节；一次 draw 覆盖整批。DrawCall 与 CPU 带宽都最省。</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// 代价：一次 draw 只能一张纹理（图集同页可以，跨页要分多次）、逐实例不能改渲染状态 / shader 关键字。
    /// </para>
    /// <para>
    /// 用法：
    /// <code>
    /// using var instancer = new SpriteInstancer(Device, atlas);
    /// instancer.Begin();
    /// for (int i = 0; i &lt; 512; i++)
    ///     instancer.Add(center, size, rotation, Color.White, SpriteParams.FromColor(tint));
    /// int drawCalls = instancer.End();   // 512 个精灵 → 1 次 draw
    /// </code>
    /// </para>
    /// </summary>
    public sealed class SpriteInstancer : IDisposable
    {
        private readonly GraphicsDevice _device;
        private readonly Texture2D _texture;
        private readonly Material _material;
        private readonly ISpriteInstancer? _impl;
        private readonly List<SpriteInstance> _instances = new();

        /// <summary>本批实例共用的 UV 矩形（取自纹理的 <see cref="Texture2D.Bounds"/>，图集子区域视图也能取到自己那一块）。</summary>
        private readonly Vector4 _uvRect;
        private bool _begun;

        /// <summary>
        /// 创建实例化绘制器。
        /// </summary>
        /// <param name="device">图形设备。</param>
        /// <param name="texture">本批实例共用的纹理（图集子区域视图也可以，UV 自动取其 Bounds）。</param>
        /// <param name="material">材质（绑定/采样/深度/剔除状态与着色器）；为 null 时用精灵默认状态。</param>
        /// <param name="fragmentSource">自定义片元着色器（GLSL ES 3.00）。为 null 时用默认的"纹理 × 逐实例颜色"。
        /// 自定义着色器可以读 <c>vParams0</c> / <c>vParams1</c>（即每实例的 8 个参数）与 <c>vColor</c>。</param>
        /// <param name="capacity">实例缓冲容量（超出时按容量分批绘制）。</param>
        public SpriteInstancer(GraphicsDevice device, Texture2D texture, Material? material = null,
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

            _impl = device.Backend.CreateInstancer(fragmentSource, capacity);
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

        public void Add(Vector2 center, Vector2 size, SpriteParams parameters = default)
            => Add(center, size, 0f, Color.White, parameters);

        public void Add(Vector2 center, Vector2 size, float rotation, SpriteParams parameters = default)
            => Add(center, size, rotation, Color.White, parameters);

        public void Add(Vector2 center, Vector2 size, float rotation, Color tint, SpriteParams parameters = default)
        {
            if (!_begun) throw new InvalidOperationException("Add 必须在 Begin / End 之间调用。");
            _instances.Add(new SpriteInstance(center, size, rotation, tint, parameters, _uvRect));
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
            Span<SpriteInstance> span = CollectionsMarshal.AsSpan(_instances);

            int draws = 0;
            for (int offset = 0; offset < span.Length; offset += _impl.Capacity)
            {
                int count = Math.Min(_impl.Capacity, span.Length - offset);
                _device.DrawInstanced(_impl, _material, projection, span.Slice(offset, count), count, _texture);
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
