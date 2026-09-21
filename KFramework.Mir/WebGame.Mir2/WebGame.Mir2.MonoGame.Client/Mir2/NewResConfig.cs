using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using MirEngine;

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
        /// <summary>地图资源(http)根地址，须以 '/' 结尾。指向“Mir2Res 作为 http 根目录”的地址。
        /// 例：http://localhost:5081/ → 实际文件 http://localhost:5081/Map/0/WemadeMir2/Tiles.Lib
        /// （其中 "0" 为地图名，"WemadeMir2/Tiles" 为 Lib 在原 Data/Map/ 下的相对子路径）。
        /// 部署时按实际 http 服务器修改（把 Mir2Res 挂为根目录）。</summary>
        public static string MapResourceBaseUrl = "http://localhost:5081/";

        /// <summary>总开关：false 时全部走默认 lib 通道，便于一键回退。</summary>
        public static bool Enabled = true;

        /// <summary>登记哪些地图的图片 Lib 已提取到 Mir2Res/Map/&lt;地图名&gt;/ 走 URL 直链（.map 仍默认）。
        /// 键 = 地图名（即 .map 文件名，不含目录/扩展名，与服务端 MapInformation.FileName 及工具侧 mapName 一致）。
        /// 例：{ "0" } 表示地图 "0" 的图片 Lib 走远程。从 Mir2Res/Map/0/ 目录即可核对。</summary>
        //public static readonly HashSet<string> MapLibsRemote = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        //{
        //    "0", "0100", "0101", "0102"
        //};

        /// <summary>当前是否走远程 Lib（进入登记地图时置 true，进入未登记地图置 false）。</summary>
        public static bool RemoteLibEnabled { get; internal set; }

        /// <summary>当前地图名（进入地图时由 MapReader.LoadAsync 设置）。用于拼远程 Lib 的 URL 路径：
        /// Mir2Res/Map/&lt;当前地图名&gt;/&lt;lib子路径&gt;.Lib。</summary>
        public static string CurrentMapName { get; internal set; } = "";

        /// <summary>给定工具侧 Lib 相对路径（如 "Data/Map/WemadeMir2/Tiles"，不含扩展名），拼出 Mir2Res 下的 http URL：
        /// 去掉 "Data/Map/" 前缀得到 "WemadeMir2/Tiles"，再按 Mir2Res/Map/&lt;当前地图名&gt;/&lt;lib子路径&gt;.Lib 组合。</summary>
        public static string BuildLibUrl(string relPathWithoutExt)
        {
            string p = (relPathWithoutExt ?? "").Replace('\\', '/');
            const string mapPrefix = "data/map/";
            if (p.StartsWith(mapPrefix, StringComparison.OrdinalIgnoreCase))
                p = p.Substring(mapPrefix.Length); // "WemadeMir2/Tiles"
            string url = MapResourceBaseUrl;
            if (!url.EndsWith("/")) url += "/";
            // Mir2Res/Map/<地图名>/<lib子路径>.Lib
            return url + "Map/" + CurrentMapName + "/" + p + ".Lib";
        }

        private static readonly HttpClient HttpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };

        /// <summary>从远程 URL 取 Lib 字节；失败/未开启返回 null（调用方应回退默认 lib 通道）。</summary>
        public static async Task<byte[]?> GetRemoteLibAsync(string relPathWithoutExt)
        {
            if (!Enabled || !RemoteLibEnabled) return null;
            try
            {
                string url = BuildLibUrl(relPathWithoutExt);
                BrowserResource.Log($"[Mir][lib] 远程拉取 {url}");
                byte[] bytes = await HttpClient.GetByteArrayAsync(url).ConfigureAwait(false);
                if (bytes != null && bytes.Length > 0) return bytes;
                BrowserResource.Log($"[Mir][lib] 远程空: {url}");
            }
            catch (Exception ex)
            {
                BrowserResource.Log($"[Mir][lib] 远程拉取失败 {relPathWithoutExt}: {ex.Message}");
            }
            return null;
        }
    }
}
