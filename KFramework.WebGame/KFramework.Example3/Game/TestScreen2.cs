
namespace KFramework.Example3
{
    internal class TestScreen2 : KUIBase
    {
        private ContentManager contentManager;

        KTransform _root;
        KTransform _child;
        KCamera _camera;
        Texture2D _texture;

        KTransform playerTransform;

        public TestScreen2()
        {
            _root = new KTransform();
            _root.LocalPosition = new Vector2(0, 0);

            _child = new KTransform();
            _child.Parent = _root;
            _child.LocalPosition = new Vector2(100, 0);
            _child.LocalScale = new Vector2(2, 2);

            _camera = new KCamera();
            _camera.LocalScale = new Vector2(1f, 1f);  // 初始缩放
            //_camera.scr = new Vector2(
            //  Game2.Instance.graphicsDeviceManager.PreferredBackBufferWidth / 2f,
            //    Game2.Instance.graphicsDeviceManager.PreferredBackBufferHeight / 2f);

            playerTransform = new KTransform();
            playerTransform.LocalPosition = new Vector2(400, 300);
        }

        private SpriteSheet _spriteSheet;
        public override void Init()
        {
            if (contentManager == null)
            {
                contentManager = KSceneMgr.Game.Content;
            }

            // 异步加载：先加载 atlas Bundle，再从其中取出 SpriteSheet（KUIBase.Init 为同步，这里 fire-and-forget）
            _ = LoadSheetAsync();
        }

        private async Task LoadSheetAsync()
        {
            try
            {
                await contentManager.LoadBundleAsync("MyRes/Atlas").ConfigureAwait(false);
                var atlasBundle = contentManager.GetBundle("MyRes/Atlas")!;
                SpriteSheetLoader mLoader = new SpriteSheetLoader(atlasBundle, KSceneMgr.Game.GraphicsDevice);
                _spriteSheet = mLoader.Load("MyRes/Atlas/AAA");
                _texture = _spriteSheet.Sprite("characters_characters_0").Texture;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[TestScreen2] 图集加载失败：{ex}");
            }
        }

        public override void Update()
        {
            _camera.Follow(playerTransform);  // 跟随玩家

            // 鼠标滚轮缩放，增量由输入系统统一维护
            int scrollDelta = KInputMgr.ScrollDelta;

            // 用增量来缩放
            if (scrollDelta != 0)
            {
                float zoomSpeed = 0.001f;
                float newZoom = _camera.LocalScale.X + scrollDelta * zoomSpeed;

                // 限制缩放范围，防止缩到 0 或负数
                newZoom = MathHelper.Clamp(newZoom, 0.1f, 5f);

                _camera.LocalScale = new Vector2(newZoom, newZoom);
            }
        }

        public override void Draw()
        {
            KSceneMgr.Game.GraphicsDevice.Clear(Color.CornflowerBlue);
            var _spriteBatch = KSceneMgr.SpriteBatch;

            // 关键：传入 ViewMatrix，SpriteBatch 自动处理所有世界→屏幕转换
            _spriteBatch.Begin(
                transformMatrix: _camera.ViewMatrix,
                sortMode: SpriteSortMode.Deferred,
                samplerState: SamplerState.PointClamp);

            // 图集尚未异步加载完成时跳过绘制
            if (_texture != null)
            {
            // 直接用世界坐标绘制，无需手动转换
            _spriteBatch.Draw(
                _texture,
                _root.WorldPosition,
                null,
                Color.White,
                _root.WorldRotation,
                Vector2.Zero,        // origin
                _root.WorldScale,
                SpriteEffects.None,
                0f);

            _spriteBatch.Draw(
                _texture,
                _child.WorldPosition,
                null,
                Color.Red,
                _child.WorldRotation,
                Vector2.Zero,
                _child.WorldScale,
                SpriteEffects.None,
                0f);
            }

            _spriteBatch.End();
        }

    }
}
