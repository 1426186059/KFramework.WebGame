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
        #region Relationship

        public void NPCDivorce()
        {
            if (Info.Married == 0)
            {
                ReceiveChat(LanguageMgr.Get("YouNotMarried"), ChatType.System);
                return;
            }

            CharacterInfo lover = Envir.GetCharacterInfo(Info.Married);
            PlayerObject player = Envir.GetPlayer(lover.Name);

            Info.Married = 0;
            Info.MarriedDate = Envir.Now;

            if (Info.Equipment[(int)EquipmentSlot.RingL] != null)
            {
                Info.Equipment[(int)EquipmentSlot.RingL].WeddingRing = -1;
                Enqueue(new S.RefreshItem { Item = Info.Equipment[(int)EquipmentSlot.RingL] });
            }

            GetRelationship(false);

            lover.Married = 0;
            lover.MarriedDate = Envir.Now;
            if (lover.Equipment[(int)EquipmentSlot.RingL] != null)
                lover.Equipment[(int)EquipmentSlot.RingL].WeddingRing = -1;

            if (player != null)
            {
                player.GetRelationship(false);
                player.ReceiveChat(LanguageMgr.Get("ForcefullyDivorced"), ChatType.System);
                if (player.Info.Equipment[(int)EquipmentSlot.RingL] != null)
                    player.Enqueue(new S.RefreshItem { Item = player.Info.Equipment[(int)EquipmentSlot.RingL] });
            }
        }

        public bool CheckMakeWeddingRing()
        {
            if (Info.Married == 0)
            {
                ReceiveChat(LanguageMgr.Get("NeedMarriedForWeddingRing"), ChatType.System);
                return false;
            }

            if (Info.Equipment[(int)EquipmentSlot.RingL] == null)
            {
                ReceiveChat(LanguageMgr.Get("NeedRingOnLeftFinger"), ChatType.System);
                return false;
            }

            if (Info.Equipment[(int)EquipmentSlot.RingL].WeddingRing != -1)
            {
                ReceiveChat(LanguageMgr.Get("AlreadyWearingWeddingRing"), ChatType.System);
                return false;
            }

            if (Info.Equipment[(int)EquipmentSlot.RingL].Info.Bind.HasFlag(BindMode.NoWeddingRing))
            {
                ReceiveChat(LanguageMgr.Get("CannotUseRingType"), ChatType.System);
                return false;
            }

            return true;
        }

        public void MakeWeddingRing()
        {
            if (CheckMakeWeddingRing())
            {
                Info.Equipment[(int)EquipmentSlot.RingL].WeddingRing = Info.Married;
                Enqueue(new S.RefreshItem { Item = Info.Equipment[(int)EquipmentSlot.RingL] });
            }
        }

        public void ReplaceWeddingRing(ulong uniqueID)
        {
            if (Dead) return;

            if (NPCPage == null || (!String.Equals(NPCPage.Key, NPCScript.ReplaceWedRingKey, StringComparison.CurrentCultureIgnoreCase))) return;

            UserItem temp = null;
            UserItem CurrentRing = Info.Equipment[(int)EquipmentSlot.RingL];

            if (CurrentRing == null)
            {
                ReceiveChat(LanguageMgr.Get("NotWearingUpgradeRing"), ChatType.System);
                return;
            }

            if (CurrentRing.WeddingRing == -1)
            {
                ReceiveChat(LanguageMgr.Get("NotWearingWeddingRingUpgrade"), ChatType.System);
                return;
            }

            int index = -1;

            for (int i = 0; i < Info.Inventory.Length; i++)
            {
                temp = Info.Inventory[i];
                if (temp == null || temp.UniqueID != uniqueID) continue;
                index = i;
                break;
            }

            if (index == -1) return;

            temp = Info.Inventory[index];


            if (temp.Info.Type != ItemType.Ring)
            {
                ReceiveChat(LanguageMgr.Get("CannotReplaceWeddingRing"), ChatType.System);
                return;
            }

            if (!CanEquipItem(temp, (int)EquipmentSlot.RingL))
            {
                ReceiveChat(LanguageMgr.Get("CannotEquipItemInUse"), ChatType.System);
                return;
            }

            if (temp.Info.Bind.HasFlag(BindMode.NoWeddingRing))
            {
                ReceiveChat(LanguageMgr.Get("CannotUseRingType"), ChatType.System);
                return;
            }

            uint cost = (uint)((Info.Inventory[index].Info.RequiredAmount * 10) * Settings.ReplaceWedRingCost);

            if (cost > Info.Gold)
            {
                ReceiveChat(LanguageMgr.Get("NotEnoughGoldForWeddingRing"), ChatType.System);
                return;
            }

            Info.Gold -= cost;
            Enqueue(new S.LoseGold { Gold = cost });


            temp.WeddingRing = Info.Married;
            CurrentRing.WeddingRing = -1;

            Info.Equipment[(int)EquipmentSlot.RingL] = temp;
            Info.Inventory[index] = CurrentRing;

            Enqueue(new S.EquipItem { Grid = MirGridType.Inventory, UniqueID = temp.UniqueID, To = (int)EquipmentSlot.RingL, Success = true });

            Enqueue(new S.RefreshItem { Item = Info.Inventory[index] });
            Enqueue(new S.RefreshItem { Item = Info.Equipment[(int)EquipmentSlot.RingL] });

        }

        public void MarriageRequest()
        {

            if (Info.Married != 0)
            {
                ReceiveChat(LanguageMgr.Get("YouAlreadyMarried"), ChatType.System);
                return;
            }

            if (Info.MarriedDate.AddDays(Settings.MarriageCooldown) > Envir.Now)
            {
                ReceiveChat(LanguageMgr.Get("MarriageCooldownAfterDivorce", Settings.MarriageCooldown), ChatType.System);
                return;
            }

            if (Info.Level < Settings.MarriageLevelRequired)
            {
                ReceiveChat(LanguageMgr.Get("NeedLevelToMarry", Settings.MarriageLevelRequired), ChatType.System);
                return;
            }

            Point target = Functions.PointMove(CurrentLocation, Direction, 1);

            if (!CurrentMap.ValidPoint(target)) return;
            Cell cell = CurrentMap.GetCell(target);
            PlayerObject player = null;

            if (cell.Objects == null || cell.Objects.Count < 1) return;

            for (int i = 0; i < cell.Objects.Count; i++)
            {
                MapObject ob = cell.Objects[i];
                if (ob.Race != ObjectType.Player) continue;

                player = Envir.GetPlayer(ob.Name);
            }



            if (player != null)
            {


                if (!Functions.FacingEachOther(Direction, CurrentLocation, player.Direction, player.CurrentLocation))
                {
                    ReceiveChat(LanguageMgr.Get("NeedFaceEachOtherForMarriage"), ChatType.System);
                    return;
                }

                if (player.Level < Settings.MarriageLevelRequired)
                {
                    ReceiveChat(LanguageMgr.Get("LoverMinLevelMarriage", Settings.MarriageLevelRequired), ChatType.System);
                    return;
                }

                if (player.Info.MarriedDate.AddDays(Settings.MarriageCooldown) > Envir.Now)
                {
                    ReceiveChat(LanguageMgr.Get("MarriageCooldownAfterDivorceOther", player.Name, Settings.MarriageCooldown), ChatType.System);
                    return;
                }

                if (!player.AllowMarriage)
                {
                    ReceiveChat(LanguageMgr.Get("ProposalNotAllowed"), ChatType.System);
                    return;
                }

                if (player == this)
                {
                    ReceiveChat(LanguageMgr.Get("CantMarryYourself"), ChatType.System);
                    return;
                }

                if (player.Dead || Dead)
                {
                    ReceiveChat(LanguageMgr.Get("CannotMarryDeadPlayer"), ChatType.System);
                    return;
                }

                if (player.MarriageProposal != null)
                {
                    ReceiveChat(LanguageMgr.Get("AlreadyHasMarriageInvitation", player.Info.Name), ChatType.System);
                    return;
                }

                if (!Functions.InRange(player.CurrentLocation, CurrentLocation, Globals.DataRange) || player.CurrentMap != CurrentMap)
                {
                    ReceiveChat(LanguageMgr.Get("NotWithinMarriageRange", player.Info.Name), ChatType.System);
                    return;
                }

                if (player.Info.Married != 0)
                {
                    ReceiveChat(LanguageMgr.Get("PlayerAlreadyMarried", player.Info.Name), ChatType.System);
                    return;
                }

                player.MarriageProposal = this;
                player.Enqueue(new S.MarriageRequest { Name = Info.Name });
            }
            else
            {
                ReceiveChat(LanguageMgr.Get("FacePlayerForMarriageRequest"), ChatType.System);
                return;
            }
        }

        public void MarriageReply(bool accept)
        {
            if (MarriageProposal == null || MarriageProposal.Info == null)
            {
                MarriageProposal = null;
                return;
            }

            if (!accept)
            {
                MarriageProposal.ReceiveChat(LanguageMgr.Get("HasRefusedToMarryYou", Info.Name), ChatType.System);
                MarriageProposal = null;
                return;
            }

            if (Info.Married != 0)
            {
                ReceiveChat(LanguageMgr.Get("YouAlreadyMarried"), ChatType.System);
                MarriageProposal = null;
                return;
            }

            if (MarriageProposal.Info.Married != 0)
            {
                ReceiveChat(LanguageMgr.Get("PlayerAlreadyMarried", MarriageProposal.Info.Name), ChatType.System);
                MarriageProposal = null;
                return;
            }


            MarriageProposal.Info.Married = Info.Index;
            MarriageProposal.Info.MarriedDate = Envir.Now;

            Info.Married = MarriageProposal.Info.Index;
            Info.MarriedDate = Envir.Now;

            GetRelationship(false);
            MarriageProposal.GetRelationship(false);

            MarriageProposal.ReceiveChat(LanguageMgr.Get("CongratulationsMarriedTo", Info.Name), ChatType.System);
            ReceiveChat(LanguageMgr.Get("CongratulationsMarriedTo", MarriageProposal.Info.Name), ChatType.System);

            MarriageProposal = null;
        }

        public void DivorceRequest()
        {

            if (Info.Married == 0)
            {
                ReceiveChat(LanguageMgr.Get("YouNotMarried"), ChatType.System);
                return;
            }


            Point target = Functions.PointMove(CurrentLocation, Direction, 1);
            if (!CurrentMap.ValidPoint(target)) return;
            Cell cell = CurrentMap.GetCell(target);
            PlayerObject player = null;

            if (cell.Objects == null || cell.Objects.Count < 1) return;

            for (int i = 0; i < cell.Objects.Count; i++)
            {
                MapObject ob = cell.Objects[i];
                if (ob.Race != ObjectType.Player) continue;

                player = Envir.GetPlayer(ob.Name);
            }

            if (player == null)
            {
                ReceiveChat(LanguageMgr.Get("FaceLoverToDivorce"), ChatType.System);
                return;
            }

            if (player != null)
            {
                if (!Functions.FacingEachOther(Direction, CurrentLocation, player.Direction, player.CurrentLocation))
                {
                    ReceiveChat(LanguageMgr.Get("FaceLoverToDivorce"), ChatType.System);
                    return;
                }

                if (player == this)
                {
                    ReceiveChat(LanguageMgr.Get("CantDivorceSelf"), ChatType.System);
                    return;
                }

                if (player.Dead || Dead)
                {
                    ReceiveChat(LanguageMgr.Get("CantDivorceDeadPlayer"), ChatType.System); //GOT TO HERE, NEED TO KEEP WORKING ON IT.
                    return;
                }

                if (player.Info.Index != Info.Married)
                {
                    ReceiveChat(LanguageMgr.Get("YouNotMarriedTo", player.Info.Name), ChatType.System);
                    return;
                }

                if (!Functions.InRange(player.CurrentLocation, CurrentLocation, Globals.DataRange) || player.CurrentMap != CurrentMap)
                {
                    ReceiveChat(LanguageMgr.Get("NotInDivorceRange", player.Info.Name), ChatType.System);
                    return;
                }

                player.DivorceProposal = this;
                player.Enqueue(new S.DivorceRequest { Name = Info.Name });
            }
            else
            {
                ReceiveChat(LanguageMgr.Get("FaceLoverToDivorce"), ChatType.System);
                return;
            }
        }

        public void DivorceReply(bool accept)
        {
            if (DivorceProposal == null || DivorceProposal.Info == null)
            {
                DivorceProposal = null;
                return;
            }

            if (!accept)
            {
                DivorceProposal.ReceiveChat(LanguageMgr.Get("HasRefusedDivorceYou", Info.Name), ChatType.System);
                DivorceProposal = null;
                return;
            }

            if (Info.Married == 0)
            {
                ReceiveChat(LanguageMgr.Get("YouNotRequireDivorce"), ChatType.System);
                DivorceProposal = null;
                return;
            }

            DivorceProposal.Info.Married = 0;
            DivorceProposal.Info.MarriedDate = Envir.Now;
            if (DivorceProposal.Info.Equipment[(int)EquipmentSlot.RingL] != null)
            {
                DivorceProposal.Info.Equipment[(int)EquipmentSlot.RingL].WeddingRing = -1;
                DivorceProposal.Enqueue(new S.RefreshItem { Item = DivorceProposal.Info.Equipment[(int)EquipmentSlot.RingL] });
            }

            Info.Married = 0;
            Info.MarriedDate = Envir.Now;
            if (Info.Equipment[(int)EquipmentSlot.RingL] != null)
            {
                Info.Equipment[(int)EquipmentSlot.RingL].WeddingRing = -1;
                Enqueue(new S.RefreshItem { Item = Info.Equipment[(int)EquipmentSlot.RingL] });
            }

            DivorceProposal.ReceiveChat(LanguageMgr.Get("YouAreDivorced", Info.Name), ChatType.System);
            ReceiveChat(LanguageMgr.Get("YouAreDivorced"), ChatType.System);

            GetRelationship(false);
            DivorceProposal.GetRelationship(false);
            DivorceProposal = null;
        }

        public void GetRelationship(bool CheckOnline = true)
        {
            if (Info.Married == 0)
            {
                Enqueue(new S.LoverUpdate { Name = "", Date = Info.MarriedDate, MapName = "", MarriedDays = 0 });
            }
            else
            {
                CharacterInfo Lover = Envir.GetCharacterInfo(Info.Married);

                PlayerObject player = Envir.GetPlayer(Lover.Name);

                if (player == null)
                    Enqueue(new S.LoverUpdate { Name = Lover.Name, Date = Info.MarriedDate, MapName = "", MarriedDays = (short)(Envir.Now - Info.MarriedDate).TotalDays });
                else
                {
                    Enqueue(new S.LoverUpdate { Name = Lover.Name, Date = Info.MarriedDate, MapName = player.CurrentMap.Info.Title, MarriedDays = (short)(Envir.Now - Info.MarriedDate).TotalDays });
                    if (CheckOnline)
                    {
                        player.GetRelationship(false);
                        player.ReceiveChat(LanguageMgr.Get("PlayerHasComeOnline", Info.Name), ChatType.System);
                    }
                }
            }
        }
        public void LogoutRelationship()
        {
            if (Info.Married == 0) return;
            CharacterInfo lover = Envir.GetCharacterInfo(Info.Married);

            if (lover == null)
            {
                MessageQueue.EnqueueDebugging(LanguageMgr.Get("PlayerMarriedIdNotFound", Name, Info.Married));
                return;
            }

            PlayerObject player = Envir.GetPlayer(lover.Name);
            if (player != null)
            {
                player.Enqueue(new S.LoverUpdate { Name = Info.Name, Date = player.Info.MarriedDate, MapName = "", MarriedDays = (short)(Envir.Now - Info.MarriedDate).TotalDays });
                player.ReceiveChat(LanguageMgr.Get("PlayerHasGoneOffline", Info.Name), ChatType.System);
            }
        }

        #endregion

        #region Mentorship

        public void MentorBreak(bool force = false)
        {
            if (Info.Mentor == 0)
            {
                ReceiveChat(LanguageMgr.Get("NoMentorship"), ChatType.System);
                return;
            }

            CharacterInfo partner = Envir.GetCharacterInfo(Info.Mentor);
            PlayerObject partnerP = Envir.GetPlayer(partner.Name);

            if (force)
            {
                Info.MentorDate = Envir.Now.AddDays(Settings.MentorLength);
                ReceiveChat(LanguageMgr.Get("YouHaveMentorshipCooldown", Settings.MentorLength), ChatType.System);
            }
            else
            {
                ReceiveChat(LanguageMgr.Get("MentorshipExpired"), ChatType.System);
            }

            if (Info.IsMentor)
            {
                if (partnerP != null)
                {
                    Info.MentorExp += partnerP.MenteeEXP;
                    partnerP.MenteeEXP = 0;
                }
            }
            else
            {
                if (partnerP != null)
                {
                    partner.MentorExp += MenteeEXP;
                    MenteeEXP = 0;
                }
            }

            Info.Mentor = 0;
            GetMentor(false);

            if (Info.IsMentor && Info.MentorExp > 0)
            {
                GainExp((uint)Info.MentorExp);
                Info.MentorExp = 0;
            }

            partner.Mentor = 0;

            if (partnerP != null)
            {
                partnerP.ReceiveChat(LanguageMgr.Get("MentorshipExpired"), ChatType.System);
                partnerP.GetMentor(false);
                if (partner.IsMentor && partner.MentorExp > 0)
                {
                    partnerP.GainExp((uint)partner.MentorExp);
                    Info.MentorExp = 0;
                }
            }
            else
            {
                if (partner.IsMentor && partner.MentorExp > 0)
                {
                    partner.Experience += partner.MentorExp;
                    partner.MentorExp = 0;
                }
            }

            Info.IsMentor = false;
            partner.IsMentor = false;
            Info.MentorExp = 0;
            partner.MentorExp = 0;
        }

        public void AddMentor(string Name)
        {
            if (Info.Mentor != 0)
            {
                ReceiveChat(LanguageMgr.Get("YouAlreadyHaveMentor"), ChatType.System);
                return;
            }

            if (Info.Name == Name)
            {
                ReceiveChat(LanguageMgr.Get("YouCantMentorYourself"), ChatType.System);
                return;
            }

            if (Info.MentorDate > Envir.Now)
            {
                ReceiveChat(LanguageMgr.Get("YouCantStartNewMentorship"), ChatType.System);
                return;
            }

            PlayerObject mentor = Envir.GetPlayer(Name);

            if (mentor == null)
            {
                ReceiveChat(LanguageMgr.Get("CannotFindPlayerByName", Name), ChatType.System);
            }
            else
            {
                mentor.MentorRequest = null;

                if (!mentor.AllowMentor)
                {
                    ReceiveChat(LanguageMgr.Get("PlayerNotAllowMentorRequests", mentor.Info.Name), ChatType.System);
                    return;
                }

                if (mentor.Info.MentorDate > Envir.Now)
                {
                    ReceiveChat(LanguageMgr.Get("PlayerCantStartMentorshipYet", mentor.Info.Name), ChatType.System);
                    return;
                }

                if (mentor.Info.Mentor != 0)
                {
                    ReceiveChat(LanguageMgr.Get("PlayerIsAlreadyMentor", mentor.Info.Name), ChatType.System);
                    return;
                }

                if (Info.Class != mentor.Info.Class)
                {
                    ReceiveChat(LanguageMgr.Get("MentoredBySameClass"), ChatType.System);
                    return;
                }
                if ((Info.Level + Settings.MentorLevelGap) > mentor.Level)
                {
                    ReceiveChat(LanguageMgr.Get("YouCanBeMentoredByHigherLevel", Settings.MentorLevelGap), ChatType.System);
                    return;
                }

                mentor.MentorRequest = this;
                mentor.Enqueue(new S.MentorRequest { Name = Info.Name, Level = Info.Level });
                ReceiveChat(LanguageMgr.Get("RequestSent"), ChatType.System);
            }

        }

        public void MentorReply(bool accept)
        {
            if (MentorRequest == null || MentorRequest.Info == null)
            {
                MentorRequest = null;
                return;
            }

            if (!accept)
            {
                MentorRequest.ReceiveChat(LanguageMgr.Get("PlayerRefusedMentor", Info.Name), ChatType.System);
                MentorRequest = null;
                return;
            }

            if (Info.Mentor != 0)
            {
                ReceiveChat(LanguageMgr.Get("YouAlreadyHaveStudent"), ChatType.System);
                return;
            }

            PlayerObject student = Envir.GetPlayer(MentorRequest.Info.Name);
            MentorRequest = null;

            if (student == null)
            {
                ReceiveChat(LanguageMgr.Get("PlayerNoLongerOnline", student.Name), ChatType.System);
                return;
            }
            else
            {
                if (student.Info.Mentor != 0)
                {
                    ReceiveChat(LanguageMgr.Get("PlayerAlreadyHasMentor", student.Info.Name), ChatType.System);
                    return;
                }
                if (Info.Class != student.Info.Class)
                {
                    ReceiveChat(LanguageMgr.Get("YouCanOnlyMentorSameClass"), ChatType.System);
                    return;
                }
                if ((Info.Level - Settings.MentorLevelGap) < student.Level)
                {
                    ReceiveChat(LanguageMgr.Get("YouCanMentorBelowLevel", Settings.MentorLevelGap), ChatType.System);
                    return;
                }

                student.Info.Mentor = Info.Index;
                student.Info.IsMentor = false;
                Info.Mentor = student.Info.Index;
                Info.IsMentor = true;
                student.Info.MentorDate = Envir.Now;
                Info.MentorDate = Envir.Now;

                ReceiveChat(LanguageMgr.Get("YouAreMentorOf", student.Info.Name), ChatType.System);
                student.ReceiveChat(LanguageMgr.Get("YouAreMentoredBy", Info.Name), ChatType.System);
                GetMentor(false);
                student.GetMentor(false);
            }
        }

        public void GetMentor(bool CheckOnline = true)
        {
            if (Info.Mentor == 0)
            {
                Enqueue(new S.MentorUpdate { Name = "", Level = 0, Online = false, MenteeEXP = 0 });
            }
            else
            {
                CharacterInfo mentor = Envir.GetCharacterInfo(Info.Mentor);

                PlayerObject player = Envir.GetPlayer(mentor.Name);

                Enqueue(new S.MentorUpdate { Name = mentor.Name, Level = mentor.Level, Online = player != null, MenteeEXP = Info.MentorExp });

                if (player != null && CheckOnline)
                {
                    player.GetMentor(false);
                    player.ReceiveChat(LanguageMgr.Get("PlayerHasComeOnline", Info.Name), ChatType.System);
                }
            }
        }

        public void LogoutMentor()
        {
            if (Info.Mentor == 0) return;

            CharacterInfo mentor = Envir.GetCharacterInfo(Info.Mentor);

            if (mentor == null)
            {
                MessageQueue.EnqueueDebugging(LanguageMgr.Get("PlayerMentorIdNotFound", Name, Info.Mentor));
                return;
            }

            PlayerObject player = Envir.GetPlayer(mentor.Name);

            if (!Info.IsMentor)
            {
                mentor.MentorExp += MenteeEXP;
            }

            if (player != null)
            {
                player.Enqueue(new S.MentorUpdate { Name = Info.Name, Level = Info.Level, Online = false, MenteeEXP = mentor.MentorExp });
                player.ReceiveChat(LanguageMgr.Get("PlayerHasGoneOffline", Info.Name), ChatType.System);
            }
        }

        #endregion
    }
}
