namespace KFramework.MonoGame
{
    internal class HTML_Window
    {
        public static HTML_Window Current = null;
        public static float DevicePixelRatio = 1000;

        public HTML_Window()
        {
            if (Current != null)
            {
                throw new InvalidOperationException("HTML_Window.Current 已存在，不能重复创建。");
            }
            Current = this;
        }
    }
}
