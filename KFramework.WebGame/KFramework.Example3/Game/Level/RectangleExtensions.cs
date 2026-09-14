using System;

namespace KFramework.Example3
{
    public static class RectangleExtensions
    {
        public static Vector2 GetIntersectionDepth(this Rectangle rectA, Rectangle rectB)
        {
            float halfWidthA = rectA.Width / 2.0f;
            float halfHeightA = rectA.Height / 2.0f;
            float halfWidthB = rectB.Width / 2.0f;
            float halfHeightB = rectB.Height / 2.0f;

            Vector2 centerA = new Vector2(rectA.Left + halfWidthA, rectA.Top + halfHeightA);
            Vector2 centerB = new Vector2(rectB.Left + halfWidthB, rectB.Top + halfHeightB);

            float distanceX = centerA.X - centerB.X;
            float distanceY = centerA.Y - centerB.Y;
            float minDistanceX = halfWidthA + halfWidthB;
            float minDistanceY = halfHeightA + halfHeightB;

            if (Math.Abs(distanceX) >= minDistanceX || Math.Abs(distanceY) >= minDistanceY)
            {
                return Vector2.Zero;
            }

            //depthX：水平方向重叠了多少。
            //depthY：垂直方向重叠了多少。
            //正负号表示方向（谁在谁的左边/右边）
            //返回的 Vector2 告诉你：要把两个矩形分开，最少需要移动多少距离。
            //比如返回值是 (3, 0)，说明水平方向重叠了3个像素，你只要把其中一个往旁边推3个像素，它们就不重叠了。
            //这在游戏里经常用来做碰撞响应——角色碰到墙壁后，根据这个深度把角色"推出来"，防止穿墙。
            //minDistanceX:如果重叠,这个是最大距离
            //minDistanceY:如果重叠,这个是最大距离
            float depthX = distanceX > 0 ? minDistanceX - distanceX : -minDistanceX - distanceX;
            float depthY = distanceY > 0 ? minDistanceY - distanceY : -minDistanceY - distanceY;
            return new Vector2(depthX, depthY);
        }
        
        public static Vector2 GetBottomCenter(this Rectangle rect)
        {
            return new Vector2(rect.X + rect.Width / 2.0f, rect.Bottom);
        }
    }
}