using Client.MirControls;
using Client.MirGraphics;
using Client.MirSounds;

namespace Client.MirScenes.Dialogs
{

    public sealed class QuestDiaryDialog : MirImageControl
    {
        public List<ClientQuestProgress> Quests = new List<ClientQuestProgress>();
        public List<QuestGroupQuestItem> TaskGroups = new List<QuestGroupQuestItem>();

        public List<string> ExpandedGroups = new List<string>();

        private MirButton _closeButton;
        private MirLabel _takenQuestsLabel;

        public QuestDiaryDialog()
        {
            Index = 961;
            Library = Libraries.Prguse;
            Movable = true;
            Sort = true;
            Location = new Point(Settings.ScreenWidth / 2 - 300 - 20, 60);

            MirImageControl TitleLabel = new MirImageControl
            {
                Index = 15,
                Library = Libraries.Title,
                Location = new Point(18, 9),
                Parent = this
            };

            _takenQuestsLabel = new MirLabel
            {
                Font = new Font(Settings.FontName, 8F),
                Parent = this,
                AutoSize = true,
                Location = new Point(210, 7)
            };

            _closeButton = new MirButton
            {
                Index = 193,
                HoverIndex = 194,
                PressedIndex = 195,
                Library = Libraries.Title,
                Parent = this,
                Location = new Point(200, 436),
                Sound = SoundList.ButtonA,
            };
            _closeButton.Click += (o, e) => Hide();

            MirButton closeButton = new MirButton
            {
                Index = 360,
                HoverIndex = 361,
                PressedIndex = 362,
                Library = Libraries.Prguse2,
                Parent = this,
                Location = new Point(289, 3),
                Sound = SoundList.ButtonA,
            };
            closeButton.Click += (o, e) => Hide();

        }

        public void DisplayQuests()
        {
            ClearLog();

            Quests = GameScene.User.CurrentQuests;

            _takenQuestsLabel.Text = GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.TakenQuestsList), Quests.Count, Globals.MaxConcurrentQuests);

            var groupedQuests = Quests.GroupBy(d => d.QuestInfo.Group).ToList();

            int nextY = 40;

            foreach (var group in groupedQuests)
            {
                List<ClientQuestProgress> singleGroup = @group.ToList();

                bool expanded = ExpandedGroups.Count <= 0 || ExpandedGroups.Contains(@group.Key);

                QuestGroupQuestItem groupQuest = new QuestGroupQuestItem(group.Key, singleGroup, expanded)
                {
                    Parent = this,
                    Visible = true,
                    Location = new Point(15, nextY),
                };
                groupQuest.ExpandedChanged += (o, e) =>
                {
                    nextY = 40;

                    foreach (QuestGroupQuestItem task in TaskGroups)
                    {
                        task.Location = new Point(15, nextY);
                        nextY += task.SizeY;

                        if (task.Expanded)
                        {
                            if (!ExpandedGroups.Contains(task.Group))
                                ExpandedGroups.Add(task.Group);
                        }
                        else
                            ExpandedGroups.Remove(task.Group);
                    }
                };
                groupQuest.SelectedQuestChanged += (o, e) =>
                {
                    QuestSingleQuestItem singleQuestItem = (QuestSingleQuestItem)o;

                    if (singleQuestItem == null) return;

                    foreach (QuestGroupQuestItem item in TaskGroups)
                    {
                        item.DeselectQuests();
                    }

                    singleQuestItem.Selected = true;
                };

                nextY += groupQuest.SizeY;

                TaskGroups.Add(groupQuest);
            }
        }

        public void ClearLog()
        {
            foreach (QuestGroupQuestItem taskGroupItem in TaskGroups)
            {
                taskGroupItem.Dispose();
            }
            TaskGroups.Clear();
        }
        public override void Show()
        {
            if (Visible) return;
            Visible = true;

            DisplayQuests();
        }

        public void Toggle()
        {
            if (!Visible)
                Show();
            else
                Hide();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            Quests.Clear();
            ClearLog();
        }
    }

}
