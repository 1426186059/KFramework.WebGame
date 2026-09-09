using System.Runtime.InteropServices.JavaScript;

namespace KFramework.Graphics;

/// <summary>
/// WebGL 2.0 的底层绑定。所有方法一对一映射到 <c>gl.xxx</c>，由 wwwroot/gl.js 提供实现。
/// 这只是薄封装，上层请用 <see cref="GraphicsDevice"/> / <see cref="SpriteBatch"/>。
/// </summary>
internal static partial class GL
{
    #region 常量

    public const int FALSE = 0;
    public const int TRUE = 1;
    public const int NONE = 0;

    public const int ZERO = 0;
    public const int ONE = 1;
    public const int SRC_COLOR = 0x0300;
    public const int ONE_MINUS_SRC_COLOR = 0x0301;
    public const int SRC_ALPHA = 0x0302;
    public const int ONE_MINUS_SRC_ALPHA = 0x0303;
    public const int DST_ALPHA = 0x0304;
    public const int ONE_MINUS_DST_ALPHA = 0x0305;

    public const int FUNC_ADD = 0x8006;

    public const int BLEND = 0x0BE2;
    public const int DEPTH_TEST = 0x0B71;
    public const int STENCIL_TEST = 0x0B90;
    public const int SCISSOR_TEST = 0x0C11;
    public const int CULL_FACE = 0x0B44;

    public const int COLOR_BUFFER_BIT = 0x00004000;
    public const int DEPTH_BUFFER_BIT = 0x00000100;
    public const int STENCIL_BUFFER_BIT = 0x00000400;

    public const int POINTS = 0x0000;
    public const int LINES = 0x0001;
    public const int TRIANGLES = 0x0004;
    public const int TRIANGLE_STRIP = 0x0005;

    public const int ARRAY_BUFFER = 0x8892;
    public const int ELEMENT_ARRAY_BUFFER = 0x8893;
    public const int STATIC_DRAW = 0x88E4;
    public const int DYNAMIC_DRAW = 0x88E8;
    public const int STREAM_DRAW = 0x88E0;

    public const int BYTE = 0x1400;
    public const int UNSIGNED_BYTE = 0x1401;
    public const int SHORT = 0x1402;
    public const int UNSIGNED_SHORT = 0x1403;
    public const int INT = 0x1404;
    public const int UNSIGNED_INT = 0x1405;
    public const int FLOAT = 0x1406;

    public const int VERTEX_SHADER = 0x8B31;
    public const int FRAGMENT_SHADER = 0x8B30;
    public const int COMPILE_STATUS = 0x8B81;
    public const int LINK_STATUS = 0x8B82;

    public const int TEXTURE_2D = 0x0DE1;
    public const int TEXTURE0 = 0x84C0;
    public const int RGB = 0x1907;
    public const int RGBA = 0x1908;
    public const int RGBA8 = 0x8058;

    public const int NEAREST = 0x2600;
    public const int LINEAR = 0x2601;
    public const int NEAREST_MIPMAP_NEAREST = 0x2700;
    public const int LINEAR_MIPMAP_LINEAR = 0x2703;

    public const int TEXTURE_MAG_FILTER = 0x2800;
    public const int TEXTURE_MIN_FILTER = 0x2801;
    public const int TEXTURE_WRAP_S = 0x2802;
    public const int TEXTURE_WRAP_T = 0x2803;
    public const int CLAMP_TO_EDGE = 0x812F;
    public const int REPEAT = 0x2901;
    public const int MIRRORED_REPEAT = 0x8370;

    public const int UNPACK_ALIGNMENT = 0x0CF5;
    public const int UNPACK_FLIP_Y = 0x9240;
    public const int UNPACK_PREMULTIPLY_ALPHA = 0x9241;

    public const int MAX_TEXTURE_SIZE = 0x0D33;
    public const int VERSION = 0x1F02;
    public const int RENDERER = 0x1F01;
    public const int NO_ERROR = 0;

    #endregion

    #region 上下文

    [JSImport("initContext", "gl")]
    internal static partial bool InitContext(string canvasId);

    [JSImport("isContextLost", "gl")]
    internal static partial bool IsContextLost();

    [JSImport("getParameterInt", "gl")]
    internal static partial int GetParameterInt(int pname);

    [JSImport("getParameterString", "gl")]
    internal static partial string GetParameterString(int pname);

    [JSImport("getError", "gl")]
    internal static partial int GetError();

    [JSImport("readPixel", "gl")]
    internal static partial void ReadPixel(int x, int y, [JSMarshalAs<JSType.MemoryView>] Span<byte> rgba);

    #endregion

    #region 着色器 / 程序

    [JSImport("createShader", "gl")]
    internal static partial JSObject CreateShader(int type);

    [JSImport("shaderSource", "gl")]
    internal static partial void ShaderSource(JSObject shader, string source);

    [JSImport("compileShader", "gl")]
    internal static partial void CompileShader(JSObject shader);

    [JSImport("getShaderParameter", "gl")]
    internal static partial int GetShaderParameter(JSObject shader, int pname);

    [JSImport("getShaderInfoLog", "gl")]
    internal static partial string GetShaderInfoLog(JSObject shader);

    [JSImport("deleteShader", "gl")]
    internal static partial void DeleteShader(JSObject shader);

    [JSImport("createProgram", "gl")]
    internal static partial JSObject CreateProgram();

    [JSImport("attachShader", "gl")]
    internal static partial void AttachShader(JSObject program, JSObject shader);

    [JSImport("linkProgram", "gl")]
    internal static partial void LinkProgram(JSObject program);

    [JSImport("getProgramParameter", "gl")]
    internal static partial int GetProgramParameter(JSObject program, int pname);

    [JSImport("getProgramInfoLog", "gl")]
    internal static partial string GetProgramInfoLog(JSObject program);

    [JSImport("useProgram", "gl")]
    internal static partial void UseProgram(JSObject program);

    [JSImport("deleteProgram", "gl")]
    internal static partial void DeleteProgram(JSObject program);

    [JSImport("getUniformLocation", "gl")]
    internal static partial JSObject? GetUniformLocation(JSObject program, string name);

    [JSImport("getAttribLocation", "gl")]
    internal static partial int GetAttribLocation(JSObject program, string name);

    [JSImport("uniform1i", "gl")]
    internal static partial void Uniform1i(JSObject location, int v);

    [JSImport("uniform1f", "gl")]
    internal static partial void Uniform1f(JSObject location, float v);

    [JSImport("uniform4f", "gl")]
    internal static partial void Uniform4f(JSObject location, float x, float y, float z, float w);

    /// <summary>矩阵按 16 个 float 的小端字节流传入（JS 侧再还原成 Float32Array）。</summary>
    [JSImport("uniformMatrix4fv", "gl")]
    internal static partial void UniformMatrix4fv(JSObject location, int transpose,
        [JSMarshalAs<JSType.MemoryView>] Span<byte> value);

    #endregion

    #region 缓冲 / 顶点数组

    [JSImport("createBuffer", "gl")]
    internal static partial JSObject CreateBuffer();

    [JSImport("bindBuffer", "gl")]
    internal static partial void BindBuffer(int target, JSObject buffer);

    [JSImport("bufferDataSize", "gl")]
    internal static partial void BufferDataSize(int target, int size, int usage);

    [JSImport("bufferData", "gl")]
    internal static partial void BufferData(int target, [JSMarshalAs<JSType.MemoryView>] Span<byte> data, int usage);

    [JSImport("bufferSubData", "gl")]
    internal static partial void BufferSubData(int target, int offset, [JSMarshalAs<JSType.MemoryView>] Span<byte> data);

    [JSImport("deleteBuffer", "gl")]
    internal static partial void DeleteBuffer(JSObject buffer);

    [JSImport("createVertexArray", "gl")]
    internal static partial JSObject CreateVertexArray();

    [JSImport("bindVertexArray", "gl")]
    internal static partial void BindVertexArray(JSObject vao);

    [JSImport("enableVertexAttribArray", "gl")]
    internal static partial void EnableVertexAttribArray(int index);

    [JSImport("vertexAttribPointer", "gl")]
    internal static partial void VertexAttribPointer(int index, int size, int type, bool normalized, int stride, int offset);

    #endregion

    #region 纹理

    [JSImport("createTexture", "gl")]
    internal static partial JSObject CreateTexture();

    [JSImport("bindTexture", "gl")]
    internal static partial void BindTexture(int target, JSObject texture);

    [JSImport("texImage2D", "gl")]
    internal static partial void TexImage2D(int target, int level, int internalFormat, int width, int height, int border, int format, int type,
        [JSMarshalAs<JSType.MemoryView>] Span<byte> data);

    [JSImport("texSubImage2D", "gl")]
    internal static partial void TexSubImage2D(int target, int level, int xoffset, int yoffset, int width, int height, int format, int type,
        [JSMarshalAs<JSType.MemoryView>] Span<byte> data);

    [JSImport("texParameteri", "gl")]
    internal static partial void TexParameteri(int target, int pname, int param);

    [JSImport("activeTexture", "gl")]
    internal static partial void ActiveTexture(int unit);

    [JSImport("deleteTexture", "gl")]
    internal static partial void DeleteTexture(JSObject texture);

    [JSImport("pixelStorei", "gl")]
    internal static partial void PixelStorei(int pname, int param);

    [JSImport("generateMipmap", "gl")]
    internal static partial void GenerateMipmap(int target);

    #endregion

    #region 状态 / 绘制

    [JSImport("enable", "gl")]
    internal static partial void Enable(int cap);

    [JSImport("disable", "gl")]
    internal static partial void Disable(int cap);

    [JSImport("blendFuncSeparate", "gl")]
    internal static partial void BlendFuncSeparate(int srcRGB, int dstRGB, int srcA, int dstA);

    [JSImport("blendEquation", "gl")]
    internal static partial void BlendEquation(int mode);

    [JSImport("clearColor", "gl")]
    internal static partial void ClearColor(float r, float g, float b, float a);

    [JSImport("clear", "gl")]
    internal static partial void Clear(int mask);

    [JSImport("viewport", "gl")]
    internal static partial void Viewport(int x, int y, int width, int height);

    [JSImport("scissor", "gl")]
    internal static partial void Scissor(int x, int y, int width, int height);

    [JSImport("drawElements", "gl")]
    internal static partial void DrawElements(int mode, int count, int type, int offset);

    [JSImport("drawArrays", "gl")]
    internal static partial void DrawArrays(int mode, int first, int count);

    #endregion
}
