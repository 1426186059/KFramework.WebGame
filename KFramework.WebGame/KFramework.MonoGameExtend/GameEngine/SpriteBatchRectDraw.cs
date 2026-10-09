using KFramework.MonoGame;

namespace KFramework.MonoGameExtend
{
    /// <summary>
    /// 扩展层的小工具：<see cref="SpriteBatch"/> 现在只有三个 <c>Draw</c>
    /// （简单位置形态 / 简单矩形形态 / 完整位置形态），这里把 UI 里最常用的那种画法 ——
    /// <b>「目标矩形 + 源矩形 + 旋转 / 锚点 / 翻转」</b> —— 换算成完整形态：
    /// 位置取目标矩形左上角，缩放按「目标矩形 : 源矩形」计算（等价于旧的那个"目标矩形既决定位置也决定缩放"的重载）。
    /// </summary>
    internal static class SpriteBatchRectDraw
    {
        /// <summary>按目标矩形绘制一个精灵（位置取矩形左上角、尺寸按矩形 : 源矩形 缩放）。</summary>
        internal static void DrawRect(this SpriteBatch batch, Texture2D texture, Rectangle destination,
                                      Rectangle? sourceRectangle, Color color, float rotation, Vector2 origin,
                                      SpriteEffects effects, float layerDepth)
            => batch.Draw(texture, destination, sourceRectangle, color, rotation, origin, null, effects, layerDepth);
    }
}
