using System;
using System.Collections.Generic;
using System.Numerics;

namespace KFramework.MonoGame
{
    /// <summary>
    /// 「着色器属性 → 实例通道」的映射表：由 <see cref="Material.SetInstanceChannels"/> 声明，
    /// 决定哪些属性值被打进 <see cref="SpriteInstance.Inst0"/> / <see cref="SpriteInstance.Inst1"/>
    /// 这 8 个 float 槽（着色器里是 <c>aInst0/aInst1</c> → <c>vInst0/vInst1</c>）。
    /// <para>
    /// 这是 Unity「实例化属性」的等价物：Unity 在 shader 的 <c>UNITY_INSTANCING_BUFFER</c> 里声明，
    /// 本引擎的实例化着色器是固定的，故把声明放在材质上。每项形如 <c>"uTint.rgb"</c>（占 3 个槽）
    /// 或 <c>"uPhase"</c>（1 个槽，取 x）。
    /// </para>
    /// <para>
    /// 只有 float / int / 向量 / 颜色能进通道；矩阵与纹理进不去 ——
    /// <see cref="SpriteBatchGPUInstance.Add"/> 传进来的块里出现没被声明的属性时会直接抛异常
    /// （实例化路径没有 uniform 覆盖层，装不下的东西无处可去；早失败好过静默画错）。
    /// </para>
    /// </summary>
    internal sealed class InstanceChannelMap
    {
        private const int ChannelCount = SpriteInstance.InstanceChannelCount;

        /// <summary>每个通道对应的属性名（null = 该通道未映射）。</summary>
        private readonly string?[] _names = new string?[ChannelCount];

        /// <summary>每个通道取该属性的哪个分量（0~3 = x/y/z/w）。</summary>
        private readonly int[] _components = new int[ChannelCount];

        /// <summary>被声明到的属性名集合（用于判断"块里有没有装不下的属性"）。</summary>
        private readonly HashSet<string> _declaredNames = new(StringComparer.Ordinal);

        /// <summary>编码时复用的 8 个槽位（避免每次绘制都分配）。</summary>
        private readonly float[] _slots = new float[ChannelCount];

        /// <summary>是否一个通道都没声明。</summary>
        public bool IsEmpty { get; private set; } = true;

        /// <summary>该属性名是否被声明进实例通道（同一属性名只要有任一分量被声明就算）。</summary>
        public bool IsDeclared(string propertyName) => _declaredNames.Contains(propertyName);

        /// <summary>设置映射：按顺序填通道；超过 8 个通道或出现不认识的分量会抛异常（早失败好过静默画错）。</summary>
        public void Set(params string[] paths)
        {
            for (int i = 0; i < ChannelCount; i++)
            {
                _names[i] = null;
                _components[i] = 0;
            }
            _declaredNames.Clear();

            int slot = 0;
            foreach (string path in paths)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(path);

                int dot = path.IndexOf('.');
                string name = dot < 0 ? path : path[..dot];
                string components = dot < 0 ? "x" : path[(dot + 1)..];
                if (components.Length == 0) components = "x";

                _declaredNames.Add(name);

                foreach (char component in components)
                {
                    if (slot >= ChannelCount)
                        throw new InvalidOperationException(
                            $"实例通道最多 {ChannelCount} 个 float 槽（声明到 \"{path}\" 时已超出）。");

                    _names[slot] = name;
                    _components[slot] = component switch
                    {
                        'r' or 'R' or 'x' or 'X' => 0,
                        'g' or 'G' or 'y' or 'Y' => 1,
                        'b' or 'B' or 'z' or 'Z' => 2,
                        'a' or 'A' or 'w' or 'W' => 3,
                        _ => throw new InvalidOperationException($"不认识的分量「{component}」（用 xyzw 或 rgba）。"),
                    };
                    slot++;
                }
            }

            IsEmpty = slot == 0;
        }

        /// <summary>
        /// 编码出这一个实例的 8 个通道值：优先取 <paramref name="block"/>（逐精灵覆盖值），
        /// 没设过则取 <paramref name="defaults"/>（效果自带的默认值），都没有就是 0。
        /// </summary>
        public void Encode(ShaderPropertyBlock? block, ShaderProperties? defaults, out Vector4 inst0, out Vector4 inst1)
        {
            float[] slots = _slots;
            for (int i = 0; i < ChannelCount; i++)
            {
                string? name = _names[i];
                slots[i] = name is null ? 0f : ReadComponent(block, defaults, name, _components[i]);
            }

            inst0 = new Vector4(slots[0], slots[1], slots[2], slots[3]);
            inst1 = new Vector4(slots[4], slots[5], slots[6], slots[7]);
        }

        /// <summary>读一个分量：先看块（这一次绘制的覆盖值），再看效果自带的默认值，都没有就 0。</summary>
        private static float ReadComponent(ShaderPropertyBlock? block, ShaderProperties? defaults, string name, int component)
        {
            if (block is not null && block.TryGetProperty(name, out ShaderProperty property)) return Read(property, component);
            if (defaults is not null && defaults.TryGetProperty(name, out ShaderProperty baseProperty)) return Read(baseProperty, component);
            return 0f;
        }

        /// <summary>按属性类型取分量；矩阵 / 纹理返回 0（它们进不了实例通道）。</summary>
        private static float Read(in ShaderProperty property, int component)
        {
            switch (property.Type)
            {
                case ShaderPropertyType.Float:
                    return property.Float;
                case ShaderPropertyType.Int:
                    return property.Int;
                case ShaderPropertyType.Color:
                case ShaderPropertyType.Vector:
                    return component switch
                    {
                        1 => property.Vector.Y,
                        2 => property.Vector.Z,
                        3 => property.Vector.W,
                        _ => property.Vector.X,
                    };
                default:
                    return 0f;
            }
        }
    }
}
