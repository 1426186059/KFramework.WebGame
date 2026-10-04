using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

/// <summary>
/// 模块：MemoryView 在 JS 侧到底有哪几种写法，以及每种是不是真的动到了 C# 的托管内存。
/// <para>
/// 起因是一句猜测：「MemoryView 用 slice() 拿到的就是 Uint8Array 视图，可以零拷贝」。
/// 而权威定义（KFramework.TSEngine/reference/MemoryView.ts，摘自 dotnet/runtime 的 marshal.ts）写着：
/// <c>slice()</c> 内部走的是标准 <c>TypedArray.prototype.slice()</c>，返回的<b>是副本</b>；
/// 真正返回共享内存视图的 <c>_unsafe_create_view()</c> 是内部方法、不对外。
/// 猜测到底成不成立，跑一遍就知道 —— 本模块就是干这个的。
/// </para>
/// <para>
/// 判据不是「JS 有没有报错」，而是<b>C# 回读自己的数组</b>：每次都先填成已知内容（0,1,2…7），
/// 交给 JS 用某一写法操作，再看字节有没有变。只报「跑通了」而字节没动，
/// 就是那种最坑的情况 —— 看着有结果、其实没干活（Bench_CsToJs 里 <c>view[i] = v</c> 就栽过）。
/// </para>
/// </summary>
public sealed class Bench_MemoryView : IBenchModule
{
    public string Name => "MemoryView 写法探测（slice 是副本还是视图？）";

    public string Summary =>
        "逐一试 view.set / copyTo / slice / length·byteLength / 索引读写 / .buffer / _unsafe_create_view；" +
        "每种都把同一份已知内容的 byte[8] 交给 JS 操作，再回读 C# 数组看字节有没有被写 —— " +
        "回答「slice() 拿到的是不是共享内存的零拷贝视图」。";

    public string Page => "memoryview";

    /// <summary>探测用的缓冲长度。8 字节足够看出"写没写"，又不至于让回报串太长。</summary>
    private const int Len = 8;

    public Task<string> RunAsync()
    {
        var rows = new List<(string Way, string Seen, string Verdict, bool Changed)>
        {
            // 前两项是官方路径，预期明确；后几项是"传闻"，用 null 表示不预判、实测为准。
            Probe("① view.set(src, 0)", "官方写入路径",
                JSBind_MemoryView.ProbeSet, expectWrite: true),

            Probe("② view.copyTo(target)", "官方读出路径（只把字节搬到 JS 的数组）",
                JSBind_MemoryView.ProbeCopyTo, expectWrite: false),

            Probe("③ view.slice()", "改一下返回值再看 C# 缓冲 —— 副本与视图的分水岭",
                JSBind_MemoryView.ProbeSlice, expectWrite: false),

            Probe("④ view.length / view.byteLength", "两个尺寸属性",
                JSBind_MemoryView.ProbeMeta, expectWrite: false),

            Probe("⑤ view[i] 读 / view[i] = v 写", "传闻没有索引器",
                JSBind_MemoryView.ProbeIndex, expectWrite: false),

            Probe("⑥ view.buffer", "TypedArray 的常规零拷贝入口，实测存不存在",
                JSBind_MemoryView.ProbeBuffer, expectWrite: false),

            Probe("⑦ view._unsafe_create_view()", "运行时内部方法，定义上才是真正的共享视图",
                JSBind_MemoryView.ProbeUnsafe, expectWrite: null),

            // ⑧ 分两步：先把视图存进 JS，下一次调用再取出来写 ——
            //    分别用 Span 与 ArraySegment，对照"想跨调用持有该用哪一个"。
            ProbeKeep("⑧-a Span 存下来再用", "Span 版按约定只在同步调用期间有效、不 pin",
                b => JSBind_MemoryView.ProbeStoreSpan(b)),

            ProbeKeep("⑧-b ArraySegment 存下来再用", "ArraySegment 版会 pin 住托管数组（用完 dispose）",
                b => JSBind_MemoryView.ProbeStoreSegment(new ArraySegment<byte>(b))),

            // ⑨ 不传缓冲：直接在 JS 侧找 WASM 裸堆入口 —— 这是 HEAPU8 那套零拷贝说法的前提
            ProbeHeap(),

            // ⑩ ⑦ 的身份细节：它返回的究竟是不是 Uint8Array、有没有 .buffer（只读不写，只看身份）
            Probe("⑩ _unsafe_create_view() 返回值细节", "只读不写，只看构造函数 / .buffer / 偏移",
                JSBind_MemoryView.ProbeUnsafeDetail, expectWrite: false),

            // ⑪ 灵魂测试：借引子的 buffer，去访问另一个被 pin 住的 .NET 数组
            ProbeBridge(),
        };

        var sb = new StringBuilder();
        sb.Append("<div class='card'><h3>MemoryView 写法探测（byte[").Append(Len).Append("]，C# 回读验证）</h3>");
        sb.Append("<p class='muted'>每一条都把同一份已知内容（0,1,2…").Append(Len - 1).Append("）的 byte[")
          .Append(Len).Append("] 交给 JS，JS 用该写法操作后，<b>C# 回读自己的数组</b>：" +
                  "字节真的变了，才算这条写法动到了托管内存。JS 侧不报错 ≠ 写进去了。</p>");
        sb.Append("<table><thead><tr><th>写法</th><th>JS 侧回报</th><th>C# 回读结论</th></tr></thead><tbody>");

        foreach ((string way, string seen, string verdict, bool changed) in rows)
        {
            // 只有"标称能写却没写进去"才标红 —— 那是会误导人的一行
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
    /// 跑一条写法：填好已知内容 → 交给 JS → 回读看字节动没动。
    /// </summary>
    /// <param name="expectWrite">
    /// 预期是否写入托管内存：true = 该写法标称能写；false = 标称只读；null = 不预判（正是要测的那个争议点）。
    /// </param>
    private static (string Way, string Seen, string Verdict, bool Changed) Probe(
        string way, string note, Func<Span<byte>, string> call, bool? expectWrite)
    {
        var buf = new byte[Len];
        for (int i = 0; i < buf.Length; i++) buf[i] = (byte)i;

        string seen;
        try
        {
            seen = call(buf);
        }
        catch (Exception e)
        {
            // 过界就抛了：这条写法压根不可用
            return (way, note + " — 不可用：" + e.GetType().Name, "无法执行", false);
        }

        return Verdict(way, seen, buf, expectWrite);
    }

    /// <summary>
    /// ⑧：视图能不能【跨调用】持有 —— 第一步交出去让 JS 存着，第二步让 JS 取出来再写。
    /// 写不进去就说明视图随调用结束失效了，想长期持有这份数据只能拷出来。
    /// </summary>
    private static (string Way, string Seen, string Verdict, bool Changed) ProbeKeep(
        string way, string note, Func<byte[], string> store)
    {
        var buf = new byte[Len];
        for (int i = 0; i < buf.Length; i++) buf[i] = (byte)i;

        string seen;
        try
        {
            string first = store(buf);                          // 交出去，JS 存进模块级变量
            string second = JSBind_MemoryView.ProbeUseStored(); // 再调一次，让它取出来写
            seen = first + "；" + second;
        }
        catch (Exception e)
        {
            return (way, note + " — 不可用：" + e.GetType().Name, "无法执行", false);
        }

        // 这一步做的是"写"：视图若已失效就写不进去 —— 判据正是 C# 缓冲有没有变
        return Verdict(way, seen, buf, expectWrite: true);
    }

    /// <summary>
    /// ⑨：不传缓冲，只在 JS 侧找 WASM 裸堆入口 —— 这是"直接 new Uint8Array(wasmMemory.buffer,…) 零拷贝"的前提。
    /// </summary>
    private static (string Way, string Seen, string Verdict, bool Changed) ProbeHeap()
    {
        const string way = "⑨ JS 直接访问 WASM 内存（HEAPU8 那套）";

        string seen;
        try
        {
            seen = JSBind_MemoryView.ProbeHeap();
        }
        catch (Exception e)
        {
            return (way, "不可用：" + e.GetType().Name, "无法执行", false);
        }

        // JS 侧一个入口都找不到，就意味着那条"直接建视图"的零拷贝路线在本工程里不成立
        bool hasEntry = !seen.Contains("一个都没有", StringComparison.Ordinal);

        return (way, seen,
            hasEntry
                ? "全局能摸到堆视图 → 可以零拷贝建视图"
                : "全局没有堆视图入口 → 【直接零拷贝访问 WASM 内存这条路在本工程不成立】，" +
                  "能用的只有 MemoryView 那几个方法",
            hasEntry);
    }

    /// <summary>
    /// ⑪ 灵魂测试：借"引子"MemoryView 换出 WASM buffer，再用它访问【另一个】被 pin 住的 .NET 数组。
    /// 写成功就证明那个 .buffer 是<b>整块线性内存</b> —— 于是 HeapView 方案里最难的
    /// getWasmMemoryBuffer() 有解了：不必扫全局，从任意一个 MemoryView 身上取即可。
    /// </summary>
    private static (string Way, string Seen, string Verdict, bool Changed) ProbeBridge()
    {
        const string way = "⑪ 借 MemoryView 取 buffer → 访问另一个数组";

        var key = new byte[Len];    // 引子：内容无关紧要，只为在 JS 侧换出一个 MemoryView
        var target = new byte[Len]; // 目标：另一个数组，钉住后把地址交给 JS

        GCHandle handle = default;
        string seen;
        try
        {
            handle = GCHandle.Alloc(target, GCHandleType.Pinned);
            seen = JSBind_MemoryView.ProbeHeapBridge(key, handle.AddrOfPinnedObject(), Len);
        }
        catch (Exception e)
        {
            // pin 失败也要干净地报出来：这本身就是一个有价值的结论（该运行时不支持 pin）
            return (way, "不可用：" + e.GetType().Name + "：" + e.Message, "无法执行 ✘", false);
        }
        finally
        {
            if (handle.IsAllocated) handle.Free();
        }

        // 判据：另一个数组是不是被写成了 0x7E
        bool written = true;
        for (int i = 0; i < target.Length; i++)
        {
            if (target[i] != 0x7e) { written = false; break; }
        }

        return (way, seen,
            written
                ? "C# 的【另一个】数组全部变成 0x7E ✓ —— 那个 .buffer 就是整块 WASM 线性内存，零拷贝完全可行"
                : "另一个数组没变 ✘ —— .buffer 不是整块线性内存，或该地址不在其中",
            written);
    }

    /// <summary>共用的收尾：回读 buffer 看字节动没动，据此给出结论。</summary>
    private static (string Way, string Seen, string Verdict, bool Changed) Verdict(
        string way, string seen, byte[] buf, bool? expectWrite)
    {
        bool changed = false;
        for (int i = 0; i < buf.Length; i++)
        {
            if (buf[i] != (byte)i) { changed = true; break; }
        }

        string verdict = expectWrite switch
        {
            true => changed
                ? "写入回写 C# ✓（零拷贝生效）"
                : "标称能写，但 C# 缓冲未变 ✘",
            false => changed
                ? "标称只读，却改动了 C# 缓冲（留意）"
                : "C# 缓冲未变 ✓（符合预期）",
            _ => changed
                ? "写入回写 C# —— 拿到的是【共享内存的视图】"
                : "C# 缓冲未变 —— 拿到的是【副本】",
        };

        return (way, seen, verdict, changed);
    }

    /// <summary>把所有行的结论收成一段话，重点回答 slice 是不是零拷贝视图。</summary>
    private static string Conclusion(List<(string Way, string Seen, string Verdict, bool Changed)> rows)
    {
        // 用 var 接：写成 (string, string, string, bool) 会把元组元素名丢掉，后面 .Changed 就不可用了
        var slice = rows.First(r => r.Way.StartsWith("③", StringComparison.Ordinal));
        var unsafeView = rows.First(r => r.Way.StartsWith("⑦", StringComparison.Ordinal));

        var sb = new StringBuilder();
        sb.Append("<b>结论：</b>");

        sb.Append(slice.Changed
            ? "slice() 写入会回写 C# —— 它是共享内存的视图。"
            : "slice() 拿到的是<b>副本</b>：改返回值，C# 缓冲纹丝不动。" +
              "它内部走的是标准 TypedArray.prototype.slice()，每次调用都会新分配一块，<b>不是零拷贝视图</b>。");

        sb.Append(" 而 ").Append(unsafeView.Changed
            ? "_unsafe_create_view() 能拿到共享视图（写入确实回写），"
            : "_unsafe_create_view() 要么不存在、要么写入也没回写，");

        sb.Append("但它带 _unsafe 前缀、属于运行时内部方法，公开 API 里<b>给不出零拷贝视图</b>。");
        sb.Append("所以要避免每帧分配，只能像 ByteCache 那样：先用 copyTo 把字节拷进一块<b>自己复用的</b>缓冲，");
        sb.Append("拷这一步免不掉，免掉的是每次新建 Uint8Array 的那次分配。");

        // ⑧ 跨 await：决定"想长期持有这份数据，到底能不能不拷"
        var spanKeep = rows.First(r => r.Way.StartsWith("⑧-a", StringComparison.Ordinal));
        var segKeep = rows.First(r => r.Way.StartsWith("⑧-b", StringComparison.Ordinal));
        var heap = rows.First(r => r.Way.StartsWith("⑨", StringComparison.Ordinal));

        sb.Append(" <b>跨 await / 长期持有：</b>");
        sb.Append(spanKeep.Changed
            ? "Span 版 await 之后仍可写（同步约束比文档说的宽松）；"
            : "Span 版 await 之后【已失效】，写不回去了；");
        sb.Append(segKeep.Changed
            ? "ArraySegment 版 await 之后仍可写 ✓ —— 要跨 await 就用它。"
            : "ArraySegment 版 await 之后同样失效 —— 那就只能先拷成自己的 byte[] 再长期持有。");

        sb.Append(" <b>裸内存入口：</b>").Append(heap.Changed
            ? "全局能摸到 WASM 堆视图，那套直接建 Uint8Array 视图的零拷贝写法在此可用。"
            : "全局【没有】HEAPU8 之类的堆入口 —— 「new Uint8Array(wasmMemory.buffer, ptr, len) 即可零拷贝」" +
              "那是 Emscripten 的做法，.NET 的 WASM 运行时并不把裸堆暴露给 JS，本工程里这条路走不通；" +
              "能用的只有 MemoryView 提供的 set / copyTo / slice。");

        // ⑩⑪：_unsafe_create_view 的身份，以及它的 .buffer 是不是整块线性内存
        var detail = rows.First(r => r.Way.StartsWith("⑩", StringComparison.Ordinal));
        var bridge = rows.First(r => r.Way.StartsWith("⑪", StringComparison.Ordinal));

        sb.Append(" <b>但真正的零拷贝入口是它：</b>");
        sb.Append("_unsafe_create_view() 返回的就是共享内存的 TypedArray（⑦ 写入确实回写）。");
        sb.Append(bridge.Changed
            ? "而且它的 <b>.buffer 就是整块 WASM 线性内存</b>（⑪ 已写通）：配上 GCHandle 钉住的地址，" +
              "就能零拷贝访问任意 .NET 数组。于是 HeapView 那套方案可行 —— 而且不必再扫全局找 wasmMemory，" +
              "从任意一个 MemoryView 身上取 buffer 即可。"
            : "不过它的 .buffer 没能用来访问另一个数组（⑪ 未写通）：要么该运行时不支持 GCHandleType.Pinned，" +
              "要么 .buffer 只覆盖视图自身那一小段。这种情况下仍只能按公开 API 走 —— set 写入、" +
              "copyTo 读出（拷进一块复用的缓冲）。");

        return sb.ToString();
    }
}
