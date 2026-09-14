
namespace KFramework.Example3
{
    internal class MainScene : KSceneBase
    {
        public SpriteFont Font1 { get; set; }
        public new SpriteFont Font2 { get; set; }

        public TestScreen mTestScreen;

        public int nLevelIndex;
        public Level mLevel;

        public override void LoadContent()
        {
            Font1 = new SpriteFont(KSceneMgr.Game.GraphicsDevice, 28f);
            Font2 = new SpriteFont(KSceneMgr.Game.GraphicsDevice, 28f);

            KDefaultRes.DefaultSpriteFont = Font2;

            nLevelIndex = 0;
            mLevel = new Level(nLevelIndex);
            //new TestScreen();
        }

        public override void Update()
        {
            if (mLevel != null)
            {
                mLevel.Update();
            }
        }

        public override void Draw()
        {
            if (mLevel != null)
            {
                mLevel.Draw();
            }
        }

        public void ReloadCurrentLevel()
        {
            LoadLevel();
        }

        public void LoadNextLevel()
        {
            nLevelIndex++;
            LoadLevel();
        }

        public void LoadLevel()
        {
            if (mLevel != null)
            {
                mLevel.Dispose();
            }

            mLevel = new Level(nLevelIndex);

            //var levelFileName = Path.GetFileName(levelPath);
            //var leaderboardFileName = Path.ChangeExtension(levelFileName, ".json");
            //leaderboardManager.Storage.SettingsFileName = leaderboardFileName;
            //level.LeaderboardManager = leaderboardManager;
            //endOfLevelMessgeState = EndOfLevelMessageState.NotShowing;
        }
    }
}
