namespace Client.MirControls
{
    /// <summary>
    /// 控件相对画布的锚点位置 —— 九宫格：4 角 + 4 边中 + 中心。
    ///
    /// 与 MirControl 的 #region Positions 九个属性一一对应（它们读的是活的
    /// Settings.ScreenWidth/Height），只是名字更规整：
    ///   TopCenter  = 原 Top        MiddleLeft  = 原 Left     MiddleCenter = 原 Center
    ///   BottomCenter = 原 Bottom   MiddleRight = 原 Right    其余四角同名。
    ///
    /// 只做这 9 个位置 + 一个固定偏移（MirControl.AnchorPos）即可覆盖原版的布局需求：
    /// 贴边、居中、居中后再偏移。更复杂的布局可覆写 MirControl.ApplyAnchor。
    /// </summary>
    public enum EAnchorType
    {
        /// <summary>不使用锚点（默认）：ApplyAnchor 对其不做任何事，控件保持绝对定位。</summary>
        None = 0,

        TopLeft,
        TopCenter,
        TopRight,

        MiddleLeft,
        MiddleCenter,
        MiddleRight,

        BottomLeft,
        BottomCenter,
        BottomRight
    }
}
