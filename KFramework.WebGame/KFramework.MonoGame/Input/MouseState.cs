using KFramework.MonoGame;

namespace KFramework.MonoGame
{
    /// <summary>
    /// 鼠标状态快照。
    ///
    /// <para>与 <see cref="KeyboardState"/> 不同，MouseState 全部由 int / bool 值字段组成，
    /// 是<b>真正的值语义</b>，可以安全地跨帧缓存（上层 <c>KMouseInput</c> 就是靠缓存它做边沿检测的）。</para>
    ///
    /// <para>适配说明：本引擎的鼠标只上报 左 / 中 / 右 三键，不支持 XButton；
    /// 滚轮只有本帧增量（无累计值、无横向滚轮）；
    /// 浏览器不允许脚本移动光标，因此没有 SetPosition 能力。</para>
    ///
    /// <para>坐标单位为<b>画布像素</b>，原点在左上角。</para>
    /// </summary>
    public struct MouseState(int x, int y, int buttons, int wheel, int previousButtons)
    {
        public readonly int X = x;
        public readonly int Y = y;
        public readonly int Buttons = buttons;
        public readonly int Wheel = wheel;
        private readonly int _previousButtons = previousButtons;

        /// <summary>鼠标位置。</summary>
        public Vector2 Position => new(X, Y);

        /// <summary>左键按住。</summary>
        public bool LeftButton => (Buttons & 1) != 0;

        /// <summary>中键按住。</summary>
        public bool MiddleButton => (Buttons & 2) != 0;

        /// <summary>右键按住。</summary>
        public bool RightButton => (Buttons & 4) != 0;

        /// <summary>左键本帧刚按下。</summary>
        public bool LeftPressed => LeftButton && (_previousButtons & 1) == 0;

        /// <summary>左键本帧刚抬起。</summary>
        public bool LeftReleased => !LeftButton && (_previousButtons & 1) != 0;

        /// <summary>滚轮本帧增量（正数为向下滚）。</summary>
        public int ScrollDelta => Wheel;
    }
}
