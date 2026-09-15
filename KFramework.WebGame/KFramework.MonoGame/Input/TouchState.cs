using KFramework.MonoGame;

namespace KFramework.MonoGame
{
    /// <summary>
    /// 单个触点。手机 / 平板等触屏设备的输入单元。
    ///
    /// <para>座标单位为画布 CSS 像素（dpr 换算由上层按需处理）。
    /// <see cref="Id"/> 在同一根手指按下期间保持不变。</para>
    ///
    /// <para>阶段（Began / Moved / Ended）由 <see cref="Input"/> 依据事件直接判定，
    /// 见 <see cref="Input.BeganTouches"/> 等，不再需要上层做 id 差分。</para>
    /// </summary>
    public readonly struct TouchPoint(int id, Vector2 position)
    {
        /// <summary>触点 ID，同一根手指在按下期间保持不变。</summary>
        public readonly int Id = id;

        /// <summary>当前位置。</summary>
        public readonly Vector2 Position = position;
    }

    /// <summary>
    /// 当前留在屏上的触点集合（由 <see cref="Input"/> 维护）。
    /// 本帧发生变化的触点请取 <see cref="Input.BeganTouches"/> /
    /// <see cref="Input.MovedTouches"/> / <see cref="Input.EndedTouches"/>。
    /// </summary>
    public struct TouchCollection(IReadOnlyList<TouchPoint> touches)
    {
        private readonly IReadOnlyList<TouchPoint> _touches = touches;

        /// <summary>当前触点数量。</summary>
        public int Count => _touches == null ? 0 : _touches.Count;

        /// <summary>按索引取触点。</summary>
        public TouchPoint this[int index] => _touches[index];
    }
}
