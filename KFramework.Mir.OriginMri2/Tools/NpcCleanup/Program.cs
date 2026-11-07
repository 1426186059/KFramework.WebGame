using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace NpcCleanup
{
    /// <summary>
    /// 独立工具：清理 NPCInfo.json 中重复的 NPC。
    /// 去重规则：在同一个 城市/村镇（FileName 的目录部分，如 "比奇省/比奇城"）内，
    ///   若两个 NPC 的“显示名”相同，则视为重复，仅保留首个，其余删除。
    ///   “显示名” = NameUI 优先；NameUI 为空时回退使用 Name。两者皆空则不参与判重（保留）。
    ///   说明：基础 NPC（如 比奇城 的“传送员”）常无 NameUI，用 Name 回退即可与
    ///         NameUI=传送员 的副本（传送员-张四/小李）正确合并成 1 个。
    /// 按 FileName 第一段（省份，如 "比奇省"）限定作用范围，避免误删其它省份。
    ///
    /// 用法：
    ///   NpcCleanup[.exe] [npcInfo路径] [省份前缀|ALL] [-dry]
    ///   例：NpcCleanup                                 # 默认路径 + 比奇省 + 真实写回
    ///       NpcCleanup -dry                            # 比奇省 仅预览不写回
    ///       NpcCleanup "D:\...\NPCInfo.json" 盟重 -dry   # 指定文件/省份，仅预览
    ///       NpcCleanup ALL -dry                         # 全部省份去重预览
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            const string defaultPath =
                @"D:\OpenSource\KFramework.WebGame\KFramework.Mir.OriginMri2\Web_Mir2.Server.Version\ExcelConfig\NPCInfo.json";

            string? path = null;
            string province = "比奇省";
            bool dry = false;

            foreach (var a in args)
            {
                if (a.Equals("-dry", StringComparison.OrdinalIgnoreCase) ||
                    a.Equals("--dry", StringComparison.OrdinalIgnoreCase))
                    dry = true;
                else if (path == null)
                    path = a;
                else
                    province = a;
            }

            if (path == null) path = defaultPath;

            if (!File.Exists(path))
            {
                Console.WriteLine($"文件不存在: {path}");
                return 1;
            }

            var root = JsonNode.Parse(File.ReadAllText(path, Encoding.UTF8));
            JsonArray arr;
            if (root is JsonArray ja) arr = ja;
            else if (root is JsonObject o && o["$values"] is JsonArray v) arr = v;
            else
            {
                Console.WriteLine("无法识别 NPCInfo.json 结构（既不是数组，也不是 {\"$values\":[...]} 信封）。");
                return 1;
            }

            var seen = new HashSet<string>();
            var toRemove = new List<JsonObject>();
            int scanned = 0, duplicate = 0, kept = 0, outOfScope = 0, noNameUI = 0;

            foreach (var node in arr)
            {
                if (node is not JsonObject npc) continue;
                scanned++;
                string fileName = npc["FileName"]?.GetValue<string>() ?? "";
                int mapIndex = npc["MapIndex"]?.GetValue<int>() ?? -1;
                string seg0 = fileName.Contains('/') ? fileName.Split('/')[0] : "";

                bool inScope = province.Equals("ALL", StringComparison.OrdinalIgnoreCase)
                    ? true
                    : seg0 == province;

                if (!inScope) { outOfScope++; kept++; continue; }

                // 显示名：优先 NameUI；NameUI 为空时回退到 Name。两者皆空则无法判重，保留。
                string nameUI = npc["NameUI"]?.GetValue<string>() ?? "";
                string displayName = !string.IsNullOrWhiteSpace(nameUI)
                    ? nameUI
                    : (npc["Name"]?.GetValue<string>() ?? "");
                if (string.IsNullOrWhiteSpace(displayName)) { noNameUI++; kept++; continue; }

                // 同一 城市/村镇（FileName 目录部分）+ 同显示名 → 视为重复，仅保留首个
                string city = fileName.Contains('/')
                    ? fileName.Substring(0, fileName.LastIndexOf('/'))
                    : fileName;
                string key = city + "|" + displayName;
                if (seen.Add(key))
                {
                    kept++;
                }
                else
                {
                    duplicate++;
                    toRemove.Add(npc);
                    int idx = npc["Index"]?.GetValue<int>() ?? -1;
                    string name = npc["Name"]?.GetValue<string>() ?? "";
                    Console.WriteLine($"  删除重复: Index={idx}  Name={name}  NameUI={nameUI}  显示名={displayName}  FileName={fileName}  City={city}");
                }
            }

            Console.WriteLine(
                $"\n范围: 省份='{province}'  扫描 {scanned} 个 | 重复 {duplicate} 个 | 保留 {kept} 个（范围外 {outOfScope} 个，NameUI为空 {noNameUI} 个）。");

            if (dry)
            {
                Console.WriteLine("[DRY-RUN] 未写回文件。");
                return 0;
            }

            // 逆序移除，避免索引漂移
            foreach (var npc in toRemove)
            {
                for (int i = arr.Count - 1; i >= 0; i--)
                {
                    if (ReferenceEquals(arr[i], npc)) { arr.RemoveAt(i); break; }
                }
            }

            var opts = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            File.WriteAllText(path, root.ToJsonString(opts), Encoding.UTF8);
            Console.WriteLine($"已写回 {path}，移除 {toRemove.Count} 个重复 NPC。");
            return 0;
        }
    }
}
