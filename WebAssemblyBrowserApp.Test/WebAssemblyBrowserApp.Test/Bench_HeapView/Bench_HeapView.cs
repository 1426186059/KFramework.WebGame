using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.JavaScript;
using System.Text;
using System.Threading.Tasks;

/// <summary>
/// 模块：验证「GCHandle pin + 裸地址 → JS TypedArray」这条零拷贝路线在本工程是否成立。
/// <para>
/// 思路（与运行时给的 MemoryView 完全不同）：先用 <see cref="GCHandle"/> 把 .NET 数组钉在
/// WASM 线性内存上，再把【内存地址 + 长度】交给 JS，由 JS 用
/// <c>new Uint8Array(wasmMemory.buffer, ptr, len)</c> 建出指向同一块内存的视图 ——
/// 若成立，JS 的读写就直接落在 .NET 数组上，一次 memcpy 都不需要。
/// </para>
/// <para>
/// 它成立有三个前提，本模块逐条实测：
/// <b>① JS 能拿到 WASM 的 memory.buffer</b>；
/// <b>② AddrOfPinnedObject() 返回的地址确实落在那块 buffer 内</b>（不只是"非 0"）；
/// <b>③ 地址还得按视图元素宽度对齐</b>（Uint32Array 之类要求 byteOffset 是宽度的整数倍）。
/// </para>
/// <para>
/// 【① 的入口已经改过一次，别再退回扫全局】
/// Bench_MemoryView 的 ⑨ 实测：.NET 的 BrowserApp 不把 wasmMemory / HEAPU8 挂在全局，扫全局必然一无所获；
/// ⑩⑪ 实测：任意一个 MemoryView 的 <c>_unsafe_create_view().buffer</c> 才是<b>整块 WASM 线性内存</b>。
/// 所以本模块把入口换成"借引子 MemoryView 取 buffer"，①-a 只保留扫全局作对照。
/// </para>
/// <para>
/// 判据一律是<b>C# 回读自己的数组</b>：交给 JS 用某条路线写，再看自己这边字节动没动。
/// 只报"跑通了"而字节没动，就是最坑的那种情况 —— 看着有结果、其实没干活。
/// </para>
/// </summary>
public sealed class Bench_HeapView : IBenchModule
{
    public string Name => "HeapView 零拷贝方案（GCHandle pin + 裸地址 → TypedArray）";

    public string Summary =>
        "先看 JS 用什么途径拿到 WASM memory.buffer（扫全局 vs 借 MemoryView 引子），" +
        "再校验 pin 到的地址是否落在 buffer 内、是否对齐；然后 As() 建共享视图让 JS 写入、" +
        "To() 拿副本让 JS 改，各自回读 C# 数组；最后实测堆增长会不会让视图失效 —— " +
        "回答「这套零拷贝在本工程到底能不能用、能用到什么程度」。";

    public string Page => "heapview";

    /// <summary>探测用的数组长度。8 字节足够看出"写没写"。</summary>
    private const int Len = 8;

    /// <summary>⑦ 里每次胁迫堆增长的块大小（8MB）。</summary>
    private const int GrowChunk = 8 * 1024 * 1024;

    /// <summary>
    /// ⑦ 里最多分配多少块。堆当前约 55MB，12×8MB≈96MB 足以逼出 grow；
    /// 再往上就有真的把页面跑 OOM 的风险，到此为止。
    /// </summary>
    private const int GrowMaxRounds = 12;

    public Task<string> RunAsync()
    {
        var rows = new List<(string Way, string Seen, string Verdict)>
        {
            ProbeGlobal(),      // ①-a 对照：扫全局（已被 ⑨ 判定为必然失败）
            ProbeViaKey(),      // ①-b 正解：借引子 MemoryView 取 buffer
            ProbePin(),         // ② 钉得住吗、地址落在 buffer 内吗
            ProbeZeroCopy(),    // ③ 核心：As() 的视图写入，C# 看不看得到
            ProbeCopy(),        // ④ 兜底：To() 的副本，改了不影响 C#
            ProbeCrossCall(),   // ⑤ 跨一次同步调用还有效吗
            ProbeAlignment(),   // ⑥ 更宽的视图：对齐与宽度校验（int[] + Uint32Array）
            ProbeGrow(),        // ⑦ 生存期的硬伤：堆增长会不会让 detach、视图静默失效
            ProbeOtherArray(),  // ⑧ 那块 buffer 是整块线性内存吗（跨数组验证）
        };

        var sb = new StringBuilder();
        sb.Append("<div class='card'><h3>HeapView 零拷贝方案探测（byte[").Append(Len).Append("]，C# 回读验证）</h3>");
        sb.Append("<p class='muted'>每条都把一份数组钉住后交给 JS，JS 用该路线写一遍，" +
                  "<b>C# 再回读自己的数组</b>：字节真的变了，才说明这条路线把数据落回了 .NET 侧。</p>");
        sb.Append("<table><thead><tr><th>路线</th><th>JS 侧回报</th><th>C# 回读结论</th></tr></thead><tbody>");

        foreach ((string way, string seen, string verdict) in rows)
        {
            // 标红的只有"该成却没成"的那几行 —— 那是最容易误判的地方
            bool suspect = verdict.Contains('✘');
            sb.Append(suspect ? "<tr class='blocked'>" : "<tr>");
            sb.Append("<td>").Append(BenchKit.Escape(way)).Append("</td>");
            sb.Append("<td class='muted'>").Append(BenchKit.Escape(seen)).Append("</td>");
            sb.Append("<td>").Append(BenchKit.Escape(verdict)).Append("</td>");
            sb.Append("</tr>");
        }

        sb.Append("</tbody></table>");
        sb.Append("<p class='muted'>").Append(Conclusion(rows)).Append("</p>");
        sb.Append("</div>");

        return Task.FromResult(sb.ToString());
    }

    /// <summary>
    /// ①-a 对照：只扫全局找 memory.buffer。<b>预期一无所获</b>（⑨ 的结论）。
    /// 留着它是为了让"必须借道 MemoryView"在本页就有对照，而不是靠另一个模块的记忆。
    /// </summary>
    private static (string, string, string) ProbeGlobal()
    {
        const string way = "①-a 扫全局找 memory.buffer（对照）";

        string seen;
        try
        {
            seen = JSBind_HeapView.ProbeGlobal();
        }
        catch (Exception e)
        {
            return (way, "不可用：" + e.GetType().Name, "无法执行");
        }

        bool found = !seen.Contains("一个都没有", StringComparison.Ordinal);
        return (way, seen,
            found
                ? "全局竟然摸到了堆视图 → 可以直接建视图（与 ⑨ 的结论不符，值得留意）"
                : "扫不到 ✓（符合预期）—— .NET 不暴露裸堆入口，这条路不通，得看 ①-b");
    }

    /// <summary>①-b 正解：借引子 MemoryView 取 buffer —— 本方案真正的入口。</summary>
    private static (string, string, string) ProbeViaKey()
    {
        const string way = "①-b 借 MemoryView 引子取 buffer（正解）";

        string seen;
        try
        {
            seen = JSBind_HeapView.ProbeViaKey(HeapView<byte>.Key);
        }
        catch (Exception e)
        {
            return (way, "不可用：" + e.GetType().Name + "：" + e.Message, "无法执行 ✘");
        }

        bool ok = seen.Contains("拿到 memory.buffer", StringComparison.Ordinal);
        return (way, seen,
            ok
                ? "拿得到 ✓ → 可以往下建视图"
                : "连引子这条路都拿不到 ✘ —— 整套零拷贝方案在本工程不成立");
    }

    /// <summary>
    /// ② GCHandle 能否钉住数组，以及拿到的地址是否<b>落在那块 buffer 之内</b>。
    /// 只看"ptr != 0"是不够的：非零却越界，建视图照样失败，
    /// 而那种失败很容易被读成"零拷贝不成立"。
    /// </summary>
    private static (string, string, string) ProbePin()
    {
        const string way = "② GCHandle.Alloc(byte[], Pinned) + 地址落在 buffer 内";

        var buf = new byte[Len];
        string range;
        nint ptr;

        // 构造就可能抛：WASM 上未必支持 GCHandleType.Pinned ——
        // 必须在这里接住，否则整个页面会白屏，而不是干净地报告"这条路走不通"。
        try
        {
            using var heap = new HeapView<byte>(buf);
            ptr = heap.Pointer;
            range = ptr != 0
                ? JSBind_HeapView.CheckRange(HeapView<byte>.Key, ptr, heap.ByteLength)
                : "ptr 为 0，无从校验";
        }
        catch (Exception e)
        {
            return (way, "抛错：" + e.GetType().Name + "：" + e.Message,
                "钉不住 ✘ —— 该运行时不支持 pin，这套零拷贝方案到此为止");
        }

        bool inRange = range.Contains("落在范围内", StringComparison.Ordinal);

        return (way,
            "IsPinned=" + (ptr != 0) + "，AddrOfPinnedObject=0x" + ptr.ToString("X") + "；" + range,
            ptr == 0
                ? "地址是 0 ✘ —— 钉不住（或运行时尚未初始化 WASM 内存），就谈不上零拷贝"
                : inRange
                    ? "钉住成功，且地址落在 buffer 范围内 ✓"
                    : "地址非零但越界 ✘ —— 视图建不出来，别误判成方案不成立");
    }

    /// <summary>
    /// ③ 核心：As() 建出共享视图，让 JS 往里写 —— C# 回读看字节有没有变。
    /// 变了，就证明这个视图真的指向 .NET 数组那块内存，零拷贝成立。
    /// </summary>
    private static (string, string, string) ProbeZeroCopy()
    {
        const string way = "③ As() 共享视图 → JS 写入 → C# 回读";

        var buf = new byte[Len];

        string seen;
        try
        {
            // 构造（pin）与建视图都放进 try：任一步失败都只是这一行不可用，不能拖垮整页
            using var heap = new HeapView<byte>(buf);
            using JSObject view = heap.As("Uint8Array");
            seen = "建视图成功；" + JSBind_HeapView.WriteView(view, 0xee);
        }
        catch (Exception e)
        {
            return (way, "不可用：" + e.GetType().Name + "：" + e.Message, "无法执行 ✘");
        }

        bool allWritten = true;
        for (int i = 0; i < buf.Length; i++)
        {
            if (buf[i] != 0xee) { allWritten = false; break; }
        }

        return (way, seen,
            allWritten
                ? "C# 数组全部变成 0xEE ✓ —— 视图真指向同一块内存，【零拷贝成立】"
                : "C# 数组没变 ✘ —— 视图没落到这个数组上，零拷贝不成立");
    }

    /// <summary>④ 兜底：To() 拿的是副本，JS 改副本不该影响 C# 数组。</summary>
    private static (string, string, string) ProbeCopy()
    {
        const string way = "④ To() 独立副本 → 改副本 → C# 回读";

        var buf = new byte[Len];

        string seen;
        try
        {
            using var heap = new HeapView<byte>(buf);
            using JSObject copy = heap.To("Uint8Array");
            seen = "拿到副本；" + JSBind_HeapView.WriteView(copy, 0xff);
        }
        catch (Exception e)
        {
            return (way, "不可用：" + e.GetType().Name + "：" + e.Message, "无法执行 ✘");
        }

        bool untouched = true;
        for (int i = 0; i < buf.Length; i++)
        {
            if (buf[i] != 0) { untouched = false; break; }
        }

        return (way, seen,
            untouched
                ? "C# 数组仍是全 0 ✓（副本语义 —— 安全兜底，多一次 memcpy）"
                : "C# 数组被改动 ✘（副本不该有这个效果）");
    }

    /// <summary>
    /// ⑤ 建出视图后，中间插一次跨界调用，再用它写 —— 看还有效没有。
    /// <b>注意这一条证明力有限</b>：插的那次调用几乎不分配内存，逼不出 memory.grow，
    /// 所以它只能说明"同一次同步流程内多调几次没事"。真正的生存期风险在 ⑦。
    /// </summary>
    private static (string, string, string) ProbeCrossCall()
    {
        const string way = "⑤ 视图跨一次同步调用还有效吗";

        var buf = new byte[Len];

        string seen;
        try
        {
            using var heap = new HeapView<byte>(buf);
            using JSObject view = heap.As("Uint8Array");
            _ = JSBind_HeapView.ProbeGlobal(); // 中间插一次跨界调用
            seen = "建视图 → 插一次跨界调用 → 再写；" + JSBind_HeapView.WriteView(view, 0x5a);
        }
        catch (Exception e)
        {
            return (way, "不可用：" + e.GetType().Name + "：" + e.Message, "无法执行 ✘");
        }

        return (way, seen,
            buf[0] == 0x5a
                ? "仍可写 ✓（跨同步调用没问题；但这条<b>证明不了</b>跨 await / 跨帧安全，看 ⑦）"
                : "写不进去了 —— 视图已失效 ✘");
    }

    /// <summary>
    /// ⑥ 更宽的视图：<c>int[]</c> 配 <c>Uint32Array</c>。
    /// 这里有两个此前完全没覆盖的坑：
    /// <b>对齐</b> —— Uint32Array 要求 byteOffset 是 4 的整数倍，否则 RangeError；
    /// <b>宽度</b> —— byte[] 却要 Uint32Array，视图长度会被当成 32 位元素个数，直接写到数组外面去
    /// （本类已在 <see cref="HeapView{T}.As"/> 里拦下这种错配）。
    /// </summary>
    private static (string, string, string) ProbeAlignment()
    {
        const string way = "⑥ 更宽的视图：int[] + Uint32Array（对齐 / 宽度校验）";

        // 先看"宽度错配"有没有被拦住：byte[] 要 Uint32Array，必须拒绝。
        // 只【建】视图、不写入 —— 建视图本身不动内存，所以这一步即使真的漏过去了也不会写坏堆。
        string guard;
        try
        {
            using var wrong = new HeapView<byte>(new byte[Len]);
            using JSObject bad = wrong.As("Uint32Array");
            guard = "宽度错配没被拦下 ✘（byte[] 竟能建 Uint32Array 视图，按 4 字节步长会写到数组外）";
            GC.KeepAlive(bad);
        }
        catch (ArgumentException)
        {
            guard = "宽度错配已被拦下 ✓（byte[] 要 Uint32Array 直接抛 ArgumentException）";
        }
        catch (Exception e)
        {
            guard = "宽度错配被拦下（" + e.GetType().Name + "）";
        }

        // 再看对齐的正式用例：int[] 配 Uint32Array，写入后 C# 回读
        var ints = new int[Len];
        string seen;
        try
        {
            using var heap = new HeapView<int>(ints);
            using JSObject view = heap.As("Uint32Array");
            seen = guard + "；" + "addr%4=" + (heap.Pointer % 4) + "；" + JSBind_HeapView.WriteView(view, 0xee);
        }
        catch (Exception e)
        {
            return (way, guard + "；不可用：" + e.GetType().Name + "：" + e.Message, "无法执行 ✘");
        }

        bool allWritten = true;
        for (int i = 0; i < ints.Length; i++)
        {
            if (ints[i] != 0xee) { allWritten = false; break; }
        }

        return (way, seen,
            allWritten
                ? "int[] 全部变成 0xEE ✓ —— 4 字节视图同样零拷贝，地址本身是对齐的"
                : "int[] 没变 ✘ —— 要么地址未按 4 字节对齐、要么视图宽度对不上");
    }

    /// <summary>
    /// ⑦ 生存期的硬伤：WASM 堆增长（memory.grow）会让底层 ArrayBuffer detach，
    /// 挂在它上面的视图随之失效 —— 而且是<b>静默</b>失效：读写既不抛错也不生效，
    /// 单看"没报错"会得出完全相反的结论。这一条此前从未实测过，是本模块最想回答的问题。
    /// <para>
    /// 做法：先把 buffer 与视图跨调用存在 JS 侧，再由 C# 不断分配并持有大块数组胁迫堆增长，
    /// 每步回头查一次状态，最后<b>真的写一次</b>再回读 —— 只有回读才能定生死。
    /// </para>
    /// </summary>
    private static (string, string, string) ProbeGrow()
    {
        const string way = "⑦ 堆增长（memory.grow）后视图还活着吗";

        var target = new byte[Len];
        string seen;
        bool detached = false;

        try
        {
            using var heap = new HeapView<byte>(target);

            string kept = JSBind_HeapView.KeepForGrow(HeapView<byte>.Key, heap.Pointer, Len);
            if (kept.StartsWith("存不下", StringComparison.Ordinal))
            {
                return (way, kept, "视图没能存下来 ✘ —— 生存期无从验证");
            }

            // 胁迫堆增长：不断分配 8MB 并【一直持有】，每分配一块就回头查一次状态。
            // 必须持有，否则 GC 一回收，压力就不存在了。
            var ballast = new List<byte[]>();
            int rounds = 0;
            string inspect = kept;
            string oom = "";
            while (rounds < GrowMaxRounds)
            {
                try
                {
                    ballast.Add(new byte[GrowChunk]);
                }
                catch (OutOfMemoryException)
                {
                    oom = "分配第 " + (rounds + 1) + " 块时 OOM，提前收手";
                    break;
                }

                rounds++;
                inspect = JSBind_HeapView.InspectKept();
                if (inspect.Contains("detach", StringComparison.Ordinal)
                    || inspect.Contains("已增长", StringComparison.Ordinal))
                {
                    break;
                }
            }

            detached = inspect.Contains("detach", StringComparison.Ordinal);
            string pressure = (oom.Length > 0 ? oom + "；" : "")
                + "共分配 " + rounds + " × " + (GrowChunk / 1024 / 1024) + "MB";

            // 真正写一次 —— 只有这一步能证明视图是活是死
            string wrote = JSBind_HeapView.WriteKept(0x2c);
            seen = pressure + " → " + inspect + " → " + wrote;

            // 写完之后才允许 ballast 变成可回收
            GC.KeepAlive(ballast);
            JSBind_HeapView.ReleaseKept();
        }
        catch (Exception e)
        {
            // 中途炸了也要把 JS 侧跨调用持有的引用放掉，别让它活到下一轮
            try { JSBind_HeapView.ReleaseKept(); } catch { /* 清理失败不影响报错 */ }
            return (way, "不可用：" + e.GetType().Name + "：" + e.Message, "无法执行 ✘");
        }

        bool alive = true;
        for (int i = 0; i < target.Length; i++)
        {
            if (target[i] != 0x2c) { alive = false; break; }
        }

        return (way, seen,
            alive
                ? "堆增长后视图仍可写 ✓ —— 本次没触发 detach；但这是“没撞上”，不等于跨 await / 跨帧安全"
                : detached
                    ? "buffer 已 detach，写入<b>静默失效</b> ✘ —— 坐实了这套方案的硬伤：" +
                      "视图绝不能跨堆增长持有，用完即弃"
                    : "写不进去了 ✘ —— 视图已失效，但 buffer 并未 detach，需进一步查");
    }

    /// <summary>
    /// ⑧ 那块 buffer 到底是"整块 WASM 线性内存"，还是只属于引子的那一小段？
    /// 用引子的 buffer 配上<b>另一个</b>被 pin 住的数组的地址，写入后看那个数组变没变。
    /// <para>
    /// 这一条是整套方案的立身之本：若 buffer 只是引子自己的私有内存，
    /// 那么"借引子"就仅仅是取到了一小段，建别的视图根本无从谈起。
    /// </para>
    /// </summary>
    private static (string, string, string) ProbeOtherArray()
    {
        const string way = "⑧ 引子的 buffer 能访问【另一个】数组吗";

        var other = new byte[Len];
        GCHandle handle = default;
        string seen;
        try
        {
            handle = GCHandle.Alloc(other, GCHandleType.Pinned);
            seen = JSBind_HeapView.WriteOther(HeapView<byte>.Key, handle.AddrOfPinnedObject(), Len, 0x7e);
        }
        catch (Exception e)
        {
            return (way, "不可用：" + e.GetType().Name + "：" + e.Message, "无法执行 ✘");
        }
        finally
        {
            if (handle.IsAllocated) handle.Free();
        }

        bool written = true;
        for (int i = 0; i < other.Length; i++)
        {
            if (other[i] != 0x7e) { written = false; break; }
        }

        return (way, seen,
            written
                ? "另一个数组全部变成 0x7E ✓ —— 那块 buffer 就是整块 WASM 线性内存，不只属于引子"
                : "另一个数组没变 ✘ —— buffer 不是整块线性内存，或地址不在其中");
    }

    /// <summary>把各条结论收成一段话，直接回答这套方案能不能用、能用到什么程度。</summary>
    private static string Conclusion(List<(string Way, string Seen, string Verdict)> rows)
    {
        // 按 Way 前缀取，别写死下标 —— 行数一变，写死的下标就会静默取错行
        var memory = rows.First(r => r.Way.StartsWith("①-b", StringComparison.Ordinal));
        var zero = rows.First(r => r.Way.StartsWith("③", StringComparison.Ordinal));
        var grow = rows.First(r => r.Way.StartsWith("⑦", StringComparison.Ordinal));

        var sb = new StringBuilder("<b>结论：</b>");

        if (memory.Verdict.Contains("不成立", StringComparison.Ordinal))
        {
            sb.Append("连「借引子 MemoryView 取 buffer」这条路都拿不到 WASM 的 memory.buffer —— " +
                      "⑪ 在 Bench_MemoryView 里测通过，这里却失败，多半是本模块的引子传参出了问题，" +
                      "先回那一页对照。若确实拿不到，则<b>这套方案在本工程不成立</b>（建视图只能抛错），" +
                      "能用仍然只有运行时给的 MemoryView：写入用 set、读出用 copyTo，而 slice() 是副本。");
            return sb.ToString();
        }

        sb.Append("buffer 的入口是「借引子 MemoryView 的 <code>_unsafe_create_view().buffer</code>」" +
                  "（扫全局那条路已被 ⑨ 证伪，本页 ①-a 是同页对照）。");

        sb.Append(zero.Verdict.Contains("成立", StringComparison.Ordinal)
            ? " <b>As() 的视图写入确实回写到了 C# 数组 —— 零拷贝【成立】</b>：" +
              "JS 与 .NET 直接读写同一块内存，一次 memcpy 都没有。"
            : " 但 As() 的写入没有回写到 C# 数组 —— 零拷贝【不成立】，只能退到 To() 拷一份。");

        sb.Append(" 生存期方面：").Append(grow.Verdict.Contains('✘')
            ? "⑦ <b>实测到了 detach</b> —— 堆一增长，旧 buffer 就被摘掉，视图随之静默失效。" +
              "所以视图<b>只能在一次同步调用内用完</b>：别存字段、别跨 await、别跨帧。"
            : "⑦ 本次没撞上 detach，但这只是\"没撞上\"，不是\"不会撞\" —— " +
              "风险依旧存在，视图仍应当用完即弃。");

        sb.Append(" 需要长期持有就用 To() 拷一份。另外 ③ 之外还有两处容易踩空、本页已一并验证：" +
                  "地址要<b>落在 buffer 内</b>（②）、要<b>按视图元素宽度对齐</b>（⑥）。");

        return sb.ToString();
    }
}
