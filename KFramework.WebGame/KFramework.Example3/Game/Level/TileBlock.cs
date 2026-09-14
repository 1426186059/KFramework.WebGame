
namespace KFramework.Example3
{
    internal class TileBlock : TileBase
    {
        public Level Level;
        KSprite mSprite;

        private Vector2 Origin
        {
            get
            {
                return mSprite.Rectangle.Size.ToVector2() * Pivot;
            }
        }

        public TileBlock(Level level, KSprite mSprite, Vector2 position, Vector2 Pivot = default)
        {
            this.Level = level;
            this.WorldPosition = BeginPos = position;
            this.mSprite = mSprite;
            this.Pivot = Pivot;
        }

        public override void Draw()
        {
            Rectangle targetRegion = new Rectangle(WorldPosition.ToPoint(), 
                (this.mSprite.Rectangle.Size.ToVector2() * Tile.TileScale).ToPoint());
            KSceneMgr.SpriteBatch.Draw(
                mSprite.Texture,
                targetRegion,
                mSprite.Rectangle,
                Color.White,
                0.0f,
                Origin,
                SpriteEffects.None,
                0.0f);

            DrawCollider2DZone();
        }

    }
}