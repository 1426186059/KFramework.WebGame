using System;
using System.Collections.Generic;
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

        // GM 命令面板（用 ScrollView 承载长列表，离屏烘焙天然裁掉溢出）
        private MirScrollView GMCommandScrollView;
        private MirButton GMUp, GMDown;
        private readonly List<MirControl> GMRows = new List<MirControl>();

        // 物品面板
        private MirImageControl ItemPanel;
        private MirButton ItemUp, ItemDown, FetchAllButton;
        private MirTextBox ItemCountBox;
        private MirScrollView ItemScrollView;   // 物品行滚动区（放在 ItemPanel 内、表头下方）
        private readonly List<MirControl> ItemRows = new List<MirControl>();
        private int _itemListVersion;

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

            GMUp = new MirButton
            {
                Parent = this, AutoSize = false, Size = new Size(18, 18),
                Location = new Point(Size.Width - 23, ContentY), Sound = SoundList.ButtonA,
                Texture = DefTex, ForeColour = ButtonFill, Border = true, BorderColour = ButtonBorder,
            };
            GMUp.Label.Text = "▲"; GMUp.Label.ForeColour = Color.White;
            GMUp.Click += (o, e) => GMCommandScrollView.ScrollBy(new Point(0, -RowH * 3));
            GMDown = new MirButton
            {
                Parent = this, AutoSize = false, Size = new Size(18, 18),
                Location = new Point(Size.Width - 23, Size.Height - 28), Sound = SoundList.ButtonA,
                Texture = DefTex, ForeColour = ButtonFill, Border = true, BorderColour = ButtonBorder,
            };
            GMDown.Label.Text = "▼"; GMDown.Label.ForeColour = Color.White;
            GMDown.Click += (o, e) => GMCommandScrollView.ScrollBy(new Point(0, RowH * 3));

            BuildGMRows();

            // —— 物品面板 ——
            ItemPanel = new MirImageControl
            {
                Parent = this, AutoSize = false, Visible = false,
                Location = new Point(ContentX, ContentY),
                Size = new Size(Size.Width - ContentX - 28, Size.Height - ContentY - 12),
                Texture = DefTex, ForeColour = PanelFill, Border = true, BorderColour = PanelBorder,
            };

            FetchAllButton = new MirButton
            {
                Parent = ItemPanel, AutoSize = false, Size = new Size(120, 22), Location = new Point(0, 0),
                Sound = SoundList.ButtonA, Texture = DefTex, ForeColour = ButtonFill,
                Border = true, BorderColour = ButtonBorder,
            };
            FetchAllButton.Label.Text = "拉取全部物品";
            FetchAllButton.Label.ForeColour = Color.White;
            FetchAllButton.Click += (o, e) => FetchAllItems();

            var countLabel = new MirLabel
            {
                Parent = ItemPanel, Location = new Point(130, 3), Size = new Size(40, 18),
                Text = "数量", ForeColour = Color.White, AutoSize = false,
            };
            ItemCountBox = new MirTextBox
            {
                Parent = ItemPanel, Border = true, BorderColour = Color.Lime,
                Location = new Point(172, 2), Size = new Size(60, 18), MaxLength = 9,
            };

            // 物品行滚动区：放在表头下方，自带填充与边框
            ItemScrollView = new MirScrollView
            {
                Parent = ItemPanel,
                Location = new Point(3, 24),
                Size = new Size(ItemPanel.Size.Width - 6, ItemPanel.Size.Height - 24 - 6),
                BackColour = PanelFill, Border = true, BorderColour = PanelBorder,
            };
            // 列表很长，只保留视口附近行可见，避免每次滚动都烘焙上千行
            ItemScrollView.ScrollChanged += OnItemScrollChanged;

            ItemUp = new MirButton
            {
                Parent = this, AutoSize = false, Size = new Size(18, 18),
                Location = new Point(Size.Width - 23, ContentY), Sound = SoundList.ButtonA,
                Texture = DefTex, ForeColour = ButtonFill, Border = true, BorderColour = ButtonBorder,
            };
            ItemUp.Label.Text = "▲"; ItemUp.Label.ForeColour = Color.White;
            ItemUp.Click += (o, e) => ItemScrollView.ScrollBy(new Point(0, -RowH * 3));
            ItemDown = new MirButton
            {
                Parent = this, AutoSize = false, Size = new Size(18, 18),
                Location = new Point(Size.Width - 23, Size.Height - 28), Sound = SoundList.ButtonA,
                Texture = DefTex, ForeColour = ButtonFill, Border = true, BorderColour = ButtonBorder,
            };
            ItemDown.Label.Text = "▼"; ItemDown.Label.ForeColour = Color.White;
            ItemDown.Click += (o, e) => ItemScrollView.ScrollBy(new Point(0, RowH * 3));

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
            RefreshItemRows();
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
        private void FetchAllItems()
        {
            // 复用现有物品信息请求机制:逐个请求索引,服务端对有效索引回 S.NewItemInfo。
            const int maxIndex = 6000;
            for (int i = 1; i <= maxIndex; i++)
                GameScene.RequestItemInfo(i);
            GameScene.Scene.OutputMessage("正在拉取全部物品,请稍候…");
        }

        private void RefreshItemRows()
        {
            // 列表有变化时重建物品行
            if (_itemListVersion == GameScene.ItemInfoList.Count) return;
            _itemListVersion = GameScene.ItemInfoList.Count;

            foreach (var c in ItemRows)
                if (!c.IsDisposed) c.Dispose();
            ItemRows.Clear();

            var list = GameScene.ItemInfoList;
            int rowW = ItemScrollView.Size.Width - 6;
            for (int i = 0; i < list.Count; i++)
            {
                var info = list[i];
                if (info == null || string.IsNullOrEmpty(info.Name)) continue;

                string name = info.Name;
                var bg = new MirImageControl
                {
                    AutoSize = false, Size = new Size(rowW, RowH), Location = new Point(3, i * RowH),
                    Texture = DefTex, ForeColour = ItemRowFill,
                };
                var label = new MirLabel
                {
                    Parent = bg, NotControl = true, Location = new Point(6, 0), Size = new Size(rowW - 12, RowH),
                    Text = name, ForeColour = Color.White, AutoSize = false,
                };
                bg.Click += (o, e) =>
                {
                    string countText = ItemCountBox.Text.Trim();
                    string msg = "@MAKE " + name;
                    if (countText.Length > 0 && int.TryParse(countText, out int cnt) && cnt > 1)
                        msg += " " + cnt;
                    Network.Enqueue(new C.GMCommand { Message = msg });
                };
                bg.BeforeDraw += (o, e) =>
                {
                    bg.ForeColour = MirControl.MouseControl == bg ? ItemRowHover : ItemRowFill;
                };

                ItemScrollView.Add(bg);   // 加入滚动内容（内容本地坐标 i*RowH）
                ItemRows.Add(bg);
            }
            ItemScrollView.SetScrollOffset(Point.Empty);   // 夹紧并触发可见窗口更新
            GameScene.Scene.OutputMessage("物品数量:" + ItemRows.Count);
        }

        // 只在视口附近保留行可见，缩小 ScrollView 每次烘焙的开销（列表可能上千行）。
        private void OnItemScrollChanged(MirScrollView sv)
        {
            int view = sv.Size.Height;
            foreach (var bg in ItemRows)
            {
                int y = bg.Location.Y;   // 内容本地坐标
                bg.Visible = y >= sv.ScrollOffset.Y - RowH * 2 && y <= sv.ScrollOffset.Y + view + RowH * 2;
            }
        }
        #endregion

        public override void Show()
        {
            if (Visible) return;
            if (Parent == null) Parent = GameScene.Scene;
            base.Show();
            BringToFront();
            // 每次显示时刷新一次物品(可能已拉取)
            if (ItemPanel.Visible) RefreshItemRows();
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
            // 物品在后台陆续到达时,自动刷新物品列表
            if (Visible && ItemPanel.Visible && _itemListVersion != GameScene.ItemInfoList.Count)
                RefreshItemRows();
        }
    }
}
