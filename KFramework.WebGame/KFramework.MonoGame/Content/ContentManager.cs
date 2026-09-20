using KFramework.MonoGame.Content;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices.JavaScript;

namespace KFramework.MonoGame
{
    // 负责加载/卸载 AssetBundle（统一委托 AssetBundleManager）
    // 也提供 加载 Text/字节数组/Texture2D 的方法（不经AssetBundle，直接按页面基址下载）
    public sealed class ContentManager : IDisposable
    {
        private readonly HttpClient _http;
        private readonly AssetBundleManager _manager;

        public ContentManager(
            string root = "hot_update_res",
            string BaseURL = null,
            AssetBundleManager.BundleCacheMode cacheMode = AssetBundleManager.BundleCacheMode.Http)
        {
            ArgumentNullException.ThrowIfNull(root);
            string rootUrl = root.TrimEnd('/');

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
            // 地图片库可能非常大（如 Tiles.Lib 有数百 MB），必须放宽超时，否则默认的 100 秒会把大文件
            // 下载打断，导致 InitializeAsync 捕获超时异常后把库标记为 _failed，地板层永久不渲染。
            _http.Timeout = TimeSpan.FromMinutes(30);
            _manager = new AssetBundleManager(_http, rootUrl, cacheMode);
        }

        /// <summary>已加载的 Bundle 逻辑名。</summary>
        public IReadOnlyList<string> LoadedBundles => _manager.LoadedBundles;

        /// <summary>取一个已加载的 Bundle（同步；包须先经 <see cref="LoadBundleAsync"/> 加载）。
        /// <paramref name="strict"/> 为 true 时按精确逻辑名匹配；为 false 时按关键字（键包含）匹配首个已加载 Bundle。</summary>
        public AssetBundle? GetBundle(string bundleName, bool strict = true)
            => _manager.GetBundle(bundleName, strict);

        /// <summary>尝试取一个已加载的 Bundle。
        /// <paramref name="strict"/> 为 true 时按精确逻辑名匹配；为 false 时按关键字（键包含）匹配首个已加载 Bundle。</summary>
        public bool TryGetBundle(string bundleName, out AssetBundle? bundle, bool strict = true)
            => _manager.TryGetBundle(bundleName, out bundle, strict);

        #region 清单与 Bundle 加载（全部异步，委托 AssetBundleManager）

        /// <summary>拉取并解析总清单 version.manifest（仅包列表与哈希，不含资源索引）。</summary>
        public async Task LoadManifestAsync(CancellationToken cancellationToken = default)
            => await _manager.FetchManifestAsync(cancellationToken).ConfigureAwait(false);

        /// <summary>
        /// 一键加载：先拉总清单，再并发加载其中列出的全部 Bundle（等价于“加载所有资源”）。
        /// </summary>
        public async Task LoadAsync(IProgress<float>? progress = null, CancellationToken cancellationToken = default, GraphicsDevice? device = null)
            => await _manager.LoadAllAsync(progress, cancellationToken, device).ConfigureAwait(false);

        /// <summary>
        /// 异步加载单个 AssetBundle。已加载过则直接返回（幂等）。
        /// <paramref name="strict"/> 为 true 时按精确逻辑名匹配；为 false 时按关键字（Name 包含）匹配首个包。
        /// </summary>
        public async Task<AssetBundle> LoadBundleAsync(
            string bundleName,
            CancellationToken cancellationToken = default,
            IProgress<float>? progress = null,
            bool strict = true,
            GraphicsDevice? device = null)
            => await _manager.LoadBundleAsync(bundleName, cancellationToken, progress, strict, device).ConfigureAwait(false);

        /// <summary>异步并发加载多个 AssetBundle（单个失败不影响其余，逐包上报进度 0~1）。</summary>
        public async Task<IReadOnlyList<AssetBundle>> LoadBundlesAsync(
            IEnumerable<string> bundleNames,
            CancellationToken cancellationToken = default,
            IProgress<float>? progress = null,
            GraphicsDevice? device = null)
            => await _manager.LoadBundlesAsync(bundleNames, cancellationToken, progress, device).ConfigureAwait(false);

        /// <summary>卸载一个已加载的 Bundle（释放其 zip 流；正在使用的纹理/字节请自行管理）。</summary>
        public void UnloadBundle(string bundleName) => _manager.UnloadBundle(bundleName);

        #endregion

        #region 松散文件下载（非 Bundle 内的资源，如关卡文本；属异步加载，但不经 AssetBundle）

        /// <summary>按页面基址异步下载任意文本（不走内容包，用于关卡等松散文件）。</summary>
        public async Task<string> LoadTextAsync(string relativePath, bool bUseCache = true, CancellationToken cancellationToken = default)
        {
            byte[] data = await _http.GetByteArrayAsync(relativePath, cancellationToken).ConfigureAwait(false);
            return ContentFunc.DecodeUtf8(data);
        }

        /// <summary>按页面基址异步下载任意字节流（不走内容包）。</summary>
        public async Task<byte[]> LoadBytesAsync(string relativePath, CancellationToken cancellationToken = default)
            => await _http.GetByteArrayAsync(relativePath, cancellationToken).ConfigureAwait(false);

        /// <summary>
        /// 分片（Range）下载任意字节流并在托管侧拼接。用于超大地图片库（如数百 MB 的 Tiles.Lib）：
        /// 浏览器/WASM 端若一次性把超大响应 marshal 成单个 byte[]，会在 JS↔托管边界失败或返回空，
        /// 导致上层把库误判为“空文件”而永久不渲染（典型表现：同目录小库都正常，唯独最大的 Tiles.Lib 走 empty 分支）。
        /// 按固定分片（默认 16MB）逐片下载，每片体积都在安全范围内。资源服务器需支持 Range
        /// （本仓库 Tools/资源服务器/asset-server.js 已支持）；不支持或异常时自动退化为整文件下载。
        /// </summary>
        public async Task<byte[]> LoadBytesChunkedAsync(string relativePath, int chunkSize = 16 * 1024 * 1024, CancellationToken cancellationToken = default)
        {
            long cs = Math.Max(1, (long)chunkSize);
            try
            {
                using var firstReq = new HttpRequestMessage(HttpMethod.Get, relativePath);
                firstReq.Headers.Range = new RangeHeaderValue(0, cs - 1);
                using var firstResp = await _http.SendAsync(firstReq, cancellationToken).ConfigureAwait(false);

                // 服务器不支持 Range：退化为整文件下载。
                if (firstResp.StatusCode != HttpStatusCode.PartialContent)
                    return await _http.GetByteArrayAsync(relativePath, cancellationToken).ConfigureAwait(false);

                long total = firstResp.Content.Headers.ContentRange?.Length ?? 0;
                // 无总大小或超出单个 byte[] 上限（~2GB）：退化为整文件下载。
                if (total <= 0 || total > int.MaxValue)
                    return await _http.GetByteArrayAsync(relativePath, cancellationToken).ConfigureAwait(false);

                var result = new byte[(int)total];
                byte[] first = await firstResp.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                if (first.Length > 0) Buffer.BlockCopy(first, 0, result, 0, first.Length);
                long pos = first.Length;

                while (pos < total)
                {
                    long end = Math.Min(pos + cs, total) - 1;
                    using var req = new HttpRequestMessage(HttpMethod.Get, relativePath);
                    req.Headers.Range = new RangeHeaderValue(pos, end);
                    using var resp = await _http.SendAsync(req, cancellationToken).ConfigureAwait(false);
                    if (!resp.IsSuccessStatusCode) break;
                    byte[] chunk = await resp.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                    if (chunk.Length == 0) break;
                    Buffer.BlockCopy(chunk, 0, result, (int)pos, chunk.Length);
                    pos += chunk.Length;
                }
                return result;
            }
            catch (Exception)
            {
                // 分片失败（如 Range 异常）：最后尝试整文件下载兜底。
                return await _http.GetByteArrayAsync(relativePath, cancellationToken).ConfigureAwait(false);
            }
        }

        #endregion

        #region 直接加载图片（不经内容包，用于图片未被打包、只是直接复制到站点目录的情形）

        /// <summary>
        /// 异步直接加载一张图片（不经内容包）。用于图片未被打包、只是直接复制到站点目录的情形
        /// （例如 wwwroot 下的 png/jpg/webp）。按页面基址下载后经浏览器原生解码为 RGBA8 并上传 GPU。
        /// 与 <see cref="AssetBundle"/> 上的取资源方法不同，本方法面向「包外松散图片」。
        /// </summary>
        public async Task<Texture2D> LoadTexture2DAsync(string relativePath, GraphicsDevice device, CancellationToken cancellationToken = default)
        {
            byte[] data = await _http.GetByteArrayAsync(relativePath, cancellationToken).ConfigureAwait(false);
            return await LoadTexture2DAsync(data, device).ConfigureAwait(false);
        }

        /// <summary>
        /// 异步直接加载一张图片（已持有图片字节，不经内容包）。内部借浏览器原生解码器把图像字节
        /// （PNG / JPG / WebP 等）解码为 RGBA8 后上传 GPU。
        /// </summary>
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
            await JSBind_Texture.DecodeImageToRgba(data, size, pixels).ConfigureAwait(false);
            return device.CreateTexture(w, h, pixels);
        }

        #endregion

        public void Dispose()
        {
            _manager.Dispose();
            _http.Dispose();
        }
    }
}
