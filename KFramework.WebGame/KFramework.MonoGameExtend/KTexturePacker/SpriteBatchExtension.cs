using System;
using KFramework.MonoGame;

namespace KFramework.MonoGameExtend
{
    public static class SpriteBatchExtensions
    {
        private const float ClockwiseNinetyDegreeRotation = (float)(Math.PI / 2.0f);

        public static void Draw(this SpriteBatch spriteBatch, KSpriteInfo sprite, Vector2 position, Color? color = null, float rotation = 0, float scale = 1, SpriteEffects spriteEffects = SpriteEffects.None)
        {
            Vector2 origin = sprite.Origin;
            if (sprite.IsRotated)
            {
                rotation -= ClockwiseNinetyDegreeRotation;
                switch (spriteEffects)
                {
                    case SpriteEffects.FlipHorizontally: spriteEffects = SpriteEffects.FlipVertically; break;
                    case SpriteEffects.FlipVertically: spriteEffects = SpriteEffects.FlipHorizontally; break;
                }
            }
            switch (spriteEffects)
            {
                case SpriteEffects.FlipHorizontally: origin.X = sprite.SourceRectangle.Width - origin.X; break;
                case SpriteEffects.FlipVertically: origin.Y = sprite.SourceRectangle.Height - origin.Y; break;
            }

            spriteBatch.Draw(
                texture: sprite.Texture,
                position: position,
                sourceRectangle: sprite.SourceRectangle,
                color: color ?? Color.White,
                rotation: rotation,
                origin: origin,
                scale: new Vector2(scale, scale),
                effects: spriteEffects,
                layerDepth: 0.0f
            );
        }

        /// <summary>以 center 为精灵中心点绘制（原点取精灵中心，照 SpriteBatch.DrawCentered 的语义）。</summary>
        public static void DrawCentered(this SpriteBatch spriteBatch, KSpriteInfo sprite, Vector2 center, Color? color = null, float rotation = 0, float scale = 1, SpriteEffects spriteEffects = SpriteEffects.None)
        {
            Vector2 origin = new Vector2(sprite.SourceRectangle.Width * 0.5f, sprite.SourceRectangle.Height * 0.5f);
            if (sprite.IsRotated)
            {
                rotation -= ClockwiseNinetyDegreeRotation;
                switch (spriteEffects)
                {
                    case SpriteEffects.FlipHorizontally: spriteEffects = SpriteEffects.FlipVertically; break;
                    case SpriteEffects.FlipVertically: spriteEffects = SpriteEffects.FlipHorizontally; break;
                }
            }
            switch (spriteEffects)
            {
                case SpriteEffects.FlipHorizontally: origin.X = sprite.SourceRectangle.Width - origin.X; break;
                case SpriteEffects.FlipVertically: origin.Y = sprite.SourceRectangle.Height - origin.Y; break;
            }

            spriteBatch.Draw(
                texture: sprite.Texture,
                position: center,
                sourceRectangle: sprite.SourceRectangle,
                color: color ?? Color.White,
                rotation: rotation,
                origin: origin,
                scale: new Vector2(scale, scale),
                effects: spriteEffects,
                layerDepth: 0.0f
            );
        }
    }
}
