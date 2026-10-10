using System.Runtime.InteropServices.JavaScript;

namespace KFramework.MonoGame
{

    /// <summary>
    /// render_canvas2d 模块绑定：Canvas2D 渲染后端（<see cref="Canvas2DBackend"/>）的平台层。
    /// 依赖 KFramework.TSEngine 项目：本类 JSImport 映射到 src/render_canvas2d.ts，产物由 SyncJsEngine 复制。
    /// <para>
    /// 与 <see cref="JSBind_WEBGL20"/> 的约定一致：.NET 的 <c>Span&lt;T&gt;</c> 在 JS 侧是 MemoryView，
    /// 不是 TypedArray，故一律标 <c>[JSMarshalAs&lt;JSType.MemoryView&gt;]</c>（且只支持 byte / int 元素类型）。
    /// </para>
    /// </summary>
    public static partial class JSBind_Canvas2D
    {
        /// <summary>创建 Canvas2D 上下文（画布元素由 html_canvas 统一管理）。</summary>
        [JSImport("init", "render_canvas2d")]
        public static partial bool Init(bool antialias);

        [JSImport("getRenderer", "render_canvas2d")]
        public static partial string GetRenderer();

        [JSImport("getMaxTextureSize", "render_canvas2d")]
        public static partial int GetMaxTextureSize();

        [JSImport("getError", "render_canvas2d")]
        public static partial int GetError();

        /// <summary>收帧：Canvas2D 随 rAF 自动呈现，无需动作。</summary>
        [JSImport("endFrame", "render_canvas2d")]
        public static partial void EndFrame();

        [JSImport("setViewport", "render_canvas2d")]
        public static partial void SetViewport(int x, int y, int width, int height);

        /// <summary>设置矩形裁剪（Canvas2D 用 clip 实现；必须成对使用 <see cref="ClearScissor"/> 撤销）。</summary>
        [JSImport("setScissor", "render_canvas2d")]
        public static partial void SetScissor(int x, int y, int width, int height);

        [JSImport("clearScissor", "render_canvas2d")]
        public static partial void ClearScissor();

        [JSImport("clear", "render_canvas2d")]
        public static partial void Clear(float r, float g, float b, float a);

        /// <summary>混合模式：0=普通 alpha 混合 1=加色 2=不透明 3=正片叠底。</summary>
        [JSImport("setBlendState", "render_canvas2d")]
        public static partial void SetBlendState(int kind);

        /// <summary>采样方式：true = 双线性（imageSmoothingEnabled），false = 最近邻。</summary>
        [JSImport("setSampler", "render_canvas2d")]
        public static partial void SetSampler(bool linear);

        [JSImport("bindTexture", "render_canvas2d")]
        public static partial void BindTexture(int id);

        /// <summary>下发"局部像素 → 画布像素"的 2×3 仿射（由 <see cref="Canvas2DShaderProgram"/> 从投影还原）。</summary>
        [JSImport("setProjection", "render_canvas2d")]
        public static partial void SetProjection(float a, float b, float c, float d, float e, float f);

        [JSImport("createTexture", "render_canvas2d")]
        public static partial void CreateTexture(int id, int width, int height);

        [JSImport("deleteTexture", "render_canvas2d")]
        public static partial void DeleteTexture(int id);

        /// <summary>
        /// 建渲染目标（离屏 canvas）。同一 id 既是绘制目标、也能当纹理采样，
        /// 故它与 <see cref="CreateTexture"/> 共用同一套句柄空间（<c>RenderTarget2D</c> 的 Handle）。
        /// </summary>
        [JSImport("createRenderTarget", "render_canvas2d")]
        public static partial void CreateRenderTarget(int id, int width, int height);

        /// <summary>绑定渲染目标：把后续绘制切到该目标的离屏 canvas；<paramref name="id"/> &lt; 0 表示回主画布。</summary>
        [JSImport("bindRenderTarget", "render_canvas2d")]
        public static partial void BindRenderTarget(int id);

        /// <summary>释放渲染目标（同时摘掉它的纹理登记）。</summary>
        [JSImport("deleteRenderTarget", "render_canvas2d")]
        public static partial void DeleteRenderTarget(int id);

        [JSImport("uploadTexture", "render_canvas2d")]
        public static partial void UploadTexture(int id, int level, [JSMarshalAs<JSType.MemoryView>] Span<byte> bytes);

        [JSImport("uploadSubTexture", "render_canvas2d")]
        public static partial void UploadSubTexture(int id, int level, int x, int y, int width, int height,
                                                   [JSMarshalAs<JSType.MemoryView>] Span<byte> bytes);

        /// <summary>
        /// 回放一段顶点批次（start / end 为<b>顶点下标</b>，每 4 个顶点一个四边形）。
        /// 传整个顶点数组的字节即可，TS 侧按 start/end 索引（顶点步长 28 字节）。
        /// </summary>
        [JSImport("drawBatch", "render_canvas2d")]
        public static partial void DrawBatch([JSMarshalAs<JSType.MemoryView>] Span<byte> vertices, int start, int end);

        [JSImport("readPixel", "render_canvas2d")]
        public static partial void ReadPixel(int x, int y, [JSMarshalAs<JSType.MemoryView>] Span<byte> rgba);

        [JSImport("readPixels", "render_canvas2d")]
        public static partial void ReadPixels(int x, int y, int width, int height,
                                             [JSMarshalAs<JSType.MemoryView>] Span<byte> rgba);
    }
}
