using System;

namespace FCGame_MonoGame2.Core
{
    internal class Layer
    {
        private Texture2D[] textures;
        private float scrollSpeed;
        
        public Layer(Texture2D[] textures, float scrollSpeed)
        {
            this.textures = textures;
            this.scrollSpeed = scrollSpeed;
        }
        
        internal void Draw(GameTime gameTime, SpriteBatch spriteBatch, float cameraPosition)
        {
            int segmentWidth = textures[0].Width;
            float x = cameraPosition * scrollSpeed;
            int leftSegment = (int)Math.Floor(x / segmentWidth);
            int rightSegment = leftSegment + 1;
            x = (x / segmentWidth - leftSegment) * -segmentWidth;

            spriteBatch.Draw(textures[leftSegment % textures.Length], new Vector2(x, 0.0f), Color.White);
            spriteBatch.Draw(textures[rightSegment % textures.Length], new Vector2(x + segmentWidth, 0.0f), Color.White);
        }

    }
}