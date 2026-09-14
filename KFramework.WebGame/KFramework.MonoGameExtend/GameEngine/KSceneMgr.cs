using KFramework.MonoGame;
using System;
using System.Collections.Generic;

namespace KFramework.MonoGameExtend
{
    public static class KSceneMgr
    {
        static readonly LinkedList<KSceneBase> mSceneList = new LinkedList<KSceneBase>();

        public static readonly KTransform DontDestroyOnLoadRoot = new KTransform();

        public static event EventHandler<EventArgs> ScreenSizeChanged;

        public static SpriteBatch SpriteBatch { get; set; } = null;
        public static Game Game { get; set; } = null;


#if DEBUG
        private static MetricsScreen _cacheMetricsScreen;
#endif
        private static KSceneBase m_Main;
        public static KSceneBase Main
        {
            get
            {
                return m_Main;
            }
        }

        public static void Init(Game mGame)
        {
            Game = mGame;
            SpriteBatch = new SpriteBatch(mGame.GraphicsDevice);
            // 浏览器窗口尺寸变化通知：Game.TickFrame 里 GraphicsDevice.SyncCanvasSize 检测到
            // 画布变化后会触发 GameWindow.SizeChanged，这里转发到 ScreenSizeChanged，
            // KCanvas 据此重算画布尺寸并把 UI 子控件级联重排，实现自适应屏幕。
            mGame.Window.SizeChanged += OnWindowSizeChangedForward;
        }

        private static void OnWindowSizeChangedForward() => OnScreenSizeChanged(Game, EventArgs.Empty);

        public static void SetMainScene(KSceneBase mainScene)
        {
            KSceneBase oldScene = m_Main;
            if (oldScene != null)
            {
                RemoveScene(oldScene);
            }
            
            m_Main = mainScene;
            AddScene(m_Main);

#if DEBUG
            if (_cacheMetricsScreen == null)
            {
                _cacheMetricsScreen = new MetricsScreen();
            }
#endif

        }

        public static void Update(GameTime gameTime)
        {
            KTime.From(gameTime);

            foreach (var v in mSceneList)
            {
                v.Update();
            }

            KTransformHelper.Do_Update_AllChildList(DontDestroyOnLoadRoot);
        }

        public static void Draw(GameTime gameTime)
        {
            foreach (var v in mSceneList)
            {
                v.Draw();
            }

            KTransformHelper.Do_Draw_AllChildList(DontDestroyOnLoadRoot);
        }

        public static void AddScene(KSceneBase mScene)
        {
            mScene.LoadContent();
            mSceneList.AddLast(mScene.SceneEntry);
        }

        public static void RemoveScene(KSceneBase mScene)
        {
            mScene.Dispose();
            mSceneList.Remove(mScene.SceneEntry);
        }

        public static void GetScene()
        {

        }

        public static void OnScreenSizeChanged(object sender, EventArgs e)
        {
            ScreenSizeChanged?.Invoke(sender, e);
        }

    }
}
