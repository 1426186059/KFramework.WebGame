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
        #region Awakening

        public void Awakening(ulong UniqueID, AwakeType type)
        {
            if (NPCPage == null || !String.Equals(NPCPage.Key, NPCScript.AwakeningKey, StringComparison.CurrentCultureIgnoreCase))
                return;

            if (type == AwakeType.None) return;

            for (int i = 0; i < Info.Inventory.Length; i++)
            {
                UserItem item = Info.Inventory[i];
                if (item == null || item.UniqueID != UniqueID) continue;

                Awake awake = item.Awake;

                if (item.Info.Bind.HasFlag(BindMode.DontUpgrade))
                {
                    Enqueue(new S.Awakening { result = -1, removeID = -1 });
                    return;
                }

                if (item.RentalInformation != null && item.RentalInformation.BindingFlags.HasFlag(BindMode.DontUpgrade))
                {
                    Enqueue(new S.Awakening { result = -1, removeID = -1 });
                    return;
                }

                if (!item.Info.CanAwakening)
                {
                    Enqueue(new S.Awakening { result = -1, removeID = -1 });
                    return;
                }

                if (awake.IsMaxLevel())
                {
                    Enqueue(new S.Awakening { result = -2, removeID = -1 });
                    return;
                }

                if (Info.Gold < item.AwakeningPrice())
                {
                    Enqueue(new S.Awakening { result = -3, removeID = -1 });
                    return;
                }

                if (HasAwakeningNeedMaterials(item, type))
                {
                    Info.Gold -= item.AwakeningPrice();
                    Enqueue(new S.LoseGold { Gold = item.AwakeningPrice() });

                    bool[] isHit;

                    switch (awake.UpgradeAwake(item, type, out isHit))
                    {
                        case -1:
                            Enqueue(new S.Awakening { result = -1, removeID = -1 });
                            break;
                        case 0:
                            AwakeningEffect(false, isHit);
                            Info.Inventory[i] = null;
                            Enqueue(new S.Awakening { result = 0, removeID = (long)item.UniqueID });
                            break;
                        case 1:
                            Enqueue(new S.RefreshItem { Item = item });
                            AwakeningEffect(true, isHit);
                            Enqueue(new S.Awakening { result = 1, removeID = -1 });
                            break;
                        default:
                            break;
                    }
                }
            }
        }

        public void DowngradeAwakening(ulong UniqueID)
        {
            if (NPCPage == null || !String.Equals(NPCPage.Key, NPCScript.DowngradeKey, StringComparison.CurrentCultureIgnoreCase))
                return;

            for (int i = 0; i < Info.Inventory.Length; i++)
            {
                UserItem item = Info.Inventory[i];
                if (item != null)
                {
                    if (item.UniqueID == UniqueID)
                    {
                        if (item.RentalInformation != null)
                        {
                            ReceiveChat(GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.UnableToDowngradeBelongsTo), item.FriendlyName, item.RentalInformation.OwnerName), ChatType.System);
                            return;
                        }

                        if (Info.Gold >= item.DowngradePrice())
                        {
                            Info.Gold -= item.DowngradePrice();
                            Enqueue(new S.LoseGold { Gold = item.DowngradePrice() });

                            Awake awake = item.Awake;
                            int result = awake.RemoveAwake();
                            switch (result)
                            {
                                case 0:
                                    ReceiveChat(GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.RemoveFailedLevel0), item.FriendlyName), ChatType.System);
                                    break;
                                case 1:
                                    ushort maxDura = (Envir.Random.Next(20) == 0) ? (ushort)(item.MaxDura - 1000) : item.MaxDura;
                                    if (maxDura < 1000) maxDura = 1000;

                                    Info.Inventory[i].CurrentDura = (Info.Inventory[i].CurrentDura >= maxDura) ? maxDura : Info.Inventory[i].CurrentDura;
                                    Info.Inventory[i].MaxDura = maxDura;
                                    ReceiveChat(GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.RemoveSuccessLevel), item.FriendlyName, item.Awake.GetAwakeLevel()), ChatType.System);
                                    Enqueue(new S.RefreshItem { Item = item });
                                    break;
                                default:
                                    break;
                            }
                        }
                    }
                }
            }
        }

        public void DisassembleItem(ulong UniqueID)
        {
            if (NPCPage == null || !String.Equals(NPCPage.Key, NPCScript.DisassembleKey, StringComparison.CurrentCultureIgnoreCase))
                return;

            for (int i = 0; i < Info.Inventory.Length; i++)
            {
                UserItem item = Info.Inventory[i];

                if (item == null || item.UniqueID != UniqueID)
                    continue;

                if (item.Info.Bind.HasFlag(BindMode.UnableToDisassemble))
                {
                    ReceiveChat(GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.UnableToDisassemble), item.FriendlyName), ChatType.System);
                    return;
                }

                if (item.RentalInformation != null && item.RentalInformation.BindingFlags.HasFlag(BindMode.UnableToDisassemble))
                {
                    ReceiveChat(GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.UnableToDisassembleBelongsTo), item.FriendlyName, item.RentalInformation.OwnerName), ChatType.System);
                    return;
                }

                if (Info.Gold >= item.DisassemblePrice())
                {
                    List<ItemInfoTable> dropList = new List<ItemInfoTable>();
                    foreach (DropInfo drop in Envir.AwakeningDrops)
                    {
                        if (drop.Item.Grade == item.Info.Grade - 1 ||
                            drop.Item.Grade == item.Info.Grade + 1)
                        {
                            if (Envir.Random.Next((drop.Chance <= 0) ? 1 : drop.Chance) == 0)
                            {
                                dropList.Add(drop.Item);
                            }
                        }

                        if (drop.Item.Grade == item.Info.Grade)
                        {
                            dropList.Add(drop.Item);
                        }
                    }

                    if (dropList.Count == 0) continue;

                    UserItem gainItem = Envir.CreateDropItem(dropList[Envir.Random.Next(dropList.Count)]);
                    if (gainItem == null) continue;
                    gainItem.Count = (ushort)Envir.Random.Next(Math.Min(ushort.MaxValue, (int)((((byte)item.Info.Grade * item.Info.RequiredAmount) / 10) + item.Quality())));
                    if (gainItem.Count < 1) gainItem.Count = 1;

                    GainItem(gainItem);

                    Enqueue(new S.LoseGold { Gold = item.DisassemblePrice() });
                    Info.Gold -= item.DisassemblePrice();

                    Enqueue(new S.DeleteItem { UniqueID = item.UniqueID, Count = item.Count });
                    Info.Inventory[i] = null;
                }
            }
        }

        public void ResetAddedItem(ulong UniqueID)
        {
            if (NPCPage == null || !String.Equals(NPCPage.Key, NPCScript.ResetKey, StringComparison.CurrentCultureIgnoreCase))
                return;

            for (int i = 0; i < Info.Inventory.Length; i++)
            {
                UserItem item = Info.Inventory[i];
                if (item != null)
                {
                    if (item.UniqueID == UniqueID)
                    {
                        if (item.RentalInformation != null)
                        {
                            ReceiveChat(GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.UnableToResetBelongsTo), item.FriendlyName, item.RentalInformation.OwnerName), ChatType.System);
                            return;
                        }

                        if (Info.Gold >= item.ResetPrice())
                        {
                            Info.Gold -= item.ResetPrice();
                            Enqueue(new S.LoseGold { Gold = item.ResetPrice() });

                            UserItem newItem = new UserItem(item.Info);

                            ushort maxDura = (Envir.Random.Next(20) == 0) ? (ushort)(item.MaxDura - 1000) : item.MaxDura;
                            if (maxDura < 1000) maxDura = 1000;

                            newItem.UniqueID = item.UniqueID;
                            newItem.ItemIndex = item.ItemIndex;
                            newItem.CurrentDura = (item.CurrentDura >= maxDura) ? maxDura : item.CurrentDura;
                            newItem.MaxDura = maxDura;
                            newItem.Count = item.Count;
                            newItem.Slots = item.Slots;
                            newItem.Awake = item.Awake;
                            newItem.ExpireInfo = item.ExpireInfo;
                            newItem.SealedInfo = item.SealedInfo;

                            Info.Inventory[i] = newItem;

                            Enqueue(new S.RefreshItem { Item = Info.Inventory[i] });
                        }
                    }
                }
            }
        }

        public void AwakeningNeedMaterials(ulong UniqueID, AwakeType type)
        {
            if (type == AwakeType.None) return;

            if (Awake.AwakeMaterials.Count < (int)type)
            {
                return;
            }

            foreach (UserItem item in Info.Inventory)
            {
                if (item != null)
                {
                    if (item.UniqueID == UniqueID)
                    {
                        Awake awake = item.Awake;

                        byte[] materialCount = new byte[2];
                        int idx = 0;

                        if (Awake.AwakeMaterialRate.Length < (int)item.Info.Grade)
                        {
                            continue;
                        }

                        foreach (List<byte> material in Awake.AwakeMaterials[(int)type - 1])
                        {
                            byte materialRate = (byte)(Awake.AwakeMaterialRate[(int)item.Info.Grade - 1] * (float)awake.GetAwakeLevel());
                            materialCount[idx] = material[(int)item.Info.Grade - 1];
                            materialCount[idx] += materialRate;
                            idx++;
                        }

                        ItemInfoTable[] materials = new ItemInfoTable[2];

                        foreach (ItemInfoTable info in Envir.ItemInfoList)
                        {
                            if (item.Info.Grade == info.Grade &&
                                info.Type == ItemType.Awakening)
                            {
                                if (info.Shape == (short)type - 1)
                                {
                                    materials[0] = info;
                                }
                                else if (info.Shape == 100)
                                {
                                    materials[1] = info;
                                }
                            }
                        }

                        Enqueue(new S.AwakeningNeedMaterials { Materials = materials, MaterialsCount = materialCount });
                        break;
                    }
                }
            }
        }

        public void AwakeningEffect(bool isSuccess, bool[] isHit)
        {
            for (int i = 0; i < 5; i++)
            {
                Enqueue(new S.ObjectEffect { ObjectID = ObjectID, Effect = isHit[i] ? SpellEffect.AwakeningHit : SpellEffect.AwakeningMiss, EffectType = 0, DelayTime = (uint)(i * 500) });
                Broadcast(new S.ObjectEffect { ObjectID = ObjectID, Effect = isHit[i] ? SpellEffect.AwakeningHit : SpellEffect.AwakeningMiss, EffectType = 0, DelayTime = (uint)(i * 500) });
            }

            Enqueue(new S.ObjectEffect { ObjectID = ObjectID, Effect = isSuccess ? SpellEffect.AwakeningSuccess : SpellEffect.AwakeningFail, EffectType = 0, DelayTime = 2500 });
            Broadcast(new S.ObjectEffect { ObjectID = ObjectID, Effect = isSuccess ? SpellEffect.AwakeningSuccess : SpellEffect.AwakeningFail, EffectType = 0, DelayTime = 2500 });
        }

        public bool HasAwakeningNeedMaterials(UserItem item, AwakeType type)
        {
            Awake awake = item.Awake;

            byte[] materialCount = new byte[2];

            int idx = 0;
            foreach (List<byte> material in Awake.AwakeMaterials[(int)type - 1])
            {
                byte materialRate = (byte)(Awake.AwakeMaterialRate[(int)item.Info.Grade - 1] * (float)awake.GetAwakeLevel());
                materialCount[idx] = material[(int)item.Info.Grade - 1];
                materialCount[idx] += materialRate;
                idx++;
            }

            byte[] currentCount = new byte[2] { 0, 0 };

            for (int i = 0; i < Info.Inventory.Length; i++)
            {
                UserItem materialItem = Info.Inventory[i];
                if (materialItem != null)
                {
                    if (materialItem.Info.Grade == item.Info.Grade &&
                        materialItem.Info.Type == ItemType.Awakening)
                    {
                        if (materialItem.Info.Shape == ((int)type - 1) &&
                            materialCount[0] - currentCount[0] != 0)
                        {
                            if (materialItem.Count <= materialCount[0] - currentCount[0])
                            {
                                currentCount[0] += (byte)materialItem.Count;
                            }
                            else if (materialItem.Count > materialCount[0] - currentCount[0])
                            {
                                currentCount[0] = (byte)(materialCount[0] - currentCount[0]);
                            }
                        }
                        else if (materialItem.Info.Shape == 100 &&
                            materialCount[1] - currentCount[1] != 0)
                        {
                            if (materialItem.Count <= materialCount[1] - currentCount[1])
                            {
                                currentCount[1] += (byte)materialItem.Count;
                            }
                            else if (materialItem.Count > materialCount[1] - currentCount[1])
                            {
                                currentCount[1] = (byte)(materialCount[1] - currentCount[1]);
                            }
                        }
                    }
                }
            }

            for (int i = 0; i < materialCount.Length; i++)
            {
                if (materialCount[i] != currentCount[i])
                {
                    Enqueue(new S.Awakening { result = -4, removeID = -1 });
                    return false;
                }
            }

            for (int i = 0; i < Info.Inventory.Length; i++)
            {
                if (Info.Inventory[i] != null)
                {
                    if (Info.Inventory[i].Info.Grade == item.Info.Grade &&
                        Info.Inventory[i].Info.Type == ItemType.Awakening)
                    {
                        if (Info.Inventory[i].Info.Shape == ((int)type - 1) &&
                            currentCount[0] > 0)
                        {
                            if (Info.Inventory[i].Count <= currentCount[0])
                            {
                                Enqueue(new S.DeleteItem { UniqueID = Info.Inventory[i].UniqueID, Count = Info.Inventory[i].Count });
                                currentCount[0] -= (byte)Info.Inventory[i].Count;
                                Info.Inventory[i] = null;
                            }
                            else if (Info.Inventory[i].Count > currentCount[0])
                            {
                                Enqueue(new S.DeleteItem { UniqueID = Info.Inventory[i].UniqueID, Count = currentCount[0] });
                                Info.Inventory[i].Count -= currentCount[0];
                                currentCount[0] = 0;
                            }
                        }
                        else if (Info.Inventory[i].Info.Shape == 100 &&
                            currentCount[1] > 0)
                        {
                            if (Info.Inventory[i].Count <= currentCount[1])
                            {
                                Enqueue(new S.DeleteItem { UniqueID = Info.Inventory[i].UniqueID, Count = Info.Inventory[i].Count });
                                currentCount[1] -= (byte)Info.Inventory[i].Count;
                                Info.Inventory[i] = null;
                            }
                            else if (Info.Inventory[i].Count > currentCount[1])
                            {
                                Enqueue(new S.DeleteItem { UniqueID = Info.Inventory[i].UniqueID, Count = currentCount[1] });
                                Info.Inventory[i].Count -= currentCount[1];
                                currentCount[1] = 0;
                            }
                        }
                    }
                }
            }
            return true;
        }

        #endregion

        #region Groups

        public void SwitchGroup(bool allow)
        {
            Enqueue(new S.SwitchGroup { AllowGroup = allow });

            if (AllowGroup == allow) return;
            AllowGroup = allow;

            // If we just disabled grouping and we're in a party, leave immediately
            if (!AllowGroup && GroupMembers != null)
                LeaveGroup();
        }

        public void LeaveGroup()
        {
            if (GroupMembers == null) return;

            // Take a snapshot BEFORE modifying
            var oldGroup = GroupMembers.ToList();

            // Remove self from the group list held by remaining members
            GroupMembers.Remove(this);

            // Notify remaining members
            if (GroupMembers.Count > 1)
            {
                Packet p = new S.DeleteMember { Name = Name };
                for (int i = 0; i < GroupMembers.Count; i++)
                    GroupMembers[i]?.Enqueue(p);
            }
            else if (GroupMembers.Count == 1)
            {
                // Last member loses the group UI
                GroupMembers[0].Enqueue(new S.DeleteGroup());
                GroupMembers[0].GroupMembers = null;
            }

            // Clear self
            GroupMembers = null;
            Enqueue(new S.DeleteGroup());

            // --- Immediate RG enforcement ---
            // Self first
            if (!IsGM && CurrentMap?.Info?.RequiredGroup == true)
                CheckGroupValidityOnMap(); // immediate (no grace)

            // Then all other members that were in the party
            for (int i = 0; i < oldGroup.Count; i++)
            {
                var m = oldGroup[i];
                if (m == null || m == this || m.IsGM) continue;

                if (m.CurrentMap?.Info?.RequiredGroup == true)
                    m.CheckGroupValidityOnMap(); // immediate (no grace)
            }
        }

        public void AddMember(string name)
        {
            if (Envir.Time < NextGroupInviteTime) return;
            NextGroupInviteTime = Envir.Time + Settings.GroupInviteDelay;

            if (GroupMembers != null && GroupMembers[0] != this)
            {
                ReceiveChat(GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.NotGroupLeader), ChatType.System);
                return;
            }

            if (GroupMembers != null && GroupMembers.Count >= Globals.MaxGroup)
            {
                ReceiveChat(GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.YouGroupMaxMembers), ChatType.System);
                return;
            }

            // New: leader cannot invite on NoGroup maps
            if (CurrentMap != null && CurrentMap.Info.NoGroup)
            {
                ReceiveChat(GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.YouCannotInviteOnSoloMaps), ChatType.System);
                return;
            }

            PlayerObject player = Envir.GetPlayer(name);

            if (player == null)
            {
                ReceiveChat(GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.CouldNotBeFound), name), ChatType.System);
                return;
            }
            if (player == this)
            {
                ReceiveChat(GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.CannotGroupSelf), ChatType.System);
                return;
            }

            if (!player.AllowGroup)
            {
                ReceiveChat(GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.NotAllowGroup), name), ChatType.System);
                return;
            }

            if (player.GroupMembers != null)
            {
                ReceiveChat(GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.AlreadyInAnotherGroup), name), ChatType.System);
                return;
            }

            if (player.GroupInvitation != null)
            {
                ReceiveChat(GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.AlreadyReceivingInviteFromOtherPlayer), name), ChatType.System);
                return;
            }

            if (player.CurrentMap != null && player.CurrentMap.Info.NoGroup)
            {
                ReceiveChat(GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.SoloMapTargetCannotAccept, player.Name), ChatType.System);
                player.ReceiveChat(GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.SoloMapCannotAccept), ChatType.System);
                return;
            }

            SwitchGroup(true);
            player.Enqueue(new S.GroupInvite { Name = Name });
            player.GroupInvitation = this;
        }

        public void DelMember(string name)
        {
            if (GroupMembers == null)
            {
                ReceiveChat(GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.NotInGroup), ChatType.System);
                return;
            }
            if (GroupMembers[0] != this)
            {
                ReceiveChat(GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.NotGroupLeader), ChatType.System);
                return;
            }

            PlayerObject player = null;

            for (int i = 0; i < GroupMembers.Count; i++)
            {
                if (string.Compare(GroupMembers[i].Name, name, StringComparison.OrdinalIgnoreCase) != 0) continue;
                player = GroupMembers[i];
                break;
            }

            if (player == null)
            {
                ReceiveChat(GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.NotInYourGroup), name), ChatType.System);
                return;
            }

            player.Enqueue(new S.DeleteGroup());
            player.LeaveGroup(); // will enforce RG for self & others
        }

        public void GroupInvite(bool accept)
        {
            if (GroupInvitation == null)
            {
                ReceiveChat(GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.NotInvitedToGroup), ChatType.System);
                return;
            }

            if (!accept)
            {
                GroupInvitation.ReceiveChat(GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.DeclinedGroupInvite), Name), ChatType.System);
                GroupInvitation = null;
                return;
            }

            if (GroupMembers != null)
            {
                ReceiveChat(GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.CannotJoinGroup), GroupInvitation.Name), ChatType.System);
                GroupInvitation = null;
                return;
            }

            if (GroupInvitation.GroupMembers != null && GroupInvitation.GroupMembers[0] != GroupInvitation)
            {
                ReceiveChat(GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.NoLongerGroupLeader), GroupInvitation.Name), ChatType.System);
                GroupInvitation = null;
                return;
            }

            if (GroupInvitation.GroupMembers != null && GroupInvitation.GroupMembers.Count >= Globals.MaxGroup)
            {
                ReceiveChat(GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.GroupMaxMembers), GroupInvitation.Name), ChatType.System);
                GroupInvitation = null;
                return;
            }
            if (!GroupInvitation.AllowGroup)
            {
                ReceiveChat(GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.NotInAllowGroup), GroupInvitation.Name), ChatType.System);
                GroupInvitation = null;
                return;
            }
            if (GroupInvitation.Node == null)
            {
                ReceiveChat(GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.PlayerNoLongerOnline), GroupInvitation.Name), ChatType.System);
                GroupInvitation = null;
                return;
            }

            if (GroupInvitation.CurrentMap != null && GroupInvitation.CurrentMap.Info.NoGroup)
            {
                ReceiveChat(GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.SoloMapTargetCannotAccept, GroupInvitation.Name), ChatType.System);
                GroupInvitation.ReceiveChat(GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.SoloMapCannotAccept), ChatType.System);
                GroupInvitation = null;
                return;
            }

            if (GroupInvitation.GroupMembers == null)
            {
                GroupInvitation.GroupMembers = new List<PlayerObject> { GroupInvitation };
                GroupInvitation.Enqueue(new S.AddMember { Name = GroupInvitation.Name });
                GroupInvitation.Enqueue(new S.GroupMembersMap { PlayerName = GroupInvitation.Name, PlayerMap = GroupInvitation.CurrentMap.Info.Title });
                GroupInvitation.Enqueue(new S.SendMemberLocation { MemberName = GroupInvitation.Name, MemberLocation = GroupInvitation.CurrentLocation });
            }

            Packet p = new S.AddMember { Name = Name };
            GroupMembers = GroupInvitation.GroupMembers;
            GroupInvitation = null;

            for (int i = 0; i < GroupMembers.Count; i++)
            {
                PlayerObject member = GroupMembers[i];

                member.Enqueue(p);
                Enqueue(new S.AddMember { Name = member.Name });

                if (CurrentMap == member.CurrentMap && Functions.InRange(CurrentLocation, member.CurrentLocation, Globals.DataRange))
                {
                    byte time = Math.Min(byte.MaxValue, (byte)Math.Max(5, (RevTime - Envir.Time) / 1000));

                    member.Enqueue(new S.ObjectHealth { ObjectID = ObjectID, Percent = PercentHealth, Expire = time, HP = Health, MaxHP = MaxHealth });
                    Enqueue(new S.ObjectHealth { ObjectID = member.ObjectID, Percent = member.PercentHealth, Expire = time, HP = member.Health, MaxHP = member.MaxHealth });

                    if (Hero != null)
                        member.Enqueue(new S.ObjectHealth { ObjectID = Hero.ObjectID, Percent = Hero.PercentHealth, Expire = time, HP = Hero.Health, MaxHP = Hero.MaxHealth });

                    if (member.Hero != null)
                        Enqueue(new S.ObjectHealth { ObjectID = member.Hero.ObjectID, Percent = member.Hero.PercentHealth, Expire = time, HP = member.Hero.Health, MaxHP = member.Hero.MaxHealth });

                    for (int j = 0; j < member.Pets.Count; j++)
                    {
                        MonsterObject pet = member.Pets[j];
                        Enqueue(new S.ObjectHealth { ObjectID = pet.ObjectID, Percent = pet.PercentHealth, Expire = time, HP = pet.Health, MaxHP = pet.MaxHealth });
                    }
                }
            }

            GroupMembers.Add(this);

            for (int j = 0; j < Pets.Count; j++)
                Pets[j].BroadcastHealthChange();

            Enqueue(p);
            GroupMemberMapNameChanged();
            GetPlayerLocation();

            // --- Immediate RG sanity after join (both sides) ---
            for (int k = 0; k < GroupMembers.Count; k++)
            {
                var member = GroupMembers[k];
                if (member == null || member.IsGM) continue;
                if (member.CurrentMap?.Info?.RequiredGroup == true)
                    member.CheckGroupValidityOnMap(); // immediate; will return if still under-size on RG
            }
        }

        public void GroupMemberMapNameChanged()
        {
            if (GroupMembers == null) return;

            for (int i = 0; i < GroupMembers.Count; i++)
            {
                PlayerObject member = GroupMembers[i];
                member.Enqueue(new S.GroupMembersMap { PlayerName = Name, PlayerMap = CurrentMap.Info.Title });
                Enqueue(new S.GroupMembersMap { PlayerName = member.Name, PlayerMap = member.CurrentMap.Info.Title });
            }
            Enqueue(new S.GroupMembersMap { PlayerName = Name, PlayerMap = CurrentMap.Info.Title });
        }

        private void DisbandGroup(string reason = null)
        {
            var group = GroupMembers;
            if (group == null || group.Count == 0) return;

            // Snapshot so we can enforce after clearing
            var snapshot = group.ToList();

            // Break links first
            foreach (var member in snapshot)
            {
                if (member == null) continue;
                member.GroupMembers = null;
            }

            // Notify clients
            foreach (var member in snapshot)
            {
                if (member == null) continue;
                member.Enqueue(new S.DeleteGroup());
                if (!string.IsNullOrEmpty(reason))
                    member.ReceiveChat(reason, ChatType.System);
            }

            // --- Immediate RG enforcement for all affected players on RG maps ---
            for (int i = 0; i < snapshot.Count; i++)
            {
                var m = snapshot[i];
                if (m == null || m.IsGM) continue;

                if (m.CurrentMap?.Info?.RequiredGroup == true)
                    m.CheckGroupValidityOnMap(); // immediate (no grace)
            }
        }
        private static bool MapHasGroupRequirement(Map map)
        {
            if (map?.Info == null) return false;
            // treat as restricted only if at least 2 are required
            return map.Info.RequiredGroup && map.Info.RequiredGroupSize >= 2;
        }

        private bool IsValidForGroupRequiredMap()
        {
            var info = CurrentMap?.Info;
            if (info == null) return true;
            if (!(info.RequiredGroup && info.RequiredGroupSize > 1)) return true;

            int requiredSize = Math.Max(2, info.RequiredGroupSize);
            int have = GroupMembers?.Count ?? 0;
            return have >= requiredSize;
        }

        private void ForceLeaveGroupRequiredMap()
        {
            if (IsGM || Node == null) return;

            Map targetMap = LastValidMap ?? Envir.GetMap(BindMapIndex);
            Point targetLocation = LastValidLocation != Point.Empty ? LastValidLocation : BindLocation;

            // Fallbacks to ensure non-RG and valid point
            if (targetMap == null || targetMap.Info == null || targetMap.Info.RequiredGroup)
            {
                SetBind(); // ensures a sane, non-RG bind for your server
                targetMap = Envir.GetMap(BindMapIndex);
                targetLocation = BindLocation;
            }
            if (targetMap == null || !targetMap.ValidPoint(targetLocation))
                targetLocation = new Point(targetMap.Width / 2, targetMap.Height / 2);

            Teleport(targetMap, targetLocation);
            ReceiveChat(GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.YouNoLongerMeetGroupRequirementsForMap), ChatType.System);
        }

        public void CheckGroupValidityOnMap()
        {
            if (IsGM) return;
            if (Node == null || CurrentMap == null) return;

            if (!CurrentMap.Info.RequiredGroup) return;

            int requiredSize = Math.Max(2, CurrentMap.Info.RequiredGroupSize);
            int have = GroupMembers?.Count ?? 0;

            if (have < requiredSize)
                ForceLeaveGroupRequiredMap();
        }

        #endregion
    }
}
