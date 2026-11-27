using Client.MirControls;
using Client.MirGraphics;
using Client.MirSounds;
using KFramework.MonoGameExtend;
using Client.MirNetwork;
using Client.MirObjects;
using SlimDX;
using System.Text.RegularExpressions;
using WebGame.Mir2.MonoGame.Client;
using C = ClientPackets;

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
                ForeColour = CommonColor.White,
                Font = FontConst.F12Bold,
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
                DrawFormat = new TextStyle(TextAnchor.MiddleLeft), ForeColour = CommonColor.FromArgb(255, 210, 180),
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
            List<ClientMagic> source = GameScene.User == null ? null : GameScene.User.Magics;
            List<ClientMagic> list = source == null ? new List<ClientMagic>() : GetSortedMagics(source);
            int count = list.Count;

            if (_rows.Count != count)
            {
                Rebuild(count);
            }

            for (int i = 0; i < _rows.Count; i++)
            {
                bool has = i < list.Count;
                _rows[i].Visible = has;
                if (has)
                {
                    _rows[i].Update(list[i]);
                }
            }
        }

        /// <summary>
        /// 原始传奇 RolePop_jn.setSkillList 排序：普通攻击置顶，已学技能在前，
        /// 其余按 cm.skill.index（本工程 OriginIndex）升序；相同项保持服务器下发顺序。
        /// </summary>
        private static List<ClientMagic> GetSortedMagics(List<ClientMagic> source)
        {
            List<ClientMagic> visible = GetVisibleMagics(source);
            var entries = new List<SkillSortEntry>(visible.Count);
            for (int i = 0; i < visible.Count; i++)
            {
                entries.Add(new SkillSortEntry(visible[i], i));
            }

            entries.Sort(CompareSkillSortEntry);

            var result = new List<ClientMagic>(visible.Count);
            for (int i = 0; i < entries.Count; i++)
            {
                result.Add(entries[i].Magic);
            }

            return result;
        }

        /// <summary>
        /// 原始传奇 RolePop_jn.setSkillList：倚天四式 30301~30304 是互斥技能组。
        /// 原版 findMaxLevelCount 统计组内已学数量 r，只保留组内第 r 个；r=-1 时四式全部隐藏。
        /// </summary>
        private static List<ClientMagic> GetVisibleMagics(List<ClientMagic> source)
        {
            int learnedFourFormCount = 0;
            for (int i = 0; i < source.Count; i++)
            {
                if (GetFourFormPosition(source[i]) >= 0 && source[i].IsLearned())
                {
                    learnedFourFormCount++;
                }
            }

            int allowedFourFormPosition = learnedFourFormCount - 1;
            var result = new List<ClientMagic>(source.Count);
            for (int i = 0; i < source.Count; i++)
            {
                int position = GetFourFormPosition(source[i]);
                if (position >= 0 && position != allowedFourFormPosition)
                {
                    continue;
                }

                result.Add(source[i]);
            }

            return result;
        }

        private static int GetFourFormPosition(ClientMagic magic)
        {
            int id = (int)magic.Spell;
            if (id == 30301)
            {
                return 0;
            }

            if (id == 30302)
            {
                return 1;
            }

            if (id == 30303)
            {
                return 2;
            }

            if (id == 30304)
            {
                return 3;
            }

            return -1;
        }

        private static int CompareSkillSortEntry(SkillSortEntry a, SkillSortEntry b)
        {
            int rankA = GetSkillSortRank(a.Magic);
            int rankB = GetSkillSortRank(b.Magic);
            if (rankA != rankB)
            {
                return rankA.CompareTo(rankB);
            }

            int indexA = GetSkillSortIndex(a.Magic);
            int indexB = GetSkillSortIndex(b.Magic);
            if (indexA != indexB)
            {
                return indexA.CompareTo(indexB);
            }

            return a.OriginalIndex.CompareTo(b.OriginalIndex);
        }

        private static int GetSkillSortRank(ClientMagic magic)
        {
            if ((int)magic.Spell == 10101)
            {
                return 0;
            }

            if (magic.IsLearned())
            {
                return 1;
            }

            return 2;
        }

        private static int GetSkillSortIndex(ClientMagic magic)
        {
            if (magic.OriginIndex > 0)
            {
                return magic.OriginIndex;
            }

            return int.MaxValue;
        }

        private struct SkillSortEntry
        {
            public ClientMagic Magic;
            public int OriginalIndex;

            public SkillSortEntry(ClientMagic magic, int originalIndex)
            {
                Magic = magic;
                OriginalIndex = originalIndex;
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
                // 点"设置"文字 → 弹数字键面板（与 JS RolePop_jnRender.onTouch→uim.show(773) 一致）
                row.OnAssignKey += magic =>
                {
                    if (magic == null || !magic.IsLearned()) return;
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

                // 技能图标：鼠标悬浮显示技能提示框（唯一可 hover 命中区），不响应点击。
                _icon = new MirImageControl
                {
                    Parent = this, Location = new Point(12, 11), Size = new Size(56, 56),
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

                // "设置"文字（JS txt_state @234,50，带下划线）：只有点它才弹技能快捷键面板。
                _state = new MirLabel
                {
                    Parent = this, 
                    Location = new Point(220, 48), 
                    Size = new Size(44, 20),
                    DrawFormat = new TextStyle(TextAnchor.MiddleCenter),
                    ForeColour = CommonColor.FromArgb(255, 90, 200),
                    UnderLine = true,
                };
                _state.Click += (o, e) =>
                {
                    if (_magic != null && !SkillDescriptionHelper.IsPassive(_magic))
                        OnAssignKey?.Invoke(_magic);
                };

                // 未学灰罩（JS img_rect，半透明）
                _dim = new MirImageControl
                {
                    Parent = this, 
                    Location = new Point(0, 0), 
                    Size = new Size(RowW, RowH),
                    Texture = KDefaultRes.DefaultTexture2D, 
                    ForeColour = CommonColor.FromArgb(100, 0, 0, 0),
                    NotControl = true, 
                    Visible = false,
                };

            }

            public void Update(ClientMagic magic)
            {
                _magic = magic;
                // 技能提示框：悬浮技能图标显示技能描述（整行不响应点击，仅"设置"文字可点）。
                _icon.Hint = SkillDescriptionHelper.Get(magic);
                // 原始传奇技能图标：纯 CDN，无本地图集回落。
                _icon.ImageUrl = Mir2OriginObjectLibrary.SkillIconPath(magic.OriginIcon);
                _icon.ForeColour = CommonColor.White;

                bool learned = magic.IsLearned();
                _name.Text = magic.Name;
                _name.ForeColour = learned ? LearnColour : UnlearnColour;
                _level.Text = learned ? magic.Level.ToString() : "";
                _level.ForeColour = LearnColour;
                _point.Text = learned ? magic.Experience.ToString() : "未学习";
                _point.ForeColour = learned ? CommonColor.LightGray : UnlearnColour;
                _expBar.Visible = learned;
                _dim.Visible = !learned;

                // 被动技能不能绑定快捷键：不显示"设置/清空"，点击行也不弹 AssignKeyPanel。
                bool passive = SkillDescriptionHelper.IsPassive(magic);
                _state.Visible = learned && !passive;
                if (_state.Visible)
                {
                    bool assigned = GameScene.User != null && GameScene.User.Magics.Exists(m => m.Key > 0 && m.Spell == magic.Spell);
                    _state.Text = assigned ? "清空" : "设置";
                }
            }
        }
    }

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
                    Font = FontConst.F12,
                    ForeColour = CommonColor.White,
                    OutLine = true,
                    OutLineColour = CommonColor.Black,
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
                    Font = FontConst.F10,
                    ForeColour = CommonColor.White,
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
                    if (SkillDescriptionHelper.IsPassive(magic)) continue;   // 被动技能不占快捷键栏

                    // 原始传奇技能图标：ResUrl.url(icon,9)=icon/skills/{OriginIcon}.png，纯 CDN 无本地回落。
                    Cells[row].ImageUrl = Mir2OriginObjectLibrary.SkillIconPath(magic.OriginIcon);
                    Cells[row].ForeColour = CommonColor.White;
                    // 技能描述 + 按键/MP/CD 摘要
                    Cells[row].Hint = SkillDescriptionHelper.Get(magic) + "\n\n" +
                        LanguageMgr.Get("SkillMpCooldownKey", magic.Name,
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
            // 技能栏是常驻 UI 容器：新角色 1 级还没学任何技能（原始传奇第一个技能 7 级才解锁），
            // HasSkill 为 false。若在这里 return，整个技能栏就不显示，玩家看不到空栏和快捷键位。
            // 空栏时 UpdateUI 只是把各格清空，不会出错，所以不再用 HasSkill 拦截显示。
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
            SkillButton.ForeColour = CommonColor.White;

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
                ForeColour = CommonColor.White,
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
                ForeColour = CommonColor.White,
            };

            // 描述文字 "X快捷键设置为" @ (150,38)
            TitleLabel = new MirLabel
            {
                Location = new Point(150, 38),
                Parent = this,
                Size = new Size(230, 32),
                DrawFormat = new TextStyle(TextAnchor.UpperLeft),
                ForeColour = CommonColor.White,
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
                ForeColour = CommonColor.White,
                Visible = !secondOn,
            };
            var mSelectSecondBG = new MirImageControl
            {
                Location = new Point(350, 35),
                Parent = this,
                AutoSize = false,
                Size = new Size(24, 24),
                AtlasNameKey = "y_common-sheet#hide_select",
                ForeColour = CommonColor.White,
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
                ForeColour = CommonColor.White,
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

    // 技能描述（从原 MagicButton.Update 的 switch 提取，供技能栏/技能面板共用）
    public static class SkillDescriptionHelper
    {
        /// <summary>被动技能：不能也不该绑定技能快捷键（与 GameScene.UseSpell 的被动 return 列表一致）。</summary>
        public static bool IsPassive(ClientMagic magic)
        {
            Spell spell = EffectSpellOf(magic);
            if (spell == Spell.无) spell = magic.Spell;
            return IsPassive(spell);
        }
        public static bool IsPassive(Spell spell)
        {
            switch (spell)
            {
                case Spell.基本剑术: // 基本剑术
                case Spell.致命剑: // 致命剑
                case Spell.吸魔: // 吸魔
                case Spell.出血: // 出血
                case Spell.精神力战法: // 精神力战法
                case Spell.攻杀剑术: // 攻杀剑术
                case Spell.专注: // 专注
                case Spell.冥想: // 冥想
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>原始技能 id → 引擎效果 Spell 值（与服务端 HumanObject.OriginEffectSpellBySkillId 一致）。
        /// 214 条技能全部用原始 id 后，技能说明文案按此表路由到已有的经典文案。</summary>
        public static Spell EffectSpellOf(ClientMagic magic)
        {
            if (magic == null) return Spell.无;
            int id = (int)magic.Spell;
            if (id <= 218) return magic.Spell;
            switch (id)
            {
                case 11011: return Spell.基本剑术;
                case 11021: return Spell.攻杀剑术;
                case 11031: return Spell.刺杀剑术;
                case 11041: return Spell.半月弯刀;
                case 11051: return Spell.野蛮冲撞;
                case 11061: return Spell.烈火剑法;
                case 11071: return Spell.开天斩;
                case 11081: return Spell.逐日剑法;
                case 11101: return Spell.狮子吼;
                case 11201: return Spell.十步一杀;
                case 12011: return Spell.火球术;
                case 12021: return Spell.抗拒火环;
                case 12041: return Spell.地狱火;
                case 12051: return Spell.雷电术;
                case 12061: return Spell.大火球;
                case 12071: return Spell.瞬间移动;
                case 12081: return Spell.爆裂火焰;
                case 12091: return Spell.火墙;
                case 12101: return Spell.疾光电影;
                case 12111: return Spell.地狱雷光;
                case 12121: return Spell.魔法盾;
                case 12131: return Spell.冰咆哮;
                case 12141: return Spell.灭天火;
                case 12151: return Spell.流星火雨;
                case 12161: return Spell.狂雷紫电;
                case 12181: return Spell.分身术;
                case 13011: return Spell.治愈术;
                case 13021: return Spell.精神力战法;
                case 13031: return Spell.施毒术;
                case 13041: return Spell.灵魂火符;
                case 13051: return Spell.召唤骷髅;
                case 13061: return Spell.隐身术;
                case 13071: return Spell.集体隐身术;
                case 13081: return Spell.幽灵盾;
                case 13091: return Spell.神圣战甲术;
                case 13101: return Spell.心灵启示;
                case 13111: return Spell.困魔咒_13111;
                case 13121: return Spell.群体治愈术;
                case 13131: return Spell.召唤神兽;
                case 13141: return Spell.噬血术;
                case 13151: return Spell.无极真气;
                case 13181: return Spell.召唤骨龙;
                case 14011: return Spell.护体神盾;
                case 308000: return Spell.气功波;
                default: return Spell.无;
            }
        }

        private static string FormatOriginTip(string show)
        {
            if (string.IsNullOrWhiteSpace(show))
            {
                return string.Empty;
            }

            string text = show.Replace("<br>", "\n").Replace("<br/>", "\n").Replace("<BR>", "\n").Replace("<br />", "\n");
            text = Regex.Replace(text, "<[^>]+>", string.Empty);
            return text.Trim();
        }

        public static string Get(ClientMagic magic)
        {
            if (magic == null)
            {
                return string.Empty;
            }

            string originTip = FormatOriginTip(magic.Show);
            string text = string.IsNullOrEmpty(originTip)
                ? GetLegacyDescription(magic)
                : magic.Name + "\n" + originTip;

            string stats = FormatTipStats(magic.TipStats);
            if (!string.IsNullOrEmpty(stats))
            {
                text += "\n\n" + stats;
            }

            return text;
        }

        private static string FormatTipStats(string stats)
        {
            if (string.IsNullOrWhiteSpace(stats))
            {
                return string.Empty;
            }

            return stats.Trim();
        }

        private static string GetLegacyDescription(ClientMagic magic)
        {
                switch (EffectSpellOf(magic))
                {  //Warrior
                    case Spell.基本剑术: // 基本剑术
                        return LanguageMgr.Get("FencingSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0);
                    case Spell.攻杀剑术: // 攻杀剑术
                        return LanguageMgr.Get("SlayingSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0);
                    case Spell.刺杀剑术: // 刺杀剑术
                        return LanguageMgr.Get("ThrustingSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0);
                    case Spell.无极真气: // 无极真气
                        return LanguageMgr.Get("RageSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.护体神盾: // 护体神盾
                        return LanguageMgr.Get("ProtectionFieldSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.半月弯刀: // 半月弯刀
                        return LanguageMgr.Get("HalfMoonSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.烈火剑法: // 烈火剑法
                        return LanguageMgr.Get("FlamingSwordSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.野蛮冲撞: // 野蛮冲撞
                        return LanguageMgr.Get("ShoulderDashSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.双半月: // 半月弯刀（双）
                        return LanguageMgr.Get("CrossHalfMoonSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.双烈火剑: // 双烈火剑
                        return LanguageMgr.Get("TwinDrakeBladeSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.困魔咒: // 困魔咒
                        return LanguageMgr.Get("EntrapmentSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.狮子吼: // 狮子吼
                        return LanguageMgr.Get("LionRoarSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.反击: // 反击
                        return LanguageMgr.Get("CounterAttackSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.金刚不坏: // 金刚护体
                        return LanguageMgr.Get("ImmortalSkinSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.狂暴: // 狂暴
                        return LanguageMgr.Get("FurySkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.逐日剑法: // 逐日剑法
                        return LanguageMgr.Get("SlashingBurstSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.剑雨: // 剑刃风暴
                        return LanguageMgr.Get("BladeAvalancheSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);

                    //Wizard
                    case Spell.火球术: // 火球术
                        return LanguageMgr.Get("FireballSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.雷电术: // 雷电术
                        return LanguageMgr.Get("ThunderboltSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.大火球: // 大火球
                        return LanguageMgr.Get("GreatFireballSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.抗拒火环: // 抗拒火环
                        return LanguageMgr.Get("RepulsionSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.地狱火: // 地狱火
                        return LanguageMgr.Get("HellfireSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.疾光电影: // 疾光电影
                        return LanguageMgr.Get("LightningSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.电击: // 电击
                        return LanguageMgr.Get("ElectricShockSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.瞬间移动: // 瞬间移动
                        return LanguageMgr.Get("TeleportSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.火墙: // 火墙
                        return LanguageMgr.Get("FireWallSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.爆裂火焰: // 爆裂火焰
                        return LanguageMgr.Get("FireBangSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.地狱雷光: // 地狱雷光
                        return LanguageMgr.Get("ThunderstormSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.魔法盾: // 魔法盾
                        return LanguageMgr.Get("MagicShieldSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.驱魔: // 驱尸术（复活死尸）
                        return LanguageMgr.Get("TurnUndeadSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.冰咆哮: // 冰咆哮
                        return LanguageMgr.Get("IceStormSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.灭天火: // 灭天火
                        return LanguageMgr.Get("FlameDisruptorSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.寒冰掌: // 寒冰掌
                        return LanguageMgr.Get("FrostCrunchSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.分身术: // 分身术
                        return LanguageMgr.Get("MirroringSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.狂雷紫电: // 狂雷紫电
                        return LanguageMgr.Get("FlameFieldSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.噬血术: // 噬血术
                        return LanguageMgr.Get("VampirismSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.冰风暴: // 暴风雪
                        return LanguageMgr.Get("BlizzardSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.流星火雨: // 流星火雨
                        return LanguageMgr.Get("MeteorStrikeSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.冰刺: // 冰刺
                        return LanguageMgr.Get("IceThrustSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.魔力增幅: // 魔法强化
                        return LanguageMgr.Get("MagicBoosterSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.快速移动: // 快速移动
                        return LanguageMgr.Get("FastMoveSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.风暴逃脱: // 风暴逃脱
                        return LanguageMgr.Get("StormEscapeSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.瞬移: // 瞬移
                        return LanguageMgr.Get("BlinkSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);

                    //Taoist
                    case Spell.精神力战法: // 精神力战法
                        return LanguageMgr.Get("SpiritSwordSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.治愈术: // 治愈术
                        return LanguageMgr.Get("HealingSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.施毒术: // 施毒术
                        return LanguageMgr.Get("PoisoningSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.灵魂火符: // 灵魂火符
                        return LanguageMgr.Get("SoulFireBallSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.幽灵盾: // 幽灵盾
                        return LanguageMgr.Get("SoulShieldSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.神圣战甲术: // 神圣战甲术
                        return LanguageMgr.Get("BlessedArmourSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.困魔咒_13111: // 困魔咒
                        return LanguageMgr.Get("TrapHexagonSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.召唤骷髅: // 召唤骷髅
                        return LanguageMgr.Get("SummonSkeletonSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.隐身术: // 隐身术
                        return LanguageMgr.Get("HidingSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.集体隐身术: // 集体隐身术
                        return LanguageMgr.Get("MassHidingSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.心灵启示: // 心灵启示
                        return LanguageMgr.Get("RevelationSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.群体治愈术: // 群体治愈术
                        return LanguageMgr.Get("MassHealingSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.召唤神兽: // 召唤神兽
                        return LanguageMgr.Get("SummonShinsuSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.终极强化: // 强化术
                        return LanguageMgr.Get("UltimateEnhancerSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.气功波: // 气功波
                        return LanguageMgr.Get("EnergyRepulsorSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.净化: // 净化术
                        return LanguageMgr.Get("PurificationSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.召唤骨龙: // 召唤骨龙
                        return LanguageMgr.Get("SummonHolyDevaSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.诅咒: // 诅咒术
                        return LanguageMgr.Get("CurseSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.心灵启示_76: // 幻象术
                        return LanguageMgr.Get("HallucinationSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.重生: // 重生术
                        return LanguageMgr.Get("ReincarnationSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.毒云: // 毒云术
                        return LanguageMgr.Get("PoisonCloudSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.能量护盾: // 能量护盾
                        return LanguageMgr.Get("EnergyShieldSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.瘟疫: // 瘟疫术
                        return LanguageMgr.Get("PlagueSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.治疗圈: // 治愈术（范围）
                        return LanguageMgr.Get("HealingCircleSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.宠物强化: // 宠物强化
                        return LanguageMgr.Get("PetEnhancerSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);

                    //Assassin
                    case Spell.致命剑: // 致命剑
                        return LanguageMgr.Get("FatalSwordPassiveSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.双斩: // 双斩
                        return LanguageMgr.Get("DoubleSlashToggleSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.加速: // 疾速
                        return LanguageMgr.Get("HasteBuffSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.十步一杀: // 十步一杀
                        return LanguageMgr.Get("FlashDashSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.开天斩: // 开天斩
                        return LanguageMgr.Get("HeavenlySwordSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.赤龙斩: // 爆炎斩
                        return LanguageMgr.Get("FireBurstSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.玄冰刃: // 陷阱
                        return LanguageMgr.Get("TrapSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.月光: // 月光斩
                        return LanguageMgr.Get("MoonlightSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.吸魔: // 吸魔
                        return LanguageMgr.Get("MPEaterSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.疾风步: // 疾风步
                        return LanguageMgr.Get("SwiftFeetSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.轻身术: // 轻身术
                        return LanguageMgr.Get("LightBodySkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.噬魂毒: // 毒剑
                        return LanguageMgr.Get("PoisonSwordSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.暗黑化身: // 暗影术
                        return LanguageMgr.Get("DarkBodySkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.新月斩: // 新月斩
                        return LanguageMgr.Get("CrescentSlashSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.出血: // 出血
                        return LanguageMgr.Get("HemorrhageSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.月雾: // 月雾
                        return LanguageMgr.Get("MoonMistSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);

                    //Archer
                    case Spell.专注: // 专注
                        return LanguageMgr.Get("FocusSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.直射击: // 直射击
                        return LanguageMgr.Get("StraightShotSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.双箭: // 双重射击
                        return LanguageMgr.Get("DoubleShotSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.爆炸陷阱: // 爆炸陷阱
                        return LanguageMgr.Get("ExplosiveTrapSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.延迟爆炸: // 延迟爆炸
                        return LanguageMgr.Get("DelayedExplosionSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.冥想: // 冥想
                        return LanguageMgr.Get("MeditationSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.后退: // 后跃
                        return LanguageMgr.Get("BackStepSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.元素射击: // 元素箭
                        return LanguageMgr.Get("ElementalShotSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.集中: // 集中
                        return LanguageMgr.Get("ConcentrationSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.石陷阱: // 石陷阱
                        return LanguageMgr.Get("StoneTrapSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.元素屏障: // 元素屏障
                        return LanguageMgr.Get("ElementalBarrierSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.召唤吸血鬼: // 召唤吸血鬼
                        return LanguageMgr.Get("SummonVampireSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.吸血箭: // 吸血箭
                        return LanguageMgr.Get("VampireShotSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.召唤蟾蜍: // 召唤蟾蜍
                        return LanguageMgr.Get("SummonToadSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.毒箭: // 毒箭
                        return LanguageMgr.Get("PoisonShotSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.残废射击: // 残废箭
                        return LanguageMgr.Get("CrippleShotSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.召唤蛇: // 召唤蛇
                        return LanguageMgr.Get("SummonSnakesSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.凝固汽油弹: // 燃烧箭
                        return LanguageMgr.Get("NapalmShotSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);
                    case Spell.天人合一: // 天人合一
                        return LanguageMgr.Get("OneWithNatureSkillDescription", magic.Level, magic.Level == 0 ? magic.Level1 : magic.Level == 1 ? magic.Level2 : magic.Level == 2 ? magic.Level3 : 0, magic.BaseCost);


                    default:
                        break;
                }
                return string.Empty;
        }
    }
}
