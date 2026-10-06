using System.Runtime.InteropServices.JavaScript;

namespace KFramework.MonoGame
{

    /// <summary>
    /// WebGL 2.0 的底层绑定。所有方法一对一映射到 <c>JSBind_WEBGL20.xxx</c>，由 KFramework.TSEngine/src/render_webgl20.ts 编译出的 wwwroot/jsengine/render_webgl20.js 提供实现（本绑定依赖 KFramework.TSEngine 项目）。
    /// 这只是薄封装，上层请用 <see cref="GraphicsDevice"/> / <see cref="SpriteBatch"/>。
    /// </summary>
    public static partial class JSBind_WEBGL20
    {

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
        public const int DST_COLOR = 0x0306;
        public const int ONE_MINUS_DST_COLOR = 0x0307;
        public const int SRC_ALPHA_SATURATE = 0x0308;

        // —— 模板操作（stencilOp 的参数，OpenGL ES 3.0 标准值） ——
        // 比较函数复用上面那组 NEVER / LESS / … / ALWAYS：GL 的 stencilFunc 与 depthFunc 用的是同一批常量。
        public const int STENCIL_KEEP = 0x1E00;
        public const int STENCIL_REPLACE = 0x1E01;
        public const int STENCIL_INCR = 0x1E02;          // 饱和加 1
        public const int STENCIL_DECR = 0x1E03;          // 饱和减 1
        public const int STENCIL_INVERT = 0x150A;
        public const int STENCIL_INCR_WRAP = 0x8507;
        public const int STENCIL_DECR_WRAP = 0x8508;

        // —— 混合方程（blendEquation 的参数） ——
        // 参考文档：WebGL 2.0 规范 / MDN WebGL2RenderingContext.blendEquation
        //   https://developer.mozilla.org/en-US/docs/Web/API/WebGL2RenderingContext/blendEquation
        // 各枚举值与 OpenGL ES 3.0 一致：FUNC_ADD=0x8006，FUNC_SUBTRACT=0x800A，
        // FUNC_REVERSE_SUBTRACT=0x800B，MIN=0x8007，MAX=0x8008
        // （MIN/MAX 在 WebGL2 原生支持；WebGL1 需先启用 EXT_blend_minmax 扩展）。
        public const int BLEND_FUNC_ADD = 0x8006;
        public const int BLEND_FUNC_SUBTRACT = 0x800A;
        public const int BLEND_FUNC_REVERSE_SUBTRACT = 0x800B;
        public const int BLEND_FUNC_MIN = 0x8007;
        public const int BLEND_FUNC_MAX = 0x8008;

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
        public const int MAX_SAMPLES = 0x8D57;   // 用于钳制 RT 请求的 MSAA 采样数上限（与画布 antialias 无关）
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



        /// <summary>初始化 WebGL2 上下文（单画布模型，无需传 id）。</summary>
        [JSImport("initContext", "render_webgl20")]
        public static partial bool InitContext();

        /// <summary>
        /// 设置是否启用 MSAA（<c>antialias</c>）。
        /// 必须在 <see cref="InitContext"/> 之前调用 —— 上下文建好后属性不可改
        ///（照 MonoGame：MSAA 属性在窗口 / 上下文创建之前设置）。
        /// </summary>
        [JSImport("setAntialias", "render_webgl20")]
        public static partial void SetAntialias(bool enabled);

        /// <summary>当前是否启用 MSAA。</summary>
        [JSImport("getAntialias", "render_webgl20")]
        public static partial bool GetAntialias();

        /// <summary>上下文是否丢失（丢失需重建）。</summary>
        [JSImport("isContextLost", "render_webgl20")]
        public static partial bool IsContextLost();

        /// <summary>读取整数型上下文参数（如 MAX_TEXTURE_SIZE）。</summary>
        [JSImport("getParameterInt", "render_webgl20")]
        public static partial int GetParameterInt(int pname);

        /// <summary>读取字符串型上下文参数（如 VERSION / RENDERER）。</summary>
        [JSImport("getParameterString", "render_webgl20")]
        public static partial string GetParameterString(int pname);

        /// <summary>取并清空当前 GL 错误码。</summary>
        [JSImport("getError", "render_webgl20")]
        public static partial int GetError();

        /// <summary>读取单像素 RGBA8（调试用）。</summary>
        [JSImport("readPixel", "render_webgl20")]
        public static partial void ReadPixel(int x, int y, [JSMarshalAs<JSType.MemoryView>] Span<byte> rgba);

        [JSImport("readPixels", "render_webgl20")]
        public static partial void ReadPixels(int x, int y, int width, int height, [JSMarshalAs<JSType.MemoryView>] Span<byte> rgba);



        /// <summary>创建着色器对象（VERTEX_SHADER / FRAGMENT_SHADER）。</summary>
        [JSImport("createShader", "render_webgl20")]
        public static partial JSObject CreateShader(int type);

        /// <summary>设置着色器 GLSL 源码。</summary>
        [JSImport("shaderSource", "render_webgl20")]
        public static partial void ShaderSource(JSObject shader, string source);

        /// <summary>编译着色器。</summary>
        [JSImport("compileShader", "render_webgl20")]
        public static partial void CompileShader(JSObject shader);

        /// <summary>取着色器编译状态/参数（如 COMPILE_STATUS）。</summary>
        [JSImport("getShaderParameter", "render_webgl20")]
        public static partial int GetShaderParameter(JSObject shader, int pname);

        /// <summary>取着色器编译错误日志。</summary>
        [JSImport("getShaderInfoLog", "render_webgl20")]
        public static partial string GetShaderInfoLog(JSObject shader);

        /// <summary>删除着色器对象。</summary>
        [JSImport("deleteShader", "render_webgl20")]
        public static partial void DeleteShader(JSObject shader);

        /// <summary>创建着色器程序。</summary>
        [JSImport("createProgram", "render_webgl20")]
        public static partial JSObject CreateProgram();

        /// <summary>挂载着色器到程序。</summary>
        [JSImport("attachShader", "render_webgl20")]
        public static partial void AttachShader(JSObject program, JSObject shader);

        /// <summary>链接程序（把着色器组合成可执行管线）。</summary>
        [JSImport("linkProgram", "render_webgl20")]
        public static partial void LinkProgram(JSObject program);

        /// <summary>取程序链接状态（如 LINK_STATUS）。</summary>
        [JSImport("getProgramParameter", "render_webgl20")]
        public static partial int GetProgramParameter(JSObject program, int pname);

        /// <summary>取程序链接错误日志。</summary>
        [JSImport("getProgramInfoLog", "render_webgl20")]
        public static partial string GetProgramInfoLog(JSObject program);

        /// <summary>启用着色器程序。</summary>
        [JSImport("useProgram", "render_webgl20")]
        public static partial void UseProgram(JSObject program);

        /// <summary>删除程序。</summary>
        [JSImport("deleteProgram", "render_webgl20")]
        public static partial void DeleteProgram(JSObject program);

        /// <summary>取 uniform 变量位置（按名查找）。</summary>
        [JSImport("getUniformLocation", "render_webgl20")]
        public static partial JSObject? GetUniformLocation(JSObject program, string name);

        /// <summary>取顶点属性位置（按名查找）。</summary>
        [JSImport("getAttribLocation", "render_webgl20")]
        public static partial int GetAttribLocation(JSObject program, string name);

        /// <summary>设置 int uniform。</summary>
        [JSImport("uniform1i", "render_webgl20")]
        public static partial void Uniform1i(JSObject location, int v);

        /// <summary>设置 float uniform。</summary>
        [JSImport("uniform1f", "render_webgl20")]
        public static partial void Uniform1f(JSObject location, float v);

        /// <summary>设置 vec4 uniform。</summary>
        [JSImport("uniform4f", "render_webgl20")]
        public static partial void Uniform4f(JSObject location, float x, float y, float z, float w);

        /// <summary>设置 mat4 uniform（16 个 float 的小端字节流）。</summary>
        [JSImport("uniformMatrix4fv", "render_webgl20")]
        public static partial void UniformMatrix4fv(JSObject location, int transpose,
            [JSMarshalAs<JSType.MemoryView>] Span<byte> value);



        /// <summary>创建缓冲对象。</summary>
        [JSImport("createBuffer", "render_webgl20")]
        public static partial JSObject CreateBuffer();

        /// <summary>绑定缓冲到目标（ARRAY_BUFFER / ELEMENT_ARRAY_BUFFER）。</summary>
        [JSImport("bindBuffer", "render_webgl20")]
        public static partial void BindBuffer(int target, JSObject buffer);

        /// <summary>预分配指定字节数的缓冲（不上传数据）。</summary>
        [JSImport("bufferDataSize", "render_webgl20")]
        public static partial void BufferDataSize(int target, int size, int usage);

        /// <summary>上传数据到缓冲。</summary>
        [JSImport("bufferData", "render_webgl20")]
        public static partial void BufferData(int target, [JSMarshalAs<JSType.MemoryView>] Span<byte> data, int usage);

        /// <summary>局部更新缓冲（从 offset 起覆盖）。</summary>
        [JSImport("bufferSubData", "render_webgl20")]
        public static partial void BufferSubData(int target, int offset, [JSMarshalAs<JSType.MemoryView>] Span<byte> data);

        /// <summary>删除缓冲。</summary>
        [JSImport("deleteBuffer", "render_webgl20")]
        public static partial void DeleteBuffer(JSObject buffer);

        /// <summary>创建 VAO（顶点数组对象）。</summary>
        [JSImport("createVertexArray", "render_webgl20")]
        public static partial JSObject CreateVertexArray();

        /// <summary>绑定 VAO（一次保存所有顶点状态）。</summary>
        [JSImport("bindVertexArray", "render_webgl20")]
        public static partial void BindVertexArray(JSObject vao);

        /// <summary>启用第 index 个顶点属性。</summary>
        [JSImport("enableVertexAttribArray", "render_webgl20")]
        public static partial void EnableVertexAttribArray(int index);

        /// <summary>设置顶点属性指针（格式/是否归一化/步长/偏移）。</summary>
        [JSImport("vertexAttribPointer", "render_webgl20")]
        public static partial void VertexAttribPointer(int index, int size, int type, bool normalized, int stride, int offset);



        /// <summary>创建纹理对象（返回整数句柄，与 WebGPU 一致；JS 侧维护 id → WebGLTexture 映射）。</summary>
        [JSImport("createTexture", "render_webgl20")]
        public static partial int CreateTexture();

        /// <summary>绑定纹理到目标（TEXTURE_2D）。</summary>
        [JSImport("bindTexture", "render_webgl20")]
        public static partial void BindTexture(int target, int texture);

        /// <summary>上传 RGBA 像素到 2D 纹理（level 0）。</summary>
        [JSImport("texImage2D", "render_webgl20")]
        public static partial void TexImage2D(int target, int level, int internalFormat, int width, int height, int border, int format, int type,
            [JSMarshalAs<JSType.MemoryView>] Span<byte> data);

        /// <summary>局部更新纹理像素。</summary>
        [JSImport("texSubImage2D", "render_webgl20")]
        public static partial void TexSubImage2D(int target, int level, int xoffset, int yoffset, int width, int height, int format, int type,
            [JSMarshalAs<JSType.MemoryView>] Span<byte> data);

        /// <summary>上传 GPU 压缩纹理字节（DXT / ASTC / BC7 等，internalFormat 见 SurfaceFormat）。</summary>
        [JSImport("compressedTexImage2D", "render_webgl20")]
        public static partial void CompressedTexImage2D(int target, int level, int internalFormat, int width, int height, int border,
            [JSMarshalAs<JSType.MemoryView>] Span<byte> data);

        /// <summary>设置纹理参数（过滤方式 / 包裹方式）。</summary>
        [JSImport("texParameteri", "render_webgl20")]
        public static partial void TexParameteri(int target, int pname, int param);

        /// <summary>激活纹理单元（TEXTURE0 + n）。</summary>
        [JSImport("activeTexture", "render_webgl20")]
        public static partial void ActiveTexture(int unit);

        /// <summary>删除纹理（参数为整数句柄）。</summary>
        [JSImport("deleteTexture", "render_webgl20")]
        public static partial void DeleteTexture(int texture);

        /// <summary>查询是否支持某 WebGL 扩展（如 WEBGL_compressed_texture_s3tc）。</summary>
        [JSImport("hasExtension", "render_webgl20")]
        public static partial bool HasExtension(string name);

        /// <summary>设置像素上传参数（对齐 / 翻转 Y / 预乘 alpha）。</summary>
        [JSImport("pixelStorei", "render_webgl20")]
        public static partial void PixelStorei(int pname, int param);

        /// <summary>为当前纹理生成 mipmap。</summary>
        [JSImport("generateMipmap", "render_webgl20")]
        public static partial void GenerateMipmap(int target);

        /// <summary>为 2D 纹理分配未初始化的存储（渲染目标用：内容由 GPU 绘制，不传像素数据）。</summary>
        [JSImport("texImage2DStorage", "render_webgl20")]
        public static partial void TexImage2DStorage(int target, int level, int internalFormat,
            int width, int height, int format, int type);



        /// <summary>创建帧缓冲对象（FBO）。</summary>
        [JSImport("createFramebuffer", "render_webgl20")]
        public static partial JSObject CreateFramebuffer();

        /// <summary>绑定 FBO；传 null 表示绑回默认帧缓冲（画布）。</summary>
        [JSImport("bindFramebuffer", "render_webgl20")]
        public static partial void BindFramebuffer(int target, JSObject? framebuffer);

        /// <summary>删除 FBO。</summary>
        [JSImport("deleteFramebuffer", "render_webgl20")]
        public static partial void DeleteFramebuffer(JSObject framebuffer);

        /// <summary>把一张纹理挂到 FBO 的颜色附着点（attachment = COLOR_ATTACHMENT0 + i）；texture 为整数句柄。</summary>
        [JSImport("framebufferTexture2D", "render_webgl20")]
        public static partial void FramebufferTexture2D(int target, int attachment, int texTarget, int texture, int level);

        /// <summary>检查 FBO 完整性（返回 FRAMEBUFFER_COMPLETE 表示可用）。</summary>
        [JSImport("checkFramebufferStatus", "render_webgl20")]
        public static partial int CheckFramebufferStatus(int target);

        /// <summary>创建渲染缓冲对象（深度 / 模板附件）。</summary>
        [JSImport("createRenderbuffer", "render_webgl20")]
        public static partial JSObject CreateRenderbuffer();

        /// <summary>绑定渲染缓冲对象到 RENDERBUFFER 目标。</summary>
        [JSImport("bindRenderbuffer", "render_webgl20")]
        public static partial void BindRenderbuffer(int target, JSObject? renderbuffer);

        /// <summary>为当前渲染缓冲分配存储（internalFormat 用 DEPTH_COMPONENT16 / DEPTH24_STENCIL8 等）。</summary>
        [JSImport("renderbufferStorage", "render_webgl20")]
        public static partial void RenderbufferStorage(int target, int internalFormat, int width, int height);

        /// <summary>把渲染缓冲挂到 FBO 的指定附着点（DEPTH_ATTACHMENT / STENCIL_ATTACHMENT 等）。</summary>
        [JSImport("framebufferRenderbuffer", "render_webgl20")]
        public static partial void FramebufferRenderbuffer(int target, int attachment, int rbTarget, JSObject? renderbuffer);

        /// <summary>删除渲染缓冲对象。</summary>
        [JSImport("deleteRenderbuffer", "render_webgl20")]
        public static partial void DeleteRenderbuffer(JSObject renderbuffer);

        /// <summary>分配多重采样 renderbuffer 存储（RT 级 MSAA 颜色 / 深度附件用）。这是离屏 FBO 的多重采样，与画布 getContext 的 antialias 无关。</summary>
        [JSImport("renderbufferStorageMultisample", "render_webgl20")]
        public static partial void RenderbufferStorageMultisample(int target, int samples, int internalFormat, int width, int height);

        /// <summary>把多重采样帧缓冲解析（resolve）到单采样帧缓冲（RT 级 MSAA 离屏目标解到可采样纹理用；与画布 antialias 无关）。</summary>
        [JSImport("blitFramebuffer", "render_webgl20")]
        public static partial void BlitFramebuffer(
            int srcX0, int srcY0, int srcX1, int srcY1,
            int dstX0, int dstY0, int dstX1, int dstY1,
            int mask, int filter);



        /// <summary>开启 GL 能力（BLEND / DEPTH_TEST / CULL_FACE 等）。</summary>
        [JSImport("enable", "render_webgl20")]
        public static partial void Enable(int cap);

        /// <summary>关闭 GL 能力。</summary>
        [JSImport("disable", "render_webgl20")]
        public static partial void Disable(int cap);

        /// <summary>设置 RGB 与 Alpha 各自独立的混合函数。</summary>
        [JSImport("blendFuncSeparate", "render_webgl20")]
        public static partial void BlendFuncSeparate(int srcRGB, int dstRGB, int srcA, int dstA);

        /// <summary>设置混合方程（BLEND_FUNC_ADD 等）。</summary>
        [JSImport("blendEquation", "render_webgl20")]
        public static partial void BlendEquation(int mode);

        /// <summary>
        /// 分别设置 RGB 与 Alpha 的混合方程（BLEND_FUNC_ADD / SUBTRACT / REVERSE_SUBTRACT / MIN / MAX）。
        /// WebGL2 原生支持 MIN / MAX（WebGL1 需 EXT_blend_minmax 扩展）。
        /// </summary>
        [JSImport("blendEquationSeparate", "render_webgl20")]
        public static partial void BlendEquationSeparate(int modeRGB, int modeAlpha);

        /// <summary>设置清屏颜色。</summary>
        [JSImport("clearColor", "render_webgl20")]
        public static partial void ClearColor(float r, float g, float b, float a);

        /// <summary>清除缓冲（COLOR / DEPTH / STENCIL_BUFFER_BIT）。</summary>
        [JSImport("clear", "render_webgl20")]
        public static partial void Clear(int mask);

        /// <summary>设置视口。</summary>
        [JSImport("viewport", "render_webgl20")]
        public static partial void Viewport(int x, int y, int width, int height);

        /// <summary>设置裁剪矩形（scissor test 生效区域）。</summary>
        [JSImport("scissor", "render_webgl20")]
        public static partial void Scissor(int x, int y, int width, int height);

        /// <summary>按索引缓冲绘制图元。</summary>
        [JSImport("drawElements", "render_webgl20")]
        public static partial void DrawElements(int mode, int count, int type, int offset);

        /// <summary>按顶点顺序绘制图元。</summary>
        [JSImport("drawArrays", "render_webgl20")]
        public static partial void DrawArrays(int mode, int first, int count);

        /// <summary>设置背面剔除模式（FRONT / BACK）。</summary>
        [JSImport("cullFace", "render_webgl20")]
        public static partial void CullFace(int mode);

        /// <summary>设置正面绕序（CW / CCW）。</summary>
        [JSImport("frontFace", "render_webgl20")]
        public static partial void FrontFace(int mode);

        /// <summary>是否写入深度缓冲。</summary>
        [JSImport("depthMask", "render_webgl20")]
        public static partial void DepthMask(bool flag);

        /// <summary>设置深度比较函数。</summary>
        [JSImport("depthFunc", "render_webgl20")]
        public static partial void DepthFunc(int func);

        /// <summary>设置模板写入掩码（哪些位可写）。</summary>
        [JSImport("stencilMask", "render_webgl20")]
        public static partial void StencilMask(int mask);

        /// <summary>设置正反面共用的模板比较函数（func + 参考值 + 读掩码）。</summary>
        [JSImport("stencilFunc", "render_webgl20")]
        public static partial void StencilFunc(int func, int reference, int mask);

        /// <summary>设置正反面共用的模板操作（fail / zfail / zpass）。</summary>
        [JSImport("stencilOp", "render_webgl20")]
        public static partial void StencilOp(int fail, int zfail, int zpass);

        /// <summary>按面设置模板比较函数（face 用 FRONT / BACK / FRONT_AND_BACK）。</summary>
        [JSImport("stencilFuncSeparate", "render_webgl20")]
        public static partial void StencilFuncSeparate(int face, int func, int reference, int mask);

        /// <summary>按面设置模板操作（face 用 FRONT / BACK / FRONT_AND_BACK）。</summary>
        [JSImport("stencilOpSeparate", "render_webgl20")]
        public static partial void StencilOpSeparate(int face, int fail, int zfail, int zpass);

    }
}
