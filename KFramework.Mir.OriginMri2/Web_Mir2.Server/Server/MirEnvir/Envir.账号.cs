// partial 文件：Envir 类按功能拆分，方法体从原 Envir.cs 原样迁入。
using ClientPackets;
using Server.Library.MirDatabase;
using Server.Library.Utils;
using Server.MirDatabase;
using Server.MirNetwork;
using Server.MirObjects;
using Server.MirObjects.Monsters;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Text.RegularExpressions;
using Library.Network;
using S = ServerPackets;
using System.Net.Http;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Server.MirEnvir
{
    public partial class Envir
    {
        public void NewAccount(ClientPackets.NewAccount p, MirConnection c)
        {
            if (!Settings.AllowNewAccount)
            {
                c.Enqueue(new ServerPackets.NewAccount { Result = 0 });
                return;
            }


            if (ConnectionLogs.TryGetValue(c.IPAddress, out MirConnectionLog currentlog))
            {
                if (currentlog.AccountsMade.Count > 2)
                {
                    IPBlocks[c.IPAddress] = Now.AddHours(24);
                    c.Enqueue(new ServerPackets.NewAccount { Result = 0 });
                    return;
                }
                currentlog.AccountsMade.Add(Time);
                for (int i = 0; i < currentlog.AccountsMade.Count; i++)
                {
                    if ((currentlog.AccountsMade[i] + 60 * 60 * 1000) < Time)
                    {
                        currentlog.AccountsMade.RemoveAt(i);
                        break;
                    }
                }
            }
            else
            {
                ConnectionLogs[c.IPAddress] = new MirConnectionLog() { IPAddress = c.IPAddress };
            }


            if (!AccountIDReg.IsMatch(p.AccountID))
            {
                c.Enqueue(new ServerPackets.NewAccount { Result = 1 });
                return;
            }

            if (!PasswordReg.IsMatch(p.Password))
            {
                c.Enqueue(new ServerPackets.NewAccount { Result = 2 });
                return;
            }
            if (!string.IsNullOrWhiteSpace(p.EMailAddress) && !EMailReg.IsMatch(p.EMailAddress) ||
                p.EMailAddress.Length > 50)
            {
                c.Enqueue(new ServerPackets.NewAccount { Result = 3 });
                return;
            }

            if (!string.IsNullOrWhiteSpace(p.UserName) && p.UserName.Length > 20)
            {
                c.Enqueue(new ServerPackets.NewAccount { Result = 4 });
                return;
            }

            if (!string.IsNullOrWhiteSpace(p.SecretQuestion) && p.SecretQuestion.Length > 30)
            {
                c.Enqueue(new ServerPackets.NewAccount { Result = 5 });
                return;
            }

            if (!string.IsNullOrWhiteSpace(p.SecretAnswer) && p.SecretAnswer.Length > 30)
            {
                c.Enqueue(new ServerPackets.NewAccount { Result = 6 });
                return;
            }

            lock (AccountLock)
            {
                if (AccountExists(p.AccountID))
                {
                    c.Enqueue(new ServerPackets.NewAccount { Result = 7 });
                    return;
                }

                AccountList.Add(new AccountInfo(p) { Index = ++NextAccountID, CreationIP = c.IPAddress });


                c.Enqueue(new ServerPackets.NewAccount { Result = 8 });
            }
        }

        public int HTTPNewAccount(ClientPackets.NewAccount p, string ip)
        {
            if (!Settings.AllowNewAccount)
            {
                return 0;
            }

            if (!AccountIDReg.IsMatch(p.AccountID))
            {
                return 1;
            }

            if (!PasswordReg.IsMatch(p.Password))
            {
                return 2;
            }
            if (!string.IsNullOrWhiteSpace(p.EMailAddress) && !EMailReg.IsMatch(p.EMailAddress) ||
                p.EMailAddress.Length > 50)
            {
                return 3;
            }

            if (!string.IsNullOrWhiteSpace(p.UserName) && p.UserName.Length > 20)
            {
                return 4;
            }

            if (!string.IsNullOrWhiteSpace(p.SecretQuestion) && p.SecretQuestion.Length > 30)
            {
                return 5;
            }

            if (!string.IsNullOrWhiteSpace(p.SecretAnswer) && p.SecretAnswer.Length > 30)
            {
                return 6;
            }

            lock (AccountLock)
            {
                if (AccountExists(p.AccountID))
                {
                    return 7;
                }

                AccountList.Add(new AccountInfo(p) { Index = ++NextAccountID, CreationIP = ip });
                return 8;
            }
        }

        public void ChangePassword(ClientPackets.ChangePassword p, MirConnection c)
        {
            if (!Settings.AllowChangePassword)
            {
                c.Enqueue(new ServerPackets.ChangePassword { Result = 0 });
                return;
            }

            if (!AccountIDReg.IsMatch(p.AccountID))
            {
                c.Enqueue(new ServerPackets.ChangePassword { Result = 1 });
                return;
            }

            if (!PasswordReg.IsMatch(p.CurrentPassword))
            {
                c.Enqueue(new ServerPackets.ChangePassword { Result = 2 });
                return;
            }

            if (!PasswordReg.IsMatch(p.NewPassword))
            {
                c.Enqueue(new ServerPackets.ChangePassword { Result = 3 });
                return;
            }

            var account = GetAccount(p.AccountID);

            if (account == null)
            {
                c.Enqueue(new ServerPackets.ChangePassword { Result = 4 });
                return;
            }

            if (account.Banned)
            {
                if (account.ExpiryDate > Now)
                {
                    c.Enqueue(new ServerPackets.ChangePasswordBanned { Reason = account.BanReason, ExpiryDate = account.ExpiryDate });
                    return;
                }
                account.Banned = false;
            }
            account.BanReason = string.Empty;
            account.ExpiryDate = DateTime.MinValue;

            p.CurrentPassword = Utils.Crypto.HashPassword(p.CurrentPassword, account.Salt);
            if (string.CompareOrdinal(account.Password, p.CurrentPassword) != 0)
            {
                c.Enqueue(new ServerPackets.ChangePassword { Result = 5 });
                return;
            }

            account.Password = p.NewPassword;
            account.RequirePasswordChange = false;
            c.Enqueue(new ServerPackets.ChangePassword { Result = 6 });
        }
        public void Login(ClientPackets.Login p, MirConnection c)
        {
            if (!Settings.AllowLogin)
            {
                c.Enqueue(new ServerPackets.Login { Result = 0 });
                return;
            }

            if (!AccountIDReg.IsMatch(p.AccountID))
            {
                c.Enqueue(new ServerPackets.Login { Result = 1 });
                return;
            }

            if (!PasswordReg.IsMatch(p.Password))
            {
                c.Enqueue(new ServerPackets.Login { Result = 2 });
                return;
            }
            var account = GetAccount(p.AccountID);

            if (account == null)
            {
                c.Enqueue(new ServerPackets.Login { Result = 3 });
                return;
            }

            if (account.Banned)
            {
                if (account.ExpiryDate > Now)
                {
                    c.Enqueue(new ServerPackets.LoginBanned
                    {
                        Reason = account.BanReason,
                        ExpiryDate = account.ExpiryDate
                    });
                    return;
                }
                account.Banned = false;
            }
            account.BanReason = string.Empty;
            account.ExpiryDate = DateTime.MinValue;

            p.Password = Utils.Crypto.HashPassword(p.Password, account.Salt);

            if (string.CompareOrdinal(account.Password, p.Password) != 0)
            {
                if (account.WrongPasswordCount++ >= 5)
                {
                    account.Banned = true;
                    account.BanReason = GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.TooManyWrongLoginAttempts);
                    account.ExpiryDate = Now.AddMinutes(2);

                    c.Enqueue(new ServerPackets.LoginBanned
                    {
                        Reason = account.BanReason,
                        ExpiryDate = account.ExpiryDate
                    });
                    return;
                }

                c.Enqueue(new ServerPackets.Login { Result = 4 });
                return;
            }
            account.WrongPasswordCount = 0;

            if (account.RequirePasswordChange)
            {
                c.Enqueue(new ServerPackets.Login { Result = 5 });
                return;
            }

            lock (AccountLock)
            {
                account.Connection?.SendDisconnect(1);

                account.Connection = c;
            }

            c.Account = account;
            c.Stage = GameStage.Select;

            account.LastDate = Now;
            account.LastIP = c.IPAddress;

            MessageQueue.Enqueue(string.Format("{0}, {1}, User logged in.", account.Connection.SessionID, account.Connection.IPAddress));
            c.Enqueue(new ServerPackets.LoginSuccess { Characters = account.GetSelectInfo() });
        }

        public int HTTPLogin(string AccountID, string Password)
        {
            if (!Settings.AllowLogin)
            {
                return 0;
            }

            if (!AccountIDReg.IsMatch(AccountID))
            {
                return 1;
            }

            if (!PasswordReg.IsMatch(Password))
            {
                return 2;
            }

            var account = GetAccount(AccountID);

            if (account == null)
            {
                return 3;
            }

            if (account.Banned)
            {
                if (account.ExpiryDate > Now)
                {
                    return 4;
                }
                account.Banned = false;
            }
            account.BanReason = string.Empty;
            account.ExpiryDate = DateTime.MinValue;
            if (string.CompareOrdinal(account.Password, Password) != 0)
            {
                if (account.WrongPasswordCount++ >= 5)
                {
                    account.Banned = true;
                    account.BanReason = GameLanguage.ServerTextMap.GetLocalization(ServerTextKeys.TooManyWrongLoginAttempts);
                    account.ExpiryDate = Now.AddMinutes(2);
                    return 5;
                }
                return 6;
            }
            account.WrongPasswordCount = 0;
            return 7;
        }

        public void NewCharacter(ClientPackets.NewCharacter p, MirConnection c, bool IsGm)
        {
            if (!Settings.AllowNewCharacter)
            {
                c.Enqueue(new ServerPackets.NewCharacter { Result = 0 });
                return;
            }

            if (ConnectionLogs.TryGetValue(c.IPAddress, out MirConnectionLog currentlog))
            {
                if (currentlog.CharactersMade.Count > 4)
                {
                    IPBlocks[c.IPAddress] = Now.AddHours(24);
                    c.Enqueue(new ServerPackets.NewCharacter { Result = 0 });
                    return;
                }
                currentlog.CharactersMade.Add(Time);
                for (int i = 0; i < currentlog.CharactersMade.Count; i++)
                {
                    if ((currentlog.CharactersMade[i] + 60 * 60 * 1000) < Time)
                    {
                        currentlog.CharactersMade.RemoveAt(i);
                        break;
                    }
                }
            }
            else
            {
                ConnectionLogs[c.IPAddress] = new MirConnectionLog() { IPAddress = c.IPAddress };
            }


            if (!CharacterReg.IsMatch(p.Name))
            {
                c.Enqueue(new ServerPackets.NewCharacter { Result = 1 });
                return;
            }

            if (!IsGm && DisabledCharNames.Contains(p.Name.ToUpper()))
            {
                c.Enqueue(new ServerPackets.NewCharacter { Result = 1 });
                return;
            }

            if (p.Gender != MirGender.Male && p.Gender != MirGender.Female)
            {
                c.Enqueue(new ServerPackets.NewCharacter { Result = 2 });
                return;
            }

            if (p.Class != MirClass.Warrior && p.Class != MirClass.Wizard && p.Class != MirClass.Taoist &&
                p.Class != MirClass.Assassin && p.Class != MirClass.Archer)
            {
                c.Enqueue(new ServerPackets.NewCharacter { Result = 3 });
                return;
            }

            if (p.Class == MirClass.Assassin && !Settings.AllowCreateAssassin ||
                p.Class == MirClass.Archer && !Settings.AllowCreateArcher)
            {
                c.Enqueue(new ServerPackets.NewCharacter { Result = 3 });
                return;
            }

            var count = 0;

            for (var i = 0; i < c.Account.Characters.Count; i++)
            {
                if (c.Account.Characters[i].Deleted) continue;

                if (++count >= Globals.MaxCharacterCount)
                {
                    c.Enqueue(new ServerPackets.NewCharacter { Result = 4 });
                    return;
                }
            }

            lock (AccountLock)
            {
                if (CharacterExists(p.Name))
                {
                    c.Enqueue(new ServerPackets.NewCharacter { Result = 5 });
                    return;
                }

                var info = new CharacterInfo(p, c) { Index = ++NextCharacterID, AccountInfo = c.Account };

                c.Account.Characters.Add(info);
                CharacterList.Add(info);

                c.Enqueue(new ServerPackets.NewCharacterSuccess { CharInfo = info.ToSelectInfo() });
            }
        }

        public bool CanCreateHero(ClientPackets.NewHero p, MirConnection c, bool IsGm)
        {
            if (!Settings.AllowNewHero)
            {
                c.Enqueue(new S.NewHero { Result = 0 });
                return false;
            }

            if (!CharacterReg.IsMatch(p.Name))
            {
                c.Enqueue(new S.NewHero { Result = 1 });
                return false;
            }

            if (!IsGm && DisabledCharNames.Contains(p.Name.ToUpper()))
            {
                c.Enqueue(new S.NewHero { Result = 1 });
                return false;
            }

            if (p.Gender != MirGender.Male && p.Gender != MirGender.Female)
            {
                c.Enqueue(new S.NewHero { Result = 2 });
                return false;
            }

            if (p.Class != MirClass.Warrior && p.Class != MirClass.Wizard && p.Class != MirClass.Taoist && p.Class != MirClass.Assassin && p.Class != MirClass.Archer)
            {
                c.Enqueue(new S.NewHero { Result = 3 });
                return false;
            }

            if (p.Class == MirClass.Warrior && !Settings.Hero_CanCreateClass[0] || p.Class == MirClass.Wizard && !Settings.Hero_CanCreateClass[1] || p.Class == MirClass.Taoist && !Settings.Hero_CanCreateClass[2] || p.Class == MirClass.Assassin && !Settings.Hero_CanCreateClass[3] || p.Class == MirClass.Archer && !Settings.Hero_CanCreateClass[4])
            {
                c.Enqueue(new S.NewHero { Result = 3 });
                return false;
            }

            lock (AccountLock)
            {
                if (CharacterExists(p.Name))
                {
                    c.Enqueue(new S.NewHero { Result = 5 });
                    return false;
                }
            }

            return true;
        }

        public bool AccountExists(string accountID)
        {
            for (var i = 0; i < AccountList.Count; i++)
                if (string.Compare(AccountList[i].AccountID, accountID, StringComparison.OrdinalIgnoreCase) == 0)
                    return true;

            return false;
        }

        public bool CharacterExists(string name)
        {
            for (var i = 0; i < CharacterList.Count; i++)
                if (string.Compare(CharacterList[i].Name, name, StringComparison.OrdinalIgnoreCase) == 0)
                    return true;

            return false;
        }

        public List<CharacterInfo> MatchPlayer(string PlayerID, bool match = false)
        {
            if (string.IsNullOrEmpty(PlayerID)) return new List<CharacterInfo>(CharacterList);

            List<CharacterInfo> list = new List<CharacterInfo>();

            for (int i = 0; i < CharacterList.Count; i++)
            {
                if (match)
                {
                    if (CharacterList[i].Name.Equals(PlayerID, StringComparison.OrdinalIgnoreCase))
                        list.Add(CharacterList[i]);
                }
                else
                {
                    if (CharacterList[i].Name.IndexOf(PlayerID, StringComparison.OrdinalIgnoreCase) >= 0)
                        list.Add(CharacterList[i]);
                }
            }

            return list;
        }
        public List<CharacterInfo> MatchPlayerbyItem(string itemIdentifier, bool match = false)
        {
            List<CharacterInfo> list = new List<CharacterInfo>();

            bool isNumeric = ulong.TryParse(itemIdentifier, out ulong itemId);

            for (int i = 0; i < CharacterList.Count; i++)
            {
                if (match)
                {
                    foreach (var item in CharacterList[i].Inventory)
                        if (item != null && ((isNumeric && item.UniqueID == itemId) || (!isNumeric && item.FriendlyName.Equals(itemIdentifier, StringComparison.OrdinalIgnoreCase))) && !list.Contains(CharacterList[i]))
                            list.Add(CharacterList[i]);

                    foreach (var item in CharacterList[i].AccountInfo.Storage)
                        if (item != null && ((isNumeric && item.UniqueID == itemId) || (!isNumeric && item.FriendlyName.Equals(itemIdentifier, StringComparison.OrdinalIgnoreCase))) && !list.Contains(CharacterList[i]))
                            list.Add(CharacterList[i]);

                    foreach (var item in CharacterList[i].QuestInventory)
                        if (item != null && ((isNumeric && item.UniqueID == itemId) || (!isNumeric && item.FriendlyName.Equals(itemIdentifier, StringComparison.OrdinalIgnoreCase))) && !list.Contains(CharacterList[i]))
                            list.Add(CharacterList[i]);

                    foreach (var item in CharacterList[i].Equipment)
                        if (item != null && ((isNumeric && item.UniqueID == itemId) || (!isNumeric && item.FriendlyName.Equals(itemIdentifier, StringComparison.OrdinalIgnoreCase))) && !list.Contains(CharacterList[i]))
                            list.Add(CharacterList[i]);

                    foreach (var mail in CharacterList[i].Mail)
                        foreach (var item in mail.Items)
                            if (item != null && ((isNumeric && item.UniqueID == itemId) || (!isNumeric && item.FriendlyName.Equals(itemIdentifier, StringComparison.OrdinalIgnoreCase))) && !list.Contains(CharacterList[i]))
                                list.Add(CharacterList[i]);
                }
                else
                {
                    foreach (var item in CharacterList[i].Inventory)
                        if (item != null && ((isNumeric && item.UniqueID == itemId) || (!isNumeric && item.FriendlyName.IndexOf(itemIdentifier, StringComparison.OrdinalIgnoreCase) >= 0)) && !list.Contains(CharacterList[i]))
                            list.Add(CharacterList[i]);

                    foreach (var item in CharacterList[i].QuestInventory)
                        if (item != null && ((isNumeric && item.UniqueID == itemId) || (!isNumeric && item.FriendlyName.IndexOf(itemIdentifier, StringComparison.OrdinalIgnoreCase) >= 0)) && !list.Contains(CharacterList[i]))
                            list.Add(CharacterList[i]);

                    foreach (var item in CharacterList[i].Equipment)
                        if (item != null && ((isNumeric && item.UniqueID == itemId) || (!isNumeric && item.FriendlyName.IndexOf(itemIdentifier, StringComparison.OrdinalIgnoreCase) >= 0)) && !list.Contains(CharacterList[i]))
                            list.Add(CharacterList[i]);

                    foreach (var item in CharacterList[i].AccountInfo.Storage)
                        if (item != null && ((isNumeric && item.UniqueID == itemId) || (!isNumeric && item.FriendlyName.IndexOf(itemIdentifier, StringComparison.OrdinalIgnoreCase) >= 0)) && !list.Contains(CharacterList[i]))
                            list.Add(CharacterList[i]);
                }
            }

            return list;
        }

        public AccountInfo GetAccount(string accountID)
        {
            for (var i = 0; i < AccountList.Count; i++)
                if (string.Compare(AccountList[i].AccountID, accountID, StringComparison.OrdinalIgnoreCase) == 0)
                    return AccountList[i];

            return null;
        }

        public AccountInfo GetAccountByCharacter(string name)
        {
            for (var i = 0; i < AccountList.Count; i++)
            {
                for (int j = 0; j < AccountList[i].Characters.Count; j++)
                {
                    if (string.Compare(AccountList[i].Characters[j].Name, name, StringComparison.OrdinalIgnoreCase) == 0)
                        return AccountList[i];
                }
            }

            return null;
        }

        public List<AccountInfo> MatchAccounts(string accountID, bool match = false)
        {
            if (string.IsNullOrEmpty(accountID)) return new List<AccountInfo>(AccountList);

            var list = new List<AccountInfo>();

            for (var i = 0; i < AccountList.Count; i++)
            {
                if (match)
                {
                    if (AccountList[i].AccountID.Equals(accountID, StringComparison.OrdinalIgnoreCase))
                        list.Add(AccountList[i]);
                }
                else
                {
                    if (AccountList[i].AccountID.IndexOf(accountID, StringComparison.OrdinalIgnoreCase) >= 0)
                        list.Add(AccountList[i]);
                }
            }

            return list;
        }

        public List<AccountInfo> MatchAccountsByPlayer(string playerName, bool match = false)
        {
            if (string.IsNullOrEmpty(playerName)) return new List<AccountInfo>(AccountList);

            var list = new List<AccountInfo>();

            for (var i = 0; i < AccountList.Count; i++)
            {
                for (var j = 0; j < AccountList[i].Characters.Count; j++)
                {
                    if (match)
                    {
                        if (AccountList[i].Characters[j].Name.Equals(playerName, StringComparison.OrdinalIgnoreCase))
                            list.Add(AccountList[i]);
                    }
                    else
                    {
                        if (AccountList[i].Characters[j].Name.IndexOf(playerName, StringComparison.OrdinalIgnoreCase) >= 0)
                            list.Add(AccountList[i]);
                    }
                }
            }

            return list;
        }

        public List<AccountInfo> MatchAccountsByIP(string ipAddress, bool matchLastIP = false, bool match = false)
        {
            if (string.IsNullOrEmpty(ipAddress)) return new List<AccountInfo>(AccountList);

            var list = new List<AccountInfo>();

            for (var i = 0; i < AccountList.Count; i++)
            {
                string ipToMatch = matchLastIP ? AccountList[i].LastIP : AccountList[i].CreationIP;

                if (match)
                {
                    if (ipToMatch.Equals(ipAddress, StringComparison.OrdinalIgnoreCase))
                        list.Add(AccountList[i]);
                }
                else
                {
                    if (ipToMatch.IndexOf(ipAddress, StringComparison.OrdinalIgnoreCase) >= 0)
                        list.Add(AccountList[i]);
                }
            }

            return list;
        }


    }
}
