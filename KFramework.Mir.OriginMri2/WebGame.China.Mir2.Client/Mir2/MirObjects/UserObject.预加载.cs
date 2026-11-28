namespace Client.MirObjects
{
    public partial class UserObject : PlayerObject
    {
        protected void SetUserOriginModels(UserItem[] equipment)
        {
            int armour = equipment != null && (int)EquipmentSlot.Armour < equipment.Length ? equipment[(int)EquipmentSlot.Armour]?.Info?.Index ?? 0 : 0;
            int weapon = equipment != null && (int)EquipmentSlot.Weapon < equipment.Length ? equipment[(int)EquipmentSlot.Weapon]?.Info?.Index ?? 0 : 0;
            int mount  = equipment != null && (int)EquipmentSlot.Mount  < equipment.Length ? equipment[(int)EquipmentSlot.Mount]?.Info?.Index ?? 0 : 0;
            // 装饰层：斗笠/面纱/玉佩/勋章/疗伤石也有装备槽，必须与服务端 ObjectPlayer.Origin*ID 同口径，
            // 否则自己的这些层恒为 0 被 DrawLayer 跳过（别人却能看到）。
            int douLi    = equipment != null && (int)EquipmentSlot.DouLi     < equipment.Length ? equipment[(int)EquipmentSlot.DouLi]?.Info?.Index ?? 0 : 0;
            int mianSha  = equipment != null && (int)EquipmentSlot.MianSha   < equipment.Length ? equipment[(int)EquipmentSlot.MianSha]?.Info?.Index ?? 0 : 0;
            int yuPei    = equipment != null && (int)EquipmentSlot.YuPei     < equipment.Length ? equipment[(int)EquipmentSlot.YuPei]?.Info?.Index ?? 0 : 0;
            int xunZhang = equipment != null && (int)EquipmentSlot.XunZhang  < equipment.Length ? equipment[(int)EquipmentSlot.XunZhang]?.Info?.Index ?? 0 : 0;
            int liaoShang= equipment != null && (int)EquipmentSlot.LiaoShang < equipment.Length ? equipment[(int)EquipmentSlot.LiaoShang]?.Info?.Index ?? 0 : 0;

            bool changed = armour != _pgArmour || weapon != _pgWeapon || mount != _pgMount
                        || douLi != _pgDouLi || mianSha != _pgMianSha || yuPei != _pgYuPei
                        || xunZhang != _pgXunZhang || liaoShang != _pgLiaoShang;

            _pgArmour = armour;
            _pgWeapon = weapon;
            _pgMount  = mount;
            _pgDouLi = douLi;
            _pgMianSha = mianSha;
            _pgYuPei = yuPei;
            _pgXunZhang = xunZhang;
            _pgLiaoShang = liaoShang;
            _originModel = PlayerBodyModelResolved();

            // 装备没变则不重复预载（RefreshStats 调用频繁），变了才重新走急队列预载新装备层。
            if (changed || !_selfModelsPreloded)
            {
                PreLoadSelfModels();
                _selfModelsPreloded = true;
            }
        }
    }
}