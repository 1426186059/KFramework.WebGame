using System;
using System.Collections.Generic;

namespace Client.MirScenes.Dialogs
{
    /// <summary>
    /// 传送员目的地数据（客户端内置，镜像 Envir/NPCs/盟重/土城/Transport.txt 的 11 分组）。
    /// Action 为服务端 @mv_xxx 页键（不含 @）；点击即发 C.CallNPC 到该页执行 MOVE，无需改动服务端。
    /// 若后续不同传送员有各自的目的地/收费，可在此按 NPC 名/地图返回不同列表。
    /// </summary>
    public static class TeleporterData
    {
        public static List<DeliverDestination> GetDestinations()
        {
            var list = new List<DeliverDestination>();

            // 主城传送
            const string main = "主城传送";
            Add(list, main, "比奇省", "mv_103");
            Add(list, main, "盟重", "mv_154");
            Add(list, main, "白日门", "mv_5384");
            Add(list, main, "沃玛森林", "mv_5426");
            Add(list, main, "毒蛇山谷", "mv_5427");
            Add(list, main, "封魔谷", "mv_5392");
            Add(list, main, "苍月岛", "mv_5402");
            Add(list, main, "魔龙城", "mv_5414");
            Add(list, main, "沙巴克皇宫", "mv_1025");
            Add(list, main, "庄园", "mv_5258");
            Add(list, main, "隐世小岛", "mv_1000");

            // 比奇省
            const string bi = "比奇省";
            Add(list, bi, "比奇省", "mv_103");
            Add(list, bi, "比奇矿区", "mv_5011");
            Add(list, bi, "北部矿区", "mv_5012");
            Add(list, bi, "东部矿区", "mv_5014");
            Add(list, bi, "南部矿区", "mv_5022");
            Add(list, bi, "废矿区一层", "mv_5016");
            Add(list, bi, "桃源之门", "mv_5087");

            // 盟重省
            const string meng = "盟重省";
            Add(list, meng, "盟重", "mv_154");
            Add(list, meng, "沙巴克皇宫", "mv_1025");
            Add(list, meng, "沙城外城", "mv_962");
            Add(list, meng, "沙巴克外城", "mv_1026");
            Add(list, meng, "沙城秘密通道", "mv_5311");
            Add(list, meng, "沃玛教主大殿", "mv_5355");
            Add(list, meng, "土城监狱", "mv_5365");

            // 白日门
            const string bai = "白日门";
            Add(list, bai, "白日门", "mv_5384");
            Add(list, bai, "沃玛寺庙入口", "mv_5416");
            Add(list, bai, "沃玛寺庙一层", "mv_5417");
            Add(list, bai, "沃玛寺庙二层", "mv_5418");
            Add(list, bai, "牛魔寺庙出入口", "mv_5410");
            Add(list, bai, "牛魔寺庙大厅", "mv_5412");
            Add(list, bai, "牛魔寺一层", "mv_5419");
            Add(list, bai, "牛魔寺六层", "mv_5425");
            Add(list, bai, "骨魔洞一层", "mv_5155");
            Add(list, bai, "骨魔洞三层", "mv_5157");
            Add(list, bai, "尸魔洞一层", "mv_5152");
            Add(list, bai, "尸魔洞三层", "mv_5153");

            // 封魔谷
            const string feng = "封魔谷";
            Add(list, feng, "封魔谷", "mv_5392");
            Add(list, feng, "封魔殿", "mv_5393");
            Add(list, feng, "封魔皇宫", "mv_5394");
            Add(list, feng, "封魔矿区", "mv_5138");
            Add(list, feng, "封魔道", "mv_5141");
            Add(list, feng, "霸者大厅", "mv_5146");
            Add(list, feng, "纵横道", "mv_5148");
            Add(list, feng, "魔魂殿", "mv_5149");
            Add(list, feng, "炼狱回廊", "mv_5150");

            // 苍月岛
            const string cang = "苍月岛";
            Add(list, cang, "苍月岛", "mv_5402");
            Add(list, cang, "苍月岛", "mv_5800");
            Add(list, cang, "牛魔寺庙出入口", "mv_5410");
            Add(list, cang, "牛魔寺庙大厅", "mv_5412");
            Add(list, cang, "骨魔洞五层", "mv_5409");
            Add(list, cang, "庄园", "mv_5413");

            // 魔龙城
            const string mo = "魔龙城";
            Add(list, mo, "魔龙城", "mv_5414");
            Add(list, mo, "魔龙岭", "mv_5415");
            Add(list, mo, "魔龙东郊", "mv_5259");
            Add(list, mo, "魔龙旧寨", "mv_5261");
            Add(list, mo, "魔龙祭坛", "mv_5262");
            Add(list, mo, "魔龙西郊", "mv_5263");
            Add(list, mo, "魔龙沼泽", "mv_5265");
            Add(list, mo, "魔龙谷", "mv_5269");
            Add(list, mo, "魔龙血路", "mv_5270");
            Add(list, mo, "魔龙血域", "mv_5271");
            Add(list, mo, "魔龙宫殿", "mv_5307");
            Add(list, mo, "魔龙殿", "mv_5308");
            Add(list, mo, "火龙洞窟", "mv_5272");
            Add(list, mo, "火龙地带", "mv_5273");
            Add(list, mo, "火龙殿", "mv_5274");

            // 危险地图
            const string danger = "危险地图";
            Add(list, danger, "世界BOSS", "mv_4902");
            Add(list, danger, "四方迷阵", "mv_4901");
            Add(list, danger, "神兽降临", "mv_4903");
            Add(list, danger, "尸王殿", "mv_5124");
            Add(list, danger, "未知暗殿", "mv_5120");
            Add(list, danger, "困惑殿堂", "mv_5233");
            Add(list, danger, "死亡神殿", "mv_5229");
            Add(list, danger, "深渊魔域", "mv_5230");
            Add(list, danger, "钳虫巢穴", "mv_5231");
            Add(list, danger, "地狱烈焰", "mv_5232");
            Add(list, danger, "恶魔祭坛", "mv_5135");
            Add(list, danger, "抉择之地", "mv_5132");
            Add(list, danger, "山谷秘道", "mv_5133");
            Add(list, danger, "赤月魔巢穴", "mv_5136");
            Add(list, danger, "邪恶之都", "mv_5137");

            // 幻境传送
            const string mir = "幻境传送";
            Add(list, mir, "幻境一层", "mv_5210");
            Add(list, mir, "幻境二层", "mv_5211");
            Add(list, mir, "幻境三层", "mv_5212");
            Add(list, mir, "幻境四层", "mv_5213");
            Add(list, mir, "幻境五层", "mv_5214");
            Add(list, mir, "幻境六层", "mv_5215");
            Add(list, mir, "幻境七层", "mv_5216");
            Add(list, mir, "幻境八层", "mv_5224");
            Add(list, mir, "幻境九层", "mv_5225");
            Add(list, mir, "幻境迷宫", "mv_5217");

            // 会员之家
            const string member = "会员之家";
            Add(list, member, "会员之家①层", "mv_5248");
            Add(list, member, "会员之家②层", "mv_5249");
            Add(list, member, "会员之家③层", "mv_5250");
            Add(list, member, "会员圣地", "mv_5251");
            Add(list, member, "会员小镇", "mv_5507");
            Add(list, member, "会员都市", "mv_5546");

            // 玛雅神殿
            const string maya = "玛雅神殿";
            Add(list, maya, "玛雅神殿一层", "mv_5242");
            Add(list, maya, "玛雅神殿二层", "mv_5243");
            Add(list, maya, "玛雅神殿三层", "mv_5244");
            Add(list, maya, "玛雅神殿四层", "mv_5245");
            Add(list, maya, "玛雅神殿五层", "mv_5246");
            Add(list, maya, "玛雅神殿六层", "mv_5247");
            Add(list, maya, "玛雅圣殿一层", "mv_5252");
            Add(list, maya, "玛雅圣殿二层", "mv_5253");

            return list;
        }

        private static void Add(List<DeliverDestination> list, string category, string name, string action)
        {
            list.Add(new DeliverDestination
            {
                Name = name,
                Category = category,
                Action = action,
                Cost = 0,
            });
        }

        // 仅“城市传送员”（主城土城传送员 Transport_Edwin）使用分类对话框。
        // 其余收费传送员（边境村 Teleport_Gilbert、毒蛇山谷 Transport_Chandler）保留原 BigButtonDialog 收费 UI。
        // NPC 名取自服务端 NPCInfo.json 的 Name 字段（客户端 NPCObject.Name）。
        private static readonly HashSet<string> CityTeleporterNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Transport_Edwin",
        };

        public static bool IsCityTeleporter(string npcName)
        {
            return npcName != null && CityTeleporterNames.Contains(npcName);
        }
    }
}
