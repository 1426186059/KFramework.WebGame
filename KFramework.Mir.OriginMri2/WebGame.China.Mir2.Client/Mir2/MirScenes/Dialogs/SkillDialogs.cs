using Client.MirControls;
using Client.MirGraphics;
using Client.MirNetwork;
using Client.MirObjects;
using Client.MirSounds;
using SlimDX;
using System.Text.RegularExpressions;
using WebGame.Mir2.MonoGame.Client;
using C = ClientPackets;

namespace Client.MirScenes.Dialogs
{

    /// <summary>
    /// 技能栏单列（8 格）：封装一列的格子、按键标注、冷却与施法/刷新/冷却处理。
    /// _keyOffset=0 → 主键 1..8（Bar1Skill1..8）；=8 → 副键 9..16（Bar2Skill1..8）。
    /// 布局按原始传奇 HomeUIRightCtrlSkin：52×52 格、格距 52、底图 y_h_grid(0.9)、右侧按键文字。
    /// </summary>
    public sealed class SkillBarColumn : MirControl
    {
        private const string HomeSheet = "y_homeUI-sheet";

        private const int CellBgSize = 50;
        private const int CellSize = 44;
        public const int ColumnWidth = CellBgSize;
        public const int ColumnHeight = CellBgSize * 8;
        public const int SlotPerColumn = 8;
        private const int CellOffset = (CellBgSize - CellSize) / 2;

        private readonly int _keyOffset;

        // CDN SkillCdEff movieClip（36 帧半黑圆遮罩），两列共享一次加载。
        private static Task<Mir2AtlasSheet> _cdSheetTask;
        private static List<string> _cdFrames;   // SkillCdEff 时间线帧名
        private const string CdJsonKey = "resource/assets/gameui4/movieClip/SkillCdEff.json";
        private const string CdPngKey = "resource/assets/gameui4/movieClip/SkillCdEff.png";

        public MirImageControl[] Cells = new MirImageControl[SlotPerColumn];
        public MirLabel[] KeyNameLabels = new MirLabel[SlotPerColumn];
        public MirAnimatedAtlasControl[] CoolDowns = new MirAnimatedAtlasControl[SlotPerColumn];
        public MirLabel[] CdTexts = new MirLabel[SlotPerColumn];   // JS 的 cdSkillTxt：居中剩余秒数

        public bool HasSkill = false;

        public SkillBarColumn(int keyOffset)
        {
            _keyOffset = keyOffset;
            Size = new Size(ColumnWidth, ColumnHeight);
            Visible = true;

            for (int row = 0; row < SlotPerColumn; row++)
            {
                int y = row * CellBgSize;
                int spellSlot = keyOffset + row + 1;   // 循环体内局部拷贝，闭包捕获每格自己的值

                var grid = new MirImageControl
                {
                    Parent = this,
                    AtlasNameKey = HomeSheet + "#y_h_grid",
                    Location = new Point(0, y),
                    AutoSize = false,
                    Size = new Size(CellBgSize, CellBgSize),
                    NotControl = true,
                };

                Cells[row] = new MirImageControl
                {
                    Parent = this,
                    Location = new Point(CellOffset, y + CellOffset),
                    AutoSize = false,
                    Size = new Size(CellSize, CellSize),
                };
                Cells[row].Click += (o, e) => GameScene.Scene.UseSpell(spellSlot);
                CoolDowns[row] = new MirAnimatedAtlasControl
                {
                    Parent = this,
                    Location = new Point(0, y + 1),
                    AutoSize = false,
                    Size = new Size(CellSize, CellSize),
                    NotControl = true,
                    Visible = false,
                };

                CdTexts[row] = new MirLabel
                {
                    Text = "",
                    Font = new Font(Settings.FontName, 12F),
                    ForeColour = Color.White,
                    OutLine = true,
                    OutLineColour = Color.Black,
                    Parent = this,
                    Location = new Point(0, y),
                    Size = new Size(CellBgSize, CellBgSize),
                    NotControl = true,
                    Visible = false,
                    DrawFormat = new TextStyle(TextAnchor.MiddleCenter),           
                };

                KeyNameLabels[row] = new MirLabel
                {
                    Text = keyOffset == 0 ? (row + 1).ToString() : ("F" + (row + 1)),
                    Font = new Font(Settings.FontName, 10F),
                    ForeColour = Color.White,
                    Parent = this,
                    Location = new Point(CellOffset, y + CellOffset),
                    Size = new Size(CellSize, CellSize),
                    NotControl = true,
                    DrawFormat = new TextStyle(TextAnchor.LowerRight)
                };
            }

            _ = LoadCdSheetAsync();
        }

        /// <summary>异步加载 CDN 的 SkillCdEff movieClip，装配到本列 8 个 CD 控件。两列共享同一片。</summary>
        private async Task LoadCdSheetAsync()
        {
            try
            {
                if (_cdSheetTask == null)
                {
                    string jsonPath = Mir2OriginResource.ResolveManifestAssetPath(CdJsonKey);
                    string pngPath = Mir2OriginResource.ResolveManifestAssetPath(CdPngKey);
                    _cdSheetTask = Mir2AtlasLibrary.GetSheetByPathsAsync(jsonPath, pngPath);
                }
                Mir2AtlasSheet sheet = await _cdSheetTask;
                if (sheet == null) return;

                _cdFrames = sheet.MovieClip?.PlayOrder;
                foreach (var cd in CoolDowns)
                {
                    if (cd == null || cd.IsDisposed) continue;
                    cd.SetMovieClip(sheet);
                }
            }
            catch (Exception ex)
            {
                KFramework.MonoGame.PrintTool.LogError("[SkillBarColumn] SkillCdEff 加载失败：" + ex);
                _cdSheetTask = null;   // 允许下次重试
            }
        }

        public void UpdateUI()
        {
            HasSkill = false;
            foreach (var m in GameScene.User.Magics)
            {
                if (m.Key >= _keyOffset + 1 && m.Key <= _keyOffset + SlotPerColumn)
                    HasSkill = true;
            }

            for (int row = 0; row < SlotPerColumn; row++)
            {
                int slot = _keyOffset + row + 1;
                Cells[row].Index = -1;
                Cells[row].ImageUrl = null;   // 清掉上一轮 CDN 图标，避免换绑/解绑后残留

                var keybind = (KeybindOptions)((int)KeybindOptions.Bar1Skill1 + (slot - 1));
                string key = CMain.InputKeys.GetKey(keybind);

                foreach (var m in GameScene.User.Magics)
                {
                    if (m.Key != slot) continue;
                    HasSkill = true;
                    ClientMagic magic = MapObject.User.GetMagic(m.Spell);
                    if (magic == null) continue;
                    if (SkillDescriptionHelper.IsPassive(magic.Spell)) continue;   // 被动技能不占快捷键栏

                    // 原始传奇技能图标：ResUrl.url(icon,9)=icon/skills/{OriginIcon}.png，纯 CDN 无本地回落。
                    Cells[row].ImageUrl = Mir2OriginObjectLibrary.SkillIconPath(magic.OriginIcon);
                    Cells[row].ForeColour = Color.White;
                    // 技能描述 + 按键/MP/CD 摘要
                    Cells[row].Hint = SkillDescriptionHelper.Get(magic) + "\n\n" +
                        GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.SkillMpCooldownKey), magic.Name,
                        (magic.BaseCost + (magic.LevelCost * magic.Level)), Functions.PrintTimeSpanFromMilliSeconds(magic.Delay), key);
                }
            }
        }

        public void ProcessSkillDelay()
        {
            for (int row = 0; row < SlotPerColumn; row++)
            {
                int slot = _keyOffset + row + 1;
                foreach (var magic in GameScene.User.Magics)
                {
                    if (magic.Key != slot) continue;

                    long timeLeft = magic.CastTime + magic.Delay - CMain.Time;

                    if (timeLeft <= 0)
                    {
                        CoolDowns[row].Visible = false;
                        CdTexts[row].Visible = false;
                        continue;
                    }

                    // SkillCdEff 时间线：首帧=全黑、末帧=不黑。按已过去的时间比例取帧，
                    // 使 CD 开始时全黑、结束时完全透出（此前用剩余比例，方向反了）。
                    if (_cdFrames != null && _cdFrames.Count > 0)
                    {
                        double cdSeconds = magic.Delay / 1000.0;
                        if (cdSeconds <= 0) cdSeconds = 1;
                        double elapsedRatio = 1.0 - (timeLeft / 1000.0) / cdSeconds;
                        int frame = (int)(_cdFrames.Count * elapsedRatio);
                        if (frame < 0) frame = 0;
                        if (frame >= _cdFrames.Count) frame = _cdFrames.Count - 1;
                        CoolDowns[row].AtlasFrame = _cdFrames[frame];
                    }
                    CoolDowns[row].Visible = true;

                    // 居中剩余秒数（JS setCdTxt：toFixed(1)，15号白字黑描边）
                    CdTexts[row].Text = (timeLeft / 1000.0).ToString("0.0");
                    CdTexts[row].Visible = true;
                }
            }
        }
    }

    /// <summary>
    /// PC 页游技能快捷栏：右上角锚定，内含两列 SkillBarColumn（主键 1..8 / 副键 F1..F8）。
    /// 按原始传奇 HomeUIRightCtrlSkin（106×464）布局移植。
    /// </summary>
    public sealed class SkillBarDialog : MirImageControl
    {
        public SkillBarColumn Column1, Column2;   // 主键列(左) / 副键列(右)
        public bool AltBind;

        /// <summary>是否显示第二列（副键 F1..F8）。唯一数据源为 Settings.SkillBarTwoRows。</summary>
        public bool ShowSecondColumn
        {
            get { return Settings.SkillBarTwoRows; }
            set
            {
                Settings.SetSkillBarTwoRows(value);   // 直接改本地持久化变量并落库
                RefreshTwoRows();
            }
        }

        /// <summary>按 Settings.SkillBarTwoRows 当前值刷新列的显隐、位置、尺寸与锚点。</summary>
        public void RefreshTwoRows()
        {
            bool value = Settings.SkillBarTwoRows;
            if (value && Column2 == null)   // 从单列切回双列时补建
                Column2 = new SkillBarColumn(SkillBarColumn.SlotPerColumn) { Parent = this, Location = new Point(0, 0) };
            if (Column2 != null) Column2.Visible = value;

            // 双列：主键在右(x=ColumnWidth)、副键在左(x=0)；单列：主键在 x=0
            Column1.Location = new Point(value ? SkillBarColumn.ColumnWidth : 0, 0);
            Size = new Size(SkillBarColumn.ColumnWidth * (value ? 2 : 1), SkillBarColumn.ColumnHeight);
            ApplyAnchor();
            UpdateUI();
        }

        private int ColumnCount => Settings.SkillBarTwoRows ? 2 : 1;

        public SkillBarDialog()
        {
            AutoSize = false;
            Size = new Size(SkillBarColumn.ColumnWidth * ColumnCount, SkillBarColumn.ColumnHeight);
            Sort = true;
            Visible = true;
            Movable = false;

            Anchor = EAnchorType.TopRight;
            AnchorPos = new Point(0, 200);
            ApplyAnchor();

            if (ShowSecondColumn)
            {
                Column1 = new SkillBarColumn(0) { Parent = this, Location = new Point(SkillBarColumn.ColumnWidth, 0) };
                Column2 = new SkillBarColumn(SkillBarColumn.SlotPerColumn) { Parent = this, Location = new Point(0, 0) };
            }
            else
            {
                Column1 = new SkillBarColumn(0) { Parent = this, Location = new Point(0, 0) };
            }
        }

        public bool HasSkill { get { return Column1.HasSkill || (ShowSecondColumn && Column2.HasSkill); } }

        public void UpdateUI()
        {
            Column1.UpdateUI();
            if (ShowSecondColumn) Column2.UpdateUI();
        }

        public void Process()
        {
            Column1.ProcessSkillDelay();
            if (ShowSecondColumn) Column2.ProcessSkillDelay();
        }

        public override void Show()
        {
            if (Visible) return;
            if (!HasSkill) return;
            Settings.SkillBar = true;
            Visible = true;
            UpdateUI();
        }

        public override void Hide()
        {
            if (!Visible) return;
            Settings.SkillBar = false;
            Visible = false;
        }
    }

    public sealed class MagicButton : MirControl
    {
        public MirImageControl LevelImage, ExpImage;
        public MirButton SkillButton;
        public MirLabel LevelLabel, NameLabel, ExpLabel, KeyLabel;
        public ClientMagic Magic;
        public MirImageControl CoolDown;
        public bool HeroMagic;

        string[] Prefixes = new string[] { "", "CTRL", "Shift" };

        public MagicButton()
        {
            Size = new Size(231, 33);

            SkillButton = new MirButton
            {
                Parent = this,
                Location = new Point(36, 0),
                Size = new Size(32, 32),   // CDN Texture 通道按 Size 绘制
                Sound = SoundList.ButtonA,
            };
            SkillButton.Click += (o, e) =>
            {
                if (HeroMagic)
                {
                    if (GameScene.Hero == null || GameScene.Hero.Dead)
                        return;
                    new AssignKeyPanel(Magic, 17, new string[]
                        {
                            "Shift" + Environment.NewLine + "F1",
                            "Shift" + Environment.NewLine + "F2",
                            "Shift" + Environment.NewLine + "F3",
                            "Shift" + Environment.NewLine + "F4",
                            "Shift" + Environment.NewLine + "F5",
                            "Shift" + Environment.NewLine + "F6",
                            "Shift" + Environment.NewLine + "F7",
                            "Shift" + Environment.NewLine + "F8"
                        })
                    { Actor = GameScene.Hero };
                }
                else
                {
                    new AssignKeyPanel(Magic, 1, new string[]
                        {
                            "F1",
                            "F2",
                            "F3",
                            "F4",
                            "F5",
                            "F6",
                            "F7",
                            "F8",
                            "Ctrl" + Environment.NewLine + "F1",
                            "Ctrl" + Environment.NewLine + "F2",
                            "Ctrl" + Environment.NewLine + "F3",
                            "Ctrl" + Environment.NewLine + "F4",
                            "Ctrl" + Environment.NewLine + "F5",
                            "Ctrl" + Environment.NewLine + "F6",
                            "Ctrl" + Environment.NewLine + "F7",
                            "Ctrl" + Environment.NewLine + "F8"
                        })
                    { Actor = GameScene.User };
                }
            };

            LevelImage = new MirImageControl
            {
                Index = 516,
                Library = Libraries.Title,
                Location = new Point(73, 7),
                Parent = this,
                NotControl = true,
            };

            ExpImage = new MirImageControl
            {
                Index = 517,
                Library = Libraries.Title,
                Location = new Point(73, 19),
                Parent = this,
                NotControl = true,
            };

            LevelLabel = new MirLabel
            {
                AutoSize = true,
                Parent = this,
                Location = new Point(88, 2),
                NotControl = true,
            };

            NameLabel = new MirLabel
            {
                AutoSize = true,
                Parent = this,
                Location = new Point(109, 2),
                NotControl = true,
            };

            ExpLabel = new MirLabel
            {
                AutoSize = true,
                Parent = this,
                Location = new Point(109, 15),
                NotControl = true,
            };

            KeyLabel = new MirLabel
            {
                AutoSize = true,
                Parent = this,
                Location = new Point(2, 2),
                NotControl = true,
            };

            CoolDown = new MirImageControl
            {
                Library = Libraries.Prguse2,
                Parent = this,
                Location = new Point(36, 0),
                Opacity = 0.6F,
                NotControl = true,
                UseOffSet = true,
            };
        }

        public void Update(ClientMagic magic)
        {
            Magic = magic;

            NameLabel.Text = Magic.Name;

            LevelLabel.Text = Magic.Level.ToString();
            switch (Magic.Level)
            {
                case 0:
                    ExpLabel.Text = string.Format("{0}/{1}", Magic.Experience, Magic.Need1);
                    break;
                case 1:
                    ExpLabel.Text = string.Format("{0}/{1}", Magic.Experience, Magic.Need2);
                    break;
                case 2:
                    ExpLabel.Text = string.Format("{0}/{1}", Magic.Experience, Magic.Need3);
                    break;
                case 3:
                    ExpLabel.Text = "-";
                    break;
            }

            KeyLabel.Text = Magic.Key == 0 ? string.Empty : string.Format("{0}{1}F{2}",
                Prefixes[(Magic.Key - 1) / 8],
                Magic.Key > 8 ? Environment.NewLine : string.Empty,
                (Magic.Key - 1) % 8 + 1);

            SkillButton.Hint = SkillDescriptionHelper.Get(magic);


            // 原始传奇技能图标：按 OriginIcon 从 CDN 加载，纯 CDN 无本地回落。
            SkillButton.ImageUrl = Mir2OriginObjectLibrary.SkillIconPath(Magic.OriginIcon);
            SkillButton.ForeColour = Color.White;

            SetDelay();
        }

        public void SetDelay()
        {
            if (Magic == null) return;

            int totalFrames = 34;

            long timeLeft = Magic.CastTime + Magic.Delay - CMain.Time;

            if (timeLeft < 100)
            {
                CoolDown.Visible = false;
                return;
            }

            int delayPerFrame = (int)(Magic.Delay / totalFrames);
            int startFrame = totalFrames - (int)(timeLeft / delayPerFrame);

            if ((CMain.Time <= Magic.CastTime + Magic.Delay))
            {
                CoolDown.Visible = true;
                CoolDown.Index = 1290 + startFrame;
            }
        }
    }

    /// <summary>
    /// 技能数字键面板（主角/英雄通用）。按原始传奇 JS SkillHotKeySkin(516×217) 布局移植：
    /// 技能图标 + "X快捷键设置为" 描述 + 16 个数字键网格(0~15) + 清空/确定按钮 + 双排技能栏勾选。
    /// 图集全部走 CDN（y_skillhotkey-sheet），键号用 word_kjjsz_{n}，选中框 y_select。
    /// 绑定沿用现有 C.MagicKey(Spell,Key) 协议，key = 键号 + KeyOffset（主角 offset=1 → F1..F16 槽位）。
    /// </summary>
    public sealed class AssignKeyPanel : MirImageControl
    {
        private const string Sheet = "y_skillhotkey-sheet";
        private const string BgUrl = "resource/assets/gameui4/jpg/bg_kjjsz_01.png";
        private const int PanelW = 520, PanelH = 210;

        public MirButton SaveButton, NoneButton;
        public UserObject Actor;
        public MirLabel TitleLabel;
        public MirImageControl MagicImage;
        public MirButton[] FKeys;
        public MirImageControl[] SelectFrames;   // 每个数字键的选中框 y_select

        public ClientMagic Magic;
        public byte Key;
        public byte KeyOffset;

        public AssignKeyPanel(ClientMagic magic, byte keyOffset, string[] keyStrings)
        {
            Magic = magic;
            Key = magic.Key;
            KeyOffset = keyOffset;

            Modal = true;
            AutoSize = false;
            Size = new Size(PanelW, PanelH);
            Location = Center;
            Parent = GameScene.Scene;
            Visible = true;

            // 背景：bg_kjjsz_01.png（_Image1）+ 内框 bg_kjjsz_03（_Image3 @ 86,20）
            var bg = new MirImageControl
            {
                Parent = this,
                ImageKey = BgUrl,
                Location = new Point(0, 0),
                AutoSize = false,
                Size = new Size(PanelW, PanelH),
                NotControl = true,
                DrawMode = MirImageDrawMode.NineGrid,
                BorderTop = 20, BorderBottom = 20, BorderLeft = 20, BorderRight = 20,
            };

            var MagicImageBg = new MirImageControl
            {
                Location = new Point(92, 26),
                Parent = this,
                Size = new Size(50, 50),
                NotControl = true,
                AtlasNameKey = "y_skillhotkey-sheet#bg_kjjsz_03",
                ForeColour = Color.White,
            };

            // 技能图标 38×38 @ (92,26)
            MagicImage = new MirImageControl
            {
                Location = new Point(97, 31),
                Parent = this,
                AutoSize = false,
                Size = new Size(40, 40),
                NotControl = true,
                ImageUrl = Mir2OriginObjectLibrary.SkillIconPath(magic.OriginIcon),
                ForeColour = Color.White,
            };

            // 描述文字 "X快捷键设置为" @ (150,38)
            TitleLabel = new MirLabel
            {
                Location = new Point(150, 38),
                Parent = this,
                Size = new Size(230, 32),
                DrawFormat = new TextStyle(TextAnchor.UpperLeft),
                ForeColour = Color.White,
                Text = magic.Name + "快捷键设置为"
            };

            // 双排技能栏开关：两图同位置互斥显隐；图集帧用 AtlasNameKey（ImageKey 只支持单图），
            // 不能设 NotControl（否则点不到，Click 不触发）。
            bool secondOn = Settings.SkillBarTwoRows;   // 从唯一数据源 Settings 读取当前开关状态
            var mUnSelectSecondBG = new MirImageControl
            {
                Location = new Point(350, 35),
                Parent = this,
                AutoSize = false,
                Size = new Size(24, 24),
                AtlasNameKey = "y_common-sheet#hide_unselect",
                ForeColour = Color.White,
                Visible = !secondOn,
            };
            var mSelectSecondBG = new MirImageControl
            {
                Location = new Point(350, 35),
                Parent = this,
                AutoSize = false,
                Size = new Size(24, 24),
                AtlasNameKey = "y_common-sheet#hide_select",
                ForeColour = Color.White,
                Visible = secondOn,
            };
            mUnSelectSecondBG.Click += (object sender, EventArgs e) =>
            {
                Settings.SetSkillBarTwoRows(true);   // 直接改本地持久化变量并落库
                GameScene.Scene?.SkillBarDialogs?.RefreshTwoRows();
                mUnSelectSecondBG.Visible = false;
                mSelectSecondBG.Visible = true;
            };
            mSelectSecondBG.Click += (object sender, EventArgs e) =>
            {
                Settings.SetSkillBarTwoRows(false);  // 直接改本地持久化变量并落库
                GameScene.Scene?.SkillBarDialogs?.RefreshTwoRows();
                mSelectSecondBG.Visible = false;
                mUnSelectSecondBG.Visible = true;
            };

            // 描述文字 "X快捷键设置为" @ (150,38)
            var mDes = new MirLabel
            {
                Location = new Point(375, 35),
                Parent = this,
                Size = new Size(100, 24),
                DrawFormat = new TextStyle(TextAnchor.MiddleLeft),
                ForeColour = Color.White,
                Text = "双排技能栏"
            };

            // 16 个数字键网格：8 列 × 2 行，起点 (33,91)，每格 44×42，横间距 0、纵间距 12（JS TileLayout）
            FKeys = new MirButton[16];
            SelectFrames = new MirImageControl[16];
            for (byte i = 0; i < FKeys.Length; i++)
            {
                int col = i % 8, row = i / 8;
                FKeys[i] = new MirButton
                {
                    Parent = this,
                    AtlasNameKey = Sheet + "#btn_kjjsz_01",
                    PressedAtlasFrame = "btn_kjjsz_02",
                    Location = new Point(33 + 44 * col, 91 + (42 + 12) * row),
                    Size = new Size(50, 50),
                    Sound = SoundList.ButtonA,
                };
                // 数字图 word_kjjsz_{n}
                var digit = new MirImageControl
                {
                    Parent = FKeys[i],
                    AtlasNameKey = Sheet + "#word_kjjsz_" + i,
                    Size = new Size(44, 42),
                    NotControl = true,
                    Anchor =  EAnchorType.MiddleCenter,
                    AnchorPos = new Point(0, 0)
                };
                digit.ApplyAnchor();

                // 选中框 y_select（来自 y_common-sheet）
                SelectFrames[i] = new MirImageControl
                {
                    Parent = FKeys[i],
                    AtlasNameKey = "y_common-sheet#y_select",
                    Location = new Point(2, 2),
                    AutoSize = false,
                    Size = new Size(45, 45),
                    NotControl = true,
                    Visible = true,
                };

                int ii = i;
                FKeys[i].Click += (o, e) =>
                {
                    Key = (byte)(ii + KeyOffset);
                };
            }

            // 清空按钮 @ (397,86) / 确定按钮 @ (397,141)，各 85×46
            NoneButton = new MirButton
            {
                Parent = this,
                AtlasNameKey = Sheet + "#btn_kjjsz_01",
                PressedAtlasFrame = "btn_kjjsz_02",
                Location = new Point(397, 86),
                Size = new Size(85, 46),
                Sound = SoundList.ButtonA,
            };
            var clearWord = new MirImageControl
            {
                Parent = NoneButton, AtlasNameKey = Sheet + "#word_kjjsz_18",
                Location = new Point(0, 0), Size = new Size(85, 46), NotControl = true,
            };
            NoneButton.Click += (o, e) => { Key = 0; SaveAndClose(); };

            SaveButton = new MirButton
            {
                Parent = this,
                AtlasNameKey = Sheet + "#btn_kjjsz_01",
                PressedAtlasFrame = "btn_kjjsz_02",
                Location = new Point(397, 141),
                Size = new Size(85, 46),
                Sound = SoundList.ButtonA,
            };
            var okWord = new MirImageControl
            {
                Parent = SaveButton, AtlasNameKey = Sheet + "#word_kjjsz_17",
                Location = new Point(0, 0), Size = new Size(85, 46), NotControl = true,
            };
            SaveButton.Click += (o, e) => SaveAndClose();

            BeforeDraw += AssignKeyPanel_BeforeDraw;

            // 透明关闭：本面板 Modal=true，点击面板矩形外（被模态拦截的区域）也会派发到本控件。
            // 判定点击落在面板之外即关闭；面板内（含背景空白）不关，避免误触 UI。
            Click += (o, e) =>
            {
                if (!DisplayRectangle.Contains(CMain.MPoint))
                {
                    Dispose();
                }
            };

        }

        private void SaveAndClose()
        {
            if (Actor != null)
            {
                for (int i = 0; i < Actor.Magics.Count; i++)
                {
                    if (Actor.Magics[i].Key == Key)
                    {
                        Actor.Magics[i].Key = 0;
                    }
                }

                Network.Enqueue(new C.MagicKey { Spell = Magic.Spell, Key = Key, OldKey = Magic.Key });
                Magic.Key = Key;
                GameScene.Scene.SkillBarDialogs.UpdateUI();
            }
            Dispose();
        }

        private void AssignKeyPanel_BeforeDraw(object sender, EventArgs e)
        {
            int key = Key - KeyOffset;
            for (int i = 0; i < FKeys.Length; i++)
            {
                SelectFrames[i].Visible = (i == key);
            }
        }

    }
}
