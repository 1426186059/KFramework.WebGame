// partial 文件：PlayerObject 功能拆分（原 PlayerObject.cs 原样迁入）。
using System.Drawing;
using C = ClientPackets;
using Server.MirDatabase;
using Server.MirEnvir;
using Server.MirNetwork;
using S = ServerPackets;
using System.Text.RegularExpressions;
using Timer = Server.MirEnvir.Timer;

namespace Server.MirObjects
{
    public partial class PlayerObject
    {
        #region IntelligentCreatures

        public void SummonIntelligentCreature(IntelligentCreatureType pType)
        {
            if (pType == IntelligentCreatureType.None) return;

            if (Dead) return;

            if (CurrentMap?.Info?.NoIntelligentCreatures == true)
            {
                ReceiveChat(GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.IntelligentCreaturesCannotBeSummonedOnMap), ChatType.System);
                return;
            }

            if (CreatureSummoned == true || SummonedCreatureType != IntelligentCreatureType.None) return;

            for (int i = 0; i < Info.IntelligentCreatures.Count; i++)
            {
                if (Info.IntelligentCreatures[i].PetType != pType) continue;

                MonsterInfoTable mInfo = Envir.GetMonsterInfo(64, (byte)pType);
                if (mInfo == null) return;

                MonsterObject monster = MonsterObject.GetMonster(mInfo);

                if (monster == null) return;
                monster.PetLevel = 0;
                monster.Master = this;
                monster.MaxPetLevel = 7;
                monster.Direction = Direction;
                monster.ActionTime = Envir.Time + 1000;

                var pet = (IntelligentCreatureObject)monster;

                pet.CreatureInfo = Info.IntelligentCreatures[i];
                pet.CreatureRules = new IntelligentCreatureRules
                {
                    MinimalFullness = Info.IntelligentCreatures[i].Info.MinimalFullness,
                    MousePickupEnabled = Info.IntelligentCreatures[i].Info.MousePickupEnabled,
                    MousePickupRange = Info.IntelligentCreatures[i].Info.MousePickupRange,
                    AutoPickupEnabled = Info.IntelligentCreatures[i].Info.AutoPickupEnabled,
                    AutoPickupRange = Info.IntelligentCreatures[i].Info.AutoPickupRange,
                    SemiAutoPickupEnabled = Info.IntelligentCreatures[i].Info.SemiAutoPickupEnabled,
                    SemiAutoPickupRange = Info.IntelligentCreatures[i].Info.SemiAutoPickupRange,
                    CanProduceBlackStone = Info.IntelligentCreatures[i].Info.CanProduceBlackStone
                };

                if (!CurrentMap.ValidPoint(Front)) return;
                monster.Spawn(CurrentMap, Front);
                Pets.Add(monster);

                CreatureSummoned = true;
                SummonedCreatureType = pType;

                ReceiveChat((GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.CreatureSummoned), Info.IntelligentCreatures[i].CustomName)), ChatType.System);
                break;
            }

            //update client
            GetCreaturesInfo();
        }

        public void UnSummonIntelligentCreature(IntelligentCreatureType pType, bool doUpdate = true)
        {
            if (pType == IntelligentCreatureType.None) return;

            for (int i = 0; i < Pets.Count; i++)
            {
                if (Pets[i].Race != ObjectType.Creature) continue;

                var pet = (IntelligentCreatureObject)Pets[i];
                if (pet.PetType != pType) continue;
                if (doUpdate) ReceiveChat(GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.CreatureDismissed), pet.CustomName), ChatType.System);

                pet.Die();

                CreatureSummoned = false;
                SummonedCreatureType = IntelligentCreatureType.None;
                break;
            }

            //update client
            if (doUpdate) GetCreaturesInfo();
        }

        public void ReleaseIntelligentCreature(IntelligentCreatureType pType, bool doUpdate = true)
        {
            if (pType == IntelligentCreatureType.None) return;

            //remove creature
            for (int i = 0; i < Info.IntelligentCreatures.Count; i++)
            {
                if (Info.IntelligentCreatures[i].PetType != pType) continue;

                if (doUpdate) ReceiveChat((GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.CreatureReleased), Info.IntelligentCreatures[i].CustomName)), ChatType.System);

                Info.IntelligentCreatures.Remove(Info.IntelligentCreatures[i]);
                break;
            }

            //re-arrange slots
            for (int i = 0; i < Info.IntelligentCreatures.Count; i++)
                Info.IntelligentCreatures[i].SlotIndex = i;

            //update client
            if (doUpdate) GetCreaturesInfo();
        }

        public void UpdateSummonedCreature(IntelligentCreatureType pType)
        {
            if (pType == IntelligentCreatureType.None) return;

            UserIntelligentCreature creatureInfo = null;
            for (int i = 0; i < Info.IntelligentCreatures.Count; i++)
            {
                if (Info.IntelligentCreatures[i].PetType != pType) continue;

                creatureInfo = Info.IntelligentCreatures[i];
                break;
            }
            if (creatureInfo == null) return;

            for (int i = 0; i < Pets.Count; i++)
            {
                if (Pets[i].Race != ObjectType.Creature) continue;

                var pet = (IntelligentCreatureObject)Pets[i];
                if (pet.PetType != pType) continue;

                pet.CustomName = creatureInfo.CustomName;
                pet.ItemFilter = creatureInfo.Filter;
                pet.CurrentPickupMode = creatureInfo.petMode;
                break;
            }
        }

        public void RefreshCreaturesTimeLeft()
        {
            if (Info.IntelligentCreatures.Count == 0) return;

            if (Envir.Time > CreatureTimeLeftTicker)
            {
                //ExpireTime
                List<int> releasedPets = new List<int>();
                CreatureTimeLeftTicker = Envir.Time + Settings.Second;

                for (int i = 0; i < Info.IntelligentCreatures.Count; i++)
                {
                    if (Info.IntelligentCreatures[i].Expire == DateTime.MinValue) continue; //permanent

                    if (Info.IntelligentCreatures[i].Expire < Envir.Now)
                    {
                        //Info.IntelligentCreatures[i].ExpireTime = 0;

                        if (CreatureSummoned && SummonedCreatureType == Info.IntelligentCreatures[i].PetType)
                        {
                            UnSummonIntelligentCreature(SummonedCreatureType, false);
                        }

                        releasedPets.Add(i);
                    }
                }

                for (int i = (releasedPets.Count - 1); i >= 0; i--)
                {
                    ReceiveChat(GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.CreatureExpired), Info.IntelligentCreatures[releasedPets[i]].CustomName), ChatType.System);
                    ReleaseIntelligentCreature(Info.IntelligentCreatures[releasedPets[i]].PetType, false);
                }

                if (SendIntelligentCreatureUpdates && CreatureSummoned && SummonedCreatureType != IntelligentCreatureType.None)
                {
                    //update client
                    GetCreaturesInfo();
                }
            }
        }

        public void RefreshCreatureSummoned()
        {
            if (SummonedCreatureType == IntelligentCreatureType.None || !CreatureSummoned)
            {
                //make sure both are in the unsummoned state
                CreatureSummoned = false;
                SummonedCreatureType = IntelligentCreatureType.None;
                return;
            }

            bool petFound = false;
            for (int i = 0; i < Pets.Count; i++)
            {
                if (Pets[i].Race != ObjectType.Creature) continue;

                var pet = (IntelligentCreatureObject)Pets[i];
                if (pet.PetType != SummonedCreatureType) continue;
                petFound = true;
                break;
            }

            if (!petFound)
            {
                MessageQueue.EnqueueDebugging(GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.SummonedCreatureNotExist), Name, SummonedCreatureType.ToString()));
                CreatureSummoned = false;
                SummonedCreatureType = IntelligentCreatureType.None;
            }
        }

        public void IntelligentCreaturePickup(bool mousemode, Point atlocation)
        {
            if (!CreatureSummoned) return;

            for (int i = 0; i < Pets.Count; i++)
            {
                if (Pets[i].Race != ObjectType.Creature) continue;

                var pet = (IntelligentCreatureObject)Pets[i];
                if (pet.PetType != SummonedCreatureType) continue;

                pet.ManualPickup(mousemode, atlocation);
                break;
            }
        }

        public void IntelligentCreatureGainPearls(int amount)
        {
            Info.PearlCount += amount;
            if (Info.PearlCount > int.MaxValue) Info.PearlCount = int.MaxValue;
        }

        public void IntelligentCreatureLosePearls(int amount)
        {
            Info.PearlCount -= amount;
            if (Info.PearlCount < 0) Info.PearlCount = 0;
        }

        public void IntelligentCreatureProducePearl()
        {
            Info.PearlCount++;
        }
        public bool IntelligentCreatureProduceBlackStone()
        {
            ItemInfoTable iInfo = Envir.GetItemInfo(Settings.CreatureBlackStoneName);
            if (iInfo == null) return false;

            UserItem item = Envir.CreateDropItem(iInfo);
            item.Count = 1;

            if (!CanGainItem(item))
            {
                MailInfo mail = new MailInfo(Info.Index)
                {
                    MailID = ++Envir.NextMailID,
                    Sender = "BlackStone",
                    Message = GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.PetProducedBlackStoneInventoryFull),
                    Items = new List<UserItem> { item },
                };

                mail.Send();
                return false;
            }

            GainItem(item);
            return true;
        }

        public void IntelligentCreatureSay(IntelligentCreatureType pType, string message)
        {
            if (!CreatureSummoned || message == "") return;
            if (pType != SummonedCreatureType) return;

            for (int i = 0; i < Pets.Count; i++)
            {
                if (Pets[i].Race != ObjectType.Creature) continue;

                var pet = (IntelligentCreatureObject)Pets[i];
                if (pet.PetType != pType) continue;

                Enqueue(new S.ObjectChat { ObjectID = Pets[i].ObjectID, Text = message, Type = ChatType.Nearby });
                return;
            }
        }

        public void StrongboxRewardItem(int boxtype)
        {
            int highRate = int.MaxValue;
            UserItem dropItem = null;

            foreach (DropInfo drop in Envir.StrongboxDrops)
            {
                int rate = (int)(Envir.Random.Next(0, drop.Chance) / Settings.DropRate);
                if (rate < 1) rate = 1;

                if (highRate > rate)
                {
                    highRate = rate;
                    dropItem = Envir.CreateFreshItem(drop.Item);
                }
            }

            if (dropItem == null)
            {
                ReceiveChat(GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.NothingFound), ChatType.System);
                return;
            }

            if (dropItem.Info.Type == ItemType.Pets && dropItem.Info.Shape == 26)
            {
                dropItem = CreateDynamicWonderDrug(boxtype, dropItem);
            }
            else
                dropItem = Envir.CreateDropItem(dropItem.Info);

            if (FreeSpace(Info.Inventory) < 1)
            {
                ReceiveChat(GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.NoMoreSpace), ChatType.System);
                return;
            }

            if (dropItem != null) GainItem(dropItem);
        }

        public void BlackstoneRewardItem()
        {
            int highRate = int.MaxValue;
            UserItem dropItem = null;
            foreach (DropInfo drop in Envir.BlackstoneDrops)
            {
                int rate = (int)(Envir.Random.Next(0, drop.Chance) / Settings.DropRate); if (rate < 1) rate = 1;

                if (highRate > rate)
                {
                    highRate = rate;
                    dropItem = Envir.CreateDropItem(drop.Item);
                }
            }
            if (FreeSpace(Info.Inventory) < 1)
            {
                ReceiveChat(GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.NoMoreSpace), ChatType.System);
                return;
            }
            if (dropItem != null) GainItem(dropItem);
        }

        private UserItem CreateDynamicWonderDrug(int boxtype, UserItem dropitem)
        {
            dropitem.CurrentDura = (ushort)1;//* 3600
            switch ((int)dropitem.Info.Effect)
            {
                case 0://exp low/med/high
                    dropitem.AddedStats[Stat.ExpRatePercent] = 5;
                    if (boxtype > 0) dropitem.AddedStats[Stat.ExpRatePercent] = 10;
                    if (boxtype > 1) dropitem.AddedStats[Stat.ExpRatePercent] = 20;
                    break;
                case 1://drop low/med/high
                    dropitem.AddedStats[Stat.ItemDropRatePercent] = 10;
                    if (boxtype > 0) dropitem.AddedStats[Stat.ItemDropRatePercent] = 20;
                    if (boxtype > 1) dropitem.AddedStats[Stat.ItemDropRatePercent] = 50;
                    break;
                case 2://hp low/med/high
                    dropitem.AddedStats[Stat.HP] = 50;
                    if (boxtype > 0) dropitem.AddedStats[Stat.HP] = 100;
                    if (boxtype > 1) dropitem.AddedStats[Stat.HP] = 200;
                    break;
                case 3://mp low/med/high
                    dropitem.AddedStats[Stat.MP] = 50;
                    if (boxtype > 0) dropitem.AddedStats[Stat.MP] = 100;
                    if (boxtype > 1) dropitem.AddedStats[Stat.MP] = 200;
                    break;
                case 4://ac low/med/high
                    dropitem.AddedStats[Stat.MaxAC] = 1;
                    if (boxtype > 0) dropitem.AddedStats[Stat.MaxAC] = 3;
                    if (boxtype > 1) dropitem.AddedStats[Stat.MaxAC] = 5;
                    break;
                case 5://amc low/med/high
                    dropitem.AddedStats[Stat.MaxMAC] = 1;
                    if (boxtype > 0) dropitem.AddedStats[Stat.MaxMAC] = 3;
                    if (boxtype > 1) dropitem.AddedStats[Stat.MaxMAC] = 5;
                    break;
                case 6://speed low/med/high
                    dropitem.AddedStats[Stat.AttackSpeed] = 2;
                    if (boxtype > 0) dropitem.AddedStats[Stat.AttackSpeed] = 3;
                    if (boxtype > 1) dropitem.AddedStats[Stat.AttackSpeed] = 4;
                    break;
            }

            return dropitem;
        }

        private IntelligentCreatureObject GetCreatureByName(string creatureName)
        {
            if (!CreatureSummoned || creatureName == "") return null;
            if (SummonedCreatureType == IntelligentCreatureType.None) return null;

            for (int i = 0; i < Pets.Count; i++)
            {
                if (Pets[i].Race != ObjectType.Creature) continue;

                var pet = (IntelligentCreatureObject)Pets[i];
                if (pet.PetType != SummonedCreatureType) continue;

                return (pet);
            }
            return null;
        }

        private void GetCreaturesInfo()
        {
            S.UpdateIntelligentCreatureList packet = new S.UpdateIntelligentCreatureList
            {
                CreatureSummoned = CreatureSummoned,
                SummonedCreatureType = SummonedCreatureType,
                PearlCount = Info.PearlCount,
            };

            for (int i = 0; i < Info.IntelligentCreatures.Count; i++)
                packet.CreatureList.Add(Info.IntelligentCreatures[i].CreateClientIntelligentCreature());

            Enqueue(packet);
        }


        #endregion
    }
}
