using System.IO.Compression;
using System.Text.Json;

namespace WebLib;

/// <summary>
/// 一个已加载的 .web.lib 资源包（对齐 Unity <c>AssetBundle</c>）。
/// 通过 <see cref="LoadFromMemory"/> / <see cref="LoadFromStream"/> 加载，纯流式、无文件系统依赖，可在浏览器 WASM 运行。
/// 引擎拿到字节后自行交给自己的解码器（webp / 音频等）。
/// </summary>
/// <example>
/// <code>
/// using var ab = AssetBundle.LoadFromMemory(bytes);
/// byte[] webp = ab.LoadAsset("textures/floor/t1.webp");
/// </code>
/// </example>
public sealed class AssetBundle : IDisposable
{
    private readonly ZipArchive _zip;
    private readonly Dictionary<string, ZipArchiveEntry> _byPath;

    /// <summary>包内清单（资源索引 + 元信息）。</summary>
    public AssetBundleContent Content { get; }

    private AssetBundle(ZipArchive zip)
    {
        _zip = zip;

        var m = zip.GetEntry("manifest.json")
            ?? throw new InvalidDataException("缺少 manifest.json，不是合法的 .web.lib");

        using var ms = new MemoryStream();
        using (var es = m.Open()) es.CopyTo(ms);
        Content = JsonSerializer.Deserialize<AssetBundleContent>(ms.ToArray())
            ?? throw new InvalidDataException("manifest.json 解析失败");

        _byPath = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in zip.Entries)
            if (!string.Equals(e.FullName, "manifest.json", StringComparison.OrdinalIgnoreCase))
                _byPath[e.FullName] = e;
    }

    /// <summary>从字节数组加载（对应 Unity AssetBundle.LoadFromMemory）。</summary>
    public static AssetBundle LoadFromMemory(byte[] bytes) =>
        new(new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read, leaveOpen: false));

    /// <summary>从流加载（对应 Unity AssetBundle.LoadFromStream）。流由调用方管理。</summary>
    public static AssetBundle LoadFromStream(Stream stream) =>
        new(new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true));

    /// <summary>按包内相对路径取出资源字节；不存在抛 <see cref="KeyNotFoundException"/>（对应 Unity LoadAsset）。</summary>
    public byte[] LoadAsset(string name) =>
        TryGetAsset(name, out var b) ? b : throw new KeyNotFoundException($"资源不存在: {name}");

    /// <summary>按包内相对路径取出资源字节；不存在返回 false。</summary>
    public bool TryGetAsset(string name, out byte[] bytes)
    {
        bytes = Array.Empty<byte>();
        if (!_byPath.TryGetValue(name, out var e))
            return false;
        using var ms = new MemoryStream();
        using (var s = e.Open()) s.CopyTo(ms);
        bytes = ms.ToArray();
        return true;
    }

    /// <summary>异步取出资源字节（对应 Unity LoadAssetAsync）。</summary>
    public Task<byte[]?> LoadAssetAsync(string name) =>
        Task.FromResult(TryGetAsset(name, out var b) ? b : null);

    /// <summary>列出包内全部资源路径（对应 Unity GetAllAssetNames）。</summary>
    public string[] GetAllAssetNames() => _byPath.Keys.ToArray();

    /// <summary>是否包含某资源（对应 Unity Contains）。</summary>
    public bool Contains(string name) => _byPath.ContainsKey(name);

    /// <summary>取某资源的元信息（路径/类型/大小/crc/哈希）。</summary>
    public AssetBundleEntry? GetAssetInfo(string name) =>
        Content.Entries.FirstOrDefault(x => string.Equals(x.Path, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>卸载（对应 Unity Unload，本库即关闭 zip 流）。</summary>
    public void Unload(bool unloadAllLoadedObjects = true) => Dispose();

    public void Dispose() => _zip.Dispose();
}
