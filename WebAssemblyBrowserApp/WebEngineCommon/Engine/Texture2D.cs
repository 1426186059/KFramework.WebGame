#nullable enable
using System;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace WebAssemblyBrowserApp.Engine;

/// <summary>
/// 动态纹理（对标 MonoGame Texture2D）：CPU 侧持有像素缓冲，
/// <see cref="SetData(int[])"/> 修改像素颜色，<see cref="Commit"/> 把整块像素重传到 GPU，
/// 之后像普通图片一样用 <see cref="Draw"/> 绘制。
/// 典型场景：FC PPU 帧缓冲 —— 每帧改像素 → Commit → Draw，全量重传即可（256×240 ≈ 240KB/帧）。
/// 像素格式：ARGB8888（0xAARRGGBB，A 在最高 8 位）。
/// </summary>
[SupportedOSPlatform("browser")]
public sealed partial class Texture2D : IDisposable
{
    private static int _nextId = 1;

    public int Width { get; }
    public int Height { get; }
    /// <summary>纹理句柄 id（三端 assets 桥内部各自映射，互不冲突）。</summary>
    public int Id { get; }
    /// <summary>首次 <see cref="Commit"/> 成功并上传后为 true；<see cref="Dispose"/> 后为 false。</summary>
    public bool Ready { get; private set; }

    private int[] _pixels;

    public Texture2D(int width, int height)
    {
        if (width <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "纹理尺寸必须为正。");
        Width = width;
        Height = height;
        Id = _nextId++;
        _pixels = new int[width * height];
    }

    public Texture2D(int width, int height, int[] pixelData) : this(width, height)
        => SetData(pixelData);

    /// <summary>
    /// 直接访问内部像素缓冲（ARGB8888，长度 Width×Height）。
    /// 适合逐像素高频写入（如 FC PPU 每帧刷帧缓冲），修改后调用 <see cref="Commit"/> 生效。
    /// 注意：只可读写内容，不要替换引用或改变数组长度。
    /// </summary>
    public int[] Pixels => _pixels;

    /// <summary>整体替换像素（长度必须等于 Width × Height）。</summary>
    public void SetData(int[] pixelData)
    {
        ArgumentNullException.ThrowIfNull(pixelData);
        if (pixelData.Length != _pixels.Length)
            throw new ArgumentException(
                $"pixelData.Length={pixelData.Length} 与纹理尺寸 {Width}×{Height}（需 {_pixels.Length}）不匹配。",
                nameof(pixelData));
        Array.Copy(pixelData, _pixels, _pixels.Length);
    }

    /// <summary>清空为指定颜色（默认全透明黑 0x00000000）。</summary>
    public void Clear(int argb = 0) => Array.Fill(_pixels, argb);

    /// <summary>写单个像素（越界自动忽略）。</summary>
    public void SetPixel(int x, int y, int argb)
    {
        if ((uint)x >= (uint)Width || (uint)y >= (uint)Height) return;
        _pixels[y * Width + x] = argb;
    }

    /// <summary>读单个像素（越界返回 0）。</summary>
    public int GetPixel(int x, int y)
        => (uint)x < (uint)Width && (uint)y < (uint)Height ? _pixels[y * Width + x] : 0;

    /// <summary>
    /// 把 srcW × srcH 的像素块写入 (x, y) 区域（局部更新，如精灵/字符/瓦片更新）。
    /// pixelData 按行主序排列，长度至少 srcW × srcH。
    /// </summary>
    public void SetData(int x, int y, int srcW, int srcH, int[] pixelData)
    {
        ArgumentNullException.ThrowIfNull(pixelData);
        if (x < 0 || y < 0 || srcW < 0 || srcH < 0 || x + srcW > Width || y + srcH > Height)
            throw new ArgumentOutOfRangeException(nameof(x), "写入区域超出纹理边界。");
        if (pixelData.Length < srcW * srcH)
            throw new ArgumentException("pixelData 长度不足，请至少提供 srcW × srcH 个像素。", nameof(pixelData));

        for (int row = 0; row < srcH; row++)
            Array.Copy(pixelData, row * srcW, _pixels, (y + row) * Width + x, srcW);
    }

    /// <summary>取当前像素副本。</summary>
    public int[] GetData()
    {
        var copy = new int[_pixels.Length];
        Array.Copy(_pixels, copy, _pixels.Length);
        return copy;
    }

    /// <summary>把当前像素缓冲重传到 GPU（纹理未创建时自动创建）。同步提交，可在帧内任意时刻调用。</summary>
    public void Commit()
    {
        JsUploadTexture(Id, Width, Height, _pixels);
        Ready = true;
    }

    /// <summary>把纹理绘制到屏幕矩形 (x, y, w, h)。未 Commit 前自动跳过。</summary>
    public void Draw(float x, float y, float w, float h)
        => Assets.Draw(this, x, y, w, h);

    /// <summary>释放 GPU 侧纹理资源。</summary>
    public void Dispose()
    {
        if (Ready)
        {
            JsDisposeTexture(Id);
            Ready = false;
        }
        GC.SuppressFinalize(this);
    }

    [JSImport("assets.uploadTexture", "main.js")]
    private static partial void JsUploadTexture(int id, int w, int h, int[] argb);

    [JSImport("assets.disposeTexture", "main.js")]
    private static partial void JsDisposeTexture(int id);
}
