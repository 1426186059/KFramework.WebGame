using Client;
using Client.MirGraphics;

namespace WebGame.Mir2.MonoGame.Client
{
    //我们的照相机是一个简单的正交照相机，跟随玩家移动，照相机的坐标系是以玩家为中心的，屏幕坐标系是以屏幕左上角为原点的。
    public class KCamera
    {
        // 把视口(画布)像素坐标转换为 UI 坐标。
        // UI 层已是恒等变换（UILayerControl.GetLayerTransform），UI 以画布原生分辨率布局，
        // 故这里只需扣掉视口原点偏移，【不再做任何逆缩放】——命中检测与可见位置天然对齐。
        // 保留 vp.X/vp.Y 偏移以兼容宿主把 Viewport 设在非原点的情况。
        public static MirEngine.Point ScreenToWorldPos(MirEngine.Point mPoint)
        {
            var vp = DXManager.GDevice.Viewport;
            if (vp.Width <= 0 || vp.Height <= 0) return mPoint;

            return new MirEngine.Point(mPoint.X - vp.X, mPoint.Y - vp.Y);
        }

        public static MirEngine.Point WorldPosToScreen(MirEngine.Point mPoint)
        {
            return MirEngine.Point.Empty;
        }
    }
}
