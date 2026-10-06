using System;
using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.InteropServices.JavaScript;

namespace KFramework.MonoGame
{
    /// <summary>
    /// 自定义精灵着色器程序（WebGL 2.0 / GLSL ES 3.00）。
    /// <para>
    /// 顶点着色器复用标准精灵顶点格式（aPosition / aTexCoord / aColor + uProjection），
    /// 顶点属性位置与默认 <see cref="SpriteEffect"/> 一致（0/1/2），因此可直接套用后端已配置好的 VAO。
    /// 片元着色器由外部传入，并额外暴露 <c>uTime</c>（动画时间）与 <c>uParams</c>（vec4，可携带分辨率/参数）两个 uniform。
    /// </para>
    /// <para>配合 <see cref="ShaderEffect"/> 使用：场景每帧写入 <see cref="ShaderEffect.Time"/> / <see cref="ShaderEffect.Params"/>，Apply 时随材质下发。</para>
    /// </summary>
    internal sealed class CustomWebGlSpriteProgram : ISpriteProgram, ICustomSpriteProgram
    {
        private const string VertexSource = @"#version 300 es
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
}";

        private readonly JSObject _program;
        private readonly JSObject? _projectionLocation;
        private readonly JSObject? _textureLocation;
        private readonly JSObject? _timeLocation;
        private readonly JSObject? _paramsLocation;
        private readonly byte[] _matrixBuffer = new byte[16 * sizeof(float)];

        private ShaderEffect? _owner;

        internal CustomWebGlSpriteProgram(string fragmentSource)
        {
            _program = JSBind_WEBGL20.CreateProgram();

            JSObject vertexShader = Compile(JSBind_WEBGL20.VERTEX_SHADER, VertexSource);
            JSObject fragmentShader = Compile(JSBind_WEBGL20.FRAGMENT_SHADER, fragmentSource);

            JSBind_WEBGL20.AttachShader(_program, vertexShader);
            JSBind_WEBGL20.AttachShader(_program, fragmentShader);
            JSBind_WEBGL20.LinkProgram(_program);

            if (JSBind_WEBGL20.GetProgramParameter(_program, JSBind_WEBGL20.LINK_STATUS) == 0)
                throw new InvalidOperationException("自定义着色器链接失败: " + JSBind_WEBGL20.GetProgramInfoLog(_program));

            JSBind_WEBGL20.DeleteShader(vertexShader);
            JSBind_WEBGL20.DeleteShader(fragmentShader);

            _projectionLocation = JSBind_WEBGL20.GetUniformLocation(_program, "uProjection");
            _textureLocation = JSBind_WEBGL20.GetUniformLocation(_program, "uTexture");
            _timeLocation = JSBind_WEBGL20.GetUniformLocation(_program, "uTime");
            _paramsLocation = JSBind_WEBGL20.GetUniformLocation(_program, "uParams");
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
                throw new InvalidOperationException($"自定义着色器编译失败: {log}");
            }
            return shader;
        }

        void ICustomSpriteProgram.SetOwner(ShaderEffect owner) => _owner = owner;

        public bool IsAnimated => true;

        public void Apply(Matrix4x4 projection)
        {
            JSBind_WEBGL20.UseProgram(_program);
            if (_projectionLocation is not null)
            {
                WriteMatrix(projection, _matrixBuffer);
                JSBind_WEBGL20.UniformMatrix4fv(_projectionLocation, 0, _matrixBuffer);
            }
            if (_textureLocation is not null) JSBind_WEBGL20.Uniform1i(_textureLocation, 0);

            float time = _owner?.Time ?? 0f;
            Vector4 p = _owner?.Params ?? Vector4.Zero;

            if (_timeLocation is not null) JSBind_WEBGL20.Uniform1f(_timeLocation, time);
            if (_paramsLocation is not null) JSBind_WEBGL20.Uniform4f(_paramsLocation, p.X, p.Y, p.Z, p.W);
        }

        public void Dispose() => JSBind_WEBGL20.DeleteProgram(_program);

        /// <summary>
        /// 把矩阵写成 16 个 float 的小端字节流交给 WebGL（列主序 + 转置抵消，见 <see cref="SpriteEffect"/> 说明）。
        /// </summary>
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
    }
}
