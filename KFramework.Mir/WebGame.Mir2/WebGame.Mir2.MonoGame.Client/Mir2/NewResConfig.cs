using KFramework.MonoGame;

namespace WebGame.Mir2.MonoGame.Client
{
    /// <summary>
    /// 地图图片资源热更（URL 直链方案，不使用 AssetBundle）。
    /// 把每张地图用到的图片 Lib 提取到 Mir2Res/Map/&lt;地图名&gt;/ 下，运行时通过 http 从 <see cref="MapResourceBaseUrl"/>
    /// 直接拉取（Mir2Res 作为 http 根目录）；.map 文件不提取，仍走默认路径。
    /// 仅当某地图在本表的 <see cref="MapLibsRemote"/> 登记时，其图片 Lib 才从远程 URL 取；否则走默认 lib 通道。
    /// </summary>
    public static class NewResConfig
    {
        public static string libBaseUrl = "http://127.0.0.1:5081";
        public const string MapRoot = "Map/";
        public static bool Enabled = true;
        public static bool RemoteLibEnabled { get; internal set; }
        public static string CurrentMapName { get; internal set; } = "";

        private static readonly ContentManager mContentManager;

        static NewResConfig()
        {
            mContentManager = new ContentManager("", libBaseUrl);
        }

        private static string BuildLibPath(string relPathWithoutExt)
        {
            string p = (relPathWithoutExt ?? "").Replace('\\', '/');
            const string mapPrefix = "data/map/";
            if (p.StartsWith(mapPrefix, StringComparison.OrdinalIgnoreCase))
            {
                p = p.Substring(mapPrefix.Length); // "WemadeMir2/Tiles"
            }
            return CurrentMapName + "/" + p + ".Lib";
        }
        
        public static async Task<byte[]?> GetRemoteLibAsync(string relPathWithoutExt)
        {
            if (!Enabled || !RemoteLibEnabled) return null;
            string norm = (relPathWithoutExt ?? "").Replace('\\', '/').ToLowerInvariant();
            if (!norm.StartsWith("data/map/")) return null;

            string path = BuildLibPath(relPathWithoutExt);
            byte[] bytes = await mContentManager.LoadBytesAsync(MapRoot + path, true).ConfigureAwait(false);
            if (bytes != null && bytes.Length > 0) return bytes;
            BrowserResource.Log($"[Mir][lib] 远程空: {path}");
            return null;
        }
    }
}
