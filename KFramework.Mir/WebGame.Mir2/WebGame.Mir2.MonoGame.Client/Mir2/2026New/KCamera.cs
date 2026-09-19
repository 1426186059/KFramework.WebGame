using Client;
using Client.MirGraphics;

namespace WebGame.Mir2.MonoGame.Client.Mir2._2026New
{
    //我们的照相机是一个简单的正交照相机，跟随玩家移动，照相机的坐标系是以玩家为中心的，屏幕坐标系是以屏幕左上角为原点的。
    public class KCamera
    {
        // 把视口(画布)像素坐标转换为 UI 逻辑坐标(Settings.ScreenWidth/Height = 1024x768)。
        // 渲染端对 UI 层施加 CreateScaleTranslation(s, s, 0, 0)（s = 视口高 / 768，以高为准缩放、平移为 0），
        // 逻辑(0,0) 对齐屏(0,0)；这里做它的逆变换 logical = screen / s，使鼠标命中检测
        // （DisplayRectangle 用逻辑坐标）与可见位置对齐。CMain.MPoint 统一为逻辑坐标，供 UI 命中检测
        // 与各场景（含地图）的鼠标逻辑使用——否则会出现"鼠标不在按钮上却高亮/点击错位"。
        public static MirEngine.Point ScreenToWorldPos(MirEngine.Point mPoint)
        {
            var vp = DXManager.GDevice.Viewport;
            if (vp.Height <= 0 || Settings.ScreenHeight <= 0) return mPoint;

            float s = (float)vp.Height / Settings.ScreenHeight;
            int x = (int)((mPoint.X - vp.X) / s);
            int y = (int)((mPoint.Y - vp.Y) / s);
            return new MirEngine.Point(x, y);
        }

        public static MirEngine.Point WorldPosToScreen(MirEngine.Point mPoint)
        {
            return MirEngine.Point.Empty;
        }
    }
}
