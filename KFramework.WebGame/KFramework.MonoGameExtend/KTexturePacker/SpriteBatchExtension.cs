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

            // SpriteBatch 的完整形态收的是"目标矩形"（位置取矩形左上角）：这里把 position + scale 折成矩形，
            // 矩形尺寸 = 源尺寸 × scale（与原来 position + scale 的语义一致）。
            var target = new Rectangle((int)MathF.Round(position.X), (int)MathF.Round(position.Y),
                                       (int)MathF.Round(sprite.SourceRectangle.Width * scale),
                                       (int)MathF.Round(sprite.SourceRectangle.Height * scale));

            spriteBatch.Draw(
                texture: sprite.Texture,
                targetRectangle: target,
                sourceRectangle: sprite.SourceRectangle,
                color: color ?? Color.White,
                rotation: rotation,
                origin: origin,
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

            // 中心点对齐 = 目标矩形的锚点落在 center、锚点取源矩形中心（矩形尺寸 = 源尺寸 × scale）。
            var target = new Rectangle((int)MathF.Round(center.X), (int)MathF.Round(center.Y),
                                       (int)MathF.Round(sprite.SourceRectangle.Width * scale),
                                       (int)MathF.Round(sprite.SourceRectangle.Height * scale));

            spriteBatch.Draw(
                texture: sprite.Texture,
                targetRectangle: target,
                sourceRectangle: sprite.SourceRectangle,
                color: color ?? Color.White,
                rotation: rotation,
                origin: origin,
                effects: spriteEffects,
                layerDepth: 0.0f
            );
        }
    }
}
