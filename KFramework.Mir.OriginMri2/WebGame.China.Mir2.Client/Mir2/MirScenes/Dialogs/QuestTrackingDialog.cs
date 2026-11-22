using Client.MirControls;
using Client.MirObjects;
using KFramework.MonoGameExtend;
using Client.MirNetwork;
using Client.MirGraphics;
using ClientPackets;
using C = ClientPackets;
using KFramework.MonoGame;

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
        private const int RowHeight = 60;
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

        /// <summary>把原始传奇（页游）任务类型映射到任务追踪面板的分组槽位。</summary>
        private static int SlotOf(ClientQuestInfo info)
        {
            if (info == null) return 0;

            switch (info.Type)
            {
                case QuestType.Main: return (int)OriginalQuestKind.Main;
                case QuestType.ChuMo:
                case QuestType.Group6: return (int)OriginalQuestKind.ChuMo;
                case QuestType.RandomBoss: return (int)OriginalQuestKind.RandomBoss;
                case QuestType.Recovery: return (int)OriginalQuestKind.Recovery;
                case QuestType.Shenwei: return (int)OriginalQuestKind.Shenwei;
                case QuestType.ShouHu: return (int)OriginalQuestKind.ShouHu;
                case QuestType.WdLogin: return (int)OriginalQuestKind.WdLogin;
                case QuestType.JiuShen: return (int)OriginalQuestKind.JiuShen;
                default: return (int)OriginalQuestKind.Xuanshang;
            }
        }

        /// <summary>
        /// 一行任务条目。MirScrollView2 只在视口内创建/复用这个控件；
        /// 特效和标签都在构造期建好，Bind 只改文字、Visible、动画开关。
        /// </summary>
        private sealed class QuestTrackingItem : MirImageControl
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
                // 整行参与命中：offered 状态点行内任意空白处都能接取/寻路到 NPC。
                // 子标签（_findLabel / 可导航的 _taskLabels）命中优先级更高，点击照常走它们自己的 Click。
                NotControl = false;
                AutoSize = false;
                Size = new Size(Math.Max(40, owner._scrollView.Size.Width), RowHeight);
                Texture = KDefaultRes.DefaultTexture2D;
                DrawImage = false;

                Location = new Point(0, 0);
                Click += (s, e) =>
                {
                    if (_isOffered)
                    {
                        TryAcceptQuest();
                        return;
                    }
                    TryTeleportFirstTarget();
                };

                _nameLabel = owner.NewLabel(CommonColor.OriginalQuestTitle, true);
                _nameLabel.Parent = this;
                _nameLabel.NotControl = true;

                _findLabel = owner.NewLabel(CommonColor.White, true);
                _findLabel.Parent = this;
                _findLabel.RichText = true;   // 单标签富文本：白色"找到 " + 绿色下划线 NPC 名
                _findLabel.NotControl = true;

                for (int i = 0; i < MaxTaskLines; i++)
                {
                    _taskLabels[i] = owner.NewLabel(CommonColor.White, true);
                    _taskLabels[i].Parent = this;
                    _taskLabels[i].NotControl = true;
                }

                Effect = new MirAnimatedAtlasControl
                {
                    Parent = this,
                    NotControl = true,
                    Loop = true,
                    Animated = false,
                    ForeColour = CommonColor.White,
                    AutoSetupMovieClip = true,
                    Visible = false,
                    AtlasNameKey = QuestBorderSheetKey,
                    Location = new Point(0, 0),
                    DrawMode = MirImageDrawMode.NineGrid,
                    BorderLeft = 20,
                    BorderTop = 20,
                    BorderRight = 20,
                    BorderBottom = 20,
                };

                Reset();
            }

            private void SetLabel(MirLabel label, string text, int x, int y, Color colour, bool show)
            {
                label.Text = text ?? string.Empty;
                label.Location = new Point(x, y);
                label.ForeColour = colour;
                label.NotControl = true;
                label.Visible = show;
            }

            // 接取/寻路到 NPC：整行 Item 与 _findLabel 共用，仅在"可提供任务"状态生效。
            private void TryAcceptQuest()
            {
                if (_quest == null || !_isOffered) return;
                Network.Enqueue(new C.QuestCmd { Action = (byte)QuestCmdType.Offered, QuestIndex = _quest.Id });
            }

            // 传送到指定任务目标：行内子标签点各自的 target 时调用。
            private void TryTeleportTarget(QuestTaskTarget target)
            {
                if (_quest == null || _isOffered || target == null) return;
                Network.Enqueue(new C.TeleportToQuestTarget
                {
                    MapIndex = target.MapIndex,
                    X = target.X,
                    Y = target.Y,
                    MonsterIndex = target.MonsterIndex
                });
            }

            // 整行点击（进行中任务）：传送到第一个有效目标，没有则什么都不做。
            private void TryTeleportFirstTarget()
            {
                if (_quest == null || _isOffered) return;
                for (int i = 0; i < MaxTaskLines; i++)
                {
                    QuestTaskTarget target = _targets[i];
                    if (target == null) continue;
                    TryTeleportTarget(target);
                    return;
                }
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

                // 原始传奇渲染：getTaskTypeName(type) 前缀 + 任务名（textstyle 28/29）。
                SetLabel(_nameLabel, QuestTypeLabel.Decorate(entry.Quest.QuestInfo.Type, entry.Quest.QuestInfo.Name), 10, 10, CommonColor.OriginalQuestTitle, true);
                // 对齐原始传奇 addTaskEffect：r.x = e[t].x - 14, r.y = e[t].y - 13, scaleX=.75, scaleY=.8。
                Effect.Location = new Point(-14, -13);
                Effect.Visible = showEffect;
                Effect.Animated = showEffect;
                Effect.SizeScale = new KFramework.MonoGame.Vector2(0.75f, 0.8f);

                if (entry.IsOffered)
                {
                    string npcName = !string.IsNullOrEmpty(entry.Quest.QuestInfo.StartNpcName)
                        ? entry.Quest.QuestInfo.StartNpcName
                        : entry.Quest.QuestInfo.Name;
                    // 富文本：白色"找到 " + 亮绿色带下划线 NPC 名。
                    SetLabel(_findLabel, "找到 <color=" + CommonColor.LimeGreenHex + "><u>" + npcName + "</u></color>", 10, 30, CommonColor.White, true);
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
                    int lineY = 30 + 15 * line;
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
                            SetLabel(_taskLabels[line - 1], prefix, 10, lineY, CommonColor.White, true);
                            if (line >= MaxTaskLines) break;

                            int prefixW = TextRenderer.MeasureText(prefix, QuestFont).Width + 2;
                            _targets[line] = target;
                            SetLabel(_taskLabels[line], targetText, 10 + prefixW, lineY, CommonColor.Green, true);
                            line++;
                            continue;
                        }
                    }

                    bool navigable = target != null && target.MapIndex > 0;
                    SetLabel(_taskLabels[line - 1], text, 10, lineY, navigable ? CommonColor.Green : CommonColor.White, true);
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
                ForeColour = CommonColor.Blue,
                Location = new Point(0, 0),
                Size = new Size(265, 188),
            };

            mShowNode_Bg = new MirImageControl
            {
                Parent = m_ShowNode,
                AtlasNameKey = "y_lefttop-sheet",
                AtlasFrame = "lefttopbg",
                Location = new Point(40, 0),
                Size = new Size(202, 188)
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
                BackColour = CommonColor.Transparent,
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
                BackColour = CommonColor.Transparent,
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
