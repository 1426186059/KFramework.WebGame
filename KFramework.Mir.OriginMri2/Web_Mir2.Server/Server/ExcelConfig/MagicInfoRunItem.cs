using System;
using Server.MirEnvir;
using S = ServerPackets;

namespace Server.ExcelConfig
{
    // 运行期技能对象：外层持有静态配置 Info，等级/熟练度/存档/发包/战斗计算都保存在本类。
    // 静态列在 MagicInfoTable（MagicInfoTable.cs），不写回 MagicInfo.json。
    public class MagicInfoRunItem
    {
        protected static Envir Envir
        {
            get { return Envir.Main; }
        }

        public Spell Spell;
        public MagicInfoTable Info;

        // MaxLevel=技能书决定的等级上限，Level=当前实际等级（0 起，靠熟练度提升）。
        public byte Level, MaxLevel, Key;
        public ushort Experience;
        public bool IsTempSpell;
        public long CastTime;

        // 是否已学会：MaxLevel>0 表示技能书/自动学会已解锁；Level 是 0 起的当前等级，不能用来判断。
        public bool IsLearned()
        {
            return MaxLevel > 0;
        }

        private MagicInfoTable GetMagicInfo(Spell spell)
        {
            for (int i = 0; i < Envir.MagicInfoList.Count; i++)
            {
                MagicInfoTable info = Envir.MagicInfoList[i];
                if (info.Spell != spell)
                {
                    continue;
                }
                return info;
            }
            return null;
        }

        public MagicInfoRunItem(Spell spell)
        {
            Spell = spell;
            Info = GetMagicInfo(Spell);
        }

        public MagicInfoRunItem(BinaryReader reader, int version, int customVersion)
        {
            Spell = (Spell)reader.ReadInt32();
            Info = GetMagicInfo(Spell);

            Level = reader.ReadByte();
            Key = reader.ReadByte();
            Experience = reader.ReadUInt16();

            if (version < 15)
            {
                return;
            }
            IsTempSpell = reader.ReadBoolean();

            if (version < 65)
            {
                return;
            }
            CastTime = reader.ReadInt64();

            // 122: 新增 MaxLevel。旧档的 Level 原本存的是技能书等级，直接迁移过来。
            MaxLevel = version > 121 ? reader.ReadByte() : Level;
        }

        public void Save(BinaryWriter writer)
        {
            writer.Write((int)Spell);

            writer.Write(Level);
            writer.Write(Key);
            writer.Write(Experience);
            writer.Write(IsTempSpell);
            writer.Write(CastTime);
            writer.Write(MaxLevel);
        }

        public Packet GetInfo(bool hero)
        {
            return new S.NewMagic
            {
                Magic = CreateClientMagic(),
                Hero = hero
            };
        }

        public ClientMagic CreateClientMagic()
        {
            return new ClientMagic
            {
                Name = Info.Name,
                Spell = Spell,
                BaseCost = Info.BaseCost,
                LevelCost = Info.LevelCost,
                Icon = Info.Icon,
                Level1 = Info.Level1,
                Level2 = Info.Level2,
                Level3 = Info.Level3,
                Need1 = Info.Need1,
                Need2 = Info.Need2,
                Need3 = Info.Need3,
                Level = Level,
                MaxLevel = MaxLevel,
                Key = Key,
                Experience = Experience,
                IsTempSpell = IsTempSpell,
                Delay = GetDelay(),
                Range = Info.Range,
                CastTime = CastTime - Envir.Time,
                OriginIcon = Info.OriginIcon,
                TargetType = (byte)Info.TargetType,
                // 需目标由 releaseType==1 决定；targetType 对召唤/火符都是1，不能用作判据。
                NeedTarget = Info.ReleaseType == 1,
                EffectIds = Info.EffectIds ?? new int[0]
            };
        }

        public int GetDamage(int DamageBase)
        {
            return (int)((DamageBase + GetPower()) * GetMultiplier());
        }

        public float GetMultiplier()
        {
            return (Info.MultiplierBase + (Level * Info.MultiplierBonus));
        }

        public int GetPower()
        {
            return (int)Math.Round((MPower() / 4F) * (Level + 1) + DefPower());
        }

        public int MPower()
        {
            if (Info.MPowerBonus > 0)
            {
                return Envir.Random.Next(Info.MPowerBase, Info.MPowerBonus + Info.MPowerBase);
            }
            else
            {
                return Info.MPowerBase;
            }
        }

        public int DefPower()
        {
            if (Info.PowerBonus > 0)
            {
                return Envir.Random.Next(Info.PowerBase, Info.PowerBonus + Info.PowerBase);
            }
            else
            {
                return Info.PowerBase;
            }
        }

        public int GetPower(int power)
        {
            return (int)Math.Round(power / 4F * (Level + 1) + DefPower());
        }

        public long GetDelay()
        {
            return Info.DelayBase - (Level * Info.DelayReduction);
        }
    }
}
