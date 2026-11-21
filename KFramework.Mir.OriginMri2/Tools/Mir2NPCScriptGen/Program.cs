using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Mir2NPCScriptGen;

/// <summary>
/// 把线上 NPCInfo.json 里 OriginSettings 的原始传奇对话/按钮，生成我们的 Envir/NPCs/*.txt 脚本。
/// 方案：保留已有脚本；只为「有对话但无脚本」的 NPC 生成；按钮文字对齐，基础按钮(购买/出售/离开)可用，其余占位。
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        bool dryRun = false;
        bool updateExisting = false;
        bool fixNames = false;
        string root = @"D:\OpenSource\KFramework.WebGame\KFramework.Mir.OriginMri2";
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--root" && i + 1 < args.Length) root = args[++i];
            else if (args[i] == "--dry-run") dryRun = true;
            else if (args[i] == "--update-existing") updateExisting = true;
            else if (args[i] == "--fix-names") fixNames = true;
        }
        if (updateExisting) return UpdateExistingMainText(root, dryRun);
        if (fixNames) return FixWeirdNames(root, dryRun);

        string npcInfoPath = Path.Combine(root, @"Web_Mir2.Server.Version\ExcelConfig\NPCInfo.json");
        string scriptRoot = Path.Combine(root, @"Web_Mir2.Server.Version\Envir\NPCs");
        if (!File.Exists(npcInfoPath))
        {
            Console.Error.WriteLine("找不到 " + npcInfoPath);
            return 1;
        }

        string backupPath = npcInfoPath + ".npcscriptgen.bak";
        if (!dryRun && !File.Exists(backupPath))
        {
            File.Copy(npcInfoPath, backupPath);
            Console.WriteLine("已备份: " + backupPath);
        }
        if (dryRun) Console.WriteLine("=== DRY RUN：不写线上 NPCInfo.json / Envir ===");

        JsonArray arr;
        try
        {
            arr = JsonNode.Parse(File.ReadAllText(npcInfoPath))!.AsArray();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("解析 NPCInfo.json 失败: " + ex.Message);
            return 1;
        }

        string toolDir = Path.Combine(root, @"Tools\Mir2NPCScriptGen");
        string scriptDir = dryRun
            ? Path.Combine(toolDir, "_dryrun_scripts", "原始传奇")
            : Path.Combine(scriptRoot, "原始传奇");
        Directory.CreateDirectory(scriptDir);

        int generated = 0, skippedHasScript = 0, skippedNoDepiction = 0, totalButtons = 0, placeholderButtons = 0;
        var report = new StringBuilder();

        foreach (var node in arr)
        {
            if (node is not JsonObject npc) continue;

            int index = GetInt(npc, "Index");
            string name = GetStr(npc, "Name");
            string fileName = GetStr(npc, "FileName");
            if (string.IsNullOrWhiteSpace(name)) name = "NPC_" + index;

            bool hasScript = !string.IsNullOrWhiteSpace(fileName) && File.Exists(Path.Combine(scriptRoot, fileName + ".txt"));
            if (hasScript)
            {
                skippedHasScript++;
                continue;
            }

            var os = npc["OriginSettings"] as JsonObject;
            string depiction = os == null ? "" : GetStr(os, "Depiction");
            string buttonRaw = os == null ? "" : GetStr(os, "Button");
            string title = os == null ? "" : GetStr(os, "Title");

            if (string.IsNullOrWhiteSpace(depiction))
            {
                skippedNoDepiction++;
                continue;
            }

            var buttons = ExtractButtons(buttonRaw);
            totalButtons += buttons.Count;

            string script = BuildScript(name, title, depiction, buttons, out int placeholders);
            placeholderButtons += placeholders;

            // 文件名优先用 Title（原始传奇 NPC 名字常是 <br> 多行艺术字，不适合做文件名）
            string nameForFile = string.IsNullOrWhiteSpace(title) ? name : title;
            string safeName = SanitizeFileName(nameForFile);
            if (string.IsNullOrWhiteSpace(safeName)) safeName = "NPC";
            string relName = "原始传奇/" + safeName + "_" + index;
            string fullPath = Path.Combine(scriptDir, safeName + "_" + index + ".txt");
            File.WriteAllText(fullPath, script, new UTF8Encoding(false));

            npc["FileName"] = relName;
            generated++;
            report.AppendLine(index + " | " + name + " -> " + relName + " 按钮数=" + buttons.Count + " 占位=" + placeholders);
        }

        if (!dryRun)
        {
            var opts = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            File.WriteAllText(npcInfoPath, arr.ToJsonString(opts), new UTF8Encoding(false));
            Console.WriteLine("已写回: " + npcInfoPath);
        }

        string reportPath = Path.Combine(toolDir, "generated_report.txt");
        File.WriteAllText(reportPath, report.ToString(), new UTF8Encoding(false));

        Console.WriteLine("生成脚本: " + generated);
        Console.WriteLine("保留已有脚本: " + skippedHasScript);
        Console.WriteLine("无对话跳过: " + skippedNoDepiction);
        Console.WriteLine("按钮总数: " + totalButtons + " (占位 " + placeholderButtons + ")");
        Console.WriteLine("报告: " + reportPath);
        return 0;
    }

    private static string BuildScript(string name, string title, string depiction, List<string> buttons, out int placeholders)
    {
        placeholders = 0;
        var sb = new StringBuilder();
        sb.AppendLine("[@Main]");
        sb.AppendLine("#SAY");

        if (!string.IsNullOrWhiteSpace(title) && title != name)
        {
            sb.AppendLine(CleanText(title));
        }

        string[] lines = depiction.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (string raw in lines)
        {
            string line = CleanText(raw);
            if (!string.IsNullOrWhiteSpace(line)) sb.AppendLine(line);
        }

        var actions = new List<string>();
        for (int i = 0; i < buttons.Count; i++)
        {
            string action = MapButtonAction(buttons[i], i + 1);
            actions.Add(action);
            if (action.StartsWith("@OP", StringComparison.Ordinal)) placeholders++;
            sb.AppendLine("<" + buttons[i] + "/" + action + ">");
        }
        // 按钮里已有“离开/退出/返回”就不再重复添加
        bool hasExitButton = buttons.Any(b => b.Contains("离开") || b.Contains("退出") || b.Contains("返回") || b.Contains("关闭"));
        if (!hasExitButton) sb.AppendLine("<离开/@exit>");

        for (int i = 0; i < buttons.Count; i++)
        {
            if (!actions[i].StartsWith("@OP", StringComparison.Ordinal)) continue;
            sb.AppendLine();
            sb.AppendLine("[" + actions[i] + "]");
            sb.AppendLine("#SAY");
            sb.AppendLine("该功能正在移植中，敬请期待。");
            sb.AppendLine("<返回/@Main>");
        }

        return sb.ToString();
    }

    private static string MapButtonAction(string text, int index)
    {
        string t = text.Trim();
        if (t.Contains("离开") || t.Contains("退出") || t.Contains("返回") || t.Contains("关闭")) return "@exit";
        if (t.Contains("购买") || t.Contains("商店") || t.Contains("商城")) return "@BuySell";
        if (t.Contains("出售")) return "@Sell";
        if (t.Contains("修理") || t.Contains("修复")) return "@Repair";
        return "@OP" + index;
    }

    private static List<string> ExtractButtons(string raw)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(raw)) return result;

        foreach (string part in Regex.Split(raw, @"[|&]"))
        {
            string txt = ExtractButtonText(part);
            if (!string.IsNullOrWhiteSpace(txt) && !result.Contains(txt)) result.Add(txt);
        }
        return result;
    }

    private static string ExtractButtonText(string part)
    {
        if (string.IsNullOrWhiteSpace(part)) return "";
        foreach (string seg in part.Split('#'))
        {
            string s = seg.Trim();
            if (string.IsNullOrEmpty(s)) continue;
            if (Regex.IsMatch(s, @"^\d+$")) continue;
            s = CleanText(s);
            if (!string.IsNullOrWhiteSpace(s)) return s;
        }
        return "";
    }

    private static string CleanText(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        s = Regex.Replace(s, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"<[^>]+>", "");
        s = WebUtility.HtmlDecode(s);
        s = s.Replace('<', '〈').Replace('>', '〉');
        s = s.Replace('/', '／');
        return s.Trim();
    }

    private static string SanitizeFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "";
        // 去掉 <br>、HTML 标签、换行；制表框/装饰字符替换为空白
        name = Regex.Replace(name, @"<br\s*/?>", "", RegexOptions.IgnoreCase);
        name = Regex.Replace(name, @"<[^>]+>", "");
        name = Regex.Replace(name, @"[\r\n\t]", "");
        name = WebUtility.HtmlDecode(name);
        // 只保留中文、字母、数字；其余（◇┌─┐│空格等装饰）去掉
        name = Regex.Replace(name, @"[^\u4e00-\u9fa5a-zA-Z0-9]", "");
        if (name.Length > 30) name = name.Substring(0, 30);
        return name;
    }

    private static string GetStr(JsonObject o, string key)
    {
        if (!o.TryGetPropertyValue(key, out var n) || n == null) return "";
        return n.GetValue<string>();
    }

    private static int GetInt(JsonObject o, string key)
    {
        if (!o.TryGetPropertyValue(key, out var n) || n == null) return 0;
        return n.GetValue<int>();
    }

    /// <summary>
    /// 对“已有脚本 + 有原始对话”的 NPC，只替换 [@Main] 页里的对话文本行，
    /// 保留按钮行、#SAY/#IF 等控制行和其它所有功能页原样，避免破坏传送等复杂脚本。
    /// </summary>
    private static int UpdateExistingMainText(string root, bool dryRun)
    {
        string npcInfoPath = Path.Combine(root, @"Web_Mir2.Server.Version\ExcelConfig\NPCInfo.json");
        string scriptRoot = Path.Combine(root, @"Web_Mir2.Server.Version\Envir\NPCs");
        var arr = JsonNode.Parse(File.ReadAllText(npcInfoPath))!.AsArray();

        int updated = 0;
        var report = new StringBuilder();
        foreach (var node in arr)
        {
            if (node is not JsonObject npc) continue;
            int index = GetInt(npc, "Index");
            string fileName = GetStr(npc, "FileName");
            if (string.IsNullOrWhiteSpace(fileName) || fileName.StartsWith("原始传奇/")) continue;

            string scriptPath = Path.Combine(scriptRoot, fileName + ".txt");
            if (!File.Exists(scriptPath)) continue;

            var os = npc["OriginSettings"] as JsonObject;
            string depiction = os == null ? "" : GetStr(os, "Depiction");
            if (string.IsNullOrWhiteSpace(depiction)) continue;

            string buttonRaw = os == null ? "" : GetStr(os, "Button");
            string[] lines = File.ReadAllLines(scriptPath);
            var newLines = ReplaceMainPage(lines, depiction, buttonRaw, out bool changed, out int btnCount);
            if (!changed)
            {
                report.AppendLine(index + " | " + GetStr(npc, "Name") + " | Main页未找到对话行，跳过");
                continue;
            }

            string outPath = dryRun
                ? Path.Combine(root, @"Tools\Mir2NPCScriptGen\_dryrun_existing", fileName + ".txt")
                : scriptPath;
            if (dryRun)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
            }
            else
            {
                string bak = scriptPath + ".npcscriptgen.bak";
                if (!File.Exists(bak)) File.Copy(scriptPath, bak);
            }
            File.WriteAllLines(outPath, newLines, new UTF8Encoding(false));

            updated++;
            report.AppendLine(index + " | " + GetStr(npc, "Name") + " | " + fileName + " | 按钮=" + btnCount);
        }

        string reportPath = Path.Combine(root, @"Tools\Mir2NPCScriptGen\updated_existing_report.txt");
        File.WriteAllText(reportPath, report.ToString(), new UTF8Encoding(false));
        Console.WriteLine((dryRun ? "[DRY RUN] " : "") + "更新 Main 对话的脚本数: " + updated);
        Console.WriteLine("报告: " + reportPath);
        return 0;
    }

    /// <summary>修复早期用富文本 NPC 名字当文件名产生的怪路径：改用 Title 做文件名。</summary>
    private static int FixWeirdNames(string root, bool dryRun)
    {
        string npcInfoPath = Path.Combine(root, @"Web_Mir2.Server.Version\ExcelConfig\NPCInfo.json");
        string scriptRoot = Path.Combine(root, @"Web_Mir2.Server.Version\Envir\NPCs");
        var arr = JsonNode.Parse(File.ReadAllText(npcInfoPath))!.AsArray();

        int fixedCount = 0;
        foreach (var node in arr)
        {
            if (node is not JsonObject npc) continue;
            int index = GetInt(npc, "Index");
            string fileName = GetStr(npc, "FileName");
            if (!fileName.StartsWith("原始传奇/")) continue;

            string oldPath = Path.Combine(scriptRoot, fileName + ".txt");
            if (!File.Exists(oldPath)) continue;

            string leaf = Path.GetFileNameWithoutExtension(fileName);
            // 只处理含富文本残迹的怪文件名
            if (!Regex.IsMatch(leaf, @"_br_|<|>|│|┌|└|┐|┘")) continue;

            var os = npc["OriginSettings"] as JsonObject;
            string title = os == null ? "" : GetStr(os, "Title");
            string nameForFile = string.IsNullOrWhiteSpace(title) ? GetStr(npc, "Name") : title;
            string safeName = SanitizeFileName(nameForFile);
            if (string.IsNullOrWhiteSpace(safeName)) safeName = "NPC" + index;

            string newRel = "原始传奇/" + safeName + "_" + index;
            string newPath = Path.Combine(scriptRoot, newRel + ".txt");

            if (!dryRun)
            {
                if (File.Exists(newPath)) File.Delete(newPath);
                File.Move(oldPath, newPath);
                npc["FileName"] = newRel;
            }
            fixedCount++;
            Console.WriteLine(index + " | " + leaf + " -> " + safeName + "_" + index);
        }

        if (!dryRun)
        {
            var opts = new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
            File.WriteAllText(npcInfoPath, arr.ToJsonString(opts), new UTF8Encoding(false));
        }
        Console.WriteLine((dryRun ? "[DRY RUN] " : "") + "修复怪文件名数: " + fixedCount);
        return 0;
    }

    /// <summary>
    /// 替换 [@Main] 段的对话与按钮为原始传奇内容：
    /// - 对话：整段替换为 depiction。
    /// - 按钮：buttonRaw 非空时按原始按钮重建（"0"→只有离开）；buttonRaw 空时保留原按钮（复杂脚本如传送员）。
    /// 控制行(#开头)、空行、其它页保持原样。
    /// </summary>
    private static string[] ReplaceMainPage(string[] lines, string depiction, string buttonRaw,
                                            out bool changed, out int btnCount)
    {
        changed = false;
        btnCount = 0;
        var result = new List<string>();
        int i = 0;
        bool inMain = false;

        var depLines = depiction.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                                .Select(CleanText)
                                .Where(s => !string.IsNullOrWhiteSpace(s))
                                .ToList();

        // buttonRaw 非空（含 "0"）就重建按钮；空字符串保留原按钮
        bool replaceButtons = !string.IsNullOrWhiteSpace(buttonRaw);
        var buttons = replaceButtons ? ExtractButtons(buttonRaw) : new List<string>();
        var newButtons = new List<string>();
        if (replaceButtons)
        {
            for (int b = 0; b < buttons.Count; b++)
                newButtons.Add("<" + buttons[b] + "/" + MapButtonAction(buttons[b], b + 1) + ">");
            bool hasExit = buttons.Any(x => x.Contains("离开") || x.Contains("退出") || x.Contains("返回") || x.Contains("关闭"));
            if (!hasExit) newButtons.Add("<离开/@exit>");
            btnCount = newButtons.Count;
        }

        bool injected = false;
        while (i < lines.Length)
        {
            string line = lines[i];
            string trimmed = line.Trim();
            if (trimmed.StartsWith("[@", StringComparison.Ordinal))
            {
                inMain = string.Equals(trimmed, "[@Main]", StringComparison.OrdinalIgnoreCase);
                result.Add(line);
                i++;
                continue;
            }

            if (inMain)
            {
                bool isButton = Regex.IsMatch(trimmed, @"<.*/@.*>");
                bool isControl = trimmed.StartsWith("#", StringComparison.Ordinal);
                bool isBlank = string.IsNullOrWhiteSpace(trimmed);

                if (!isButton && !isControl && !isBlank)
                {
                    // 对话行：整段替换为原始对话，并在其后注入新按钮（如需替换按钮）
                    if (!injected)
                    {
                        result.AddRange(depLines);
                        if (replaceButtons) result.AddRange(newButtons);
                        injected = true;
                        changed = true;
                        // 若替换按钮，跳过后续所有旧按钮行
                    }
                    i++;
                    continue;
                }

                if (isButton && replaceButtons && injected)
                {
                    i++; // 丢弃旧按钮行（新按钮已在对话后注入）
                    continue;
                }
            }

            result.Add(line);
            i++;
        }
        return result.ToArray();
    }
}
