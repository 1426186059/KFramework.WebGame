namespace KFramework.MonoGame
{
    /// <summary>
    /// 找不到满足给定设备偏好的图形设备时抛出（照 MonoGame 的 Graphics.NoSuitableGraphicsDeviceException）。
    /// </summary>
    public sealed class NoSuitableGraphicsDeviceException : Exception
    {
        /// <summary>构造一个不带消息的异常。</summary>
        public NoSuitableGraphicsDeviceException()
            : base()
        {
        }

        /// <summary>
        /// 用指定的错误消息构造异常。
        /// </summary>
        /// <param name="message">描述错误的消息。</param>
        public NoSuitableGraphicsDeviceException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// 用指定的错误消息与引发异常的内部异常构造异常。
        /// </summary>
        /// <param name="message">描述错误的消息。</param>
        /// <param name="inner">导致当前异常的异常。</param>
        public NoSuitableGraphicsDeviceException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
