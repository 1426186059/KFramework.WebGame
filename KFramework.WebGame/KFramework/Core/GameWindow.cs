using KFramework.Graphics;

namespace KFramework;

/// <summary>浏览器画布窗口。</summary>
public sealed class GameWindow
{
    private readonly GraphicsDevice _device;

    internal GameWindow(GraphicsDevice device) => _device = device;

    /// <summary>绘制缓冲宽度（物理像素，已含设备像素比）。</summary>
    public int Width => _device.Viewport.Width;

    /// <summary>绘制缓冲高度（物理像素，已含设备像素比）。</summary>
    public int Height => _device.Viewport.Height;

    public Vector2 Size => new(Width, Height);

    public float DevicePixelRatio => _device.DevicePixelRatio;

    /// <summary>CSS 像素尺寸，用于把鼠标坐标换算到逻辑坐标。</summary>
    public Vector2 CssSize => _device.CssSize;

    public string Title
    {
        set => Platform.SetTitle(value);
    }

    /// <summary>画布尺寸变化时触发（含浏览器缩放、手机旋转）。</summary>
    public event Action? SizeChanged;

    internal void RaiseSizeChanged() => SizeChanged?.Invoke();
}
