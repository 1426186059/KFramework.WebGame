using System.Buffers.Binary;
using System.Runtime.InteropServices.JavaScript;

namespace KFramework.MonoGame
{

    /// <summary>
    /// WebGL 2D 的默认着色器程序（GLSL ES 3.00）：SpriteBatch 未指定自定义 Effect 时用它。
    /// 顶点为 位置(float4)+颜色+UV，片元做一次纹理采样与颜色相乘。
    /// </summary>
    internal sealed class WebGL_ShaderProgram_2D_Default : IShaderProgram
    {
        private const string VertexSource = """
            #version 300 es
            // 顶点输入对齐 Unity 精灵着色器的 appdata_t：aPosition ↔ float4 vertex : POSITION，
            // aColor ↔ float4 color : COLOR，aTexCoord ↔ float2 texcoord : TEXCOORD0。
            // 声明顺序必须与 WebGl20Backend.ConfigureAttributes 的槽位顺序一致（位置 → 颜色 → UV）。
            in vec4 aPosition;
            in vec4 aColor;
            in vec2 aTexCoord;

            uniform mat4 uProjection;

            out vec2 vTexCoord;
            out vec4 vColor;
            // UNITY_VERTEX_INPUT_INSTANCE_ID 在 GLSL 里的等价物：实例号是内置输入，不占顶点布局。
            // 片元着色器要用它的话，声明 flat in int vInstanceID; 即可。
            flat out int vInstanceID;

            void main()
            {
                // 位置自带 w = 1，直接就是裁剪空间齐次坐标（照 D3D9 的 XYZRHW）。
                gl_Position = uProjection * aPosition;
                vTexCoord = aTexCoord;
                vColor = aColor;
                vInstanceID = gl_InstanceID;
            }
            """;

        private const string FragmentSource = """
            #version 300 es
            precision highp float;

            in vec2 vTexCoord;
            in vec4 vColor;

            uniform sampler2D uTexture;

            out vec4 fragColor;

            void main()
            {
                fragColor = texture(uTexture, vTexCoord) * vColor;
            }
            """;

        private readonly JSObject _program;
        private readonly JSObject? _projectionLocation;
        private readonly JSObject? _textureLocation;
        private readonly byte[] _matrixBuffer = new byte[16 * sizeof(float)];
        private bool _locationsLogged;

        internal readonly int PositionLocation;
        internal readonly int TexCoordLocation;
        internal readonly int ColorLocation;

        internal JSObject Program => _program;

        internal WebGL_ShaderProgram_2D_Default()
        {
            _program = JSBind_WEBGL20.CreateProgram();

            JSObject vertexShader = Compile(JSBind_WEBGL20.VERTEX_SHADER, VertexSource);
            JSObject fragmentShader = Compile(JSBind_WEBGL20.FRAGMENT_SHADER, FragmentSource);

            JSBind_WEBGL20.AttachShader(_program, vertexShader);
            JSBind_WEBGL20.AttachShader(_program, fragmentShader);
            JSBind_WEBGL20.LinkProgram(_program);

            if (JSBind_WEBGL20.GetProgramParameter(_program, JSBind_WEBGL20.LINK_STATUS) == 0)
                throw new InvalidOperationException("着色器链接失败: " + JSBind_WEBGL20.GetProgramInfoLog(_program));

            JSBind_WEBGL20.DeleteShader(vertexShader);
            JSBind_WEBGL20.DeleteShader(fragmentShader);

            _projectionLocation = JSBind_WEBGL20.GetUniformLocation(_program, "uProjection");
            _textureLocation = JSBind_WEBGL20.GetUniformLocation(_program, "uTexture");
            PositionLocation = JSBind_WEBGL20.GetAttribLocation(_program, "aPosition");
            TexCoordLocation = JSBind_WEBGL20.GetAttribLocation(_program, "aTexCoord");
            ColorLocation = JSBind_WEBGL20.GetAttribLocation(_program, "aColor");
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
                throw new InvalidOperationException($"着色器编译失败: {log}");
            }
            return shader;
        }

        // 实现 IShaderProgram：接口成员需为 public（接口本身与其实现类都是 internal，对外仍不可见）。
        public bool IsAnimated => false;

        public void Apply(Matrix4x4 projection, Material material, MaterialPropertyBlock? properties, ShaderProperties effect)
        {
            // 内置精灵程序只声明了 uProjection / uTexture：着色器属性无处可写，故 effect / material / properties 三者都忽略
            //（要往着色器设变量请用自定义程序，见 WebGL_ShaderProgram_2D_Custom）。
            JSBind_WEBGL20.UseProgram(_program);
            if (_projectionLocation is not null)
            {
                WriteMatrix(projection, _matrixBuffer);
                JSBind_WEBGL20.UniformMatrix4fv(_projectionLocation, 0, _matrixBuffer);
            }

            if (_textureLocation is not null)
            {
                JSBind_WEBGL20.Uniform1i(_textureLocation, 0);
            }

            if (!_locationsLogged)
            {
                _locationsLogged = true;
                PrintTool.Log($"[WebGL_ShaderProgram_2D_Default] attribute: pos={PositionLocation} color={ColorLocation} uv={TexCoordLocation} | " +
                                  $"uniform: proj={(_projectionLocation is null ? "null" : "ok")} tex={(_textureLocation is null ? "null" : "ok")}");
            }
        }

        /// <summary>
        /// 把矩阵写成 16 个 float 的小端字节流交给 WebGL。
        ///
        /// WebGL 的 uniformMatrix4fv 要求【列主序】数据，且 transpose 参数必须为 false
        /// （传 true 会直接产生 INVALID_VALUE 错误，不能指望 GPU 帮我们转置）。
        ///
        /// 而本引擎的矩阵是行主序 + 行向量（p' = p × M），与 GLSL 的列向量（v' = M × v）
        /// 相差一个转置。好在"行主序内存按原样摊平"恰好等于"转置矩阵的列主序"，且
        ///     v' = Mᵀ × v   与   p' = p × M
        /// 数学上完全等价，所以这里【按字段原顺序直写】即可，不需要任何额外转置操作。
        /// 平移 M41 / M42 / M43 会自然落到列主序数组的第 12 / 13 / 14 位（也就是第 4 列），
        /// 正是 GLSL 期望的平移位置。
        /// </summary>
        private static void WriteMatrix(in Matrix4x4 value, Span<byte> destination)
        {
            Write(destination, 0, value.M11);  Write(destination, 1, value.M12);
            Write(destination, 2, value.M13);  Write(destination, 3, value.M14);
            Write(destination, 4, value.M21);  Write(destination, 5, value.M22);
            Write(destination, 6, value.M23);  Write(destination, 7, value.M24);
            Write(destination, 8, value.M31);  Write(destination, 9, value.M32);
            Write(destination, 10, value.M33); Write(destination, 11, value.M34);
            Write(destination, 12, value.M41); Write(destination, 13, value.M42);
            Write(destination, 14, value.M43); Write(destination, 15, value.M44);
        }

        private static void Write(Span<byte> destination, int index, float value)
            => BinaryPrimitives.WriteSingleLittleEndian(destination.Slice(index * 4, 4), value);

        public void Dispose() => JSBind_WEBGL20.DeleteProgram(_program);
    }
}
