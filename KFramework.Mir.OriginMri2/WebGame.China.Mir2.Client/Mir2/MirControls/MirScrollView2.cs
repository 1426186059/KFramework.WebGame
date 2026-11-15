using System;
using System.Collections.Generic;
using SlimDX;
using Client.MirGraphics;
using KFramework.MonoGameExtend;

namespace Client.MirControls
{
    /// <summary>
    /// 无限滚动列表（虚拟化 ScrollView + <b>行控件复用</b>）—— 继承 <see cref="MirScrollView"/> 的离屏烘焙、滚动条、滚轮、内容区拖拽。
    ///
    /// 与「滚出视口就 Dispose、滚入视口再 new」的旧实现不同，这里维护一个固定大小的<b>行控件池</b>：
    /// 池大小 = 视口可见行数 + 上下各 <see cref="BufferRows"/> 行缓冲，整个列表生命周期只创建这么多个控件；
    /// 滚动时滚出视口的行只是 <c>Visible=false</c>，槽位被重新分配给滚入的行并调用 <c>bindRow</c> 换数据。
    /// 因此滚动 1 行只做 1 次重绑，不创建/销毁任何控件，也不重复订阅事件（行里的输入框内容、按钮闭包都跟着绑定数据走）。
    ///
    /// 用法：
    /// <code>
    /// var list = new MirScrollView2 { Parent = xxx, Location = ..., Size = new Size(W, H) };
    /// list.SetDataSource(items.Count, rowHeight: 26, CreateRowView, BindRow);
    /// // CreateRowView() 只建不含数据的空壳；BindRow(row, i) 把第 i 行数据写进已有行控件
    /// </code>
    /// 本控件负责：① 按 itemCount*rowHeight 声明虚拟总尺寸；② 按 ScrollOffset.Y/rowHeight 算可见行区间并复用池；
    /// ③ 行的 Location.Y 设为 i*rowHeight（Location.X 由 createRowView 决定）。
    /// </summary>
    public class MirScrollView2 : MirScrollView
    {
        private const int BufferRows = 2;

        private int _itemCount;
        private int _rowHeight = 26;
        private Func<MirControl> _createRowView;      // 只创建空壳，池扩容时才调用
        private Action<MirControl, int> _bindRow;     // 把第 i 行数据写进已有行控件
        private readonly List<MirControl> _rows = new List<MirControl>();  // 行控件池：只增不减
        private readonly List<int> _boundIndex = new List<int>();          // 每个槽位当前绑定的行号，-1 = 空闲/隐藏
        private int _lastFirst = int.MinValue;   // 上次刷新时的首行索引，用于「视口未跨行则不重绑」节流
        private bool _refreshing;                // 防止 bindRow 引起的 Redraw → ScrollChanged 递归

        public MirScrollView2()
        {
            ScrollChanged += sv => RefreshVisible();
        }

        /// <summary>配置无限滚动数据源并立即刷新可见行。
        /// <param name="itemCount">总行数（可上万；只影响滚动条范围，不影响池大小）</param>
        /// <param name="rowHeight">每行像素高度（必须 &gt; 0）</param>
        /// <param name="createRowView">创建一个【不含数据】的行控件空壳（Size 已设、Location.X 已设；Location.Y 由本控件覆盖）。控件不得已有 Parent。</param>
        /// <param name="bindRow">把第 i 行的数据写入已有行控件；复用槽位时只调这一个回调。</param>
        /// </summary>
        public void SetDataSource(int itemCount, int rowHeight,
                                  Func<MirControl> createRowView, Action<MirControl, int> bindRow)
        {
            _itemCount = Math.Max(0, itemCount);
            _rowHeight = rowHeight > 0 ? rowHeight : 26;
            _createRowView = createRowView;
            _bindRow = bindRow;

            // 数据源换了（例如搜索过滤结果变化），旧绑定全部失效：强制可见行重绑，
            // 但行控件本身保留，不销毁、不重新订阅事件。
            for (int k = 0; k < _boundIndex.Count; k++) _boundIndex[k] = -1;
            _lastFirst = int.MinValue;

            // 虚拟总尺寸：高度=总行数*行高（滚动条据此算最大偏移）；
            // 宽度必须=视口宽，否则 Content.DisplayRectangle 宽度为 0 → IsMouseOver 永远 false，
            // 鼠标事件递归不进内容容器，行内按钮/输入框/点击全都点不到。
            Content.Size = new Size(Size.Width, _itemCount * _rowHeight);
            SetScrollOffset(Point.Zero);   // 回到顶部（会触发 ScrollChanged → RefreshVisible）
            RefreshVisible();
        }

        /// <summary>按当前滚动位置复用行控件池。通常由 ScrollChanged 自动触发；数据变化后可手动调用。</summary>
        public void RefreshVisible()
        {
            if (_refreshing) return;
            if (_createRowView == null || _bindRow == null || _rowHeight <= 0 || _itemCount <= 0)
            {
                HideAllRows();
                InvalidateTexture();
                return;
            }

            int first = Math.Max(0, ScrollOffset.Y / _rowHeight - BufferRows);
            int last = Math.Min(_itemCount, first + Size.Height / _rowHeight + BufferRows * 2);
            // 视口没跨行 → 一个槽位都不用重绑（拖拽逐像素滚动时零开销）
            if (first == _lastFirst && _rows.Count > 0) return;
            _lastFirst = first;

            _refreshing = true;
            try
            {
                int wanted = last - first;

                // 池只按需扩容：第一次滚动到某处才补齐槽位，之后永不 Dispose。
                // 走 Content.InsertControl 让基类 DragContent 只挂一次 MouseDown/MouseUp 转发。
                while (_rows.Count < wanted)
                {
                    MirControl row = _createRowView();
                    _rows.Add(row);
                    _boundIndex.Add(-1);
                    Content.InsertControl(Content.Controls.Count, row);
                }

                // 已绑定在目标区间内的槽位保持原样；其余槽位标记为空闲，等待重新分配。
                var keep = new Dictionary<int, int>();   // 行号 -> 池下标
                for (int k = 0; k < _rows.Count; k++)
                {
                    int idx = _boundIndex[k];
                    if (idx >= first && idx < last && !keep.ContainsKey(idx))
                        keep[idx] = k;
                    else
                        _boundIndex[k] = -1;
                }

                int free = 0;
                for (int i = first; i < last; i++)
                {
                    if (keep.ContainsKey(i)) continue;   // 这一行已经有槽位，控件和数据都不动

                    while (free < _rows.Count && _boundIndex[free] != -1) free++;
                    if (free >= _rows.Count) break;       // 池大小已 >= wanted，正常不会走到

                    MirControl row = _rows[free];
                    _boundIndex[free] = i;
                    _bindRow(row, i);
                    row.Location = new Point(row.Location.X, i * _rowHeight);
                    free++;
                }

                // 多余的槽位隐藏而不是销毁；隐藏控件不绘制、不参与鼠标命中。
                for (int k = 0; k < _rows.Count; k++)
                    _rows[k].Visible = _boundIndex[k] >= 0;

                InvalidateTexture();   // 离屏纹理失效，下次 Draw 重新烘焙这批行
            }
            finally
            {
                _refreshing = false;
            }
        }

        private void HideAllRows()
        {
            for (int k = 0; k < _rows.Count; k++)
            {
                _rows[k].Visible = false;
                _boundIndex[k] = -1;
            }
            _lastFirst = int.MinValue;
        }
    }
}
