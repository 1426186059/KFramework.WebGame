// 补齐 System.Windows.Forms 兼容壳中 SystemWindowsForms.cs 未覆盖、但 Mir2 代码用到的类型。
namespace MirEngine
{
    public enum FormBorderStyle
    {
        None = 0
    }

    public delegate void MouseEventHandler(object sender, MouseEventArgs e);
    public delegate void KeyEventHandler(object sender, KeyEventArgs e);
    public delegate void KeyPressEventHandler(object sender, KeyPressEventArgs e);
}
