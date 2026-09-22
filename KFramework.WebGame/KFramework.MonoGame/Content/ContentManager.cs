using System.Runtime.InteropServices.JavaScript;

namespace KFramework.MonoGame
{
    public sealed class ContentManager : IDisposable
    {
        private readonly HttpClient _http;
        private readonly AssetBundleManager _manager;
        private readonly string _rootDir;

        public ContentManager(
            string root = "hot_update_res",
            string BaseURL = null)
        {
            ArgumentNullException.ThrowIfNull(root);
            _rootDir = root.TrimEnd('/');

            string baseUri = BaseURL;
            if (string.IsNullOrWhiteSpace(BaseURL))
            {
                baseUri = JSBind_Platform.GetBaseUri();
            }

            Uri? baseAddress = null;
            if (!string.IsNullOrEmpty(baseUri))
            {
                int cut = baseUri.LastIndexOf('/');
                if (cut > 0 && !baseUri.EndsWith('/'))
                {
                    baseUri = baseUri.Substring(0, cut + 1);
                }
                if (!Uri.TryCreate(baseUri, UriKind.Absolute, out baseAddress))
                {
                    baseAddress = null;
                }
            }

            _http = baseAddress is null ? new HttpClient() : new HttpClient { BaseAddress = baseAddress };
            _http.Timeout = TimeSpan.FromMinutes(5);
            _manager = new AssetBundleManager(_http, _rootDir);
        }

        /// <summary>已加载的 Bundle 逻辑名。</summary>
        public IReadOnlyList<string> LoadedBundles => _manager.LoadedBundles;

        public Task<bool> HasBundleCacheAsync(string bundleFile, CancellationToken cancellationToken = default)
            => _manager.IsBundleCachedAsync(bundleFile, cancellationToken);
        
        public AssetBundle? GetBundle(string bundleName, bool strict = true)
            => _manager.GetBundle(bundleName, strict);

        public bool TryGetBundle(string bundleName, out AssetBundle? bundle, bool strict = true)
            => _manager.TryGetBundle(bundleName, out bundle, strict);

        
        public async Task LoadManifestAsync(CancellationToken cancellationToken = default)
        { 
            await _manager.FetchManifestAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task LoadAsync(IProgress<float>? progress = null, CancellationToken cancellationToken = default, GraphicsDevice? device = null)
        { 
             await _manager.LoadAllAsync(progress, cancellationToken, device).ConfigureAwait(false);
        }

        public async Task<AssetBundle> LoadBundleAsync(
            string bundleName,
            CancellationToken cancellationToken = default,
            IProgress<float>? progress = null,
            bool strict = true,
            GraphicsDevice? device = null)
        { 
            return await _manager.LoadBundleAsync(bundleName, cancellationToken, progress, strict, device).ConfigureAwait(false);
        }

        /// <summary>异步并发加载多个 AssetBundle（单个失败不影响其余，逐包上报进度 0~1）。</summary>
        public async Task<IReadOnlyList<AssetBundle>> LoadBundlesAsync(
            IEnumerable<string> bundleNames,
            CancellationToken cancellationToken = default,
            IProgress<float>? progress = null,
            GraphicsDevice? device = null)
        {
            return await _manager.LoadBundlesAsync(bundleNames, cancellationToken, progress, device).ConfigureAwait(false);
        }

        /// <summary>卸载一个已加载的 Bundle（释放其 zip 流；正在使用的纹理/字节请自行管理）。</summary>
        public void UnloadBundle(string bundleName) => _manager.UnloadBundle(bundleName);

        /// <summary>
        /// 缓存 GC：清理 Cache Storage 里不在当前清单中的历史版本资源包（返回删除条数；失败返回 -1）。
        /// 包文件名带内容哈希，热更一次就留一份旧文件，建议每次热更后调用一次。
        /// </summary>
        /// <param name="extraKeep">额外保留的相对路径，用于保护资源根目录下非资源包的缓存条目。</param>
        public Task<int> PruneCacheAsync(IEnumerable<string>? extraKeep = null, CancellationToken cancellationToken = default)
            => _manager.PruneCacheAsync(null, extraKeep, cancellationToken);


        private string ResolveRooted(string path)
        { 
            return Uri.TryCreate(path, UriKind.Absolute, out _) ? path : $"{_rootDir}/{path.TrimStart('/')}";
        }

        public async Task<string> LoadTextAsync(string relativePath, bool bUseCache = false, CancellationToken cancellationToken = default)
        {
            byte[] data = await ContentFunc.LoadCacheOrDownloadAsync(_http, ResolveRooted(relativePath), bUseCache, cancellationToken).ConfigureAwait(false);
            return ContentFunc.DecodeUtf8(data);
        }

        /// <summary>按资源根(root)异步加载任意字节流（不走内容包）。先查本地 Cache Storage，未命中则远程下载并写回，下次直接命中本地缓存。</summary>
        public async Task<byte[]> LoadBytesAsync(string relativePath, bool bUseCache = false, CancellationToken cancellationToken = default)
        { 
            return await ContentFunc.LoadCacheOrDownloadAsync(_http, ResolveRooted(relativePath), bUseCache, cancellationToken).ConfigureAwait(false);
        }

        public async Task<Texture2D> LoadTexture2DAsync(string relativePath, GraphicsDevice device, bool bUseCache = false, CancellationToken cancellationToken = default)
        {
            byte[] data = await ContentFunc.LoadCacheOrDownloadAsync(_http, ResolveRooted(relativePath), bUseCache, cancellationToken).ConfigureAwait(false);
            return await LoadTexture2DAsync(data, device).ConfigureAwait(false);
        }

        public async Task<Texture2D> LoadTexture2DAsync(byte[] data, GraphicsDevice device)
        {
            int w, h;
            using (JSObject sizeObj = await JSBind_Texture.GetImageSize(data).ConfigureAwait(false))
            {
                w = sizeObj.GetPropertyAsInt32("width");
                h = sizeObj.GetPropertyAsInt32("height");
            }
            if (w <= 0 || h <= 0) throw new InvalidOperationException("图片解码失败：尺寸无效。");
            // 已知尺寸，预分配像素缓冲后一次性解码（源生成互操作不支持直接回传 byte[]，故走 out 缓冲）。
            int[] size = new int[2];
            byte[] pixels = new byte[w * h * 4];
            await JSBind_Texture.DecodeImageToRgba(data, new ArraySegment<int>(size), new ArraySegment<byte>(pixels)).ConfigureAwait(false);
            return device.CreateTexture(w, h, pixels);
        }

        public void Dispose()
        {
            _manager.Dispose();
            _http.Dispose();
        }
    }
}
