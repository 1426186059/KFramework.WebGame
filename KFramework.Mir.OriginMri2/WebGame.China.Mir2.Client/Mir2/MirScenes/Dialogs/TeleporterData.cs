using System;
using System.Collections.Generic;

namespace Client.MirScenes.Dialogs
{
    /// <summary>
    /// 传送员目的地数据，1:1 取自《原始传奇》3config.deliver + NPC 600(传送员) 的 Deliver 分组字符串。
    /// 展示名 = deliver.showName，副标题 = deliver.showText（等级/转生门槛）；Id = deliverId，
    /// 点击复用 C.CallNPC 发 @deliver:ID 形式的 Key，服务端按同一张 3config.deliver 表解析 toMapId/x/y 并传送。
    /// 其余带脚本的传送员（收费传送员等）保留原 NPC 脚本菜单。
    /// </summary>
    public static class TeleporterData
    {
        /// <summary>启用自定义分类传送对话框（对齐原始传奇 DeliverDialog）。</summary>
        public static bool UseCustomDialog = true;

        public static List<DeliverDestination> GetDestinations()
        {
            var list = new List<DeliverDestination>();

            // 主城传送 (11)
            Add(list, "主城传送", "庄园", 12000);
            Add(list, "主城传送", "比奇", 12001);
            Add(list, "主城传送", "盟重", 12002);
            Add(list, "主城传送", "白日门", 12003);
            Add(list, "主城传送", "毒蛇山谷", 12004, "70级可进");
            Add(list, "主城传送", "封魔谷", 12005, "40级可进");
            Add(list, "主城传送", "苍月岛", 12006, "45级可进");
            Add(list, "主城传送", "魔龙城", 12007, "50级开启");
            Add(list, "主城传送", "石原", 12008, "4转开启");
            Add(list, "主城传送", "海岭城", 12009, "开服120天且6转");
            Add(list, "主城传送", "蛮荒营地", 12010, "开服270天且需要完成任务：[蛮荒]天地乱劫");

            // 比奇省 (21)
            Add(list, "比奇省", "毒蛇山谷", 12100);
            Add(list, "比奇省", "沃玛森林", 12101);
            Add(list, "比奇省", "地狱烈焰", 12102);
            Add(list, "比奇省", "堕落坟场", 12103);
            Add(list, "比奇省", "深渊魔域", 12104);
            Add(list, "比奇省", "沃玛一层", 12105);
            Add(list, "比奇省", "沃玛二层", 12106);
            Add(list, "比奇省", "半兽一层", 12107);
            Add(list, "比奇省", "半兽二层", 12108);
            Add(list, "比奇省", "半兽三层", 12109);
            Add(list, "比奇省", "洞穴一层", 12110);
            Add(list, "比奇省", "洞穴二层", 12111);
            Add(list, "比奇省", "比奇矿区", 12112);
            Add(list, "比奇省", "北部矿区", 12113);
            Add(list, "比奇省", "东部矿区", 12114);
            Add(list, "比奇省", "废矿一层", 12115);
            Add(list, "比奇省", "矿区二层", 12116);
            Add(list, "比奇省", "回收站", 12117);
            Add(list, "比奇省", "南部矿区", 12118);
            Add(list, "比奇省", "毒蛇一层", 12119);
            Add(list, "比奇省", "毒蛇二层", 12120);

            // 盟重省 (18)
            Add(list, "盟重省", "困惑殿堂", 12201);
            Add(list, "盟重省", "未知暗殿", 12202);
            Add(list, "盟重省", "祖玛一层", 12203);
            Add(list, "盟重省", "祖玛二层", 12204);
            Add(list, "盟重省", "祖玛三层", 12205);
            Add(list, "盟重省", "祖玛四层", 12206);
            Add(list, "盟重省", "祖玛五层", 12207);
            Add(list, "盟重省", "石墓一层", 12208);
            Add(list, "盟重省", "石墓二层", 12209);
            Add(list, "盟重省", "石墓三层", 12210);
            Add(list, "盟重省", "石墓四层", 12211);
            Add(list, "盟重省", "石墓五层", 12212);
            Add(list, "盟重省", "石墓六层", 12213);
            Add(list, "盟重省", "南一层", 12214);
            Add(list, "盟重省", "死亡棺材", 12215);
            Add(list, "盟重省", "黑暗地带", 12216);
            Add(list, "盟重省", "生死之间", 12217);
            Add(list, "盟重省", "传奇部落", 12218);

            // 白日门 (13)
            Add(list, "白日门", "钳虫巢穴", 12301);
            Add(list, "白日门", "赤月魔穴", 12302);
            Add(list, "白日门", "丛林迷宫", 12303);
            Add(list, "白日门", "东走廊", 12304);
            Add(list, "白日门", "西走廊", 12305);
            Add(list, "白日门", "西回隧道", 12306);
            Add(list, "白日门", "东二层", 12307);
            Add(list, "白日门", "峡谷广场", 12308);
            Add(list, "白日门", "东长廊", 12309);
            Add(list, "白日门", "抉择之地", 12310);
            Add(list, "白日门", "山谷秘道", 12311);
            Add(list, "白日门", "山谷秘道", 12312);
            Add(list, "白日门", "恶魔祭坛", 12313);

            // 封魔谷 (14)
            Add(list, "封魔谷", "封魔矿区", 12401, "40级可进");
            Add(list, "封魔谷", "崎路", 12402, "40级可进");
            Add(list, "封魔谷", "连接通道", 12403, "40级可进");
            Add(list, "封魔谷", "封魔道", 12404, "40级可进");
            Add(list, "封魔谷", "疾风殿", 12405, "40级可进");
            Add(list, "封魔谷", "光芒回廊", 12406, "40级可进");
            Add(list, "封魔谷", "烈焰殿", 12407, "40级可进");
            Add(list, "封魔谷", "雷霆之路", 12408, "40级可进");
            Add(list, "封魔谷", "霸者大厅", 12409, "40级可进");
            Add(list, "封魔谷", "幽冥回廊", 12410, "40级可进");
            Add(list, "封魔谷", "纵横道", 12411, "40级可进");
            Add(list, "封魔谷", "魔魂殿", 12412, "40级可进");
            Add(list, "封魔谷", "炼狱回廊", 12413, "40级可进");
            Add(list, "封魔谷", "封魔殿", 12414, "40级可进");

            // 苍月岛 (12)
            Add(list, "苍月岛", "尸魔一层", 12501, "45级可进");
            Add(list, "苍月岛", "尸魔三层", 12502, "45级可进");
            Add(list, "苍月岛", "尸魔二层", 12503, "45级可进");
            Add(list, "苍月岛", "骨魔一层", 12504, "45级可进");
            Add(list, "苍月岛", "骨魔二层", 12505, "45级可进");
            Add(list, "苍月岛", "骨魔三层", 12506, "45级可进");
            Add(list, "苍月岛", "骨魔四层", 12507, "45级可进");
            Add(list, "苍月岛", "骨魔洞五层", 15409, "1级可进");
            Add(list, "苍月岛", "牛魔一层", 12509, "45级可进");
            Add(list, "苍月岛", "牛魔二层", 12510, "45级可进");
            Add(list, "苍月岛", "牛魔三层", 12511, "45级可进");
            Add(list, "苍月岛", "牛魔四层", 12512, "45级可进");

            // 魔龙城 (13)
            Add(list, "魔龙城", "魔龙东郊", 12601, "50级开启");
            Add(list, "魔龙城", "东胜地", 12602, "50级开启");
            Add(list, "魔龙城", "魔龙旧寨", 12603, "50级开启");
            Add(list, "魔龙城", "魔龙祭坛", 12604, "50级开启");
            Add(list, "魔龙城", "魔龙西郊", 12605, "50级开启");
            Add(list, "魔龙城", "西胜地", 12606, "50级开启");
            Add(list, "魔龙城", "魔龙沼泽", 12607, "50级开启");
            Add(list, "魔龙城", "魔龙岭", 12608, "50级开启");
            Add(list, "魔龙城", "西魔龙关", 12609, "50级开启");
            Add(list, "魔龙城", "东魔龙关", 12610, "50级开启");
            Add(list, "魔龙城", "魔龙谷", 12611, "50级开启");
            Add(list, "魔龙城", "魔龙血路", 12612, "50级开启");
            Add(list, "魔龙城", "魔龙血域", 12613, "50级开启");

            // 危险地图 (8)
            Add(list, "危险地图", "地宫入口", 12701);
            Add(list, "危险地图", "地宫峡谷", 12702);
            Add(list, "危险地图", "地宫深渊", 12703);
            Add(list, "危险地图", "火龙洞窟", 12704, "1转开启");
            Add(list, "危险地图", "雷炎一层", 12705, "2转开启");
            Add(list, "危险地图", "狐月山脉", 12706, "3转开启");
            Add(list, "危险地图", "冰宫一层", 12707, "4转可进");
            Add(list, "危险地图", "石原", 12708, "4转可进");

            // 幻境传送 (7)
            Add(list, "幻境传送", "幻境一层", 12801);
            Add(list, "幻境传送", "幻境二层", 12802);
            Add(list, "幻境传送", "幻境三层", 12803);
            Add(list, "幻境传送", "幻境四层", 12804);
            Add(list, "幻境传送", "幻境五层", 12805);
            Add(list, "幻境传送", "幻境六层", 12806);
            Add(list, "幻境传送", "幻境七层", 12807);

            // 会员之家 (3)
            Add(list, "会员之家", "副本①层", 12901, "开通青铜会员可进");
            Add(list, "会员之家", "副本②层", 12902, "开通白银会员可进");
            Add(list, "会员之家", "副本③层", 12903, "开通黄金会员可进");

            // 玛雅神殿 (6)
            Add(list, "玛雅神殿", "神殿一层", 13001, "角色低于1转");
            Add(list, "玛雅神殿", "神殿二层", 13002, "仅1转可进");
            Add(list, "玛雅神殿", "神殿三层", 13003, "仅2转可进");
            Add(list, "玛雅神殿", "神殿四层", 13004, "仅3转可进");
            Add(list, "玛雅神殿", "神殿五层", 13005, "仅4转可进");
            Add(list, "玛雅神殿", "玛雅六层", 13006, "仅5转可进");

            return list;
        }

        private static void Add(List<DeliverDestination> list, string category, string name, int deliverId, string note = "")
        {
            list.Add(new DeliverDestination
            {
                Id = deliverId,
                Name = name,
                Category = category,
                Cost = 0,
                CostText = note,
            });
        }

        // 所有名字为"传送员"的 NPC 一律走这套 11 分组分类面板（包括原本带脚本的 1466 等），
        // 唯一例外是比奇省传送员 ConfigIndex=13031（MapIndex=103）——它保留旧的 NPC 脚本对话菜单。
        // 带前缀的专门 NPC（行会传送员/沙巴克传送员/幽灵船传送员等）功能不同，不在此列。
        private const int BiqiTeleporterConfigId = 13031;

        public static bool IsCityTeleporter(Client.MirObjects.NPCObject npc)
        {
            return npc != null
                && npc.Name == "传送员"
                && npc.ConfigIndex != BiqiTeleporterConfigId;
        }
    }
}
