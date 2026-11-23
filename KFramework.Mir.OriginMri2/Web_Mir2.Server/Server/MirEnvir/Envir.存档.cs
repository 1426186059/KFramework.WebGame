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
        public CharacterInfo GetArchivedCharacter(string name)
        {
            DirectoryInfo dir = new DirectoryInfo(ArchivePath);
            FileInfo[] files = dir.GetFiles($"{name}*.MirCA");

            if (files.Length != 1)
            {
                return null;
            }

            var fileInfo = files[0];

            CharacterInfo info = null;

            using (var stream = fileInfo.OpenRead())
            {
                using var reader = new BinaryReader(stream);

                var version = reader.ReadInt32();
                var customVersion = reader.ReadInt32();

                info = new CharacterInfo(reader, version, customVersion);
            }

            return info;
        }

        public void SaveArchivedCharacter(CharacterInfo info)
        {
            if (!Directory.Exists(ArchivePath)) Directory.CreateDirectory(ArchivePath);

            using var stream = File.Create(Path.Combine(ArchivePath, @$"{info.Name}{Now:_MMddyyyy_HHmmss}.MirCA"));
            using var writer = new BinaryWriter(stream);

            writer.Write(Version);
            writer.Write(CustomVersion);

            info.Save(writer);
        }

        public void SaveAccounts()
        {
            while (Saving)
                Thread.Sleep(1);

            try
            {
                using (var stream = File.Create(AccountPath + "n"))
                    SaveAccounts(stream);
                if (File.Exists(AccountPath))
                    File.Move(AccountPath, AccountPath + "o");
                File.Move(AccountPath + "n", AccountPath);
                if (File.Exists(AccountPath + "o"))
                    File.Delete(AccountPath + "o");
            }
            catch (Exception ex)
            {
                MessageQueue.Enqueue(ex);
            }
        }

        private void SaveAccounts(Stream stream)
        {
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Version);
                writer.Write(CustomVersion);
                writer.Write(NextAccountID);
                writer.Write(NextCharacterID);
                writer.Write(NextUserItemID);
                writer.Write(NextHeroID);

                writer.Write(GuildList.Count);
                writer.Write(NextGuildID);
                writer.Write(HeroList.Count);
                for (var i = 0; i < HeroList.Count; i++)
                    HeroList[i].Save(writer);
                writer.Write(AccountList.Count);
                for (var i = 0; i < AccountList.Count; i++)
                    AccountList[i].Save(writer);

                writer.Write(NextAuctionID);
                writer.Write(Auctions.Count);
                foreach (var auction in Auctions)
                    auction.Save(writer);

                writer.Write(NextMailID);

                writer.Write(GameshopLog.Count);
                foreach (var item in GameshopLog)
                {
                    writer.Write(item.Key);
                    writer.Write(item.Value);
                }

                writer.Write(SavedSpawns.Count);
                foreach (var Spawn in SavedSpawns)
                {
                    var Save = new RespawnSave { RespawnIndex = Spawn.Info.RespawnIndex, NextSpawnTick = Spawn.NextSpawnTick, Spawned = Spawn.Count >= Spawn.Info.Count * SpawnMultiplier };
                    Save.Save(writer);
                }
            }
        }

        private void SaveGuilds(bool forced = false)
        {
            if (!Directory.Exists(Settings.GuildPath)) Directory.CreateDirectory(Settings.GuildPath);

            if (GuildRefreshNeeded == true) //deletes guild files and resaves with new indexing if a guild is deleted.
            {
                foreach (var guildfile in Directory.GetFiles(Settings.GuildPath, "*.mgd"))
                {
                    File.Delete(guildfile);
                }

                GuildRefreshNeeded = false;
                forced = true; //triggers a full resave of all guilds
            }

            for (var i = 0; i < GuildList.Count; i++)
            {
                if (GuildList[i].NeedSave || forced)
                {
                    GuildList[i].NeedSave = false;

					GuildObject liveGuild = Guilds.Find(g => g.Guildindex == GuildList[i].GuildIndex);
					if (liveGuild != null)
					{
						GuildList[i] = liveGuild.Info;
					}

					var mStream = new MemoryStream();
                    var writer = new BinaryWriter(mStream);
					GuildList[i].Save(writer);
                    var fStream = new FileStream(Path.Combine(Settings.GuildPath, i + ".mgdn"), FileMode.Create);
                    var data = mStream.ToArray();
                    fStream.BeginWrite(data, 0, data.Length, EndSaveGuildsAsync, fStream);
                }
            }
        }
        private void EndSaveGuildsAsync(IAsyncResult result)
        {
            var fStream = result.AsyncState as FileStream;
            try
            {
                if (fStream == null) return;
                var oldfilename = fStream.Name.Substring(0, fStream.Name.Length - 1);
                var newfilename = fStream.Name;
                fStream.EndWrite(result);
                fStream.Dispose();
                if (File.Exists(oldfilename))
                    File.Move(oldfilename, oldfilename + "o");
                File.Move(newfilename, oldfilename);
                if (File.Exists(oldfilename + "o"))
                    File.Delete(oldfilename + "o");
            }
            catch (Exception)
            {
            }
        }

        private void SaveGoods(bool forced = false)
        {
            if (!Directory.Exists(Settings.GoodsPath)) Directory.CreateDirectory(Settings.GoodsPath);

            for (var i = 0; i < MapList.Count; i++)
            {
                var map = MapList[i];

                if (map.NPCs.Count == 0) continue;

                for (var j = 0; j < map.NPCs.Count; j++)
                {
                    var npc = map.NPCs[j];

                    if (forced)
                    {
                        npc.ProcessGoods(forced);
                    }

                    if (!npc.NeedSave) continue;

                    var path = Path.Combine(Settings.GoodsPath, npc.Info.Index + ".msdn");

                    var mStream = new MemoryStream();
                    var writer = new BinaryWriter(mStream);
                    var Temp = 9999;
                    writer.Write(Temp);
                    writer.Write(Version);
                    writer.Write(CustomVersion);
                    writer.Write(npc.UsedGoods.Count);

                    for (var k = 0; k < npc.UsedGoods.Count; k++)
                    {
                        npc.UsedGoods[k].Save(writer);
                    }

                    var fStream = new FileStream(path, FileMode.Create);
                    var data = mStream.ToArray();
                    fStream.BeginWrite(data, 0, data.Length, EndSaveGoodsAsync, fStream);
                }
            }
        }
        private void EndSaveGoodsAsync(IAsyncResult result)
        {
            try
            {
                var fStream = result.AsyncState as FileStream;
                if (fStream == null) return;
                var oldfilename = fStream.Name.Substring(0, fStream.Name.Length - 1);
                var newfilename = fStream.Name;
                fStream.EndWrite(result);
                fStream.Dispose();
                if (File.Exists(oldfilename))
                    File.Move(oldfilename, oldfilename + "o");
                File.Move(newfilename, oldfilename);
                if (File.Exists(oldfilename + "o"))
                    File.Delete(oldfilename + "o");
            }
            catch (Exception)
            {
            }
        }

        private void SaveConquests(bool forced = false)
        {
            if (!Directory.Exists(Settings.ConquestsPath)) Directory.CreateDirectory(Settings.ConquestsPath);
            for (var i = 0; i < ConquestList.Count; i++)
            {
                if (!ConquestList[i].NeedSave && !forced) continue;
                ConquestList[i].NeedSave = false;
                var mStream = new MemoryStream();
                var writer = new BinaryWriter(mStream);
                ConquestList[i].Save(writer);
                var fStream = new FileStream(Path.Combine(Settings.ConquestsPath, ConquestList[i].Info.Index + ".mcdn"), FileMode.Create);
                var data = mStream.ToArray();
                fStream.BeginWrite(data, 0, data.Length, EndSaveConquestsAsync, fStream);
            }
        }
        private void EndSaveConquestsAsync(IAsyncResult result)
        {
            var fStream = result.AsyncState as FileStream;
            try
            {
                if (fStream == null) return;
                var oldfilename = fStream.Name.Substring(0, fStream.Name.Length - 1);
                var newfilename = fStream.Name;
                fStream.EndWrite(result);
                fStream.Dispose();
                if (File.Exists(oldfilename))
                    File.Move(oldfilename, oldfilename + "o");
                File.Move(newfilename, oldfilename);
                if (File.Exists(oldfilename + "o"))
                    File.Delete(oldfilename + "o");
            }
            catch (Exception)
            {
            }
        }

        public void BeginSaveAccounts()
        {
            if (Saving) return;

            Saving = true;


            using (var mStream = new MemoryStream())
            {
                if (File.Exists(AccountPath))
                {
                    if (!Directory.Exists(AccountsBackUpPath)) Directory.CreateDirectory(AccountsBackUpPath);
                    var fileName =
                        $"Accounts {Now.Year:0000}-{Now.Month:00}-{Now.Day:00} {Now.Hour:00}-{Now.Minute:00}-{Now.Second:00}.bak";
                    if (File.Exists(Path.Combine(AccountsBackUpPath, fileName))) File.Delete(Path.Combine(AccountsBackUpPath, fileName));
                    File.Move(AccountPath, Path.Combine(AccountsBackUpPath, fileName));
                }

                SaveAccounts(mStream);
                var fStream = new FileStream(AccountPath + "n", FileMode.Create);

                var data = mStream.ToArray();
                fStream.BeginWrite(data, 0, data.Length, EndSaveAccounts, fStream);
            }
        }

        private void EndSaveAccounts(IAsyncResult result)
        {
            var fStream = result.AsyncState as FileStream;
            try
            {
                if (fStream != null)
                {
                    var oldfilename = fStream.Name.Substring(0, fStream.Name.Length - 1);
                    var newfilename = fStream.Name;
                    fStream.EndWrite(result);
                    fStream.Dispose();
                    if (File.Exists(oldfilename))
                        File.Move(oldfilename, oldfilename + "o");
                    File.Move(newfilename, oldfilename);
                    if (File.Exists(oldfilename + "o"))
                        File.Delete(oldfilename + "o");
                }
            }
            catch (Exception)
            {
            }

            Saving = false;
        }

    }
}
