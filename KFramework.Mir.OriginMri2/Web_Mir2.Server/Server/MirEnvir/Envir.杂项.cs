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
        public void ReloadNPCs()
        {
            SaveGoods(true);

            Robot.Clear();

            var keys = Scripts.Keys;

            foreach (var key in keys)
            {
                Scripts[key].Load();
            }

            MessageQueue.Enqueue("NPC Scripts Reloaded.");
        }

        public void ReloadDrops()
        {
            int configDrops = 0, scriptDrops = 0;
            for (var i = 0; i < MonsterInfoList.Count; i++)
            {
                var monster = MonsterInfoList[i];
                monster.Drops.Clear();

                if (monster.DropSettings != null)
                {
                    foreach (var setting in monster.DropSettings)
                    {
                        var drop = DropInfo.FromSetting(setting);
                        if (drop != null) monster.Drops.Add(drop);
                    }
                    configDrops++;
                    continue;
                }

                string path = Path.Combine(Settings.DropPath, monster.Name + ".txt");

                if (!string.IsNullOrEmpty(monster.DropPath))
                {
                    path = Path.Combine(Settings.DropPath, monster.DropPath + ".txt");
                }

                DropInfo.Load(monster.Drops, monster.Name, path, 0, true);
                scriptDrops++;
            }

            FishingDrops.Clear();
            for (int i = 0; i < 19; i++)
            {
                var path = Path.Combine(Settings.DropPath, Settings.FishingDropFilename + ".txt");
                path = path.Replace("00", i.ToString("D2"));

                DropInfo.Load(FishingDrops, $"Fishing {i}", path, (byte)i, i < 2);
            }

            AwakeningDrops.Clear();
            DropInfo.Load(AwakeningDrops, "Awakening", Path.Combine(Settings.DropPath, Settings.AwakeningDropFilename + ".txt"));

            StrongboxDrops.Clear();
            DropInfo.Load(StrongboxDrops, "StrongBox", Path.Combine(Settings.DropPath, Settings.StrongboxDropFilename + ".txt"));

            BlackstoneDrops.Clear();
            DropInfo.Load(BlackstoneDrops, "Blackstone", Path.Combine(Settings.DropPath, Settings.BlackstoneDropFilename + ".txt"));

            MessageQueue.Enqueue($"Drops Loaded. (配置表 {configDrops} / 脚本 {scriptDrops})");
        }

        public void ReloadLineMessages()
        {
            LineMessages.Clear();

            var path = Path.Combine(Settings.EnvirPath, "LineMessage.txt");

            if (!File.Exists(path))
            {
                File.WriteAllText(path, "");
            }
            else
            {
                var lines = File.ReadAllLines(path);

                for (var i = 0; i < lines.Length; i++)
                {
                    if (lines[i].StartsWith(";") || string.IsNullOrWhiteSpace(lines[i])) continue;
                    LineMessages.Add(lines[i]);
                }

                MessageQueue.Enqueue("LineMessages reloaded.");
            }
        }

        private WorldMapIcon ValidateWorldMap()
        {
            foreach (WorldMapIcon wmi in Settings.WorldMapSetup.Icons)
            {
                MapInfoTable info = GetMapInfo(wmi.MapIndex);

                if (info == null)
                    return wmi;
            }
            return null;
        }

        public void DeleteGuild(GuildObject guild)
        {
            Guilds.Remove(guild);
            GuildList.Remove(guild.Info);

            GuildRefreshNeeded = true;
            MessageQueue.Enqueue(string.Format("{0} guild will be deleted from the server.", guild.Info.Name));
        }
    }
}
