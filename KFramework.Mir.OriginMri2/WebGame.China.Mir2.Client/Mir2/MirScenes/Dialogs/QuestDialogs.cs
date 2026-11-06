using Client.MirControls;
using Client.MirGraphics;
using Client.MirNetwork;
using Client.MirObjects;
using Client.MirSounds;
using KFramework.MonoGameExtend;
using MirEngine;
using System.Text.RegularExpressions;
using C = ClientPackets;

namespace Client.MirScenes.Dialogs
{
    public sealed class QuestListDialog : MirImageControl
    {
        private MirLabel _npcNameLabel, _descLabel, _PlayerTaskTalk;
        private List<MirLabel> _descSegments = new List<MirLabel>(); // 描述渲染出的彩色/链接覆盖层（含每行底色标签），刷新时整体释放

        public List<ClientQuestProgress> Quests = new List<ClientQuestProgress>();

        public int SelectedIndex = 0;

        public ClientQuestProgress SelectedQuest;
        public QuestMessage Message;
        public QuestRewards Reward;

        public QuestRow[] Rows = new QuestRow[0];

        public int StartIndex = 0;

        public uint CurrentNPCID = 0;

        // 任务面板内各控件相对背景的统一左缩进，调这个值即可整体左右平移（默认 20）
        private const int PanelIndent = 20;

        public QuestListDialog()
        {
            Movable = false;
            Sort = true;
            AutoSize = false;
            Size = new Size(600, 180);
            Location = new Point(0, 0);
            //Texture = KDefaultRes.DefaultTexture2D;

            MirImageControl BG = new MirImageControl
            {
                Parent = this,
                Location = new Point(0, 0), //Y = 9
                ImageKey = "resource/assets/gameui4/jpg/y_task_bg.png"
            }; 

            // 精简后的任务详情：第一行 NPC 名，第二行 任务描述，末行 “Finish” 下划线黄字（点此交任务）。
            // 隐藏原本冗长的滚动文本与滚动条。
            //Message.Visible = false;
            _npcNameLabel = new MirLabel
            {
                Parent = this,
                AutoSize = true,
                Location = new Point(PanelIndent, 10),
                Font = new Font(Settings.FontName, 12F, KFramework.MonoGame.FontStyle.Bold),
                ForeColour = Color.Yellow,
            };

            _descLabel = new MirLabel
            {
                Parent = this,
                Location = new Point(PanelIndent, 34),
                Size = new Size(500, 130),
                DrawFormat = TextFormatFlags.WordBreak,
                ForeColour = Color.White,
            };

            Reward = new QuestRewards
            {
                Parent = this,
                Visible = false,
                Size = new Size(313, 130),
                Location = new Point(PanelIndent, 120)
            };

            _PlayerTaskTalk = new MirLabel
            {
                Parent = this,
                AutoSize = true,
                Location = new Point(PanelIndent, 145),
                Text = "Finish",
                ForeColour = Color.Yellow,
                UnderLine = true,
                Sound = SoundList.ButtonA,
                NotControl = false,
                Visible = false,
            };
            _PlayerTaskTalk.Click += (o, e) =>
            {
                if (SelectedQuest == null || SelectedQuest.QuestInfo == null) return;

                if (!SelectedQuest.Taken)
                {
                    // 尚未接取：在 NPC 旁点击“接取任务”（玩家已通过“寻找NPC”传送至该 NPC 范围内）
                    Network.Enqueue(new C.AcceptQuest { QuestIndex = SelectedQuest.QuestInfo.Index, Navigate = false });
                }
                else if (SelectedQuest.Completed)
                {
                    Network.Enqueue(new C.FinishQuest { QuestIndex = SelectedQuest.QuestInfo.Index });
                }
            };

            MirButton closeButton = new MirButton
            {
                AtlasNameKey = "y_common-sheet",
                AtlasFrame = "com_btn_close0",
                PressedAtlasFrame = "com_btn_close1",
                Parent = this,
                Location = new Point(546, 3),
                Sound = SoundList.ButtonA,
            };
            closeButton.Click += (o, e) => Hide();
        }

        public override void Show()
        {
            if (Visible) return;
            Visible = true;

            CurrentNPCID = GameScene.NPCID;

            Reset();

            DisplayInfo();
            GameScene.Scene.QuestTrackingDialog.SyncWithNpcPanels();
        }

        public override void Hide()
        {
            if (!Visible) return;
            Visible = false;

            GameScene.Scene.NPCDialog.Hide();
            GameScene.Scene.QuestTrackingDialog.SyncWithNpcPanels();
        }

        public void Toggle()
        {
            if (!Visible)
                Show();
            else
                Hide();
        }

        public void DisplayInfo()
        {
            if (!GetAvailableQuests())
            {
                Hide();
                return;
            }
            Reset();
            RefreshInterface();
        }

        public void Reset()
        {
            StartIndex = 0;
            SelectedIndex = 0;
            SelectedQuest = null;

            //Message.Visible = false;
            //Reward.Visible = false;

            for (int i = 0; i < Rows.Length; i++)
            {
                if(Rows[i] != null) Rows[i].Dispose();

                Rows[i] = null;
            }
        }

        public bool GetAvailableQuests()
        {
            NPCObject npc = (NPCObject)MapControl.GetObject(CurrentNPCID);
            if (npc != null)
            {
                Quests = npc.GetAvailableQuests();
            }

            return Quests.Count >= 1 && npc != null;
        }

        public void RefreshInterface()
        {
            int maxIndex = Quests.Count - Rows.Length;

            if (StartIndex > maxIndex) StartIndex = maxIndex;
            if (StartIndex < 0) StartIndex = 0;

            // 优先显示「当前任务」（已接取），否则显示可接取的悬赏任务（如从“寻找NPC”打开面板时尚未领取）
            ClientQuestProgress current = Quests.FirstOrDefault(q => q.Taken) ?? Quests.FirstOrDefault(q => !q.Taken);
            if (current == null) return;

            for (int i = 0; i < Rows.Length; i++)
            {
                if (i >= Quests.Count) break;

                if (Rows[i] != null)
                    Rows[i].Dispose();

                Rows[i] = new QuestRow
                {
                    Quest = current,
                    Location = new Point(9, 10 + i * 19),
                    Parent = this,
                };
                Rows[i].Click += (o, e) =>
                {
                    QuestRow row = (QuestRow) o;

                    if (row.Quest != SelectedQuest)
                    {
                        SelectedQuest = row.Quest;
                        Reward.UpdateRewards(SelectedQuest);
                        UpdateSimpleDetail();
                        SelectedIndex = FindSelectedIndex();
                        UpdateRows();

                        ReDisplayButtons();
                    }
                };

                if (SelectedQuest != null)
                {
                    if (SelectedIndex == i)
                    {
                        SelectedQuest = Rows[i].Quest;
                        Reward.UpdateRewards(SelectedQuest);
                        UpdateSimpleDetail();
                    }
                }
            }

            UpdateRows();

            ReDisplayButtons();
        }

        public void UpdateRows()
        {
            if (SelectedQuest == null)
            {
                // 直接选「当前任务」（已接取的任务），不再依赖 Rows[0]（可能为空或错位）
                SelectedQuest = Quests.FirstOrDefault(q => q.Taken) ?? Quests.FirstOrDefault();
                if (SelectedQuest == null) return;

                Reward.UpdateRewards(SelectedQuest);
                UpdateSimpleDetail();
            }

            for (int i = 0; i < Rows.Length; i++)
            {
                if (Rows[i] == null) continue;

                Rows[i].Selected = false;

                if (Rows[i].Quest == SelectedQuest)
                {
                    Rows[i].Selected = true;
                }

                Rows[i].UpdateInterface();
            }
        }

        public void ReDisplayButtons()
        {
            _PlayerTaskTalk.Visible = true;

            if (Reward != null)
            {
                Reward.Visible = true;
            }

            if (SelectedQuest == null) return;

            if (!SelectedQuest.Taken)
            {
                // 尚未接取：按钮文本取玩家任务台词（PlayerTaskTalk），无台词则回退“接取任务”
                var talk = SelectedQuest.QuestInfo.PlayerTaskTalk;
                _PlayerTaskTalk.Text = (talk != null && talk.Count > 0 && !string.IsNullOrWhiteSpace(talk[0])) ? talk[0] : "接取任务";
            }
            else if (SelectedQuest.Completed)
            {
                _PlayerTaskTalk.Text = "恭喜, 任务完成了,领取你的奖励吧。";
            }
            else
            {
                _PlayerTaskTalk.Text = "继续";
            }
        }

        private void UpdateSimpleDetail()
        {
            if (SelectedQuest == null)
            {
                _npcNameLabel.Text = "";
                ClearDescription();
                _PlayerTaskTalk.Visible = false;
                return;
            }

            string npcName = "";
            if (GameScene.NPCID != 0)
            {
                var npc = MapControl.GetObject(GameScene.NPCID) as NPCObject;
                if (npc != null) npcName = npc.Name;
            }
            _npcNameLabel.Text = npcName;

            List<string> desc;
            if (SelectedQuest.Taken && !SelectedQuest.QuestInfo.SameFinishNPC
                && SelectedQuest.QuestInfo.CompletionDescription.Count > 0
                && GameScene.NPCID == SelectedQuest.QuestInfo.FinishNPCIndex)
            {
                desc = SelectedQuest.QuestInfo.CompletionDescription;
            }
            else
            {
                desc = SelectedQuest.QuestInfo.Description;
            }

            RenderDescription(desc);
        }

        // 渲染任务描述：解析内联颜色/链接标签（{文字/颜色}、[ITEM:idx|名] 等，与 NPC 对话同一套格式）。
        // 底色白字由 _descLabel（WordBreak 自动换行）承载，彩色/链接词作为覆盖层叠在其上，
        // 复用 NPCDialogs 的公开正则与取色逻辑，确保描述与 NPC 对话表现一致。
        private static readonly Regex _colorTag = new Regex(@"\{([^/]*)/[^}]*\}", RegexOptions.Compiled);
        private static readonly Regex _linkTagNamed = new Regex(@"\[[^\]]*\|\s*([^\]]*)\]", RegexOptions.Compiled);
        private static readonly Regex _linkTagPlain = new Regex(@"\[([^\]|]*)\]", RegexOptions.Compiled);

        // 渲染任务描述：去掉所有颜色/链接富文本标记，纯白字显示，并浓缩为最多两行（其余合并进第二行）。
        private void RenderDescription(List<string> lines)
        {
            ClearDescription();

            var plain = new List<string>();
            foreach (var raw in lines)
            {
                string s = StripQuestRichText(raw);
                if (!string.IsNullOrWhiteSpace(s)) plain.Add(s);
            }

            string text;
            if (plain.Count <= 2)
                text = string.Join("\n", plain);
            else
                text = plain[0] + "\n" + string.Join(" ", plain.GetRange(1, plain.Count - 1));

            _descLabel.ForeColour = Color.White;
            _descLabel.Text = text;
            _descLabel.Visible = true;
        }

        // 去除任务描述里的 {文字/颜色} 与 [TYPE:idx|名字] / [名字] 标记，仅保留可阅读文本。
        private static string StripQuestRichText(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            s = _colorTag.Replace(s, "$1");      // {文字/颜色} -> 文字
            s = _linkTagNamed.Replace(s, "$1");  // [TYPE:idx|名字] -> 名字
            s = _linkTagPlain.Replace(s, "$1");  // [名字] -> 名字
            return s.Trim();
        }

        private void ClearDescription()
        {
            foreach (var s in _descSegments) s.Dispose();
            _descSegments.Clear();
            _descLabel.Visible = false;
            _descLabel.Text = "";
        }

        public int FindSelectedIndex()
        {
            int selectedIndex = 0;
            if (SelectedQuest != null)
            {
                for (int i = 0; i < Rows.Length; i++)
                {
                    if (Rows[i] == null || SelectedQuest != Rows[i].Quest) continue;

                    selectedIndex = i;
                }
            }

            return selectedIndex;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            
            Quests.Clear();

            SelectedQuest = null;
            Message = null;

            foreach (QuestRow row in Rows.Where(row => row != null && !row.IsDisposed))
            {
                row.Dispose();
            }
        }

    }

    //Sub controls
    public sealed class QuestRow : MirControl
    {
        public ClientQuestProgress Quest;
        public MirLabel NameLabel, RequirementLabel;
        public MirImageControl IconImage;

        public bool Selected = false;

        public QuestRow()
        {
            Sound = SoundList.ButtonA;
            Size = new Size(200, 17);

            BeforeDraw += QuestRow_BeforeDraw;

            IconImage = new MirImageControl
            {
                Index = 0,
                Library = Libraries.Prguse,
                Location = new Point(3, 0),
                Parent = this,
                Visible = false
            };

            RequirementLabel = new MirLabel
            {
                Location = new Point(20, 0),
                Size = new Size(178, 17),
                DrawFormat = TextFormatFlags.VerticalCenter,
                Parent = this,
                NotControl = true,
            };

            NameLabel = new MirLabel
            {
                Location = new Point(60, 0),
                Size = new Size(140, 17),
                DrawFormat = TextFormatFlags.VerticalCenter,
                Parent = this,
                NotControl = true,
            };

            UpdateInterface();
        }

        void QuestRow_BeforeDraw(object sender, EventArgs e)
        {
            UpdateInterface();
        }

        public void UpdateInterface()
        {
            if (Quest == null) return;

            int iconTypeOffset = (int)Quest.Icon > 3 ? 15 : 0;

            IconImage.Index = 961 + (int)Quest.Icon + iconTypeOffset;
            IconImage.Visible = true;

            NameLabel.Text = Quest.QuestInfo.Name;
            RequirementLabel.Text = Quest.QuestInfo.MinLevelNeeded > 0 ? "Lv " + Quest.QuestInfo.MinLevelNeeded : "";
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            Quest = null;
            NameLabel = null;
            RequirementLabel = null;
            IconImage = null;

            Selected = false;
        }
    }
    public sealed class QuestMessage : MirControl
    {
        public ClientQuestProgress Quest;
        public MirButton ScrollUpButton, ScrollDownButton, PositionBar;

        private static readonly Regex C = new Regex(@"{(.*?/.*?)}");

        private readonly MirLabel[] _textLabel;
        private readonly List<MirLabel> _textButtons = new List<MirLabel>();

        public int TopLine;
        public int BottomLine;
        public int LineCount = 10;
        public bool DisplayProgress;

        public int PosX, PosMinY, PosMaxY;

        public Font Font = new Font(Settings.FontName, 8F);
        public List<string> CurrentLines = new List<string>();

        private readonly string TaskTitle = GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.Tasks), ProgressTitle = GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.Progress), ReturnTitle = GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.QuestReturn), TimeLimitTitle = GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.TimeLimit);

        public QuestMessage(MirButton scrollUpButton, MirButton scrollDownButton, MirButton positionBar, int lineCount, bool displayProgress = false)
        {
            ScrollUpButton = scrollUpButton;
            ScrollDownButton = scrollDownButton;
            PositionBar = positionBar;
            DisplayProgress = displayProgress;

            MouseWheel += QuestMessage_MouseWheel;
            PositionBar.OnMoving += PositionBar_OnMoving;

            LineCount = lineCount;
            _textLabel = new MirLabel[LineCount];

            PosX = PositionBar.Location.X;
            PosMinY = PositionBar.Location.Y;
            PosMaxY = PositionBar.Location.Y;

            ScrollUpButton.Click += (o, e) =>
            {
                if (TopLine <= 0) return;

                TopLine--;

                NewText(CurrentLines, false);
                UpdatePositionBar();
            };

            ScrollDownButton.Click += (o, e) =>
            {
                if (TopLine + LineCount >= CurrentLines.Count) return;

                TopLine++;

                NewText(CurrentLines, false);
                UpdatePositionBar();
            };

            AfterDraw += QuestMessage_AfterDraw;

        }

        private void QuestMessage_AfterDraw(object sender, EventArgs e)
        {
            if (Quest == null || CurrentLines.Count < 1) return;

            int adjust = 0;

            for (int i = TopLine; i < BottomLine; i++)
            {
                if (i != 0 && CurrentLines[i] != TaskTitle && CurrentLines[i] != ProgressTitle && CurrentLines[i] != ReturnTitle && CurrentLines[i]  != TimeLimitTitle) continue;

                Libraries.Prguse.Draw(919, new Point(DisplayLocation.X + 5, DisplayLocation.Y + 5 + (i - TopLine) * 15 + adjust), Color.White);

                adjust += 5;
            }
        }

        private void QuestMessage_MouseWheel(object sender, MouseEventArgs e)
        {
            int count = e.Delta / SystemInformation.MouseWheelScrollDelta;

            if (TopLine == 0 && count >= 0) return;
            if (TopLine == CurrentLines.Count - 1 && count <= 0) return;
            if (CurrentLines.Count <= LineCount) return;

            TopLine -= count;

            if (TopLine < 0) TopLine = 0;
            if (TopLine + LineCount > CurrentLines.Count - 1) TopLine = CurrentLines.Count - LineCount;

            NewText(CurrentLines, false);

            UpdatePositionBar();
        }

        private void PositionBar_OnMoving(object sender, MouseEventArgs e)
        {
            int x = PosX;
            int y = PositionBar.Location.Y;

            if (y >= PosMaxY) y = PosMaxY;
            if (y <= PosMinY) y = PosMinY;

            int location = y - PosMinY;
            int interval = (PosMaxY - PosMinY) / (CurrentLines.Count - LineCount);

            double yPoint = (double)location / interval;

            TopLine = Convert.ToInt16(Math.Floor(yPoint));

            PositionBar.Location = new Point(x, y);

            NewText(CurrentLines, false);
        }

        private void UpdatePositionBar()
        {
            if (CurrentLines.Count <= LineCount)
            {
                PositionBar.Visible = false;
                return;
            }

            PositionBar.Visible = true;

            int interval = (PosMaxY - PosMinY) / (CurrentLines.Count - LineCount);

            int x = PosX;
            int y = PosMinY + (TopLine * interval);

            if (y >= PosMaxY) y = PosMaxY;
            if (y <= PosMinY) y = PosMinY;


            PositionBar.Location = new Point(x, y);
        }

        public void UpdateQuest(ClientQuestProgress quest)
        {
            Quest = quest;

            CurrentLines.Clear();

            //add quest title at the beginning
            CurrentLines.Insert(0, Quest.QuestInfo.Name);

            if (Quest.Taken && !Quest.QuestInfo.SameFinishNPC && Quest.QuestInfo.CompletionDescription.Count > 0 && GameScene.Scene.QuestListDialog.CurrentNPCID == Quest.QuestInfo.FinishNPCIndex)
            {
                foreach (var line in Quest.QuestInfo.CompletionDescription)
                {
                    CurrentLines.Add(line);
                }
            }
            else
            {
                foreach (var line in Quest.QuestInfo.Description)
                {
                    CurrentLines.Add(line);
                }

                AdjustDescription();
            }

            NewText(CurrentLines);
        }

        private void AdjustDescription()
        {
            if (Quest.QuestInfo.TaskDescription.Count > 0)
            {
                CurrentLines.Add(" ");
                CurrentLines.Add(TaskTitle);
                foreach (string task in Quest.QuestInfo.TaskDescription)
                {
                    CurrentLines.Add(task);
                }
            }

            if (Quest.QuestInfo.ReturnDescription.Count > 0)
            {
                CurrentLines.Add(" ");

                CurrentLines.Add(ReturnTitle);

                foreach (string questgiver in Quest.QuestInfo.ReturnDescription)
                {
                    CurrentLines.Add(questgiver);
                }
            }

            if (Quest.QuestInfo.TimeLimitInSeconds > 0)
            {
                CurrentLines.Add(" ");
                CurrentLines.Add(TimeLimitTitle);
                CurrentLines.Add(Functions.PrintTimeSpanFromSeconds(Quest.QuestInfo.TimeLimitInSeconds, true));
            }

            if (Quest.Taken && Quest.TaskList.Count > 0 && DisplayProgress)
            {
                CurrentLines.Add(" ");

                CurrentLines.Add(ProgressTitle);

                foreach (string task in Quest.TaskList)
                {
                    CurrentLines.Add(task);
                }
            }
        }

        private void NewText(List<string> lines, bool resetIndex = true)
        {
            if (resetIndex)
            {
                TopLine = 0;
                CurrentLines = lines;
                UpdatePositionBar();
            }

            foreach (MirLabel t in _textButtons.Where(t => t != null))
                t.Dispose();

            foreach (MirLabel t in _textLabel.Where(t => t != null))
                t.Text = "";

            _textButtons.Clear();

            int adjust = 0;

            BottomLine = lines.Count > LineCount ? ((LineCount + TopLine) > lines.Count ? lines.Count : (LineCount + TopLine)) : lines.Count;

            for (int i = TopLine; i < BottomLine; i++)
            {
                Font font = Font;
                Color fontColor = Color.White;
                bool title = false;

                if (i == 0 || lines[i] == TaskTitle || lines[i] == ProgressTitle || lines[i] == ReturnTitle || lines[i] == TimeLimitTitle)
                {
                    font = new Font(Settings.FontName, 10F, KFramework.MonoGame.FontStyle.Bold);
                    title = true;

                    if (i == 0)
                    {
                        fontColor = Color.Yellow;
                    }
                }

                _textLabel[i - TopLine] = new MirLabel
                {
                    Font = font,
                    ForeColour = fontColor,
                    DrawFormat = TextFormatFlags.WordBreak,
                    Visible = true,
                    Parent = this,
                    Size = new Size(Size.Width, 20),
                    Location = new Point(title ? 15 : 0, 0 + (i - TopLine) * 15 + adjust),
                    NotControl = true
                };

                if (title)
                {
                    adjust += 5;
                }

                if (i >= lines.Count)
                {
                    _textLabel[i - TopLine].Text = string.Empty;
                    continue;
                }

                string currentLine = lines[i];

                List<Match> matchList = C.Matches(currentLine).Cast<Match>().ToList();

                // Add link patterns (using static patterns from NPCDialog)
                matchList.AddRange(NPCDialog.MonsterLink.Matches(currentLine).Cast<Match>());
                matchList.AddRange(NPCDialog.NPCLink.Matches(currentLine).Cast<Match>());
                matchList.AddRange(NPCDialog.ItemLink.Matches(currentLine).Cast<Match>());
                
                int oldLength = currentLine.Length;

                foreach (Match match in matchList.OrderBy(o => o.Index).ToList())
                {
                    int offSet = oldLength - currentLine.Length;

                    // Check if this is a link type
                    string linkType = null;
                    if (NPCDialog.MonsterLink.Match(match.Value).Success)
                        linkType = "MONSTER";
                    else if (NPCDialog.NPCLink.Match(match.Value).Success)
                        linkType = "NPC";
                    else if (NPCDialog.ItemLink.Match(match.Value).Success)
                        linkType = "ITEM";

                    if (linkType != null)
                    {
                        string linkIdx = match.Groups["idx"].Captures.Count > 0 ? match.Groups["idx"].Captures[0].Value : match.Groups["idx"].Value;
                        string providedName = match.Groups["name"].Success ? match.Groups["name"].Captures[0].Value : null;
                        string displayName = NPCDialog.GetDisplayNameForLink(linkType, linkIdx, providedName);
                        if (string.IsNullOrEmpty(displayName))
                            displayName = $"LINK_{linkIdx}";

                        int matchStart = match.Index - offSet;
                        int matchLength = match.Length;

                        currentLine = currentLine.Remove(matchStart, matchLength).Insert(matchStart, displayName);
                        string text2 = currentLine.Substring(0, matchStart);
                        Point offset = NPCDialog.CalculateLinkOffset(text2, _textLabel[i - TopLine]);

                        Point anchorPoint = _textLabel[i - TopLine].Location.Add(offset);
                        NewLink(displayName, linkType, linkIdx, anchorPoint);

                        continue;
                    }

                    Capture capture = match.Groups[1].Captures[0];
                    string[] values = capture.Value.Split('/');
                    currentLine = currentLine.Remove(capture.Index - 1 - offSet, capture.Length + 2).Insert(capture.Index - 1 - offSet, values[0]);
                    string text = currentLine.Substring(0, capture.Index - 1 - offSet) + " ";
                    Size size = TextRenderer.MeasureText(text, _textLabel[i - TopLine].Font, new MirEngine.Size(_textLabel[i - TopLine].Size.Width, int.MaxValue));

                    if (C.Match(match.Value).Success)
                        NewColour(values[0], values[1], _textLabel[i - TopLine].Location.Add(new Point(size.Width - 10, 0)));
                }

                _textLabel[i - TopLine].Text = currentLine;
                _textLabel[i - TopLine].MouseWheel += QuestMessage_MouseWheel;
            }
        }

        private void NewColour(string text, string colour, Point p)
        {
            Color textColour = ColorTranslator.FromHtml(colour);

            MirLabel temp = new MirLabel
            {
                AutoSize = true,
                Visible = true,
                Parent = this,
                Location = p,
                Text = text,
                ForeColour = textColour,
                Font = Font
            };
            temp.MouseWheel += QuestMessage_MouseWheel;

            _textButtons.Add(temp);
        }

        private void NewLink(string text, string linkType, string linkIdx, Point p)
        {
            MirLabel temp = new MirLabel
            {
                AutoSize = true,
                Visible = true,
                Parent = this,
                Location = p,
                Text = text,
                ForeColour = Color.Cyan,
                Font = Font
            };

            temp.MouseEnter += (o, e) =>
            {
                temp.ForeColour = Color.Orange;
                NPCDialog.ShowTooltipForLink(linkType, linkIdx, temp.DisplayLocation);
            };

            temp.MouseLeave += (o, e) =>
            {
                temp.ForeColour = Color.Cyan;
                NPCDialog.HideTooltipForLink();
            };

            temp.MouseWheel += QuestMessage_MouseWheel;
            _textButtons.Add(temp);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            Quest = null;
            ScrollUpButton = null;
            ScrollDownButton = null;
            PositionBar = null;

            CurrentLines.Clear();
        }
    }
    public sealed class QuestRewards : MirControl
    {
        public ClientQuestProgress Quest;

        private readonly List<MirLabel> _labels = new List<MirLabel>();

        public void UpdateRewards(ClientQuestProgress quest)
        {
            Quest = quest;
            ClearLabels();

            if (Quest == null) return;

            ClientQuestInfo info = Quest.QuestInfo;
            int x = 8; // 一行内的左内边距

            if (info.RewardExp > 0)
                x = AddSegment(x, "经验 " + info.RewardExp.ToString("###0"));
            if (info.RewardGold > 0)
                x = AddSegment(x, "金币 " + info.RewardGold.ToString("###0"));
            if (info.RewardCredit > 0)
                x = AddSegment(x, "元宝 " + info.RewardCredit.ToString("###0"));

            // 固定奖励物品
            foreach (var r in FilterRewards(info.RewardsFixedItem))
                x = AddItemSegment(x, r.Item, r.Count);

            // 可选奖励物品（一行内全部列出，仅作展示、全部发放）
            foreach (var r in FilterRewards(info.RewardsSelectItem))
                x = AddItemSegment(x, r.Item, r.Count);

            Redraw();
        }

        // 非物品奖励（经验/金币/元宝）：整段下划线黄字，字号 8
        private int AddSegment(int x, string text)
        {
            var label = new MirLabel
            {
                Parent = this,
                AutoSize = true,
                Location = new Point(x, 2),
                Text = text,
                ForeColour = Color.Yellow,
                UnderLine = true,
                NotControl = false,
                Font = new Font(Settings.FontName, 12F),
            };

            _labels.Add(label);

            return x + label.Size.Width + 12;
        }

        // 物品奖励：物品名（下划线、悬浮/点击弹背包同款提示） + 其后 " x数量"
        private int AddItemSegment(int x, ItemInfo item, int count)
        {
            var nameLabel = new MirLabel
            {
                Parent = this,
                AutoSize = true,
                Location = new Point(x, 2),
                Text = item.Name,
                ForeColour = Color.Yellow,
                UnderLine = true,
                NotControl = false,
                Font = new Font(Settings.FontName, 12F),
            };

            _labels.Add(nameLabel);

            KFramework.MonoGame.PrintTool.LogError($"[QuestReward] AddItemSegment 创建 label item={item.Name}");

            UserItem showItem = new UserItem(item) { MaxDura = item.Durability, CurrentDura = item.Durability };
            nameLabel.MouseEnter += (o, e) =>
            {
                KFramework.MonoGame.PrintTool.LogError($"[QuestReward] MouseEnter item={item.Name}");
                try { GameScene.Scene.CreateItemLabel(showItem); }
                catch (Exception ex) { KFramework.MonoGame.PrintTool.LogError("[QuestReward] CreateItemLabel 失败: " + ex); }
            };
            nameLabel.Click += (o, e) =>
            {
                KFramework.MonoGame.PrintTool.LogError($"[QuestReward] Click item={item.Name}");
                try { GameScene.Scene.CreateItemLabel(showItem); }
                catch (Exception ex) { KFramework.MonoGame.PrintTool.LogError("[QuestReward] CreateItemLabel 失败: " + ex); }
            };
            nameLabel.MouseLeave += (o, e) =>
            {
                KFramework.MonoGame.PrintTool.LogError($"[QuestReward] MouseLeave item={item.Name}");
                GameScene.Scene.DisposeItemLabel();
            };

            x += nameLabel.Size.Width;

            if (count > 1)
            {
                var countLabel = new MirLabel
                {
                    Parent = this,
                    AutoSize = true,
                    Location = new Point(x, 2),
                    Text = " x" + count,
                    ForeColour = Color.Yellow,
                    NotControl = false,
                    Font = new Font(Settings.FontName, 12F),
                };

                _labels.Add(countLabel);
                x += countLabel.Size.Width;
            }

            return x + 12;
        }

        private void ClearLabels()
        {
            foreach (var l in _labels)
                if (l != null && !l.IsDisposed) l.Dispose();
            _labels.Clear();
        }

        public List<QuestItemReward> FilterRewards(List<QuestItemReward> rewardItems)
        {
            List<QuestItemReward> filtered = new List<QuestItemReward>();
            if (rewardItems == null) return filtered;
            foreach (var reward in rewardItems)
            {
                ItemInfo item = reward.Item;

                switch (MapObject.User.Gender)
                {
                    case MirGender.Male:
                        if (!item.RequiredGender.HasFlag(RequiredGender.Male)) continue;
                        break;
                    case MirGender.Female:
                        if (!item.RequiredGender.HasFlag(RequiredGender.Female)) continue;
                        break;
                }

                filtered.Add(reward);
            }

            return filtered;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            Quest = null;
            ClearLabels();
        }

    }
    public sealed class QuestCell : MirControl
    {
        public ItemInfo Item;
        public UserItem ShowItem;
        public ushort Count;

        public bool Selected;
        public bool Fixed;

        private MirLabel CountLabel { get; set; }

        public QuestCell()
        {
            Size = new Size(32, 32);
        }

        protected override void OnMouseEnter()
        {
            base.OnMouseEnter();

            if (ShowItem == null) ShowItem = new UserItem(Item) { MaxDura = Item.Durability, CurrentDura = Item.Durability };

            GameScene.Scene.CreateItemLabel(ShowItem);
        }
        protected override void OnMouseLeave()
        {
            base.OnMouseLeave();
            GameScene.Scene.DisposeItemLabel();
            GameScene.HoverItem = null;
            ShowItem = null;
        }

        protected internal override void DrawControl()
        {
            if (Item == null) return;

            Size size = Libraries.Items.GetTrueSize(Item.Image);
            Point offSet = new Point((40 - size.Width) / 2, (32 - size.Height) / 2);

            CreateDisposeLabel();

            if (Fixed)
            {
                Libraries.Prguse.Draw(989, DisplayLocation.X, DisplayLocation.Y - 1);
            }
            else if (Selected)
            {
                Libraries.Prguse.Draw(979, DisplayLocation.X, DisplayLocation.Y - 5);
            }

            Libraries.Items.Draw(Item.Image, offSet.X + DisplayLocation.X, offSet.Y + DisplayLocation.Y);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            Item = null;
            ShowItem = null;

            DisposeCountLabel();
        }

        private void CreateDisposeLabel()
        {
            if (Count <= 1)
            {
                DisposeCountLabel();
                return;
            }

            if (CountLabel == null || CountLabel.IsDisposed)
            {
                CountLabel = new MirLabel
                {
                    AutoSize = true,
                    ForeColour = Color.Yellow,
                    NotControl = true,
                    OutLine = false,
                    Parent = this,
                };
            }

            CountLabel.Text = Count.ToString("###0");
            CountLabel.Location = new Point(Size.Width - CountLabel.Size.Width + 8, Size.Height - CountLabel.Size.Height);
        }
        private void DisposeCountLabel()
        {
            if (CountLabel != null && !CountLabel.IsDisposed)
                CountLabel.Dispose();
            CountLabel = null;
        }
    }


    public sealed class QuestGroupQuestItem : MirControl
    {
        public string Group = string.Empty;
        public List<ClientQuestProgress> Quests = new List<ClientQuestProgress>();

        public bool Expanded = true;
        public int SizeY = 15;

        private readonly MirButton _expandButton;
        private readonly MirLabel _groupLabel;
        private readonly List<QuestSingleQuestItem> _tasks = new List<QuestSingleQuestItem>();

        public event EventHandler ExpandedChanged;
        private void OnExpandedChanged()
        {
            if (ExpandedChanged != null)
                ExpandedChanged.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler SelectedQuestChanged;
        private void OnSelectedQuestChanged(object ob)
        {
            if (SelectedQuestChanged != null)
                SelectedQuestChanged.Invoke(ob, EventArgs.Empty);
        }

        public QuestGroupQuestItem(string group, List<ClientQuestProgress> quests, bool expanded)
        {
            Group = group;
            Quests = quests;
            Expanded = expanded;

            _expandButton = new MirButton
            {
                Index = Expanded ? 917 : 918,
                Library = Libraries.Prguse,
                Parent = this,
                Size = new Size(16, 14),
                Location = new Point(0, 0),
                Sound = SoundList.ButtonA
            };
            _expandButton.Click += (o, e) => ChangeExpand();

            _groupLabel = new MirLabel
            {
                Text = Group,
                AutoSize = true,
                Parent = this,
                Font = new Font(Settings.FontName, 8F),
                ForeColour = Color.LimeGreen,
                Location = new Point(18, 0),
                Visible = true,
            };

            for (int i = 0; i < Quests.Count; i++)
            {
                bool Track = Settings.TrackedQuests.Contains(Quests[i].Id) ? true : false;
                QuestSingleQuestItem singleQuest = new QuestSingleQuestItem(Quests[i])
                {
                    Parent = this,
                    Location = new Point(18, (15 * (i + 1))),
                    Size = new Size(280, 15),
                    Visible = Expanded
                };
                singleQuest.SelectedQuestChanged += (o, e) => OnSelectedQuestChanged(o);

                _tasks.Add(singleQuest);

                if (Expanded)
                    SizeY += 15;
            }

            Size = new Size(280, SizeY);
        }

        public void UpdatePositions()
        {
            SizeY = 15;

            foreach (var singleTask in _tasks)
            {
                singleTask.Visible = Expanded;

                if (Expanded) SizeY += 15;
            }

            Size = new Size(280, SizeY);
        }

        public void ClearTasks()
        {
            foreach (QuestSingleQuestItem task in _tasks)
            {
                task.Dispose();
            }
            _tasks.Clear();
        }

        public void DeselectQuests()
        {
            foreach (QuestSingleQuestItem task in _tasks)
            {
                task.Selected = false;
            }
        }

        private void ChangeExpand()
        {
            Expanded = !Expanded;

            _expandButton.Index = Expanded ? 917 : 918;

            UpdatePositions();

            OnExpandedChanged();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            Group = string.Empty;
            Quests.Clear();

            _expandButton.Dispose();
            _groupLabel.Dispose();

            ClearTasks();
        }
    }
    public sealed class QuestSingleQuestItem : MirControl
    {
        private MirLabel _questLabel, _stateLabel;
        private readonly MirImageControl _selectedImage;

        public ClientQuestProgress Quest;

        public bool Selected;
        public bool TrackQuest;

        public event EventHandler SelectedQuestChanged;
        private void OnSelectedQuestChanged()
        {
            if (SelectedQuestChanged != null)
                SelectedQuestChanged.Invoke(this, EventArgs.Empty);
        }

        public QuestSingleQuestItem(ClientQuestProgress quest)
        {
            Quest = quest;
            Size = new Size(250, 15);
            TrackQuest = GameScene.Scene.QuestTrackingDialog.TrackedQuestsIds.Contains(quest.Id);

            string name = Quest.QuestInfo.Name;
            string level = string.Format("Lv{0}", Quest.QuestInfo.MinLevelNeeded);
            string state = quest.Completed ? GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.Complete) : GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.InProgress);

            bool lowLevelQuest = (MapObject.User.Level - quest.QuestInfo.MinLevelNeeded) > 10;

            BeforeDraw += QuestTaskSingleItem_BeforeDraw;
            AfterDraw += QuestTaskSingleItem_AfterDraw;

            _selectedImage = new MirImageControl
            {
                Index = 956,
                Library = Libraries.Prguse,
                Location = new Point(-10, 0),
                Parent = this,
                Visible = false
            };

            _questLabel = new MirLabel
            {
                Text = string.Format("{0,-4} {1}", level, name),
                AutoSize = true,
                Font = new Font(Settings.FontName, 8F),
                ForeColour = lowLevelQuest ? Color.Gray : quest.New ? Color.Yellow : Color.White,
                Parent = this,
                Location = new Point(0, 0),
                Sound = SoundList.ButtonA
            };

            _questLabel.Click += (o, e) =>
            {
                MouseEventArgs me = e as MouseEventArgs;

                if (me == null) return;

                switch (me.Button)
                {
                    case MouseButtons.Left:
                        GameScene.Scene.QuestDetailDialog.DisplayQuestDetails(Quest);
                        break;
                    case MouseButtons.Right:
                        {
                            if (TrackQuest)
                            {
                                GameScene.Scene.QuestTrackingDialog.RemoveQuest(Quest);
                            }
                            else
                            {
                                if (GameScene.Scene.QuestTrackingDialog.TrackedQuestsIds.Count >= 5) return;

                                GameScene.Scene.QuestTrackingDialog.AddQuest(Quest);
                            }

                            TrackQuest = !TrackQuest;
                        }
                        break;
                }

                OnSelectedQuestChanged();
            };

            _stateLabel = new MirLabel
            {
                Text = string.Format("{0}", state),
                AutoSize = true,
                Font = new Font(Settings.FontName, 8F),
                ForeColour = lowLevelQuest ? Color.Gray : quest.New ? Color.Yellow : Color.White,
                Parent = this,
                Location = new Point(185, 0),
                Sound = SoundList.ButtonA
            };
        }

        void QuestTaskSingleItem_BeforeDraw(object sender, EventArgs e)
        {
            _selectedImage.Visible = Selected;
        }

        void QuestTaskSingleItem_AfterDraw(object sender, EventArgs e)
        {
            if (TrackQuest)
            {
                Libraries.Prguse.Draw(997, new Point(DisplayLocation.X - 15, DisplayLocation.Y), Color.White);
            }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            Quest = null;
            _questLabel.Dispose();
            _questLabel = null;
        }
    }

}
