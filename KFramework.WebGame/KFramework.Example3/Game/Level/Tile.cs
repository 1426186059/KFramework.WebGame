
namespace KFramework.Example3
{
    internal struct Tile
    {
        public static float TileWidth { get; set; } = 16;
        public static float TileHeight { get; set; } = 16;
        public static Vector2 TileSize => new Vector2(TileWidth, TileHeight);
        public static Vector2 TileScale => new Vector2(TileWidth / 16f, TileHeight / 16f);

        public static float TileFloorY = 0;
        public static float TileMinPosX = 0;
        public static float TileMaxPosX = 0;

        public TileCollision Collision;
        public TileBase Target { get; set; }
        
        public Tile()
        {
            this.Collision = TileCollision.Passable;
            this.Target = null;
        }

        public Tile(TileCollision collision, TileBase target = null)
        {
            this.Collision = collision;
            this.Target = target;
        }

        public void Update()
        {
            if (Target != null)
            {
                KTransform mTarget = (KTransform)Target;
                mTarget.Update();
            }
        }

        public void Draw()
        {
            if (Target != null)
            {
                KTransform mTarget = (KTransform)Target;
                mTarget.Draw();
            }
        }

    }
}