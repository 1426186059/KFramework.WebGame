using System;
using SlimDX;
using Client.MirGraphics;
using KFramework.MonoGameExtend;

namespace Client.MirControls
{
    /// <summary>
    /// 无限滚动列表（虚拟化 ScrollView）—— 继承 <see cref="MirScrollView"/> 的离屏烘焙 + 滚动条 + 滚轮，
    /// 但<b>不把所有行控件都 Add 进内容容器</b>：无论数据源有几千几万行，内容容器里始终只有
    /// 「当前视口内 + 上下各 2 行缓冲」这几十个行控件，滚出视口的行立即 Dispose，滚入视口的行按需创建。
    /// 因此内存占用与每帧烘焙开销恒定，与 <see cref="SetDataSource"/> 的 itemCount 无关。
    ///
    /// 用法：
    /// <code>
    /// var list = new MirScrollView2 { Parent = xxx, Location = ..., Size = new Size(W, H) };
    /// list.SetDataSource(items.Count, rowHeight: 26, i =&gt; CreateRow(items[i]));   // CreateRow 返回一个 Location/Size 已就绪的行控件
    /// </code>
    /// 行控件由调用方在 createRow(i) 里完整创建（背景/图标/文本/按钮等），本控件只负责：
    /// ① 按 itemCount*rowHeight 声明虚拟总尺寸（滚动条据此计算最大偏移）；
    /// ② 滚动时按 ScrollOffset.Y/rowHeight 算出可见行区间 [first, last)，只把该区间行 Add 进内容容器；
    /// ③ 行的 Location.Y 由本控件设为 i*rowHeight（createRow 里只需设 Size，Location.X 由 createRow 决定）。
    /// 滚动条 / 滚轮 / 拖拽 / 离屏裁切全部沿用 <see cref="MirScrollView"/>，无需调用方关心。
    /// </summary>
    public class MirScrollView2 : MirScrollView
    {
        private int _itemCount;
        private int _rowHeight = 26;
        private Func<int, MirControl> _createRow;
        private int _lastFirst = int.MinValue;   // 上次重建时的首行索引，用于「视口未跨行则不重建」节流
        private bool _refreshing;                // 防止 ScrollChanged → RefreshVisible → SetScrollOffset → ScrollChanged 递归

        public MirScrollView2()
        {
            ScrollChanged += sv => RefreshVisible();
        }

        /// <summary>配置无限滚动数据源并立即刷新可见行。
        /// <param name="itemCount">总行数（可上万；只影响滚动条范围，不影响内存）</param>
        /// <param name="rowHeight">每行像素高度（必须 &gt; 0）</param>
        /// <param name="createRow">为第 i 行创建一个全新行控件（Size 已设、Location.X 已设；Location.Y 由本控件覆盖）。控件不得已有 Parent。</param>
        /// </summary>
        public void SetDataSource(int itemCount, int rowHeight, Func<int, MirControl> createRow)
        {
            _itemCount = Math.Max(0, itemCount);
            _rowHeight = rowHeight > 0 ? rowHeight : 26;
            _createRow = createRow;
            _lastFirst = int.MinValue;   // 强制重建

            // 虚拟总尺寸：高度=总行数*行高（滚动条据此算最大偏移）；
            // 宽度必须=视口宽，否则 _content.DisplayRectangle 宽度为 0 → IsMouseOver 永远 false，
            // 鼠标事件递归不进 _content 的子控件，行内按钮/输入框/点击全都点不到。
            Content.Size = new Size(Size.Width, _itemCount * _rowHeight);
            SetScrollOffset(Point.Empty);   // 回到顶部（会触发 ScrollChanged → RefreshVisible）
            RefreshVisible();
        }

        /// <summary>按当前滚动位置重建可见行。通常由 ScrollChanged 自动触发；数据变化后可手动调用。</summary>
        public void RefreshVisible()
        {
            if (_refreshing) return;   // 防递归
            if (_createRow == null || _rowHeight <= 0 || _itemCount <= 0)
            {
                ClearContent();
                InvalidateTexture();
                return;
            }

            int first = Math.Max(0, ScrollOffset.Y / _rowHeight - 2);
            int last = Math.Min(_itemCount, first + Size.Height / _rowHeight + 4);
            // 视口没跨行且已有行 → 不重建（滚动 1 像素不该触发整批 Dispose/重建）
            if (first == _lastFirst && Content.Controls.Count > 0) return;
            _lastFirst = first;

            _refreshing = true;
            try
            {
                ClearContent();
                for (int i = first; i < last; i++)
                {
                    MirControl row = _createRow(i);
                    row.Location = new Point(row.Location.X, i * _rowHeight);
                    Content.InsertControl(Content.Controls.Count, row);
                }
                InvalidateTexture();   // 离屏纹理失效，下次 Draw 重新烘焙这批可见行
            }
            finally
            {
                _refreshing = false;
            }
        }

        /// <summary>清空内容容器里上一批可见行（Dispose 并移除）。不会触碰滚动条 _track/_thumb（它们挂在 ScrollView 下，不在 Content 里）。</summary>
        private void ClearContent()
        {
            for (int i = Content.Controls.Count - 1; i >= 0; i--)
            {
                MirControl c = Content.Controls[i];
                Content.Controls.RemoveAt(i);
                try { c?.Dispose(); } catch { }
            }
        }
    }
}
