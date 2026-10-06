using System.Collections.Generic;

namespace KFramework.MonoGame
{
    /// <summary>
    /// 「属性块 → 顶点通道」的映射表：把 MaterialPropertyBlock 里的属性值搬进顶点流，
    /// 使"同一材质 + 每个物体不同属性"仍然只产生一次 DrawCall。
    /// <para>
    /// 由 <see cref="Material.SetSpriteChannels"/> 声明：最多 8 个通道，按声明顺序依次填
    /// <c>aParams0.xyzw</c> → <c>aParams1.xyzw</c>。每项形如 <c>"uTint.rgb"</c>（3 个分量占 3 个通道）
    /// 或 <c>"uPulse"</c>（1 个通道，取 x）。
    /// </para>
    /// <para>
    /// 通道是 8 位归一化（0~1，精度 1/255）：颜色天然合适；需要"更大范围 / 更细精度"的标量
    /// （枚举、角度、时间等）请在着色器里按比例还原，例如 <c>angle = 通道值 * 6.2831853</c>。
    /// </para>
    /// <para>
    /// 只有 <see cref="MaterialPropertyType.Float"/> / <see cref="MaterialPropertyType.Int"/> /
    /// <see cref="MaterialPropertyType.Vector"/> / <see cref="MaterialPropertyType.Color"/> 能进通道；
    /// <b>矩阵与纹理进不去</b> —— 含这类属性的块只能走 uniform 路径（该次绘制会切批）。
    /// </para>
    /// </summary>
    internal sealed class SpriteChannelMap
    {
        /// <summary>通道数（= <see cref="SpriteParams.SizeInBytes"/>，与顶点里的 8 个字节一一对应）。</summary>
        public const int ChannelCount = SpriteParams.SizeInBytes;

        /// <summary>每个通道对应的属性名（null = 该通道未映射）。</summary>
        private readonly string?[] _names = new string?[ChannelCount];

        /// <summary>每个通道取该属性的哪个分量（'x' / 'y' / 'z' / 'w'）。</summary>
        private readonly char[] _components = new char[ChannelCount];

        /// <summary>编码时复用的 8 个槽位（避免每次绘制都分配）。</summary>
        private readonly float[] _slots = new float[ChannelCount];

        /// <summary>被映射到的属性名集合（用于判断"块里有没有映射不了的属性"）。</summary>
        private readonly HashSet<string> _mappedNames = new(StringComparer.Ordinal);

        /// <summary>是否一个通道都没声明。</summary>
        public bool IsEmpty { get; private set; } = true;

        /// <summary>该属性名是否被映射（同一属性名只要有任一分量被映射就算）。</summary>
        public bool IsMapped(string propertyName) => _mappedNames.Contains(propertyName);

        /// <summary>设置映射：按顺序填通道，超过 8 个通道或出现不认识的分量会抛异常（早失败好过静默画错）。</summary>
        public void Set(params string[] paths)
        {
            for (int i = 0; i < ChannelCount; i++)
            {
                _names[i] = null;
                _components[i] = 'x';
            }
            _mappedNames.Clear();

            int slot = 0;
            foreach (string path in paths)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(path);

                int dot = path.IndexOf('.');
                string name = dot < 0 ? path : path[..dot];
                string components = dot < 0 ? "x" : path[(dot + 1)..];
                if (components.Length == 0) components = "x";

                _mappedNames.Add(name);

                foreach (char component in components)
                {
                    if (slot >= ChannelCount)
                        throw new InvalidOperationException(
                            $"顶点通道最多 {ChannelCount} 个（声明到 \"{path}\" 时已超出）。");

                    _names[slot] = name;
                    _components[slot] = component switch
                    {
                        'r' or 'R' or 'x' or 'X' => 'x',
                        'g' or 'G' or 'y' or 'Y' => 'y',
                        'b' or 'B' or 'z' or 'Z' => 'z',
                        'a' or 'A' or 'w' or 'W' => 'w',
                        _ => throw new InvalidOperationException($"不认识的分量「{component}」（用 xyzw 或 rgba）。"),
                    };
                    slot++;
                }
            }

            IsEmpty = slot == 0;
        }

        /// <summary>
        /// 编码出这一次绘制的 8 个通道值：映射过的槽位取「属性块 → 材质基线」里对应属性的分量；
        /// 未映射的槽位沿用调用方显式给的 <paramref name="explicitParams"/>（那就是"直接给顶点参数"的用法）。
        /// </summary>
        public SpriteParams Encode(MaterialPropertyBlock? block, MaterialProperties baseline, SpriteParams explicitParams)
        {
            for (int i = 0; i < ChannelCount; i++)
            {
                string? name = _names[i];
                _slots[i] = name is null
                    ? ExplicitComponent(explicitParams, i)
                    : ReadComponent(block, baseline, name, _components[i]);
            }

            return new SpriteParams(_slots[0], _slots[1], _slots[2], _slots[3],
                                    _slots[4], _slots[5], _slots[6], _slots[7]);
        }

        /// <summary>未映射通道沿用显式顶点参数（8 位值还原成 0~1）。</summary>
        private static float ExplicitComponent(in SpriteParams parameters, int channel) => channel switch
        {
            0 => parameters.P0 / 255f,
            1 => parameters.P1 / 255f,
            2 => parameters.P2 / 255f,
            3 => parameters.P3 / 255f,
            4 => parameters.P4 / 255f,
            5 => parameters.P5 / 255f,
            6 => parameters.P6 / 255f,
            _ => parameters.P7 / 255f,
        };

        /// <summary>读一个分量：先看块（逐次绘制的覆盖值），再看材质基线，都没有就 0。</summary>
        private static float ReadComponent(MaterialPropertyBlock? block, MaterialProperties baseline, string name, char component)
        {
            if (block is not null && block.TryGetProperty(name, out MaterialProperty property) && TryRead(property, component, out float value))
                return Math.Clamp(value, 0f, 1f);

            if (baseline.TryGetProperty(name, out MaterialProperty baseProperty) && TryRead(baseProperty, component, out float baseValue))
                return Math.Clamp(baseValue, 0f, 1f);

            return 0f;
        }

        /// <summary>按属性类型取分量；矩阵 / 纹理返回 false（进不了通道）。</summary>
        private static bool TryRead(in MaterialProperty property, char component, out float value)
        {
            switch (property.Type)
            {
                case MaterialPropertyType.Float:
                    value = property.Float;
                    return true;
                case MaterialPropertyType.Int:
                    value = property.Int;
                    return true;
                case MaterialPropertyType.Color:
                case MaterialPropertyType.Vector:
                    value = component switch
                    {
                        'y' => property.Vector.Y,
                        'z' => property.Vector.Z,
                        'w' => property.Vector.W,
                        _ => property.Vector.X,
                    };
                    return true;
                default:
                    value = 0f;
                    return false;
            }
        }
    }
}
