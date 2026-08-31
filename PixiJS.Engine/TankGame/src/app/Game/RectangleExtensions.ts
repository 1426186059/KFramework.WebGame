import { Bounds, Point, Rectangle } from "pixi.js";

export class RectangleExtensions
{
    /**
     * 计算两个 Pixi 矩形对象的碰撞深度
     * @param rectA - Pixi 的矩形对象 (如 new Rectangle(...) 或 sprite.getBounds())
     * @param rectB - 另一个 Pixi 矩形对象
     * @returns 返回 Pixi.Point，包含分离所需的 x/y 深度。未碰撞则返回 {x: 0, y: 0}
     */
    public static getIntersectionDepth(rectA: Bounds,  rectB: Bounds): Point 
    {
        // 1. 直接使用 Pixi 矩形自带的 x, y, width, height 属性计算半宽高
        const halfWidthA = rectA.width / 2.0;
        const halfHeightA = rectA.height / 2.0;
        const halfWidthB = rectB.width / 2.0;
        const halfHeightB = rectB.height / 2.0;

        // 2. 计算中心点 (可以直接用 Pixi.Point)
        const centerA = new Point(rectA.x + halfWidthA, rectA.y + halfHeightA);
        const centerB = new Point(rectB.x + halfWidthB, rectB.y + halfHeightB);

        // 3. 计算中心点距离
        const distanceX = centerA.x - centerB.x;
        const distanceY = centerA.y - centerB.y;
        
        // 4. 计算刚好接触的临界距离
        const minDistanceX = halfWidthA + halfWidthB;
        const minDistanceY = halfHeightA + halfHeightB;

        // 5. 判断是否发生碰撞 (分离轴定理)
        if (Math.abs(distanceX) >= minDistanceX || Math.abs(distanceY) >= minDistanceY) 
        {
            return new Point(0, 0);
        }

        // 6. 计算重叠深度 (完全复刻那位大哥的 C# 逻辑)
        const depthX = distanceX > 0 
            ? minDistanceX - distanceX 
            : -minDistanceX - distanceX;

        const depthY = distanceY > 0 
            ? minDistanceY - distanceY 
            : -minDistanceY - distanceY;

        return new Point(depthX, depthY);
    }
}