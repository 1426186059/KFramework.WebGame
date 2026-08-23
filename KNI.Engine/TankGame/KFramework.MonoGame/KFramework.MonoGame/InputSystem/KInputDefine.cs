using Microsoft.Xna.Framework;
using System;

namespace KFramework.MonoGame
{
    /// <summary>
    /// 鼠标按键
    /// </summary>
    public enum MouseButton
    {
        Left,
        Right,
        Middle,
        XButton1,
        XButton2,
    }

    /// <summary>
    /// 指针来源设备
    /// </summary>
    public enum KPointerSource
    {
        Mouse,
        Touch,
    }

    /// <summary>
    /// 按键的瞬时状态
    /// </summary>
    public enum KPressState
    {
        /// <summary>未按下</summary>
        None,
        /// <summary>本帧刚按下</summary>
        Down,
        /// <summary>持续按住</summary>
        Held,
        /// <summary>本帧刚抬起</summary>
        Up,
    }

    /// <summary>
    /// 指针（鼠标 / 触摸）事件参数
    /// </summary>
    public class KPointerEventArgs : EventArgs
    {
        /// <summary>当前屏幕坐标</summary>
        public Vector2 Position { get; set; }

        /// <summary>按下时的屏幕坐标</summary>
        public Vector2 PressPosition { get; set; }

        /// <summary>相对上一帧的位移</summary>
        public Vector2 Delta { get; set; }

        /// <summary>来源设备</summary>
        public KPointerSource Source { get; set; }

        /// <summary>鼠标按键（触摸时恒为 Left）</summary>
        public MouseButton Button { get; set; }

        /// <summary>触摸点 id（鼠标时用按键序号）</summary>
        public int PointerId { get; set; }

        /// <summary>事件是否已被消费</summary>
        public bool Used { get; private set; }

        /// <summary>整数坐标，便于 Rectangle.Contains</summary>
        public Point PositionPoint => new Point((int)Position.X, (int)Position.Y);

        public void Use() => Used = true;

        public void Reset()
        {
            Used = false;
        }
    }

    /// <summary>
    /// 可点击对象 —— 任何想接收指针事件的对象都实现这个接口
    /// </summary>
    public interface IClickable
    {
        /// <summary>是否参与事件分发</summary>
        bool RaycastEnabled { get; }

        /// <summary>碰撞区域（屏幕坐标）</summary>
        Rectangle Bounds { get; }

        /// <summary>优先级，数值越大越先接收事件</summary>
        int Priority { get; }

        /// <summary>指针按下</summary>
        bool OnPointerDown(KPointerEventArgs args);

        /// <summary>指针抬起</summary>
        bool OnPointerUp(KPointerEventArgs args);

        /// <summary>完整点击（按下与抬起都在区域内）</summary>
        bool OnPointerClick(KPointerEventArgs args);

        /// <summary>指针移入</summary>
        void OnPointerEnter(KPointerEventArgs args);

        /// <summary>指针移出</summary>
        void OnPointerExit(KPointerEventArgs args);
    }

    /// <summary>
    /// 输入设备统一接口，由 KInputMgr 调度
    /// </summary>
    public interface IKInputDevice
    {
        /// <summary>设备名，用于查询与调试</summary>
        string Name { get; }

        /// <summary>是否启用（禁用后不再轮询）</summary>
        bool Enabled { get; set; }

        /// <summary>当前平台是否可用</summary>
        bool IsAvailable { get; }

        /// <summary>初始化，KInputMgr 创建时调用一次</summary>
        void Init();

        /// <summary>每帧轮询一次</summary>
        void Update(GameTime gameTime);

        /// <summary>重置内部状态（如切场景、失焦）</summary>
        void Reset();
    }
}
