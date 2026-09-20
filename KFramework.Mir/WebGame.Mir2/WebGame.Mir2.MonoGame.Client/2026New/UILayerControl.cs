using Client;
using Client.MirGraphics;

namespace WebGame.Mir2.MonoGame.Client
{
    // UI 层：承载对话框 / HUD 等界面控件。
    //
    // 本层为【恒等变换】：UI 以画布原生分辨率布局并 1:1 上屏，不做任何缩放。
    // 窗口尺寸变化的适配改由锚点重排负责（MirAnchor / MirControl.Relayout / MirScene.RelayoutAll）。
    //
    // 为什么不再用层变换拉伸：
    //  - 非等比拉伸（sx = 视口宽/1024、sy = 视口高/768）会让 UI 变形——圆变椭圆、字体横向压扁，
    //    宽高比与目标分辨率差得越多越明显，不可接受。
    //  - 等比缩放虽不变形，但在宽高比不同的屏幕上必然留黑边。
    // 故采用"位置靠锚点、尺寸靠可选的等比 UIScale"：两者都不产生 x/y 拉伸不一致。
    //
    // 因为本层是恒等变换，KCamera.ScreenToWorldPos 亦为恒等，鼠标命中坐标天然对齐，
    // 不需要再做逆缩放运算。
    public sealed class UILayerControl : LayerControl
    {
        protected override KFramework.MonoGame.Matrix4x4 GetLayerTransform(int viewportWidth, int viewportHeight)
        {
            return KFramework.MonoGame.Matrix4x4.CreateScaleTranslation(1f, 1f, 0f, 0f);
        }

        public override Size Size { 
            get => new Size(DXManager.GDevice.Viewport.Width, DXManager.GDevice.Viewport.Height); 
            set; }

    }
}
