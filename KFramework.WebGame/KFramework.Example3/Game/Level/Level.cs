namespace KFramework.Example3
{
    internal class Level : IDisposable
    {
        int nLevelIndex;
        private const int EntityLayer = 2;
        private Tile[,] tiles;
        public AssetBundle mContentInstace;
        private string levelPath;
        public SpriteSheet mSpriteSheet_charactersAtlas;
        public SpriteSheet mSpriteSheet_misc3Atlas;

        public event EventHandler<EventArgs> DrawOrderChanged;
        public event EventHandler<EventArgs> VisibleChanged;

        public int Width => tiles.GetLength(0);
        public int Height => tiles.GetLength(1);

        public int DrawOrder => throw new NotImplementedException();

        public bool Visible => throw new NotImplementedException();

        private Player mPlayer;
        public readonly LinkedList<TileBlock> mBackgroundObjectList = new LinkedList<TileBlock>();
        public readonly LinkedList<PowerUpObject> mPowerUpObjectList = new LinkedList<PowerUpObject>();
        public readonly LinkedList<EnemyBase> mEnemyObjectList = new LinkedList<EnemyBase>();
        public bool Paused { get; set; }
        public ParticleManager mParticleManager { get; set; }

        private StartScreen mStartScreen;
        private GameScreen mGameScreen;

        private Level(int nLevelIndex, Stream levelStream,
                      SpriteSheet charactersAtlas, SpriteSheet misc3Atlas)
        {
            this.mContentInstace = KSceneMgr.Game.Content.GetBundle("MyRes/Sounds")
                ?? throw new InvalidOperationException("内容包 content 尚未加载");
            mSpriteSheet_charactersAtlas = charactersAtlas;
            mSpriteSheet_misc3Atlas = misc3Atlas;

            PrintTool.Assert(mSpriteSheet_charactersAtlas != null);
            PrintTool.Assert(mSpriteSheet_misc3Atlas != null);

            this.nLevelIndex = nLevelIndex;
            LoadTiles(levelStream);

            KSprite particleTexture = new KSprite(mSpriteSheet_misc3Atlas.Sprite("misc-3_16"));
            mParticleManager = new ParticleManager(particleTexture, Vector2.Zero);

            KSceneMgr.ScreenSizeChanged += OnWindowSizeChanged;
            
            PlayerData.Instance.Reset();
            if (nLevelIndex == 0)
            {
                mStartScreen = new StartScreen(this);
            }
            else
            {
                mGameScreen = new GameScreen(this);
            }

        }

        /// <summary>
        /// 异步创建关卡：
        /// 1) 先按需异步加载所需 AssetBundle（content 包内含图集页、图集描述与音效）；
        /// 2) 关卡文本通过 HTTP 从页面基址远程下载（与内容包同一套下载链路）；
        /// 3) 图集（SpriteSheet）从已加载的 Bundle 中异步取出切片，最后构造 <see cref="Level"/>。
        /// 由于 Bundle 是异步加载的，窗口缩放触发重建关卡时资源会被重新异步加载出来。
        /// </summary>
        public static async Task<Level> LoadAsync(int nLevelIndex, CancellationToken cancellationToken = default)
        {
            ContentManager content = KSceneMgr.Game.Content;

            // 异步加载本关卡依赖的 Bundle（资源全部从 Bundle 中读取）
            await content.LoadBundleAsync("MyRes/Atlas", cancellationToken).ConfigureAwait(false);

            string text = await content
                .LoadTextAsync($"Levels/{nLevelIndex:00}.txt", cancellationToken)
                .ConfigureAwait(false);
            using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(text));

            var atlasBundle = content.GetBundle("MyRes/Atlas")!;
            SpriteSheetLoader mLoader = new SpriteSheetLoader(atlasBundle, KSceneMgr.Game.GraphicsDevice);
            SpriteSheet characters = mLoader.Load(atlasBundle, "MyRes/Atlas/characters");
            SpriteSheet misc3 = mLoader.Load(atlasBundle, "MyRes/Atlas/misc-3");

            return new Level(nLevelIndex, stream, characters, misc3);
        }
        
        private void LoadTiles(Stream fileStream)
        {
            int width;
            List<string> lines = new List<string>();
            int ignoreLineCount = 3;
            int nMaxWidth = 0;
            using (StreamReader reader = new StreamReader(fileStream))
            {
                while (ignoreLineCount-- > 0)
                {
                    reader.ReadLine();
                }

                string line = reader.ReadLine();
                width = line.Length;
                while (line != null)
                {
                    if (line.Length > nMaxWidth)
                    {
                        nMaxWidth = line.Length;
                    }

                    lines.Add(line);
                    line = reader.ReadLine();
                }
            }

            tiles = new Tile[nMaxWidth, lines.Count];
            Tile.TileHeight = KSceneMgr.Game.GraphicsDevice.Viewport.Height / (float)Height;
            Tile.TileWidth = Tile.TileHeight;
            Tile.TileFloorY = KSceneMgr.Game.GraphicsDevice.Viewport.Height;
            Tile.TileMinPosX = 0;
            Tile.TileMaxPosX = nMaxWidth * Tile.TileWidth - KSceneMgr.Game.GraphicsDevice.Viewport.Width;

            for (int y = 0; y < Height; ++y)
            {
                for (int x = 0; x < Width; ++x)
                {
                    char tileType = ' ';
                    if (x < lines[y].Length)
                    {
                        tileType = lines[y][x];
                    }
                    tiles[x, y] = LoadTile(tileType, x, y);
                }
            }
            
        }

        private Tile LoadTile(char tileType, int x, int y)
        {
            switch (tileType)
            {
                case ' ':
                case '.':
                    return new Tile();

                // Exit
                case 'E': //旗杆
                    return LoadExitTile(x, y, TileCollision.Exit);
                case 'P': //玩家出生点
                    return LoadStartTile(x, y);

                case 'A': //敌人：板栗崽
                    return LoadEnemyTile(x, y,  TileCollision.Passable, EEnemyType.Goomba);
                case 'B': //敌人：乌龟
                    return LoadEnemyTile(x, y, TileCollision.Passable, EEnemyType.Koopas);

                // Impassable block
                case '#': //地板砖块
                    return LoadTile(x, y, mSpriteSheet_misc3Atlas, "misc-3_68", TileCollision.Impassable);
                case '=': //可碎的砖块
                    return LoadBreakableBlock(x, y, TileCollision.Breakable);
                case '*': //棕色石头
                    return LoadTile(x, y, mSpriteSheet_misc3Atlas, "misc-3_67", TileCollision.Impassable);
                    
                case 'Q': //问号 金币
                    return LoadQuestionBlock(x, y, TileCollision.Impassable, QuestionBlock.EGiftType.Coin, 1);
                case 'R': //问号 金币
                    return LoadQuestionBlock(x, y, TileCollision.Impassable, QuestionBlock.EGiftType.Coin, 5);;
                case 'S': //问号 蘑菇
                    return LoadQuestionBlock(x, y, TileCollision.Impassable, QuestionBlock.EGiftType.Mushroom);
                case 'T': //问号 蘑菇 加一个条命
                    return LoadQuestionBlock(x, y, TileCollision.Impassable, QuestionBlock.EGiftType.OneUpMushroom);
                case 'U': //问号 火花
                    return LoadQuestionBlock(x, y, TileCollision.Impassable, QuestionBlock.EGiftType.FireFlower);

                case 'V': //金币砖 星星
                    return LoadCoinBlock(x, y, TileCollision.Impassable, CoinBlock.EGiftType.UltimateStar);
                case 'W': //金币砖 金币
                    return LoadCoinBlock(x, y, TileCollision.Impassable, CoinBlock.EGiftType.Coin, 10);

                case 'X': //云朵
                    return LoadClouds(x, y, TileCollision.Passable);
                case 'Y': //小山
                    return LoadHill(x, y, TileCollision.Passable);
                case 'Z': //荆棘丛
                    return LoadBushes(x, y, TileCollision.Passable);
                
                case 'a': //管道
                    return LoadPipeBlock(x, y, TileCollision.Impassable, PipeBlock.EPipeType.Head);
                case 'b': //管道
                    return LoadPipeBlock(x, y, TileCollision.Impassable, PipeBlock.EPipeType.Connect);
                case 'c': //管道
                    return LoadPipeBlock(x, y, TileCollision.Impassable, PipeBlock.EPipeType.Connect2);

                default:
                    throw new NotSupportedException();
            }
        }

        private Tile LoadTile(int x, int y, SpriteSheet mSpriteSheet, string name, TileCollision collision)
        {
            KSprite mSprite = new KSprite(mSpriteSheet.Sprite(name));
            Tile mTile = new Tile(collision);
            mTile.Target = new TileBlock(this, mSprite, GetTitleWorldPos(x, y));
            return mTile;
        }

        static int[] CloudsIndexList = new int[] { 103, 104, 105 };
        private Tile LoadClouds(int x, int y, TileCollision collision)
        {
            int nIndex = RandomTool.RandomArrayIndex(0, CloudsIndexList.Length);
            KSprite mSprite = new KSprite(mSpriteSheet_misc3Atlas.Sprite("misc-3_" + CloudsIndexList[nIndex]));
            mBackgroundObjectList.AddLast(new TileBlock(this, mSprite, GetTitleWorldPos(x, y)));
            return default;
        }

        static int[] HillIndexList = new int[] { 79, 88 };
        private Tile LoadHill(int x, int y, TileCollision collision)
        {
            int nIndex = RandomTool.RandomArrayIndex(0, HillIndexList.Length);
            KSprite mSprite = new KSprite(mSpriteSheet_misc3Atlas.Sprite("misc-3_" + HillIndexList[nIndex]));
            mBackgroundObjectList.AddLast(new TileBlock(this, mSprite, GetTitleWorldPos(x, y + 1), new Vector2(0, 1)));
            return default;
        }

        static int[] BushesIndexList = new int[] { 117, 118, 119 };
        private Tile LoadBushes(int x, int y, TileCollision collision)
        {
            int nIndex = RandomTool.RandomArrayIndex(0, BushesIndexList.Length);
            KSprite mSprite = new KSprite(mSpriteSheet_misc3Atlas.Sprite("misc-3_" + BushesIndexList[nIndex]));
            mBackgroundObjectList.AddLast(new TileBlock(this, mSprite, GetTitleWorldPos(x, y + 1), new Vector2(0, 1)));
            return default;
        }

        private Tile LoadBreakableBlock(int x, int y, TileCollision collision)
        {
            Tile mTile = new Tile();
            Vector2 start = GetTitleWorldPos(x, y);
            mTile.Target = new BreakableBlock(this, start);
            mTile.Collision = collision;
            return mTile;
        }

        private Tile LoadCoinBlock(int x, int y, TileCollision collision, CoinBlock.EGiftType nGiftType, int nCount = 1)
        {
            Tile mTile = new Tile();
            Vector2 start = GetTitleWorldPos(x, y);
            mTile.Target = new CoinBlock(this, start, nGiftType, nCount);
            mTile.Collision = collision;
            return mTile;
        }

        private Tile LoadQuestionBlock(int x, int y, 
            TileCollision collision, 
            QuestionBlock.EGiftType GiftType, 
            int nCount = 1)
        {
            Tile mTile = new Tile();
            Vector2 start = GetTitleWorldPos(x, y);
            mTile.Target = new QuestionBlock(this, start, GiftType, nCount);
            mTile.Collision = collision;
            return mTile;
        }

        private Tile LoadPipeBlock(int x, int y, TileCollision collision, PipeBlock.EPipeType nType)
        {
            Tile mTile = new Tile();
            Vector2 start = GetTitleWorldPos(x, y + 1);
            mTile.Target = new PipeBlock(this, start, nType);
            mTile.Collision = collision;
            return mTile;
        }

        private Tile LoadEnemyTile(int x, int y, TileCollision collision, EEnemyType nType)
        {
            Tile mTile = new Tile();
            Vector2 start = GetTitleWorldPos(x, y + 1);
            mTile.Collision = collision;
            mTile.Target = new EnemyEmitter(this, start, nType);
            return mTile;
        }
        
        private Tile LoadStartTile(int x, int y)
        {
            Vector2 start = GetTitleWorldPos(x, y + 1);
            mPlayer = new Player(this, start);
            if (nLevelIndex == 0)
            {
                mPlayer.Mode = PlayerMode.Scripting;
            }
            else
            {
                mPlayer.Mode = PlayerMode.Playing;
            }
            return default;
        }

        private Tile LoadExitTile(int x, int y, TileCollision collision)
        {
            Tile mTile = new Tile();
            Vector2 start = GetTitleWorldPos(x, y + 1);
            mTile.Target = new CastleBlock(this, start);
            mTile.Collision = collision;
            return mTile;
        }

        public Rectangle GetTileRectangle(int x, int y)
        {
            float nOffsetY = KSceneMgr.Game.GraphicsDevice.Viewport.Height - Height * Tile.TileHeight;
            return new Rectangle(
                new Point((int)Math.Round(x * Tile.TileWidth), (int)Math.Round(nOffsetY + y * Tile.TileHeight)),
                new Point((int)Math.Round(Tile.TileWidth), (int)Math.Round(Tile.TileHeight)));
        }

        public Vector2 GetTitleWorldPos(int x, int y)
        {
           return GetTileRectangle(x, y).Location.ToVector2();
        }

        public TileCollision GetCollision(int x, int y)
        {
            // Prevent escaping past the level ends.
            if (x < 0 || x >= Width)
                return TileCollision.Impassable;
            // Allow jumping past the level top and falling through the bottom.
            if (y < 0 || y >= Height)
                return TileCollision.Passable;

            return tiles[x, y].Collision;
        }

        public Tile GetTile(int x, int y)
        {
            if (x < 0 || x >= Width)
            {
                return new Tile(TileCollision.Impassable, default);
            }

            if (y < 0 || y >= Height)
            {
                return new Tile(TileCollision.Passable, default);
            }
            return tiles[x, y];
        }

        internal void BreakTile(int x, int y)
        {
            mParticleManager.LocalPosition = GetTile(x, y).Target.Collider2DZone.Center.ToVector2();
            mParticleManager.Emit(6, ParticleEffectType.BreakableBlock_Break, Color.White);
            RemoveTile(x, y);
        }

        internal void RemoveTile(int x, int y)
        {
            // Replace the tile with a passable, textureless tile.
            tiles[x, y] = new Tile();
        }
        
        public void Dispose()
        {
            KSceneMgr.ScreenSizeChanged -= OnWindowSizeChanged;

            // 注意：mContentInstace 就是 KSceneMgr.Game.Content，是整个游戏共享、由
            // Game.Dispose() 在退出时统一释放的 ContentManager。Player / 各类方块 / 敌人
            // 在游戏运行中都会通过它 LoadSound(...)，因此它绝不能在此处被 Dispose——
            // 否则窗口缩放重建 Level 时会释放掉共享的 HttpClient，导致后续关卡 HTTP 下载
            // 抛 ObjectDisposedException。这里只释放 Level 自己持有的资源。

            if (mStartScreen != null)
            {
                mStartScreen.Dispose();
            }
            else if(mGameScreen != null)
            {
                mGameScreen.Dispose();
            }

            for (int i = 0; i < Width; i++)
            {
                for (int j = 0; j < Height; j++)
                {
                    if(tiles[i, j].Target != null)
                    {
                        tiles[i, j].Target.Dispose();
                        tiles[i, j].Target = null;
                    }
                }
            }

            foreach(var v in mPowerUpObjectList)
            {
                v.Dispose();
            }

            foreach (var v in mEnemyObjectList)
            {
                v.Dispose();
            }

            foreach (var v in mBackgroundObjectList)
            {
                v.Dispose();
            }
        }

        public void Update()
        {
            for (int i = 0; i < Width; i++)
            {
                for (int j = 0; j < Height; j++)
                {
                    tiles[i, j].Update();
                }
            }

            if (mPlayer != null)
            {
                mPlayer.Update();
            }
            
            var mNode = mPowerUpObjectList.First;
            while(mNode != null)
            {
                mNode.Value.Update();
                if(mNode.Value.IsDispose)
                {
                    var curNode = mNode;
                    mNode = mNode.Next;
                    mPowerUpObjectList.Remove(curNode);
                }
                else
                {
                    mNode = mNode.Next;
                }
            }

            var mEnemyNode = mEnemyObjectList.First;
            while (mEnemyNode != null)
            {
                mEnemyNode.Value.Update();
                if (mEnemyNode.Value.IsDispose)
                {
                    var curNode = mEnemyNode;
                    mEnemyNode = mEnemyNode.Next;
                    mEnemyObjectList.Remove(curNode);
                }
                else
                {
                    mEnemyNode = mEnemyNode.Next;
                }
            }

            mParticleManager.Update();
            PlayerData.Instance.AddTime(KTime.deltaTime);
        }

        public void Draw()
        {
            Matrix4x4 viewMatrix = mPlayer.World_To_View_Matrix;
            var mSpriteBatch = KSceneMgr.SpriteBatch;
            mSpriteBatch.Begin(
                transformMatrix: viewMatrix,
                sortMode: SpriteSortMode.Deferred,
                samplerState: SamplerState.PointClamp,
                blendState: BlendState.NonPremultiplied);

            DrawLevel();
            mSpriteBatch.End();
        }

        private void DrawLevel()
        {
            var mSpriteBatch = KSceneMgr.SpriteBatch;

            foreach(var v in mBackgroundObjectList)
            {
                v.Draw();
            }

            foreach (var v in mPowerUpObjectList)
            {
                v.Draw();
            }

            for (int i = 0; i < Width; i++)
            {
                for (int j = 0; j < Height; j++)
                {
                    tiles[i, j].Draw();
                }
            }

            foreach (var v in mEnemyObjectList)
            {
                v.Draw();
            }

            mPlayer.Draw();
            mParticleManager.Draw(mSpriteBatch);
        }

        public void OnWindowSizeChanged(object sender, EventArgs e)
        {
            // 交给 MainScene 异步重新下载并重建关卡（会负责释放旧 Level）
            ((MainScene)KSceneMgr.Main).ReloadCurrentLevel();
        }

    }
}