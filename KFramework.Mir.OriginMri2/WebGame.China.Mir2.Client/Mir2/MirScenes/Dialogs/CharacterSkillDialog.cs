using Client.MirControls;
using Client.MirGraphics;
using Client.MirSounds;
using KFramework.MonoGame;
using KFramework.MonoGameExtend;
using Color = MirEngine.Color;

namespace Client.MirScenes.Dialogs
{
    /// <summary>
    /// 角色技能面板（主角专用）。从 CharacterDialog 的技能页拆出，参考原始传奇 JS 的
    /// NewheroPop_jnSkin：竖排大格列表（图标 + 名称 + 等级 + 经验条 + 快捷键），带滚动条。
    /// 英雄功能本工程不使用，英雄技能页仍留在 CharacterDialog 中，与本类无关。
    /// </summary>
    public sealed class CharacterSkillDialog : MirImageControl
    {
        private const string Sheet = "y_common-sheet";
        private const string BgUrl = "resource/assets/gameui4/jpg/bag_bg.png";

        // 面板尺寸（背景 bag_bg.png 原生 354×475，沿用 CharacterDialog 同款底图）
        private const int PanelW = 354;
        private const int PanelH = 475;

        // 列表区域（JS: group_page 286×396 @ (15,66)，这里按本工程底图微调）
        private const int ListX = 16, ListY = 62, ListW = 286, ListH = 386;

        // 单个技能格（JS: NewheroPop_jnRenderSkin 275×77，本工程按可用宽度收缩）
        private const int RowW = 266, RowH = 70, RowGap = 4, RowStep = RowH + RowGap;

        private static readonly Color RowFill = new Color(45, 54, 84);
        private static readonly Color RowHover = new Color(70, 98, 152);
        private static readonly Texture2D DefTex = KDefaultRes.DefaultTexture2D;

        private MirScrollView _scroll;
        private MirButton _closeButton;
        private readonly List<SkillRow> _rows = new List<SkillRow>();

        public CharacterSkillDialog()
        {
            Size = new Size(PanelW, PanelH);
            Movable = true;
            Sort = true;
            AutoSize = false;
            ImageKey = BgUrl;

            var title = new MirLabel
            {
                Parent = this,
                Text = "技能",
                Location = new Point(16, 16),
                Size = new Size(PanelW - 60, 24),
                DrawFormat = new TextStyle(TextAnchor.MiddleLeft),
                ForeColour = Color.BurlyWood,
                Font = new Font(Settings.FontName, 10F, KFramework.MonoGame.FontStyle.Bold),
            };

            _scroll = new MirScrollView
            {
                Parent = this,
                Location = new Point(ListX, ListY),
                Size = new Size(ListW, ListH),
            };
            _scroll.ShowScrollBar = true;

            _closeButton = new MirButton
            {
                AtlasNameKey = Sheet + "#com_btn_close0",
                PressedAtlasFrame = "com_btn_close1",
                Parent = this,
                Location = new Point(Size.Width - 30, 10),
                Size = new Size(26, 26),
                Sound = SoundList.ButtonA,
            };
            _closeButton.Click += (o, e) => Hide();
            _closeButton.BringToFront();

            BeforeDraw += (o, e) => RefreshUI();
        }

        /// <summary>显示主角技能面板（居中置顶）。</summary>
        public override void Show()
        {
            base.Show();
            BringToFront();
            Location = new Point((Settings.ScreenWidth - Size.Width) / 2,
                                 (Settings.ScreenHeight - Size.Height) / 2);
        }

        public void Toggle()
        {
            if (Visible)
            {
                Hide();
            }
            else
            {
                Show();
            }
        }

        public void RefreshUI()
        {
            var user = GameScene.User;
            var list = user == null ? null : user.Magics;
            int count = list == null ? 0 : list.Count;

            if (count != _rows.Count)
            {
                Rebuild(count);
            }
            else
            {
                UpdateRows();
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
                SkillRow row = new SkillRow(i)
                {
                    Location = new Point(4, i * RowStep),
                    Size = new Size(RowW, RowH),
                    AutoSize = false,
                };
                _rows.Add(row);
                _scroll.Add(row); // 必须走 Add 挂到 MirScrollView 的内容容器，Parent = _scroll 不会被烘焙渲染
            }

            UpdateRows();
        }

        private void UpdateRows()
        {
            var list = GameScene.User == null ? null : GameScene.User.Magics;
            for (int i = 0; i < _rows.Count; i++)
            {
                if (list == null || i >= list.Count)
                    _rows[i].Visible = false;
                else
                {
                    _rows[i].Visible = true;
                    _rows[i].Update(list[i]);
                }
            }
        }

        /// <summary>单个技能格：JS NewheroPop_jnRenderSkin 风格（图标 + 名称 + 等级 + 经验条 + 快捷键）。</summary>
        private sealed class SkillRow : MirImageControl
        {
            private readonly int _index;
            private readonly MirImageControl _bg;
            private readonly MirButton _icon;
            private readonly MirLabel _name;
            private readonly MirLabel _level;
            private readonly MirLabel _key;
            private readonly MirImageControl _expBack;
            private readonly MirImageControl _expFill;
            private readonly MirLabel _hint;
            private ClientMagic _magic;

            public SkillRow(int index)
            {
                _index = index;

                _bg = new MirImageControl
                {
                    Parent = this,
                    Location = new Point(0, 2),
                    Size = new Size(RowW - 4, RowH - 4),
                    AutoSize = false,
                    NotControl = true,
                    Texture = DefTex,
                    ForeColour = RowFill,
                };

                _icon = new MirButton
                {
                    Parent = this,
                    Location = new Point(8, 7),
                    Size = new Size(56, 56),
                    Library = Libraries.MagIcon2,
                    Index = 0,
                    PressedIndex = 1,
                    Sound = SoundList.ButtonA,
                };
                _icon.Click += (o, e) =>
                {
                    if (_magic == null || GameScene.User == null || GameScene.User.Dead)
                        return;
                    new AssignKeyPanel(_magic, 1, new string[]
                    {
                        "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8",
                        "Ctrl" + Environment.NewLine + "F1", "Ctrl" + Environment.NewLine + "F2",
                        "Ctrl" + Environment.NewLine + "F3", "Ctrl" + Environment.NewLine + "F4",
                        "Ctrl" + Environment.NewLine + "F5", "Ctrl" + Environment.NewLine + "F6",
                        "Ctrl" + Environment.NewLine + "F7", "Ctrl" + Environment.NewLine + "F8",
                    }) { Actor = GameScene.User };
                };

                _name = new MirLabel
                {
                    Parent = this,
                    Location = new Point(72, 8),
                    Size = new Size(120, 22),
                    AutoSize = false,
                    DrawFormat = new TextStyle(TextAnchor.UpperLeft),
                    ForeColour = Color.White,
                };

                _level = new MirLabel
                {
                    Parent = this,
                    Location = new Point(RowW - 62, 8),
                    Size = new Size(54, 22),
                    AutoSize = false,
                    DrawFormat = new TextStyle(TextAnchor.UpperRight),
                    ForeColour = Color.Yellow,
                };

                _expBack = new MirImageControl
                {
                    Parent = this,
                    Location = new Point(72, 38),
                    Size = new Size(RowW - 84, 8),
                    AutoSize = false,
                    NotControl = true,
                    Texture = DefTex,
                    ForeColour = new Color(18, 22, 38),
                };
                _expFill = new MirImageControl
                {
                    Parent = this,
                    Location = new Point(72, 38),
                    Size = new Size(0, 8),
                    AutoSize = false,
                    NotControl = true,
                    Texture = DefTex,
                    ForeColour = Color.LimeGreen,
                };

                _key = new MirLabel
                {
                    Parent = this,
                    Location = new Point(72, 50),
                    Size = new Size(RowW - 84, 16),
                    AutoSize = false,
                    DrawFormat = new TextStyle(TextAnchor.UpperLeft),
                    ForeColour = Color.LightGray,
                };

                _hint = new MirLabel
                {
                    Parent = this,
                    Location = new Point(8, 52),
                    Size = new Size(56, 14),
                    AutoSize = false,
                    DrawFormat = new TextStyle(TextAnchor.MiddleCenter),
                    ForeColour = Color.Gold,
                    Text = "快捷键",
                };

                BeforeDraw += (o, e) =>
                {
                    _bg.ForeColour = MirControl.MouseControl == this || MirControl.MouseControl == _icon ? RowHover : RowFill;
                };
            }

            public void Update(ClientMagic magic)
            {
                _magic = magic;
                if (magic == null)
                {
                    Visible = false;
                    return;
                }

                int iconIndex = magic.Icon * 2;
                _icon.Index = iconIndex;
                _icon.PressedIndex = iconIndex + 1;
                _name.Text = magic.Name;
                _level.Text = "Lv" + magic.Level;

                ushort need = magic.Level switch
                {
                    1 => magic.Need1,
                    2 => magic.Need2,
                    3 => magic.Need3,
                    _ => magic.Need1,
                };
                float ratio = need > 0 ? Math.Min(1f, magic.Experience / (float)need) : 0f;
                _expFill.Size = new Size(Math.Max(0, (int)((RowW - 84) * ratio)), 8);

                _key.Text = magic.Key > 0
                    ? "快捷键: " + KeyName(magic.Key)
                    : "点击图标设置快捷键";
            }

            private static string KeyName(byte key)
            {
                if (key >= 1 && key <= 8) return "F" + key;
                if (key >= 9 && key <= 16) return "Ctrl+F" + (key - 8);
                return key.ToString();
            }
        }
    }
}
