using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.JavaScript;

namespace KFramework.MonoGame
{
    /// <summary>
    /// WebGL 2D 的 SRP-Batcher 式程序（照 Unity 的 URP / SRP Batcher）：
    /// <b>DrawCall 不减少</b>，但"换物体"的 CPU 开销被压到只剩一次绑定。
    /// <para>
    /// 三条路的对照：
    /// <list type="table">
    ///   <item><term><see cref="SpriteBatch"/>（CPU 合批）</term><description>几何在 CPU 侧展开、按纹理分批；
    ///   逐物体差异随顶点数据走，但每帧要把整批顶点重新上传。</description></item>
    ///   <item><term><see cref="GpuInstanceBatch"/>（GPU 实例化）</term><description>单位四边形 + 逐实例属性（divisor=1），
    ///   一次 <c>drawElementsInstanced</c> 画完整批：DrawCall 最少，但逐实例数据只能走顶点通道（受属性槽与格式限制）。</description></item>
    ///   <item><term>本类（SRP Batcher 式）</term><description>逐物体数据放 <b>uniform buffer</b>：整段一次性上传，
    ///   之后每个物体只 <c>bindBufferRange</c> 换个偏移 + <c>drawElements</c>；材质常量进另一个常驻缓冲，材质不变就一次都不传。
    ///   优势是不受顶点属性格式限制（凑不齐的槽、矩阵/整型/更多分量都能放）；代价是 DrawCall 仍是每物体一次。</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>两个容易踩的硬约束</b>（都在这里处理掉了）：
    /// <list type="number">
    ///   <item><description>GLSL ES 3.00 的 uniform block 没有 <c>layout(binding = N)</c>，绑定点必须用
    ///   <c>glUniformBlockBinding</c> 显式指定（见构造函数）。</description></item>
    ///   <item><description><c>bindBufferRange</c> 的偏移必须是 <c>UNIFORM_BUFFER_OFFSET_ALIGNMENT</c> 的整数倍
    ///   （常见 256），所以每条记录按该值向上取整占位，C# 侧先把记录摆到对齐后的位置上，再整段一次上传。</description></item>
    /// </list>
    /// </para>
    /// </summary>
    internal sealed class WebGL_ShaderProgram_2D_Urp : IUrpProgram
    {
        /// <summary>单位四边形（逐顶点）：位置 0~1 + UV 0~1，16 字节/顶点。</summary>
        private const int QuadVertexBytes = 16;

        /// <summary>逐物体常量块的绑定点（顶点着色器里的 <c>UnityPerDraw</c>）。</summary>
        private const int PerDrawBindingPoint = 0;

        /// <summary>逐材质常量块的绑定点（片元着色器里的 <c>UnityPerMaterial</c>）。</summary>
        private const int PerMaterialBindingPoint = 1;

        /// <summary>逐物体缓冲的初始容量（不够会自动增长，故只是个起步值）。</summary>
        private const int InitialCapacity = 256;

        /// <summary>逐材质常量块的字节数（std140：一个 vec4）。</summary>
        private const int MaterialBlockBytes = 16;

        private const string VertexSource = 
@"#version 300 es
in vec2 aQuadPos;      // 单位四边形 (0,0)-(1,1)，逐顶点
in vec2 aQuadUv;

// 逐物体常量缓冲（照 Unity 的 UnityPerDraw）：整段一次性上传；
// 绘制时每个物体只用 bindBufferRange 换一段偏移 —— 不上传、不设 uniform、不切程序。
layout(std140) uniform UnityPerDraw
{
    mat4 unity_ObjectToWorld;   // 照 Unity：对象→世界矩阵
    vec4 uUvRect;               // 逐物体 UV 矩形（xy 起点 / zw 尺寸）
    vec4 uTint;                 // 逐物体颜色
};

// 逐帧数据：一段只设一次（URP 里它在 UnityPerFrame 缓冲里）
uniform mat4 uProjection;

out vec2 vTexCoord;
out vec4 vColor;

void main()
{
    gl_Position = uProjection * (unity_ObjectToWorld * vec4(aQuadPos, 0.0, 1.0));
    vTexCoord = uUvRect.xy + aQuadUv * uUvRect.zw;
    vColor = uTint;
}";

        /// <summary>默认片元着色器：纹理 × 逐物体颜色 × 材质常量。</summary>
        private const string DefaultFragmentSource = 
@"#version 300 es
precision highp float;
in vec2 vTexCoord;
in vec4 vColor;

// 逐材质常量缓冲（照 Unity 的 UnityPerMaterial）：材质不变 → 一段只上传一次。
layout(std140) uniform UnityPerMaterial
{
    vec4 uColorScale;
};

uniform sampler2D uTexture;
out vec4 fragColor;

void main()
{
    fragColor = texture(uTexture, vTexCoord) * vColor * uColorScale;
}";

        private readonly JSObject _program;
        private readonly JSObject _vertexArray;
        private readonly JSObject _quadBuffer;
        private readonly JSObject _indexBuffer;
        private readonly JSObject _perDrawBuffer;
        private readonly JSObject _materialBuffer;

        private readonly JSObject? _projectionLocation;
        private readonly JSObject? _textureLocation;
        private readonly byte[] _matrixBuffer = new byte[16 * sizeof(float)];

        /// <summary>每条逐物体记录的实际占位（96 向上取整到 <c>UNIFORM_BUFFER_OFFSET_ALIGNMENT</c>）。</summary>
        private readonly int _stride;

        /// <summary>逐物体常量的上传暂存（按 <see cref="_stride"/> 摆位，整段一次上传）；容量不够时整块换新。</summary>
        private byte[] _staging;

        /// <summary>逐材质常量的上传暂存。</summary>
        private readonly byte[] _materialBytes = new byte[MaterialBlockBytes];

        /// <summary>逐材质常量块按对齐取整后的字节数（<c>bindBufferRange</c> 的 size 用它，稳妥些）。</summary>
        private readonly int _materialSize;

        /// <summary>逐物体缓冲当前能放的物体数（不够就按 2 倍增长，调用方不需要关心）。</summary>
        public int Capacity { get; private set; }

        internal WebGL_ShaderProgram_2D_Urp(string? fragmentSource)
        {
            Capacity = InitialCapacity;

            _program = JSBind_WEBGL20.CreateProgram();
            JSObject vertexShader = Compile(JSBind_WEBGL20.VERTEX_SHADER, VertexSource);
            JSObject fragmentShader = Compile(JSBind_WEBGL20.FRAGMENT_SHADER,
                string.IsNullOrWhiteSpace(fragmentSource) ? DefaultFragmentSource : fragmentSource!);

            JSBind_WEBGL20.AttachShader(_program, vertexShader);
            JSBind_WEBGL20.AttachShader(_program, fragmentShader);
            JSBind_WEBGL20.LinkProgram(_program);
            if (JSBind_WEBGL20.GetProgramParameter(_program, JSBind_WEBGL20.LINK_STATUS) == 0)
                throw new InvalidOperationException("URP 着色器链接失败: " + JSBind_WEBGL20.GetProgramInfoLog(_program));

            JSBind_WEBGL20.DeleteShader(vertexShader);
            JSBind_WEBGL20.DeleteShader(fragmentShader);

            _projectionLocation = JSBind_WEBGL20.GetUniformLocation(_program, "uProjection");
            _textureLocation = JSBind_WEBGL20.GetUniformLocation(_program, "uTexture");

            // GLSL ES 3.00 的 uniform block 没有 layout(binding = N)：绑定点必须显式指定（一次即可）。
            int perDrawBlock = JSBind_WEBGL20.GetUniformBlockIndex(_program, "UnityPerDraw");
            int perMaterialBlock = JSBind_WEBGL20.GetUniformBlockIndex(_program, "UnityPerMaterial");
            if (perDrawBlock == JSBind_WEBGL20.INVALID_INDEX || perMaterialBlock == JSBind_WEBGL20.INVALID_INDEX)
                throw new InvalidOperationException(
                    "URP 着色器里找不到 uniform block（需要 UnityPerDraw 与 UnityPerMaterial）：检查顶点 / 片元源码。");

            JSBind_WEBGL20.UniformBlockBinding(_program, perDrawBlock, PerDrawBindingPoint);
            JSBind_WEBGL20.UniformBlockBinding(_program, perMaterialBlock, PerMaterialBindingPoint);

            // 对齐要求：bindBufferRange 的偏移必须是 UNIFORM_BUFFER_OFFSET_ALIGNMENT 的整数倍。
            int alignment = JSBind_WEBGL20.GetParameterInt(JSBind_WEBGL20.UNIFORM_BUFFER_OFFSET_ALIGNMENT);
            if (alignment <= 0) alignment = 256;   // 实现没给就按最常见的 256 保守处理
            _stride = AlignUp(UrpDrawData.SizeInBytes, alignment);
            _materialSize = AlignUp(MaterialBlockBytes, alignment);
            _staging = new byte[Capacity * _stride];

            // ---- 缓冲：单位四边形（静态）、索引（静态）、逐物体常量（动态）、逐材质常量（很小）----
            _quadBuffer = JSBind_WEBGL20.CreateBuffer();
            _indexBuffer = JSBind_WEBGL20.CreateBuffer();
            _perDrawBuffer = JSBind_WEBGL20.CreateBuffer();
            _materialBuffer = JSBind_WEBGL20.CreateBuffer();

            _vertexArray = JSBind_WEBGL20.CreateVertexArray();

            JSBind_WEBGL20.BindVertexArray(_vertexArray);

            JSBind_WEBGL20.BindBuffer(JSBind_WEBGL20.ARRAY_BUFFER, _quadBuffer);
            JSBind_WEBGL20.BufferData(JSBind_WEBGL20.ARRAY_BUFFER, BuildUnitQuad(), JSBind_WEBGL20.STATIC_DRAW);

            JSBind_WEBGL20.BindBuffer(JSBind_WEBGL20.ELEMENT_ARRAY_BUFFER, _indexBuffer);
            JSBind_WEBGL20.BufferData(JSBind_WEBGL20.ELEMENT_ARRAY_BUFFER, BuildQuadIndices(), JSBind_WEBGL20.STATIC_DRAW);

            BindVertexAttribute("aQuadPos", 2, QuadVertexBytes, 0);
            BindVertexAttribute("aQuadUv", 2, QuadVertexBytes, 8);

            JSBind_WEBGL20.BindBuffer(JSBind_WEBGL20.UNIFORM_BUFFER, _perDrawBuffer);
            JSBind_WEBGL20.BufferDataSize(JSBind_WEBGL20.UNIFORM_BUFFER, Capacity * _stride, JSBind_WEBGL20.DYNAMIC_DRAW);

            JSBind_WEBGL20.BindBuffer(JSBind_WEBGL20.UNIFORM_BUFFER, _materialBuffer);
            JSBind_WEBGL20.BufferDataSize(JSBind_WEBGL20.UNIFORM_BUFFER, _materialSize, JSBind_WEBGL20.DYNAMIC_DRAW);

            // 收尾：解绑 VAO，避免污染其它绘制。
            JSBind_WEBGL20.BindVertexArray(null!);

            // 打一条诊断日志：UBO 的绑定点与对齐要求（出问题时这是第一手线索）。
            PrintTool.Log($"[WebGL_ShaderProgram_2D_Urp] block: perDraw={perDrawBlock}@{PerDrawBindingPoint} " +
                          $"perMaterial={perMaterialBlock}@{PerMaterialBindingPoint} | " +
                          $"offsetAlignment={alignment} → 每条记录占位 {_stride} 字节（数据 {UrpDrawData.SizeInBytes} 字节）| capacity={Capacity}");
        }

        private static int AlignUp(int value, int alignment)
            => alignment <= 1 ? value : (value + alignment - 1) / alignment * alignment;

        /// <summary>按名字取属性槽位并配置指针（着色器里没用到就跳过）。</summary>
        private void BindVertexAttribute(string name, int size, int stride, int offset)
        {
            int location = JSBind_WEBGL20.GetAttribLocation(_program, name);
            if (location < 0) return;

            JSBind_WEBGL20.EnableVertexAttribArray(location);
            JSBind_WEBGL20.VertexAttribPointer(location, size, JSBind_WEBGL20.FLOAT, false, stride, offset);
        }

        /// <summary>单位四边形：4 个顶点（位置 0~1 + UV 0~1）。</summary>
        private static byte[] BuildUnitQuad()
        {
            float[] data =
            [
                0f, 0f, 0f, 0f,
                1f, 0f, 1f, 0f,
                0f, 1f, 0f, 1f,
                1f, 1f, 1f, 1f,
            ];
            return MemoryMarshal.AsBytes(data.AsSpan()).ToArray();
        }

        /// <summary>两个三角形：0,1,2 / 1,3,2（与 SpriteBatcher 的四边形剖分一致）。</summary>
        private static byte[] BuildQuadIndices()
        {
            ushort[] indices = [0, 1, 2, 1, 3, 2];
            return MemoryMarshal.AsBytes(indices.AsSpan()).ToArray();
        }

        public int DrawSegment(in Matrix4x4 projection, Span<UrpDrawData> draws, int count,
                               Texture2D texture, in Vector4 materialColor, bool uploadMaterial)
        {
            if (count <= 0) return 0;

            JSBind_WEBGL20.UseProgram(_program);

            // 逐帧数据：一段只设一次（不是逐物体）。
            if (_projectionLocation is not null)
            {
                WriteMatrix(projection, _matrixBuffer);
                JSBind_WEBGL20.UniformMatrix4fv(_projectionLocation, 0, _matrixBuffer);
            }
            if (_textureLocation is not null) JSBind_WEBGL20.Uniform1i(_textureLocation, 0);

            // 逐材质常量：材质/值变了才上传（这就是 SRP Batcher 的"每材质一份常驻常量缓冲"）。
            if (uploadMaterial) UploadMaterial(materialColor);

            UploadPerDraw(draws, count);

            JSBind_WEBGL20.BindVertexArray(_vertexArray);

            for (int i = 0; i < count; i++)
            {
                // 【核心】换物体 = 只改这一次绑定：不上传数据、不设 uniform、不切程序。
                JSBind_WEBGL20.BindBufferRange(JSBind_WEBGL20.UNIFORM_BUFFER, PerDrawBindingPoint,
                                               _perDrawBuffer, i * _stride, _stride);
                JSBind_WEBGL20.DrawElements(JSBind_WEBGL20.TRIANGLES, 6, JSBind_WEBGL20.UNSIGNED_SHORT, 0);
            }

            return count;
        }

        /// <summary>
        /// 上传整段逐物体常量：记录本来是 96 字节紧排的，而每条必须摆到 <c>i × _stride</c>（对齐后），
        /// 所以在暂存里逐条搬到位，再整段一次 <c>bufferSubData</c> —— 绝不逐物体上传。
        /// </summary>
        private void UploadPerDraw(Span<UrpDrawData> draws, int count)
        {
            EnsureCapacity(count);

            Span<byte> packed = MemoryMarshal.AsBytes(draws.Slice(0, count));
            Span<byte> staging = _staging;

            for (int i = 0; i < count; i++)
                packed.Slice(i * UrpDrawData.SizeInBytes, UrpDrawData.SizeInBytes)
                      .CopyTo(staging.Slice(i * _stride, UrpDrawData.SizeInBytes));

            JSBind_WEBGL20.BindBuffer(JSBind_WEBGL20.UNIFORM_BUFFER, _perDrawBuffer);
            JSBind_WEBGL20.BufferSubData(JSBind_WEBGL20.UNIFORM_BUFFER, 0, staging[..(count * _stride)]);
        }

        /// <summary>
        /// 确保逐物体缓冲放得下 <paramref name="count"/> 条记录，不够就按 2 倍增长（摊还 O(1)）。
        /// 缓冲重分配是安全的：每个物体在绘制时都会现做一次 <c>bindBufferRange</c>，不存在"记住旧缓冲"的状态。
        /// </summary>
        private void EnsureCapacity(int count)
        {
            if (count <= Capacity) return;

            Capacity = Math.Max(count, Capacity * 2);
            _staging = new byte[Capacity * _stride];

            JSBind_WEBGL20.BindBuffer(JSBind_WEBGL20.UNIFORM_BUFFER, _perDrawBuffer);
            JSBind_WEBGL20.BufferDataSize(JSBind_WEBGL20.UNIFORM_BUFFER, Capacity * _stride, JSBind_WEBGL20.DYNAMIC_DRAW);
        }

        /// <summary>上传逐材质常量并绑定到它的绑定点。</summary>
        private void UploadMaterial(in Vector4 color)
        {
            Span<byte> staging = _materialBytes;
            BinaryPrimitives.WriteSingleLittleEndian(staging[..4], color.X);
            BinaryPrimitives.WriteSingleLittleEndian(staging.Slice(4, 4), color.Y);
            BinaryPrimitives.WriteSingleLittleEndian(staging.Slice(8, 4), color.Z);
            BinaryPrimitives.WriteSingleLittleEndian(staging.Slice(12, 4), color.W);

            JSBind_WEBGL20.BindBuffer(JSBind_WEBGL20.UNIFORM_BUFFER, _materialBuffer);
            JSBind_WEBGL20.BufferSubData(JSBind_WEBGL20.UNIFORM_BUFFER, 0, staging);
            JSBind_WEBGL20.BindBufferRange(JSBind_WEBGL20.UNIFORM_BUFFER, PerMaterialBindingPoint,
                                           _materialBuffer, 0, _materialSize);
        }

        private static JSObject Compile(int type, string source)
        {
            JSObject shader = JSBind_WEBGL20.CreateShader(type);
            JSBind_WEBGL20.ShaderSource(shader, source);
            JSBind_WEBGL20.CompileShader(shader);
            if (JSBind_WEBGL20.GetShaderParameter(shader, JSBind_WEBGL20.COMPILE_STATUS) == 0)
            {
                string log = JSBind_WEBGL20.GetShaderInfoLog(shader);
                JSBind_WEBGL20.DeleteShader(shader);
                throw new InvalidOperationException($"URP 着色器编译失败: {log}");
            }
            return shader;
        }

        /// <summary>矩阵写法与 WebGL_ShaderProgram_2D_Default 一致（行主序直写 = GLSL 期望的列主序）。</summary>
        private static void WriteMatrix(in Matrix4x4 value, Span<byte> destination)
        {
            Write(destination, 0, value.M11); Write(destination, 1, value.M12);
            Write(destination, 2, value.M13); Write(destination, 3, value.M14);
            Write(destination, 4, value.M21); Write(destination, 5, value.M22);
            Write(destination, 6, value.M23); Write(destination, 7, value.M24);
            Write(destination, 8, value.M31); Write(destination, 9, value.M32);
            Write(destination, 10, value.M33); Write(destination, 11, value.M34);
            Write(destination, 12, value.M41); Write(destination, 13, value.M42);
            Write(destination, 14, value.M43); Write(destination, 15, value.M44);
        }

        private static void Write(Span<byte> destination, int index, float value)
            => BinaryPrimitives.WriteSingleLittleEndian(destination.Slice(index * 4, 4), value);

        public void Dispose()
        {
            JSBind_WEBGL20.DeleteBuffer(_quadBuffer);
            JSBind_WEBGL20.DeleteBuffer(_indexBuffer);
            JSBind_WEBGL20.DeleteBuffer(_perDrawBuffer);
            JSBind_WEBGL20.DeleteBuffer(_materialBuffer);
            JSBind_WEBGL20.DeleteProgram(_program);
        }
    }
}
