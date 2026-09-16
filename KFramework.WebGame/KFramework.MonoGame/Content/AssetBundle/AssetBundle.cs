using System.Drawing;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KFramework.MonoGame;

/// <summary>
/// 一个已加载的 .web.lib 资源包（对齐 Unity <c>AssetBundle</c>）。
/// 通过 <see cref="LoadFromMemory"/> / <see cref="LoadFromStream"/> 加载，纯流式、无文件系统依赖，可在浏览器 WASM 运行。
/// 引擎拿到字节后自行交给自己的解码器（webp / 音频等）。
///
/// 一个资源包对应一个“Bundle”，里面装着若干资源（图集页、音效、JSON 等）。
/// 所有资源都从已加载的 Bundle 中按名字取出。
/// </summary>
/// <example>
/// <code>
/// using var ab = AssetBundle.LoadFromMemory(bytes);
/// byte[] webp = ab.LoadAsset("myres/atlas/characters_0");
/// </code>
/// </example>
public sealed class AssetBundle : IDisposable
{
    private readonly ZipArchive _zip;
    private readonly Dictionary<string, ZipArchiveEntry> _byPath;

    /// <summary>包内清单（资源索引 + 元信息）。</summary>
    public AssetBundleContent Content { get; }

    private static readonly JsonSerializerOptions s_opts = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private AssetBundle(ZipArchive zip)
    {
        _zip = zip;

        var m = zip.GetEntry("manifest.json")
                ?? throw new InvalidDataException("缺少 manifest.json，不是合法的 .web.lib");

        using var ms = new MemoryStream();
        using (var es = m.Open()) es.CopyTo(ms);
        Content = JsonSerializer.Deserialize<AssetBundleContent>(ms.ToArray(), s_opts)
                  ?? throw new InvalidDataException("manifest.json 解析失败");

        _byPath = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in zip.Entries)
            if (!string.Equals(e.FullName, "manifest.json", StringComparison.OrdinalIgnoreCase))
                _byPath[e.FullName] = e;
    }

    /// <summary>从字节数组加载（对应 Unity AssetBundle.LoadFromMemory）。</summary>
    public static AssetBundle LoadFromMemory(byte[] bytes)
        => new(new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read, leaveOpen: false));

    /// <summary>从流加载（对应 Unity AssetBundle.LoadFromStream）。流由调用方管理。</summary>
    public static AssetBundle LoadFromStream(Stream stream)
        => new(new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true));

    /// <summary>按包内相对路径取出资源字节；不存在抛 <see cref="KeyNotFoundException"/>（对应 Unity LoadAsset）。</summary>
    public byte[] LoadAsset(string name)
        => TryGetAsset(name, out var b) ? b : throw new KeyNotFoundException($"资源不存在: {name}");

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
    public Task<byte[]?> LoadAssetAsync(string name)
        => Task.FromResult(TryGetAsset(name, out var b) ? b : null);

    /// <summary>列出包内全部资源路径（对应 Unity GetAllAssetNames）。</summary>
    public string[] GetAllAssetNames() => _byPath.Keys.ToArray();

    /// <summary>是否包含某资源（对应 Unity Contains）。</summary>
    public bool Contains(string name) => _byPath.ContainsKey(name);

    /// <summary>取某资源的元信息（路径 / 类型 / 大小 / crc / 哈希 / 像素尺寸）。</summary>
    public AssetBundleEntry? GetAssetInfo(string name)
        => Content.Entries.FirstOrDefault(x => string.Equals(x.Path, name, StringComparison.OrdinalIgnoreCase));

    // ============ 同步资源取出（包已驻留内存后即时，对齐 Unity AssetBundle.LoadAsset 同步语义） ============
    // 真正的异步只发生在 ContentManager.LoadBundleAsync（拉包）；取资源本身不依赖网络，因此为同步。
    // 纹理需要上传 GPU，故由调用方传入 GraphicsDevice。

    /// <summary>同步读取文本原文（UTF-8，去 BOM）。</summary>
    public string LoadText(string name) => InnerCommonFunc.DecodeUtf8(LoadAsset(name));

    /// <summary>同步读取并反序列化 JSON（包已加载后即时）。</summary>
    public T? LoadJson<T>(string name)
        => JsonSerializer.Deserialize<T>(LoadText(name), s_opts);

    /// <summary>同步取一张整图纹理（图集请改用 <see cref="KFramework.MonoGameExtend.SpriteSheetLoader"/> 加载）。</summary>
    public Texture2D LoadTexture(string name, GraphicsDevice device)
    {
        AssetBundleEntry? info = GetAssetInfo(name)
            ?? throw new KeyNotFoundException($"资源不存在: {name}");
        if (info.Page >= 0)
            throw new InvalidOperationException(
                $"资源「{name}」是旧格式的子图条目，请改用 SpriteSheetLoader 加载图集（整页纹理 + source rect）。");
        byte[] pixels = LoadAsset(name);
        int width = info.Width;
        int height = info.Height;

        // 图集页以 PNG 形式入库，运行时需解码为 RGBA8 再上传 GPU；
        // 个别散图也可能直接存 RGBA8，故按 PNG 魔数判断，不解码则原样上传。
        // 注：若下游内容构建把图集页转成了 WebP，其解析由 JS 宿主层完成后以 RGBA8 交给运行端；运行端零依赖、不参与 WebP 解码。
        if (pixels.Length >= 8 && pixels[0] == 0x89 && pixels[1] == 0x50 && pixels[2] == 0x4E && pixels[3] == 0x47)
        {
            Bitmap bmp = PngDecoder.Decode(pixels);
            pixels = bmp.Pixels;
            if (width <= 0 || height <= 0) { width = bmp.Width; height = bmp.Height; }
        }

        if (width <= 0 || height <= 0)
            throw new InvalidOperationException($"纹理 “{name}” 缺少像素尺寸，无法上传 GPU。");
        return device.CreateTexture(width, height, pixels);
    }

    /// <summary>同步尝试取一张纹理；找不到返回 false。</summary>
    public bool TryLoadTexture(string name, GraphicsDevice device, out Texture2D? tex)
    {
        try { tex = LoadTexture(name, device); return true; }
        catch (KeyNotFoundException) { tex = null; return false; }
    }

    /// <summary>包内全部资源名（含图集子图与数据）。</summary>
    public IReadOnlyList<string> AssetNames => Content.Entries.Select(e => e.Path).ToArray();

    /// <summary>卸载（对应 Unity Unload，本库即关闭 zip 流）。</summary>
    public void Unload(bool unloadAllLoadedObjects = true) => Dispose();

    public void Dispose() => _zip.Dispose();
}
