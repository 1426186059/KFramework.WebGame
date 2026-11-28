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
        protected bool _selfModelsPreloded;

        protected void PreLoadSelfModels()
        {
            if (this != User) return;
            Mir2OriginObjectLibrary.PreLoadModel(Mir2ObjectKind.Player, _originModel, EResLoadPriority.User);
            Mir2OriginObjectLibrary.PreLoadModel(Mir2ObjectKind.Player, PlayerHairModel(), EResLoadPriority.User);
            Mir2OriginObjectLibrary.PreLoadModel(Mir2ObjectKind.Player, PlayerHuJianModel(), EResLoadPriority.User);
            Mir2OriginObjectLibrary.PreLoadModel(Mir2ObjectKind.Player, PlayerHuiJiModel(), EResLoadPriority.User);
            Mir2OriginObjectLibrary.PreLoadModel(Mir2ObjectKind.Player, PlayerWeaponModel(), EResLoadPriority.User);
            Mir2OriginObjectLibrary.PreLoadModel(Mir2ObjectKind.Player, PlayerMountModel(), EResLoadPriority.User);
            Mir2OriginObjectLibrary.PreLoadModel(Mir2ObjectKind.Player, PlayerZhenfaModel(), EResLoadPriority.User);
            Mir2OriginObjectLibrary.PreLoadModel(Mir2ObjectKind.Player, PlayerFootModel(), EResLoadPriority.User);
            Mir2OriginObjectLibrary.PreLoadModel(Mir2ObjectKind.Player, PlayerPiFengModel(), EResLoadPriority.User);
            Mir2OriginObjectLibrary.PreLoadModel(Mir2ObjectKind.Player, PlayerDouLiModel(), EResLoadPriority.User);
            Mir2OriginObjectLibrary.PreLoadModel(Mir2ObjectKind.Player, PlayerMianShaModel(), EResLoadPriority.User);
            Mir2OriginObjectLibrary.PreLoadModel(Mir2ObjectKind.Player, PlayerYuPeiModel(), EResLoadPriority.User);
            Mir2OriginObjectLibrary.PreLoadModel(Mir2ObjectKind.Player, PlayerXunZhangModel(), EResLoadPriority.User);
            Mir2OriginObjectLibrary.PreLoadModel(Mir2ObjectKind.Player, PlayerHuDunModel(), EResLoadPriority.User);
            Mir2OriginObjectLibrary.PreLoadModel(Mir2ObjectKind.Player, PlayerXuanGuangModel(), EResLoadPriority.User);
            Mir2OriginObjectLibrary.PreLoadModel(Mir2ObjectKind.Player, PlayerTitleModel(), EResLoadPriority.User);
            Mir2OriginObjectLibrary.PreLoadModel(Mir2ObjectKind.Player, PlayerWingModel(), EResLoadPriority.User);
            // 裸身兜底模型（身体 CDN 缺失时回落 9001/9002）也预载，避免回退时再等待。
            Mir2OriginObjectLibrary.PreLoadModel(Mir2ObjectKind.Player, Gender == MirGender.Female ? 9002 : 9001, EResLoadPriority.User);

            PreLoadSelfMagicEffects();
        }

        /// <summary>预载自己已学技能的特效帧：skills_condition → effLevelID → skillEffects → effect.model。
        /// 高优先级（与自身模型同级），避免被地图/UI/可见实体请求长期饿死；
        /// 命中缓存/在途请求由 Mir2OriginObjectLibrary.Request 短路，重复调用无网络开销。</summary>

        public void PreLoadSelfMagicEffects()
        {
            UserObject self = this as UserObject;
            if (self == null || self != User || self.Magics == null) return;

            int requests = 0;
            int learned = 0;
            foreach (ClientMagic magic in self.Magics)
            {
                if (magic == null || magic.MaxLevel <= 0) continue; // 未学技能（MaxLevel=0 占位）不预载
                learned++;
                requests += PreLoadMagicEffectsCore(magic);
            }
            PrintTool.Log($"[技能特效预载] 已学 {learned}/{self.Magics.Count} 个技能，发起 {requests} 个方向请求（优先级 User）");
        }

        /// <summary>新学技能 / 技能升级时，只预载这一个技能当前等级的特效。</summary>

        public void PreLoadMagicEffects(ClientMagic magic)
        {
            if (this != User)
            {
                return;
            }
            PreLoadMagicEffectsCore(magic);
        }


        private int PreLoadMagicEffectsCore(ClientMagic magic)
        {
            int requests = 0;
            if (magic == null || magic.MaxLevel <= 0) return 0;
            if (!ExcelTableMgr.TryGetSkillEffects((int)magic.Spell, magic.Level, out var steps) || steps == null) return 0;

            foreach (var step in steps)
            {
                if (step.effect <= 0) continue;
                if (!ExcelTableMgr.TryGetEffect(step.effect, out var effect) || effect == null || effect.model <= 0) continue;

                int action = effect.action > 0 ? effect.action : 1;
                if (effect.totaldir <= 1)
                {
                    // 不区分方向的特效固定用配置的 dir，与 OriginEffect 的 _direction 口径一致。
                    Mir2OriginObjectLibrary.Request(Mir2ObjectKind.Monster, effect.model, action, effect.dir, EResLoadPriority.User);
                    requests++;
                }
                else
                {
                    int maxDir = effect.totaldir == 5 ? 4 : 7;
                    for (int d = 0; d <= maxDir; d++)
                    {
                        Mir2OriginObjectLibrary.Request(Mir2ObjectKind.Monster, effect.model, action, d, EResLoadPriority.User);
                        requests++;
                    }
                }
            }
            return requests;
        }

        /// <summary>后台懒预取：把自己当前动作的各方向暖好，转身/换动作不跳帧（低优先级，空闲才下）。</summary>

        public override void PreLoadBackground()
        {
            if (_originModel > 0)
                Mir2OriginObjectLibrary.PreLoadAction(Mir2ObjectKind.Player, _originModel, Mir2OriginObjectLibrary.MapAction(CurrentAction), EResLoadPriority.Background);
        }
        // 武器层：优先【幻武 type1】（时装武器，覆盖普通武器）；否则武器槽 cm.item[col17]；无则 0 不绘制。

    }
}