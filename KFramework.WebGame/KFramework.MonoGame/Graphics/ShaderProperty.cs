using System.Numerics;

namespace KFramework.MonoGame
{
    /// <summary>
    /// 着色器属性类型：对应 Unity Shader 里声明的属性种类，决定绘制时用哪个 uniform 接口下发。
    /// <para>
    /// Color 与 Vector 都按 vec4 下发，区别只在 <see cref="Material.SetColor"/> 传入的是 0~255 的
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
    }
}
