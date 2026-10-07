using System.Numerics;
using System.Runtime.InteropServices;

namespace KFramework.MonoGame
{
    /// <summary>
    /// SpriteBatch 的顶点，语义与字节布局对齐 Unity 精灵着色器的 appdata_t（Sprites-Default）：
    /// <code>
    /// struct appdata_t {
    ///     float4 vertex   : POSITION;      // 16 字节（x, y, z, w），w 固定为 1
    ///     float4 color    : COLOR;         // 4 字节 unorm8x4（照 Unity 的 Color32，着色器里仍按 float4 取用）
    ///     float2 texcoord : TEXCOORD0;     // 8 字节
    ///     UNITY_VERTEX_INPUT_INSTANCE_ID   // 不占顶点字节：实例号是内置输入（GLSL 的 gl_InstanceID / WGSL 的 @builtin(instance_index)）
    /// };
    /// </code>
    /// <para>
    /// 布局（步长 28 字节）：0 位置（float32x4）/ 16 颜色（unorm8x4）/ 20 UV（float32x2）。
    /// WebGL20 的 VAO 与 WebGPU 的 <c>vertex.buffers</c> 与此严格一一对应，改这里必须同步改那两个后端。
    /// </para>
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct VertexPositionColorTexture
    {
        /// <summary>位置（x, y, z, w）：w 固定为 1，可当裁剪空间坐标直传（照 D3D9 的 XYZRHW）。</summary>
        public Vector4 Position;

        /// <summary>顶点色（顶点里是 4 个归一化字节，着色器按 float4 取用）。</summary>
        public Color Color;

        /// <summary>纹理坐标。</summary>
        public Vector2 TexCoord;

        /// <summary>顶点步长：后端顶点布局的 arrayStride / VAO stride 必须取它。</summary>
        public const int SizeInBytes = 28;

        public VertexPositionColorTexture(Vector4 position, Color color, Vector2 texCoord)
        {
            Position = position;
            Color = color;
            TexCoord = texCoord;
        }
    }
}
