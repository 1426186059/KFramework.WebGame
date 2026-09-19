using System.Runtime.InteropServices.JavaScript;

namespace KFramework.MonoGame
{

    /// <summary>
    /// WebGL 2.0 的底层绑定。所有方法一对一映射到 <c>JSBind_GL.xxx</c>，由 KFramework.TSEngine/src/gl.ts 编译出的 wwwroot/jsengine/gl.js 提供实现（本绑定依赖 KFramework.TSEngine 项目）。
    /// 这只是薄封装，上层请用 <see cref="GraphicsDevice"/> / <see cref="SpriteBatch"/>。
    /// </summary>
    internal static partial class JSBind_GL
    {
        #region 常量

        // —— 基础布尔 / 空值：GL 以 0/1 表示 false/true，NONE 即 0（pname 缺省值） ——
        public const int FALSE = 0;
        public const int TRUE = 1;
        public const int NONE = 0;

        // —— 混合因子（blendFuncSeparate 的源/目标因子） ——
        public const int ZERO = 0;
        public const int ONE = 1;
        public const int SRC_COLOR = 0x0300;
        public const int ONE_MINUS_SRC_COLOR = 0x0301;
        public const int SRC_ALPHA = 0x0302;
        public const int ONE_MINUS_SRC_ALPHA = 0x0303;
        public const int DST_ALPHA = 0x0304;
        public const int ONE_MINUS_DST_ALPHA = 0x0305;

        // —— 混合方程（blendEquation 的参数） ——
        public const int FUNC_ADD = 0x8006;

        // —— 渲染状态开关 & 面/绕序（enable/disable、cullFace、frontFace） ——
        public const int BLEND = 0x0BE2;
        public const int DEPTH_TEST = 0x0B71;
        public const int STENCIL_TEST = 0x0B90;
        public const int SCISSOR_TEST = 0x0C11;
        public const int CULL_FACE = 0x0B44;
        public const int FRONT = 0x0404;
        public const int BACK = 0x0405;
        public const int CW = 0x0900;
        public const int CCW = 0x0901;

        // —— 深度比较函数（glDepthFunc），与 CompareFunction 枚举对应 ——
        public const int NEVER = 0x0200;
        public const int LESS = 0x0201;
        public const int EQUAL = 0x0202;
        public const int LEQUAL = 0x0203;
        public const int GREATER = 0x0204;
        public const int NOTEQUAL = 0x0205;
        public const int GEQUAL = 0x0206;
        public const int ALWAYS = 0x0207;

        // —— 清除缓冲位掩码（clear 的 mask，可位或组合） ——
        public const int COLOR_BUFFER_BIT = 0x00004000;
        public const int DEPTH_BUFFER_BIT = 0x00000100;
        public const int STENCIL_BUFFER_BIT = 0x00000400;

        // —— 图元类型（drawArrays / drawElements 的 mode） ——
        public const int POINTS = 0x0000;
        public const int LINES = 0x0001;
        public const int TRIANGLES = 0x0004;
        public const int TRIANGLE_STRIP = 0x0005;

        // —— 缓冲目标 & 用法（bindBuffer、bufferData 的 target / usage） ——
        public const int ARRAY_BUFFER = 0x8892;
        public const int ELEMENT_ARRAY_BUFFER = 0x8893;
        public const int STATIC_DRAW = 0x88E4;
        public const int DYNAMIC_DRAW = 0x88E8;
        public const int STREAM_DRAW = 0x88E0;

        // —— 元素数据类型（顶点属性 / 像素数据的单个元素类型） ——
        public const int BYTE = 0x1400;
        public const int UNSIGNED_BYTE = 0x1401;
        public const int SHORT = 0x1402;
        public const int UNSIGNED_SHORT = 0x1403;
        public const int INT = 0x1404;
        public const int UNSIGNED_INT = 0x1405;
        public const int FLOAT = 0x1406;

        // —— 着色器类型 & 编译/链接状态查询 ——
        public const int VERTEX_SHADER = 0x8B31;
        public const int FRAGMENT_SHADER = 0x8B30;
        public const int COMPILE_STATUS = 0x8B81;
        public const int LINK_STATUS = 0x8B82;

        // —— 纹理目标 & 像素格式（bindTexture、texImage2D 的 format / internalFormat） ——
        public const int TEXTURE_2D = 0x0DE1;
        public const int TEXTURE0 = 0x84C0;
        public const int RGB = 0x1907;
        public const int RGBA = 0x1908;
        public const int RGBA8 = 0x8058;

        // —— 纹理过滤方式（TEXTURE_MIN/MAG_FILTER 的取值） ——
        public const int NEAREST = 0x2600;
        public const int LINEAR = 0x2601;
        public const int NEAREST_MIPMAP_NEAREST = 0x2700;
        public const int LINEAR_MIPMAP_LINEAR = 0x2703;

        // —— 纹理参数名 & 包裹方式（texParameteri 的 pname / param） ——
        public const int TEXTURE_MAG_FILTER = 0x2800;
        public const int TEXTURE_MIN_FILTER = 0x2801;
        public const int TEXTURE_WRAP_S = 0x2802;
        public const int TEXTURE_WRAP_T = 0x2803;
        public const int CLAMP_TO_EDGE = 0x812F;
        public const int REPEAT = 0x2901;
        public const int MIRRORED_REPEAT = 0x8370;

        // —— 像素上传参数（pixelStorei 的 pname） ——
        public const int UNPACK_ALIGNMENT = 0x0CF5;
        public const int UNPACK_FLIP_Y = 0x9240;
        public const int UNPACK_PREMULTIPLY_ALPHA = 0x9241;

        // —— 上下文查询参数 & 错误码（getParameter* 的 pname、getError 返回值） ——
        public const int MAX_TEXTURE_SIZE = 0x0D33;
        public const int VERSION = 0x1F02;
        public const int RENDERER = 0x1F01;
        public const int NO_ERROR = 0;

        // —— 帧缓冲 / 渲染缓冲（离屏渲染 RenderTarget，照 MonoGame 的 FramebufferHelper） ——
        public const int FRAMEBUFFER = 0x8D40;
        public const int READ_FRAMEBUFFER = 0x8CA8;
        public const int DRAW_FRAMEBUFFER = 0x8CA9;
        public const int RENDERBUFFER = 0x8D41;
        public const int COLOR_ATTACHMENT0 = 0x8CE0;
        public const int DEPTH_ATTACHMENT = 0x8D00;
        public const int STENCIL_ATTACHMENT = 0x8D20;
        public const int DEPTH_STENCIL_ATTACHMENT = 0x821A;
        // 完整性检查返回值：FRAMEBUFFER_COMPLETE 表示 FBO 可用
        public const int FRAMEBUFFER_COMPLETE = 0x8CD5;

        // —— renderbuffer 的内部格式（深度 / 模板，照 GLES3 的 RenderbufferStorage） ——
        public const int DEPTH_COMPONENT16 = 0x81A5;
        public const int DEPTH_COMPONENT24 = 0x81A6;
        public const int DEPTH_COMPONENT32F = 0x8CAC;
        public const int DEPTH24_STENCIL8 = 0x88F0;
        public const int STENCIL_INDEX8 = 0x8D48;

        #endregion

        #region 上下文

        /// <summary>初始化 WebGL2 上下文，绑定到指定 canvas。</summary>
        [JSImport("initContext", "gl")]
        internal static partial bool InitContext(string canvasId);

        /// <summary>上下文是否丢失（丢失需重建）。</summary>
        [JSImport("isContextLost", "gl")]
        internal static partial bool IsContextLost();

        /// <summary>读取整数型上下文参数（如 MAX_TEXTURE_SIZE）。</summary>
        [JSImport("getParameterInt", "gl")]
        internal static partial int GetParameterInt(int pname);

        /// <summary>读取字符串型上下文参数（如 VERSION / RENDERER）。</summary>
        [JSImport("getParameterString", "gl")]
        internal static partial string GetParameterString(int pname);

        /// <summary>取并清空当前 GL 错误码。</summary>
        [JSImport("getError", "gl")]
        internal static partial int GetError();

        /// <summary>读取单像素 RGBA8（调试用）。</summary>
        [JSImport("readPixel", "gl")]
        internal static partial void ReadPixel(int x, int y, [JSMarshalAs<JSType.MemoryView>] Span<byte> rgba);

        #endregion

        #region 着色器 / 程序

        /// <summary>创建着色器对象（VERTEX_SHADER / FRAGMENT_SHADER）。</summary>
        [JSImport("createShader", "gl")]
        internal static partial JSObject CreateShader(int type);

        /// <summary>设置着色器 GLSL 源码。</summary>
        [JSImport("shaderSource", "gl")]
        internal static partial void ShaderSource(JSObject shader, string source);

        /// <summary>编译着色器。</summary>
        [JSImport("compileShader", "gl")]
        internal static partial void CompileShader(JSObject shader);

        /// <summary>取着色器编译状态/参数（如 COMPILE_STATUS）。</summary>
        [JSImport("getShaderParameter", "gl")]
        internal static partial int GetShaderParameter(JSObject shader, int pname);

        /// <summary>取着色器编译错误日志。</summary>
        [JSImport("getShaderInfoLog", "gl")]
        internal static partial string GetShaderInfoLog(JSObject shader);

        /// <summary>删除着色器对象。</summary>
        [JSImport("deleteShader", "gl")]
        internal static partial void DeleteShader(JSObject shader);

        /// <summary>创建着色器程序。</summary>
        [JSImport("createProgram", "gl")]
        internal static partial JSObject CreateProgram();

        /// <summary>挂载着色器到程序。</summary>
        [JSImport("attachShader", "gl")]
        internal static partial void AttachShader(JSObject program, JSObject shader);

        /// <summary>链接程序（把着色器组合成可执行管线）。</summary>
        [JSImport("linkProgram", "gl")]
        internal static partial void LinkProgram(JSObject program);

        /// <summary>取程序链接状态（如 LINK_STATUS）。</summary>
        [JSImport("getProgramParameter", "gl")]
        internal static partial int GetProgramParameter(JSObject program, int pname);

        /// <summary>取程序链接错误日志。</summary>
        [JSImport("getProgramInfoLog", "gl")]
        internal static partial string GetProgramInfoLog(JSObject program);

        /// <summary>启用着色器程序。</summary>
        [JSImport("useProgram", "gl")]
        internal static partial void UseProgram(JSObject program);

        /// <summary>删除程序。</summary>
        [JSImport("deleteProgram", "gl")]
        internal static partial void DeleteProgram(JSObject program);

        /// <summary>取 uniform 变量位置（按名查找）。</summary>
        [JSImport("getUniformLocation", "gl")]
        internal static partial JSObject? GetUniformLocation(JSObject program, string name);

        /// <summary>取顶点属性位置（按名查找）。</summary>
        [JSImport("getAttribLocation", "gl")]
        internal static partial int GetAttribLocation(JSObject program, string name);

        /// <summary>设置 int uniform。</summary>
        [JSImport("uniform1i", "gl")]
        internal static partial void Uniform1i(JSObject location, int v);

        /// <summary>设置 float uniform。</summary>
        [JSImport("uniform1f", "gl")]
        internal static partial void Uniform1f(JSObject location, float v);

        /// <summary>设置 vec4 uniform。</summary>
        [JSImport("uniform4f", "gl")]
        internal static partial void Uniform4f(JSObject location, float x, float y, float z, float w);

        /// <summary>设置 mat4 uniform（16 个 float 的小端字节流）。</summary>
        [JSImport("uniformMatrix4fv", "gl")]
        internal static partial void UniformMatrix4fv(JSObject location, int transpose,
            [JSMarshalAs<JSType.MemoryView>] Span<byte> value);

        #endregion

        #region 缓冲 / 顶点数组

        /// <summary>创建缓冲对象。</summary>
        [JSImport("createBuffer", "gl")]
        internal static partial JSObject CreateBuffer();

        /// <summary>绑定缓冲到目标（ARRAY_BUFFER / ELEMENT_ARRAY_BUFFER）。</summary>
        [JSImport("bindBuffer", "gl")]
        internal static partial void BindBuffer(int target, JSObject buffer);

        /// <summary>预分配指定字节数的缓冲（不上传数据）。</summary>
        [JSImport("bufferDataSize", "gl")]
        internal static partial void BufferDataSize(int target, int size, int usage);

        /// <summary>上传数据到缓冲。</summary>
        [JSImport("bufferData", "gl")]
        internal static partial void BufferData(int target, [JSMarshalAs<JSType.MemoryView>] Span<byte> data, int usage);

        /// <summary>局部更新缓冲（从 offset 起覆盖）。</summary>
        [JSImport("bufferSubData", "gl")]
        internal static partial void BufferSubData(int target, int offset, [JSMarshalAs<JSType.MemoryView>] Span<byte> data);

        /// <summary>删除缓冲。</summary>
        [JSImport("deleteBuffer", "gl")]
        internal static partial void DeleteBuffer(JSObject buffer);

        /// <summary>创建 VAO（顶点数组对象）。</summary>
        [JSImport("createVertexArray", "gl")]
        internal static partial JSObject CreateVertexArray();

        /// <summary>绑定 VAO（一次保存所有顶点状态）。</summary>
        [JSImport("bindVertexArray", "gl")]
        internal static partial void BindVertexArray(JSObject vao);

        /// <summary>启用第 index 个顶点属性。</summary>
        [JSImport("enableVertexAttribArray", "gl")]
        internal static partial void EnableVertexAttribArray(int index);

        /// <summary>设置顶点属性指针（格式/是否归一化/步长/偏移）。</summary>
        [JSImport("vertexAttribPointer", "gl")]
        internal static partial void VertexAttribPointer(int index, int size, int type, bool normalized, int stride, int offset);

        #endregion

        #region 纹理

        /// <summary>创建纹理对象。</summary>
        [JSImport("createTexture", "gl")]
        internal static partial JSObject CreateTexture();

        /// <summary>绑定纹理到目标（TEXTURE_2D）。</summary>
        [JSImport("bindTexture", "gl")]
        internal static partial void BindTexture(int target, JSObject texture);

        /// <summary>上传 RGBA 像素到 2D 纹理（level 0）。</summary>
        [JSImport("texImage2D", "gl")]
        internal static partial void TexImage2D(int target, int level, int internalFormat, int width, int height, int border, int format, int type,
            [JSMarshalAs<JSType.MemoryView>] Span<byte> data);

        /// <summary>局部更新纹理像素。</summary>
        [JSImport("texSubImage2D", "gl")]
        internal static partial void TexSubImage2D(int target, int level, int xoffset, int yoffset, int width, int height, int format, int type,
            [JSMarshalAs<JSType.MemoryView>] Span<byte> data);

        /// <summary>上传 GPU 压缩纹理字节（DXT / ASTC / BC7 等，internalFormat 见 SurfaceFormat）。</summary>
        [JSImport("compressedTexImage2D", "gl")]
        internal static partial void CompressedTexImage2D(int target, int level, int internalFormat, int width, int height, int border,
            [JSMarshalAs<JSType.MemoryView>] Span<byte> data);

        /// <summary>设置纹理参数（过滤方式 / 包裹方式）。</summary>
        [JSImport("texParameteri", "gl")]
        internal static partial void TexParameteri(int target, int pname, int param);

        /// <summary>激活纹理单元（TEXTURE0 + n）。</summary>
        [JSImport("activeTexture", "gl")]
        internal static partial void ActiveTexture(int unit);

        /// <summary>删除纹理。</summary>
        [JSImport("deleteTexture", "gl")]
        internal static partial void DeleteTexture(JSObject texture);

        /// <summary>查询是否支持某 WebGL 扩展（如 WEBGL_compressed_texture_s3tc）。</summary>
        [JSImport("hasExtension", "gl")]
        internal static partial bool HasExtension(string name);

        /// <summary>设置像素上传参数（对齐 / 翻转 Y / 预乘 alpha）。</summary>
        [JSImport("pixelStorei", "gl")]
        internal static partial void PixelStorei(int pname, int param);

        /// <summary>为当前纹理生成 mipmap。</summary>
        [JSImport("generateMipmap", "gl")]
        internal static partial void GenerateMipmap(int target);

        /// <summary>为 2D 纹理分配未初始化的存储（渲染目标用：内容由 GPU 绘制，不传像素数据）。</summary>
        [JSImport("texImage2DStorage", "gl")]
        internal static partial void TexImage2DStorage(int target, int level, int internalFormat,
            int width, int height, int format, int type);

        #endregion

        #region 帧缓冲（离屏渲染 / RenderTarget）

        /// <summary>创建帧缓冲对象（FBO）。</summary>
        [JSImport("createFramebuffer", "gl")]
        internal static partial JSObject CreateFramebuffer();

        /// <summary>绑定 FBO；传 null 表示绑回默认帧缓冲（画布）。</summary>
        [JSImport("bindFramebuffer", "gl")]
        internal static partial void BindFramebuffer(int target, JSObject? framebuffer);

        /// <summary>删除 FBO。</summary>
        [JSImport("deleteFramebuffer", "gl")]
        internal static partial void DeleteFramebuffer(JSObject framebuffer);

        /// <summary>把一张纹理挂到 FBO 的颜色附着点（attachment = COLOR_ATTACHMENT0 + i）。</summary>
        [JSImport("framebufferTexture2D", "gl")]
        internal static partial void FramebufferTexture2D(int target, int attachment, int texTarget, JSObject texture, int level);

        /// <summary>检查 FBO 完整性（返回 FRAMEBUFFER_COMPLETE 表示可用）。</summary>
        [JSImport("checkFramebufferStatus", "gl")]
        internal static partial int CheckFramebufferStatus(int target);

        /// <summary>创建渲染缓冲对象（深度 / 模板附件）。</summary>
        [JSImport("createRenderbuffer", "gl")]
        internal static partial JSObject CreateRenderbuffer();

        /// <summary>绑定渲染缓冲对象到 RENDERBUFFER 目标。</summary>
        [JSImport("bindRenderbuffer", "gl")]
        internal static partial void BindRenderbuffer(int target, JSObject? renderbuffer);

        /// <summary>为当前渲染缓冲分配存储（internalFormat 用 DEPTH_COMPONENT16 / DEPTH24_STENCIL8 等）。</summary>
        [JSImport("renderbufferStorage", "gl")]
        internal static partial void RenderbufferStorage(int target, int internalFormat, int width, int height);

        /// <summary>把渲染缓冲挂到 FBO 的指定附着点（DEPTH_ATTACHMENT / STENCIL_ATTACHMENT 等）。</summary>
        [JSImport("framebufferRenderbuffer", "gl")]
        internal static partial void FramebufferRenderbuffer(int target, int attachment, int rbTarget, JSObject? renderbuffer);

        /// <summary>删除渲染缓冲对象。</summary>
        [JSImport("deleteRenderbuffer", "gl")]
        internal static partial void DeleteRenderbuffer(JSObject renderbuffer);

        #endregion

        #region 状态 / 绘制

        /// <summary>开启 GL 能力（BLEND / DEPTH_TEST / CULL_FACE 等）。</summary>
        [JSImport("enable", "gl")]
        internal static partial void Enable(int cap);

        /// <summary>关闭 GL 能力。</summary>
        [JSImport("disable", "gl")]
        internal static partial void Disable(int cap);

        /// <summary>设置 RGB 与 Alpha 各自独立的混合函数。</summary>
        [JSImport("blendFuncSeparate", "gl")]
        internal static partial void BlendFuncSeparate(int srcRGB, int dstRGB, int srcA, int dstA);

        /// <summary>设置混合方程（FUNC_ADD 等）。</summary>
        [JSImport("blendEquation", "gl")]
        internal static partial void BlendEquation(int mode);

        /// <summary>设置清屏颜色。</summary>
        [JSImport("clearColor", "gl")]
        internal static partial void ClearColor(float r, float g, float b, float a);

        /// <summary>清除缓冲（COLOR / DEPTH / STENCIL_BUFFER_BIT）。</summary>
        [JSImport("clear", "gl")]
        internal static partial void Clear(int mask);

        /// <summary>设置视口。</summary>
        [JSImport("viewport", "gl")]
        internal static partial void Viewport(int x, int y, int width, int height);

        /// <summary>设置裁剪矩形（scissor test 生效区域）。</summary>
        [JSImport("scissor", "gl")]
        internal static partial void Scissor(int x, int y, int width, int height);

        /// <summary>按索引缓冲绘制图元。</summary>
        [JSImport("drawElements", "gl")]
        internal static partial void DrawElements(int mode, int count, int type, int offset);

        /// <summary>按顶点顺序绘制图元。</summary>
        [JSImport("drawArrays", "gl")]
        internal static partial void DrawArrays(int mode, int first, int count);

        /// <summary>设置背面剔除模式（FRONT / BACK）。</summary>
        [JSImport("cullFace", "gl")]
        internal static partial void CullFace(int mode);

        /// <summary>设置正面绕序（CW / CCW）。</summary>
        [JSImport("frontFace", "gl")]
        internal static partial void FrontFace(int mode);

        /// <summary>是否写入深度缓冲。</summary>
        [JSImport("depthMask", "gl")]
        internal static partial void DepthMask(bool flag);

        /// <summary>设置深度比较函数。</summary>
        [JSImport("depthFunc", "gl")]
        internal static partial void DepthFunc(int func);

        #endregion
    }
}
