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
        public List<int> TrackedQuestsIds = new List<int>();
        public List<MirLabel> TaskLines = new List<MirLabel>();

        public Font QuestFont = new Font(Settings.FontName, 12F);

        private MirImageControl m_ShowNode;
        private MirButton m_HideNode;

        // CDN 上的 ebc12760_8637.png 反解析自 manifest：
        //   resource/assets/gameui4/movieClip/y_task_complete.png
        // 配套描述 y_task_complete.json（frameRate=10，10 帧）走 Mir2AtlasLibrary 配置化加载。
        // 原始传奇 JS 的做法：每个任务条目是一个 render item，特效放独立 eff_group，
        // 按 item.x-14 / item.y-13 定位（原版 scale .75/.8，本工程按条目高度缩到 .55）。
        // 这里等价为每个 Item 自带特效。
        private const string QuestBorderSheetKey = "resource/assets/gameui4/movieClip/y_task_complete.json";
        // 原版 JS 是 scaleX=.75 / scaleY=.8（单帧 274x86 → 显示约 217x80），
        // 但它的条目高 42+19*goals（61~99px）；我们条目只有 45~75px，按 .8 会盖住下一条，
        // 故缩到 .55（约 159x55），刚好框住一条「寻找 NPC」/已完成任务。
        private const float QuestBorderScale = 0.55f;
        private readonly List<QuestTrackingItem> _items = new List<QuestTrackingItem>();

        /// <summary>
        /// 一个任务条目 = 任务名 + 若干内容行（或「寻找 NPC」提示）+ 可选金色边框特效。
        /// 对应原始传奇 taskRenderDic 里的一个 render item；X/Y 就是 item.x / item.y。
        /// 条目的文字、点击、传送、特效全部自己负责，Dialog 只管排布。
        /// </summary>
        private sealed class QuestTrackingItem
        {
            private readonly QuestTrackingDialog _owner;
            private readonly List<MirLabel> _labels = new List<MirLabel>();

            public ClientQuestProgress Quest { get; }
            public int X { get; }
            public int Y { get; }
            public int Height { get; private set; }
            public bool IsOffered { get; }
            public MirAnimatedAtlasControl Effect { get; private set; }

            public QuestTrackingItem(QuestTrackingDialog owner, ClientQuestProgress quest, int x, int y, bool isOffered)
            {
                _owner = owner;
                Quest = quest;
                X = x;
                Y = y;
                IsOffered = isOffered;
            }

            public void Build(bool showEffect)
            {
                if (showEffect)
                    Effect = _owner.CreateQuestBorderEffect(X, Y);

                AddLabel(Quest.QuestInfo.Name, X, Y, Color.LimeGreen, true);

                if (IsOffered)
                    BuildOfferedHint();
                else
                    BuildTaskLines();
            }

            private void BuildOfferedHint()
            {
                string npcName = !string.IsNullOrEmpty(Quest.QuestInfo.StartNpcName)
                    ? Quest.QuestInfo.StartNpcName
                    : Quest.QuestInfo.Name;

                var label = AddLabel("寻找 " + npcName, X, Y + 20, Color.Gold, false);
                label.UnderLine = true;

                ClientQuestProgress q = Quest;
                label.Click += (s, e) =>
                {
                    PrintTool.Log($"offeredLabel.Click quest={q.Id}");
                    Network.Enqueue(new AcceptQuest { QuestIndex = q.Id, Navigate = true });
                };

                Height = 45;
            }

            private void BuildTaskLines()
            {
                int ti = 0;
                foreach (string text in Quest.TaskList)
                {
                    string line = text;
                    int lineY = Y + 20 + 15 * ti;
                    QuestTaskTarget tgt = (Quest.Targets != null && ti < Quest.Targets.Count) ? Quest.Targets[ti] : null;
                    ti++;

                    if (tgt != null && tgt.MapIndex > 0 && !string.IsNullOrEmpty(tgt.Name))
                    {
                        int idx = line.IndexOf(tgt.Name, StringComparison.Ordinal);
                        if (idx >= 0)
                        {
                            string prefix = line.Substring(0, idx);
                            string tgtText = line.Substring(idx);
                            AddLabel(prefix, X, lineY, Color.White, true);

                            int prefixW = TextRenderer.MeasureText(prefix, _owner.QuestFont).Width + 2;
                            var tgtLabel = AddLabel(tgtText, X + prefixW, lineY, Color.Green, false);
                            tgtLabel.UnderLine = true;
                            BindTeleport(tgtLabel, tgt, "tgtLabel.Click");
                            continue;
                        }
                    }

                    bool navigable = tgt != null && tgt.MapIndex > 0;
                    var taskLabel = AddLabel(line, X, lineY, navigable ? Color.Green : Color.White, !navigable);
                    taskLabel.UnderLine = navigable;
                    if (navigable)
                        BindTeleport(taskLabel, tgt, "questTaskLabel.Click");
                }

                Height = 15 * ti + 30;
            }

            private void BindTeleport(MirLabel label, QuestTaskTarget tgt, string logTag)
            {
                label.Click += (s, e) =>
                {
                    KFramework.MonoGame.PrintTool.Log($"{logTag} map={tgt.MapIndex} x={tgt.X} y={tgt.Y} monster={tgt.MonsterIndex}");
                    Network.Enqueue(new TeleportToQuestTarget
                    {
                        MapIndex = tgt.MapIndex,
                        X = tgt.X,
                        Y = tgt.Y,
                        MonsterIndex = tgt.MonsterIndex
                    });
                };
            }

            private MirLabel AddLabel(string text, int x, int y, Color colour, bool notControl)
            {
                var label = new MirLabel
                {
                    Text = text,
                    AutoSize = true,
                    BackColour = Color.Transparent,
                    Font = _owner.QuestFont,
                    ForeColour = colour,
                    Location = new Point(x, y),
                    OutLine = true,
                    NotControl = notControl,
                    Parent = _owner,
                    Visible = true,
                };
                _labels.Add(label);
                _owner.TaskLines.Add(label);
                return label;
            }

            public void Dispose()
            {
                foreach (MirLabel label in _labels)
                {
                    label.Parent = null;
                    label.Dispose();
                }
                _labels.Clear();

                if (Effect != null)
                {
                    Effect.Parent = null;
                    Effect.Dispose();
                    Effect = null;
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

            m_HideNode.Click += (o, e) =>
            {
                m_ShowNode.Visible = true;
                m_HideNode.Visible = false;
            };

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

            var mShowNode_Bg = new MirImageControl
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
            mShowNode_HideBtn.Click += (o, e) =>
            {
                m_ShowNode.Visible = false;
                m_HideNode.Visible = true;
                KFramework.MonoGame.PrintTool.Log("mShowNode_HideBtn.Click");
            };

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

        }
        
        private MirAnimatedAtlasControl CreateQuestBorderEffect(int itemX, int itemY)
        {
            var ani = new MirAnimatedAtlasControl
            {
                Parent = this,
                NotControl = true,
                Location = new Point(itemX, itemY),
                Loop = true,
                Animated = true,
                ForeColour = Color.White,
                AutoSetupMovieClip = true,
                AtlasNameKey = QuestBorderSheetKey
            };
            return ani;
        }

        private int AddItem(int x, int y, ClientQuestProgress quest, bool isOffered, bool showEffect)
        {
            var item = new QuestTrackingItem(this, quest, x, y, isOffered);
            item.Build(showEffect);
            _items.Add(item);
            return item.Height;
        }

        public void DisplayQuests()
        {
            foreach (MirLabel label in TaskLines)
            {
                label.Parent = null;
                label.Dispose();
            }
            TaskLines.Clear();

            foreach (QuestTrackingItem item in _items)
            {
                item.Dispose();
            }
            _items.Clear();

            List<ClientQuestProgress> questsToTrack = new List<ClientQuestProgress>();

            questsToTrack.Clear();
            questsToTrack.AddRange(from quest in MapObject.User.CurrentQuests from id in TrackedQuestsIds.Where(id => quest.Id == id) select quest);
            if (questsToTrack.Count < 1 && MapObject.User.OfferedQuests.Count < 1)
            {
                SyncWithNpcPanels();
                return;
            }

            int y = 0;

            for (int i = 0; i < questsToTrack.Count; i++)
            {
                ClientQuestProgress trackedInfo = questsToTrack[i];
                y += AddItem(45, 20 + y, trackedInfo, false, trackedInfo.Completed);
                if (i >= 5)
                {
                    break;
                }
            }

            if (MapObject.User.OfferedQuests.Count > 0 && questsToTrack.Count > 0)
            {
                y += 15;
            }

            foreach (ClientQuestProgress offered in MapObject.User.OfferedQuests)
            {
                if (offered.QuestInfo == null)
                {
                    continue;
                }
                y += AddItem(45, 20 + y, offered, true, true);
            }

            SyncWithNpcPanels();
        }

        /// <summary>fromServer=true 表示这次变化来自服务器（登录恢复或 S.TrackQuest 确认），不再回包。</summary>
        public void AddQuest(ClientQuestProgress quest, bool fromServer = false)
        {
            if (TrackedQuestsIds.Any(d => d == quest.Id) || TrackedQuestsIds.Count >= 5)
            {
                return;
            }

            TrackedQuestsIds.Add(quest.Id);
            DisplayQuests();

            if (!fromServer)
                Network.Enqueue(new C.TrackQuest { QuestIndex = quest.Id, Track = true });
        }

        public void RemoveQuest(ClientQuestProgress quest, bool fromServer = false)
        {
            if (!TrackedQuestsIds.Remove(quest.Id)) return;
            DisplayQuests();

            if (!fromServer)
                Network.Enqueue(new C.TrackQuest { QuestIndex = quest.Id, Track = false });
        }

        public void SyncWithNpcPanels()
        {
            Visible = !(GameScene.Scene.NPCDialog.Visible || GameScene.Scene.QuestListDialog.Visible);
        }

    }

}
