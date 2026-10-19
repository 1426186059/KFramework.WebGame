using KFramework.MonoGame;

namespace KFramework.MonoGameExtend
{
    /// <summary>
    /// 把整个 MonoGameExtend 运行时打包成一个 <see cref="GameComponent"/>，挂到 <see cref="Game"/> 上即可用。
    /// <para>
    /// 它一次性接管的链路是：<c>KInputMgr</c>（指针分发）→ <c>KTime</c>（deltaTime / timeScale）
    /// → <c>KSceneMgr</c>（主场景 + <c>DontDestroyOnLoadRoot</c> 节点树）→ 树上的各个系统。
    /// 也就是说，<b>KTween / KTimer / KUpdateMgr / KUIRoot / KCanvas / KWidget 全都不用单独做成组件</b>：
    /// 它们要么是 <see cref="SingleTonMonoBehaviour{T}"/>（构造时把自己挂到 KSceneMgr.DontDestroyOnLoadRoot 下），
    /// 要么是节点树上的 KTransform，随本组件的 <see cref="Update"/> / <see cref="Draw"/> 被递归驱动。
    /// </para>
    /// <para>
    /// 用法（构造函数或 <c>Initialize()</c> 里都行，Initialize 时设备与 SpriteBatch 已就绪）：
    /// <code>
    /// public MyGame() : base(GraphicsBackendKind.WebGL20)
    /// {
    ///     Components.Add(new KEngineComponent(this));
    /// }
    /// </code>
    /// 之后游戏只需要在 <c>LoadContentAsync</c> 里加载内容、<c>KSceneMgr.SetMainScene(...)</c> 切场景，
    /// 不必再重写 Update / Draw 去手动转发 <c>KSceneMgr.Update/Draw</c> / <c>KInputMgr.Update</c>。
    /// </para>
    /// <para>
    /// 注意执行时序：<see cref="Game"/> 在 <c>TickFrame</c> 里的顺序是
    /// <c>Input.Update → Game.Update → Components.Update → Input.LateUpdate → Clear → Game.Draw → Components.Draw → EndFrame</c>。
    /// 所以本组件（<see cref="GameComponent.UpdateOrder"/> 默认 -1000）会先于其它组件跑完 KTime 与场景树，
    /// 之后添加的组件即可直接读到本帧的 <c>KTime.deltaTime</c>；绘制则发生在 <see cref="Game.Draw"/> 之后。
    /// 若希望别的组件先改状态、场景后更新，把本组件的 UpdateOrder 调大即可。
    /// </para>
    /// </summary>
    public sealed class KEngineComponent : DrawableGameComponent
    {
        /// <summary>场景树的默认更新顺序（尽量靠前，保证 KTime 在本帧其它组件之前刷新）。</summary>
        private const int DefaultUpdateOrder = -1000;

        private bool _inited;

        public KEngineComponent(Game game) : base(game)
        {
            UpdateOrder = DefaultUpdateOrder;
        }

        /// <summary>是否驱动 <c>KInputMgr.Update</c>（指针/点击分发）。自己接管输入时置 false。</summary>
        public bool EnableInput { get; set; } = true;

        /// <summary>
        /// 键盘监听绑到画布（true）还是 window 全局捕获（false，默认）。
        /// 画布模式要求画布获焦，但页面里有输入框时不会误收游戏键；全局模式不怕焦点被抢走。
        /// 只在首次初始化时生效。
        /// </summary>
        public bool KeyboardUseCanvasListener { get; set; }

        /// <summary>是否驱动 <c>KSceneMgr.Update</c>（KTime + 场景树 + KTween / KTimer）。</summary>
        public bool EnableScene { get; set; } = true;

        /// <summary>是否驱动 <c>KSceneMgr.Draw</c>（场景树绘制）。</summary>
        public bool EnableDraw { get; set; } = true;

        public override void Initialize()
        {
            EnsureInitialized();
        }

        public override void LoadContent()
        {
            base.LoadContent();
            EnsureInitialized();
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
            if (EnableDraw) KSceneMgr.Draw(gameTime);
        }

        /// <summary>
        /// 延迟到首次真正运行时才初始化：组件可能在 Game 构造函数里就被加上，那时设备 / SpriteBatch 还没建。
        /// 两个 Init 都是幂等的，重复调用安全。
        /// </summary>
        private void EnsureInitialized()
        {
            if (_inited) return;
            _inited = true;

            KInputMgr.Init(KeyboardUseCanvasListener);
            KSceneMgr.Init(Game);
        }
    }
}
