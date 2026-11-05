using Client.MirControls;
using Client.MirObjects;
using KFramework.MonoGameExtend;
using Client.MirNetwork;
using Client.MirGraphics;
using ClientPackets;

namespace Client.MirScenes.Dialogs
{

    public sealed class QuestTrackingDialog : MirImageControl
    {
        public List<int> TrackedQuestsIds = new List<int>();
        public List<MirLabel> TaskLines = new List<MirLabel>();

        public Font QuestFont = new Font(Settings.FontName, 12F);

        private MirLabel _questNameLabel, _questTaskLabel;
        private MirImageControl m_ShowNode;
        private MirButton m_HideNode;

        public QuestTrackingDialog()
        {
            Location = new Point(0, 50);
            AutoSize = false;
            Size = new Size(265, 188);
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

        public void DisplayQuests()
        {
            foreach (MirLabel label in TaskLines)
                label.Dispose();

            List<ClientQuestProgress> questsToTrack = new List<ClientQuestProgress>();

            questsToTrack.Clear();

            questsToTrack.AddRange(from quest in MapObject.User.CurrentQuests from id in TrackedQuestsIds.Where(id => quest.Id == id) select quest);

            if (questsToTrack.Count < 1)
            {
                // 无任务也常驻左上角显示（与原始传奇一致）：仅清空任务行，不隐藏整个面板。
                Show();
                return;
            }

            int y = 0;

            for (int i = 0; i < questsToTrack.Count; i++)
            {
                _questNameLabel = new MirLabel
                {
                    Text = questsToTrack[i].QuestInfo.Name,
                    AutoSize = true,
                    BackColour = Color.Transparent,
                    Font = QuestFont,
                    ForeColour = Color.LimeGreen,
                    Location = new Point(45, 20 + y),
                    OutLine = true,
                    NotControl = true,
                    Parent = this,
                    Visible = true,
                };

                TaskLines.Add(_questNameLabel);

                int ti = 0;
                foreach (string questToTrack in questsToTrack[i].TaskList)
                {
                    y += 15;

                    string trackedQuest = questToTrack;
                    QuestTaskTarget tgt = (questsToTrack[i].Targets != null && ti < questsToTrack[i].Targets.Count) ? questsToTrack[i].Targets[ti] : null;
                    ti++;

                    if (tgt != null && !string.IsNullOrEmpty(tgt.Name))
                    {
                        int idx = trackedQuest.IndexOf(tgt.Name, System.StringComparison.Ordinal);

                        if (idx >= 0)
                        {
                            // 拆分：前缀（白） + 目标名+进度（绿、下划线、可点击传送）
                            string prefix = trackedQuest.Substring(0, idx);
                            string tgtText = trackedQuest.Substring(idx);

                            var prefixLabel = new MirLabel
                            {
                                Text = prefix,
                                AutoSize = true,
                                BackColour = Color.Transparent,
                                Font = QuestFont,
                                ForeColour = Color.White,
                                Location = new Point(45, 25 + y),
                                OutLine = true,
                                NotControl = true,
                                Parent = this,
                                Visible = true
                            };
                            TaskLines.Add(prefixLabel);

                            int prefixW = TextRenderer.MeasureText(prefix, QuestFont).Width + 2;
                            var tgtLabel = new MirLabel
                            {
                                Text = tgtText,
                                
                                AutoSize = true,
                                BackColour = Color.Transparent,
                                Font = QuestFont,
                                ForeColour = Color.Green,
                                Location = new Point(45 + prefixW, 25 + y),
                                OutLine = true,
                                UnderLine = true,
                                Parent = this,
                                Visible = true
                            };
                            tgtLabel.Click += (s, e) =>
                            {
                                KFramework.MonoGame.PrintTool.Log($"tgtLabel.Click map={tgt.MapIndex} x={tgt.X} y={tgt.Y}");
                                Network.Enqueue(new TeleportToQuestTarget
                                {
                                    MapIndex = tgt.MapIndex,
                                    X = tgt.X,
                                    Y = tgt.Y
                                });
                            };
                            TaskLines.Add(tgtLabel);
                            continue;
                        }

                        // 目标名未出现在文本中（如自定义对话描述）：整行绿色下划线可点击
                        _questTaskLabel = new MirLabel
                        {
                            Text = trackedQuest,
                            AutoSize = true,
                            BackColour = Color.Transparent,
                            Font = QuestFont,
                            ForeColour = Color.Green,
                            Location = new Point(45, 25 + y),
                            OutLine = true,
                            UnderLine = true,
                            Parent = this,
                            Visible = true,
                        };
                        _questTaskLabel.Click += (s, e) =>
                        {
                            KFramework.MonoGame.PrintTool.Log($"questTaskLabel.Click map={tgt.MapIndex} x={tgt.X} y={tgt.Y}");
                            Network.Enqueue(new TeleportToQuestTarget
                            {
                                MapIndex = tgt.MapIndex,
                                X = tgt.X,
                                Y = tgt.Y
                            });
                        };
                        TaskLines.Add(_questTaskLabel);
                        continue;
                    }

                    _questTaskLabel = new MirLabel
                    {
                        Text = trackedQuest,
                        AutoSize = true,
                        BackColour = Color.Transparent,
                        Font = QuestFont,
                        ForeColour = Color.White, //trackedQuest.Contains("(Completed)") ? Color.LimeGreen : 
                        Location = new Point(45, 25 + y),
                        OutLine = true,
                        NotControl = true,
                        Parent = this,
                        Visible = true,
                    };

                    TaskLines.Add(_questTaskLabel);
                }

                if (i >= 5) break;

                y += 30;
            }

            //Size = new Size(150, 20 + y + 30);

            Show();
        }

        public void AddQuest(ClientQuestProgress quest, bool New = false)
        {
            if (TrackedQuestsIds.Any(d => d == quest.Id) || TrackedQuestsIds.Count >= 5) return;

            TrackedQuestsIds.Add(quest.Id);

            DisplayQuests();
            if (!New)
                UpdateTrackedQuests();
        }

        public void RemoveQuest(ClientQuestProgress quest)
        {
            TrackedQuestsIds.Remove(quest.Id);

            DisplayQuests();
            UpdateTrackedQuests();
        }

        public void UpdateTrackedQuests()
        {
            for (int j = 0; j < Settings.TrackedQuests.Length; j++)
            {
                if (TrackedQuestsIds.Count > 0 && j < TrackedQuestsIds.Count)
                {
                    Settings.TrackedQuests[j] = TrackedQuestsIds[j];
                    continue;
                }
                Settings.TrackedQuests[j] = -1;
            }

            Settings.SaveTrackedQuests(GameScene.User.Name);
        }

    }

}
