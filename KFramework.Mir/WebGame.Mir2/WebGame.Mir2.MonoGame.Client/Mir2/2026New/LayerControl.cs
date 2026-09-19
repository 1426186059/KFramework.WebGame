using Client;
using Client.MirControls;
using Client.MirGraphics;
using KFramework.MonoGame;
using SlimDX.Direct3D9;

namespace WebGame.Mir2.MonoGame.Client.Mir2._2026New
{
    // 层容器：带自身变换把子控件烘焙到离屏纹理，供 MirScene.DrawControl 分层上屏；
    // 同时作为 UI 层 / 世界层的统一父节点。
    // 鼠标命中测试委托给子控件，使命中派发能下钻到层内具体控件（按钮 / 文本框等），
    // 并保留“UI 层优先、未命中再回退世界层”的语义。
    public sealed class LayerControl : MirControl
    {
        // 该层的世界→屏幕变换（视口尺寸驱动），由 MirScene 构造时按角色赋值。
        public Func<int, int, Matrix4x4> LayerTransform;

        public void Bake()
        {
            if (TextureValid) return;
            CreateTexture();
        }

        public Texture2D RenderTargetTexture => ControlTexture?.RenderTarget;

        public void Invalidate() => TextureValid = false;

        public void Add(MirControl control) => AddControl(control);
        public void Insert(int index, MirControl control) => InsertControl(index, control);

        // 层容器本身无可见矩形，命中测试下钻到子控件：任一子控件命中即返回 true，
        // 使 MirControl.OnMouseMove 的递归能进入层内找到真正的目标控件（否则 MouseControl 收不到，
        // 表现为按钮点击、输入框光标全部失效）。
        public override bool IsMouseOver(MirEngine.Point p)
        {
            if (!Visible) return false;
            for (int i = Controls.Count - 1; i >= 0; i--)
                if (Controls[i].IsMouseOver(p))
                    return true;
            return false;
        }

        protected override void CreateTexture()
        {
            var vp = DXManager.GDevice.Viewport;
            int rtW = vp.Width, rtH = vp.Height;

            if (TextureSize.Width != rtW || TextureSize.Height != rtH)
                DisposeTexture();

            if (ControlTexture == null || ControlTexture.Disposed)
            {
                DXManager.ControlList.Add(this);
                ControlTexture = new SlimDX.Direct3D9.Texture(DXManager.Device, rtW, rtH, 1, Usage.RenderTarget, Format.A8R8G8B8, Pool.Default);
                TextureSize = new Size(rtW, rtH);
            }
            Surface oldSurface = DXManager.CurrentSurface;
            Surface surface = ControlTexture.GetSurfaceLevel(0);
            DXManager.SetSurface(surface);

            DXManager.Device.Clear(ClearFlags.Target, BackColour, 0, 0);

            DXManager.RenderTransform = LayerTransform != null
                ? LayerTransform(vp.Width, vp.Height)
                : Matrix4x4.CreateScaleTranslation((float)vp.Height / Settings.ScreenHeight, (float)vp.Height / Settings.ScreenHeight, 0, 0f);
            try
            {
                BeforeDrawControl();
                DrawChildControls();
                AfterDrawControl();
            }
            finally
            {
                DXManager.RenderTransform = null;
            }

            DXManager.SetSurface(oldSurface);
            TextureValid = true;
            surface.Dispose();
        }
    }
}
