using System;
using System.Collections.Generic;
using System.Numerics;

namespace KFramework.MonoGame
{
    /// <summary>
    /// 材质属性表（着色器 uniform 的「名字 → 值」集合）：<see cref="Material"/> 与
    /// <see cref="MaterialPropertyBlock"/> 共用的那部分，对应 Unity 里 Material 与 MaterialPropertyBlock
    /// 都提供的那组 SetFloat / SetInt / SetVector / SetColor / SetMatrix / SetTexture / HasProperty / GetXXX。
    /// <para>
    /// 两者的区别只在用途（照 Unity）：
    /// Material 上的是「这个材质的基线值」，材质被多个绘制复用；
    /// MaterialPropertyBlock 上的是「这一次绘制临时覆盖的值」，绘制时先下发材质的属性、再下发 Block 的覆盖值，
    /// 因此可以一个材质 + 一个重复使用的 Block 画出 N 种外观，而不必给每个实例 new 一个材质。
    /// </para>
    /// <para>
    /// 属性值按名字索引（<see cref="StringComparer.Ordinal"/>），名字要与着色器里声明的 uniform 完全一致；
    /// 名字在着色器里不存在时绘制时会被忽略（同 Unity）。每次增删改都会推进 <see cref="PropertiesVersion"/>，
    /// 供渲染层判断「内容变了、材质去重短路要失效」。
    /// </para>
    /// </summary>
    public abstract class MaterialProperties
    {
        /// <summary>空的属性表：没设过任何属性时 <see cref="Properties"/> 返回它（避免每次分配）。</summary>
        private static readonly Dictionary<string, MaterialProperty> EmptyProperties = new();

        /// <summary>属性表（按需分配：没调过 SetXXX 就不会有字典）。</summary>
        private Dictionary<string, MaterialProperty>? _properties;

        /// <summary>属性版本号：每次属性增删改 +1，供渲染层判断内容有没有变。</summary>
        public int PropertiesVersion { get; private set; }

        /// <summary>当前的属性表（只读视图；没设过属性时为空表）。</summary>
        public IReadOnlyDictionary<string, MaterialProperty> Properties
        {
            get { return _properties ?? EmptyProperties; }
        }

        /// <summary>是否一条属性都没设（照 Unity 的 MaterialPropertyBlock.isEmpty）。</summary>
        public bool IsEmpty
        {
            get { return _properties is null || _properties.Count == 0; }
        }

        /// <summary>设置 float 属性（对应着色器里的 uniform float）。</summary>
        public void SetFloat(string name, float value)
        {
            Set(name, new MaterialProperty { Type = MaterialPropertyType.Float, Float = value });
        }

        /// <summary>设置 int 属性（对应着色器里的 uniform int / bool / 枚举）。</summary>
        public void SetInt(string name, int value)
        {
            Set(name, new MaterialProperty { Type = MaterialPropertyType.Int, Int = value });
        }

        /// <summary>设置 vec4 属性（对应着色器里的 uniform vec4）。</summary>
        public void SetVector(string name, Vector4 value)
        {
            Set(name, new MaterialProperty { Type = MaterialPropertyType.Vector, Vector = value });
        }

        /// <summary>设置 vec3 属性（对应着色器里的 uniform vec3；w 补 0）。</summary>
        public void SetVector(string name, Vector3 value)
        {
            SetVector(name, new Vector4(value.X, value.Y, value.Z, 0f));
        }

        /// <summary>设置颜色属性：0~255 的 <see cref="Color"/> 会归一化成 0~1 的 vec4 下发（照 Unity 的 SetColor）。</summary>
        public void SetColor(string name, Color value)
        {
            Set(name, new MaterialProperty
            {
                Type = MaterialPropertyType.Color,
                Vector = new Vector4(value.R / 255f, value.G / 255f, value.B / 255f, value.A / 255f),
            });
        }

        /// <summary>设置 mat4 属性（对应着色器里的 uniform mat4）。</summary>
        public void SetMatrix(string name, Matrix4x4 value)
        {
            Set(name, new MaterialProperty { Type = MaterialPropertyType.Matrix, Matrix = value });
        }

        /// <summary>设置纹理属性（对应着色器里的 uniform sampler2D）；传 null 表示解绑该采样器。</summary>
        public void SetTexture(string name, Texture2D? value)
        {
            Set(name, new MaterialProperty { Type = MaterialPropertyType.Texture, Texture = value });
        }

        /// <summary>是否设过该属性（不看着色器里有没有，只看这里设没设）。</summary>
        public bool HasProperty(string name)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            return _properties is not null && _properties.ContainsKey(name);
        }

        /// <summary>取属性值：设过返回 true，否则 false。</summary>
        public bool TryGetProperty(string name, out MaterialProperty property)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            if (_properties is not null && _properties.TryGetValue(name, out property)) return true;
            property = default;
            return false;
        }

        /// <summary>取属性类型；没设过抛 <see cref="KeyNotFoundException"/>（同 Unity 取不存在的属性会报错）。</summary>
        public MaterialPropertyType GetPropertyType(string name)
        {
            return Get(name).Type;
        }

        /// <summary>取 float 属性。</summary>
        public float GetFloat(string name)
        {
            return Get(name).Float;
        }

        /// <summary>取 int 属性。</summary>
        public int GetInt(string name)
        {
            return Get(name).Int;
        }

        /// <summary>取 vec4 属性。</summary>
        public Vector4 GetVector(string name)
        {
            return Get(name).Vector;
        }

        /// <summary>取颜色属性（存的是 0~1，这里还原成 0~255 的 <see cref="Color"/>）。</summary>
        public Color GetColor(string name)
        {
            Vector4 v = Get(name).Vector;
            return new Color((byte)Math.Clamp(v.X * 255f, 0f, 255f),
                             (byte)Math.Clamp(v.Y * 255f, 0f, 255f),
                             (byte)Math.Clamp(v.Z * 255f, 0f, 255f),
                             (byte)Math.Clamp(v.W * 255f, 0f, 255f));
        }

        /// <summary>取 mat4 属性。</summary>
        public Matrix4x4 GetMatrix(string name)
        {
            return Get(name).Matrix;
        }

        /// <summary>取纹理属性。</summary>
        public Texture2D? GetTexture(string name)
        {
            return Get(name).Texture;
        }

        /// <summary>清空全部属性（渲染状态等其它内容不动）。</summary>
        public void ClearProperties()
        {
            if (_properties is null || _properties.Count == 0) return;
            _properties.Clear();
            PropertiesVersion++;
        }

        /// <summary>写入一条属性并推进版本号（同名覆盖，照 Unity 的后设覆盖先设）。</summary>
        protected void Set(string name, MaterialProperty property)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            _properties ??= new Dictionary<string, MaterialProperty>(StringComparer.Ordinal);
            _properties[name] = property;
            PropertiesVersion++;
        }

        /// <summary>按名取属性，没设过就抛异常（内部读取路径）。</summary>
        private MaterialProperty Get(string name)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            if (_properties is not null && _properties.TryGetValue(name, out MaterialProperty property)) return property;
            throw new KeyNotFoundException($"没有名为「{name}」的属性（先用 SetFloat / SetVector / SetColor / SetMatrix / SetTexture 设置）。");
        }
    }
}
