using Client.MirControls;
using Client.MirSounds;

/*
 * 原始传奇传送员（NPC id 600）标准分组结构 —— 本对话框分类(Category)的权威参考：
 *   主城传送 / 比奇省 / 盟重省 / 白日门 / 封魔谷 / 苍月岛 / 魔龙城 / 危险地图 / 幻境传送 / 会员之家 / 玛雅神殿
 * 服务端土城传送员 Envir/NPCs/盟重/土城/Transport.txt 即按此 11 分组组织菜单，
 * 目的地采用服务器真实地图 FileName（= 数字索引，如 比奇省=103、盟重=154、白日门=5384…）。
 *
 * 布局：左侧竖排一级菜单（分类），右侧二级入口（目的地 2 列网格）。
 *   面板 400×573（= 背景图 y_cs_bg.jpg 原生尺寸）；左/右均为 MirScrollView（隐藏滚动条）。
 *   左分类按钮用 com_z_btn_2(_2) 选中态；右目的地按钮用 com_z_btn_1(_1_1)；标题下方分隔线；右上关闭。
 * 注：线上背景为 gameui3/jpg/delieve_bgi.png、按钮帧为 com_z_btn_6 / z_boss_img0 / z_com_line1 /
 *     z_com_btn_close7 —— 这些 gameui3 资源不在本工程 manifest 中，故改用同图集现成帧替代：
 *     分类按钮 com_z_btn_1(_1) / 目的地按钮 com_z_btn_2(_2) / 分隔线 com_line_h / 关闭 com_btn_close0；
 *     背景用 manifest 中存在的 gameui4/jpg/y_cs_bg.jpg。
 */

namespace Client.MirScenes.Dialogs
{
    /// <summary>
    /// 传送员目的地数据（由服务端下发；当前服务端尚无 deliver 系统，先定义客户端模型）。
    /// </summary>
    public sealed class DeliverDestination
    {
        public int Id;            // deliverId（服务端用以定位目的地图/坐标）
        public string Name;       // 显示名，如 "比奇城"
        public string Category;   // 左侧分类，如 "比奇省"（空串归入"全部"）
        public int Cost;          // 消耗（元宝/金币），0 表示免费
        public string CostText;   // 自定义消耗文本（优先于 Cost 展示）
        public string Description;// 悬浮/副标题说明（可选）
        public string Action;     // 服务端 @mv_xxx 页键（不含 @），点击即发 C.CallNPC 到该页执行 MOVE
    }

    /// <summary>
    /// 传送员专属对话框（严格对齐线上游戏 DeliverDialog 布局）。
    /// 图集：gameui4 公共 UI 图集 y_common-sheet。
    /// </summary>
    public sealed class NPCTeleporterDialog : MirImageControl
    {
        public event Action<DeliverDestination> OnTeleport;
        public uint CurrentNPCID;   // 当前交互的传送员 NPC ObjectID（OnTeleport 时发包用）

        /// <summary>自定义传送对话框模式下：发送 @Main 仅用于“开起”服务端对话（设置 NPCObjectID/NPCScriptID），
        /// 其回包（原菜单文本）需忽略——置此标记后 NPCResponse 会跳过显示原菜单、且不隐藏本框。</summary>
        public static bool SuppressNpcDialog;

        private const string Sheet = "y_common-sheet";
        // 背景：manifest 中存在的 gameui4/jpg/y_cs_bg.jpg（线上原图为 gameui3/jpg/delieve_bgi.png，本工程无该资源）。
        private const string BgUrl = "resource/assets/gameui4/jpg/y_cs_bg.jpg";

        // —— 布局：左竖排一级菜单 + 右二级入口（左/右均为 MirScrollView，隐藏滚动条）——
        private const int PanelW = 400;   // = 背景图 y_cs_bg.jpg 原生尺寸 400×573
        private const int PanelH = 573;

        // 左侧竖排一级菜单（MirScrollView 容器尺寸；按钮尺寸对齐 com_z_btn_2 帧 92×35）
        private const int CatMenuX = 10, CatMenuY = 45, CatMenuW = 110, CatMenuH = 262;
        private const int CatBtnW = 92, CatBtnH = 35, CatGap = 4;
        private const int CatStep = CatBtnH + CatGap;   // 单项纵向步进

        // 右侧二级入口（MirScrollView 内的 2 列网格；按钮尺寸对齐 com_z_btn_1 帧 106×40）
        private const int DestX = 130, DestY = 45, DestW = 245, DestH = 262;
        private const int CellW = 106, CellH = 40, ColGap = 10, RowGap = 10;

        private MirLabel _titleLabel;
        private MirImageControl _sepLine;
        private MirButton _closeButton;
        private MirScrollView _catScroll;       // 左侧竖排一级菜单（滚动视图，隐藏滚动条）
        private MirScrollView _destScroll;      // 右侧二级入口（滚动视图，隐藏滚动条）

        private readonly List<MirButton> _categoryButtons = new List<MirButton>();
        private readonly List<string> _categoryNames = new List<string>();   // 分类标签（Text 只写，需单独存）

        private List<DeliverDestination> _allItems = new List<DeliverDestination>();
        private List<DeliverDestination> _visibleItems = new List<DeliverDestination>();
        private readonly List<MirButton> _destButtons = new List<MirButton>();

        public NPCTeleporterDialog()
        {
            _ = SetManifestImageAsync(BgUrl);   // 背景走 manifest 解析（CDN 哈希布局），避免 404
            Size = new Size(PanelW, PanelH);
            Movable = true;
            Sort = true;

            // 标题（线上原图把标题烤进背景，本工程背景无标题，故补一个轻量标题）
            _titleLabel = new MirLabel
            {
                Text = "传送员",
                Parent = this,
                Location = new Point(16, 15),
                AutoSize = true,
                ForeColour = Color.BurlyWood,
                Font = new Font(Settings.FontName, 10F, KFramework.MonoGame.FontStyle.Bold),
            };

            // 分隔线（标题下方一条横线；线上用 z_com_line1，本工程用 com_line_h）
            _sepLine = new MirImageControl
            {
                AtlasNameKey = Sheet + "#com_line_h",
                Parent = this,
                Location = new Point(10, 36),
                AutoSize = false,
                Size = new Size(PanelW - 20, 2),
            };

            // 分隔线（标题下方一条竖线；线上用 z_com_line1，本工程用 com_line_h）
            var Line2 = new MirImageControl
            {
                AtlasNameKey = Sheet + "#com_line_v",
                Parent = this,
                Location = new Point(120, 36),
                AutoSize = false,
                Size = new Size(2, PanelH - 38),
            };

            // 关闭按钮（右上角；线上 btn_close 用 z_com_btn_close7，本工程用 com_btn_close0）
            _closeButton = new MirButton
            {
                AtlasNameKey = Sheet + "#com_btn_close0",
                Parent = this,
                // 必须落在 this.Size 内：命中测试先判断父控件 DisplayRectangle，按钮越界则永不进入子控件递归 → 点击无响应。
                // 想做成"原始传奇那样压在右上边框"只需把 this.Size 宽度放大到能包住按钮即可（不必越界）。
                Location = new Point(Size.Width - 26, 2),
                Size = new Size(26, 26),
                Sound = SoundList.ButtonA,
            };
            _closeButton.Click += (o, e) => Hide();

            // 左侧竖排一级菜单（MirScrollView，隐藏滚动条；分类按钮在 BuildCategories 里纵向堆叠）
            _catScroll = new MirScrollView
            {
                Parent = this,
                Location = new Point(CatMenuX, CatMenuY),
                Size = new Size(CatMenuW, 500),
            };
            _catScroll.ShowScrollBar = false;

            // 右侧二级入口（MirScrollView 内的 2 列网格，隐藏滚动条；按钮随分类重建）
            _destScroll = new MirScrollView
            {
                Parent = this,
                Location = new Point(DestX, DestY),
                Size = new Size(DestW, 500),
            };
            _destScroll.ShowScrollBar = false;

            _closeButton.BringToFront();   // 置顶：避免同区域控件遮挡导致“关闭无响应”
        }

        /// <summary>填充并显示：title=窗口标题，items=该 NPC 的全部传送目的地。</summary>
        public void Open(string title, List<DeliverDestination> items, uint npcId = 0)
        {
            CurrentNPCID = npcId;
            _titleLabel.Text = string.IsNullOrEmpty(title) ? "传送员" : title;
            _allItems = items ?? new List<DeliverDestination>();

            BuildCategories();

            Location = new Point((Settings.ScreenWidth - Size.Width) / 2,
                                 (Settings.ScreenHeight - Size.Height) / 2);
            Visible = true;
            BringToFront();
        }

        /// <summary>仿 Unity ScrollRect：鼠标在面板任意位置（不限于滚动条/列表）都滚动对应侧列表——左栏区域滚分类、其余滚目的地。</summary>
        public override void OnMouseWheel(MouseEventArgs e)
        {
            bool overCat = e.Location.X < CatMenuX + CatMenuW;
            var target = overCat ? _catScroll : _destScroll;
            int notches = Math.Abs(e.Delta) >= 120 ? e.Delta / 120 : e.Delta;
            if (notches != 0)
                target.ScrollBy(new Point(0, notches * 30));
            e.Handled = true;
            base.OnMouseWheel(e);
        }

        private void BuildCategories()
        {
            foreach (var b in _categoryButtons)
            {
                b.Dispose();
            }

            _categoryButtons.Clear();
            _categoryNames.Clear();

            var cats = new List<string>();
            bool anyCat = false;
            foreach (var it in _allItems)
            {
                var c = it.Category ?? "";
                if (!string.IsNullOrEmpty(c)) anyCat = true;
                if (!cats.Contains(c)) cats.Add(c);
            }
            if (!anyCat)
            {
                cats.Clear();
                cats.Add("");
            }

            // 左侧竖排一级菜单：纵向堆叠进 _catScroll 内容容器（MirScrollView 自带裁剪 + 滚动）
            int bx = (CatMenuW - CatBtnW) / 2;   // 与 com_z_btn_2 帧宽 92 对齐并居中
            int y = 0;
            for (int i = 0; i < cats.Count; i++)
            {
                var cat = cats[i];
                var btn = new MirButton
                {
                    AtlasNameKey = Sheet,
                    AtlasFrame = "com_z_btn_2_1",
                    Parent = _catScroll.Content,
                    Location = new Point(bx, y),
                    Size = new Size(CatBtnW, CatBtnH),
                    CenterText = true,
                    Text = string.IsNullOrEmpty(cat) ? "全部" : cat,
                    Sound = SoundList.ButtonA,
                    OnlyDrawWhenActive = false,
                };
                int idx = i;
                btn.Click += (o, e) => SelectCategory(idx);
                _categoryButtons.Add(btn);
               
                _categoryNames.Add(string.IsNullOrEmpty(cat) ? "全部" : cat);
                y += CatStep;
            }
            _catScroll.UpdateContentSize();

            SelectCategory(0);
        }

        private void SelectCategory(int index)
        {
            for (int i = 0; i < _categoryButtons.Count; i++)
            {
                bool sel = (i == index);
                _categoryButtons[i].AtlasFrame = sel ? "com_z_btn_2" : "com_z_btn_2_1";
            }
            _catScroll.InvalidateTexture();   // 类别高亮切换后让左侧滚动视图重烘，否则选中态不刷新

            string cat = (index >= 0 && index < _categoryNames.Count) ? _categoryNames[index] : "";
            _visibleItems = new List<DeliverDestination>();
            foreach (var it in _allItems)
            {
                string c = it.Category ?? "";
                if (index == 0 && _categoryNames[0] == "全部")
                    _visibleItems.Add(it);   // 第一项“全部”
                else if (c == cat)
                    _visibleItems.Add(it);
            }
            BuildDestButtons();
        }

        private void BuildDestButtons()
        {
            foreach (var b in _destButtons)
                b.Dispose();
            _destButtons.Clear();

            const int cols = 2;
            int startX = (DestW - (CellW * cols + ColGap * (cols - 1))) / 2;   // 2 列整体居中
            for (int i = 0; i < _visibleItems.Count; i++)
            {
                var it = _visibleItems[i];
                int r = i / cols, c = i % cols;
                int x = startX + c * (CellW + ColGap);
                int y = r * (CellH + RowGap);
                string costLine = it.Cost > 0 ? $"消耗 {it.Cost}"
                               : (string.IsNullOrEmpty(it.CostText) ? "" : it.CostText);
                var btn = new MirButton
                {
                    AtlasNameKey = Sheet,
                    AtlasFrame = "com_z_btn_1",
                    PressedAtlasFrame = "com_z_btn_1_1",
                    Parent = _destScroll.Content,
                    Location = new Point(x, y),
                    Size = new Size(CellW, CellH),
                    CenterText = true,
                    Text = string.IsNullOrEmpty(costLine) ? it.Name : it.Name + "\n" + costLine,
                    Sound = SoundList.ButtonA,
                };
                int captured = i;
                btn.Click += (o, e) => SelectDestination(_visibleItems[captured]);
                _destButtons.Add(btn);
            }
            _destScroll.UpdateContentSize();
            _destScroll.SetScrollOffset(Point.Empty);   // 切换分类后回到顶部
        }

        private void SelectDestination(DeliverDestination item)
        {
            OnTeleport?.Invoke(item);
        }
    }
}
