using System.Collections.Generic;
using Library;
using MirClient.Assets;

namespace MirEngine;

/// <summary>
/// WASM 库管理器（LibraryCore 移植的公开门面）。
/// 按原版 <see cref="LibraryFile"/> 枚举加载 .Zl 资源库，复用 M1 已验证的
/// <see cref="AssetManager"/> 解码/上传管线（DXT1/DXT5 → RGBA → Canvas 纹理）。
/// Client 代码统一通过它取图，不再直接使用整数 fileId。
/// </summary>
public static class LibraryManager
{
    /// <summary>底层资源管理器（与 M1 共用同一实例，避免重复解码）。</summary>
    public static AssetManager Assets { get; } = new();

    /// <summary>
    /// LibraryFile → AssetManager 整数键。
    /// 地图库沿用原版 Libraries.KROrder 编号（与 M1 的地图渲染共享缓存），
    /// M_Hum 沿用 M1 的 200，其余分配稳定合成键（从 1000 起，不与 KROrder/200 冲突）。
    /// </summary>
    private static readonly Dictionary<LibraryFile, int> Ids = new();
    private static int _next = 1000;

    static LibraryManager()
    {
        foreach (KeyValuePair<int, LibraryFile> kv in Libraries.KROrder)
            Ids[kv.Value] = kv.Key;

        Ids[LibraryFile.M_Hum] = 200;
    }

    private static int ResolveId(LibraryFile file)
    {
        if (Ids.TryGetValue(file, out int id))
            return id;

        int synthetic = _next++;
        Ids[file] = synthetic;
        return synthetic;
    }

    /// <summary>加载（或覆盖）一个库。data 为整个 .Zl 文件字节。</summary>
    public static void Load(LibraryFile file, byte[] data) => Assets.AddLibrary(ResolveId(file), data);

    public static bool Has(LibraryFile file) => Assets.HasLibrary(ResolveId(file));

    public static int ImageCount(LibraryFile file) => Assets.LibraryImageCount(ResolveId(file));

    /// <summary>取得图片的 Canvas 纹理句柄；0 表示该图不存在或无法解码。</summary>
    public static int GetTexture(LibraryFile file, int index) => Assets.GetTexture(ResolveId(file), index);

    /// <summary>从索引读尺寸，无需解码像素。</summary>
    public static bool TryGetSize(LibraryFile file, int index, out short width, out short height)
        => Assets.TryGetSize(ResolveId(file), index, out width, out height);

    /// <summary>读精灵锚点偏移（角色/怪物精确定位用）。</summary>
    public static bool TryGetOffset(LibraryFile file, int index, out short offX, out short offY)
        => Assets.TryGetOffset(ResolveId(file), index, out offX, out offY);

    /// <summary>该库相对 wwwroot 的 URL（用于 JS 预下载）。</summary>
    public static string GetUrl(LibraryFile file) => Libraries.GetUrl(file);
}
