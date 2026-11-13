using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using Client.MirControls;
using Client.MirGraphics;
using Client.MirNetwork;
using Client.MirSounds;
using KFramework.MonoGame;
using KFramework.MonoGameExtend;
using WebGame.Mir2.MonoGame.Client;
using C = ClientPackets;
using Point = MirEngine.Point;
using Size = MirEngine.Size;
using Color = MirEngine.Color;

namespace Client.MirScenes.Dialogs
{
    // 全部用引擎默认图(KDefaultRes.DefaultTexture2D 白纹理) + ForeColour 着色 + Border 描边绘制,
    // 不依赖将来废弃的 Libraries.* 图集贴图。
    public sealed class GMDialog : MirImageControl
    {
        public static GMDialog Instance;

        // 引擎默认白纹理(拉伸着色)
        private static readonly Texture2D DefTex = KDefaultRes.DefaultTexture2D;

        // 配色
        private static readonly Color WindowFill = new Color(22, 26, 42);
        private static readonly Color WindowBorder = new Color(110, 150, 210);
        private static readonly Color PanelFill = new Color(28, 33, 52);
        private static readonly Color PanelBorder = new Color(70, 100, 150);
        private static readonly Color ButtonFill = new Color(48, 68, 110);
        private static readonly Color ButtonBorder = new Color(120, 150, 200);
        private static readonly Color TabInactive = new Color(40, 46, 70);
        private static readonly Color TabActive = new Color(86, 116, 176);
        private static readonly Color ItemRowFill = new Color(30, 36, 56);
        private static readonly Color ItemRowHover = new Color(70, 98, 152);

        // 窗口
        private MirButton CloseButton;
        private MirButton GMCommandTab, ItemTab;

        // GM 命令面板（用 ScrollView 承载长列表，离屏烘焙天然裁掉溢出，自带竖直滚动条）
        private MirScrollView GMCommandScrollView;
        private readonly List<MirControl> GMRows = new List<MirControl>();

        // 物品面板
        private MirImageControl ItemPanel;

        private MirTextBox ItemSearchBox;        // 物品搜索框（按 Id 或 名字 过滤）
        private string _itemSearch = string.Empty;
        private MirScrollView2 ItemScrollView;   // 物品行虚拟化滚动列表（无限滚动，只创建视口内行）
        private List<ItemTableDTO> _filteredCache = new List<ItemTableDTO>();  // 当前搜索过滤结果（createRow 按索引取）
        private int _lastItemsCount = -1;        // 上次见到的全量物品条数，用于后台加载完成后自动刷新

        // GM 命令清单(命令名|参数提示)。发送时自动加 @ 前缀,参数以空格分隔。
        private static readonly string[] GMCommands = new[]
        {
            "LOGIN|登录GM(输入密码)",
            "KILL|击杀 [角色]",
            "CHANGEGENDER|切换性别 [角色]",
            "LEVEL|设置等级 [角色] [等级]",
            "LEVELHERO|设置英雄等级 [角色] [等级]",
            "MAKE|制造物品 <物品名/ID> [数量]",
            "CLEARBUFFS|清除Buff [角色]",
            "CLEARBAG|清空背包 [角色]",
            "SUPERMAN|超级战士",
            "GAMEMASTER|切换GM",
            "OBSERVER|观察者",
            "ALLOWGUILD|允许行会",
            "RECALL|召唤 [角色]",
            "OBSERVE|观察",
            "ENABLEGROUPRECALL|允许组队召唤",
            "GROUPRECALL|组队召唤",
            "RECALLMEMBER|召唤成员",
            "RECALLLOVER|召唤伴侣",
            "TIME|时间",
            "ROLL|ROLL",
            "MAP|地图信息 [地图名]",
            "BACKUPPLAYER|备份角色 [角色]",
            "ARCHIVEPLAYER|归档角色 [角色]",
            "LOADPLAYER|加载角色 [角色]",
            "RESTOREPLAYER|恢复角色 [角色]",
            "MOVE|移动 [地图] [X] [Y]",
            "MAPMOVE|地图移动 [地图] [X] [Y]",
            "GOTO|前往 [地图] [X] [Y]",
            "MOB|刷怪 <怪物名> [数量] [等级]",
            "RECALLMOB|召回怪物",
            "RELOADDROPS|重载掉落",
            "RELOADNPCS|重载NPC",
            "CLEARIPBLOCKS|清除IP封锁",
            "GIVEGOLD|给金币 [角色] [数量]",
            "GIVEPEARLS|给元宝 [角色] [数量]",
            "GIVECREDIT|给积分 [角色] [数量]",
            "GIVESKILL|给技能 [角色] [技能] [等级]",
            "FIND|查找 [角色]",
            "LEAVEGUILD|离开行会 [角色]",
            "CREATEGUILD|创建行会 [行会名]",
            "ALLOWTRADE|允许交易",
            "TRIGGER|触发",
            "RIDE|骑乘",
            "SETFLAG|设置旗标 [数值]",
            "LISTFLAGS|列出旗标",
            "CLEARFLAGS|清除旗标",
            "CLEARMOB|清怪 [地图]",
            "CHANGECLASS|切换职业 [角色] [职业]",
            "DIE|自杀",
            "HAIR|设置发型 [数值]",
            "DECO|设置装饰",
            "ADJUSTPKPOINT|调整PK点 [角色] [数值]",
            "AWAKENING|觉醒 [装备槽]",
            "REMOVEAWAKENING|移除觉醒",
            "STARTWAR|开启攻城 [攻城ID]",
            "ADDINVENTORY|扩展背包",
            "ADDSTORAGE|扩展仓库",
            "SUMMONHERO|召唤英雄",
            "ALLOWOBSERVE|允许观察",
            "INFO|信息 [角色]",
            "CLEARQUESTS|清除任务 [角色]",
            "SETQUEST|设置任务 [任务ID] [状态] [角色]",
            "TOGGLETRANSFORM|切换变身",
            "STARTCONQUEST|开始攻城 [攻城ID]",
            "RESETCONQUEST|重置攻城 [攻城ID]",
            "GATES|城门 [OPEN/CLOSE]",
            "CHANGEFLAG|更改旗标 [数值]",
            "CHANGEFLAGCOLOUR|旗标颜色 [R] [G] [B]",
            "REVIVE|复活 [角色]",
            "DELETESKILL|删除技能 [角色] [技能]",
            "SETTIMER|设置计时器 <键> <秒> <类型>",
        };

        private const int RowH = 26;
        private const int ContentX = 14;
        private const int ContentY = 50;

        public GMDialog()
        {
            Instance = this;

            Movable = true;
            Sort = true;
            AutoSize = false;
            Size = new Size(720, 540);
            Location = new Point(120, 60);
            Texture = DefTex;
            ForeColour = WindowFill;
            Border = true;
            BorderColour = WindowBorder;

            CloseButton = new MirButton
            {
                Parent = this, AutoSize = false, Size = new Size(24, 24),
                Location = new Point(Size.Width - 28, 4), Sound = SoundList.ButtonA,
                Texture = DefTex, ForeColour = ButtonFill, Border = true, BorderColour = ButtonBorder,
            };
            CloseButton.Label.Text = "X";
            CloseButton.Label.ForeColour = Color.White;
            CloseButton.Click += (o, e) => Hide();

            GMCommandTab = new MirButton
            {
                Parent = this, AutoSize = false, Size = new Size(120, 26),
                Location = new Point(ContentX, 14), Sound = SoundList.ButtonA,
                Texture = DefTex, ForeColour = TabActive, Border = true, BorderColour = ButtonBorder,
            };
            GMCommandTab.Label.Text = "GM 命令";
            GMCommandTab.Label.ForeColour = Color.White;
            GMCommandTab.Click += (o, e) => ShowGMCommandTab();

            ItemTab = new MirButton
            {
                Parent = this, AutoSize = false, Size = new Size(120, 26),
                Location = new Point(ContentX + 125, 14), Sound = SoundList.ButtonA,
                Texture = DefTex, ForeColour = TabInactive, Border = true, BorderColour = ButtonBorder,
            };
            ItemTab.Label.Text = "物品表";
            ItemTab.Label.ForeColour = Color.White;
            ItemTab.Click += (o, e) => ShowItemTab();

            // —— GM 命令面板（ScrollView 承载长列表）——
            GMCommandScrollView = new MirScrollView
            {
                Parent = this, Visible = true,
                Location = new Point(ContentX, ContentY),
                Size = new Size(Size.Width - ContentX - 28, Size.Height - ContentY - 12),
                BackColour = PanelFill, Border = true, BorderColour = PanelBorder,
            };

            BuildGMRows();

            // —— 物品面板 ——
            ItemPanel = new MirImageControl
            {
                Parent = this, AutoSize = false, Visible = false,
                Location = new Point(ContentX, ContentY),
                Size = new Size(Size.Width - ContentX - 28, Size.Height - ContentY - 12),
                Texture = DefTex, ForeColour = PanelFill, Border = true, BorderColour = PanelBorder,
            };

            // 物品搜索框：按 Id（纯数字）或 名字（包含，忽略大小写）过滤，左对齐
            var searchLabel = new MirLabel
            {
                Parent = ItemPanel, Location = new Point(3, 3), Size = new Size(40, 18),
                Text = "搜索", ForeColour = Color.White, AutoSize = false,
            };
            ItemSearchBox = new MirTextBox
            {
                Parent = ItemPanel, Border = true, BorderColour = Color.Lime, MaxLength = 40,
                Location = new Point(45, 2), Size = new Size(180, 18),
            };
            ItemSearchBox.TextBox.TextChanged += (s, e) =>
            {
                _itemSearch = ItemSearchBox.Text;
                ApplyFilterAndRefresh();
            };

            // 物品行滚动区：虚拟化无限滚动（MirScrollView2），只创建视口内可见行，12594 项也不卡。
            ItemScrollView = new MirScrollView2
            {
                Parent = ItemPanel,
                Location = new Point(3, 24),
                Size = new Size(ItemPanel.Size.Width - 6, ItemPanel.Size.Height - 24 - 6),
                BackColour = PanelFill, Border = true, BorderColour = PanelBorder,
            };

            ShowGMCommandTab();
        }

        private void ShowGMCommandTab()
        {
            GMCommandScrollView.Visible = true;
            ItemPanel.Visible = false;
            GMCommandTab.ForeColour = TabActive;
            ItemTab.ForeColour = TabInactive;
        }

        private void ShowItemTab()
        {
            GMCommandScrollView.Visible = false;
            ItemPanel.Visible = true;
            GMCommandTab.ForeColour = TabInactive;
            ItemTab.ForeColour = TabActive;
            ApplyFilterAndRefresh();
        }

        #region GM 命令行
        private void BuildGMRows()
        {
            // 行控件按"内容本地坐标"布局（ScrollView 负责滚动 + 裁切），无需手动移动/裁剪
            for (int i = 0; i < GMCommands.Length; i++)
            {
                string[] parts = GMCommands[i].Split(new[] { '|' }, 2);
                string cmd = parts[0];
                string hint = parts.Length > 1 ? parts[1] : string.Empty;

                var label = new MirLabel
                {
                    Location = new Point(0, i * RowH + 1), Size = new Size(340, 24),
                    Text = cmd + (string.IsNullOrEmpty(hint) ? "" : "  -  " + hint),
                    ForeColour = Color.Yellow, AutoSize = false,
                };
                var box = new MirTextBox
                {
                    Border = true, BorderColour = Color.Lime,
                    Location = new Point(350, i * RowH + 2), Size = new Size(210, 20), MaxLength = 200,
                };
                var btn = new MirButton
                {
                    AutoSize = false, Size = new Size(64, 22), Location = new Point(566, i * RowH + 1),
                    Sound = SoundList.ButtonA, Texture = DefTex, ForeColour = ButtonFill,
                    Border = true, BorderColour = ButtonBorder,
                };
                btn.Label.Text = "执行";
                btn.Label.ForeColour = Color.White;
                btn.Click += (o, e) => SendGMCommand(cmd, box.Text);

                box.TextBox.KeyDown += (s, ev) =>
                {
                    if (ev.KeyCode == Keys.Enter)
                    {
                        SendGMCommand(cmd, box.Text);
                        ev.Handled = true;
                    }
                };

                GMCommandScrollView.Add(label);
                GMCommandScrollView.Add(box);
                GMCommandScrollView.Add(btn);
                GMRows.Add(label);
                GMRows.Add(box);
                GMRows.Add(btn);
            }
        }

        private static void SendGMCommand(string cmd, string rawArgs)
        {
            string args = (rawArgs ?? string.Empty).Trim();
            // 参数以空格分隔(双空格也可,服务端按空白拆分),每个参数间用一个空格连接
            string msg = "@" + cmd + (args.Length > 0 ? " " + args : "");
            Network.Enqueue(new C.GMCommand { Message = msg });
        }
        #endregion

        #region 物品行
        // 物品数据由 ExcelTableMgr.InitializeAsync 在启动时预加载（MyRes/ExcelConfig/ItemInfo.json，12594 项），
        // GM 面板直接取 ExcelTableMgr.Items，不再自己异步拉表。若打开时还在加载，Update 监测到条数变化后自动刷新。

        // 按搜索词过滤：纯数字→按 Id(Index) 精确匹配；否则按名字包含(忽略大小写)
        private List<ItemTableDTO> GetFilteredItems()
        {
            var all = ExcelTableMgr.Items;
            string q = _itemSearch.Trim();
            if (q.Length == 0) return all;
            if (int.TryParse(q, out int id) && id > 0)
                return all.Where(x => x.Index == id).ToList();
            return all.Where(x => !string.IsNullOrEmpty(x.Name) &&
                                  x.Name.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
        }

        /// <summary>按当前搜索词刷新过滤结果并喂给虚拟化列表。</summary>
        private void ApplyFilterAndRefresh()
        {
            _filteredCache = GetFilteredItems();
            ItemScrollView.SetDataSource(_filteredCache.Count, RowH, CreateItemRow);
        }

        /// <summary>MirScrollView2 的行工厂：为第 i 行创建一个完整行控件（背景/origin图标/名字/数量/执行）。</summary>
        private MirControl CreateItemRow(int i)
        {
            var info = _filteredCache[i];
            int rowW = ItemScrollView.Size.Width - 6 - MirScrollView.ScrollBarWidth;

            var bg = new MirImageControl
            {
                AutoSize = false, Size = new Size(rowW, RowH), Location = new Point(3, 0),
                Texture = DefTex, ForeColour = ItemRowFill,
            };

            // 物品图标：走原始传奇 CDN origin 图标（与背包 MirItemCell 同链路），不依赖 Libraries.Items。
            int img = (int)info.Image;
            var icon = new MirImageControl
            {
                Parent = bg, DrawImage = false, AutoSize = false, Size = new Size(20, 20),
                Location = new Point(4, (RowH - 20) / 2), NotControl = true,
            };
            icon.BeforeDraw += (o, e) =>
            {
                Mir2OriginObjectLibrary.Request(Mir2ObjectKind.Item, img,
                                                priority: Mir2OriginObjectLibrary.PriorityItem);
                Mir2OriginObjectLibrary.DrawItemIconFit(img, icon.DisplayLocation.X, icon.DisplayLocation.Y, 20, 20);
            };

            // 名字
            var label = new MirLabel
            {
                Parent = bg, NotControl = true,
                Location = new Point(28, 0), Size = new Size(Math.Max(40, rowW - 264), RowH),
                Text = info.Name, ForeColour = Color.White, AutoSize = false,
            };

            // 品质（按 Grade 着色，GM 一眼区分档次）
            new MirLabel
            {
                Parent = bg, NotControl = true,
                Location = new Point(rowW - 232, 0), Size = new Size(56, RowH),
                Text = "品质 " + info.Grade, ForeColour = GradeColour(info.Grade), AutoSize = false,
            };

            // 需求等级（RequiredAmount = 装备需求等级）
            new MirLabel
            {
                Parent = bg, NotControl = true,
                Location = new Point(rowW - 168, 0), Size = new Size(56, RowH),
                Text = "需求 " + info.RequiredAmount, ForeColour = Color.LightGray, AutoSize = false,
            };

            // 数量输入框（默认 1）
            var qtyBox = new MirTextBox
            {
                Parent = bg, Border = true, BorderColour = Color.Lime, MaxLength = 9,
                Location = new Point(rowW - 104, (RowH - 20) / 2), Size = new Size(44, 20),
                Text = "1",
            };
            // 烘焙内的文本框键入后不会自动重画，手动让滚动视图重烘焙以显示输入的数字
            qtyBox.TextBox.TextChanged += (s, e) => ItemScrollView.InvalidateTexture();

            // 执行按钮
            var btn = new MirButton
            {
                Parent = bg, AutoSize = false, Size = new Size(52, 20),
                Location = new Point(rowW - 56, (RowH - 20) / 2),
                Sound = SoundList.ButtonA, Texture = DefTex, ForeColour = ButtonFill,
                Border = true, BorderColour = ButtonBorder,
            };
            btn.Label.Text = "执行";
            btn.Label.ForeColour = Color.White;

            // 发送 @MAKE 物品名 [数量]（数量为 1 时省略）
            string name = info.Name;
            void Execute()
            {
                string qtyText = qtyBox.Text.Trim();
                string msg = "@MAKE " + name;
                if (qtyText.Length > 0 && int.TryParse(qtyText, out int qty) && qty > 1)
                    msg += " " + qty;
                Network.Enqueue(new C.GMCommand { Message = msg });
            }

            bg.Click += (o, e) => Execute();   // 点行内空白/图标/名字执行
            btn.Click += (o, e) => Execute();   // 点执行按钮（Click 不冒泡，不会重复执行）

            bg.BeforeDraw += (o, e) =>
            {
                bg.ForeColour = MirControl.MouseControl == bg ? ItemRowHover : ItemRowFill;
            };

            return bg;
        }

        /// <summary>品质 Grade → 颜色（0普通灰 … ≥5神器橙），供物品行着色。</summary>
        private static Color GradeColour(int grade)
        {
            switch (grade)
            {
                case 0: return Color.Gray;
                case 1: return Color.White;
                case 2: return Color.LimeGreen;
                case 3: return Color.RoyalBlue;
                case 4: return Color.MediumPurple;
                default: return Color.Orange;
            }
        }
        #endregion

        public override void Show()
        {
            if (Visible) return;
            if (Parent == null) Parent = GameScene.Scene;
            base.Show();
            BringToFront();
            // 每次显示时确保物品表已开始加载；若当前在物品页则刷新虚拟列表
            // 每次显示时刷新一次物品列表（ExcelTableMgr 已在启动时预加载；若还在加载，Update 会监测到条数变化后自动刷新）
            if (ItemPanel.Visible) ApplyFilterAndRefresh();
        }

        public new void Hide()
        {
            base.Hide();
        }

        public void Toggle()
        {
            if (Visible) Hide();
            else Show();
        }

        public override void Update()
        {
            base.Update();
            // 物品表在 ExcelTableMgr 后台预加载，加载完成（条数从 0 变 >0）时若在物品页则自动刷新一次。
            if (Visible && ItemPanel.Visible)
            {
                int cur = ExcelTableMgr.Items.Count;
                if (cur != _lastItemsCount)
                {
                    _lastItemsCount = cur;
                    ApplyFilterAndRefresh();
                }
            }
        }
    }
}
