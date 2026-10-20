using System.Threading.Tasks;
using KFramework.MonoGame;

namespace KFramework.MonoGameExtend
{
    public sealed class KEngineComponent : DrawableGameComponent
    {
        /// <summary>场景树的默认更新顺序（尽量靠前，保证 KTime 在本帧其它组件之前刷新）。</summary>
        private const int DefaultUpdateOrder = -1000;

        private bool _inited;

        public KEngineComponent(Game game) : base(game)
        {
            UpdateOrder = DefaultUpdateOrder;
        }

        public bool EnableInput { get; set; } = true;
        public bool InputUseCanvasListener { get; set; } = false;
        public bool EnableScene { get; set; } = true;

        public override void Initialize()
        {
            EnsureInitialized();
        }

        public override Task LoadContentAsync()
        {
            EnsureInitialized();
            return base.LoadContentAsync();
        }

        public override void Update(GameTime gameTime)
        {
            EnsureInitialized();

            // 顺序与原先各游戏手写的一致：先输入（本帧的输入状态），再推进 KTime 与场景树。
            if (EnableInput) KInputMgr.Update(gameTime);
            if (EnableScene) KSceneMgr.Update(gameTime);
        }

        public override void Draw(GameTime gameTime)
        {
            EnsureInitialized();
            if (EnableScene) KSceneMgr.Draw(gameTime);
        }

        /// <summary>
        /// 延迟到首次真正运行时才初始化：组件可能在 Game 构造函数里就被加上，那时设备 / SpriteBatch 还没建。
        /// 两个 Init 都是幂等的，重复调用安全。
        /// </summary>
        private void EnsureInitialized()
        {
            if (_inited) return;
            _inited = true;

            KInputMgr.Init(InputUseCanvasListener);
            KSceneMgr.Init(Game);
        }
    }
}
