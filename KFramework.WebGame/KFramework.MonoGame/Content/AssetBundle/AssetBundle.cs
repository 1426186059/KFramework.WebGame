using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices.JavaScript;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace KFramework.MonoGame;

/// <summary>
/// 一个已加载的 .web.lib 资源包（对齐 Unity <c>AssetBundle</c>）。
/// 通过 <see cref="LoadFromMemory"/> / <see cref="LoadFromStream"/> 加载，纯流式、无文件系统依赖，可在浏览器 WASM 运行。
/// 包内纹理在 <see cref="DecodeTexturesAsync"/>（LoadBundle 阶段）按 <see cref="AssetBundleEntry.Format"/> 解码为 RGBA8 后上传 GPU；
/// 非纹理资源（音频 / JSON 等）原样取出。
///
/// 一个资源包对应一个“Bundle”，里面装着若干资源（图集页、音效、JSON 等）。
/// 所有资源都从已加载的 Bundle 中按名字取出。
/// </summary>
/// <example>
/// <code>
/// using var ab = AssetBundle.LoadFromMemory(bytes);
/// await ab.DecodeTexturesAsync();                              // 加载阶段解码纹理（Png 等）
/// Texture2D tex = ab.LoadTexture("myres/atlas/characters_0", device); // 仅上传 GPU
/// </code>
/// </example>
public sealed class AssetBundle : IDisposable
{
    private readonly ZipArchive _zip;
    private readonly Dictionary<string, ZipArchiveEntry> _byPath;
    // 加载阶段（LoadBundleAsync）预解码后的 RGBA8 像素缓存：path -> RGBA8（长度 = W*H*4）。
    private readonly Dictionary<string, byte[]> _decodedTextures = new(StringComparer.OrdinalIgnoreCase);

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
    /// <remarks>解码已在 <see cref="DecodeTexturesAsync"/>（LoadBundle 异步阶段）完成并缓存；此处仅做 GPU 上传。</remarks>
    public Texture2D LoadTexture(string name, GraphicsDevice device)
    {
        AssetBundleEntry? info = GetAssetInfo(name)
            ?? throw new KeyNotFoundException($"资源不存在: {name}");
        if (info.Page >= 0)
            throw new InvalidOperationException(
                $"资源「{name}」是旧格式的子图条目，请改用 SpriteSheetLoader 加载图集（整页纹理 + source rect）。");
        if (info.Format == AssetTextureFormat.Ktx2)
            throw new InvalidOperationException(
                $"资源「{name}」为 KTX2（GPU 压缩纹理）：转码需 GPU 上下文，请改用 LoadTextureAsync 异步加载。");

        // 优先用加载阶段预解码的缓存；未命中（如直接 LoadFromMemory 而未调 DecodeTexturesAsync）则按格式兜底解码。
        if (!_decodedTextures.TryGetValue(name, out var pixels))
            pixels = DecodeEntryPixels(name, info);

        int width = info.Width;
        int height = info.Height;
        if (width <= 0 || height <= 0)
            throw new InvalidOperationException($"纹理 “{name}” 缺少像素尺寸，无法上传 GPU。");
        return device.CreateTexture(width, height, pixels);
    }

    /// <summary>
    /// 异步加载一张整图纹理。对 RGBA8 / Png（已在 <see cref="DecodeTexturesAsync"/> 预解码）/ Webp 复用同步上传路径；
    /// 对 KTX2（GPU 压缩纹理）则借浏览器 Basis 转码器把 KTX2 转码为当前设备原生压缩格式并直接上传 GPU。
    /// </summary>
    public async Task<Texture2D> LoadTextureAsync(string name, GraphicsDevice device)
    {
        AssetBundleEntry? info = GetAssetInfo(name)
            ?? throw new KeyNotFoundException($"资源不存在: {name}");
        if (info.Page >= 0)
            throw new InvalidOperationException(
                $"资源「{name}」是旧格式的子图条目，请改用 SpriteSheetLoader 加载图集（整页纹理 + source rect）。");

        // KTX2 是 GPU 压缩纹理：只有它才需要借浏览器 Basis 转码器转码后直接上传 GPU
        // （唯一需要异步、依赖 GPU 上下文的格式）。
        if (info.Format == AssetTextureFormat.Ktx2)
        {
            byte[] raw = LoadAsset(name);
            (int basisFormat, int glFormat) = Ktx2TranscodeSelector.Pick();
            JSObject handle = await JSBind_Texture.UploadKtx2(raw, basisFormat, glFormat).ConfigureAwait(false);
            int width = info.Width, height = info.Height;
            if (width <= 0 || height <= 0)
                throw new InvalidOperationException($"纹理 “{name}” 缺少像素尺寸，无法上传 GPU。");
            return new Texture2D(device, handle, width, height, ownsHandle: true);
        }

        // 其余格式（Rgba / Png / Webp 等）：像素已在 DecodeTexturesAsync 预解码，走同步上传路径。
        return LoadTexture(name, device);
    }

    // ============ 加载阶段异步解码（对齐 PixiJS：bundle 拉取/解包/解码异步，取资源同步） ============
    // 把需要解码的纹理（如 Png）在 LoadBundleAsync 阶段提前解码为 RGBA8 并缓存，
    // 使 LoadTexture 只负责 GPU 上传、不再做图像解码。解码本身为 CPU 同步（WASM 单线程），
    // 但被安排在异步加载阶段完成，逐资源取用时保持同步、零解码。Rgba 格式本身已是像素，无需预解码。

    /// <summary>
    /// 在包已驻留内存后、取资源之前，把需要解码的纹理（Png / Webp）提前解码为 RGBA8 并缓存。
    /// 应在 <see cref="ContentManager.LoadBundleAsync"/>（异步阶段）调用一次。
    /// </summary>
    /// <remarks>
    /// Rgba 本身已是像素，直接上传无需预解码；Png 走托管 PngDecoder 同步解码；
    /// Webp 无托管解码器，借浏览器原生 <c>createImageBitmap</c> 异步解码（WASM/浏览器目标）。
    /// </remarks>
    public async Task DecodeTexturesAsync()
    {
        foreach (var e in Content.Entries)
        {
            if (!string.Equals(e.Type, "texture", StringComparison.OrdinalIgnoreCase)) continue;
            if (e.Format == AssetTextureFormat.Rgba) continue;
            // KTX2 是 GPU 压缩纹理：转码需 GPU 上下文与 Basis 转码器，不能在包加载阶段做，
            // 留到 LoadTextureAsync 上传时按需转码+上传。此处跳过预解码。
            if (e.Format == AssetTextureFormat.Ktx2) continue;

            if (e.Format == AssetTextureFormat.Webp)
            {
                // Webp 无托管解码器：借浏览器原生解码（需清单中的宽高来预分配像素缓冲）。
                int w = e.Width, h = e.Height;
                if (w <= 0 || h <= 0)
                    throw new InvalidOperationException($"纹理 “{e.Path}” 缺少像素尺寸，无法解码 WebP。");
                byte[] raw = LoadAsset(e.Path);
                var pixels = new byte[w * h * 4];
                var size = new int[2];
                await JSBind_Texture.DecodeImageToRgba(raw, size, pixels).ConfigureAwait(false);
                _decodedTextures[e.Path] = pixels;
                continue;
            }

            _decodedTextures[e.Path] = DecodeEntryPixels(e.Path, e);
        }
    }

    /// <summary>按条目声明的格式把包内纹理字节解码为 RGBA8（供预解码缓存与 LoadTexture 兜底共用）。</summary>
    private byte[] DecodeEntryPixels(string name, AssetBundleEntry e)
    {
        byte[] raw = LoadAsset(name);
        return e.Format switch
        {
            AssetTextureFormat.Rgba => raw,
            AssetTextureFormat.Png  => PngDecoder.Decode(raw).Pixels,
            AssetTextureFormat.Webp => throw new InvalidOperationException(
                $"纹理 “{name}” 为 WebP：必须在 LoadBundleAsync 阶段（DecodeTexturesAsync）经浏览器原生解码，请先调用 LoadBundleAsync，不要走同步兜底。"),
            AssetTextureFormat.Ktx2 => throw new NotSupportedException(
                $"纹理 “{name}” 为 KTX2（GPU 压缩纹理）：请使用 LoadTextureAsync 经浏览器 Basis 转码器上传，不要走同步兜底。"),
            _ => raw,
        };
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
