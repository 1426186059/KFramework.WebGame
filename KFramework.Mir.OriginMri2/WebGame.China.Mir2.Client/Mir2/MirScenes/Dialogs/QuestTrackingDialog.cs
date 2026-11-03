using Client.MirControls;
using Client.MirObjects;

namespace Client.MirScenes.Dialogs
{

    public sealed class QuestTrackingDialog : MirImageControl
    {
        public List<int> TrackedQuestsIds = new List<int>();
        public List<MirLabel> TaskLines = new List<MirLabel>();

        public Font QuestFont = new Font(Settings.FontName, 8F);

        private MirLabel _questNameLabel, _questTaskLabel;

        public QuestTrackingDialog()
        {
            Movable = true;
            Location = new Point(0, 100);
            Sort = false;
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
                Hide();
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
                    Location = new Point(5, 20 + y),
                    OutLine = true,
                    Parent = this,
                    Visible = true,
                };

                TaskLines.Add(_questNameLabel);

                foreach (string questToTrack in questsToTrack[i].TaskList)
                {
                    y += 15;

                    string trackedQuest = questToTrack;
                    _questTaskLabel = new MirLabel
                    {
                        Text = trackedQuest,
                        AutoSize = true,
                        BackColour = Color.Transparent,
                        Font = QuestFont,
                        ForeColour = Color.White, //trackedQuest.Contains("(Completed)") ? Color.LimeGreen : 
                        Location = new Point(25, 20 + y),
                        OutLine = true,
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
