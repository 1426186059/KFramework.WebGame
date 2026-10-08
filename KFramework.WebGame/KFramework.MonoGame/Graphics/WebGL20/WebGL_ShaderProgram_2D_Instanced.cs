using System.Runtime.InteropServices;
using System.Runtime.InteropServices.JavaScript;

namespace KFramework.MonoGame
{
    /// <summary>
    /// WebGL 2D 的 GPU 实例化程序：一次 <c>drawElementsInstanced</c> 画 N 个实例。
    /// <para>
    /// 与 <see cref="SpriteBatch"/> 的路子对照：
    /// <list type="bullet">
    ///   <item><description>SpriteBatch：几何在 CPU 侧展开（每精灵 4 个顶点、每个顶点 28 字节），按纹理分批，逐批一次 drawElements。</description></item>
    ///   <item><description>本类：几何只有 4 个顶点的单位四边形（静态，一次上传），逐实例的「对象→世界矩阵 / 颜色 / UV 矩形 /
    ///   两个属性槽」全部按实例放进第二根缓冲（<c>vertexAttribDivisor = 1</c>，每实例 116 字节），
    ///   一次 draw 覆盖整个实例列表。</description></item>
    /// </list>
    /// 因此和"几何在 CPU 侧按精灵展开"相比，实例化把 CPU 侧每精灵的顶点开销从 4×28 字节降到一份实例数据，
    /// 并且把 DrawCall 压到"1 / 缓冲容量"。代价是一次 draw 只能一张纹理（图集同一页可以，跨页要分多次）。
    /// </para>
    /// <para>
    /// <b>矩阵就是 Unity 的 <c>unity_ObjectToWorld</c></b>：顶点着色器写
    /// <c>gl_Position = uProjection * (aObjectToWorld * vec4(单位四边形, 0, 1))</c>。
    /// 行主序直发、GLSL 按列读，恰好等价于本引擎的行向量约定 p×M（见 <see cref="Matrix4x4"/> 的说明）。
    /// </para>
    /// <para>
    /// 逐实例属性槽（<c>aInst0/aInst1</c> → <c>vInst0/vInst1</c>）是"每个实例各自持有自己的属性值"的落点，
    /// 由 <see cref="Material.SetGpuInstanceChannels"/> 声明内容、
    /// <see cref="GpuInstanceBatch.Draw(Texture2D, Rectangle?, Rectangle?, Color, float, Vector2, Vector2, SpriteEffects, float, ShaderPropertyBlock?)"/> 写值；
    /// 内置片元着色器不读它们，自定义片元着色器直接读这两个 varying 即可。
    /// </para>
    /// </summary>
    internal sealed class WebGL_ShaderProgram_2D_Instanced : IGpuInstanceProgram
    {
        /// <summary>单位四边形（逐顶点，divisor=0）：位置 0~1 + UV 0~1，16 字节/顶点。</summary>
        private const int QuadVertexBytes = 16;

        /// <summary>逐实例属性（divisor=1）的步长（= <see cref="GpuInstance.SizeInBytes"/>）。</summary>
        private const int InstanceStride = GpuInstance.SizeInBytes;

        // 逐实例字段的偏移直接问结构体本身，避免"改了字段顺序忘了改这里"（历史上踩过两次）。
        private static readonly int TintOffset = (int)Marshal.OffsetOf<GpuInstance>(nameof(GpuInstance.Tint));
        private static readonly int UvRectOffset = (int)Marshal.OffsetOf<GpuInstance>(nameof(GpuInstance.UvRect));
        private static readonly int Inst0Offset = (int)Marshal.OffsetOf<GpuInstance>(nameof(GpuInstance.Inst0));
        private static readonly int Inst1Offset = (int)Marshal.OffsetOf<GpuInstance>(nameof(GpuInstance.Inst1));

        private const string VertexSource = 
@"#version 300 es
in vec2 aQuadPos;           // 单位四边形 (0,0)-(1,1)，逐顶点（divisor = 0）
in vec2 aQuadUv;
// 逐实例：对象→世界矩阵（照 Unity 的 unity_ObjectToWorld）。GLSL 的 mat4 属性占 4 个连续槽位（每列一个 vec4）。
in mat4 aObjectToWorld;
in vec4 aTint;              // 逐实例：颜色
in vec4 aUvRect;            // 逐实例：UV 矩形（xy = 起点，zw = 尺寸）
// 逐实例属性槽：每个实例各自持有自己的属性值，内容由 Material.SetGpuInstanceChannels 声明后编码进实例数据。
in vec4 aInst0;
in vec4 aInst1;
uniform mat4 uProjection;
out vec2 vTexCoord;
out vec4 vColor;
out vec4 vInst0;
out vec4 vInst1;
// UNITY_VERTEX_INPUT_INSTANCE_ID 在 GLSL 里的等价物：实例号是内置输入，不占顶点布局。
flat out int vInstanceID;
void main()
{
    // 单位四边形先按逐实例矩阵变换到世界（Unity 里就是 unity_ObjectToWorld * 顶点），再乘投影到裁剪空间。
    // 矩阵按行主序直发、GLSL 按列读，两者恰好等价于本引擎的行向量约定 p×M（见 Matrix4x4 的说明）。
    gl_Position = uProjection * (aObjectToWorld * vec4(aQuadPos, 0.0, 1.0));
    vTexCoord = aUvRect.xy + aQuadUv * aUvRect.zw;
    vColor = aTint;
    vInst0 = aInst0;
    vInst1 = aInst1;
    vInstanceID = gl_InstanceID;
}";

        /// <summary>默认片元着色器：纹理 × 逐实例颜色。</summary>
        private const string DefaultFragmentSource = 
@"#version 300 es
precision highp float;
in vec2 vTexCoord;
in vec4 vColor;
uniform sampler2D uTexture;
out vec4 fragColor;
void main()
{
    fragColor = texture(uTexture, vTexCoord) * vColor;
}";

        private readonly JSObject _program;
        private readonly JSObject _vertexArray;
        private readonly JSObject _quadBuffer;
        private readonly JSObject _indexBuffer;
        private readonly JSObject _instanceBuffer;

        private readonly JSObject? _projectionLocation;
        private readonly JSObject? _textureLocation;
        private readonly byte[] _matrixBuffer = new byte[16 * sizeof(float)];

        public int Capacity { get; }

        internal WebGL_ShaderProgram_2D_Instanced(string? fragmentSource, int capacity)
        {
            Capacity = capacity > 0 ? capacity : 256;

            _program = JSBind_WEBGL20.CreateProgram();
            JSObject vertexShader = Compile(JSBind_WEBGL20.VERTEX_SHADER, VertexSource);
            JSObject fragmentShader = Compile(JSBind_WEBGL20.FRAGMENT_SHADER,
                string.IsNullOrWhiteSpace(fragmentSource) ? DefaultFragmentSource : fragmentSource!);

            JSBind_WEBGL20.AttachShader(_program, vertexShader);
            JSBind_WEBGL20.AttachShader(_program, fragmentShader);
            JSBind_WEBGL20.LinkProgram(_program);
            if (JSBind_WEBGL20.GetProgramParameter(_program, JSBind_WEBGL20.LINK_STATUS) == 0)
                throw new InvalidOperationException("实例化着色器链接失败: " + JSBind_WEBGL20.GetProgramInfoLog(_program));

            JSBind_WEBGL20.DeleteShader(vertexShader);
            JSBind_WEBGL20.DeleteShader(fragmentShader);

            _projectionLocation = JSBind_WEBGL20.GetUniformLocation(_program, "uProjection");
            _textureLocation = JSBind_WEBGL20.GetUniformLocation(_program, "uTexture");

            // ---- 缓冲：单位四边形（静态）、索引（静态）、实例数据（动态）----
            _quadBuffer = JSBind_WEBGL20.CreateBuffer();
            _indexBuffer = JSBind_WEBGL20.CreateBuffer();
            _instanceBuffer = JSBind_WEBGL20.CreateBuffer();

            _vertexArray = JSBind_WEBGL20.CreateVertexArray();

            JSBind_WEBGL20.BindVertexArray(_vertexArray);

            JSBind_WEBGL20.BindBuffer(JSBind_WEBGL20.ARRAY_BUFFER, _quadBuffer);
            JSBind_WEBGL20.BufferData(JSBind_WEBGL20.ARRAY_BUFFER, BuildUnitQuad(), JSBind_WEBGL20.STATIC_DRAW);

            JSBind_WEBGL20.BindBuffer(JSBind_WEBGL20.ELEMENT_ARRAY_BUFFER, _indexBuffer);
            JSBind_WEBGL20.BufferData(JSBind_WEBGL20.ELEMENT_ARRAY_BUFFER, BuildQuadIndices(), JSBind_WEBGL20.STATIC_DRAW);

            // 逐顶点属性（divisor = 0，WebGL2 里 divisor 的默认值就是 0，这里显式写出来便于对照）
            BindVertexAttribute("aQuadPos", 2, JSBind_WEBGL20.FLOAT, false, QuadVertexBytes, 0, 0);
            BindVertexAttribute("aQuadUv", 2, JSBind_WEBGL20.FLOAT, false, QuadVertexBytes, 8, 0);

            // 逐实例属性（divisor = 1：每个实例推进一条记录）
            JSBind_WEBGL20.BindBuffer(JSBind_WEBGL20.ARRAY_BUFFER, _instanceBuffer);
            JSBind_WEBGL20.BufferDataSize(JSBind_WEBGL20.ARRAY_BUFFER, Capacity * InstanceStride, JSBind_WEBGL20.DYNAMIC_DRAW);

            // 逐实例矩阵：Unity 的 unity_ObjectToWorld 等价物（偏移 0，占 4 个槽位）
            BindMatrixAttribute("aObjectToWorld", InstanceStride, 0, 1);
            BindVertexAttribute("aTint", 4, JSBind_WEBGL20.UNSIGNED_BYTE, true, InstanceStride, TintOffset, 1);
            BindVertexAttribute("aUvRect", 4, JSBind_WEBGL20.FLOAT, false, InstanceStride, UvRectOffset, 1);
            // 逐实例属性槽：内容由 Material.SetGpuInstanceChannels 声明。
            BindVertexAttribute("aInst0", 4, JSBind_WEBGL20.FLOAT, false, InstanceStride, Inst0Offset, 1);
            BindVertexAttribute("aInst1", 4, JSBind_WEBGL20.FLOAT, false, InstanceStride, Inst1Offset, 1);

            // 收尾：把顶点数组解绑，避免污染其它绘制（SpriteBatch 每次绘制都会绑自己的 VAO）。
            JSBind_WEBGL20.BindVertexArray(null!);
        }

        /// <summary>按名字取属性槽位并配置指针 + 实例步长（着色器里没用到就跳过）。</summary>
        private void BindVertexAttribute(string name, int size, int type, bool normalized, int stride, int offset, int divisor)
        {
            int location = JSBind_WEBGL20.GetAttribLocation(_program, name);
            if (location < 0) return;

            JSBind_WEBGL20.EnableVertexAttribArray(location);
            JSBind_WEBGL20.VertexAttribPointer(location, size, type, normalized, stride, offset);
            JSBind_WEBGL20.VertexAttribDivisor(location, divisor);
        }

        /// <summary>
        /// 绑定 <c>mat4</c> 属性：GLSL 的矩阵属性占 4 个【连续】槽位（一列一个 vec4），
        /// <c>GetAttribLocation</c> 返回的是第 0 列的槽位，故四列都要各自配指针与实例步长。
        /// </summary>
        private void BindMatrixAttribute(string name, int stride, int offset, int divisor)
        {
            int location = JSBind_WEBGL20.GetAttribLocation(_program, name);
            if (location < 0) return;

            for (int column = 0; column < 4; column++)
            {
                int slot = location + column;
                int columnOffset = offset + column * 4 * sizeof(float);

                JSBind_WEBGL20.EnableVertexAttribArray(slot);
                JSBind_WEBGL20.VertexAttribPointer(slot, 4, JSBind_WEBGL20.FLOAT, false, stride, columnOffset);
                JSBind_WEBGL20.VertexAttribDivisor(slot, divisor);
            }
        }

        /// <summary>单位四边形：4 个顶点（位置 0~1 + UV 0~1），顺序与索引配合。</summary>
        private static byte[] BuildUnitQuad()
        {
            float[] data =
            [
                0f, 0f, 0f, 0f,
                1f, 0f, 1f, 0f,
                0f, 1f, 0f, 1f,
                1f, 1f, 1f, 1f,
            ];
            return System.Runtime.InteropServices.MemoryMarshal.AsBytes(data.AsSpan()).ToArray();
        }

        /// <summary>两个三角形：0,1,2 / 1,3,2（与 SpriteBatcher 的四边形剖分一致）。</summary>
        private static byte[] BuildQuadIndices()
        {
            ushort[] indices = [0, 1, 2, 1, 3, 2];
            return System.Runtime.InteropServices.MemoryMarshal.AsBytes(indices.AsSpan()).ToArray();
        }

        public void Draw(in Matrix4x4 transform, Span<GpuInstance> instances, int count, Texture2D texture)
        {
            if (count <= 0) return;

            JSBind_WEBGL20.UseProgram(_program);

            if (_projectionLocation is not null)
            {
                WriteMatrix(transform, _matrixBuffer);
                JSBind_WEBGL20.UniformMatrix4fv(_projectionLocation, 0, _matrixBuffer);
            }
            // 精灵纹理固定在 0 号单元（与 SpriteBatch / 自定义程序一致）。
            if (_textureLocation is not null) JSBind_WEBGL20.Uniform1i(_textureLocation, 0);

            // UV 矩形与逐实例矩阵都不走 uniform：它们随实例数据一起进缓冲（GpuInstance.UvRect / ObjectToWorld），
            // 所以同一个 draw 里每个实例都能取纹理的不同子区域、各有自己的变换。
            JSBind_WEBGL20.BindVertexArray(_vertexArray);
            JSBind_WEBGL20.BindBuffer(JSBind_WEBGL20.ARRAY_BUFFER, _instanceBuffer);
            JSBind_WEBGL20.BufferSubData(JSBind_WEBGL20.ARRAY_BUFFER, 0,
                MemoryMarshal.AsBytes(instances.Slice(0, count)));
            JSBind_WEBGL20.DrawElementsInstanced(JSBind_WEBGL20.TRIANGLES, 6, JSBind_WEBGL20.UNSIGNED_SHORT, 0, count);
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
                throw new InvalidOperationException($"实例化着色器编译失败: {log}");
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
            => System.Buffers.Binary.BinaryPrimitives.WriteSingleLittleEndian(destination.Slice(index * 4, 4), value);

        public void Dispose()
        {
            JSBind_WEBGL20.DeleteBuffer(_quadBuffer);
            JSBind_WEBGL20.DeleteBuffer(_indexBuffer);
            JSBind_WEBGL20.DeleteBuffer(_instanceBuffer);
            JSBind_WEBGL20.DeleteProgram(_program);
        }
    }
}
