using System;
using System.Collections.Generic;
using System.Drawing;
using Client.MirControls;
using Client.MirGraphics;
using Client.MirNetwork;
using Client.MirSounds;
using WebGame.Mir2.MonoGame.Client;
using C = ClientPackets;
using Point = MirEngine.Point;
using Size = MirEngine.Size;
using Color = MirEngine.Color;

namespace Client.MirScenes.Dialogs
{
    public sealed class GMDialog : MirImageControl
    {
        public static GMDialog Instance;

        // 窗口
        private MirButton CloseButton;
        private MirButton GMCommandTab, ItemTab;

        // GM 命令面板
        private MirImageControl GMCommandPanel;
        private MirButton GMUp, GMDown;
        private int GMScrollOffset;
        private readonly List<MirControl> GMRows = new List<MirControl>();

        // 物品面板
        private MirImageControl ItemPanel;
        private MirButton ItemUp, ItemDown, FetchAllButton;
        private MirTextBox ItemCountBox;
        private int ItemScrollOffset;
        private readonly List<MirControl> ItemRows = new List<MirControl>();
        private int _itemListVersion;

        // GM 命令清单(命令名|参数提示)。发送时自动加 @ 前缀。
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

        private const int RowH = 20;
        private const int ContentX = 12;
        private const int ContentY = 46;

        public GMDialog()
        {
            Instance = this;

            Index = 920;
            Library = Libraries.Prguse;
            Movable = true;
            Sort = true;
            Size = new Size(600, 480);
            Location = new Point(160, 80);

            CloseButton = new MirButton
            {
                HoverIndex = 361,
                Index = 360,
                PressedIndex = 362,
                Library = Libraries.Prguse2,
                Parent = this,
                Location = new Point(Size.Width - 25, 3),
                Sound = SoundList.ButtonA,
            };
            CloseButton.Click += (o, e) => Hide();

            GMCommandTab = new MirButton
            {
                Index = 500,
                Library = Libraries.Title,
                Parent = this,
                Size = new Size(90, 20),
                Location = new Point(ContentX, 14),
                Sound = SoundList.ButtonA,
            };
            GMCommandTab.Label.Text = "GM 命令";
            GMCommandTab.Click += (o, e) => ShowGMCommandTab();

            ItemTab = new MirButton
            {
                Index = 502,
                Library = Libraries.Title,
                Parent = this,
                Size = new Size(90, 20),
                Location = new Point(ContentX + 95, 14),
                Sound = SoundList.ButtonA,
            };
            ItemTab.Label.Text = "物品表";
            ItemTab.Click += (o, e) => ShowItemTab();

            // —— GM 命令面板 ——
            GMCommandPanel = new MirImageControl
            {
                Parent = this,
                Location = new Point(ContentX, ContentY),
                Size = new Size(Size.Width - ContentX - 26, Size.Height - ContentY - 12),
                Visible = true,
                DrawImage = false,
            };

            GMUp = new MirButton
            {
                Index = 240, HoverIndex = 241, PressedIndex = 242,
                Library = Libraries.Prguse2, Parent = this, Size = new Size(16, 16),
                Location = new Point(Size.Width - 22, ContentY), Sound = SoundList.ButtonA,
            };
            GMUp.Click += (o, e) => { GMScrollOffset -= RowH * 3; ClampGMScroll(); UpdateGMRows(); };
            GMDown = new MirButton
            {
                Index = 243, HoverIndex = 244, PressedIndex = 245,
                Library = Libraries.Prguse2, Parent = this, Size = new Size(16, 16),
                Location = new Point(Size.Width - 22, Size.Height - 28), Sound = SoundList.ButtonA,
            };
            GMDown.Click += (o, e) => { GMScrollOffset += RowH * 3; ClampGMScroll(); UpdateGMRows(); };

            BuildGMRows();

            // —— 物品面板 ——
            ItemPanel = new MirImageControl
            {
                Parent = this,
                Location = new Point(ContentX, ContentY),
                Size = new Size(Size.Width - ContentX - 26, Size.Height - ContentY - 12),
                Visible = false,
                DrawImage = false,
            };

            FetchAllButton = new MirButton
            {
                Index = 500, Library = Libraries.Title, Parent = ItemPanel,
                Size = new Size(110, 20), Location = new Point(0, 0), Sound = SoundList.ButtonA,
            };
            FetchAllButton.Label.Text = "拉取全部物品";
            FetchAllButton.Click += (o, e) => FetchAllItems();

            var countLabel = new MirLabel
            {
                Parent = ItemPanel, Location = new Point(120, 2), Size = new Size(40, 18),
                Text = "数量", ForeColour = Color.White, AutoSize = false,
            };
            ItemCountBox = new MirTextBox
            {
                Parent = ItemPanel, Border = true, BorderColour = Color.Lime,
                Location = new Point(160, 1), Size = new Size(60, 18), MaxLength = 9,
            };

            ItemUp = new MirButton
            {
                Index = 240, HoverIndex = 241, PressedIndex = 242,
                Library = Libraries.Prguse2, Parent = this, Size = new Size(16, 16),
                Location = new Point(Size.Width - 22, ContentY), Sound = SoundList.ButtonA,
            };
            ItemUp.Click += (o, e) => { ItemScrollOffset -= RowH * 3; ClampItemScroll(); UpdateItemRows(); };
            ItemDown = new MirButton
            {
                Index = 243, HoverIndex = 244, PressedIndex = 245,
                Library = Libraries.Prguse2, Parent = this, Size = new Size(16, 16),
                Location = new Point(Size.Width - 22, Size.Height - 28), Sound = SoundList.ButtonA,
            };
            ItemDown.Click += (o, e) => { ItemScrollOffset += RowH * 3; ClampItemScroll(); UpdateItemRows(); };

            ShowGMCommandTab();
        }

        private void ShowGMCommandTab()
        {
            GMCommandPanel.Visible = true;
            ItemPanel.Visible = false;
            GMCommandTab.Index = 501;
            ItemTab.Index = 502;
            UpdateGMRows();
        }

        private void ShowItemTab()
        {
            GMCommandPanel.Visible = false;
            ItemPanel.Visible = true;
            GMCommandTab.Index = 500;
            ItemTab.Index = 503;
            RefreshItemRows();
        }

        #region GM 命令行
        private void BuildGMRows()
        {
            for (int i = 0; i < GMCommands.Length; i++)
            {
                string[] parts = GMCommands[i].Split(new[] { '|' }, 2);
                string cmd = parts[0];
                string hint = parts.Length > 1 ? parts[1] : string.Empty;

                var label = new MirLabel
                {
                    Parent = GMCommandPanel, Location = new Point(0, 0), Size = new Size(300, 18),
                    Text = cmd + (string.IsNullOrEmpty(hint) ? "" : "  -  " + hint),
                    ForeColour = Color.Yellow, AutoSize = false, Visible = false,
                };
                var box = new MirTextBox
                {
                    Parent = GMCommandPanel, Border = true, BorderColour = Color.Lime,
                    Location = new Point(305, 0), Size = new Size(150, 18), MaxLength = 200, Visible = false,
                };
                var btn = new MirButton
                {
                    Index = 500, Library = Libraries.Title, Parent = GMCommandPanel,
                    Size = new Size(46, 18), Location = new Point(460, 0), Sound = SoundList.ButtonA, Visible = false,
                };
                btn.Label.Text = "执行";
                btn.Click += (o, e) =>
                {
                    SendGMCommand(cmd, box.Text);
                };

                box.TextBox.KeyDown += (s, ev) =>
                {
                    if (ev.KeyCode == Keys.Enter)
                    {
                        SendGMCommand(cmd, box.Text);
                        ev.Handled = true;
                    }
                };

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

        private void ClampGMScroll()
        {
            int max = Math.Max(0, GMCommands.Length * RowH - GMCommandPanel.Size.Height);
            if (GMScrollOffset < 0) GMScrollOffset = 0;
            if (GMScrollOffset > max) GMScrollOffset = max;
        }

        private void UpdateGMRows()
        {
            ClampGMScroll();
            for (int i = 0; i < GMCommands.Length; i++)
            {
                int y = i * RowH - GMScrollOffset;
                bool vis = y >= -RowH && y <= GMCommandPanel.Size.Height;
                for (int k = 0; k < 3; k++)
                {
                    var c = GMRows[i * 3 + k];
                    c.Location = new Point(c.Location.X, y + 1);
                    c.Visible = vis;
                }
            }
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
            {
                if (!c.IsDisposed) c.Dispose();
            }
            ItemRows.Clear();

            var list = GameScene.ItemInfoList;
            for (int i = 0; i < list.Count; i++)
            {
                var info = list[i];
                if (info == null || string.IsNullOrEmpty(info.Name)) continue;

                string name = info.Name;
                var label = new MirLabel
                {
                    Parent = ItemPanel, Location = new Point(0, 0), Size = new Size(ItemPanel.Size.Width - 6, 18),
                    Text = name, ForeColour = Color.White, AutoSize = false, Visible = false,
                };
                int idx = i;
                label.Click += (o, e) =>
                {
                    string countText = ItemCountBox.Text.Trim();
                    string msg = "@MAKE " + name;
                    if (countText.Length > 0 && int.TryParse(countText, out int cnt) && cnt > 1)
                        msg += " " + cnt;
                    Network.Enqueue(new C.GMCommand { Message = msg });
                };
                label.BeforeDraw += (o, e) =>
                {
                    label.BackColour = MirControl.MouseControl == label ? new Color(60, 60, 120) : Color.Transparent;
                };
                ItemRows.Add(label);
            }
            ClampItemScroll();
            UpdateItemRows();
            GameScene.Scene.OutputMessage("物品数量:" + ItemRows.Count);
        }

        private void ClampItemScroll()
        {
            int max = Math.Max(0, ItemRows.Count * RowH - (ItemPanel.Size.Height - 24));
            if (ItemScrollOffset < 0) ItemScrollOffset = 0;
            if (ItemScrollOffset > max) ItemScrollOffset = max;
        }

        private void UpdateItemRows()
        {
            ClampItemScroll();
            int top = 24; // 为"拉取全部/数量"行留空间
            for (int i = 0; i < ItemRows.Count; i++)
            {
                int y = top + i * RowH - ItemScrollOffset;
                bool vis = y >= top - RowH && y <= ItemPanel.Size.Height;
                var c = ItemRows[i];
                c.Location = new Point(c.Location.X, y);
                c.Visible = vis;
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
            else UpdateGMRows();
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
