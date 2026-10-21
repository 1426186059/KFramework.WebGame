using System.Threading.Tasks;
using KFramework.MonoGame;
using KFramework.MonoGameExtend;

namespace KFramework.Editor
{
    /// <summary>
    /// 用 KFramework 引擎搭的「类 Unity 编辑器」宿主：WebGL20 后端、WASM 应用。
    /// </summary>
    public sealed class EditorGame : Game
    {
        public EditorGame() : base(GraphicsBackendKind.WebGL20)
        {
            ClearColor = new Color(20, 22, 30);
            Components.Add(new KEngineComponent(this));   // 接入引擎（输入/时间/场景树）
        }

        protected override async Task LoadContentAsync()
        {
            KDefaultRes.DefaultSpriteFont = new SpriteFont(GraphicsDevice, 18f);
            await ContentManager.Default.LoadAsync();
            KSceneMgr.SetMainScene(new EditorScene());     // 切到编辑器主场景
        }
    }
}
