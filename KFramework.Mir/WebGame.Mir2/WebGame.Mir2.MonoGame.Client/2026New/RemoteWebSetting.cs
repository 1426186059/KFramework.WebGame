// Web 部署相关配置集中地。
//
// 桌面端这些本是「随安装环境变化、需要运行时从服务器下发的配置」（资源服务器地址、游戏 WS 网关、
// 语言包地址等）。Web 端没有「本地」概念，全部集中写死在此处：部署时只改这一个文件即可，
// 不需要 remote-config.json。
//
// 各字段即是「内置默认」；语言包等需要运行时拉取的内容，下载失败时回退到代码内置值。
using Client;

public static class RemoteWebSetting
{
    // 默认资源 lib 服务器（原 CMain.Init 写死的 :5080，根=Crystal Build，松加载原始 .Lib）
    public const string LibBaseUrl = "http://127.0.0.1:5080/";

    // AssetBundle(hot_update_res) 服务器（原写死的 :5081/hot_update_res/）
    public const string New_LibBaseUrl = "http://127.0.0.1:5081/";

    // 游戏 WS 网关（原 Settings.IPAddress:Port）。Web 下连接地址由部署决定，不从本地 ini 取。
    public const string GameServerUrl = "ws://127.0.0.1:7000";

    // 语言包相对地址（相对 LibBaseUrl）。例如 Chinese.json -> <LibBaseUrl>i18n/Chinese.json。
    // 启动时会尝试从此处下载默认词库覆盖代码内置默认；下载失败则保留代码内置默认。
    public const string LanguageBaseRootDir = "Localization/";

    public static async void Init()
    {
        await GameLanguage.LoadServerLanguageAsync(Settings.Language);
    }
}
