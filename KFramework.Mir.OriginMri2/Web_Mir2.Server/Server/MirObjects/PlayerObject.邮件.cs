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
        #region Mail

        public void SendMail(string name, string message)
        {
            if (Envir.Time < NextMailTime)
            {
                //not even an error: users shouldnt be sending mails so fast only bots do.
                return;
            }

            NextMailTime = Envir.Time + 10000;

            if (message.Length > 500)
            {
                ReceiveChat(GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.TextExceedsMailLimit), ChatType.System);
                return;
            }

            CharacterInfo player = Envir.GetCharacterInfo(name);

            if (player == null)
            {
                ReceiveChat(GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.CouldNotFindPlayer), name), ChatType.System);
                return;
            }

            if (player.Mail.Count > 50)
            {
                ReceiveChat(GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.RecipientsMailboxFull), ChatType.System);
                return;
            }

            if (player.Friends.Any(e => e.Info == Info && e.Blocked))
            {
                ReceiveChat(GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.PlayerNotAcceptingMail), ChatType.System);
                return;
            }

            if (Info.Friends.Any(e => e.Info == player && e.Blocked))
            {
                ReceiveChat(GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.CannotMailPlayerOnBlacklist), ChatType.System);
                return;
            }

            //sent from player
            MailInfo mail = new MailInfo(player.Index, true)
            {
                Sender = Info.Name,
                Message = message,
                Gold = 0
            };

            mail.Send();
        }

        public void SendMail(string name, string message, uint gold, ulong[] items, bool stamped)
        {
            if (Envir.Time < NextMailTime)
            {
                //not even an error: users shouldnt be sending mails so fast only bots do.
                return;
            }

            NextMailTime = Envir.Time + 10000;

            if (message.Length > 500)
            {
                ReceiveChat(GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.TextExceedsMailSizeLimit), ChatType.System);
                return;
            }

            CharacterInfo player = Envir.GetCharacterInfo(name);

            if (player == null)
            {
                ReceiveChat(GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.CouldNotFindPlayer), name), ChatType.System);
                return;
            }

            if (player.Mail.Count > 50)
            {
                ReceiveChat(GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.RecipientsMailboxFull), ChatType.System);
                return;
            }

            bool hasStamp = false;
            uint totalGold = 0;
            uint parcelCost = GetMailCost(items, gold, stamped);

            totalGold = gold + parcelCost;

            if (Info.Gold < totalGold || Info.Gold < gold || gold > totalGold)
            {
                Enqueue(new S.MailSent { Result = -1 });
                return;
            }

            //Validate user has stamp
            if (stamped)
            {
                for (int i = 0; i < Info.Inventory.Length; i++)
                {
                    UserItem item = Info.Inventory[i];

                    if (item == null || item.Info.Type != ItemType.Nothing || item.Info.Shape != 1 || item.Count < 1) continue;

                    hasStamp = true;

                    if (item.Count > 1) item.Count--;
                    else Info.Inventory[i] = null;

                    Enqueue(new S.DeleteItem { UniqueID = item.UniqueID, Count = 1 });
                    break;
                }
            }

            List<UserItem> giftItems = new List<UserItem>();

            for (int j = 0; j < (hasStamp ? 5 : 1); j++)
            {
                if (items[j] < 1) continue;

                for (int i = 0; i < Info.Inventory.Length; i++)
                {
                    UserItem item = Info.Inventory[i];

                    if (item == null || items[j] != item.UniqueID) continue;

                    if (item.Info.Bind.HasFlag(BindMode.DontTrade))
                    {
                        ReceiveChat(GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.CannotBeMailed), item.FriendlyName), ChatType.System);
                        return;
                    }

                    if (item.Info.Bind.HasFlag(BindMode.NoMail))
                    {
                        ReceiveChat(GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.CannotBeMailed), item.FriendlyName), ChatType.System);
                        Enqueue(new S.MailSent { Result = -1 });
                        return;
                    }

                    if (item.RentalInformation != null && item.RentalInformation.BindingFlags.HasFlag(BindMode.DontTrade))
                    {
                        ReceiveChat(GameLanguage.ServerTextMap.GetLocalization((ServerTextKeys.CannotBeMailed), item.FriendlyName), ChatType.System);
                        return;
                    }

                    giftItems.Add(item);

                    Info.Inventory[i] = null;
                    Enqueue(new S.DeleteItem { UniqueID = item.UniqueID, Count = item.Count });
                }
            }

            if (totalGold > 0)
            {
                Info.Gold -= totalGold;
                Enqueue(new S.LoseGold { Gold = totalGold });
            }

            //Create parcel
            MailInfo mail = new MailInfo(player.Index, true)
            {
                MailID = ++Envir.NextMailID,
                Sender = Info.Name,
                Message = message,
                Gold = gold,
                Items = giftItems
            };

            mail.Send();

            Enqueue(new S.MailSent { Result = 1 });
        }

        public void ReadMail(ulong mailID)
        {
            MailInfo mail = Info.Mail.SingleOrDefault(e => e.MailID == mailID);

            if (mail == null) return;

            mail.DateOpened = Envir.Now;

            GetMail();
        }

        public void CollectMail(ulong mailID)
        {
            MailInfo mail = Info.Mail.SingleOrDefault(e => e.MailID == mailID);

            if (mail == null) return;

            if (!mail.Collected)
            {
                ReceiveChat(GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.MailCollectFromPostOffice), ChatType.System);
                return;
            }

            if (mail.Items.Count > 0)
            {
                if (!CanGainItems(mail.Items.ToArray()))
                {
                    ReceiveChat(GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.CannotCollectWhenBagFull), ChatType.System);
                    return;
                }

                for (int i = 0; i < mail.Items.Count; i++)
                {
                    GainItem(mail.Items[i]);
                }
            }

            if (mail.Gold > 0)
            {
                uint gold = mail.Gold;

                if (gold + Info.Gold >= uint.MaxValue)
                    gold = uint.MaxValue - Info.Gold;

                GainGold(gold);
            }

            mail.Items = new List<UserItem>();
            mail.Gold = 0;

            mail.Collected = true;

            Enqueue(new S.ParcelCollected { Result = 1 });

            GetMail();
        }

        public void DeleteMail(ulong mailID)
        {
            MailInfo mail = Info.Mail.SingleOrDefault(e => e.MailID == mailID);

            if (mail == null) return;

            Info.Mail.Remove(mail);

            GetMail();
        }

        public void LockMail(ulong mailID, bool lockMail)
        {
            MailInfo mail = Info.Mail.SingleOrDefault(e => e.MailID == mailID);

            if (mail == null) return;

            mail.Locked = lockMail;

            GetMail();
        }

        public uint GetMailCost(ulong[] items, uint gold, bool stamped)
        {
            uint cost = 0;

            if (!Settings.MailFreeWithStamp || !stamped)
            {
                if (gold > 0 && Settings.MailCostPer1KGold > 0)
                {
                    cost += (uint)Math.Floor((decimal)gold / 1000) * Settings.MailCostPer1KGold;
                }

                if (items != null && items.Length > 0 && Settings.MailItemInsurancePercentage > 0)
                {
                    for (int j = 0; j < (stamped ? 5 : 1); j++)
                    {
                        if (items[j] < 1) continue;

                        for (int i = 0; i < Info.Inventory.Length; i++)
                        {
                            UserItem item = Info.Inventory[i];

                            if (item == null || items[j] != item.UniqueID) continue;

                            cost += (uint)Math.Floor((double)item.Price() / 100 * Settings.MailItemInsurancePercentage);
                        }
                    }
                }
            }


            return cost;
        }

        public void GetMail()
        {
            List<ClientMail> mail = new List<ClientMail>();

            int start = (Info.Mail.Count - Settings.MailCapacity) > 0 ? (Info.Mail.Count - (int)Settings.MailCapacity) : 0;

            for (int i = start; i < Info.Mail.Count; i++)
            {
                foreach (UserItem itm in Info.Mail[i].Items)
                {
                    CheckItem(itm);
                }

                mail.Add(Info.Mail[i].CreateClientMail());
            }

            //foreach (MailInfo m in Info.Mail)
            //{
            //    foreach (UserItem itm in m.Items)
            //    {
            //        CheckItem(itm);
            //    }

            //    mail.Add(m.CreateClientMail());
            //}

            NewMail = false;

            Enqueue(new S.ReceiveMail { Mail = mail });
        }

        public int GetMailAwaitingCollectionAmount()
        {
            int count = 0;
            for (int i = 0; i < Info.Mail.Count; i++)
            {
                if (!Info.Mail[i].Collected) count++;
            }

            return count;
        }

        #endregion
    }
}
