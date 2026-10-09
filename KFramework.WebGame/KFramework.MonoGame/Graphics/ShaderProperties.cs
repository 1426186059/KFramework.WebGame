using System.Numerics;

namespace KFramework.MonoGame
{
    /// <summary>
    /// 着色器属性表（uniform 的「名字 → 值」集合），两处持有者的共用基类：
    /// <see cref="ShaderEffect"/>（效果自带的值，可被多个材质共用）与
    /// <see cref="ShaderPropertyBlock"/>（每一次绘制的临时覆盖值）。
    /// 对应 Unity 里 Material / MaterialPropertyBlock 提供的那组
    /// SetFloat / SetInt / SetVector / SetColor / SetMatrix / SetTexture / HasProperty / GetXXX。
    /// <para>
    /// 两者按用途与优先级区分（越靠"这一次绘制"越优先）：
    /// ShaderEffect 上是「这个效果的默认材质属性」→ Block 上是「这一次绘制临时覆盖的值」。
    /// 绘制时先发效果的默认值、再发块里的覆盖值（同名以块为准，照 Unity 的 SetPropertyBlock），
    /// 因此一份共享效果 + 一个重复使用的 Block 就能画出 N 种外观，而不必给每个实例 new 一个效果。
    /// </para>
    /// <para>
    /// 属性值按名字索引（<see cref="StringComparer.Ordinal"/>），名字要与着色器里声明的 uniform 完全一致；
    /// 名字在着色器里不存在时绘制时会被忽略（同 Unity）。每次增删改都会推进 <see cref="PropertiesVersion"/>，
    /// 供渲染层判断「内容变了、材质去重短路要失效」。
    /// </para>
    /// </summary>
    public abstract class ShaderProperties
    {
        /// <summary>空的属性表：没设过任何属性时 <see cref="Properties"/> 返回它（避免每次分配）。</summary>
        private static readonly Dictionary<string, ShaderProperty> EmptyProperties = new();

        /// <summary>属性表（按需分配：没调过 SetXXX 就不会有字典）。</summary>
        private Dictionary<string, ShaderProperty>? _properties;

        /// <summary>属性版本号：每次属性增删改 +1，供渲染层判断内容有没有变。</summary>
        public int PropertiesVersion { get; private set; }

        /// <summary>当前的属性表（只读视图；没设过属性时为空表）。</summary>
        public IReadOnlyDictionary<string, ShaderProperty> Properties
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
            => Set(name, new ShaderProperty(value));

        /// <summary>设置 int 属性（对应着色器里的 uniform int / bool / 枚举）。</summary>
        public void SetInt(string name, int value)
            => Set(name, new ShaderProperty(value));

        /// <summary>设置 vec4 属性（对应着色器里的 uniform vec4）。</summary>
        public void SetVector(string name, Vector4 value)
            => Set(name, new ShaderProperty(value));

        /// <summary>设置 vec3 属性（对应着色器里的 uniform vec3；w 补 0）。</summary>
        public void SetVector(string name, Vector3 value)
            => Set(name, new ShaderProperty(value));

        /// <summary>设置颜色属性：0~255 的 <see cref="Color"/> 会归一化成 0~1 的 vec4 下发（照 Unity 的 SetColor）。</summary>
        public void SetColor(string name, Color value)
            => Set(name, new ShaderProperty(value));

        /// <summary>设置 mat4 属性（对应着色器里的 uniform mat4）。</summary>
        public void SetMatrix(string name, Matrix4x4 value)
            => Set(name, new ShaderProperty(value));

        /// <summary>设置纹理属性（对应着色器里的 uniform sampler2D）；传 null 表示解绑该采样器。</summary>
        public void SetTexture(string name, Texture2D? value)
            => Set(name, new ShaderProperty(value));

        /// <summary>是否设过该属性（不看着色器里有没有，只看这里设没设）。</summary>
        public bool HasProperty(string name)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            return _properties is not null && _properties.ContainsKey(name);
        }

        /// <summary>取属性值：设过返回 true，否则 false。</summary>
        public bool TryGetProperty(string name, out ShaderProperty property)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            if (_properties is not null && _properties.TryGetValue(name, out property)) return true;
            property = default;
            return false;
        }

        /// <summary>取属性类型；没设过抛 <see cref="KeyNotFoundException"/>（同 Unity 取不存在的属性会报错）。</summary>
        public ShaderPropertyType GetPropertyType(string name)
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
        protected void Set(string name, ShaderProperty property)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            _properties ??= new Dictionary<string, ShaderProperty>(StringComparer.Ordinal);
            _properties[name] = property;
            PropertiesVersion++;
        }

        /// <summary>按名取属性，没设过就抛异常（内部读取路径）。</summary>
        private ShaderProperty Get(string name)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            if (_properties is not null && _properties.TryGetValue(name, out ShaderProperty property)) return property;
            throw new KeyNotFoundException($"没有名为「{name}」的属性（先用 SetFloat / SetVector / SetColor / SetMatrix / SetTexture 设置）。");
        }
    }
}
