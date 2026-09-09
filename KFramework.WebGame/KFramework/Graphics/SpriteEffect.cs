using System.Buffers.Binary;
using System.Runtime.InteropServices.JavaScript;

namespace KFramework.Graphics;

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
        _program = GL.CreateProgram();

        JSObject vertexShader = Compile(GL.VERTEX_SHADER, VertexSource);
        JSObject fragmentShader = Compile(GL.FRAGMENT_SHADER, FragmentSource);

        GL.AttachShader(_program, vertexShader);
        GL.AttachShader(_program, fragmentShader);
        GL.LinkProgram(_program);

        if (GL.GetProgramParameter(_program, GL.LINK_STATUS) == 0)
            throw new InvalidOperationException("着色器链接失败: " + GL.GetProgramInfoLog(_program));

        GL.DeleteShader(vertexShader);
        GL.DeleteShader(fragmentShader);

        _projectionLocation = GL.GetUniformLocation(_program, "uProjection");
        _textureLocation = GL.GetUniformLocation(_program, "uTexture");
        PositionLocation = GL.GetAttribLocation(_program, "aPosition");
        TexCoordLocation = GL.GetAttribLocation(_program, "aTexCoord");
        ColorLocation = GL.GetAttribLocation(_program, "aColor");
    }

    private static JSObject Compile(int type, string source)
    {
        JSObject shader = GL.CreateShader(type);
        GL.ShaderSource(shader, source);
        GL.CompileShader(shader);
        if (GL.GetShaderParameter(shader, GL.COMPILE_STATUS) == 0)
        {
            string log = GL.GetShaderInfoLog(shader);
            GL.DeleteShader(shader);
            throw new InvalidOperationException($"着色器编译失败: {log}");
        }
        return shader;
    }

    internal void Apply(Matrix4x4 projection)
    {
        GL.UseProgram(_program);
        if (_projectionLocation is not null)
        {
            WriteMatrix(projection, _matrixBuffer);
            GL.UniformMatrix4fv(_projectionLocation, 0, _matrixBuffer);
        }
        if (_textureLocation is not null) GL.Uniform1i(_textureLocation, 0);

        if (!_locationsLogged)
        {
            _locationsLogged = true;
            Console.WriteLine($"[SpriteEffect] attribute: pos={PositionLocation} uv={TexCoordLocation} color={ColorLocation} | " +
                              $"uniform: proj={(_projectionLocation is null ? "null" : "ok")} tex={(_textureLocation is null ? "null" : "ok")}");
        }
    }

    /// <summary>按列主序把矩阵写成 16 个 float 的小端字节流。</summary>
    private static void WriteMatrix(in Matrix4x4 value, Span<byte> destination)
    {
        Write(destination, 0, value.M11);  Write(destination, 1, value.M21);
        Write(destination, 2, value.M31);  Write(destination, 3, value.M41);
        Write(destination, 4, value.M12);  Write(destination, 5, value.M22);
        Write(destination, 6, value.M32);  Write(destination, 7, value.M42);
        Write(destination, 8, value.M13);  Write(destination, 9, value.M23);
        Write(destination, 10, value.M33); Write(destination, 11, value.M43);
        Write(destination, 12, value.M14); Write(destination, 13, value.M24);
        Write(destination, 14, value.M34); Write(destination, 15, value.M44);
    }

    private static void Write(Span<byte> destination, int index, float value)
        => BinaryPrimitives.WriteSingleLittleEndian(destination.Slice(index * 4, 4), value);

    public void Dispose() => GL.DeleteProgram(_program);
}
