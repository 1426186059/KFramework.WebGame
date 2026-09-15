using System.Buffers.Binary;
using System.Runtime.InteropServices.JavaScript;

using KFramework.MonoGame;

namespace KFramework.MonoGame
{

    /// <summary>
    /// SpriteBatch 使用的内置着色器（GLSL ES 3.00）。顶点为 位置+UV+颜色，片元做一次纹理采样与颜色相乘。
    /// </summary>
    internal sealed class SpriteEffect : IDisposable
    {
        private const string VertexSource = """
            #version 300 es
            in vec2 aPosition;
            in vec2 aTexCoord;
            in vec4 aColor;

            uniform mat4 uProjection;

            out vec2 vTexCoord;
            out vec4 vColor;

            void main()
            {
                gl_Position = uProjection * vec4(aPosition, 0.0, 1.0);
                vTexCoord = aTexCoord;
                vColor = aColor;
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

        internal SpriteEffect()
        {
            _program = JSBind_GL.CreateProgram();

            JSObject vertexShader = Compile(JSBind_GL.VERTEX_SHADER, VertexSource);
            JSObject fragmentShader = Compile(JSBind_GL.FRAGMENT_SHADER, FragmentSource);

            JSBind_GL.AttachShader(_program, vertexShader);
            JSBind_GL.AttachShader(_program, fragmentShader);
            JSBind_GL.LinkProgram(_program);

            if (JSBind_GL.GetProgramParameter(_program, JSBind_GL.LINK_STATUS) == 0)
                throw new InvalidOperationException("着色器链接失败: " + JSBind_GL.GetProgramInfoLog(_program));

            JSBind_GL.DeleteShader(vertexShader);
            JSBind_GL.DeleteShader(fragmentShader);

            _projectionLocation = JSBind_GL.GetUniformLocation(_program, "uProjection");
            _textureLocation = JSBind_GL.GetUniformLocation(_program, "uTexture");
            PositionLocation = JSBind_GL.GetAttribLocation(_program, "aPosition");
            TexCoordLocation = JSBind_GL.GetAttribLocation(_program, "aTexCoord");
            ColorLocation = JSBind_GL.GetAttribLocation(_program, "aColor");
        }

        private static JSObject Compile(int type, string source)
        {
            JSObject shader = JSBind_GL.CreateShader(type);
            JSBind_GL.ShaderSource(shader, source);
            JSBind_GL.CompileShader(shader);
            if (JSBind_GL.GetShaderParameter(shader, JSBind_GL.COMPILE_STATUS) == 0)
            {
                string log = JSBind_GL.GetShaderInfoLog(shader);
                JSBind_GL.DeleteShader(shader);
                throw new InvalidOperationException($"着色器编译失败: {log}");
            }
            return shader;
        }

        internal void Apply(Matrix4x4 projection)
        {
            JSBind_GL.UseProgram(_program);
            if (_projectionLocation is not null)
            {
                WriteMatrix(projection, _matrixBuffer);
                JSBind_GL.UniformMatrix4fv(_projectionLocation, 0, _matrixBuffer);
            }
            if (_textureLocation is not null) JSBind_GL.Uniform1i(_textureLocation, 0);

            if (!_locationsLogged)
            {
                _locationsLogged = true;
                PrintTool.Log($"[SpriteEffect] attribute: pos={PositionLocation} uv={TexCoordLocation} color={ColorLocation} | " +
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

        public void Dispose() => JSBind_GL.DeleteProgram(_program);
    }
}
