using KFramework.MonoGame;
using KFramework.MonoGameExtend;

namespace KFramework.Game.Template
{
    /// <summary>
    /// 最小游戏模板：仅用于验证 WASM 渲染链路（框架初始化 + 清屏 + SpriteBatch 画纹理/文字）。
    /// 直接继承 KFramework.MonoGame 的 Game 基类即可；每帧基类会先按 ClearColor 清屏，再调用本类的 Draw。
    /// 类名（KFramework.Game.Template）与 KFramework.Game 命名空间重名前缀，故基类用完全限定名。
    /// </summary>
    public sealed class GameTemplateGame : KFramework.MonoGame.Game
    {
        private Texture2D _white = null!;

        public GameTemplateGame() : base(GraphicsBackendKind.WebGL20, "#game", antialias: false)
        {
            // 每帧绘制前清屏色。
            ClearColor = new Color(12, 14, 24);
            IsFixedTimeStep = false;
        }

        protected override Task LoadContentAsync()
        {
            // 程序化生成的系统字体，不依赖任何字体资源文件。
            KDefaultRes.DefaultSpriteFont = new SpriteFont(GraphicsDevice, 22f);
            // 一张 1×1 白纹理，用来画纯色矩形（拉伸即可）。
            _white = GraphicsDevice.CreateTexture(1, 1, new byte[] { 255, 255, 255, 255 });
            return Task.CompletedTask;
        }

        protected override void Draw(GameTime gameTime)
        {
            var sb = SpriteBatch;
            sb.Begin(); // 默认 NonPremultiplied（直通 alpha）

            // 半透明红矩形：验证 alpha 混合是否按预期工作。
            sb.Draw(_white, new Rectangle(60, 60, 260, 160), null, new Color(255, 80, 80, 170));
            // 一段说明文字：验证字体渲染。
            sb.DrawString(KDefaultRes.DefaultSpriteFont, "KFramework.Game.Template 运行中", new Vector2(70, 120), Color.White);

            sb.End();
        }
    }
}
