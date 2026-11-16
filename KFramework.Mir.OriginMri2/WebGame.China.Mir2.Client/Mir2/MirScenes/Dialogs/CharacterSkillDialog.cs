using Client.MirControls;
using Client.MirGraphics;
using Client.MirSounds;
using KFramework.MonoGameExtend;
using Color = MirEngine.Color;

namespace Client.MirScenes.Dialogs
{
    /// <summary>
    /// 角色技能面板（主角专用）。按原始传奇 PC 网页版 NewheroPop_jnSkin(286×396) 布局移植：
    /// 竖向滚动的技能列表，每行 NewheroPop_jnRenderSkin(275×77)：技能图标(56×56) + 技能名 + 等级
    /// + 技能点 + 经验条 + 右侧"设置/清空"按钮。
    /// 数字键绑定逻辑（用户要求整合）：点每行"设置/清空"→ 弹 SkillHotKeyPop(数字键面板=AssignKeyPanel)，
    /// 该技能若已在快捷栏则显示"清空"并可直接解绑。与 JS RolePop_jnRender.onTouch→uim.show(773) 一致。
    /// 主角快捷栏 key = 1..16（对应数字键面板 slot 0..15），英雄走 CharacterDialog，与本类无关。
    /// </summary>
    public sealed class CharacterSkillDialog : MirImageControl
    {
        private const string CommonSheet = "y_common-sheet";
        private const string NGridSheet = "y_ngrid-sheet";
        private const string RoleSheet = "y_role-sheet";

        private const int RowW = 275, RowH = 77;
        private const int ListX = 9, ListY = 70;
        private const int ViewW = 285, ViewH = 390;
        private const byte MainKeyOffset = 1;   // 主角快捷栏 F1..F16

        private static readonly Color LearnColour = new Color(235, 220, 170);
        private static readonly Color UnlearnColour = new Color(120, 120, 130);

        private MirScrollView _scroll;
        private MirButton _closeButton;
        private MirLabel _pageLabel;
        private readonly List<SkillRow> _rows = new List<SkillRow>();

        public CharacterSkillDialog()
        {
            Movable = true;
            Sort = true;
            AutoSize = false;
            Size = new Size(350, 475);

            // 背景 bag_bg0.png（JS img_bg，y_common-sheet 帧）
            var mBg = new MirImageControl
            {
                Parent = this,
                ImageKey = "resource/assets/gameui4/jpg/bag_bg.png",
                Location = new Point(0, 0),
                Size = new Size(318, 475),
                NotControl = true
            };

            var mTitle = new MirLabel
            {
                Parent = this,
                Text = "技能",
                Location = new Point(0, 20),
                Size = new Size(318, 30),
                DrawFormat = new TextStyle(TextAnchor.MiddleCenter),
                ForeColour = Color.White,
                Font = new Font(Settings.FontName, 12F, KFramework.MonoGame.FontStyle.Bold),
                NotControl = true
            };
            
            // 竖向滚动技能列表（JS s1 / skillList，itemRenderer=RolePop_jnRender）
            _scroll = new MirScrollView
            {
                Parent = this,
                Location = new Point(ListX, ListY),
                Size = new Size(ViewW, ViewH),
            };
            _scroll.ShowScrollBar = true;

            // 底部页码标签（原版此位置是"觉醒技能"/"技能配置"按钮，客户端无觉醒系统，仅显示提示）
            _pageLabel = new MirLabel
            {
                Parent = this, Text = "技能 · 点击【设置】绑定快捷键",
                Location = new Point(8, 480), 
                Size = new Size(230, 28),
                DrawFormat = new TextStyle(TextAnchor.MiddleLeft), ForeColour = Color.FromArgb(255, 210, 180),
                NotControl = true,
            };

            // 关闭按钮 com_btn_close0/1
            _closeButton = new MirButton
            {
                AtlasNameKey = CommonSheet + "#com_btn_close0",
                PressedAtlasFrame = "com_btn_close1",
                Parent = this,
                Location = new Point(318, 50),
                Size = new Size(24, 24),
                Sound = SoundList.ButtonA,
            };
            _closeButton.Click += (o, e) => Hide();
            _closeButton.BringToFront();

            BeforeDraw += (o, e) => RefreshUI();
        }

        public override void Show()
        {
            base.Show();
            BringToFront();
            Location = new Point((Settings.ScreenWidth - Size.Width) / 2,
                                 (Settings.ScreenHeight - Size.Height) / 2);
        }

        public void Toggle()
        {
            if (Visible) Hide();
            else Show();
        }

        public void RefreshUI()
        {
            var list = GameScene.User == null ? null : GameScene.User.Magics;
            int count = list == null ? 0 : list.Count;

            if (_rows.Count != count)
                Rebuild(count);

            for (int i = 0; i < _rows.Count; i++)
            {
                bool has = list != null && i < list.Count;
                _rows[i].Visible = has;
                if (has) _rows[i].Update(list[i]);
            }
        }

        private void Rebuild(int count)
        {
            foreach (var row in _rows)
            {
                _scroll.Remove(row);
                row.Dispose();
            }
            _rows.Clear();

            for (int i = 0; i < count; i++)
            {
                var row = new SkillRow();
                row.Location = new Point(0, i * RowH);
                row.Size = new Size(RowW, RowH);
                row.AutoSize = false;
                // 点"设置/清空"→ 弹数字键面板（与 JS RolePop_jnRender.onTouch→uim.show(773) 一致）
                row.OnAssignKey += magic =>
                {
                    if (magic == null || magic.Level == 0) return;
                    new AssignKeyPanel(magic, MainKeyOffset, null) { Actor = GameScene.User };
                };
                _rows.Add(row);
                _scroll.Add(row);
            }
        }

        /// <summary>
        /// 技能行（JS NewheroPop_jnRenderSkin 275×77）：
        /// 图标(56×56 @12) + 名(@84,8) + 等级(@228,9) + 技能点(@139,47) + 经验条(z_skill_exp @84,47)
        /// + 右侧"设置/清空"(@234,50) + 未学灰罩(img_rect)。
        /// </summary>
        private sealed class SkillRow : MirImageControl
        {
            public Action<ClientMagic> OnAssignKey;
            private readonly MirImageControl _icon;
            private readonly MirLabel _name, _level, _point, _state;
            private readonly MirImageControl _expBar, _levelBar, _dim;
            private ClientMagic _magic;

            public SkillRow()
            {
                new MirImageControl
                {
                    Parent = this,
                    AtlasNameKey = RoleSheet + "#role_txtbg",
                    Location = new Point(79, 7),
                    AutoSize = false,
                    Size = new Size(190, 70),
                    NotControl = true,
                    DrawMode = MirImageDrawMode.NineGrid,
                    BorderTop = 10, BorderBottom = 10, BorderLeft = 10, BorderRight = 10,
                };

                _icon = new MirImageControl
                {
                    Parent = this, Location = new Point(12, 11), Size = new Size(56, 56), NotControl = true,
                };
                _name = new MirLabel
                {
                    Parent = this, Location = new Point(84, 8), Size = new Size(140, 30),
                    DrawFormat = new TextStyle(TextAnchor.MiddleLeft), NotControl = true,
                };
                _expBar = new MirImageControl
                {
                    Parent = this, 
                    AtlasNameKey = RoleSheet + "#z_skill_exp",
                    Location = new Point(84, 47),  
                    NotControl = true,
                };
                _levelBar = new MirImageControl
                {
                    Parent = this,
                    AtlasNameKey = RoleSheet + "#role_jn_img0",
                    Location = new Point(200, 12),
                    NotControl = true,
                };
                _level = new MirLabel
                {
                    Parent = this, Location = new Point(230, 7), Size = new Size(42, 30),
                    DrawFormat = new TextStyle(TextAnchor.MiddleCenter), NotControl = true,
                };
                _point = new MirLabel
                {
                    Parent = this, Location = new Point(139, 47), Size = new Size(90, 18),
                    DrawFormat = new TextStyle(TextAnchor.MiddleLeft), NotControl = true,
                };

                // "设置/清空"按钮（JS txt_state @234,50；点击弹数字键面板）
                _state = new MirLabel
                {
                    Parent = this, 
                    Location = new Point(220, 48), 
                    Size = new Size(44, 20),
                    DrawFormat = new TextStyle(TextAnchor.MiddleCenter),
                    ForeColour = Color.FromArgb(255, 90, 200),
                    UnderLine = true,
                };

                // 未学灰罩（JS img_rect，半透明）
                _dim = new MirImageControl
                {
                    Parent = this, 
                    Location = new Point(0, 0), 
                    Size = new Size(RowW, RowH),
                    Texture = KDefaultRes.DefaultTexture2D, 
                    ForeColour = Color.FromArgb(100, 0, 0, 0),
                    NotControl = true, 
                    Visible = false,
                };

                Click += (o, e) => OnAssignKey?.Invoke(_magic);
            }

            public void Update(ClientMagic magic)
            {
                _magic = magic;
                string url = Mir2OriginObjectLibrary.SkillIconPath(magic.OriginIcon);
                if (!string.IsNullOrEmpty(url))
                {
                    _icon.ImageUrl = url;
                    _icon.ForeColour = Color.White;
                }
                else
                {
                    _icon.ImageUrl = null;
                    _icon.Library = Libraries.MagIcon2;
                    _icon.Index = magic.Icon * 2;
                }

                bool learned = magic.Level > 0;
                _name.Text = magic.Name;
                _name.ForeColour = learned ? LearnColour : UnlearnColour;
                _level.Text = learned ? magic.Level.ToString() : "";
                _level.ForeColour = LearnColour;
                _point.Text = learned ? magic.Experience.ToString() : "未学习";
                _point.ForeColour = learned ? Color.LightGray : UnlearnColour;
                _expBar.Visible = learned;
                _dim.Visible = !learned;

                // JS：该技能在 handyKeys 中 → "清空"，否则 → "设置"
                bool assigned = GameScene.User != null && GameScene.User.Magics.Exists(m => m.Key > 0 && m.Spell == magic.Spell);
                _state.Text = assigned ? "清空" : "设置";
                _state.Visible = learned;
            }
        }
    }
}
