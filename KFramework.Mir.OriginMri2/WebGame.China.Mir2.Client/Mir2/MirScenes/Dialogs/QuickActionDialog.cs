using System;
using Client.MirControls;
using Client.MirGraphics;
using Client.MirSounds;
using WebGame.Mir2.MonoGame.Client;

namespace Client.MirScenes.Dialogs
{
    /// <summary>
    /// 原始传奇 NoticeUseItemViewSkin / NoticeWearEquipViewSkin 移植：
    /// 获得技能书或战力更高的装备时，屏幕侧边浮出的 170×186 快捷操作小窗。
    /// 布局/贴图全部对齐 default.thm 的 exml：y_kscd_bg.jpg 背景 + 64×64 物品格 + 标题/名字/倒计时 + com_z_btn_1「使 用」按钮。
    /// 点击「使 用」复用背包格子的 UseItem() 完成穿戴/学技能，不新增服务端协议。
    /// </summary>
    public sealed class QuickActionDialog : MirImageControl
    {
        // NoticeUseItemViewSkin / NoticeWearEquipViewSkin：$bs 170×186
        private const int PanelW = 170, PanelH = 186;
        private const int HoldSeconds = 8;

        // 背景与图集帧（manifest key / y_common-sheet）
        private const string BgKey = "resource/assets/gameui4/jpg/y_kscd_bg.jpg";
        private const string BtnAtlas = "y_common-sheet";
        private const string BtnUp = "com_z_btn_1";
        private const string BtnDown = "com_z_btn_1_1";
        private const string CloseUp = "com_btn_close0";
        private const string CloseDown = "com_btn_close1";

        private readonly StoreIcon _icon;
        private readonly MirLabel _titleLabel, _nameLabel, _powerLabel, _countLabel;
        private readonly MirButton _sureButton, _closeButton;

        private UserItem _item;
        private Action _onAction;
        private long _deadline;

        public QuickActionDialog()
        {
            AutoSize = false;
            Size = new Size(PanelW, PanelH);
            ImageKey = BgKey;
            BackColour = CommonColor.FromArgb(0, 0, 0, 0);
            Border = false;
            NotControl = false;
            Movable = false;

            // 标题：原版 _Label1_i y=13 size=18 horizontalCenter=-13 → 中心 x=72
            _titleLabel = new MirLabel
            {
                Parent = this,
                Location = new Point(22, 10),
                Size = new Size(100, 26),
                AutoSize = false,
                DrawFormat = new TextStyle(TextAnchor.MiddleCenter),
                ForeColour = CommonColor.FromArgb(255, 255, 210, 120),
                Text = "快捷使用",
                NotControl = true,
            };

            // 物品格：原版 RealBagEquipRender 64×64 y=40 horizontalCenter=-13 → x=40
            _icon = new StoreIcon
            {
                Parent = this,
                Location = new Point(40, 40),
                Size = new Size(64, 64),
                NotControl = true,
            };

            // 名字：原版 txt_name y=100 height=30，左对齐
            _nameLabel = new MirLabel
            {
                Parent = this,
                Location = new Point(8, 96),
                Size = new Size(154, 24),
                AutoSize = false,
                DrawFormat = new TextStyle(TextAnchor.MiddleCenter),
                ForeColour = CommonColor.White,
                NotControl = true,
            };

            // 战力提升（仅快捷装备显示）：原版 txt_power + img_up，绿色上箭头文案
            _powerLabel = new MirLabel
            {
                Parent = this,
                Location = new Point(8, 117),
                Size = new Size(154, 18),
                AutoSize = false,
                DrawFormat = new TextStyle(TextAnchor.MiddleCenter),
                ForeColour = CommonColor.FromArgb(255, 120, 235, 120),
                NotControl = true,
            };

            // 「使 用」：原版 btn_sure 80×35 horizontalCenter=-13 / verticalCenter=60 → x=32,y=136
            _sureButton = new MirButton
            {
                Parent = this,
                AutoSize = false,
                Size = new Size(80, 35),
                Location = new Point(30, 135),
                AtlasNameKey = BtnAtlas + "#" + BtnUp,
                HoverAtlasFrame = BtnDown,
                PressedAtlasFrame = BtnDown,
                CenterText = true,
                Text = "使 用",
                Sound = SoundList.ButtonA,
                BackColour = CommonColor.FromArgb(0, 0, 0, 0),
                Border = false,
            };
            _sureButton.Click += (o, e) => DoAction();

            // 倒计时：原版 txt_time horizontalCenter=39 / verticalCenter=60 → 按钮右侧
            _countLabel = new MirLabel
            {
                Parent = this,
                Location = new Point(108, 136),
                Size = new Size(54, 33),
                AutoSize = false,
                DrawFormat = new TextStyle(TextAnchor.MiddleCenter),
                ForeColour = CommonColor.FromArgb(255, 190, 190, 190),
                NotControl = true,
            };

            // 关闭按钮：原版 btn_close x=144 y=0，com_btn_close0/1（26×42）
            _closeButton = new MirButton
            {
                Parent = this,
                AutoSize = false,
                Size = new Size(26, 26),
                Location = new Point(144, 0),
                AtlasNameKey = BtnAtlas + "#" + CloseUp,
                PressedAtlasFrame = CloseDown,
                Sound = SoundList.ButtonA,
                BackColour = CommonColor.FromArgb(0, 0, 0, 0),
                Border = false,
            };
            _closeButton.Click += (o, e) => Hide();

            Visible = false;
        }

        /// <summary>快捷使用（技能书）：原版 NoticeUseItemView「快速使用」。</summary>
        public void ShowUse(UserItem item, Action onAction)
        {
            Show(item, "快速使用", 0, onAction);
        }

        /// <summary>快捷装备（战力更高）：原版 NoticeWearEquipView「快捷装备」。</summary>
        public void ShowEquip(UserItem item, int powerDelta, Action onAction)
        {
            Show(item, "快捷装备", powerDelta, onAction);
        }

        private void Show(UserItem item, string title, int powerDelta, Action onAction)
        {
            if (item == null || item.Info == null) return;

            _item = item;
            _onAction = onAction;
            _deadline = CMain.Time + HoldSeconds * 1000;

            _icon.Item = item;
            _titleLabel.Text = title;
            _nameLabel.Text = item.Info.FriendlyName ?? string.Empty;
            _powerLabel.Text = powerDelta > 0 ? $"↑ 战力 +{powerDelta}" : string.Empty;
            _countLabel.Text = $"({HoldSeconds})";

            // 原版浮窗出现在屏幕侧边（不遮挡角色）：PC/浏览器统一放右下偏上。
            Location = new Point(Math.Max(4, Settings.ScreenWidth - PanelW - 12),
                                 Math.Max(4, Settings.ScreenHeight - PanelH - 120));

            Visible = true;
            BringToFront();
        }

        public override void Hide()
        {
            base.Hide();
            _item = null;
            _onAction = null;
        }

        // 由 GameScene.Process 每帧调用：倒计时递减，到 0 自动关闭。
        public void Process(long time)
        {
            if (!Visible) return;
            int left = (int)Math.Max(0, (_deadline - time + 999) / 1000);
            string text = $"({left})";
            if (_countLabel.Text != text) _countLabel.Text = text;
            if (time >= _deadline) Hide();
        }

        private void DoAction()
        {
            if (_item == null) return;
            Action action = _onAction;
            Hide();
            action?.Invoke();
        }

        /// <summary>
        /// 单件装备评分（用于判断"战力更高"）。经典传奇权重：攻/魔/道 > 防/魔防 > 其它。
        /// </summary>
        public static int PowerOf(UserItem item)
        {
            if (item == null || item.Info == null) return 0;
            Stats s = new Stats(item.Info.Stats ?? new Stats());
            if (item.AddedStats != null) s.Add(item.AddedStats);

            int dc = s[Stat.MinDC] + s[Stat.MaxDC];
            int mc = s[Stat.MinMC] + s[Stat.MaxMC];
            int sc = s[Stat.MinSC] + s[Stat.MaxSC];
            int ac = s[Stat.MinAC] + s[Stat.MaxAC];
            int mac = s[Stat.MinMAC] + s[Stat.MaxMAC];

            int power = dc * 3 + mc * 3 + sc * 3 + ac * 2 + mac * 2;
            power += s[Stat.HP] + s[Stat.MP];
            return power;
        }

        /// <summary>是否为可穿戴装备（用于区分「快捷装备」与「快捷使用」）。</summary>
        public static bool IsEquipment(ItemType type)
        {
            switch (type)
            {
                case ItemType.Weapon:
                case ItemType.Armour:
                case ItemType.Helmet:
                case ItemType.Necklace:
                case ItemType.Bracelet:
                case ItemType.Ring:
                case ItemType.Amulet:
                case ItemType.Belt:
                case ItemType.Boots:
                case ItemType.Stone:
                case ItemType.Torch:
                case ItemType.Mount:
                case ItemType.Reins:
                case ItemType.Bells:
                case ItemType.Saddle:
                case ItemType.Ribbon:
                case ItemType.Mask:
                case ItemType.DouLi:
                case ItemType.MianSha:
                case ItemType.XunZhang:
                case ItemType.YuPei:
                case ItemType.LiaoShang:
                    return true;
                default:
                    return false;
            }
        }
    }
}
