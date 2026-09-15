
using System;
using System.Threading.Tasks;

namespace KFramework.Example3
{
    internal class MainScene : KSceneBase
    {
        public SpriteFont Font1 { get; set; }
        public new SpriteFont Font2 { get; set; }

        public TestScreen mTestScreen;

        public int nLevelIndex;
        public Level? mLevel;

        public override void LoadContent()
        {
            Font1 = new SpriteFont(KSceneMgr.Game.GraphicsDevice, 28f);
            Font2 = new SpriteFont(KSceneMgr.Game.GraphicsDevice, 28f);

            KDefaultRes.DefaultSpriteFont = Font2;

            nLevelIndex = 0;
            _ = LoadLevelAsync();
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

        public void ReloadCurrentLevel() => _ = LoadLevelAsync();

        public void LoadNextLevel()
        {
            nLevelIndex++;
            _ = LoadLevelAsync();
        }

        public void LoadLevel() => _ = LoadLevelAsync();

        // 自增令牌：连续触发（切关 / 缩放）时只保留最后一次加载结果，避免竞态与重复释放
        private int _loadToken;

        private async Task LoadLevelAsync()
        {
            int myToken = ++_loadToken;

            Level? old = mLevel;
            mLevel = null;
            old?.Dispose();

            Level? level = null;
            try
            {
                level = await Level.LoadAsync(nLevelIndex).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[MainScene] 关卡 {nLevelIndex} 下载/加载失败：{ex}");
                return;
            }

            if (myToken != _loadToken)
            {
                level.Dispose();
                return;
            }
            mLevel = level;
        }
    }
}
