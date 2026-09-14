
namespace KFramework.Example3
{
    internal class PowerUp_Mushroom : PowerUpObject
    {
        private SoundEffect appears_Sound;
        private SpriteRenderer mSpriteRenderer;

        public override Rectangle Collider2DZone
        {
            get
            {
                Vector2 Size = mSpriteRenderer.Sprite.Rectangle.Size.ToVector2() * Tile.TileScale;
                return new Rectangle(
                    (WorldPosition - Pivot * Size).ToPoint(),
                    Size.ToPoint());
            }
        }

        public PowerUp_Mushroom(Level level, Vector2 position) : base(level, position, 150)
        {
            LoadContent();
            nMoveState = EMoveState.Appear;
        }

        private void LoadContent()
        {
            appears_Sound = mLevel.mContentInstace.LoadSound("MyRes/Sounds/smb_powerup_appears");
            appears_Sound.Play();

            mSpriteRenderer = new SpriteRenderer();
            mSpriteRenderer.Sprite = new KSprite(mLevel.mSpriteSheet_misc3Atlas.Sprite("misc-3_5"));
            Pivot = mSpriteRenderer.Pivot = new Vector2(0.5f, 1);
            mSpriteRenderer.Parent = this;
            mSpriteRenderer.LocalPosition = Vector2.Zero;
            mSpriteRenderer.LocalScale = Tile.TileScale;
            WorldPosition = BeginPos + new Vector2(0, Collider2DZone.Size.Y);
        }

        public override void Update()
        {
            base.Update();

            if(WorldPosition.Y >= Tile.TileFloorY)
            {
                OnKilled(null);
            }
        }
        
        public override void Draw()
        {
            mSpriteRenderer?.Draw();
            DrawCollider2DZone();
        }

        /// <summary>
        /// Handles the enemy being killed by the player.
        /// </summary>
        /// <param name="killedBy">The player who killed the enemy.</param>
        public void OnKilled(Player mPlayer)
        {
            if (!IsDispose)
            {
                Dispose();
            }
        }
    }
}