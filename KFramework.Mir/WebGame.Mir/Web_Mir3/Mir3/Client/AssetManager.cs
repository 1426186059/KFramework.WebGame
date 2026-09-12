using MirClient.Rendering;

namespace MirClient.Assets;

/// <summary>
/// 多库资源管理器：按 (库编号, 图片索引) 懒解码并上传为 Canvas 纹理。
/// 同一张图只解码一次，之后复用纹理句柄。
/// </summary>
public sealed class AssetManager
{
    /// <summary>M-Hum 这类非地图库用的虚拟编号（不与 KrOrder 的 0..71 冲突）。</summary>
    public const int HumanLibrary = 200;

    private readonly Dictionary<int, ZlLibrary> _libs = new();
    private readonly Dictionary<int, byte[]> _raw = new();
    private readonly Dictionary<int, int> _texKeys = new();

    private int _nextKey = 1;

    public int TextureCount { get; private set; }

    /// <summary>解码失败（被拒绝）的次数，用于诊断。</summary>
    public int FailedCount { get; private set; }

    public void AddLibrary(int fileId, byte[] data)
    {
        ZlLibrary lib = ZlLibrary.ReadIndex(data);
        _libs[fileId] = lib;
        _raw[fileId] = data;
    }

    public bool HasLibrary(int fileId) => _libs.ContainsKey(fileId);

    public int LibraryImageCount(int fileId)
        => _libs.TryGetValue(fileId, out ZlLibrary? lib) ? lib.ValidCount : 0;

    /// <summary>从索引读取图片尺寸，不需要解码。</summary>
    public bool TryGetSize(int fileId, int index, out short width, out short height)
    {
        width = 0;
        height = 0;

        if (index < 0 || !_libs.TryGetValue(fileId, out ZlLibrary? lib)) return false;
        if (index >= lib.Images.Length) return false;

        ZlImage? img = lib.Images[index];
        if (img == null || img.Width <= 0 || img.Height <= 0) return false;

        width = img.Width;
        height = img.Height;
        return true;
    }

    /// <summary>读取精灵锚点偏移（用于角色/怪物精确定位），不需要解码像素。</summary>
    public bool TryGetOffset(int fileId, int index, out short offX, out short offY)
    {
        offX = 0;
        offY = 0;

        if (index < 0 || !_libs.TryGetValue(fileId, out ZlLibrary? lib)) return false;
        if (index >= lib.Images.Length) return false;

        ZlImage? img = lib.Images[index];
        if (img == null) return false;

        offX = img.OffSetX;
        offY = img.OffSetY;
        return true;
    }

    /// <summary>取得纹理句柄；返回 0 表示该图不存在或无法解码。</summary>
    public int GetTexture(int fileId, int index)
    {
        if (index < 0) return 0;

        int code = (fileId << 20) | (index & 0x000F_FFFF);
        if (_texKeys.TryGetValue(code, out int cached))
            return cached;

        int key = DecodeAndUpload(fileId, index);
        if (key == 0) FailedCount++;

        _texKeys[code] = key;
        return key;
    }

    private int DecodeAndUpload(int fileId, int index)
    {
        if (!_libs.TryGetValue(fileId, out ZlLibrary? lib)) return 0;
        if (index >= lib.Images.Length) return 0;

        ZlImage? img = lib.Images[index];
        if (img == null || img.Position <= 0) return 0;

        byte[] raw = _raw[fileId];
        if (img.Position + img.PayloadSize > raw.Length) return 0;

        byte[]? rgba = lib.DecodeImage(img, raw.AsSpan(img.Position, img.PayloadSize));
        if (rgba == null || img.Width <= 0 || img.Height <= 0) return 0;

        int key = _nextKey++;
        MirCanvas.CreateImage(key, rgba, img.Width, img.Height);
        TextureCount++;
        return key;
    }

    /// <summary>释放底层文件缓冲（纹理已上传给 Canvas 后即可回收）。</summary>
    public void ReleaseRaw(int fileId)
    {
        _raw.Remove(fileId);
    }
}
