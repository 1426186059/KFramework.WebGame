using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mir.Map;
using MapExtract2;
using Mir.Lib;

/// <summary>
/// 端到端验证：用蒸馏工具的真实代码跑一遍 0.map（比奇省），把产物写到 Mir2Res/Map/0，
/// 再解码蒸馏出的 .Lib，和 0.map 里每个格子实际引用的 (库编号, 图索引) 逐项核对：
///  1) MapUsageExtractor 有没有漏掉格子真正要画的图（漏掉→蒸馏成 17 字节占位→游戏缺图）；
///  2) 蒸馏库里 used 的图是否真在、且字节与原版 .Lib 逐字节一致（验证“和原版一致”）。
/// </summary>
class Program
{
    const string ClientRoot = @"D:\OpenSource\Crystal\Build\Client\Debug";
    const string ResRoot = @"D:\OpenSource\KFramework.WebGame\KFramework.Mir\WebGame.Mir2\Mir2Res";
    const string MapName = "0";
    const int DoorFrameSafety = 8;
    const string MapPrefix = "Data/Map/";

    static int Main()
    {
        string mapFile = Path.Combine(ClientRoot, "Map", MapName + ".map");
        if (!File.Exists(mapFile)) { Console.WriteLine("地图不存在: " + mapFile); return 1; }

        // 1) 解析 0.map（与蒸馏工具同一份 MapReader）
        var mr = new MapReader(File.ReadAllBytes(mapFile));
        Console.WriteLine($"[地图] 格式={mr.MapFormatName} Type={mr.MapTypeId} 尺寸={mr.Width}x{mr.Height}");

        // 2) 工具自带的 MapUsageExtractor 收集 used
        var used = MapUsageExtractor.Extract(mr, DoorFrameSafety);

        // 3) 运行蒸馏（与 DistillOneMapToDisk 完全一致），把产物写到 Mir2Res/Map/0
        DistillToDisk(mr, used);

        // 4) 独立扫描每个格子“实际绘制”的基础 (库,图)，查 MapUsageExtractor 是否漏
        var referenced = ScanReferencedBase(mr);
        int gap = 0;
        foreach (var kv in referenced)
        {
            if (!used.TryGetValue(kv.Key, out var set))
            {
                Console.WriteLine($"[提取器漏库] lib#{kv.Key} 被格子引用但 MapUsageExtractor 未登记（整库会被蒸馏成占位，运行时 404）");
                gap += kv.Value.Count;
                continue;
            }
            foreach (var idx in kv.Value)
                if (!set.Contains(idx))
                {
                    Console.WriteLine($"[提取器漏图] lib#{kv.Key} idx={idx} 被格子绘制引用，但蒸馏未包含（会变占位→游戏缺图）");
                    gap++;
                }
        }
        Console.WriteLine($"[引用] 独立扫描基础图数={referenced.Values.Sum(s => s.Count)}；提取器收集图数={used.Values.Sum(s => s.Count)}；漏掉={gap}");

        // 5) 解码蒸馏产物，逐项核对 used 图是否真在 + 字节一致
        int libOK = 0, missingLib = 0;
        int totalUsed = used.Values.Sum(s => s.Count);
        int realKept = 0, realKeptBytesDiff = 0, lostReal = 0, benignEmpty = 0, distillExtra = 0;
        int srcEmpty17 = 0, srcOutOfRange = 0, srcUnknown = 0;
        var lostSamples = new List<string>();
        var extraSamples = new List<string>();
        var missingLibs = new List<string>();
        // 按库诊断：used 最大索引是否超过源库 Count（疑似拆分库只映射了其中一个文件）
        var splitWarn = new List<string>();

        foreach (var kv in used)
        {
            int libIndex = kv.Key;
            string? rel = MapLibPathTable.GetRelPath(libIndex);
            if (rel == null) { Console.WriteLine($"[无路径] lib#{libIndex} 在 MapLibPathTable 无对应路径（客户端也是空槽，正常跳过）"); continue; }

            string relNoDataMap = rel.StartsWith(MapPrefix, StringComparison.OrdinalIgnoreCase) ? rel.Substring(MapPrefix.Length) : rel;
            string distillPath = Path.Combine(ResRoot, "Map", MapName, relNoDataMap + ".Lib");
            string? srcLib = ResolveSourceLib(rel);

            if (!File.Exists(distillPath)) { missingLib++; missingLibs.Add(distillPath); Console.WriteLine($"[蒸馏库缺失] {distillPath}"); continue; }

            using var dLib = new MirLibReader(distillPath);
            MirLibReader? sLib = srcLib != null ? new MirLibReader(srcLib) : null;
            int srcCount = sLib?.Count ?? -1;
            if (srcLib == null) Console.WriteLine($"[源库缺失] {rel}.Lib（跳过字节比对）");

            if (sLib != null && dLib.Count != sLib.Count)
                Console.WriteLine($"[数量错位] lib#{libIndex} ({rel}) 源={sLib.Count} 蒸馏={dLib.Count}");

            foreach (var idx in kv.Value)
            {
                // 蒸馏侧：是否真有图（>17 字节）
                bool dReal = idx >= 0 && idx < dLib.Count && dLib.GetImageLength(idx) > 17;
                // 源侧：是否真有图（在范围内且装得下且 >17 字节）
                bool sReal = false;
                if (sLib != null && idx >= 0 && idx < sLib.Count && sLib.ImageFits(idx))
                    sReal = sLib.GetImageLength(idx) > 17;

                if (dReal && sReal)
                {
                    byte[] d = dLib.ReadImageBytes(idx);
                    byte[] s = sLib!.ReadImageBytes(idx);
                    if (BytesEqual(d, s)) realKept++;
                    else { realKeptBytesDiff++; if (realKeptBytesDiff <= 10) lostSamples.Add($"[字节不一致] lib#{libIndex} ({rel}) idx={idx} 蒸馏={d.Length} 源={s.Length}"); }
                }
                else if (dReal && !sReal)
                {
                    // 蒸馏有图，但源侧是空/越界——蒸馏多保留了一张（无害），仅计数
                    distillExtra++;
                    if (extraSamples.Count < 10) extraSamples.Add($"[蒸馏多保留] lib#{libIndex} ({rel}) idx={idx} 源侧状态=空/越界");
                }
                else if (!dReal && sReal)
                {
                    // ★ 真正“丢图”：源有真实图，蒸馏却成了占位 ★
                    lostReal++;
                    if (lostSamples.Count < 10) lostSamples.Add($"[★丢图] lib#{libIndex} ({rel}) idx={idx} 源长度={sLib!.GetImageLength(idx)} 蒸馏=17字节占位");
                }
                else // !dReal && !sReal：源本身就是空/越界 → 蒸馏如实写 17 字节，原版也如此，无害
                {
                    benignEmpty++;
                    if (sLib == null) srcUnknown++;
                    else if (idx < 0 || idx >= sLib.Count) srcOutOfRange++;
                    else srcEmpty17++;
                }
            }
            sLib?.Dispose();

            // 拆分库诊断：used 里最大索引超过源库 Count，且确有越界引用 → 可能该 libIndex 在客户端由多个 .Lib 文件拼接
            if (srcCount > 0 && kv.Value.Count > 0)
            {
                int usedMax = kv.Value.Max();
                if (usedMax >= srcCount)
                {
                    int overCount = kv.Value.Count(i => i >= srcCount);
                    splitWarn.Add($"  lib#{libIndex} ({rel}) 源库Count={srcCount} 但 used 最大索引={usedMax} 越界引用数={overCount}");
                }
            }
            libOK++;
        }

        Console.WriteLine("=== 汇总 ===");
        Console.WriteLine($"  used 引用图数={totalUsed}");
        Console.WriteLine($"  蒸馏库数={libOK}  缺失库={missingLib}");
        Console.WriteLine($"  源有图且蒸馏一致(realKept)={realKept}");
        Console.WriteLine($"  源有图但字节不一致={realKeptBytesDiff}");
        Console.WriteLine($"  ★ 源有真实图却被蒸馏成占位(真丢图)={lostReal}");
        Console.WriteLine($"  源本就空/越界→蒸馏如实写占位(无害)={benignEmpty}  [其中:源空17字节={srcEmpty17} 源越界={srcOutOfRange} 源库缺失无法比对={srcUnknown}]");
        Console.WriteLine($"  蒸馏多出(源侧无图)={distillExtra}");
        Console.WriteLine($"  提取器漏图(used 未覆盖格子引用)={gap}");
        if (lostSamples.Count > 0) Console.WriteLine("详情(前10):\n  " + string.Join("\n  ", lostSamples));
        if (extraSamples.Count > 0) Console.WriteLine("蒸馏多保留样例:\n  " + string.Join("\n  ", extraSamples));
        if (splitWarn.Count > 0)
            Console.WriteLine($"[可能的拆分库缺口] 以下库 used 最大索引超过单一源文件 Count（需确认客户端是否由多文件拼接，若是则高索引在游戏中也会缺图）:\n" + string.Join("\n", splitWarn));
        if (missingLibs.Count > 0) Console.WriteLine("缺失蒸馏库:\n  " + string.Join("\n  ", missingLibs));

        bool ok = lostReal == 0 && realKeptBytesDiff == 0 && gap == 0 && missingLib == 0;
        Console.WriteLine(ok
            ? "结论：蒸馏产物与 0.map 引用一一对应，所有真实图均逐字节一致，未丢图 ✅"
            : "结论：发现不一致 ❌（见上）");

        // 6) 端到端独立验证：绕过工具自身的解析与 used，直接从原始字节重算 Draw 请求，
        //    拿这些请求去蒸馏产物里逐个查证（这才是真正回答“Lib 内容与 .map 是否一致”的验证）
        byte[] rawBytes = File.ReadAllBytes(mapFile);
        long expectedSize = 8L + (long)mr.Width * mr.Height * RawDrawVerifier.CellSize;
        Console.WriteLine($"[布局校验]  mapFile={rawBytes.Length}B  预期(8+w*h*{RawDrawVerifier.CellSize})={expectedSize}B  一致={rawBytes.Length == expectedSize}");

        // 诊断：为什么蒸馏会额外产出 WemadeMir3 的库
        RawDrawVerifier.DumpLayerIndexStats(rawBytes);

        // 基础请求 = 每格每帧必画 -> 缺图肉眼可见；扩展请求 = 动画/门 -> 只在播放时可见
        var baseReq = RawDrawVerifier.ScanBaseRequests(rawBytes);
        var extReq = RawDrawVerifier.ScanExtraRequests(rawBytes);
        Console.WriteLine($"[请求-基础] 每格必画: {baseReq.Count} 个库 / {baseReq.Values.Sum(s => s.Count)} 个 (库,图)");
        Console.WriteLine($"[请求-扩展] 动画/门: {extReq.Count} 个库 / {extReq.Values.Sum(s => s.Count)} 个 (库,图)");
        RawDrawVerifier.Verify("基础请求(静态可见)", ClientRoot, ResRoot, MapName, baseReq, used);
        RawDrawVerifier.Verify("扩展请求(动画/门)", ClientRoot, ResRoot, MapName, extReq, used);

        return 0;
    }

    // 与 DistillOneMapToDisk 等价的蒸馏+落盘
    static void DistillToDisk(MapReader mr, Dictionary<int, SortedSet<int>> used)
    {
        string clientRoot = ClientRoot.TrimEnd('\\', '/');
        foreach (var kv in used)
        {
            int libIndex = kv.Key;
            string? rel = MapLibPathTable.GetRelPath(libIndex);
            if (rel == null) continue;
            string? srcLib = ResolveSourceLib(rel);
            if (srcLib == null) { Console.WriteLine($"[源库缺失] {rel}.Lib（跳过蒸馏）"); continue; }

            byte[] distilled = LibDistiller.Distill(srcLib, new HashSet<int>(kv.Value));
            string relNoDataMap = rel.StartsWith(MapPrefix, StringComparison.OrdinalIgnoreCase) ? rel.Substring(MapPrefix.Length) : rel;
            string outPath = Path.Combine(ResRoot, "Map", MapName, relNoDataMap + ".Lib");
            Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
            File.WriteAllBytes(outPath, distilled);
        }
        Console.WriteLine($"[蒸馏] 已重新蒸馏 {used.Count} 个库到 Mir2Res/Map/{MapName}");
    }

    // 独立扫描：每个格子“实际绘制”的基础 (库,图)。只取基础帧（不去重动画/门区间），
    // 用来检测 MapUsageExtractor 是否漏掉游戏真正会画的图。
    static Dictionary<int, SortedSet<int>> ScanReferencedBase(MapReader mr)
    {
        var referenced = new Dictionary<int, SortedSet<int>>();
        void Ensure(int lib) { if (!referenced.ContainsKey(lib)) referenced[lib] = new SortedSet<int>(); }
        void Add(int lib, int idx) { if (idx >= 0) { Ensure(lib); referenced[lib].Add(idx); } }

        int w = mr.Width, h = mr.Height;
        var cells = mr.MapCells;
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                var c = cells[x][y];
                if (c == null) continue;

                if (c.BackIndex >= 0)
                {
                    Ensure(c.BackIndex);
                    uint raw = (uint)c.BackImage & 0x1FFFFFFFu;
                    if (raw != 0) Add(c.BackIndex, (int)raw - 1);
                }
                if (c.MiddleIndex >= 0)
                {
                    Ensure(c.MiddleIndex);
                    int idx = (int)c.MiddleImage - 1;
                    if (idx >= 0) Add(c.MiddleIndex, idx);
                }
                if (c.FrontIndex >= 0 && c.FrontIndex != 200)
                {
                    Ensure(c.FrontIndex);
                    uint raw = (uint)c.FrontImage & 0x7FFFu;
                    if (raw != 0) Add(c.FrontIndex, (int)raw - 1);
                }
                if (c.TileAnimationFrames > 0)
                {
                    int baseIdx = c.TileAnimationImage - 1;
                    if (baseIdx >= 0) Add(190, baseIdx);
                }
            }
        return referenced;
    }

    static string? ResolveSourceLib(string rel)
    {
        string src = Path.Combine(ClientRoot, rel + ".Lib");
        if (File.Exists(src)) return src;
        // WemadeMir3 地形子目录回退到父级（与工具 TryResolveSourceLib 一致）
        string[] parts = rel.Split('/');
        for (int i = 0; i < parts.Length - 1; i++)
        {
            string seg = parts[i];
            if (seg.Equals("wood", StringComparison.OrdinalIgnoreCase) ||
                seg.Equals("sand", StringComparison.OrdinalIgnoreCase) ||
                seg.Equals("snow", StringComparison.OrdinalIgnoreCase) ||
                seg.Equals("forest", StringComparison.OrdinalIgnoreCase))
            {
                var l = new List<string>(parts); l.RemoveAt(i);
                string alt = string.Join("/", l);
                string p = Path.Combine(ClientRoot, alt + ".Lib");
                if (File.Exists(p)) return p;
            }
        }
        return null;
    }

    static bool BytesEqual(byte[] a, byte[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
        return true;
    }
}
