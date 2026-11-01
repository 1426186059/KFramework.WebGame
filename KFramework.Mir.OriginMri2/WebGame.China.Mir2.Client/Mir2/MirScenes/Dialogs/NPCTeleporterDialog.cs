using Client.MirControls;
using Client.MirSounds;

/*
 * 原始传奇传送员（NPC id 600）标准分组结构 —— 本对话框分类(Category)的权威参考：
 *   主城传送 / 比奇省 / 盟重省 / 白日门 / 封魔谷 / 苍月岛 / 魔龙城 / 危险地图 / 幻境传送 / 会员之家 / 玛雅神殿
 * 服务端土城传送员 Envir/NPCs/盟重/土城/Transport.txt 即按此 11 分组组织菜单，
 * 目的地采用服务器真实地图 FileName（= 数字索引，如 比奇省=103、盟重=154、白日门=5384…）。
 *
 * 布局按线上当前结构重做：左侧竖排一级菜单（分类），右侧二级入口（目的地网格）。
 *   面板 510×312；左侧竖排分类（com_z_btn_1 / _1_1 选中）；右侧 2 列目的地网格
 *   （com_z_btn_2 / _2_1），可上下翻页；标题下方分隔线；右上关闭。
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

        private const string Sheet = "y_common-sheet";
        // 背景：manifest 中存在的 gameui4/jpg/y_cs_bg.jpg（线上原图为 gameui3/jpg/delieve_bgi.png，本工程无该资源）。
        private const string BgUrl = "resource/assets/gameui4/jpg/y_cs_bg.jpg";

        // —— 布局：左竖排一级菜单 + 右二级入口（按线上当前结构）——
        private const int PanelW = 400;
        private const int PanelH = 573;

        // 左侧竖排一级菜单（纵向略宽松，超出可视区可上下翻页）
        private const int CatMenuX = 15, CatMenuY = 42, CatMenuW = 110, CatMenuH = 262;
        private const int CatBtnH = 35, CatGap = 4;
        private const int CatStep = CatBtnH + CatGap;   // 单项步进 30

        // 右侧二级入口（目的地 2 列网格，左右紧凑）
        private const int DestX = 140, DestY = 42, DestW = 358, DestH = 262;
        private const int CellW = 106, CellH = 40, ColGap = 5, RowGap = 10;
        private const int Cols = 2, Rows = 4;          // 右侧可见 2×4 = 8 格
        private const int MaxVisible = Cols * Rows;

        private const int DestArrowX = DestX + CellW * Cols + ColGap + 2;   // 右网格右侧外，紧贴网格

        private MirLabel _titleLabel;
        private MirImageControl _sepLine;
        private MirButton _closeButton;
        private MirControl _catMenu;            // 左侧竖排一级菜单容器
        private MirButton _upButton, _downButton;
        private MirButton _catUp, _catDown;     // 左菜单上下翻页
        private int _catOffsetY;                // 左菜单纵向滚动偏移
        private int _catTotalH;                 // 左菜单内容总高

        private readonly List<MirButton> _categoryButtons = new List<MirButton>();
        private readonly List<string> _categoryNames = new List<string>();   // 分类标签（Text 只写，需单独存）

        private List<DeliverDestination> _allItems = new List<DeliverDestination>();
        private List<DeliverDestination> _visibleItems = new List<DeliverDestination>();
        private readonly List<MirButton> _destButtons = new List<MirButton>();
        private int _destStart;                 // 右网格起始下标（每次滚动 Cols）

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
                Location = new Point(16, 2),
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
                Size = new Size(PanelW - 20, 2),
            };

            // 关闭按钮（右上角；线上 btn_close 用 z_com_btn_close7，本工程用 com_btn_close0）
            _closeButton = new MirButton
            {
                AtlasNameKey = Sheet + "#com_btn_close0",
                Parent = this,
                Location = new Point(PanelW - 30, 2),
                Size = new Size(26, 26),
                Sound = SoundList.ButtonA,
            };
            _closeButton.Click += (o, e) => Hide();

            // 左侧竖排一级菜单容器（本引擎无 Clip，分类按钮在 BuildCategories 里纵向堆叠）
            _catMenu = new MirControl
            {
                Parent = this,
                Location = new Point(CatMenuX, CatMenuY),
                Size = new Size(CatMenuW, CatMenuH),
            };

            // 右侧二级入口网格（2 列；上下翻页）
            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Cols; c++)
                {
                    int index = r * Cols + c;
                    var btn = new MirButton
                    {
                        AtlasNameKey = Sheet,
                        AtlasFrame = "com_z_btn_1",
                        PressedAtlasFrame = "com_z_btn_1_1",
                        Parent = this,
                        Location = new Point(DestX + c * (CellW + ColGap), DestY + r * (CellH + RowGap)),
                        Size = new Size(CellW, CellH),
                        CenterText = true,
                        Sound = SoundList.ButtonA,
                        Visible = false,
                    };
                    int captured = index;
                    btn.Click += (o, e) =>
                    {
                        int dataIdx = _destStart + captured;
                        if (dataIdx < _visibleItems.Count)
                            SelectDestination(_visibleItems[dataIdx]);
                    };
                    _destButtons.Add(btn);
                }
            }

            _upButton = new MirButton
            {
                Parent = this, Location = new Point(DestArrowX, DestY), Size = new Size(18, 18),
                CenterText = true, Text = "▲", Sound = SoundList.ButtonA, Visible = false,
            };
            _downButton = new MirButton
            {
                Parent = this, Location = new Point(DestArrowX, DestY + DestH - 18), Size = new Size(18, 18),
                CenterText = true, Text = "▼", Sound = SoundList.ButtonA, Visible = false,
            };
            _upButton.Click += (o, e) => { if (_destStart >= Cols) { _destStart -= Cols; RefreshDestList(); } };
            _downButton.Click += (o, e) => { if (_destStart + MaxVisible < _visibleItems.Count) { _destStart += Cols; RefreshDestList(); } };

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

        private void BuildCategories()
        {
            foreach (var b in _categoryButtons)
                b.Dispose();
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

            // 左侧竖排一级菜单：纵向堆叠，超出可视区（本引擎无 Clip）直接隐藏
            int y = 0;
            for (int i = 0; i < cats.Count; i++)
            {
                var cat = cats[i];
                var btn = new MirButton
                {
                    AtlasNameKey = Sheet,
                    AtlasFrame = "com_z_btn_2_1",
                    PressedAtlasFrame = "com_z_btn_2",
                    Parent = _catMenu,
                    Location = new Point(0, y),
                    Size = new Size(92, 35),
                    CenterText = true,
                    Text = string.IsNullOrEmpty(cat) ? "全部" : cat,
                    Sound = SoundList.ButtonA,
                };
                int idx = i;
                btn.Click += (o, e) => SelectCategory(idx);
                _categoryButtons.Add(btn);
                _categoryNames.Add(string.IsNullOrEmpty(cat) ? "全部" : cat);
                y += CatStep;
            }
            _catTotalH = y - CatGap;          // 末项多算一个 gap，扣掉
            _catOffsetY = 0;
            ApplyCatScroll();

            SelectCategory(0);
        }

        private void ApplyCatScroll()
        {
            for (int i = 0; i < _categoryButtons.Count; i++)
            {
                int by = i * CatStep - _catOffsetY;
                _categoryButtons[i].Location = new Point(0, by);
                _categoryButtons[i].Visible = by + CatBtnH > 0 && by < CatMenuH;
            }
        }

        private void SelectCategory(int index)
        {
            for (int i = 0; i < _categoryButtons.Count; i++)
            {
                _categoryButtons[i].AtlasNameKey = Sheet;
                _categoryButtons[i].AtlasFrame = "com_z_btn_2_1";
                _categoryButtons[i].PressedAtlasFrame = "com_z_btn_2";
            }

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
            _destStart = 0;
            RefreshDestList();
        }

        private void RefreshDestList()
        {
            for (int k = 0; k < _destButtons.Count; k++)
            {
                var btn = _destButtons[k];
                int dataIdx = _destStart + k;
                if (dataIdx < _visibleItems.Count)
                {
                    var it = _visibleItems[dataIdx];
                    string costLine = it.Cost > 0 ? $"消耗 {it.Cost}"
                                   : (string.IsNullOrEmpty(it.CostText) ? "" : it.CostText);
                    btn.Text = string.IsNullOrEmpty(costLine) ? it.Name : it.Name + "\n" + costLine;
                    btn.Visible = true;
                }
                else
                {
                    btn.Visible = false;
                }
            }

            bool canScroll = _visibleItems.Count > MaxVisible;
            _upButton.Visible = canScroll && _destStart > 0;
            _downButton.Visible = canScroll && _destStart + MaxVisible < _visibleItems.Count;
        }

        private void SelectDestination(DeliverDestination item)
        {
            OnTeleport?.Invoke(item);
        }
    }
}
