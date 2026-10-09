using System.Numerics;

namespace KFramework.MonoGame
{
    /// <summary>
    /// 着色器属性类型：对应 Unity Shader 里声明的属性种类，决定绘制时用哪个 uniform 接口下发。
    /// <para>
    /// Color 与 Vector 都按 vec4 下发，区别只在 <see cref="ShaderProperties.SetColor"/> 传入的是 0~255 的
    /// <see cref="Color"/>、写入前会归一化成 0~1（照 Unity 的 SetColor 语义：着色器拿到的是 0~1 的 float4）。
    /// </para>
    /// </summary>
    public enum ShaderPropertyType
    {
        /// <summary>float（uniform1f）。</summary>
        Float,

        /// <summary>int / bool（uniform1i）。</summary>
        Int,

        /// <summary>颜色（归一化后的 vec4，uniform4f）。</summary>
        Color,

        /// <summary>向量 vec4（uniform4f）。</summary>
        Vector,

        /// <summary>矩阵 mat4（uniformMatrix4fv）。</summary>
        Matrix,

        /// <summary>纹理 sampler2D（绑定到材质纹理单元并把单元号写进 uniform1i）。</summary>
        Texture,
    }

    /// <summary>
    /// 一条着色器属性的值：按 <see cref="Type"/> 取对应字段（与 Unity 的材质属性一样只存值，
    /// 名字/类型对不对得上由具体着色器决定）。声明为 struct 以避免每条属性一次堆分配。
    /// <para>
    /// 每个构造函数对应 <see cref="ShaderProperties"/> 上的一个 Set 方法（类型也据此推出），
    /// 省掉"先设 <see cref="Type"/> 再填字段"的样板：<c>new ShaderProperty(1.5f)</c> /
    /// <c>new ShaderProperty(color)</c> / <c>new ShaderProperty(matrix)</c> / <c>new ShaderProperty(tex)</c> …
    /// 只用到某一个字段时，其余字段由 <c>this()</c> 链零初始化（<see cref="Texture"/> 为 null）。
    /// </para>
    /// </summary>
    public struct ShaderProperty
    {
        /// <summary>本条属性的类型（决定用哪个 uniform 接口下发）。</summary>
        public ShaderPropertyType Type;

        /// <summary>float 值（<see cref="ShaderPropertyType.Float"/>）。</summary>
        public float Float;

        /// <summary>int 值（<see cref="ShaderPropertyType.Int"/>）。</summary>
        public int Int;

        /// <summary>vec4 值（<see cref="ShaderPropertyType.Vector"/> / <see cref="ShaderPropertyType.Color"/>，后者为 0~1）。</summary>
        public Vector4 Vector;

        /// <summary>mat4 值（<see cref="ShaderPropertyType.Matrix"/>）。</summary>
        public Matrix4x4 Matrix;

        /// <summary>纹理（<see cref="ShaderPropertyType.Texture"/>）；设为 null 表示解绑该采样器。</summary>
        public Texture2D? Texture;

        /// <summary>float 属性（与 <see cref="ShaderProperties.SetFloat"/> 等价）。</summary>
        public ShaderProperty(float value)
            : this()
        {
            Type = ShaderPropertyType.Float;
            Float = value;
        }

        /// <summary>int 属性（与 <see cref="ShaderProperties.SetInt"/> 等价）。</summary>
        public ShaderProperty(int value)
            : this()
        {
            Type = ShaderPropertyType.Int;
            Int = value;
        }

        /// <summary>bool 属性：按 int 下发（true = 1，false = 0）—— 照 Unity 用 int 设布尔开关。</summary>
        public ShaderProperty(bool value)
            : this()
        {
            Type = ShaderPropertyType.Int;
            Int = value ? 1 : 0;
        }

        /// <summary>vec4 属性（与 <see cref="ShaderProperties.SetVector(string, Vector4)"/> 等价）。</summary>
        public ShaderProperty(Vector4 value)
            : this()
        {
            Type = ShaderPropertyType.Vector;
            Vector = value;
        }

        /// <summary>vec3 属性：w 补 0（与 <see cref="ShaderProperties.SetVector(string, Vector3)"/> 等价）。</summary>
        public ShaderProperty(Vector3 value)
            : this()
        {
            Type = ShaderPropertyType.Vector;
            Vector = new Vector4(value.X, value.Y, value.Z, 0f);
        }

        /// <summary>vec2 属性：z / w 补 0。</summary>
        public ShaderProperty(Vector2 value)
            : this()
        {
            Type = ShaderPropertyType.Vector;
            Vector = new Vector4(value.X, value.Y, 0f, 0f);
        }

        /// <summary>颜色属性：0~255 的 <see cref="Color"/> 归一化成 0~1 的 vec4（与 <see cref="ShaderProperties.SetColor"/> 等价）。</summary>
        public ShaderProperty(Color value)
            : this()
        {
            Type = ShaderPropertyType.Color;
            Vector = new Vector4(value.R / 255f, value.G / 255f, value.B / 255f, value.A / 255f);
        }

        /// <summary>mat4 属性（与 <see cref="ShaderProperties.SetMatrix"/> 等价）。</summary>
        public ShaderProperty(Matrix4x4 value)
            : this()
        {
            Type = ShaderPropertyType.Matrix;
            Matrix = value;
        }

        /// <summary>纹理属性（与 <see cref="ShaderProperties.SetTexture"/> 等价）；传 null 表示解绑该采样器。</summary>
        public ShaderProperty(Texture2D? value)
            : this()
        {
            Type = ShaderPropertyType.Texture;
            Texture = value;
        }
    }
}
