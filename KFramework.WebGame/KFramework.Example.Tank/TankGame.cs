using KFramework.MonoGame;
using KFramework.MonoGameExtend;

namespace KFramework.Example.Tank
{

    /// <summary>
    /// 坦克大战的宿主：组件式接入 MonoGameExtend —— 只挂一个 <see cref="KEngineComponent"/>，
    /// 输入、KTime、场景树、KTween / KTimer 就都由它每帧驱动，本类只负责内容与场景。
    /// 对应 PixiJS 版的 CreationEngine + GameScene。
    /// </summary>
    public sealed class TankGame : Game
    {
        public TankGame() : base(GraphicsBackendKind.WebGL20)
        {
            // 挂上即接管：KInputMgr（并自动激活键鼠/触摸）→ KTime → 场景树 → 树上所有系统（KTween / KTimer / UI）。
            // 组件在首次真正运行时才初始化，因此这里写在构造函数里是安全的（那时设备还没建）。
            Components.Add(new KEngineComponent(this));
        }

        protected override async Task LoadContentAsync()
        {
            // 节点树 UI 文本需要默认字体
            KDefaultRes.DefaultSpriteFont = new SpriteFont(GraphicsDevice, 40f);

            await ContentManager.Default.LoadAsync().ConfigureAwait(false);

            KSceneMgr.SetMainScene(new GameScene());
        }
    }

}
