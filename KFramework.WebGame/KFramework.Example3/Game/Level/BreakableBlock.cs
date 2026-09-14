
namespace KFramework.Example3
{
    internal class BreakableBlock : TileBase
    {
        public Level Level;

        private const float GravityAcceleration = 3400.0f;
        private Vector2 Velocity;
        private readonly Vector2 HitInitVelocity = new Vector2(0, -300f);
        private Vector2 preBottomPos;

        SoundEffect bumpSound;
        SoundEffect breakSound;

        KSprite mSprite;
        bool bDestroy = false;
        ParticleManager mParticleManager;

        public Rectangle BoundingRectangle
        {
            get
            {
                return new Rectangle(WorldPosition.ToPoint(), Tile.TileSize.ToPoint());
            }
        }
        
        public BreakableBlock(Level level, Vector2 position)
        {
            this.Level = level;
            this.WorldPosition = position;
            this.preBottomPos = WorldPosition;
            LoadContent();
        }

        public void LoadContent()
        {
            bumpSound = Level.mContentInstace.LoadSound("MyRes/Sounds/smb_bump");
            breakSound = Level.mContentInstace.LoadSound("MyRes/Sounds/smb_breakblock");
            mSprite = new KSprite(Level.mSpriteSheet_misc3Atlas.Sprite("misc-3_13"));

            //var particleTexture = Level.mContentInstace.Load<Texture2D>("MyRes/Effects/blank");
            //mParticleManager = new ParticleManager(particleTexture, new Vector2(400, 200));
        }

        public void HandleHit(bool bBreak)
        {
            if (bDestroy)
            {
                return;
            }

            if (bBreak)
            {
                bDestroy = true;
                breakSound.Play();
            }
            else
            {
                bumpSound.Play();
                Velocity = HitInitVelocity;
            }

        }

        public override void Update()
        {
            if (!bDestroy && Velocity != Vector2.Zero)
            {
                float deltaTime = KTime.deltaTime;
                //速度 = 速度 + 重力加速度 × 时间
                //位置 = 位置 + 速度 × 时间
                Velocity.Y += GravityAcceleration * deltaTime;
                WorldPosition += Velocity * deltaTime;

                if(WorldPosition.Y > preBottomPos.Y)
                {
                    WorldPosition = preBottomPos;
                    Velocity = Vector2.Zero;
                }
            }
        }
        
        public override void Draw()
        {
            Rectangle targetRegion = new Rectangle(WorldPosition.ToPoint(), Tile.TileSize.ToPoint());
            KSceneMgr.SpriteBatch.Draw(
                mSprite.Texture,
                targetRegion,
                mSprite.Rectangle,
                Color.White,
                0.0f,
                Vector2.Zero,
                SpriteEffects.None,
                0.0f);

            DrawCollider2DZone();
        }

    }
}