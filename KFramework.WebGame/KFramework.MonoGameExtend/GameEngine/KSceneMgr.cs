using KFramework.MonoGame;
using System;
using System.Collections.Generic;

namespace KFramework.MonoGameExtend
{
    public static class KSceneMgr
    {
        // 遍历方式和 KTransformHelper.Do_Update_AllChildList 保持一致：
        // 不用 foreach / 枚举器（LinkedList 的枚举器会做版本校验，遍历中增删节点会抛
        // InvalidOperation_EnumFailedVersion），而是手动拿 LinkedListNode 走链表：
        //   1. 先缓存 next，再处理当前节点 —— 当前节点在 Update / Draw 里被摘掉也不会断链；
        //   2. 处理前判一次 node.List != null —— 跳过本次遍历中已被其它场景摘掉的节点。
        // 这样场景在自己的 Update / Draw 里切主场景（SetMainScene）增删链表都是安全的。
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

            LinkedListNode<KSceneBase> mEntry = mSceneList.First;
            while (mEntry != null)
            {
                LinkedListNode<KSceneBase> next = mEntry.Next;
                mEntry.Value.Update();
                mEntry = next;
            }

            KTransformHelper.Do_Update_AllChildList(DontDestroyOnLoadRoot);
        }

        public static void Draw(GameTime gameTime)
        {
            // 真实帧率统计：只有画出去的帧才计数（deltaTime 在固定步长下是恒定的，不能拿来算 FPS）
            KTime.StepRenderFrame();

            LinkedListNode<KSceneBase> mEntry = mSceneList.First;
            while (mEntry != null)
            {
                LinkedListNode<KSceneBase> next = mEntry.Next;
                mEntry.Value.Draw();
                mEntry = next;
            }

            KTransformHelper.Do_Draw_AllChildList(DontDestroyOnLoadRoot);
        }

        public static void AddScene(KSceneBase mScene)
        {
            if (mScene == null || mScene.SceneEntry.List != null)
            {
                return;
            }

            mScene.LoadContent();
            mSceneList.AddLast(mScene.SceneEntry);
        }

        public static void RemoveScene(KSceneBase mScene)
        {
            if (mScene == null || mScene.SceneEntry.List == null)
            {
                return;
            }

            // 先摘链再 Dispose：遍历时 next 已缓存，摘掉当前节点也不会断链；
            // 且 Dispose 里若再次调用 RemoveScene，SceneEntry.List 已是 null，直接被挡掉。
            mSceneList.Remove(mScene.SceneEntry);
            mScene.Dispose();
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
