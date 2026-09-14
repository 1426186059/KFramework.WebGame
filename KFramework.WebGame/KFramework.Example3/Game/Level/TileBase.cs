
namespace KFramework.Example3
{
    internal class TileBase : KTransform
    {
        internal enum Collider2DType
        {
            Box,
            Circle,
        }

        public TileCollision Collision;
        public Collider2DType nCollider2DType;
        public Vector2 Pivot { get; set; }
        protected Vector2 BeginPos;

        public virtual Rectangle Collider2DZone
        {
            get
            {
                return new Rectangle(WorldPosition.ToPoint(), Tile.TileSize.ToPoint());
            }
        }

        private const bool bDrawCollider2DZone = false;
        public void DrawCollider2DZone()
        {
            if (bDrawCollider2DZone)
            {
                var batch = KSceneMgr.SpriteBatch;
                Vector2 worldPos = WorldPosition;
                Vector2 origin = Vector2.Zero;
                Rectangle target = Collider2DZone;

                Color mColor = new Color(255, 255, 255, 200);
                //mColor = Color.White * 0.5f;

                batch.Draw(
                    KDefaultRes.DefaultSprite.Texture,
                    target,
                    KDefaultRes.DefaultSprite.Rectangle,
                    mColor,
                    WorldRotation,
                    origin,
                    SpriteEffects.None,
                    0);
            }
        }
    }
}
