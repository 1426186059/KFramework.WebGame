// 原始传奇配置分片 3config 的根类（对应 3config10.25341.1.json）。
// 顶层 27 张表（rank/denglu/qiandao/suitshow/...），目前均未建具体 DTO，
// 全部由基类 ExtraTables 原样兜底（表名 → JsonElement），以后要用某张表时在本类加具体 DTO 属性即可。
namespace Server.MirDatabase
{
    public class OriginMir2Config3 : OriginConfigRoot
    {
    }
}
