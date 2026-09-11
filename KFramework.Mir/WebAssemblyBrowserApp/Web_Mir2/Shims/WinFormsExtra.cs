// 补齐 System.Windows.Forms 兼容壳中 SystemWindowsForms.cs 未覆盖、但 Mir2 代码用到的类型。
namespace MirEngine
{
    public enum FormBorderStyle
    {
        None = 0,
        FixedDialog = 1,
        Sizable = 2,
        FixedSingle = 3,
        Fixed3D = 4,
        FixedToolWindow = 5,
        SizableToolWindow = 6
    }

    public enum ScreenOrientation
    {
        Angle0 = 0,
        Angle90 = 1,
        Angle180 = 2,
        Angle270 = 3
    }

    public delegate void MouseEventHandler(object sender, MouseEventArgs e);
    public delegate void KeyEventHandler(object sender, KeyEventArgs e);
    public delegate void KeyPressEventHandler(object sender, KeyPressEventArgs e);
}
