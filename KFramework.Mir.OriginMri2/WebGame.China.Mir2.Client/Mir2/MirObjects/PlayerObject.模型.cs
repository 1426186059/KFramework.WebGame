using Client.MirGraphics;
using Client.MirNetwork;
using Client.MirScenes;
using Client.MirSounds;
using Client.MirControls;
using S = ServerPackets;
using C = ClientPackets;
using Client.MirScenes.Dialogs;
using WebGame.Mir2.MonoGame.Client;

namespace Client.MirObjects
{
    public partial class PlayerObject : MapObject
    {
        protected int PlayerBodyModelResolved()
        {
            int costume = Mir2OriginModelResolver.GetTitleModel(_pgZhuangBan, Gender == MirGender.Female, 0);
            if (costume > 0) return costume;                       // 时装身体（覆盖盔甲）
            int armour = Mir2OriginModelResolver.GetModel(_pgArmour, Gender == MirGender.Female, 0);
            if (armour > 0) return armour;                         // 普通盔甲（含职业差异）
            return Gender == MirGender.Female ? 9002 : 9001;       // 裸身：男 9001 / 女 9002（仅按性别）
        }
        // 本地玩家(UserObject)没有 ObjectPlayer 包，需从自身 Equipment 反推各层 origin 模型号（页游物品 id = ItemInfoTable.Index）。
        // 与服务端 ObjectPlayer.OriginArmourID = Equipment[Armour]?.Info.Index 同义；盔甲/武器/坐骑来自装备槽。
        // 翅膀/装扮等本服务端未在下发的 UserInformation 中追踪，回落 0（不绘制）。仅按传入 Equipment 解算；装备变化(SetSlots)时再次调用刷新。

        private int PlayerWeaponModel()
            => Mir2OriginModelResolver.GetTitleModel(_pgHuanWu, Gender == MirGender.Female,
                Mir2OriginModelResolver.GetModel(_pgWeapon, Gender == MirGender.Female, 0));
        // 翅膀层模型号：本客户端 Looks_Wings 是另一套（byte，且页游羽翼在 cm.title type13），暂无「Looks_Wings → 页游羽翼id」映射，暂返回 0。
        // 待接 cm.title[yuyiId].model 后即可点亮。

        private int PlayerWingModel() => 0;
        // 坐骑层模型号：页游把 fashionMount 直接作为 origin 模型号传给 animal.init2（main.min.js 实锤），OriginMountID = 服务端 Mount.MountType 即模型号。

        private int PlayerMountModel() => _pgMount;
        // 以下装扮类均来自 cm.title[id].model（type 区分）：称号3 / 足迹2 / 徽记4 / 法阵5 / 旋光7。id 由服务端下发（当前默认 0）。

        private int PlayerTitleModel() => Mir2OriginModelResolver.GetTitleModel(_pgTitle, Gender == MirGender.Female, 0);    // 称号

        private int PlayerFootModel() => Mir2OriginModelResolver.GetTitleModel(_pgFoot, Gender == MirGender.Female, 0);      // 足迹

        private int PlayerHuiJiModel() => Mir2OriginModelResolver.GetTitleModel(_pgHuiJi, Gender == MirGender.Female, 0);    // 徽记

        private int PlayerZhenfaModel() => Mir2OriginModelResolver.GetTitleModel(_pgZhenfa, Gender == MirGender.Female, 0);  // 法阵

        private int PlayerXuanGuangModel() => Mir2OriginModelResolver.GetTitleModel(_pgXuanGuang, Gender == MirGender.Female, 0); // 旋光
        // 以下装饰装备层均来自 cm.item[type] 角色精灵（type：斗笠50/勋章51/玉佩52/护盾53/护肩54/披风55/面纱406）。id 由服务端下发（默认 0）。

        private int PlayerDouLiModel() => Mir2OriginModelResolver.GetModel(_pgDouLi, Gender == MirGender.Female, 0);      // 斗笠（头部）

        private int PlayerMianShaModel() => Mir2OriginModelResolver.GetModel(_pgMianSha, Gender == MirGender.Female, 0);   // 面纱（面部）

        private int PlayerYuPeiModel() => Mir2OriginModelResolver.GetModel(_pgYuPei, Gender == MirGender.Female, 0);        // 玉佩

        private int PlayerXunZhangModel() => Mir2OriginModelResolver.GetModel(_pgXunZhang, Gender == MirGender.Female, 0);  // 勋章（荣誉勋章）

        private int PlayerHuDunModel() => Mir2OriginModelResolver.GetModel(_pgHuDun, Gender == MirGender.Female, 0);        // 护盾

        private int PlayerHuJianModel() => Mir2OriginModelResolver.GetModel(_pgHuJian, Gender == MirGender.Female, 0);      // 护肩

        private int PlayerPiFengModel() => Mir2OriginModelResolver.GetModel(_pgPiFeng, Gender == MirGender.Female, 0);      // 披风

        private int PlayerLiaoShangModel() => Mir2OriginModelResolver.GetModel(_pgLiaoShang, Gender == MirGender.Female, 0); // 疗伤石（页游多为纯属性，无精灵）
        // 头发层：原始传奇里头发是叠在（光头）身体底模之上的独立 origin 模型层；已确认 model/9498 男、model/9499 女（CDN 版本 10.1393.1，含完整角色动画 1/2/3/4/5/10/21/23/99）。
        // 与经典 Mir2 一致：身体底模（9001/9002 等）不含头发，头发需单独叠绘。本版本 CDN 仅两套头发模型，按性别取（Hair 发型字段暂不参与模型选择）。

        private int PlayerHairModel() => Gender == MirGender.Female ? 9499 : 9498;

    }
}