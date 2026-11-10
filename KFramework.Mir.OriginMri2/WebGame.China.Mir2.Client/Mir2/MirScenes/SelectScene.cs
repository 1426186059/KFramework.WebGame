using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Client.MirControls;
using Client.MirGraphics;
using Client.MirNetwork;
using Client.MirScenes.Dialogs;
using Client.MirSounds;
using WebGame.Mir2.MonoGame.Client;
using C = ClientPackets;
using S = ServerPackets;

namespace Client.MirScenes
{
    // 《原始传奇》选人/创角界面（fugu_20201203）1:1 移植，覆盖旧 Mir2 选人场景。
    //
    // 布局来源：gameEui.json 的 "CreateRoleSkin3"（= default.thm*.json 里
    // resource/eui_skins/createrole/CreateRoleSkinNew3.exml，gjs 30KB），
    // $bs = 1390x960，$eleC 里 60+ 个控件的 x/y/width/height/source 全部照抄。
    // 之前只用 qufu1.min.js 里几个硬编码坐标（415/778、658/630）自己摆，是错的：
    // 那些是创建特效/按钮特效的位置，不是控件布局；且把 1390x860 的主背景图
    // 误当成 1390x960 的设计尺寸。
    //
    // 交互来源：qufu1.min.js 的 CreateRoleUICtrl3（setinfo / createrole / startgame /
    // changecarrer / randomcarrer / ToCDB / getcarrer / setTimeLimit / errtips），
    // main10.25487.1.min.js 的 CreateRoleUICtrlNew3 逻辑一致。
    //   setinfo: 0 角色 -> create_grp.visible=true 且隐藏所有角色信息标签；
    //            1 角色 -> 只显示 txt_name0/level0/c0；
    //            2 角色 -> 六个标签全显示，立绘 source = img_carrer{career}_{sex}。
    //   createrole: create_grp.x = 376（已有角色1）/ 735（空号）。
    //
    // 按钮帧名来自 CreateRoleSkinNew3.exml 的 gjs：
    //   btn_choose1/2 = btn_cjzy_01 / btn_cjzy_02；
    //   btn_start = btn_kaishiyouxi_01；btn_create = btn_chuangjian_01；
    //   btn_huifu = btn_huifurenwu_01；btn_return = btn_fanhui_01；
    //   btn_delete = btn_shanchurenwu_01；btn_close = com_btn_close0（y_common-sheet）；
    //   btn_carrer1/2/3 = icon_cjzy_01/02/03；btn_sex1/2 = icon_cjzy_06/05；
    //   btn_tj = btn_tijiao_01；btn_random = btn_cjzy_03。
    // 皮肤里的 word_cjzy_01..05 / txt_* 是独立 eui.Label（按钮贴图本身不含文字），
    // 本实现全部补成中文 MirLabel，位置照抄 EXML。
    //
    // 网络协议完全用本工程 C.* / S.*，不照搬页游 QufuMessageHandler 的 VarInt 协议。
    public sealed class SelectScene : MirScene
    {
        private const int DesignW = 1390, DesignH = 960;
        private const int InitTimerSeconds = 30;
        private const int SlideMilliseconds = 1500;

        private const string Atlas = "y_createrole-sheet";
        private static readonly Random Rnd = new Random();

        private static float ScaleF
        {
            get { return Math.Min(Settings.ScreenWidth / (float)DesignW, Settings.ScreenHeight / (float)DesignH); }
        }

        private static Point P(int x, int y)
        {
            float s = ScaleF;
            return new Point(
                (int)((Settings.ScreenWidth - DesignW * s) / 2 + x * s),
                (int)((Settings.ScreenHeight - DesignH * s) / 2 + y * s));
        }

        private static Point R(int x, int y)
        {
            float s = ScaleF;
            return new Point((int)(x * s), (int)(y * s));
        }

        private static Size Sz(int w, int h)
        {
            float s = ScaleF;
            return new Size(Math.Max(1, (int)(w * s)), Math.Max(1, (int)(h * s)));
        }

        public List<SelectInfo> Characters = new List<SelectInfo>();
        private int _selected;

        private MirImageControl _root, _mainBg;
        private readonly MirImageControl[] _slotBg = new MirImageControl[2];
        private readonly MirImageControl[] _slotModel = new MirImageControl[2];
        // 页游 eff_grp0/1 里的角色待机 MovieClip：resource/assets/gameui4/movieClip/carrer{c}_{s}.json。
        private readonly MirAtlasAnimatedControl[] _slotAnim = new MirAtlasAnimatedControl[2];
        private readonly string[] _slotAnimKey = new string[2];
        private readonly MirLabel[][] _slotCaption = new MirLabel[2][];
        private readonly MirLabel[][] _slotValue = new MirLabel[2][];

        private readonly MirButton[] _chooseButton = new MirButton[2];
        private MirButton _startButton, _createButton, _deleteButton, _exitButton, _huifuButton;
        private MirLabel _limitLabel, _errorLabel, _qufuLabel, _lastAccessLabel;

        // create_grp：EXML (735,198) 322x428，内部控件坐标都是相对该 Group。
        private MirImageControl _createPanel;
        private MirTextBox _nameBox;
        private readonly MirButton[] _classButtons = new MirButton[3];
        private readonly MirButton[] _sexButtons = new MirButton[2];
        private MirButton _submitButton, _randomNameButton, _closeCreateButton;
        private MirClass _newClass = MirClass.Warrior;
        private MirGender _newGender = 0;

        private long _countdownEnd, _errorUntil, _slideEnd;
        private int _slideSlot, _modelBaseY;
        private bool _slideRunning;

        public SelectScene(List<SelectInfo> characters)
        {
            SoundManager.PlayMusic(SoundList.SelectMusic, true);
            Disposing += (o, e) => SoundManager.StopMusic();

            // EXML 的 bg 是 1920x1080 居中黑色 eui.Rect，容器底部 100px 就是这块黑底。
            BackColour = Color.Black;

            Characters = characters ?? new List<SelectInfo>();
            SortList();

            KeyPress += OnKeyPress;
            BuildUI();
            SetInfo();
            ResetCountdown();
        }

        private void BuildUI()
        {
            // 根容器绝不能设 NotControl：MirControl.OnMouseMove 是「父控件自身
            // IsMouseOver 命中后才递归子控件」，根一旦 NotControl，整棵 UI 树都
            // 收不到鼠标事件（表现为所有按钮点不动）。只给纯装饰控件设 NotControl。
            _root = new MirImageControl
            {
                Parent = this,
                AutoSize = false,
                Location = P(0, 0),
                Size = Sz(DesignW, DesignH),
            };

            // _Image1 (0,-1) 1390x860 —— 注意不是铺满 960 的设计高度。
            _mainBg = Image(_root, "resource/assets/gameui4/jpg/bg_cjzy_02.jpg", 0, -1, DesignW, 860);

            // _Image2 / _Image3：两个槽底板，EXML (377,206) / (724,206)。
            _slotBg[0] = Image(_root, "resource/assets/gameui4/jpg/bg_cjzy_03.png", 377, 206, 334, 392);
            _slotBg[1] = Image(_root, "resource/assets/gameui4/jpg/bg_cjzy_04.png", 724, 206, 346, 399);

            // _Image4/5/6：底部三块信息面板底图。
            AtlasImage(_root, "bg_cjzy_01", 320, 618, 217, 170);
            Image(_root, "resource/assets/gameui4/jpg/bg_cjzy_01_2.png", 545, 617, 347, 170);
            AtlasImage(_root, "bg_cjzy_01_1", 897, 618, 217, 170);

            // _Image7 + txt_qufu：顶部区服名条。
            AtlasImage(_root, "bg_cjzy_05", 623, 155, 195, 37);
            _qufuLabel = Label(_root, 658, 160, 119, 24, "不删档999服");

            // _Image8..13：两槽各 3 条 120x20 信息底条（名字/等级/职业）。
            for (int i = 0; i < 2; i++)
            {
                int x = i == 0 ? 387 : 959;
                AtlasImage(_root, "bg_cjzy_00", x, 690, 120, 20);
                AtlasImage(_root, "bg_cjzy_00", x, 717, 120, 20);
                AtlasImage(_root, "bg_cjzy_00", x, 744, 120, 20);
            }

            // word_cjzy_04 / 05：两槽标题文字（图片字，这里用中文 Label 替代）。
            Label(_root, 343, 653, 80, 24, "角色信息");
            Label(_root, 918, 652, 80, 24, "角色信息");

            // img_role1 / img_role2：EXML (415,223) / (765,217)，立绘 source=img_carrer{career}_{sex}。
            for (int i = 0; i < 2; i++)
            {
                _slotModel[i] = new MirImageControl
                {
                    Parent = _root,
                    AutoSize = false,
                    NotControl = true,
                    Location = R(i == 0 ? 415 : 765, i == 0 ? 223 : 217),
                    // 图集就绪后 FitAtlasSizeDynamic 换成帧的真实尺寸（134x322 等），避免拉伸变形。
                    Size = Sz(150, 322),
                    Visible = false,
                };
                FitAtlasSizeDynamic(_slotModel[i]);

                // 动画层与静态立绘完全重合叠在上层：静态立绘始终保留，
                // 动画没播出来也不会把角色弄没。不在逐帧回调里改 Size ——
                // 那会每帧 Redraw 重烘焙场景，回调抛异常还会打断 CMain 渲染循环导致整屏 UI 消失。
                _slotAnim[i] = new MirAtlasAnimatedControl
                {
                    Parent = _root,
                    AutoSize = false,
                    NotControl = true,
                    Location = R(i == 0 ? 415 : 765, i == 0 ? 223 : 217),
                    Size = Sz(150, 322),
                    Visible = false,
                    Loop = true,
                    AnimationDelay = 250,   // 6 组 carrer*.json 实测 frameRate=4
                };
            }
            _modelBaseY = R(0, 223).Y;

            // _Label1..3 / txt_name0..c0（槽1），_Label4..6 / txt_name1..c1（槽2）。
            string[] fieldNames = { "名字", "等级", "职业" };
            for (int i = 0; i < 2; i++)
            {
                int cx = i == 0 ? 344 : 919;
                int vx = i == 0 ? 391 : 966;
                int[] cy = i == 0 ? new[] { 693, 720, 747 } : new[] { 692, 719, 746 };
                int[] vy = i == 0 ? new[] { 693, 720, 746 } : new[] { 692, 719, 745 };
                _slotCaption[i] = new MirLabel[3];
                _slotValue[i] = new MirLabel[3];
                for (int k = 0; k < 3; k++)
                {
                    _slotCaption[i][k] = Label(_root, cx, cy[k], 44, 24, fieldNames[k]);
                    _slotValue[i][k] = Label(_root, vx, vy[k], 110, 24, "");
                }
            }

            // 按钮群（EXML 坐标）。这些按钮在原版里坐标有部分重叠，由状态/可见性切换；
            // 本实现按角色数与创角面板状态显式控制，避免同时糊在一起。
            _chooseButton[0] = Button(_root, "btn_cjzy_01", "btn_cjzy_01_1", 393, 624, 132, 65, "选择角色");
            _chooseButton[0].Click += (o, e) => ChangeSlot(0);
            _chooseButton[1] = Button(_root, "btn_cjzy_02", "btn_cjzy_02_1", 970, 624, 132, 65, "选择角色");
            _chooseButton[1].Click += (o, e) => ChangeSlot(1);

            _startButton = Button(_root, "btn_kaishiyouxi_01", "btn_kaishiyouxi_01_1", 648, 628, 143, 62, "进入游戏");
            _startButton.Click += (o, e) => StartGame();

            _createButton = Button(_root, "btn_chuangjian_01", "btn_chuangjian_01_1", 550, 674, 132, 59, "创建角色");
            _createButton.Click += (o, e) => OpenCreate();

            _huifuButton = Button(_root, "btn_huifurenwu_01", "btn_huifurenwu_01_1", 661, 677, 119, 54, "恢复人物");
            _huifuButton.Click += (o, e) => ShowError("本服暂不支持恢复人物");

            _exitButton = Button(_root, "btn_fanhui_01", "btn_fanhui_01_1", 762, 678, 118, 52, "返回");
            _exitButton.Click += (o, e) => CMain.Instance.Close();

            _deleteButton = Button(_root, "btn_shanchurenwu_01", "btn_shanchurenwu_01_1", 649, 716, 132, 59, "删除角色");
            _deleteButton.Click += (o, e) => DeleteCharacter();

            // txt_limitTime (770,652) "(30)"
            _limitLabel = Label(_root, 770, 652, 100, 24, "");
            _errorLabel = Label(_root, 545, 590, 300, 28, "");
            _errorLabel.Visible = false;
            _lastAccessLabel = Label(_root, 24, 920, 700, 24, "");

            BuildCreatePanel();
        }

        // gameEui.json 里绝大多数 eui.Image / eui.Button 只有 x/y/source，没有 width/height，
        // 页游是「源图多大就画多大」。之前给每个控件硬编码 w/h 会把帧拉伸变形（立绘最明显）。
        // 图集异步加载完成后按帧的 SourceRectangle 重设 Size；Sz(w,h) 仅作加载前的占位/兜底。
        // 尺寸仍乘 ScaleF，保证与坐标、整体缩放一致。
        private static void FitAtlasSize(MirImageControl c, string frame)
        {
            EventHandler apply = (o, e) =>
            {
                if (c.IsDisposed || c.AtlasSheet == null || c.AtlasSheet.Frames == null) return;
                if (c.AtlasSheet.Frames.TryGetValue(frame, out var f))
                    c.Size = Sz(f.SourceRectangle.Width, f.SourceRectangle.Height);
            };
            c.AtlasLoaded += apply;
            if (c.AtlasSheet != null) apply(c, EventArgs.Empty);
        }

        // 立绘的帧名会随角色职业/性别变化（SetInfo 里重设 AtlasNameKey），故按当前帧名取尺寸。
        private static void FitAtlasSizeDynamic(MirImageControl c)
        {
            EventHandler apply = (o, e) =>
            {
                if (c.IsDisposed || c.AtlasSheet == null || c.AtlasSheet.Frames == null) return;
                string frame = c.AtlasFrame;
                if (!string.IsNullOrEmpty(frame) && c.AtlasSheet.Frames.TryGetValue(frame, out var f))
                    c.Size = Sz(f.SourceRectangle.Width, f.SourceRectangle.Height);
            };
            c.AtlasLoaded += apply;
            if (c.AtlasSheet != null) apply(c, EventArgs.Empty);
        }

        private static MirImageControl Image(MirControl parent, string key, int x, int y, int w, int h)
        {
            return new MirImageControl
            {
                Parent = parent,
                AutoSize = false,
                NotControl = true,
                ImageKey = key,
                Location = R(x, y),
                Size = Sz(w, h),
            };
        }

        private static MirImageControl AtlasImage(MirControl parent, string frame, int x, int y, int w, int h)
        {
            // EXML 里这些背景条明确写了 width/height（如 _Image8..13 是 120x20），
            // 页游是把 158x31 的 bg_cjzy_00 帧缩放显示；不要按帧原尺寸改 Size，
            // 否则底条撑大、字和背景的排布全乱。
            return new MirImageControl
            {
                Parent = parent,
                AutoSize = false,
                NotControl = true,
                AtlasNameKey = Atlas + "#" + frame,
                Location = R(x, y),
                Size = Sz(w, h),
            };
        }

        private static MirLabel Label(MirControl parent, int x, int y, int w, int h, string text)
        {
            return new MirLabel
            {
                Parent = parent,
                NotControl = true,
                Location = R(x, y),
                Size = Sz(w, h),
                Text = text,
                ForeColour = Color.White,
                DrawFormat = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter,
            };
        }

        // 按钮贴图不含文字（EXML 里靠 labelDisplay / 独立 txt_* 标签），补一层居中中文。
        private static MirButton Button(MirControl parent, string normal, string pressed,
            int x, int y, int w, int h, string caption)
        {
            MirButton b = new MirButton
            {
                Parent = parent,
                AutoSize = false,
                Location = R(x, y),
                Size = Sz(w, h),
                AtlasNameKey = Atlas + "#" + normal,
                HoverAtlasFrame = pressed,
                PressedAtlasFrame = pressed,
                Sound = SoundList.ButtonA,
            };
            FitAtlasSize(b, normal);
            if (!string.IsNullOrEmpty(caption))
            {
                MirLabel cap = Label(parent, x, y, w, h, caption);
                // 按钮真实尺寸/可见性变化时，文字层同步，避免文字错位或按钮隐藏后字还在。
                b.SizeChanged += (o, e) => { if (!cap.IsDisposed) cap.Size = b.Size; };
                b.VisibleChanged += (o, e) => { if (!cap.IsDisposed) cap.Visible = b.Visible; };
            }
            return b;
        }

        // create_grp (735,198) 322x428，子元素坐标全部相对该 Group，照抄 EXML。
        private void BuildCreatePanel()
        {
            _createPanel = new MirImageControl
            {
                Parent = _root,
                AutoSize = false,
                Location = R(735, 198),
                Size = Sz(322, 428),
                Visible = false,
            };

            Image(_createPanel, "resource/assets/gameui4/jpg/bg_cjzy_06.png", 0, 0, 322, 428);

            // _Image18/19/20 = word_cjzy_01/02/03（标题图片字）
            Label(_createPanel, 113, 41, 90, 27, "创建角色");
            Label(_createPanel, 113, 134, 90, 26, "选择职业");
            Label(_createPanel, 113, 248, 90, 27, "选择性别");

            // _Image17 输入框底 (69,77) 158x31；txt_input (78,80) 141x25。
            AtlasImage(_createPanel, "bg_cjzy_00", 69, 77, 158, 31);
            _nameBox = new MirTextBox
            {
                Parent = _createPanel,
                Location = R(78, 80),
                Size = Sz(141, 25),
                MaxLength = Globals.MaxCharacterNameLength,
            };

            // btn_random (206,70) 34x37；btn_close (258,22) 26x42。
            _randomNameButton = CreateButton("btn_cjzy_03", "btn_cjzy_03_1", 206, 70, 34, 37, "换");
            _randomNameButton.Click += (o, e) => { _nameBox.Text = RandomName(_newGender); ResetCountdown(); };

            _closeCreateButton = new MirButton
            {
                Parent = _createPanel,
                AutoSize = false,
                Location = R(258, 22),
                Size = Sz(26, 42),
                AtlasNameKey = "y_common-sheet#com_btn_close0",
                HoverAtlasFrame = "com_btn_close1",
                PressedAtlasFrame = "com_btn_close1",
                Sound = SoundList.ButtonA,
            };
            _closeCreateButton.Click += (o, e) => CloseCreate();

            // btn_carrer1/2/3 (46/116/188,162) 53x46；txt_carrer* (64/134/206,216)。
            string[] careers = { "战士", "法师", "道士" };
            for (int i = 0; i < 3; i++)
            {
                _classButtons[i] = CreateButton("icon_cjzy_0" + (i + 1), "icon_cjzy_0" + (i + 1) + "_1",
                    46 + i * 70, 162, 53, 46, "");
                Label(_createPanel, 64 + i * 70, 216, 53, 24, careers[i]);
                int idx = i;
                _classButtons[i].Click += (o, e) =>
                {
                    _newClass = (MirClass)idx;
                    RefreshCreateSelection();
                    ResetCountdown();
                };
            }

            // btn_sex1/2 (93/136,275/276) 52x44；txt_sex* (119/165,327)。
            string[] sexes = { "男", "女" };
            string[] sexFrames = { "icon_cjzy_06", "icon_cjzy_05" };
            for (int i = 0; i < 2; i++)
            {
                _sexButtons[i] = CreateButton(sexFrames[i], sexFrames[i] + "_1",
                    93 + i * 43, 275 + i, 52, 44, "");
                Label(_createPanel, 119 + i * 46, 327, 40, 24, sexes[i]);
                int idx = i;
                _sexButtons[i].Click += (o, e) =>
                {
                    _newGender = (MirGender)idx;
                    RefreshCreateSelection();
                    ResetCountdown();
                };
            }

            // btn_tj (96,358) 109x57
            _submitButton = CreateButton("btn_tijiao_01", "btn_tijiao_01_1", 96, 358, 109, 57, "创建");
            _submitButton.Click += (o, e) => SubmitCreate();

            SetCreateVisible(false);
        }

        // 用裸图集名 + 显式 AtlasFrame，异步加载完成时不会盖掉 RefreshCreateSelection 的选中帧。
        private MirButton CreateButton(string normal, string pressed, int x, int y, int w, int h, string caption)
        {
            MirButton b = new MirButton
            {
                Parent = _createPanel,
                AutoSize = false,
                Location = R(x, y),
                Size = Sz(w, h),
                AtlasNameKey = Atlas,
                AtlasFrame = normal,
                HoverAtlasFrame = pressed,
                PressedAtlasFrame = pressed,
                Sound = SoundList.ButtonA,
            };
            FitAtlasSize(b, normal);
            if (!string.IsNullOrEmpty(caption))
            {
                MirLabel cap = Label(_createPanel, x, y, w, h, caption);
                b.SizeChanged += (o, e) => { if (!cap.IsDisposed) cap.Size = b.Size; };
                b.VisibleChanged += (o, e) => { if (!cap.IsDisposed) cap.Visible = b.Visible; };
            }
            return b;
        }

        private void SetCreateVisible(bool visible)
        {
            _createPanel.Visible = visible;
            foreach (MirControl c in _createPanel.Controls) c.Visible = visible;
        }

        // ---- JS 逻辑移植 ----------------------------------------------------

        // JS setinfo()：0 角色直接展开创角面板；1/2 角色按数量显示两组信息标签。
        private void SetInfo()
        {
            int count = Math.Min(2, Characters.Count);
            if (_selected >= count) _selected = 0;

            for (int i = 0; i < 2; i++)
            {
                bool has = i < count;

                // JS setinfo() 只切 txt_name*/txt_level*/txt_c* 的可见性；
                // _Label1..6（"名字/等级/职业"）是独立字段标题，始终显示。
                // 之前把标题一起按 has 隐藏，槽一空连这些字都看不到了。
                for (int k = 0; k < 3; k++) _slotCaption[i][k].Visible = true;

                if (has)
                {
                    SelectInfo info = Characters[i];
                    _slotModel[i].AtlasNameKey = Atlas + "#" + ModelKey(info.Class, info.Gender);
                    _slotValue[i][0].Text = info.Name;
                    _slotValue[i][1].Text = info.Level.ToString();
                    _slotValue[i][2].Text = ClassName(info.Class);
                    for (int k = 0; k < 3; k++) _slotValue[i][k].Visible = true;
                    UpdateSlotAnimation(i, MovieKey(info.Class, info.Gender), true);
                }
                else
                {
                    for (int k = 0; k < 3; k++)
                    {
                        _slotValue[i][k].Text = "";
                        _slotValue[i][k].Visible = false;
                    }
                    UpdateSlotAnimation(i, null, false);
                }

                // 静态立绘始终显示：动画只是叠在上层的效果，绝不因动画就绪而把角色藏掉。
                _slotModel[i].Visible = has;

                // 未选中的槽变暗（页游 img_roleg* 的 getgray()）。
                // 只用 ForeColour 压暗，不动全局灰度状态/不透明度，避免影响整屏其它控件。
                bool dim = has && i != _selected;
                Color tint = dim ? Color.FromArgb(255, 130, 130, 130) : Color.White;
                _slotModel[i].ForeColour = tint;
                _slotAnim[i].ForeColour = tint;
            }

            // 槽底板固定用 EXML 的 _Image2/_Image3 两张图，不要按选中态换图：
            // 之前误用 bg_cjzy_06.png（那是 create_grp 的 322x428 面板背景）当高亮底，
            // 被拉伸到 334x392 后整个槽看起来尺寸/样式都不对。
            bool hasChar = count > 0;
            _startButton.Visible = hasChar;
            _startButton.Enabled = hasChar;
            _deleteButton.Visible = hasChar;
            _deleteButton.Enabled = hasChar;
            _chooseButton[0].Visible = count > 1;
            _chooseButton[1].Visible = count > 1;
            _createButton.Visible = count < 2;
            _createButton.Enabled = count < 2;
            _huifuButton.Visible = false;   // 本工程协议没有恢复人物
            _exitButton.Visible = true;

            _lastAccessLabel.Text = hasChar
                ? "上次登录：" + (Characters[_selected].LastAccess == DateTime.MinValue
                    ? "从未" : Characters[_selected].LastAccess.ToString("yyyy/MM/dd HH:mm:ss"))
                : "";
            if (Characters.Count > 2)
                _lastAccessLabel.Text += "（还有 " + (Characters.Count - 2) + " 个角色未显示：原版选人界面只有 2 个槽）";
        }

        private static string ModelKey(MirClass c, MirGender g)
        {
            int career = Math.Min(3, (int)c + 1);
            int sex = Math.Min(2, (int)g + 1);
            return "img_carrer" + career + "_" + sex;
        }

        // 页游 loadres()：RES.getResByUrl("img/carrer"+career+"_"+sex+".json")，
        // 在 CDN 上的实际 manifest key 是下面这条（两个版本 manifest 都有 6 组）。
        private static string MovieKey(MirClass c, MirGender g)
        {
            int career = Math.Min(3, (int)c + 1);
            int sex = Math.Min(2, (int)g + 1);
            return "resource/assets/gameui4/movieClip/carrer" + career + "_" + sex + ".json";
        }

        private void UpdateSlotAnimation(int slot, string key, bool active)
        {
            MirAtlasAnimatedControl anim = _slotAnim[slot];
            if (!active)
            {
                _slotAnimKey[slot] = null;
                anim.Animated = false;
                anim.Visible = false;
                return;
            }

            if (_slotAnimKey[slot] == key)
            {
                // 已加载过：SetInfo 重入（切槽/创建/删除）时恢复播放即可。
                bool ready = anim.AtlasSheet != null
                    && anim.AtlasAnimationFrames != null && anim.AtlasAnimationFrames.Count > 0;
                anim.Animated = ready;
                anim.Visible = ready;
                return;
            }

            _slotAnimKey[slot] = key;
            anim.Animated = false;
            anim.Visible = false;
            _ = LoadSlotAnimationAsync(slot, key);
        }

        private async Task LoadSlotAnimationAsync(int slot, string key)
        {
            try
            {
                // MovieClip（mc/res）图集：Mir2AtlasSheet 会解出 PlayOrder 时间线，
                // LoadSheetCoreAsync 用同名 png 补齐整页贴图（carrerX_Y.json -> carrerX_Y.png）。
                Mir2AtlasSheet sheet = await Mir2AtlasLibrary.GetSheetAsync(key, DXManager.GDevice);
                MirAtlasAnimatedControl anim = _slotAnim[slot];
                // 角色已切换或场景销毁：丢弃旧结果，避免动画盖到新角色上。
                if (anim.IsDisposed || _slotAnim[slot] != anim || _slotAnimKey[slot] != key) return;

                // mc 的 frameRate Mir2AtlasSheet 未暴露，补读一次 JSON（同一路径，命中缓存）。
                byte[] raw = null;
                try { raw = await Mir2OriginResource.GetBytesAsync(Mir2OriginResource.ResolveManifestAssetPath(key)); }
                catch { }
                ReadMovieMeta(raw, out long delay, out _, out _);
                anim.AnimationDelay = delay;
                // 动画层保持与静态立绘同坐标；不套用 mc.frames[].x/y，
                // 否则会在旁边多出一个错位的角色。

                anim.AtlasSheet = sheet;
                // 优先 mc 时间线；万一该资源是普通 frames 图集，退化为按帧名顺序播放。
                List<string> order = sheet.PlayOrder != null && sheet.PlayOrder.Count > 0
                    ? new List<string>(sheet.PlayOrder)
                    : (sheet.Frames != null ? new List<string>(sheet.Frames.Keys) : null);
                if (order == null || order.Count == 0) return;
                anim.AtlasAnimationFrames = order;
                anim.Animated = true;
                anim.Visible = true;
                // 静态立绘保留在下面，动画只是叠加效果，绝不隐藏。
            }
            catch (Exception ex)
            {
                KFramework.MonoGame.PrintTool.LogError("[SelectScene] 角色动画加载失败：" + key + " -> " + ex);
            }
        }

        // Egret MovieClip 首个时间线的 frameRate 与首帧绘制偏移（Mir2AtlasSheet 未暴露这两项）。
        // 实测 6 组 carrer*.json：frameRate=4、timeline=16 帧，首帧 x/y 因职业性别而异。
        private static void ReadMovieMeta(byte[] bytes, out long delay, out int x, out int y)
        {
            delay = 250;
            x = 0;
            y = 0;
            if (bytes == null || bytes.Length == 0) return;
            try
            {
                using (JsonDocument doc = JsonDocument.Parse(bytes))
                {
                    JsonElement root = doc.RootElement;
                    if (!root.TryGetProperty("mc", out JsonElement mc)) return;
                    foreach (JsonProperty timeline in mc.EnumerateObject())
                    {
                        if (timeline.Value.TryGetProperty("frameRate", out JsonElement fr)
                            && fr.TryGetInt32(out int fps) && fps > 0)
                            delay = 1000L / fps;
                        if (timeline.Value.TryGetProperty("frames", out JsonElement frames)
                            && frames.ValueKind == JsonValueKind.Array && frames.GetArrayLength() > 0)
                        {
                            JsonElement first = frames[0];
                            if (first.TryGetProperty("x", out JsonElement jx)) jx.TryGetInt32(out x);
                            if (first.TryGetProperty("y", out JsonElement jy)) jy.TryGetInt32(out y);
                        }
                        break;   // 与 Mir2AtlasSheet.Parse 一致：只取首个时间线
                    }
                }
            }
            catch { }
        }

        private static string ClassName(MirClass c)
        {
            switch (c)
            {
                case MirClass.Warrior: return "战士";
                case MirClass.Wizard: return "法师";
                case MirClass.Taoist: return "道士";
                case MirClass.Assassin: return "刺客";
                case MirClass.Archer: return "弓手";
                default: return "";
            }
        }

        private static string ToCDB(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            char[] chars = text.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                int c = chars[i];
                if (c >= 65281 && c <= 65374) chars[i] = (char)(c - 65248);
                else if (c == 12288) chars[i] = (char)32;
            }
            return new string(chars);
        }

        private static readonly string[] NamePrefix = { "风云", "烈焰", "苍月", "铁血", "逐日", "龙吟", "寒霜", "破军" };
        private static readonly string[] MaleSuffix = { "战神", "霸王", "狂刀", "天尊", "无双", "傲天" };
        private static readonly string[] FemaleSuffix = { "飞雪", "清影", "月华", "凝霜", "如烟", "灵儿" };

        private static string RandomName(MirGender gender)
        {
            string[] suffix = (int)gender == 0 ? MaleSuffix : FemaleSuffix;
            return NamePrefix[Rnd.Next(NamePrefix.Length)] + suffix[Rnd.Next(suffix.Length)];
        }

        private void ChangeSlot(int index)
        {
            if (index >= Math.Min(2, Characters.Count) || index == _selected) return;
            _selected = index;
            SetInfo();
            _slideSlot = index;
            _slideEnd = CMain.Time + SlideMilliseconds;
            _slideRunning = true;
            ResetCountdown();
        }

        private void OpenCreate()
        {
            if (Characters.Count >= 2) { ShowError("角色已满"); return; }

            // JS createrole(): create_grp.x = 376（已有角色1）/ 735（空号）
            _createPanel.Location = R(Characters.Count > 0 ? 376 : 735, 198);
            SetCreateVisible(true);
            _newClass = (MirClass)Rnd.Next(0, 3);
            _newGender = 0;
            _nameBox.Text = RandomName(_newGender);
            _nameBox.SetFocus();
            _submitButton.Enabled = true;
            RefreshCreateSelection();
            ResetCountdown();
        }

        private void CloseCreate()
        {
            SetCreateVisible(false);
            _submitButton.Enabled = false;
            ResetCountdown();
        }

        private void RefreshCreateSelection()
        {
            string[] careers = { "icon_cjzy_01", "icon_cjzy_02", "icon_cjzy_03" };
            string[] sexes = { "icon_cjzy_06", "icon_cjzy_05" };
            for (int i = 0; i < 3; i++)
                _classButtons[i].AtlasFrame = i == (int)_newClass ? careers[i] + "_1" : careers[i];
            for (int i = 0; i < 2; i++)
                _sexButtons[i].AtlasFrame = i == (int)_newGender ? sexes[i] + "_1" : sexes[i];
        }

        private void SubmitCreate()
        {
            string name = ToCDB(_nameBox.Text);
            _nameBox.Text = name;
            if (name.Length < Globals.MinCharacterNameLength || name.Length > Globals.MaxCharacterNameLength)
            {
                ShowError("角色名长度不符合要求");
                return;
            }
            _submitButton.Enabled = false;
            Network.Enqueue(new C.NewCharacter { Name = name, Class = _newClass, Gender = _newGender });
            ResetCountdown();
        }

        private void StartGame()
        {
            if (_createPanel.Visible) { SubmitCreate(); return; }
            if (_selected < 0 || _selected >= Characters.Count) return;
            if (!Libraries.Loaded) { ShowError("资源加载中，请稍候"); ResetCountdown(); return; }
            _startButton.Enabled = false;
            Network.Enqueue(new C.StartGame { CharacterIndex = Characters[_selected].Index });
        }

        private void DeleteCharacter()
        {
            if (_selected < 0 || _selected >= Characters.Count) return;
            MirMessageBox message = new MirMessageBox("确定删除角色 " + Characters[_selected].Name + "？", MirMessageBoxButtons.YesNo);
            int index = Characters[_selected].Index;
            string name = Characters[_selected].Name;
            message.YesButton.Click += (o1, e1) =>
            {
                MirInputBox inputBox = new MirInputBox("请输入角色名确认");
                inputBox.OKButton.Click += (o, e) =>
                {
                    if (inputBox.InputTextBox.Text == name)
                    {
                        _deleteButton.Enabled = false;
                        Network.Enqueue(new C.DeleteCharacter { CharacterIndex = index });
                    }
                    else MirMessageBox.Show("输入不正确");
                    inputBox.Dispose();
                };
                inputBox.Show();
            };
            message.Show();
        }

        public void SortList()
        {
            if (Characters != null) Characters.Sort((c1, c2) => c2.LastAccess.CompareTo(c1.LastAccess));
        }

        private void ResetCountdown() { _countdownEnd = CMain.Time + InitTimerSeconds * 1000L; }

        private void ShowError(string text)
        {
            _errorLabel.Text = text;
            _errorLabel.Visible = true;
            _errorUntil = CMain.Time + 2000;
        }

        private void OnKeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyCode != Keys.Enter) return;
            if (_createPanel.Visible) SubmitCreate();
            else if (_startButton.Enabled) StartGame();
            e.Handled = true;
        }

        public override void Process()
        {
            long now = CMain.Time;
            if (_countdownEnd > 0)
            {
                long left = _countdownEnd - now;
                if (left <= 0)
                {
                    _countdownEnd = 0;
                    _limitLabel.Text = "";
                    if (_createPanel.Visible) SubmitCreate();
                    else if (_startButton.Enabled) StartGame();
                }
                else _limitLabel.Text = "（" + (int)Math.Ceiling(left / 1000M) + "）";
            }
            if (_errorLabel.Visible && now >= _errorUntil) _errorLabel.Visible = false;

            if (_slideRunning)
            {
                float t = 1f - (_slideEnd - now) / (float)SlideMilliseconds;
                if (t >= 1f) { t = 1f; _slideRunning = false; }
                t = Math.Max(0f, t);
                int dy = (int)(360 * (1f - t) * ScaleF);
                MirImageControl model = _slotModel[_slideSlot];
                model.Location = new Point(model.Location.X, _modelBaseY + dy);
            }
        }

        // ---- 本工程网络包（协议不变）--------------------------------------

        public override void ProcessPacket(Packet p)
        {
            switch (p.Index)
            {
                case (short)ServerPacketIds.NewCharacter: NewCharacter((S.NewCharacter)p); break;
                case (short)ServerPacketIds.NewCharacterSuccess: NewCharacter((S.NewCharacterSuccess)p); break;
                case (short)ServerPacketIds.DeleteCharacter: DeleteCharacter((S.DeleteCharacter)p); break;
                case (short)ServerPacketIds.DeleteCharacterSuccess: DeleteCharacter((S.DeleteCharacterSuccess)p); break;
                case (short)ServerPacketIds.StartGame: StartGame((S.StartGame)p); break;
                case (short)ServerPacketIds.StartGameBanned: StartGame((S.StartGameBanned)p); break;
                case (short)ServerPacketIds.StartGameDelay: StartGame((S.StartGameDelay)p); break;
                default: base.ProcessPacket(p); break;
            }
        }

        private void NewCharacter(S.NewCharacter p)
        {
            _submitButton.Enabled = true;
            switch (p.Result)
            {
                case 0: ShowError("当前禁止创建角色"); CloseCreate(); break;
                case 1: ShowError("角色名不可用"); _nameBox.SetFocus(); break;
                case 2: ShowError("性别无效"); break;
                case 3: ShowError("职业无效"); break;
                case 4: ShowError("角色数量已达上限"); CloseCreate(); break;
                case 5: ShowError("角色名已存在"); _nameBox.SetFocus(); break;
            }
        }

        private void NewCharacter(S.NewCharacterSuccess p)
        {
            CloseCreate();
            MirMessageBox.Show("角色创建成功");
            Characters.Insert(0, p.CharInfo);
            _selected = 0;
            SetInfo();
            ResetCountdown();
        }

        private void DeleteCharacter(S.DeleteCharacter p)
        {
            _deleteButton.Enabled = true;
            if (p.Result == 0) ShowError("当前禁止删除角色");
            else if (p.Result == 1) ShowError("所选角色不存在");
        }

        private void DeleteCharacter(S.DeleteCharacterSuccess p)
        {
            _deleteButton.Enabled = true;
            MirMessageBox.Show("角色删除成功");
            for (int i = 0; i < Characters.Count; i++)
                if (Characters[i].Index == p.CharacterIndex) { Characters.RemoveAt(i); break; }
            SetInfo();
            ResetCountdown();
        }

        private void StartGame(S.StartGameDelay p)
        {
            _startButton.Enabled = true;
            long time = CMain.Time + p.Milliseconds;
            MirMessageBox message = new MirMessageBox("请稍候后再进入游戏", MirMessageBoxButtons.OK);
            message.AfterDraw += (o, e) => { if (CMain.Time > time) { message.Dispose(); StartGame(); } };
            message.Show();
        }

        private void StartGame(S.StartGameBanned p)
        {
            _startButton.Enabled = true;
            MirMessageBox.Show("账号被封禁：" + p.Reason + "，到期 " + p.ExpiryDate);
        }

        private void StartGame(S.StartGame p)
        {
            _startButton.Enabled = true;
            switch (p.Result)
            {
                case 0: ShowError("当前禁止进入游戏"); break;
                case 1: ShowError("尚未登录"); break;
                case 2: ShowError("角色不存在"); break;
                case 3: ShowError("未找到有效地图或出生点"); break;
                case 4: ActiveScene = new GameScene(); Dispose(); break;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _root = null;
                _mainBg = null;
                _startButton = null;
                _createButton = null;
                _deleteButton = null;
                _exitButton = null;
                _huifuButton = null;
                _limitLabel = null;
                _errorLabel = null;
                _qufuLabel = null;
                _lastAccessLabel = null;
                _createPanel = null;
                _nameBox = null;
                _submitButton = null;
                _randomNameButton = null;
                _closeCreateButton = null;
                _slideRunning = false;
                _countdownEnd = 0;
                Characters = null;
            }
            base.Dispose(disposing);
        }
    }
}
