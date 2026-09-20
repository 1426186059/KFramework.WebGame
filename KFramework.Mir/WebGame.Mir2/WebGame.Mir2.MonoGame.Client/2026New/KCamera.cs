using Client;
using Client.MirGraphics;

namespace WebGame.Mir2.MonoGame.Client
{
    //我们的照相机是一个简单的正交照相机，跟随玩家移动，照相机的坐标系是以玩家为中心的，屏幕坐标系是以屏幕左上角为原点的。
    public class KCamera
    {
        // 把视口(画布)像素坐标转换为 UI 逻辑坐标(Settings.ScreenWidth/Height = 1024x768)。
        // 渲染端对 UI 层施加 CreateScaleTranslation(sx, sy, 0, 0) 做非均匀拉伸铺满
        // （sx = 视口宽/Settings.ScreenWidth、sy = 视口高/Settings.ScreenHeight，逻辑(0,0) 对齐屏(0,0)），
        // 这里做它的逆变换 logical = (screen - vp.origin) / (sx, sy)，使鼠标命中检测
        // （DisplayRectangle 用逻辑坐标）与可见位置对齐。CMain.MPoint 统一为逻辑坐标，供 UI 命中检测
        // 与各场景（含地图）的鼠标逻辑使用——否则会出现"鼠标不在按钮上却高亮/点击错位"。
        // 注意：缩放仅作用于 UI 层逻辑坐标；世界层为 1:1 窗口像素、与此无关。
        public static MirEngine.Point ScreenToWorldPos(MirEngine.Point mPoint)
        {
            var vp = DXManager.GDevice.Viewport;
            if (vp.Height <= 0 || Settings.ScreenHeight <= 0 || vp.Width <= 0 || Settings.ScreenWidth <= 0) return mPoint;

            float sx = (float)vp.Width / Settings.ScreenWidth;
            float sy = (float)vp.Height / Settings.ScreenHeight;
            int x = (int)((mPoint.X - vp.X) / sx);
            int y = (int)((mPoint.Y - vp.Y) / sy);
            return new MirEngine.Point(x, y);
        }

        public static MirEngine.Point WorldPosToScreen(MirEngine.Point mPoint)
        {
            return MirEngine.Point.Empty;
        }
    }
}
