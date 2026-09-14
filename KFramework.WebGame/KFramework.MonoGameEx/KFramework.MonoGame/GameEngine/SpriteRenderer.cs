using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace KFramework.MonoGame
{
    /// <summary>
    /// 对标 Unity 的 SpriteRenderer。
    /// 挂载到 KTransform 节点上，自动按节点的世界变换（位置/旋转/缩放）绘制精灵。
    /// 约定：绘制发生在外部已 Begin 的 SpriteBatch 上下文里（与 Level 的 viewMatrix 体系一致），
    /// 本组件只负责 Draw，不 Begin/End。
    /// </summary>
    public class SpriteRenderer : KTransform
    {
        public enum SpriteDrawMode
        {
            Simple,  // 按 Size 直接拉伸
            Sliced,  // 九宫格，边缘不缩放、中间缩放
            Tiled,   // 按原始像素平铺填充 Size
        }

        // ===== 对标 Unity SpriteRenderer 的属性 =====

        private KSprite m_Sprite;
        /// <summary>要渲染的精灵（对标 sprite）。</summary>
        public KSprite Sprite
        {
            get => m_Sprite;
            set
            {
                m_Sprite = value;
                // 未显式指定 size 时，跟随原始精灵像素尺寸
                if (!m_SizeOverridden && value.Texture != null)
                {
                    m_Size = value.Rectangle.Size.ToVector2();
                }
            }
        }

        /// <summary>叠加颜色（对标 color）。默认白色=原色。</summary>
        public Color Color { get; set; } = Color.White;

        private bool m_FlipX;
        /// <summary>水平翻转（对标 flipX）。</summary>
        public bool FlipX
        {
            get => m_FlipX;
            set
            {
                m_FlipX = value;
                UpdateSpriteEffects();
            }
        }

        private bool m_FlipY;
        /// <summary>垂直翻转（对标 flipY）。</summary>
        public bool FlipY
        {
            get => m_FlipY;
            set
            {
                m_FlipY = value;
                UpdateSpriteEffects();
            }
        }

        private int m_SortingOrder;
        /// <summary>同层内的绘制顺序，越大越靠前（配合 BackToFront 排序）。对标 sortingOrder。</summary>
        public int SortingOrder
        {
            get => m_SortingOrder;
            set
            {
                m_SortingOrder = value;
                m_LayerDepth = ComputeLayerDepth(value, m_SortingLayer);
            }
        }

        private int m_SortingLayer;
        /// <summary>排序层，越大越靠前。对标 sortingLayerID/sortingLayer。</summary>
        public int SortingLayer
        {
            get => m_SortingLayer;
            set
            {
                m_SortingLayer = value;
                m_LayerDepth = ComputeLayerDepth(m_SortingOrder, value);
            }
        }

        private SpriteDrawMode m_DrawMode = SpriteDrawMode.Simple;
        /// <summary>绘制模式：Simple / Sliced(九宫格) / Tiled(平铺)。</summary>
        public SpriteDrawMode DrawMode
        {
            get => m_DrawMode;
            set => m_DrawMode = value;
        }

        private Vector2 m_Size = new Vector2(100, 100);
        private bool m_SizeOverridden;
        /// <summary>显示尺寸（像素，未缩放前）。Simple/Sliced 模式使用。设置后覆盖自动尺寸。</summary>
        public Vector2 Size
        {
            get => m_Size;
            set
            {
                m_Size = value;
                m_SizeOverridden = true;
            }
        }

        /// <summary>轴心（归一化 0-1，对标 Sprite.pivot）。(0,0)=左上，(0.5,0.5)=中心。
        /// Simple/Tiled 模式下生效。</summary>
        public Vector2 Pivot { get; set; } = new Vector2(0, 0);

        /// <summary>九宫格边距（左/上/右/下），单位为精灵原始像素。Sliced/Tiled 模式使用。</summary>
        public Rectangle Border { get; set; } = Rectangle.Empty;

        // ===== 内部状态 =====

        private SpriteEffects m_SpriteEffects = SpriteEffects.None;
        private float m_LayerDepth;

        public Rectangle Collider2DZone
        {
            get
            {
                Vector2 worldSize = Size * WorldScale;
                return new Rectangle(
                    (WorldPosition - worldSize * Pivot).ToPoint(),
                    worldSize.ToPoint());
            }
        }

        public SpriteRenderer()
        {
            m_LayerDepth = ComputeLayerDepth(m_SortingOrder, m_SortingLayer);
        }

        public SpriteRenderer(KSprite sprite) : this()
        {
            Sprite = sprite;
        }

        private void UpdateSpriteEffects()
        {
            SpriteEffects fx = SpriteEffects.None;
            if (m_FlipX) fx |= SpriteEffects.FlipHorizontally;
            if (m_FlipY) fx |= SpriteEffects.FlipVertically;
            m_SpriteEffects = fx;
        }

        // 把 (layer, order) 映射到 MonoGame 的 layerDepth(0~1)。
        // 配合 SpriteSortMode.BackToFront：depth 越大越后画=越靠前（与 Unity sortingOrder 语义一致）。
        private static float ComputeLayerDepth(int order, int layer)
        {
            const int MaxOrder = 1000;
            const int MaxLayer = 32;
            float layerPart = (layer + MaxLayer) * 1.0f / (MaxLayer * 2 + 1);
            float orderPart = (order + MaxOrder) * 1.0f / (MaxOrder * 2 + 1) / (MaxLayer * 2 + 1);
            return MathHelper.Clamp(layerPart + orderPart, 0f, 1f);
        }

        public override void Draw()
        {
            if (m_Sprite.Texture == null)
                return;

            switch (m_DrawMode)
            {
                case SpriteDrawMode.Sliced:
                    DrawSliced();
                    break;
                case SpriteDrawMode.Tiled:
                    DrawTiled();
                    break;
                default:
                    DrawSimple();
                    break;
            }
        }

        // ===== Simple：按 Size 直接拉伸 =====
        private void DrawSimple()
        {
            var batch = KSceneMgr.SpriteBatch;
            Vector2 worldPos = WorldPosition;
            Vector2 origin = Pivot * m_Sprite.Rectangle.Size.ToVector2();
            Rectangle target = new Rectangle(
                worldPos.ToPoint(),
                (m_Size * WorldScale).ToPoint());

            batch.Draw(
                m_Sprite.Texture,
                target,
                m_Sprite.Rectangle,
                Color,
                WorldRotation,
                origin,
                m_SpriteEffects,
                m_LayerDepth);
        }

        // ===== Sliced：九宫格 =====
        private void DrawSliced()
        {
            var batch = KSceneMgr.SpriteBatch;
            Rectangle src = m_Sprite.Rectangle;
            Rectangle b = Rectangle.Empty;
            if (m_Sprite.mRef != null)
            {
                b = m_Sprite.mRef.Border;
            }

            // 源图九宫格切分
            int left = b.Left, top = b.Top, right = b.Right, bottom = b.Bottom;
            int centerW = src.Width - left - right;
            int centerH = src.Height - top - bottom;
            if (centerW < 1) centerW = 1;
            if (centerH < 1) centerH = 1;

            // 目标尺寸（含世界缩放）
            Vector2 scaledSize = m_Size * WorldScale;
            int dstW = (int)Math.Round(scaledSize.X);
            int dstH = (int)Math.Round(scaledSize.Y);

            // 目标边宽：保持角/边原始像素尺寸（不受拉伸影响）
            int dstLeft = (int)Math.Round(left * WorldScale.X);
            int dstTop = (int)Math.Round(top * WorldScale.Y);
            int dstRight = (int)Math.Round(right * WorldScale.X);
            int dstBottom = (int)Math.Round(bottom * WorldScale.Y);
            int dstCenterW = Math.Max(0, dstW - dstLeft - dstRight);
            int dstCenterH = Math.Max(0, dstH - dstTop - dstBottom);

            Vector2 origin = new Vector2(0); // 九宫格自身管理布局，不走 Pivot 旋转中心
            Vector2 pos = WorldPosition;
            float rot = WorldRotation;

            // 3x3 九块绘制
            DrawGridCell(batch, pos, rot, src, b,
                dstLeft, dstTop, dstRight, dstBottom, dstCenterW, dstCenterH,
                origin);
        }

        private void DrawGridCell(
            SpriteBatch batch, Vector2 basePos, float rot,
            Rectangle src, Rectangle b,
            int dstLeft, int dstTop, int dstRight, int dstBottom,
            int dstCenterW, int dstCenterH,
            Vector2 origin)
        {
            // 源九宫格矩形
            Rectangle[] srcCells = new Rectangle[9];
            srcCells[0] = new Rectangle(src.X, src.Y, b.Left, b.Top);
            srcCells[1] = new Rectangle(src.X + b.Left, src.Y, src.Width - b.Left - b.Right, b.Top);
            srcCells[2] = new Rectangle(src.X + src.Width - b.Right, src.Y, b.Right, b.Top);
            srcCells[3] = new Rectangle(src.X, src.Y + b.Top, b.Left, src.Height - b.Top - b.Bottom);
            srcCells[4] = new Rectangle(src.X + b.Left, src.Y + b.Top, src.Width - b.Left - b.Right, src.Height - b.Top - b.Bottom);
            srcCells[5] = new Rectangle(src.X + src.Width - b.Right, src.Y + b.Top, b.Right, src.Height - b.Top - b.Bottom);
            srcCells[6] = new Rectangle(src.X, src.Y + src.Height - b.Bottom, b.Left, b.Bottom);
            srcCells[7] = new Rectangle(src.X + b.Left, src.Y + src.Height - b.Bottom, src.Width - b.Left - b.Right, b.Bottom);
            srcCells[8] = new Rectangle(src.X + src.Width - b.Right, src.Y + src.Height - b.Bottom, b.Right, b.Bottom);

            // 目标九宫格尺寸
            int[] dstW = { dstLeft, dstCenterW, dstRight };
            int[] dstH = { dstTop, dstCenterH, dstBottom };

            for (int r = 0; r < 3; r++)
            {
                for (int c = 0; c < 3; c++)
                {
                    int w = dstW[c];
                    int h = dstH[r];
                    if (w <= 0 || h <= 0)
                        continue;

                    // 目标左上角（基于 basePos 的偏移，先按世界缩放偏移量累加，再整体旋转）
                    float ox = (c == 0 ? 0 : (c == 1 ? dstLeft : dstLeft + dstCenterW));
                    float oy = (r == 0 ? 0 : (r == 1 ? dstTop : dstTop + dstCenterH));
                    Vector2 cellPos = basePos + new Vector2(ox, oy);

                    // 旋转围绕 basePos（让九宫格整体随节点旋转）
                    Vector2 rotated = RotateAround(cellPos, basePos, rot);

                    Rectangle dstRect = new Rectangle(rotated.ToPoint(), new Point(w, h));

                    batch.Draw(
                        m_Sprite.Texture,
                        dstRect,
                        srcCells[r * 3 + c],
                        Color,
                        rot,
                        origin,
                        m_SpriteEffects,
                        m_LayerDepth);
                }
            }
        }

        // ===== Tiled：按原始像素平铺填充 Size =====
        private void DrawTiled()
        {
            var batch = KSceneMgr.SpriteBatch;
            Rectangle src = m_Sprite.Rectangle;
            Vector2 scaledSize = m_Size * WorldScale;
            int dstW = (int)Math.Round(scaledSize.X);
            int dstH = (int)Math.Round(scaledSize.Y);

            // 平铺单元尺寸：用精灵原始像素尺寸（不受 Size 拉伸影响，仅受 WorldScale）
            int tileW = (int)Math.Round(src.Width * WorldScale.X);
            int tileH = (int)Math.Round(src.Height * WorldScale.Y);
            if (tileW < 1) tileW = 1;
            if (tileH < 1) tileH = 1;

            Vector2 basePos = WorldPosition;
            float rot = WorldRotation;
            Vector2 origin = new Vector2(0);

            for (int y = 0; y < dstH; y += tileH)
            {
                for (int x = 0; x < dstW; x += tileW)
                {
                    int w = Math.Min(tileW, dstW - x);
                    int h = Math.Min(tileH, dstH - y);
                    if (w <= 0 || h <= 0)
                        continue;

                    Vector2 cellPos = basePos + new Vector2(x, y);
                    Vector2 rotated = RotateAround(cellPos, basePos, rot);
                    Rectangle dstRect = new Rectangle(rotated.ToPoint(), new Point(w, h));

                    // 末块若被裁切，对应截取源矩形
                    Rectangle sRect = src;
                    if (w < tileW || h < tileH)
                    {
                        sRect = new Rectangle(
                            src.X,
                            src.Y,
                            (int)Math.Round(w / WorldScale.X),
                            (int)Math.Round(h / WorldScale.Y));
                    }

                    batch.Draw(
                        m_Sprite.Texture,
                        dstRect,
                        sRect,
                        Color,
                        rot,
                        origin,
                        m_SpriteEffects,
                        m_LayerDepth);
                }
            }
        }

        private static Vector2 RotateAround(Vector2 point, Vector2 pivot, float angle)
        {
            if (angle == 0f)
                return point;
            float cos = (float)System.Math.Cos(angle);
            float sin = (float)System.Math.Sin(angle);
            Vector2 d = point - pivot;
            return pivot + new Vector2(d.X * cos - d.Y * sin, d.X * sin + d.Y * cos);
        }
    }
}
