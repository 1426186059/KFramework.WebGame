using System.Net.Mail;

namespace KFramework.Example3
{
    internal class CastleBlock : TileBase
    {
        private SoundEffect appears_Sound;
        private Level mLevel;

        private SpriteRenderer mSpriteRenderer_Castle;
        private SpriteRenderer mSpriteRenderer_Flag;
        private SpriteRenderer mSpriteRenderer_CastleFlag;

        public override Rectangle Collider2DZone
        {
            get
            {
                Vector2 FlagScale = new Vector2(0.2f, 1f) * Tile.TileScale;
                Vector2 FlagOffset = new Vector2(16, 0) * Tile.TileScale;
                Vector2 Size = mSpriteRenderer_Flag.Sprite.Rectangle.Size.ToVector2() * FlagScale;

                return new Rectangle(
                    (WorldPosition - Size * mSpriteRenderer_Flag.Pivot + FlagOffset).ToPoint(),
                    Size.ToPoint());
            }
        }

        public CastleBlock(Level level, Vector2 position)
        {
            this.mLevel = level;
            this.WorldPosition = position;
            this.BeginPos = position;

            LoadContent();
        }

        public void LoadContent()
        {
            appears_Sound = mLevel.mContentInstace.LoadSound("MyRes/Sounds/smb_powerup_appears");

            mSpriteRenderer_Flag = new SpriteRenderer();
            mSpriteRenderer_Flag.Sprite = new KSprite(mLevel.mSpriteSheet_misc3Atlas.Sprite("misc-3_9"));
            mSpriteRenderer_Flag.Parent = this;
            mSpriteRenderer_Flag.Pivot = new Vector2(0, 1);
            mSpriteRenderer_Flag.LocalPosition = Vector2.Zero;
            mSpriteRenderer_Flag.LocalScale = Tile.TileScale;

            mSpriteRenderer_Castle = new SpriteRenderer();
            mSpriteRenderer_Castle.Sprite = new KSprite(mLevel.mSpriteSheet_misc3Atlas.Sprite("misc-3_113"));
            mSpriteRenderer_Castle.Parent = this;
            mSpriteRenderer_Castle.Pivot = new Vector2(0.5f, 1);
            mSpriteRenderer_Castle.LocalPosition = new Vector2(500, 0);
            mSpriteRenderer_Castle.LocalScale = Tile.TileScale;

            mSpriteRenderer_CastleFlag = new SpriteRenderer();
            mSpriteRenderer_CastleFlag.Sprite = new KSprite(mLevel.mSpriteSheet_misc3Atlas.Sprite("misc-3_106"));
            mSpriteRenderer_CastleFlag.Parent = mSpriteRenderer_Castle;
            mSpriteRenderer_CastleFlag.Pivot = new Vector2(0.5f, 1);
            mSpriteRenderer_CastleFlag.LocalPosition = new Vector2(0, -50);
            mSpriteRenderer_CastleFlag.LocalScale = Vector2.One;
            mSpriteRenderer_CastleFlag.activeSelf = true;
        }

        public override void Update()
        {
            base.Update();
            if(mPlayer != null)
            {
                if(mPlayer.WorldPosition.X >= mSpriteRenderer_Castle.WorldPosition.X)
                {
                    mPlayer.mPlayerModeScripting = Player.EPlayerModeScripting.None;
                    mPlayer = null;
                    mSpriteRenderer_CastleFlag.activeSelf = true;
                    KTweenEx.moveY(mSpriteRenderer_CastleFlag, mSpriteRenderer_Castle.Collider2DZone.Top, 1);
                }
            }
        }
        
        public override void Draw()
        {
            if (mSpriteRenderer_Flag.activeInHierarchy)
            {
                mSpriteRenderer_Flag.Draw();
            }

            if (mSpriteRenderer_CastleFlag.activeInHierarchy)
            {
                mSpriteRenderer_CastleFlag.Draw();
            }

            if (mSpriteRenderer_Castle.activeInHierarchy)
            {
                mSpriteRenderer_Castle.Draw();
            }

            DrawCollider2DZone();
        }

        private Player mPlayer;
        public void DoPlayerAni(Player mPlayer)
        {
            this.mPlayer = mPlayer;
            this.mPlayer.WorldPosition = new Vector2(this.mSpriteRenderer_Flag.WorldPosition.X + 10, this.mPlayer.WorldPosition.Y);
        }

    }
}