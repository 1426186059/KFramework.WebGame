using System;

namespace KFramework.MonoGame
{
    /// <summary>
    /// 一块 HTML 画布（<c>&lt;canvas&gt;</c>）—— 与 TS 层 <c>html_canvas.ts</c> 的 <c>canvas</c> 模块一一对应的对象封装。
    /// 一个实例 = 页面上的一块画布（按 DOM id 定位），自带当前矩形 <see cref="Rect"/>，调用方不必每次重复传 id。底层直接转发到 <see cref="JSBind_HTML_Canvas"/>。
    /// <para>
    /// 所有写操作只改 CSS：后备缓冲 = CSS 尺寸 × DPR，由 <see cref="GraphicsDevice.SyncCanvasSize"/> 同步，
    /// 输入坐标也按画布 rect 自动换算。
    /// </para>
    /// </summary>
    /// <remarks>
    /// 与 TS 导出的对应关系：create / applyLayout / restoreLayout / destroy / exists / getRect / getHTMLPageSize，
    /// 全部经 <see cref="JSBind_HTML_Canvas"/> 的 [JSImport] 直连，本类不再经 <see cref="HTML_Canvas_Func"/> 中转
    /// （<see cref="HTML_Canvas_Func"/> 现在只保留「建」与「销」两块画布的生命周期入口）。
    /// </remarks>
    public sealed class HTML_Canvas
    {
        private Rectangle? _rect;

        /// <summary>
        /// 用 DOM id（或 "#id" 选择器）关联一块画布。构造本身不创建 DOM 元素，
        /// 需要新建时调用 <see cref="Create(HTML_CanvasLayoutMode, int, int, int, int)"/>。
        /// </summary>
        /// <param name="idOrSelector">画布 DOM id，可写 "#id" 形式；空串回落到默认 id。</param>
        public HTML_Canvas(string idOrSelector)
        {
            Id = HTML_Canvas_Func.ToCanvasId(idOrSelector);
        }

        /// <summary>本实例对应的 DOM id（已归一化，无 "#" 前缀）。</summary>
        public string Id { get; }

        /// <summary>页面上这块画布是否已存在（含引擎曾创建过的）。</summary>
        public bool Exists => JSBind_HTML_Canvas.Exists(Id);

        /// <summary>
        /// 画布当前的实际矩形（相对窗口左上角，单位 CSS 像素）—— 对应 HTML 里这块元素的 rect。
        /// </summary>
        /// <remarks>
        /// 值是从浏览器读回来的缓存：每次布局操作成功后会自动刷新；
        /// 若画布被页面自己的 CSS / 脚本改动过，调 <see cref="Refresh"/> 重新拉取。
        /// 画布不存在时为 <see cref="Rectangle.Empty"/>。
        /// </remarks>
        public Rectangle Rect => _rect ??= ReadRect() ?? Rectangle.Empty;

        /// <summary>重新从浏览器读回 <see cref="Rect"/>（画布被外部改动后调用）。</summary>
        public void Refresh() => _rect = ReadRect();

        /// <summary>创建这块画布：按 <paramref name="mode"/> 布局（Rect 摆位 / Centered 居中 / Fullscreen 填满整个 HTML 页面 / Size 仅设尺寸）。已存在时返回 false。</summary>
        public bool Create(HTML_CanvasLayoutMode mode, int x, int y, int width, int height)
            => Refresh(JSBind_HTML_Canvas.Create(Id, (int)mode, x, y, width, height));

        /// <summary>
        /// 【通用布局入口】一次调用表达四种摆位需求，对应 TS 的 applyLayout。
        /// 引擎内部与上层业务都可以通过它组合出任意布局，无需为每种情况新增一个 JS 导出。
        /// </summary>
        /// <param name="mode">布局方式。</param>
        /// <param name="x">左上角 X（<see cref="HTML_CanvasLayoutMode.Centered"/> / <see cref="HTML_CanvasLayoutMode.Size"/> 时忽略）。</param>
        /// <param name="y">左上角 Y（同上）。</param>
        /// <param name="width">CSS 宽度（<see cref="HTML_CanvasLayoutMode.Fullscreen"/> 时忽略）。</param>
        /// <param name="height">CSS 高度（同上）。</param>
        /// <returns>画布存在且设置成功时返回 true。</returns>
        public bool SetLayout(HTML_CanvasLayoutMode mode, int x, int y, int width, int height)
            => Refresh(JSBind_HTML_Canvas.ApplyLayout(Id, (int)mode, x, y, width, height));

        /// <summary>设置画布位置与 CSS 尺寸（像素，相对窗口左上角）。</summary>
        public bool SetRect(int x, int y, int width, int height)
            => SetLayout(HTML_CanvasLayoutMode.Rect, x, y, width, height);

        /// <summary>只改 CSS 尺寸，位置保持不动。</summary>
        public bool SetSize(int width, int height)
            => SetLayout(HTML_CanvasLayoutMode.Size, 0, 0, width, height);

        /// <summary>摆到浏览器窗口正中；之后窗口缩放会自动重新居中。</summary>
        public bool SetCentered(int width, int height)
            => SetLayout(HTML_CanvasLayoutMode.Centered, 0, 0, width, height);

        /// <summary>撤销引擎写在这块画布上的行内样式，恢复页面自身布局，并解除居中跟随。</summary>
        public bool RestoreLayout() => Refresh(JSBind_HTML_Canvas.RestoreLayout(Id));

        /// <summary>从 DOM 移除这块画布。</summary>
        public bool Destroy()
        {
            bool ok = JSBind_HTML_Canvas.Destroy(Id);
            if (ok) _rect = null;
            return ok;
        }

        /// <summary>写操作成功后顺带回读一次真实矩形，保证 <see cref="Rect"/> 与浏览器一致。</summary>
        private bool Refresh(bool ok)
        {
            if (ok) Refresh();
            return ok;
        }

        /// <summary>从浏览器读回画布矩形；画布不存在或尺寸非法时返回 <c>null</c>。</summary>
        private Rectangle? ReadRect()
        {
            Span<int> view = stackalloc int[4];
            JSBind_HTML_Canvas.GetRect(Id, view);

            Rectangle rect = new(view[0], view[1], view[2], view[3]);
            return rect.Width <= 0 || rect.Height <= 0 ? null : rect;
        }

        /// <inheritdoc />
        public override string ToString() => $"HTML_Canvas(id={Id}, rect={Rect})";
    }
}
