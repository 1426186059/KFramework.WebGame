using Client.MirControls;
using Client.MirObjects;
using KFramework.MonoGameExtend;
using Client.MirNetwork;
using Client.MirGraphics;
using ClientPackets;
using C = ClientPackets;

namespace Client.MirScenes.Dialogs
{
    public sealed class QuestTrackingDialog : MirImageControl
    {
        public const int MaxTrackedQuests = 5;

        public List<int> TrackedQuestsIds = new List<int>();
        public List<MirLabel> TaskLines = new List<MirLabel>();

        public static Font QuestFont = new Font(Settings.FontName, 12F);

        private MirImageControl m_ShowNode;
        private MirButton m_HideNode;
        private MirImageControl mShowNode_Bg;
        private MirScrollView2 _scrollView;

        // 原始传奇 JS 的 TaskType 共 9 种：1主线 2除魔 3随机BOSS 4回收 5神威 7守护 8登陆 99悬赏 130九神。
        // taskRenderDic 每种类型最多一个条目 → 最多 9 行；行高固定，交给 MirScrollView2 虚拟化滚动。
        private const int SlotCount = 9;
        private const int RowHeight = 120;
        private const string QuestBorderSheetKey = "resource/assets/gameui4/movieClip/y_task_complete.json";
        // 原版 scaleX=.75 / scaleY=.8，我们的条目更紧凑，缩到 .55。
        private const float QuestBorderScale = 0.55f;
        private const int MaxTaskLines = 8;

        private sealed class DisplayEntry
        {
            public ClientQuestProgress Quest;
            public bool IsOffered;
        }

        private readonly List<DisplayEntry> _display = new List<DisplayEntry>();

        private enum OriginalQuestKind
        {
            Main = 0,
            ChuMo,
            RandomBoss,
            Recovery,
            Shenwei,
            ShouHu,
            WdLogin,
            Xuanshang,
            JiuShen
        }

        /// <summary>把任务表里的原始 taskgroupid 映射到原版 9 种 TaskType 的槽位。</summary>
        private static int SlotOf(ClientQuestInfo info)
        {
            if (info == null) return 0;

            if (info.OriginalType == 0)
                return info.Type == QuestType.Story ? 0 : (int)OriginalQuestKind.Xuanshang;

            switch (info.OriginalType)
            {
                case 1: return (int)OriginalQuestKind.Main;
                case 2: return (int)OriginalQuestKind.ChuMo;
                case 3: return (int)OriginalQuestKind.RandomBoss;
                case 4: return (int)OriginalQuestKind.Recovery;
                case 5: return (int)OriginalQuestKind.Shenwei;
                case 6: return (int)OriginalQuestKind.ChuMo;
                case 7: return (int)OriginalQuestKind.ShouHu;
                case 8: return (int)OriginalQuestKind.WdLogin;
                case 130: return (int)OriginalQuestKind.JiuShen;
                case 99:
                case 101:
                case 102:
                case 103:
                case 104:
                case 105:
                case 106:
                case 107:
                case 108:
                case 109:
                case 110:
                case 120:
                case 201:
                    return (int)OriginalQuestKind.Xuanshang;
                default: return (int)OriginalQuestKind.Xuanshang;
            }
        }

        /// <summary>
        /// 一行任务条目。MirScrollView2 只在视口内创建/复用这个控件；
        /// 特效和标签都在构造期建好，Bind 只改文字、Visible、动画开关。
        /// </summary>
        private sealed class QuestTrackingItem : MirControl
        {
            private readonly MirLabel _nameLabel;
            private readonly MirLabel _findLabel;
            private readonly MirLabel[] _taskLabels = new MirLabel[MaxTaskLines];
            private readonly QuestTaskTarget[] _targets = new QuestTaskTarget[MaxTaskLines];

            private ClientQuestProgress _quest;
            private bool _isOffered;

            public MirAnimatedAtlasControl Effect { get; }

            public QuestTrackingItem(QuestTrackingDialog owner)
            {
                NotControl = true;
                Size = new Size(Math.Max(40, owner._scrollView.Size.Width), RowHeight);
                Location = new Point(0, 0);

                _nameLabel = owner.NewLabel(Color.LimeGreen, true);
                _nameLabel.Parent = this;

                _findLabel = owner.NewLabel(Color.Gold, false);
                _findLabel.Parent = this;
                _findLabel.UnderLine = true;
                _findLabel.Click += (s, e) =>
                {
                    if (_quest == null || !_isOffered) return;
                    Network.Enqueue(new C.QuestCmd { Action = (byte)QuestCmdType.Offered, QuestIndex = _quest.Id });
                };

                for (int i = 0; i < MaxTaskLines; i++)
                {
                    int index = i;
                    _taskLabels[i] = owner.NewLabel(Color.White, true);
                    _taskLabels[i].Parent = this;
                    _taskLabels[i].Click += (s, e) =>
                    {
                        QuestTaskTarget target = _targets[index];
                        if (_quest == null || _isOffered || target == null) return;
                        Network.Enqueue(new C.TeleportToQuestTarget
                        {
                            MapIndex = target.MapIndex,
                            X = target.X,
                            Y = target.Y,
                            MonsterIndex = target.MonsterIndex
                        });
                    };
                }

                Effect = new MirAnimatedAtlasControl
                {
                    Parent = this,
                    NotControl = true,
                    Loop = true,
                    Animated = false,
                    Blending = true,
                    ForeColour = Color.White,
                    AutoSetupMovieClip = false,
                    Visible = false,
                };
                Effect.AtlasLoaded += (o, e) =>
                {
                    var sheet = Effect.AtlasSheet;
                    if (sheet?.MovieClip != null)
                        Effect.SetMovieClip(sheet, QuestBorderScale);
                };
                Effect.AtlasNameKey = QuestBorderSheetKey;

                Reset();
            }

            private void SetLabel(MirLabel label, string text, int x, int y, Color colour, bool notControl, bool show)
            {
                label.Text = text ?? string.Empty;
                label.Location = new Point(x, y);
                label.ForeColour = colour;
                label.NotControl = notControl;
                label.Visible = show;
            }

            public void Reset()
            {
                _quest = null;
                _isOffered = false;
                for (int i = 0; i < MaxTaskLines; i++)
                {
                    _targets[i] = null;
                    _taskLabels[i].Visible = false;
                }
                _nameLabel.Visible = false;
                _findLabel.Visible = false;
                Effect.Visible = false;
                Effect.Animated = false;
            }

            public void Bind(DisplayEntry entry, bool showEffect)
            {
                _quest = entry.Quest;
                _isOffered = entry.IsOffered;

                SetLabel(_nameLabel, entry.Quest.QuestInfo.Name, 2, 2, Color.LimeGreen, true, true);
                Effect.Location = new Point(-12, -11);
                Effect.Visible = showEffect;
                Effect.Animated = showEffect;

                if (entry.IsOffered)
                {
                    string npcName = !string.IsNullOrEmpty(entry.Quest.QuestInfo.StartNpcName)
                        ? entry.Quest.QuestInfo.StartNpcName
                        : entry.Quest.QuestInfo.Name;
                    SetLabel(_findLabel, "寻找 " + npcName, 2, 22, Color.Gold, false, true);
                    for (int i = 0; i < MaxTaskLines; i++)
                        _taskLabels[i].Visible = false;
                    return;
                }

                _findLabel.Visible = false;

                int line = 0;
                foreach (string taskText in entry.Quest.TaskList)
                {
                    if (line >= MaxTaskLines) break;

                    string text = taskText ?? string.Empty;
                    int lineY = 22 + 15 * line;
                    QuestTaskTarget target = (entry.Quest.Targets != null && line < entry.Quest.Targets.Count)
                        ? entry.Quest.Targets[line] : null;
                    _targets[line] = target;
                    line++;

                    if (target != null && target.MapIndex > 0 && !string.IsNullOrEmpty(target.Name))
                    {
                        int idx = text.IndexOf(target.Name, StringComparison.Ordinal);
                        if (idx >= 0)
                        {
                            string prefix = text.Substring(0, idx);
                            string targetText = text.Substring(idx);
                            SetLabel(_taskLabels[line - 1], prefix, 2, lineY, Color.White, true, true);
                            if (line >= MaxTaskLines) break;

                            int prefixW = TextRenderer.MeasureText(prefix, QuestFont).Width + 2;
                            _targets[line] = target;
                            SetLabel(_taskLabels[line], targetText, 2 + prefixW, lineY, Color.Green, false, true);
                            line++;
                            continue;
                        }
                    }

                    bool navigable = target != null && target.MapIndex > 0;
                    SetLabel(_taskLabels[line - 1], text, 2, lineY, navigable ? Color.Green : Color.White, !navigable, true);
                }

                for (int i = line; i < MaxTaskLines; i++)
                {
                    _targets[i] = null;
                    _taskLabels[i].Visible = false;
                }
            }
        }

        public QuestTrackingDialog()
        {
            Location = new Point(0, 50);
            AutoSize = false;
            Size = new Size(265, 188);
            DrawImage = false;
            Texture = KDefaultRes.DefaultTexture2D;

            m_HideNode = new MirButton
            {
                AtlasNameKey = "y_lefttop-sheet",
                AtlasFrame = "lefttop_leftbg2",
                Location = new Point(0, 0),
                Parent = this,
                Size = new Size(19, 188),
                Visible = false,
            };
            m_HideNode.LabelImage.AtlasNameKey = "y_lefttop-sheet";
            m_HideNode.LabelImage.AtlasFrame = "lefttop_leftbg0";
            m_HideNode.LabelImage.Size = new Size(12, 46);
            m_HideNode.LabelImage.Location = m_HideNode.LabelImage.Center;
            m_HideNode.LabelImage.Visible = true;
            m_HideNode.Click += (o, e) => ShowNode();

            m_ShowNode = new MirImageControl
            {
                Parent = this,
                AutoSize = false,
                DrawImage = false,
                Texture = KDefaultRes.DefaultTexture2D,
                ForeColour = Color.Blue,
                Location = new Point(0, 0),
                Size = new Size(265, 188),
            };

            mShowNode_Bg = new MirImageControl
            {
                Parent = m_ShowNode,
                AtlasNameKey = "y_lefttop-sheet",
                AtlasFrame = "lefttopbg",
                Location = new Point(40, 0),
            };

            var mShowNode_HideBtn = new MirButton
            {
                Parent = m_ShowNode,
                AtlasNameKey = "y_lefttop-sheet",
                AtlasFrame = "lefttop_leftbg2",
                Location = new Point(245, 0),
                Size = new Size(19, 188),
            };
            mShowNode_HideBtn.Click += (o, e) => HideNode();
            mShowNode_HideBtn.LabelImage.AtlasNameKey = "y_lefttop-sheet";
            mShowNode_HideBtn.LabelImage.AtlasFrame = "lefttop_leftbg1";
            mShowNode_HideBtn.LabelImage.Size = new Size(12, 46);
            mShowNode_HideBtn.LabelImage.Location = mShowNode_HideBtn.LabelImage.Center;
            mShowNode_HideBtn.LabelImage.Visible = true;

            var mSwitchBtn = new MirButton
            {
                Parent = m_ShowNode,
                AtlasNameKey = "y_lefttop-sheet",
                AtlasFrame = "lefttop_change0",
                PressedAtlasFrame = "lefttop_change0_1",
            };
            mSwitchBtn.Location = new Point(0, 188 / 2 - 20);

            var mTaskBtn = new MirButton
            {
                Parent = m_ShowNode,
                AtlasNameKey = "y_lefttop-sheet",
                AtlasFrame = "lefttop_bg0_1",
                Location = new Point(0, 0),
            };

            var mTeamBtn = new MirButton
            {
                Parent = m_ShowNode,
                AtlasNameKey = "y_lefttop-sheet",
                AtlasFrame = "lefttop_bg1_1",
                Location = new Point(0, 188 - 85),
            };

            // 滚动区就是 lefttopbg 背景本身：同位置、同大小，隐藏滚动条，只保留滚轮/拖拽。
            _scrollView = new MirScrollView2
            {
                Parent = mShowNode_Bg,
                Location = new Point(0, 0),
                Size = mShowNode_Bg.Size,
                BackColour = Color.Transparent,
                Border = false,
                ShowScrollBar = false,
            };
            mShowNode_Bg.AtlasLoaded += (o, e) =>
            {
                _scrollView.Location = new Point(0, 0);
                _scrollView.Size = mShowNode_Bg.Size;
                _scrollView.SetDataSource(_display.Count, RowHeight, CreateQuestRow, BindQuestRow);
            };

            _scrollView.SetDataSource(0, RowHeight, CreateQuestRow, BindQuestRow);
        }

        private MirLabel NewLabel(Color colour, bool notControl)
        {
            var label = new MirLabel
            {
                AutoSize = true,
                BackColour = Color.Transparent,
                Font = QuestFont,
                ForeColour = colour,
                OutLine = true,
                NotControl = notControl,
                Visible = false,
            };
            TaskLines.Add(label);
            return label;
        }

        private MirControl CreateQuestRow()
        {
            return new QuestTrackingItem(this);
        }

        private void BindQuestRow(MirControl row, int index)
        {
            if (index < 0 || index >= _display.Count)
            {
                ((QuestTrackingItem)row).Reset();
                return;
            }

            DisplayEntry entry = _display[index];
            ((QuestTrackingItem)row).Bind(entry, entry.IsOffered || entry.Quest.Completed);
        }

        public void HideNode()
        {
            m_ShowNode.Visible = false;
            m_HideNode.Visible = true;
        }

        public void ShowNode()
        {
            m_ShowNode.Visible = true;
            m_HideNode.Visible = false;
            DisplayQuests();
        }

        public void DisplayQuests()
        {
            var questsToTrack = new List<ClientQuestProgress>();
            questsToTrack.AddRange(from quest in MapObject.User.CurrentQuests
                                   from id in TrackedQuestsIds.Where(id => quest.Id == id)
                                   select quest);

            // 每种 TaskType 最多一个条目：已接取优先，未接取补空槽。
            var assigned = new Dictionary<int, DisplayEntry>();

            for (int i = 0; i < questsToTrack.Count; i++)
            {
                ClientQuestProgress quest = questsToTrack[i];
                if (quest.QuestInfo == null) continue;

                int slot = SlotOf(quest.QuestInfo);
                if (!assigned.ContainsKey(slot))
                    assigned[slot] = new DisplayEntry { Quest = quest, IsOffered = false };

                if (i >= 5) break;
            }

            foreach (ClientQuestProgress offered in MapObject.User.OfferedQuests)
            {
                if (offered.QuestInfo == null) continue;

                int slot = SlotOf(offered.QuestInfo);
                if (!assigned.ContainsKey(slot))
                    assigned[slot] = new DisplayEntry { Quest = offered, IsOffered = true };
            }

            _display.Clear();
            foreach (int slot in assigned.Keys.OrderBy(k => k))
                _display.Add(assigned[slot]);

            _scrollView.SetDataSource(_display.Count, RowHeight, CreateQuestRow, BindQuestRow);
            SyncWithNpcPanels();
        }

        // 跟踪列表完全由服务器下发（接取任务时自动记录），客户端只维护显示。
        public void AddQuest(ClientQuestProgress quest)
        {
            if (TrackedQuestsIds.Any(d => d == quest.Id) || TrackedQuestsIds.Count >= MaxTrackedQuests)
                return;

            TrackedQuestsIds.Add(quest.Id);
            DisplayQuests();
        }

        public void RemoveQuest(ClientQuestProgress quest)
        {
            if (!TrackedQuestsIds.Remove(quest.Id)) return;
            DisplayQuests();
        }

        public void SyncWithNpcPanels()
        {
            Visible = !(GameScene.Scene.NPCDialog.Visible || GameScene.Scene.QuestListDialog.Visible);
        }
    }
}
