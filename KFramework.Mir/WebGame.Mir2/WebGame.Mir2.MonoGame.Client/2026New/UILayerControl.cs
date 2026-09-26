using Client.MirGraphics;

namespace WebGame.Mir2.MonoGame.Client
{
    public sealed class UILayerControl : LayerControl
    {
        protected override KFramework.MonoGame.Matrix4x4 GetLayerTransform(int viewportWidth, int viewportHeight)
        {
            return KFramework.MonoGame.Matrix4x4.CreateScaleTranslation(1f, 1f, 0f, 0f);
        }

        public override Size Size { 
            get => new Size(DXManager.GDevice.Viewport.Width, DXManager.GDevice.Viewport.Height); 
            set; }

        // TrueSize 默认读 _size 字段，而本层从不给它赋值，会恒为 (0,0)。
        // 对话框的 Parent 是 UILayer，拖拽边界钳制(Parent.TrueSize)会因此把位置钳到 (0,0)。
        // 让 TrueSize 与 Size 一致返回实时视口，钳制才能按真实屏幕尺寸进行。
        public override Size TrueSize
            => new Size(DXManager.GDevice.Viewport.Width, DXManager.GDevice.Viewport.Height);


    }
}
