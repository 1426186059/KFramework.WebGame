// 原始传奇配置分片 1config 的根类（对应 1config10.25341.1.json）。
// 1config 只有 item 一张表（17166 项的稀疏数字列数组，列号无语义名），
// 其语义映射已在 Tools/Mir2ItemConverter + 服务器 ItemInfoTable 完成，故这里不建单条 DTO，
// 整张表由基类 ExtraTables 原样兜底（item → JsonElement），以后要用直接取 JsonElement 或加具体 DTO。
namespace Server.MirDatabase
{
    public class OriginMir2Config1 : OriginConfigRoot
    {
    }
}
