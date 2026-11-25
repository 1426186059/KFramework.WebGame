using Client.MirControls;
using Client.MirGraphics;
using Client.MirNetwork;
using Client.MirSounds;
using C = ClientPackets;

namespace Client.MirScenes.Dialogs
{

    public sealed class QuestDetailDialog : MirImageControl
    {
        private readonly MirButton _pauseButton;

        public ClientQuestProgress Quest;
        public QuestMessage Message;
        public QuestRewards Reward;

        public QuestDetailDialog()
        {
            Index = 960;
            Library = Libraries.Prguse;
            Movable = true;
            Sort = true;
            Location = new Point(Settings.ScreenWidth / 2 + 20, 60);

            MirImageControl TitleLabel = new MirImageControl
            {
                Index = 16,
                Library = Libraries.Title,
                Location = new Point(18, 9),
                Parent = this
            };


            MirButton upButton = new MirButton
            {
                Index = 197,
                HoverIndex = 198,
                PressedIndex = 199,
                Library = Libraries.Prguse2,
                Parent = this,
                Size = new Size(16, 14),
                Location = new Point(293, 33),
                Sound = SoundList.ButtonA,
                Visible = true
            };

            MirButton downButton = new MirButton
            {
                Index = 207,
                HoverIndex = 208,
                Library = Libraries.Prguse2,
                PressedIndex = 209,
                Parent = this,
                Size = new Size(16, 14),
                Location = new Point(293, 280),
                Sound = SoundList.ButtonA,
                Visible = true
            };

            MirButton positionBar = new MirButton
            {
                Index = 205,
                HoverIndex = 206,
                PressedIndex = 206,
                Library = Libraries.Prguse2,
                Location = new Point(293, 48),
                Parent = this,
                Movable = true,
                Sound = SoundList.None,
                Visible = false
            };

            Message = new QuestMessage(upButton, downButton, positionBar, 16, true)
            {
                Font = FontConst.F9,
                Parent = this,
                Size = new Size(280, 320),
                Location = new Point(10, 35),
                PosMinY = 46,
                PosMaxY = 261
            };



            Reward = new QuestRewards
            {
                Parent = this,
                Size = new Size(315, 130),
                Location = new Point(5, 307)
            };



            _pauseButton = new MirButton
            {
                Index = 270,
                HoverIndex = 271,
                PressedIndex = 272,
                Library = Libraries.Title,
                Parent = this,
                Location = new Point(120, 436),
                Sound = SoundList.ButtonA,
                Visible = false
            };

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

            //MirButton helpButton = new MirButton
            //{
            //    Index = 257,
            //    HoverIndex = 258,
            //    PressedIndex = 259,
            //    Library = Libraries.Prguse2,
            //    Parent = this,
            //    Location = new Point(266, 3),
            //    Sound = SoundList.ButtonA,
            //};
            //helpButton.Click += (o, e) => GameScene.Scene.HelpDialog.DisplayPage("Quests");

        }

        public void DisplayQuestDetails(ClientQuestProgress quest)
        {
            if (quest == null) return;

            Quest = quest;
            Reward.UpdateRewards(Quest);
            Message.UpdateQuest(Quest);

            Show();
        }

    }

}
