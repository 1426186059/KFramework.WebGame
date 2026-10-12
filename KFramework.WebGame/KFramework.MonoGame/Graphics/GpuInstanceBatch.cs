using System.Numerics;

namespace KFramework.MonoGame
{
    public sealed class GpuInstanceBatch : IDisposable
    {
        private readonly GraphicsDevice _device;

        /// <summary>本批的材质快照（照 <see cref="SpriteBatch"/> 的 <c>_cache_Mat</c>：复用同一个材质，每帧零分配）。</summary>
        private Material _material = null;

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

        private Matrix4x4 _transformProjection;
        private bool _begun;

        private struct Entry
        {
            public GpuInstance Data;
            public Texture2D Texture;
            public float SortKey;
            public int Order;
        }

        public GpuInstanceBatch(GraphicsDevice device)
        {
            ArgumentNullException.ThrowIfNull(device);
            _device = device;
        }

        public bool IsSupported => _device.Backend.SupportsGpuInstancing;
        public int Capacity => _impl?.Capacity ?? 0;
        public int InstanceCount => _entries.Count;
        
        public void Begin(Material material, SpriteSortMode sortMode = SpriteSortMode.Deferred,
                          Matrix4x4? transformMatrix = null)
        {
            if (_begun)
            {
                throw new InvalidOperationException("上一次 Begin 还没有对应的 End。");
            }

            _material = material;
            _impl = ResolveProgram(material?.Effect?.FragmentSource);
            if (_impl is null)
            {
                throw new NotSupportedException($"后端「{_device.Backend.Name}」尚未接入 GPU 实例化。");
            }

            _sortMode = sortMode;
            _transform = transformMatrix ?? Matrix4x4.Identity;
            _projection = _device.CreateSpriteProjection();
            _transformProjection = _transform * _projection;

            _device.ApplyRenderStates(_material);
            _entries.Clear();

            _begun = true;
        }

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
        
        public void Draw(Texture2D texture, Rectangle? targetRectangle, Rectangle? sourceRectangle, Color color,
                         float rotation, Vector2 origin, Vector2 scale, SpriteEffects effects, float layerDepth = 0f)
        {
            ArgumentNullException.ThrowIfNull(texture);
            if (!_begun) throw new InvalidOperationException("Draw 必须在 Begin / End 之间调用。");

            Rectangle source = sourceRectangle ?? new Rectangle(0, 0, texture.Width, texture.Height);
            Vector2 position = targetRectangle.HasValue
                ? new Vector2(targetRectangle.Value.X, targetRectangle.Value.Y)
                : Vector2.Zero;
            float w = (targetRectangle?.Width ?? source.Width) * scale.X;
            float h = (targetRectangle?.Height ?? source.Height) * scale.Y;

            origin = new Vector2(source.Width == 0 ? 0f : origin.X * w / source.Width,
                                 source.Height == 0 ? 0f : origin.Y * h / source.Height);

            SpriteBatch.ComputeUv(texture, sourceRectangle, effects, out Vector2 uvTL, out Vector2 uvBR);
            GpuInstance instance = new GpuInstance(
                GpuInstance.CreateObjectToWorld(position, origin, new Vector2(w, h), rotation),
                color,
                new Vector4(uvTL.X, uvTL.Y, uvBR.X - uvTL.X, uvBR.Y - uvTL.Y));

            _entries.Add(new Entry
            {
                Data = instance,
                Texture = texture,
                SortKey = SortKeyFor(texture, layerDepth),
                Order = _entries.Count,
            });

        }

        /// <summary>提交并按"纹理段 + 容量"分批发起实例化绘制，返回本次的 DrawCall 数。</summary>
        public int End()
        {
            if (!_begun) throw new InvalidOperationException("End 必须在 Begin 之后调用。");
            _begun = false;

            return FlushQueued();
        }

        /// <summary>
        /// 把已排队的实例提交掉：排序（非 Deferred 时）→ 按"连续同一张纹理"切段 →
        /// 每段拷成连续数组、按容量分块 → 每块一次实例化 draw。只由 <see cref="End"/> 调用。
        /// </summary>
        private int FlushQueued()
        {
            if (_entries.Count == 0)
            {
                return 0;
            }

            if (_impl is null) 
            { 
                _entries.Clear(); 
                return 0; 
            }

            if (_sortMode != SpriteSortMode.Deferred)
            {
                _entries.Sort(EntryComparison);
            }

            if (_scratch.Length < _entries.Count)
            {
                _scratch = new GpuInstance[Math.Max(_entries.Count, GraphicsDevice.MaxBatchSize)];
            }

            Matrix4x4 transform = _transformProjection;
            int draws = 0;

            int start = 0;
            while (start < _entries.Count)
            {
                // 连续同一张纹理拼成一段：一次实例化 draw 只能绑一张纹理，段内才能合并成一次 draw。
                Texture2D texture = _entries[start].Texture;
                int end = start + 1;
                while (end < _entries.Count && ReferenceEquals(_entries[end].Texture, texture))
                {
                    end++;
                }

                int runLength = end - start;
                for (int i = 0; i < runLength; i++)
                {
                    _scratch[i] = _entries[start + i].Data;
                }

                for (int offset = 0; offset < runLength; offset += _impl.Capacity)
                {
                    int count = Math.Min(_impl.Capacity, runLength - offset);
                    _device.DrawGpuInstances(
                        _impl, 
                        transform, 
                        _scratch.AsSpan(offset, count), 
                        count, 
                        texture);

                    draws++;
                }

                start = end;
            }

            _entries.Clear();
            return draws;
        }

        private float SortKeyFor(Texture2D texture, float layerDepth) => _sortMode switch
        {
            SpriteSortMode.Texture => texture.SortingKey,
            SpriteSortMode.FrontToBack => layerDepth,
            SpriteSortMode.BackToFront => -layerDepth,
            _ => 0f,
        };

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
