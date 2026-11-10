using System;
using System.Collections.Generic;
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
    // 《原始传奇》选人界面移植（fugu_20201203），直接覆盖旧 Mir2 选人场景。
    //
    // 版式与交互一律以 qufu1.min.js 里 CreateRoleUICtrl3 / CreateRoleUICtrl4 的「运行几何」为准，
    // 不用 gameEui.json / default.thm.json 的皮肤快照（那批 exml 是构建期旧快照，实测与实际 UI 不匹配）：
    //   createmovieCreate : _createClip.x=415 / 778, y=233      -> 两个角色槽中心，间距 363
    //   startmovieCreate  : _startClip.x=658, y=630              -> 开始游戏按钮中心
    //   createrole        : create_grp.x=376（已有角色1）/ 735   -> 创角面板落点
    //   showmodel         : maskShape.drawRect(0,0,300,360)      -> 角色立绘区 300x360
    //   movieCreate       : img_model0.x=30,y=-60                -> 立绘相对槽的偏移
    //   changecarrer      : maskShape.y 223 -> -137, 1500ms      -> 换槽滑入动画
    //   createChildren    : INIT_TIMER=30 / START_TIMER          -> 倒计时自动创建或进入
    //   setinfo           : roleName1/roleName2 决定 0/1/2 个槽
    //   onChangeUIType(1) : 职业/性别切换并高亮、刷新立绘
    //   ToCDB             : 全角字符转半角（名字规范化）
    //   getcarrer         : 1战士 2法师 3道士
    //
    // 美术取自 allmanifest10.25341.1.json + y_createrole-sheet_json：
    //   bg_cjzy_02.jpg(1390x860) 主背景、bg_cjzy_03/06.png 槽底、
    //   img_carrer{1..3}_{1..2} 立绘、btn_kaishiyouxi/btn_chuangjian/btn_shanchurenwu/btn_fanhui/
    //   btn_tijiao/btn_cjzy_01 按钮、bg_cjzy_01_2 创角面板底。
    //   注意：页游按钮贴图本身不含文字（文字是 EXML 里单独叠的 eui.Label），
    //   所以本实现给每个按钮补一层居中中文 MirLabel，否则就是一排没字的色块。
    //
    // 网络协议完全用本工程 C.* / S.*，不照搬页游 QufuMessageHandler 的 VarInt 协议：
    //   进入游戏 C.StartGame{CharacterIndex}，创建 C.NewCharacter{Name,Class,Gender}，
    //   删除 C.DeleteCharacter{CharacterIndex}。
    public sealed class SelectScene : MirScene
    {
        // 主背景尺寸即设计坐标系；整块 UI 等比缩放后居中，对应 JS onResize 的 (stageW-skinW)>>1。
        private const int DesignW = 1390, DesignH = 860;
        private const int InitTimerSeconds = 30;
        private const int SlideMilliseconds = 1500;

        private const string Atlas = "y_createrole-sheet";
        private static readonly Random Rnd = new Random();
        private static readonly int[] SlotCenterX = { 415, 778 };

        private static float ScaleF
        {
            get { return Math.Min(Settings.ScreenWidth / (float)DesignW, Settings.ScreenHeight / (float)DesignH); }
        }

        // 屏幕绝对坐标（用于根容器）。
        private static Point P(int x, int y)
        {
            float s = ScaleF;
            return new Point(
                (int)((Settings.ScreenWidth - DesignW * s) / 2 + x * s),
                (int)((Settings.ScreenHeight - DesignH * s) / 2 + y * s));
        }

        // 容器内部的相对坐标。
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

        private MirImageControl _root;
        private readonly MirImageControl[] _slotBg = new MirImageControl[2];
        private readonly MirImageControl[] _slotModel = new MirImageControl[2];
        private readonly MirLabel[] _slotName = new MirLabel[2];
        private readonly MirLabel[] _slotInfo = new MirLabel[2];
        private MirButton _startButton, _createButton, _deleteButton, _exitButton;
        private MirLabel _countdownLabel, _errorLabel, _serverLabel, _lastAccessLabel;

        // 内嵌创角面板，对应 JS 的 create_grp（不是模态对话框）。
        private MirImageControl _createPanel;
        private MirImageControl _createModel;
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
            _root = new MirImageControl
            {
                Parent = this,
                AutoSize = false,
                NotControl = true,
                Location = P(0, 0),
                Size = Sz(DesignW, DesignH),
                ImageKey = "resource/assets/gameui4/jpg/bg_cjzy_02.jpg",
            };

            _serverLabel = new MirLabel
            {
                Parent = _root,
                NotControl = true,
                Location = R(24, 18),
                Size = Sz(400, 28),
                Text = "原始传奇",
                ForeColour = Color.White,
            };

            // 两个角色槽：中心 415 / 778（JS _createClip）。暗色底 bg_cjzy_03，选中改用亮龙纹 bg_cjzy_06。
            for (int i = 0; i < 2; i++)
            {
                int x = SlotCenterX[i];

                _slotBg[i] = new MirImageControl
                {
                    Parent = _root,
                    AutoSize = false,
                    NotControl = true,
                    Location = R(x - 167, 120),
                    Size = Sz(334, 392),
                    ImageKey = "resource/assets/gameui4/jpg/bg_cjzy_03.png",
                };

                // 立绘：JS movieCreate 用 img_carrer{career}_{sex}，300x360 遮罩。
                _slotModel[i] = new MirImageControl
                {
                    Parent = _root,
                    AutoSize = false,
                    NotControl = true,
                    Location = R(x - 150, 140),
                    Size = Sz(300, 360),
                    Visible = false,
                };

                _slotName[i] = MakeLabel(_root, x - 120, 505, 240, 24, "");
                _slotInfo[i] = MakeLabel(_root, x - 120, 529, 240, 24, "");

                // 透明命中层：MirButton 无 Library/Atlas 时什么都不画，只提供点击区。
                int idx = i;
                MirButton hit = new MirButton
                {
                    Parent = _root,
                    AutoSize = false,
                    Location = R(x - 167, 120),
                    Size = Sz(334, 392),
                    Sound = SoundList.ButtonA,
                };
                hit.Click += (o, e) => ChangeSlot(idx);
            }

            _modelBaseY = R(0, 140).Y;

            // 主界面四个按钮统一排在底部一行，尺寸严格按图集帧原始大小，不再东一个西一个。
            // 开始游戏中心对齐 JS startbtn_eff @ (658,630)；其余三个等距排在下一行。
            _startButton = MakeButton("btn_kaishiyouxi_01", "btn_kaishiyouxi_01_1", 587, 590, 143, 62, "进入游戏");
            _startButton.Click += (o, e) => StartGame();

            _createButton = MakeButton("btn_chuangjian_01", "btn_chuangjian_01_1", 300, 690, 132, 59, "创建角色");
            _createButton.Click += (o, e) => OpenCreate();

            _deleteButton = MakeButton("btn_shanchurenwu_01", "btn_shanchurenwu_01_1", 460, 690, 132, 59, "删除角色");
            _deleteButton.Click += (o, e) => DeleteCharacter();

            _exitButton = MakeButton("btn_fanhui_01", "btn_fanhui_01_1", 620, 690, 118, 52, "返回");
            _exitButton.Click += (o, e) => CMain.Instance.Close();

            _countdownLabel = MakeLabel(_root, 568, 665, 180, 26, "");
            _errorLabel = MakeLabel(_root, 508, 548, 300, 28, "");
            _errorLabel.Visible = false;
            _lastAccessLabel = MakeLabel(_root, 24, 810, 600, 24, "");

            BuildCreatePanel();
        }

        private static MirLabel MakeLabel(MirControl parent, int x, int y, int w, int h, string text)
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

        // 按钮贴图不含文字，统一补一层居中文字标签；标签 NotControl，点击仍落到按钮上。
        private static void AddCaption(MirControl parent, int x, int y, int w, int h, string caption)
        {
            new MirLabel
            {
                Parent = parent,
                NotControl = true,
                Location = R(x, y),
                Size = Sz(w, h),
                Text = caption,
                ForeColour = Color.White,
                DrawFormat = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter,
            };
        }

        private static MirButton MakeButtonCore(MirControl parent, string normal, string pressed,
            int x, int y, int w, int h, string caption)
        {
            MirButton button = new MirButton
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
            AddCaption(parent, x, y, w, h, caption);
            return button;
        }

        private MirButton MakeButton(string normal, string pressed, int x, int y, int w, int h, string caption)
        {
            return MakeButtonCore(_root, normal, pressed, x, y, w, h, caption);
        }

        // 创角面板：JS create_grp，x=376（已有角色1时开在槽2）/ 735（空号时）。
        // 面板内按钮统一用 btn_cjzy_01(132x65) 帧 + 中文标题，等宽等高排整齐，
        // 不再用 icon_cjzy_* 那种 53x46 小图标当按钮（挤在一起会显得杂乱）。
        private void BuildCreatePanel()
        {
            _createPanel = new MirImageControl
            {
                Parent = _root,
                AutoSize = false,
                Location = R(735, 240),
                Size = Sz(347, 360),
                AtlasNameKey = Atlas + "#bg_cjzy_01_2",
                DrawMode = MirImageDrawMode.NineGrid,
                BorderLeft = 20, BorderTop = 20, BorderRight = 20, BorderBottom = 20,
                Visible = false,
            };

            MakeLabel(_createPanel, 20, 10, 260, 26, "创建角色");

            _createModel = new MirImageControl
            {
                Parent = _createPanel,
                AutoSize = false,
                NotControl = true,
                Location = R(100, 40),
                Size = Sz(150, 170),
            };

            _nameBox = new MirTextBox
            {
                Parent = _createPanel,
                Location = R(20, 220),
                Size = Sz(200, 24),
                MaxLength = Globals.MaxCharacterNameLength,
            };

            _randomNameButton = MakeSmallButton("btn_cjzy_01", "btn_cjzy_01_1", 230, 215, 100, 34, "随机名");
            _randomNameButton.Click += (o, e) =>
            {
                _nameBox.Text = RandomName(_newGender);
                ResetCountdown();
            };

            string[] careers = { "战士", "法师", "道士" };
            for (int i = 0; i < 3; i++)
            {
                _classButtons[i] = MakeSmallButton("btn_cjzy_01", "btn_cjzy_01_1",
                    20 + i * 105, 255, 90, 40, careers[i]);
                int idx = i;
                _classButtons[i].Click += (o, e) =>
                {
                    _newClass = (MirClass)idx;
                    RefreshCreateSelection();
                    ResetCountdown();
                };
            }

            string[] sexes = { "男", "女" };
            for (int i = 0; i < 2; i++)
            {
                _sexButtons[i] = MakeSmallButton("btn_cjzy_01", "btn_cjzy_01_1",
                    20 + i * 105, 305, 90, 40, sexes[i]);
                int idx = i;
                _sexButtons[i].Click += (o, e) =>
                {
                    _newGender = (MirGender)idx;
                    RefreshCreateSelection();
                    ResetCountdown();
                };
            }

            _submitButton = MakeSmallButton("btn_tijiao_01", "btn_tijiao_01_1", 215, 305, 120, 40, "创建角色");
            _submitButton.Click += (o, e) => SubmitCreate();

            _closeCreateButton = MakeSmallButton("btn_cjzy_03", "btn_cjzy_03_1", 295, 10, 40, 30, "×");
            _closeCreateButton.Click += (o, e) => CloseCreate();

            SetCreateVisible(false);
        }

        // 用裸图集名 + 显式 AtlasFrame：异步加载完成时不会覆盖 RefreshCreateSelection 设置的选中帧。
        private MirButton MakeSmallButton(string normal, string pressed, int x, int y, int w, int h, string caption)
        {
            MirButton button = new MirButton
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
            AddCaption(_createPanel, x, y, w, h, caption);
            return button;
        }

        // 面板隐藏时连子控件一起关掉，避免输入框仍抢焦点 / 按钮仍参与命中。
        private void SetCreateVisible(bool visible)
        {
            _createPanel.Visible = visible;
            _createModel.Visible = visible;
            _nameBox.Visible = visible;
            for (int i = 0; i < 3; i++) _classButtons[i].Visible = visible;
            for (int i = 0; i < 2; i++) _sexButtons[i].Visible = visible;
            _submitButton.Visible = visible;
            _randomNameButton.Visible = visible;
            _closeCreateButton.Visible = visible;
        }

        // ---- JS 逻辑移植 ----------------------------------------------------

        // JS setinfo()：按已有角色数填充两个槽，默认选中最后登录（本工程按 LastAccess 排序后的第一个）。
        private void SetInfo()
        {
            for (int i = 0; i < 2; i++)
            {
                bool has = i < Characters.Count;
                _slotModel[i].Visible = has;
                _slotName[i].Visible = has;
                _slotInfo[i].Visible = has;

                if (has)
                {
                    SelectInfo info = Characters[i];
                    _slotModel[i].AtlasNameKey = Atlas + "#" + ModelKey(info.Class, info.Gender);
                    _slotName[i].Text = info.Name;
                    _slotInfo[i].Text = ClassName(info.Class) + "  等级 " + info.Level;
                }
                else
                {
                    _slotName[i].Text = "";
                    _slotInfo[i].Text = "";
                }
            }

            if (_selected >= Characters.Count) _selected = 0;

            // 选中槽用亮龙纹底，未选用暗底（对应 JS img_roleg.filters=getgray 的反义表达）。
            for (int i = 0; i < 2; i++)
                _slotBg[i].ImageKey = i == _selected && _selected < Characters.Count
                    ? "resource/assets/gameui4/jpg/bg_cjzy_06.png"
                    : "resource/assets/gameui4/jpg/bg_cjzy_03.png";

            _startButton.Enabled = Characters.Count > 0 && _selected < Characters.Count;
            _deleteButton.Enabled = _startButton.Enabled;
            _createButton.Enabled = Characters.Count < 2;

            if (Characters.Count > 0 && _selected < Characters.Count)
            {
                DateTime last = Characters[_selected].LastAccess;
                _lastAccessLabel.Text = last == DateTime.MinValue
                    ? "上次登录：从未"
                    : "上次登录：" + last.ToString("yyyy/MM/dd HH:mm:ss");
            }
            else
            {
                _lastAccessLabel.Text = "";
            }

            // 页游只有两个角色槽；若本工程服务端返回更多角色，明确提示而不是静默藏起来。
            if (Characters.Count > 2)
                _lastAccessLabel.Text += "（还有 " + (Characters.Count - 2) + " 个角色未显示：原版选人界面只有 2 个槽）";
        }

        private static string ModelKey(MirClass c, MirGender g)
        {
            // JS career 1..3、sex 1..2；本工程枚举从 0 起，超出的职业（刺客/弓手）回落到战士立绘。
            int career = Math.Min(3, (int)c + 1);
            int sex = Math.Min(2, (int)g + 1);
            return "img_carrer" + career + "_" + sex;
        }

        // JS getcarrer()
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

        // JS ToCDB()：全角转半角，名字规范化后再发包。
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

        // 页游随机名走 CMDReqRandomNameMessage（本工程协议里没有），改为本地按性别生成，
        // 语义与 JS updateName() 一致：把随机名填进 txt_input。
        private static readonly string[] NamePrefix = { "风云", "烈焰", "苍月", "铁血", "逐日", "龙吟", "寒霜", "破军" };
        private static readonly string[] MaleSuffix = { "战神", "霸王", "狂刀", "天尊", "无双", "傲天" };
        private static readonly string[] FemaleSuffix = { "飞雪", "清影", "月华", "凝霜", "如烟", "灵儿" };

        private static string RandomName(MirGender gender)
        {
            string[] suffix = (int)gender == 0 ? MaleSuffix : FemaleSuffix;
            return NamePrefix[Rnd.Next(NamePrefix.Length)] + suffix[Rnd.Next(suffix.Length)];
        }

        // JS changecarrer()：换槽 + 1500ms 滑入（原版 maskShape.y 223 -> -137，位移 360）。
        private void ChangeSlot(int index)
        {
            if (index >= Characters.Count || index == _selected) return;

            _selected = index;
            SetInfo();

            _slideSlot = index;
            _slideEnd = CMain.Time + SlideMilliseconds;
            _slideRunning = true;
            ResetCountdown();
        }

        // JS createrole()：打开内嵌创角面板，随机职业、性别归位、给一个随机名。
        private void OpenCreate()
        {
            if (Characters.Count >= 2) { ShowError("角色已满"); return; }

            _createPanel.Location = R(Characters.Count > 0 ? 376 : 735, 240);
            SetCreateVisible(true);
            _newClass = (MirClass)Rnd.Next(0, 3); // JS randomcarrer()
            _newGender = 0;                       // JS nowsex = 1
            _nameBox.Text = RandomName(_newGender); // JS sendReqRandomNameMessage + updateName
            _nameBox.SetFocus();
            _submitButton.Enabled = true;
            RefreshCreateSelection();
            ResetCountdown();
        }

        // JS closecreate()
        private void CloseCreate()
        {
            SetCreateVisible(false);
            _submitButton.Enabled = false;
            ResetCountdown();
        }

        private void RefreshCreateSelection()
        {
            for (int i = 0; i < 3; i++)
                _classButtons[i].AtlasFrame = i == (int)_newClass ? "btn_cjzy_01_1" : "btn_cjzy_01";
            for (int i = 0; i < 2; i++)
                _sexButtons[i].AtlasFrame = i == (int)_newGender ? "btn_cjzy_01_1" : "btn_cjzy_01";

            _createModel.AtlasNameKey = Atlas + "#" + ModelKey(_newClass, _newGender);
        }

        // JS startgame() / putcreaterole()：名字规范化后发创建包；不照搬 VarInt 协议。
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
            Network.Enqueue(new C.NewCharacter
            {
                Name = name,
                Class = _newClass,
                Gender = _newGender,
            });
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
                    else
                    {
                        MirMessageBox.Show("输入不正确");
                    }
                    inputBox.Dispose();
                };
                inputBox.Show();
            };
            message.Show();
        }

        public void SortList()
        {
            if (Characters != null)
                Characters.Sort((c1, c2) => c2.LastAccess.CompareTo(c1.LastAccess));
        }

        // JS setTimeLimit()：30 秒倒计时，超时自动提交创建或进入游戏。
        private void ResetCountdown()
        {
            _countdownEnd = CMain.Time + InitTimerSeconds * 1000L;
        }

        private void ShowError(string text)
        {
            _errorLabel.Text = text;
            _errorLabel.Visible = true;
            _errorUntil = CMain.Time + 2000; // JS errtips 的 2e3
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
                    _countdownLabel.Text = "";
                    if (_createPanel.Visible) SubmitCreate();
                    else if (_startButton.Enabled) StartGame();
                }
                else
                {
                    _countdownLabel.Text = "（" + (int)Math.Ceiling(left / 1000M) + "）";
                }
            }

            if (_errorLabel.Visible && now >= _errorUntil)
                _errorLabel.Visible = false;

            // 换槽滑入动画：1500ms 内立绘从 +360 滑回原位。
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
            {
                if (Characters[i].Index == p.CharacterIndex)
                {
                    Characters.RemoveAt(i);
                    break;
                }
            }
            SetInfo();
            ResetCountdown();
        }

        private void StartGame(S.StartGameDelay p)
        {
            _startButton.Enabled = true;
            long time = CMain.Time + p.Milliseconds;
            MirMessageBox message = new MirMessageBox("请稍候后再进入游戏", MirMessageBoxButtons.OK);
            message.AfterDraw += (o, e) =>
            {
                if (CMain.Time <= time) return;
                message.Dispose();
                StartGame();
            };
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
                case 4:
                    ActiveScene = new GameScene();
                    Dispose();
                    break;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _root = null;
                _startButton = null;
                _createButton = null;
                _deleteButton = null;
                _exitButton = null;
                _countdownLabel = null;
                _errorLabel = null;
                _serverLabel = null;
                _lastAccessLabel = null;
                _createPanel = null;
                _createModel = null;
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
