using System.Buffers.Binary;
using System.Runtime.InteropServices.JavaScript;

namespace KFramework.MonoGame
{
    /// <summary>
    /// WebGL 2.0 的 3D 网格着色器程序：把"真实 3D 顶点 + MVP 矩阵"投到裁剪空间，
    /// 由硬件深度缓冲做遮挡（不再用画家算法做排序）。
    /// <para>顶点布局与 <see cref="VertexPositionColorTexture"/> 严格一致：
    /// position(vec4)@0 / color(unorm8x4)@16 / uv(vec2)@20，步长 28。</para>
    /// <para>片元 = 纹理 × 逐顶点颜色。当纹理用 1×1 白图时，结果即为逐顶点 Lambert 色。</para>
    /// </summary>
    internal sealed class WebGL_ShaderProgram_3D
    {
        private const string VertexSource = """
            #version 300 es
            in vec4 aPosition;
            in vec4 aColor;
            in vec2 aTexCoord;

            uniform mat4 uMVP;

            out vec2 vTexCoord;
            out vec4 vColor;

            void main()
            {
                gl_Position = uMVP * aPosition;
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

        internal readonly JSObject Program;
        internal readonly JSObject? MvpLocation;
        internal readonly JSObject? TextureLocation;
        internal readonly int PositionLocation;
        internal readonly int ColorLocation;
        internal readonly int TexCoordLocation;

        internal WebGL_ShaderProgram_3D()
        {
            Program = JSBind_WEBGL20.CreateProgram();
            JSObject vertexShader = Compile(JSBind_WEBGL20.VERTEX_SHADER, VertexSource);
            JSObject fragmentShader = Compile(JSBind_WEBGL20.FRAGMENT_SHADER, FragmentSource);
            JSBind_WEBGL20.AttachShader(Program, vertexShader);
            JSBind_WEBGL20.AttachShader(Program, fragmentShader);
            JSBind_WEBGL20.LinkProgram(Program);
            if (JSBind_WEBGL20.GetProgramParameter(Program, JSBind_WEBGL20.LINK_STATUS) == 0)
                throw new InvalidOperationException("3D 着色器链接失败: " + JSBind_WEBGL20.GetProgramInfoLog(Program));
            JSBind_WEBGL20.DeleteShader(vertexShader);
            JSBind_WEBGL20.DeleteShader(fragmentShader);

            MvpLocation = JSBind_WEBGL20.GetUniformLocation(Program, "uMVP");
            TextureLocation = JSBind_WEBGL20.GetUniformLocation(Program, "uTexture");
            PositionLocation = JSBind_WEBGL20.GetAttribLocation(Program, "aPosition");
            ColorLocation = JSBind_WEBGL20.GetAttribLocation(Program, "aColor");
            TexCoordLocation = JSBind_WEBGL20.GetAttribLocation(Program, "aTexCoord");
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
                throw new InvalidOperationException($"3D 着色器编译失败: {log}");
            }
            return shader;
        }

        public void Dispose() => JSBind_WEBGL20.DeleteProgram(Program);
    }
}
