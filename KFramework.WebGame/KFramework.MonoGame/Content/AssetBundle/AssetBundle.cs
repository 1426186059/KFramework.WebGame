using System.IO.Compression;
using System.Text.Json;

namespace KFramework.MonoGame
{

    /// <summary>
    /// 一个已加载的 .web.lib 资源包（对齐 Unity <c>AssetBundle</c>）。
    /// 通过 <see cref="LoadFromMemory"/> / <see cref="LoadFromStream"/> 加载，纯流式、无文件系统依赖，可在浏览器 WASM 运行。
    /// 包内纹理在 <see cref="DecodeTexturesAsync"/>（LoadBundle 阶段）按 <see cref="AssetBundleEntry.Format"/> 解码/转码为字节并缓存：
    /// Rgba 原样、Png/Webp 解码为 RGBA8、Ktx2 借 Basis 转码器转码为设备原生压缩字节（目标格式由全局 WebGL2 上下文的扩展支持度决定，无需传入 <see cref="GraphicsDevice"/>）；
    /// GPU 上统一推迟到 <see cref="LoadTexture"/>（KTX2 走 CreateTexture 压缩格式重载，其余走 CreateTexture 的 RGBA8 路径）。
    /// 非纹理资源（音频 / JSON 等）原样取出。四种格式取用时逻辑一致，且未被引用的纹理不会提前占用显存。
    /// KTX2 转码依赖全局 WebGL2 上下文（游戏初始化、GL 上下文就绪后加载包即可），无需在加载阶段传入 <see cref="GraphicsDevice"/>。
    ///
    /// 一个资源包对应一个“Bundle”，里面装着若干资源（图集页、音效、JSON 等）。
    /// 所有资源都从已加载的 Bundle 中按名字取出。
    /// </summary>
    /// <example>
    /// <code>
    /// using var ab = AssetBundle.LoadFromMemory(bytes);
    /// await ab.DecodeTexturesAsync();                      // 加载阶段解码/转码纹理（KTX2 转码为字节，上传推迟到 LoadTexture）
    /// Texture2D tex = ab.LoadTexture("myres/atlas/characters_0", device); // 仅上传 GPU
    /// </code>
    /// </example>
    public sealed class AssetBundle : IDisposable
    {
        /// <summary>
        /// 解包阶段解码/转码后的纹理数据：CPU 侧字节 + 其对应的 GL 内部格式。
        /// 与 MonoGame 用 Format + 字节描述一张纹理一致，这里缓存的是上传 GPU 之前的数据。
        /// </summary>
        /// <param name="Data">Rgba/Png/Webp 为 RGBA8 像素（W*H*4）；Ktx2 为转码出的设备原生压缩字节。</param>
        /// <param name="GlFormat">像素/压缩数据的 GL 内部格式常量（RGBA8 或某压缩格式）。</param>
        private readonly record struct DecodedTexture(byte[] Data, SurfaceFormat GlFormat);
        private readonly ZipArchive _zip;
        private readonly Dictionary<string, ZipArchiveEntry> _byPath;
        // 解包阶段（LoadBundleAsync）解码/转码后的纹理数据缓存：path -> 字节 + 对应 GL 内部格式。
        // Rgba/Png/Webp 存 RGBA8 像素、GlFormat=SurfaceFormat.Color；Ktx2 存转码出的设备原生压缩字节、GlFormat=对应压缩格式。
        // 与“上传 GPU”解耦——上传统一推迟到 LoadTexture，未被实际引用的纹理不会占用显存。
        // 思路与 MonoGame 用 Format + 字节描述一张纹理一致，只是这里缓存的是上传前的 CPU 数据。
        private readonly Dictionary<string, DecodedTexture> _decodedTextures = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>包内清单（资源索引 + 元信息）。</summary>
        public AssetBundleContent Content { get; }

        private AssetBundle(ZipArchive zip)
        {
            _zip = zip;

            var m = zip.GetEntry("manifest.json")
                    ?? throw new InvalidDataException("缺少 manifest.json，不是合法的 .web.lib");

            using var ms = new MemoryStream();
            using (var es = m.Open()) es.CopyTo(ms);
            Content = JsonSerializer.Deserialize(ms.ToArray(), AppJsonContext.Default.AssetBundleContent)
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

        // 严格：按包内相对路径精确匹配；宽松：Path 包含关键字（不区分大小写）的第一个匹配。
        private AssetBundleEntry? FindEntry(string name, bool strict)
        {
            if (strict)
                return Content.Entries.FirstOrDefault(x => string.Equals(x.Path, name, StringComparison.OrdinalIgnoreCase));
            return Content.Entries.FirstOrDefault(x => x.Path.Contains(name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>按包内相对路径取出资源字节；不存在抛 <see cref="KeyNotFoundException"/>（对应 Unity LoadAsset）。
        /// <paramref name="strict"/> 为 true 时按精确路径匹配；为 false 时按关键字（Path 包含）匹配首个资源。</summary>
        public byte[] LoadAsset(string name, bool strict = true)
            => TryGetAsset(name, out var b, strict) ? b : throw new KeyNotFoundException($"资源不存在: {name}");

        /// <summary>按包内相对路径取出资源字节；不存在返回 false。
        /// <paramref name="strict"/> 为 true 时按精确路径匹配；为 false 时按关键字（Path 包含）匹配首个资源。</summary>
        public bool TryGetAsset(string name, out byte[] bytes, bool strict = true)
        {
            bytes = Array.Empty<byte>();
            string path = strict ? name : (FindEntry(name, false)?.Path ?? string.Empty);
            if (string.IsNullOrEmpty(path) || !_byPath.TryGetValue(path, out var e))
                return false;
            using var ms = new MemoryStream();
            using (var s = e.Open()) s.CopyTo(ms);
            bytes = ms.ToArray();
            return true;
        }

        /// <summary>异步取出资源字节（对应 Unity LoadAssetAsync）。</summary>
        public Task<byte[]?> LoadAssetAsync(string name, bool strict = true)
            => Task.FromResult(TryGetAsset(name, out var b, strict) ? b : null);

        /// <summary>列出包内全部资源路径（对应 Unity GetAllAssetNames）。</summary>
        public string[] GetAllAssetNames() => _byPath.Keys.ToArray();

        /// <summary>是否包含某资源（对应 Unity Contains，仅精确路径）。</summary>
        public bool Contains(string name) => _byPath.ContainsKey(name);

        /// <summary>取某资源的元信息（路径 / 类型 / 大小 / crc / 哈希 / 像素尺寸）。
        /// <paramref name="strict"/> 为 true 时按精确路径匹配；为 false 时按关键字（Path 包含）匹配首个资源。</summary>
        public AssetBundleEntry? GetAssetInfo(string name, bool strict = true)
            => FindEntry(name, strict);

        // ============ 同步资源取出（包已驻留内存后即时，对齐 Unity AssetBundle.LoadAsset 同步语义） ============
        // 真正的异步只发生在 ContentManager.LoadBundleAsync（拉包）；取资源本身不依赖网络，因此为同步。
        // 纹理需要上传 GPU，故由调用方传入 GraphicsDevice。

        /// <summary>同步读取文本原文（UTF-8，去 BOM）。
        /// <paramref name="strict"/> 为 true 时按精确路径匹配；为 false 时按关键字（Path 包含）匹配首个资源。</summary>
        public string LoadText(string name, bool strict = true) => ContentFunc.DecodeUtf8(LoadAsset(name, strict));

        /// <summary>同步取一张整图纹理（图集请改用 <see cref="KFramework.MonoGameExtend.SpriteSheetLoader"/> 加载）。
        /// <paramref name="strict"/> 为 true 时按精确路径匹配；为 false 时按关键字（Path 包含）匹配首个纹理。</summary>
        /// <remarks>解码已在 <see cref="DecodeTexturesAsync"/>（LoadBundle 异步阶段）完成并缓存；此处仅做 GPU 上传。</remarks>
        public Texture2D LoadTexture(string name, GraphicsDevice device, bool strict = true)
        {
            AssetBundleEntry? info = GetAssetInfo(name, strict);
            if (info == null)
            {
                throw new KeyNotFoundException($"资源不存在: {name}");
            }

            if (!_decodedTextures.TryGetValue(info.Path, out var decodedKtx2))
            {
                throw new InvalidOperationException(
                    $"资源「{name}」需在 LoadBundleAsync 阶段先经 DecodeTexturesAsync 预转码");
            }

            int w = info.Width, h = info.Height;
            if (w <= 0 || h <= 0)
            {
                throw new InvalidOperationException($"纹理 “{name}” 缺少像素尺寸，无法上传 GPU。");
            }
            return device.CreateTexture(w, h, decodedKtx2.Data, decodedKtx2.GlFormat);
        }
        
        public async Task DecodeTexturesAsync()
        {
            foreach (var e in Content.Entries)
            {
                if (e.Type != ContentAssetType.Texture)
                {
                    continue;
                }

                if (e.Format == ContentTextureDataFormat.Rgba)
                {
                    byte[] raw = LoadAsset(e.Path);
                    _decodedTextures[e.Path] = new DecodedTexture(raw, SurfaceFormat.Color);
                }
                else if (e.Format == ContentTextureDataFormat.Ktx2)
                {
                    int w = e.Width, h = e.Height;
                    if (w <= 0 || h <= 0)
                        throw new InvalidOperationException($"纹理 “{e.Path}” 缺少像素尺寸，无法转码 KTX2。");
                    byte[] raw = LoadAsset(e.Path);
                    (int basisFormat, SurfaceFormat glFormat) = Ktx2TranscodeSelector.Pick();
                    int size = Ktx2TranscodeSelector.GetTranscodedSize(glFormat, w, h);
                    byte[] compressed = new byte[size];
                    await JSBind_Texture.TranscodeKtx2Into(raw, basisFormat, new ArraySegment<byte>(compressed)).ConfigureAwait(false);
                    _decodedTextures[e.Path] = new DecodedTexture(compressed, glFormat);
                }
                else
                {
                    int w = e.Width, h = e.Height;
                    if (w <= 0 || h <= 0)
                        throw new InvalidOperationException($"纹理 “{e.Path}” 缺少像素尺寸，无法解码 {e.Format}。");
                    byte[] raw = LoadAsset(e.Path);
                    var pixels = new byte[w * h * 4];
                    var size = new int[2];
                    if (!await JSBind_Texture.DecodeImageToRgbaAsync2(new ArraySegment<byte>(raw), new ArraySegment<int>(size), new ArraySegment<byte>(pixels)).ConfigureAwait(false))
                        throw new InvalidOperationException($"纹理 “{e.Path}” 解码失败。");
                    _decodedTextures[e.Path] = new DecodedTexture(pixels, SurfaceFormat.Color);
                }
            }
        }

        /// <summary>同步尝试取一张纹理；找不到返回 false。
        /// <paramref name="strict"/> 为 true 时按精确路径匹配；为 false 时按关键字（Path 包含）匹配首个纹理。</summary>
        public bool TryLoadTexture(string name, GraphicsDevice device, out Texture2D? tex, bool strict = true)
        {
            try { tex = LoadTexture(name, device, strict); return true; }
            catch (KeyNotFoundException) { tex = null; return false; }
        }

        /// <summary>包内全部资源名（含图集子图与数据），均为<b>相对 raw 目录</b>的路径，并保留原始扩展名（如 myres/atlas/characters.png、myres/data/game.json）。</summary>
        public IReadOnlyList<string> AssetNames => Content.Entries.Select(e => e.Path).ToArray();

        /// <summary>卸载（对应 Unity Unload，本库即关闭 zip 流）。</summary>
        public void Unload(bool unloadAllLoadedObjects = true) => Dispose();

        public void Dispose()
        {
            // 缓存的是字节（CPU）与 GL 内部格式（int），无 GPU 资源需要显式回收；关闭 zip 流即可。
            _zip.Dispose();
            this._decodedTextures.Clear();
        }
    }

}
