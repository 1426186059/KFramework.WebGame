using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mir.Lib;
using MapExtract2;

/// <summary>
/// 端到端独立验证：不复用 MapReader / MapUsageExtractor 的任何结果，
/// 直接从 0.map 的原始字节按 Type100 布局解析，并逐条复刻 GameScene.Draw 的
/// 判空 / 掩码 / 层级逻辑，算出运行时真正会访问的全部 (库编号, 图索引)，
/// 再到蒸馏产物 Lib 里逐个查证：该索引是否真有图、字节是否与原始 Lib 完全一致。
///
/// 关键区分（这是定位“哪些图真的显示不了”的核心）：
///   基础请求 = 每格每帧 Draw 必定访问的索引（Back/Middle/Front 的静态索引），缺图 = 肉眼可见空洞；
///   扩展请求 = 动画帧 / 门开关偏移 / 瓦片动画才会访问到的索引，缺图只在动画播放或开门时可见。
/// </summary>
public static class RawDrawVerifier
{
    /// <summary>Type100 单格字节数，用于校验布局（地图大小 = 头 8 字节 + w*h*CellSize）。</summary>
    public const int CellSize = 26;

    // 单格布局说明（与 MapCode.LoadMapType100 顺序一致，合计 26 字节）：
    // BackIndex(2) BackImage(4) MiddleIndex(2) MiddleImage(2) FrontIndex(2) FrontImage(2)
    // DoorIndex(1) DoorOffset(1) FrontAnimFrame(1) FrontAnimTick(1) MidAnimFrame(1) MidAnimTick(1)
    // TileAnimImage(2) TileAnimOffset(2) TileAnimFrames(1) Light(1)

    /// <summary>
    /// 基础请求：每格 Draw 必定访问的 (库,索引)。缺图会直接表现为地图上的空洞/马赛克。
    /// </summary>
    public static Dictionary<int, SortedSet<int>> ScanBaseRequests(byte[] b)
    {
        var req = new Dictionary<int, SortedSet<int>>();
        void Req(int lib, int idx)
        {
            if (lib < 0 || idx < 0) return;
            if (!req.TryGetValue(lib, out var s)) { s = new SortedSet<int>(); req[lib] = s; }
            s.Add(idx);
        }

        int w = BitConverter.ToInt16(b, 4);
        int h = BitConverter.ToInt16(b, 6);
        int off = 8;

        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                short backIndex = BitConverter.ToInt16(b, off); off += 2;
                int backImage = BitConverter.ToInt32(b, off); off += 4;
                short midIndex = BitConverter.ToInt16(b, off); off += 2;
                short midImage = BitConverter.ToInt16(b, off); off += 2;
                short frontLibIndex = BitConverter.ToInt16(b, off); off += 2;
                short frontImage = BitConverter.ToInt16(b, off); off += 2;
                byte doorIndex = (byte)(b[off++] & 0x7F);
                byte doorOffset = b[off++];
                byte frontAnimFrame = b[off++];
                byte frontAnimTick = b[off++];
                byte midAnimFrame = b[off++];
                byte midAnimTick = b[off++];
                short tileAnimImage = BitConverter.ToInt16(b, off); off += 2;
                short tileAnimOffset = BitConverter.ToInt16(b, off); off += 2;
                byte tileAnimFrames = b[off++];
                byte light = b[off++];

                // Back — GameScene.cs:11017-11041
                if (backImage != 0 && backIndex != -1)
                {
                    int idx = (int)((uint)backImage & 0x1FFFFFFFu) - 1;
                    if (idx >= 0) Req(backIndex, idx);
                }
                // Middle — GameScene.cs:11045-11060（静态帧）
                int mid = midImage - 1;
                if (mid >= 0 && midIndex != -1) Req(midIndex, mid);
                // Front — GameScene.cs:11064-11099（静态帧）
                int fr = (int)((uint)frontImage & 0x7FFFu) - 1;
                if (fr >= 0 && frontLibIndex != -1 && frontLibIndex != 200) Req(frontLibIndex, fr);
            }
        }
        return req;
    }

    /// <summary>
    /// 扩展请求：只有动画播放 / 门开关 / 瓦片动画才会访问到的额外索引。
    /// 缺图只在对应动画或开门时可见，静态看地图不会发现。
    /// </summary>
    public static Dictionary<int, SortedSet<int>> ScanExtraRequests(byte[] b)
    {
        var req = new Dictionary<int, SortedSet<int>>();
        void Rng(int lib, int from, int to)
        {
            if (lib < 0) return;
            if (to < from) (from, to) = (to, from);
            if (!req.TryGetValue(lib, out var s)) { s = new SortedSet<int>(); req[lib] = s; }
            for (int i = from; i <= to; i++) if (i >= 0) s.Add(i);
        }

        int w = BitConverter.ToInt16(b, 4);
        int h = BitConverter.ToInt16(b, 6);
        int off = 8;

        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                short backIndex = BitConverter.ToInt16(b, off); off += 2;
                int backImage = BitConverter.ToInt32(b, off); off += 4;
                short midIndex = BitConverter.ToInt16(b, off); off += 2;
                short midImage = BitConverter.ToInt16(b, off); off += 2;
                short frontLibIndex = BitConverter.ToInt16(b, off); off += 2;
                short frontImage = BitConverter.ToInt16(b, off); off += 2;
                byte doorIndex = (byte)(b[off++] & 0x7F);
                byte doorOffset = b[off++];
                byte frontAnimFrame = b[off++];
                byte frontAnimTick = b[off++];
                byte midAnimFrame = b[off++];
                byte midAnimTick = b[off++];
                short tileAnimImage = BitConverter.ToInt16(b, off); off += 2;
                short tileAnimOffset = BitConverter.ToInt16(b, off); off += 2;
                byte tileAnimFrames = b[off++];
                byte light = b[off++];

                // Middle 动画帧（运行时会连续访问 idx..idx+frames）
                int mid = midImage - 1;
                if (mid >= 0 && midIndex != -1 && midAnimFrame > 0)
                    Rng(midIndex, mid + 1, mid + midAnimFrame);

                // Front 动画帧 + 门偏移
                int fr = (int)((uint)frontImage & 0x7FFFu) - 1;
                if (fr >= 0 && frontLibIndex != -1 && frontLibIndex != 200)
                {
                    int anim = frontAnimFrame & 0x7F;
                    if (anim > 0) Rng(frontLibIndex, fr + 1, fr + anim);
                    if (doorIndex > 0 && doorOffset > 0)
                    {
                        int doorMax = (doorOffset + 1) * doorOffset + 8 * (doorOffset + 1);
                        Rng(frontLibIndex, fr + 1, fr + doorMax);
                    }
                }

                // 瓦片动画 — 固定走库 190
                if (tileAnimFrames > 0)
                {
                    int baseIdx = tileAnimImage - 1;
                    if (baseIdx >= 0)
                    {
                        int step = tileAnimOffset ^ 0x2000;
                        Rng(190, baseIdx, baseIdx + step * (tileAnimFrames - 1));
                    }
                }
            }
        }
        return req;
    }

    /// <summary>把每个请求拿到蒸馏 Lib 里去对照源 Lib 查证。</summary>
    public static bool Verify(string label, string clientRoot, string resRoot, string mapName,
                              Dictionary<int, SortedSet<int>> requests,
                              Dictionary<int, SortedSet<int>> used)
    {
        Console.WriteLine();
        Console.WriteLine($"========== 端到端验证 [{label}]（原始字节 → Draw 请求 → 查蒸馏 Lib）==========");

        int totalReq = 0, okReal = 0, byteDiff = 0, lostReal = 0, benign = 0, extra = 0;
        int notInUsed = 0, missingLib = 0, noPath = 0, oobBenign = 0, srcEmptyBenign = 0;
        var empties = new List<string>();

        foreach (var kv in requests.OrderBy(k => k.Key))
        {
            int lib = kv.Key;
            string? rel = MapLibPathTable.GetRelPath(lib);
            if (rel == null) { noPath++; continue; }

            const string prefix = "Data/Map/";
            string relNo = rel.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? rel.Substring(prefix.Length) : rel;
            string distillPath = Path.Combine(resRoot, "Map", mapName, relNo + ".Lib");
            string srcPath = Path.Combine(clientRoot, rel + ".Lib");

            if (!File.Exists(distillPath))
            {
                missingLib++;
                Console.WriteLine($"  [蒸馏库缺失] lib#{lib} ({rel}) -> {distillPath}");
                continue;
            }

            using var dLib = new MirLibReader(distillPath);
            MirLibReader? sLib = File.Exists(srcPath) ? new MirLibReader(srcPath) : null;

            int thisOK = 0, thisLost = 0, thisDiff = 0, thisBenign = 0, thisOOB = 0, thisExtra = 0, thisEmpty = 0;

            foreach (var idx in kv.Value)
            {
                totalReq++;
                if (!used.TryGetValue(lib, out var uset) || !uset.Contains(idx)) notInUsed++;

                bool dHas = idx >= 0 && idx < dLib.Count && dLib.GetImageLength(idx) > 17;
                bool sHas = sLib != null && idx >= 0 && idx < sLib.Count && sLib.ImageFits(idx) && sLib.GetImageLength(idx) > 17;

                if (dHas && sHas)
                {
                    byte[] A = dLib.ReadImageBytes(idx);
                    byte[] B = sLib!.ReadImageBytes(idx);
                    if (BytesEqual(A, B)) { okReal++; thisOK++; }
                    else
                    {
                        byteDiff++; thisDiff++;
                        if (byteDiff <= 5) Console.WriteLine($"  [字节不一致] lib#{lib} ({rel}) idx={idx} 蒸馏={A.Length}B 源={B.Length}B");
                    }
                }
                else if (!dHas && sHas)
                {
                    lostReal++; thisLost++;
                    if (lostReal <= 5) Console.WriteLine($"  [★真丢图] lib#{lib} ({rel}) idx={idx} 源有真图({sLib!.GetImageLength(idx)}B) 蒸馏却是占位");
                }
                else if (dHas && !sHas) { extra++; thisExtra++; }
                else
                {
                    benign++; thisBenign++;
                    bool isOob = sLib != null && (idx < 0 || idx >= sLib.Count);
                    if (isOob) { oobBenign++; thisOOB++; }
                    else { srcEmptyBenign++; thisEmpty++; }
                    if (empties.Count < 8) empties.Add($"lib#{lib}({rel}) idx={idx}{(isOob ? " [源越界]" : " [源空图]}")}");
                }
            }

            Console.WriteLine($"  lib#{lib} ({rel}) 请求={kv.Value.Count} 有图一致={thisOK} 真丢图={thisLost} 字节差={thisDiff} 缺图={thisBenign}(源越界{thisOOB}/源空{thisEmpty}) 多保留={thisExtra}");
            sLib?.Dispose();
        }

        Console.WriteLine($"--- [{label}] 汇总 ---");
        Console.WriteLine($"  请求总数={totalReq}");
        Console.WriteLine($"  蒸馏有真图且字节与源一致={okReal}");
        Console.WriteLine($"  字节不一致={byteDiff}");
        Console.WriteLine($"  ★源有真图但蒸馏是占位(蒸馏丢图)={lostReal}");
        Console.WriteLine($"  取不到图(源本身就没有)={benign}  [源越界={oobBenign} 源Lib该槽位空={srcEmptyBenign}]");
        Console.WriteLine($"  蒸馏多保留={extra}  未被 used 覆盖={notInUsed}   缺失库={missingLib}");
        if (benign > 0 && empties.Count > 0) Console.WriteLine("  缺图样例: " + string.Join(", ", empties));

        bool ok = lostReal == 0 && byteDiff == 0 && notInUsed == 0 && missingLib == 0;
        Console.WriteLine(ok
            ? $"[{label}] 结论：蒸馏无误 ✅ —— 取不到的图在源 Lib 里本来就没有（与蒸馏无关）"
            : $"[{label}] 结论：发现问题 ❌（见上）");
        return ok;
    }

    /// <summary>
    /// 诊断：dump 每格三层索引(BackIndex/MiddleIndex/FrontIndex)的取值分布，
    /// 重点揪出落到 >=200(WemadeMir3 映射区间)的索引：统计出现次数，以及其中"对应层确实有图"的次数。
    /// 用来解释"蒸馏工具为什么会额外提取 WemadeMir3 的库"。
    /// </summary>
    public static void DumpLayerIndexStats(byte[] b)
    {
        int w = BitConverter.ToInt16(b, 4);
        int h = BitConverter.ToInt16(b, 6);
        int off = 8;

        var back = new Dictionary<short, int>();
        var mid = new Dictionary<short, int>();
        var front = new Dictionary<short, int>();
        // (层,索引) -> (出现格数, 其中该层"有图"的格数)
        var mir3 = new Dictionary<(string layer, short idx), (int cells, int withImage)>();

        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                short backIndex = BitConverter.ToInt16(b, off); off += 2;
                int backImage = BitConverter.ToInt32(b, off); off += 4;
                short midIndex = BitConverter.ToInt16(b, off); off += 2;
                short midImage = BitConverter.ToInt16(b, off); off += 2;
                short frontLibIndex = BitConverter.ToInt16(b, off); off += 2;
                short frontImage = BitConverter.ToInt16(b, off); off += 2;
                byte doorIndex = (byte)(b[off++] & 0x7F);
                byte doorOffset = b[off++];
                byte frontAnimFrame = b[off++];
                byte frontAnimTick = b[off++];
                byte midAnimFrame = b[off++];
                byte midAnimTick = b[off++];
                short tileAnimImage = BitConverter.ToInt16(b, off); off += 2;
                short tileAnimOffset = BitConverter.ToInt16(b, off); off += 2;
                byte tileAnimFrames = b[off++];
                byte light = b[off++];

                Inc(back, backIndex);
                Inc(mid, midIndex);
                Inc(front, frontLibIndex);

                // 记录 >=200（MapLibPathTable 里 WemadeMir3 / ShandaMir3 的映射区间）
                if (backIndex >= 200)
                {
                    bool has = ((uint)backImage & 0x1FFFFFFFu) != 0;
                    AddMir3(mir3, "Back", backIndex, has);
                }
                if (midIndex >= 200) AddMir3(mir3, "Middle", midIndex, midImage - 1 >= 0);
                if (frontLibIndex >= 200 && frontLibIndex != 200)
                    AddMir3(mir3, "Front", frontLibIndex, ((uint)frontImage & 0x7FFFu) != 0);
            }
        }

        Console.WriteLine();
        Console.WriteLine("========== 层索引分布诊断（为什么蒸馏会多出 WemadeMir3）==========");
        Console.WriteLine($"  地图 {w}x{h} = {w * h} 格");
        Console.WriteLine($"  BackIndex 取值: " + Fmt(back));
        Console.WriteLine($"  MiddleIndex 取值: " + Fmt(mid));
        Console.WriteLine($"  FrontIndex 取值(top12): " + Fmt(front, 12));

        if (mir3.Count == 0)
        {
            Console.WriteLine("  没有任何格子使用 >=200 的索引 —— 不会触发 WemadeMir3 映射");
            return;
        }

        Console.WriteLine();
        Console.WriteLine("  [!] 落到 >=200 区间的索引（会被 MapLibPathTable 映射成 WemadeMir3/ShandaMir3 的库）:");
        foreach (var kv in mir3.OrderBy(k => k.Key.idx).ThenBy(k => k.Key.layer))
        {
            string? rel = MapLibPathTable.GetRelPath(kv.Key.idx);
            Console.WriteLine($"      {kv.Key.layer,-6} index={kv.Key.idx,-5} -> {rel,-45} 出现格数={kv.Value.cells,-6} 其中该层有图={kv.Value.withImage}");
        }
        int totalCells = mir3.Values.Sum(v => v.cells);
        int totalWithImg = mir3.Values.Sum(v => v.withImage);
        Console.WriteLine($"  合计 {totalCells} 个格子引用了 >=200 的库索引，其中真正有图的只有 {totalWithImg} 个");
        Console.WriteLine("  → 若 withImage=0，说明这些索引是'索引设了但本格没图'的噪声格子；");
        Console.WriteLine("    MapUsageExtractor 里 EnsureLib() 只要索引>=0 就登记该库，因此会把它们一并蒸馏出来。");
    }

    private static void Inc(Dictionary<short, int> d, short k)
    {
        d[k] = d.TryGetValue(k, out int v) ? v + 1 : 1;
    }

    private static void AddMir3(Dictionary<(string, short), (int, int)> d, string layer, short idx, bool hasImage)
    {
        var key = (layer, idx);
        if (!d.TryGetValue(key, out var cur)) cur = (0, 0);
        cur.Item1++;
        if (hasImage) cur.Item2++;
        d[key] = cur;
    }

    private static string Fmt(Dictionary<short, int> d, int take = 8)
    {
        if (d.Count == 0) return "(无)";
        var items = d.OrderByDescending(k => k.Value).Take(take)
            .Select(k => $"{k.Key}:{k.Value}");
        return string.Join("  ", items) + (d.Count > take ? $"  ...(共 {d.Count} 种)" : "");
    }

    private static bool BytesEqual(byte[] a, byte[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
        return true;
    }
}
