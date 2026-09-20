namespace WebGame.Mir2.MonoGame.Client
{
    // 世界层：承载地图 / 角色 / 物品等【世界坐标】内容。
    // 投影变换恒为单位矩阵（1:1 透传，不缩放、不 aspect-fill）——世界坐标本就不该被放大，
    // 窗口更大只是“看到更多世界”（以 48x32 世界像素为单位的视口更大），而非放大世界。
    // MapControl 内鼠标即世界像素（与屏幕 1:1），与 OffSetX/Y、ViewRangeX/Y 一致。
    // 注意：KCamera.ScreenToWorldPos 的 s=h/768 只服务于【UI 层逻辑坐标】，与世界层无关。
    public sealed class WorldLayerControl : LayerControl
    {
        protected override KFramework.MonoGame.Matrix4x4 GetLayerTransform(int viewportWidth, int viewportHeight)
            => KFramework.MonoGame.Matrix4x4.CreateScaleTranslation(1f, 1f, 0f, 0f);
    }
}
