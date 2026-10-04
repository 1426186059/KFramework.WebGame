using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

/// <summary>
/// 模块：探测 dotnet/runtime 里<b>本来就公开</b>的那套 WASM 内存 API 在本工程能不能用。
/// <para>
/// 起因是 Bench_MemoryView 的 ⑨ —— 它扫全局找 <c>wasmMemory</c>/<c>HEAPU8</c> 一无所获，
/// 于是 Bench_HeapView 绕道去用 <c>MemoryView._unsafe_create_view()</c>（带 _unsafe 前缀，属内部方法）。
/// 而 dotnet/runtime 源码里其实有一整套<b>公开</b>的内存 API：
/// <c>export-api.ts</c> 导出 <c>localHeapViewU8()</c> 与 <c>setHeapU8</c>/<c>getHeapU8</c> 系列，
/// <c>exports.ts:57-69</c> 把 <c>Module</c> 与这套 API 一起挂在 <c>create()</c> 的返回值上，
/// <c>exports.ts:71-77</c> 还专门留了个全局入口 <c>globalThis.getDotnetRuntime(runtimeId)</c>。
/// 换句话说，⑨ 不是"没有入口"，是<b>候选名单没扫到它</b>。
/// </para>
/// <para>
/// 本模块逐条实测这些 API：存不存在、拿到的是什么、三条来路的 buffer 是不是同一块、
/// 以及能不能直接用来访问 C# 钉住的数组 —— 判据一律是<b>C# 回读自己的数组</b>。
/// 若能行，Bench_HeapView 那条 _unsafe 绕道就该整个拆掉。
/// </para>
/// </summary>
public sealed class Bench_RuntimeApi : IBenchModule
{
    public string Name => "运行时公开内存 API（localHeapViewU8 / getDotnetRuntime / Module）";

    public string Summary =>
        "按 dotnet/runtime 源码找出来的一整套公开内存 API，逐条实测：先从哪拿到 RuntimeAPI" +
        "（注入 vs 全局 getDotnetRuntime vs ⑨ 的老办法扫描），再看 localHeapViewU8() 全套与 api.Module，" +
        "比较三条来路的 buffer 是否同一块，最后用它们直接读写 C# 钉住的数组（C# 回读验证），" +
        "并精确观测堆增长 —— 回答「_unsafe_create_view 那条绕道能不能换成公开 API」。";

    public string Page => "runtimeapi";

    /// <summary>探测用的数组长度。8 字节足够看出"写没写"。</summary>
    private const int Len = 8;

    /// <summary>
    /// ⑤ 用的引子：一个 1 字节的托管数组，传过去就是个 MemoryView。
    /// <b>只有 ⑤ 需要它</b> —— ⑥⑦ 的签名里没有引子，那正是本模块要证明的。
    /// </summary>
    private static readonly byte[] s_key = new byte[1];

    /// <summary>⑧ 里每次胁迫堆增长的块大小（8MB）。</summary>
    private const int GrowChunk = 8 * 1024 * 1024;

    /// <summary>⑧ 里最多分配多少块（12×8MB≈96MB 足以逼出 grow，再往上真有 OOM 风险）。</summary>
    private const int GrowMaxRounds = 12;

    public Task<string> RunAsync()
    {
        var rows = new List<(string Way, string Seen, string Verdict)>
        {
            ProbeGlobalRuntime(),   // ①-a 官方全局入口 getDotnetRuntime
            ProbeInjectedApi(),     // ①-b main.js 注入的 RuntimeAPI（主路）
            ProbeLegacyScan(),      // ①-c 老办法扫全局（对照，坐实 ⑨ 扫漏了）
            ProbeHeapViewU8(),      // ② localHeapViewU8() 本身
            ProbeAllHeapViews(),    // ③ 全套 localHeapViewXxx
            ProbeModule(),          // ④ api.Module：HEAPU8 / wasmMemory
            ProbeBufferIdentity(),  // ⑤ 三路 buffer 是不是同一块
            ProbeWritePinned(),     // ⑥ 灵魂测试：公开 API 直写 C# 钉住的数组
            ProbeScalarApi(),       // ⑦ setHeapU8 / getHeapU8 单值读写
            ProbeGrow(),            // ⑧ 堆增长的精确观测
        };

        var sb = new StringBuilder();
        sb.Append("<div class='card'><h3>运行时公开内存 API 探测</h3>");
        sb.Append("<p class='muted'>依据 dotnet/runtime 源码（<code>src/mono/browser/runtime</code>）：" +
                  "<code>export-api.ts</code> 导出堆视图与单值读写，<code>exports.ts</code> 把它们连同 " +
                  "<code>Module</code> 挂在 <code>create()</code> 的返回值上，并另留全局入口 " +
                  "<code>globalThis.getDotnetRuntime(runtimeId)</code>。" +
                  "凡涉及写入的条目，判据都是<b>C# 回读自己的数组</b>：字节真变了才算数。</p>");
        sb.Append("<table><thead><tr><th>条目</th><th>JS 侧回报</th><th>结论</th></tr></thead><tbody>");

        foreach ((string way, string seen, string verdict) in rows)
        {
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

    // ---------------------------------------------------------------- ① 入口

    /// <summary>
    /// ①-a 官方全局入口 <c>globalThis.getDotnetRuntime(0)</c>。
    /// 这条是<b>兜底</b>：它成不成都不影响主路（①-b 注入），但它的存在与否直接决定
    /// ⑨ 那句"全局没有堆视图入口"是"真没有"还是"没扫到"。
    /// </summary>
    private static (string, string, string) ProbeGlobalRuntime()
    {
        const string way = "①-a globalThis.getDotnetRuntime（官方全局入口）";

        string seen;
        try
        {
            seen = JSBind_RuntimeApi.ProbeGlobalRuntime();
        }
        catch (Exception e)
        {
            return (way, "不可用：" + e.GetType().Name + "：" + e.Message, "无法执行 ✘");
        }

        bool ok = seen.Contains("拿到 RuntimeAPI", StringComparison.Ordinal);
        return (way, seen,
            ok
                ? "全局就能拿到 RuntimeAPI ✓ —— ⑨ 那句「全局没有入口」是扫漏了它"
                : "全局这条路不通 ✘（只是兜底不可用；主路看 ①-b 注入，照样能跑）");
    }

    /// <summary>①-b main.js 注入的 RuntimeAPI —— 不依赖任何全局符号，本模块的主路。</summary>
    private static (string, string, string) ProbeInjectedApi()
    {
        const string way = "①-b main.js 注入的 RuntimeAPI（主路）";

        string seen;
        try
        {
            seen = JSBind_RuntimeApi.ProbeInjectedApi();
        }
        catch (Exception e)
        {
            return (way, "不可用：" + e.GetType().Name + "：" + e.Message, "无法执行 ✘");
        }

        bool ok = seen.Contains("拿到 RuntimeAPI", StringComparison.Ordinal);
        return (way, seen,
            ok
                ? "拿到 ✓ —— create() 的返回值上就有整套内存 API，后面都走它"
                : "没注入 ✘ —— main.js 的 setRuntimeApi 没接上，下面各条都会失败");
    }

    /// <summary>
    /// ①-c 对照：老办法扫全局。JS 侧会分开报"⑨ 当初那 5 个候选"与"补上 getDotnetRuntime 后"，
    /// 让"⑨ 错在候选名单不全"这件事在同一行里自证。
    /// </summary>
    private static (string, string, string) ProbeLegacyScan()
    {
        const string way = "①-c 老办法扫全局（对照）";

        string seen;
        try
        {
            seen = JSBind_RuntimeApi.ProbeLegacyScan();
        }
        catch (Exception e)
        {
            return (way, "不可用：" + e.GetType().Name + "：" + e.Message, "无法执行");
        }

        bool legacyFound = seen.Contains("扫到了", StringComparison.Ordinal);
        bool hasGlobalFn = seen.Contains("有 ——", StringComparison.Ordinal);

        return (way, seen,
            legacyFound
                ? "老候选竟然扫到了 —— 与 ⑨ 的结论不符，值得留意"
                : hasGlobalFn
                    ? "老候选一无所获，但 getDotnetRuntime 有 ✓ —— 复现了 ⑨ 的“一无所获”，也定位了它漏掉的那一个"
                    : "两条都没扫到 —— 入口只在 RuntimeAPI 对象上（看 ①-b），全局确实不该指望");
    }

    // ---------------------------------------------------------------- ② ③ ④

    /// <summary>
    /// ② <c>localHeapViewU8()</c>。除身份外，重点看 buffer 是不是 SharedArrayBuffer ——
    /// SAB 不可 detach，一旦是它，Bench_HeapView ⑦ 担心的那个风险就不成立。
    /// </summary>
    private static (string, string, string) ProbeHeapViewU8()
    {
        const string way = "② localHeapViewU8()（公开 API 的主角）";

        string seen;
        try
        {
            seen = JSBind_RuntimeApi.ProbeHeapViewU8();
        }
        catch (Exception e)
        {
            return (way, "不可用：" + e.GetType().Name + "：" + e.Message, "无法执行 ✘");
        }

        bool ok = seen.Contains("instanceof Uint8Array=true", StringComparison.Ordinal);
        bool isSab = seen.Contains("SharedArrayBuffer=true", StringComparison.Ordinal);

        return (way, seen,
            !ok
                ? "没拿到有效的 Uint8Array ✘ —— 公开 API 这条路不通"
                : isSab
                    ? "拿到整块堆视图 ✓，且 buffer 是 SharedArrayBuffer —— SAB 不可 detach，" +
                      "堆增长【不会】让视图失效"
                    : "拿到整块堆视图 ✓，buffer 不是 SAB —— 堆增长会 detach，视图用完即弃");
    }

    /// <summary>③ 全套 <c>localHeapViewXxx()</c> —— 只列清单，不判对错。</summary>
    private static (string, string, string) ProbeAllHeapViews()
    {
        const string way = "③ 全套 localHeapViewXxx()";

        string seen;
        try
        {
            seen = JSBind_RuntimeApi.ProbeAllHeapViews();
        }
        catch (Exception e)
        {
            return (way, "不可用：" + e.GetType().Name, "无法执行");
        }

        int missing = seen.Split("=缺失").Length - 1;
        int failed = seen.Split("=抛错").Length - 1;

        return (way, seen,
            missing == 0 && failed == 0
                ? "九个全在 ✓（U8/U16/U32、I8/I16/I32/I64Big、F32/F64）"
                : "缺 " + missing + " 个、抛错 " + failed + " 个 —— 只有健全的那几个可用");
    }

    /// <summary>
    /// ④ <c>api.Module</c>。有 <c>wasmMemory</c> 就能直接读 <c>buffer.byteLength</c>，
    /// 于是"堆涨没涨"是读出来的，不用再像 Bench_HeapView ⑦ 那样靠旧视图长度去猜。
    /// </summary>
    private static (string, string, string) ProbeModule()
    {
        const string way = "④ api.Module（HEAPU8 / wasmMemory）";

        string seen;
        try
        {
            seen = JSBind_RuntimeApi.ProbeModule();
        }
        catch (Exception e)
        {
            return (way, "不可用：" + e.GetType().Name + "：" + e.Message, "无法执行 ✘");
        }

        bool hasHeap = seen.Contains("HEAPU8=Uint8Array", StringComparison.Ordinal);
        bool hasMem = seen.Contains("wasmMemory=有", StringComparison.Ordinal);

        return (way, seen,
            !hasHeap
                ? "Module 上没有 HEAPU8 ✘ —— 拿不到堆视图"
                : hasMem
                    ? "HEAPU8 与 wasmMemory 都在 ✓ —— 堆增长可以精确观测（⑧ 靠它）"
                    : "有 HEAPU8，但没有 wasmMemory —— 堆增长只能退回去靠旧视图长度推断");
    }

    // ---------------------------------------------------------------- ⑤ 同一性

    /// <summary>
    /// ⑤ 三路 buffer 的同一性。按 marshal.ts:481-493，
    /// <c>_unsafe_create_view()</c> 内部就是 <c>new Uint8Array(localHeapViewU8().buffer, …)</c>，
    /// 所以三者应当是同一个对象 —— 同一，就说明那条 _unsafe 绕道纯属多余。
    /// </summary>
    private static (string, string, string) ProbeBufferIdentity()
    {
        const string way = "⑤ 三路 buffer 是不是同一块";

        string seen;
        try
        {
            seen = JSBind_RuntimeApi.ProbeBufferIdentity(s_key);
        }
        catch (Exception e)
        {
            return (way, "不可用：" + e.GetType().Name + "：" + e.Message, "无法执行 ✘");
        }

        bool same = seen.Contains("三者同一对象=true", StringComparison.Ordinal);
        return (way, seen,
            same
                ? "三条路拿到的是同一块 WASM 线性内存 ✓ —— _unsafe_create_view 只是它的包装，绕道可以拆"
                : "不是同一块 ✘ —— 与源码（marshal.ts:481）不符，绕道可能另有隐情");
    }

    // ---------------------------------------------------------------- ⑥ ⑦ 直写钉住的数组

    /// <summary>
    /// ⑥ 灵魂测试：用<b>公开 API</b> 的 buffer 配上 C# 钉住的数组地址建视图并写入。
    /// 注意调用里<b>没有引子</b> —— 这正是要证明的：公开 API 足够，不必再借 MemoryView。
    /// </summary>
    private static (string, string, string) ProbeWritePinned()
    {
        const string way = "⑥ 公开 API 直写 C# 钉住的数组（无引子）";

        var buf = new byte[Len];
        GCHandle handle = default;
        string seen;
        try
        {
            handle = GCHandle.Alloc(buf, GCHandleType.Pinned);
            seen = JSBind_RuntimeApi.WriteViaPublicApi(handle.AddrOfPinnedObject(), Len, 0xc3);
        }
        catch (Exception e)
        {
            return (way, "不可用：" + e.GetType().Name + "：" + e.Message, "无法执行 ✘");
        }
        finally
        {
            if (handle.IsAllocated) handle.Free();
        }

        bool allWritten = true;
        for (int i = 0; i < buf.Length; i++)
        {
            if (buf[i] != 0xc3) { allWritten = false; break; }
        }

        return (way, seen,
            allWritten
                ? "C# 数组全部变成 0xC3 ✓ —— <b>公开 API 完全能替代 _unsafe_create_view</b>，" +
                  "而且连引子都不用传"
                : "C# 数组没变 ✘ —— 公开 API 的 buffer 没落到这个数组上");
    }

    /// <summary>
    /// ⑦ <c>setHeapU8</c>/<c>getHeapU8</c>：按地址直接读写单个字节，连视图都不建。
    /// 双重判据：JS 侧 <c>getHeapU8</c> 读回的值 + C# 侧回读数组，两边都得对上。
    /// </summary>
    private static (string, string, string) ProbeScalarApi()
    {
        const string way = "⑦ setHeapU8 / getHeapU8（单值读写）";

        var buf = new byte[Len];
        GCHandle handle = default;
        string seen;
        try
        {
            handle = GCHandle.Alloc(buf, GCHandleType.Pinned);
            nint ptr = handle.AddrOfPinnedObject();
            seen = JSBind_RuntimeApi.SetU8At(ptr, 0xa5) + "；" + JSBind_RuntimeApi.GetU8At(ptr);
        }
        catch (Exception e)
        {
            return (way, "不可用：" + e.GetType().Name + "：" + e.Message, "无法执行 ✘");
        }
        finally
        {
            if (handle.IsAllocated) handle.Free();
        }

        bool csSees = buf[0] == 0xa5;
        bool jsSees = seen.Contains("= 165", StringComparison.Ordinal); // 0xA5 = 165

        return (way, seen,
            csSees && jsSees
                ? "JS 读回 165、C# 数组 [0] 也是 0xA5 ✓ —— 按地址单值读写同样零拷贝"
                : csSees
                    ? "C# 看到了 0xA5，但 getHeapU8 读回的不是 —— 两面对不上，需查"
                    : "C# 数组没变 ✘ —— setHeapU8 没写到这个地址");
    }

    // ---------------------------------------------------------------- ⑧ 堆增长

    /// <summary>
    /// ⑧ 堆增长的<b>精确</b>观测：靠 <c>wasmMemory.buffer.byteLength</c> 直接读有没有涨，
    /// 再看旧 buffer 有没有 detach，最后真写一次、由 C# 回读定生死。
    /// 与 Bench_HeapView ⑦ 的差别：那里只能从"旧视图长度变了没"去推断，这里是读出来的。
    /// </summary>
    private static (string, string, string) ProbeGrow()
    {
        const string way = "⑧ 堆增长精确观测（wasmMemory + 回读）";

        var target = new byte[Len];
        GCHandle handle = default;
        string seen;
        bool detached = false;
        bool grew = false;

        try
        {
            handle = GCHandle.Alloc(target, GCHandleType.Pinned);

            string kept = JSBind_RuntimeApi.StartGrowWatch(handle.AddrOfPinnedObject(), Len);
            if (kept.StartsWith("存不下", StringComparison.Ordinal))
            {
                return (way, kept, "观测基线没存下来 ✘ —— 无法精确判断堆增长，退回旧视图长度推断");
            }

            // 胁迫堆增长：不断分配 8MB 并【一直持有】，每分配一块就查一次。
            var ballast = new List<byte[]>();
            int rounds = 0;
            string check = kept;
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
                check = JSBind_RuntimeApi.CheckGrow();
                if (check.Contains("detach", StringComparison.Ordinal)
                    || check.Contains("确实增长了", StringComparison.Ordinal))
                {
                    break;
                }
            }

            // 判据要精确：JS 的"确实增长了，但旧 buffer【未】detach"里也含 detach 二字，
            // 只查 "detach" 会把"涨了但没 detach"误判成"已 detach"。
            detached = check.Contains("已 detach", StringComparison.Ordinal);
            grew = check.Contains("确实增长了", StringComparison.Ordinal);

            // 真正写一次 —— 只有这一步能证明视图是活是死
            string wrote = JSBind_RuntimeApi.WriteKeptView(0x3c);
            seen = (oom.Length > 0 ? oom + "；" : "")
                + "共分配 " + rounds + " × " + (GrowChunk / 1024 / 1024) + "MB → "
                + check.Replace("✘", "") + " → " + wrote;

            // 写完之后才允许 ballast 变成可回收
            GC.KeepAlive(ballast);
            JSBind_RuntimeApi.ReleaseGrowWatch();
        }
        catch (Exception e)
        {
            try { JSBind_RuntimeApi.ReleaseGrowWatch(); } catch { /* 清理失败不影响报错 */ }
            return (way, "不可用：" + e.GetType().Name + "：" + e.Message, "无法执行 ✘");
        }
        finally
        {
            if (handle.IsAllocated) handle.Free();
        }

        bool alive = target[0] == 0x3c;

        return (way, seen,
            alive
                ? "写的进去 ✓（" + (grew ? "期间内存确实增长过，本次没撞上 detach" : "期间内存未增长") +
                  "）—— 但这是“没撞上”，不是“不会撞”，视图仍应用完即弃"
                : detached
                    ? "buffer 已 detach，写入<b>静默失效</b> ✘ —— 由 wasmMemory 精确坐实：" +
                      "堆一涨，旧 buffer 就被摘掉"
                    : "写不进去了 ✘ —— 视图已失效却未 detach，需进一步查");
    }

    // ---------------------------------------------------------------- 结论

    /// <summary>把各条收成一段话，直接回答"那条 _unsafe 绕道能不能换成公开 API"。</summary>
    private static string Conclusion(List<(string Way, string Seen, string Verdict)> rows)
    {
        // 按前缀取，别写死下标 —— 行数一变，写死的下标就会静默取错行
        var injected = rows.First(r => r.Way.StartsWith("①-b", StringComparison.Ordinal));
        var legacy = rows.First(r => r.Way.StartsWith("①-c", StringComparison.Ordinal));
        var heapView = rows.First(r => r.Way.StartsWith("②", StringComparison.Ordinal));
        var identity = rows.First(r => r.Way.StartsWith("⑤", StringComparison.Ordinal));
        var write = rows.First(r => r.Way.StartsWith("⑥", StringComparison.Ordinal));
        var grow = rows.First(r => r.Way.StartsWith("⑧", StringComparison.Ordinal));

        var sb = new StringBuilder("<b>结论：</b>");

        if (injected.Verdict.Contains("没注入", StringComparison.Ordinal))
        {
            sb.Append("main.js 没把 <code>create()</code> 的返回值注入进来 —— 本模块的探测都无从谈起，" +
                      "先去 main.js 确认 <code>setRuntimeApi</code> 的接线。这不影响 Bench_HeapView 那条既有路线。");
            return sb.ToString();
        }

        sb.Append("RuntimeAPI 拿得到（主路是 main.js 注入，不依赖任何全局符号）；")
          .Append(legacy.Verdict.Contains("复现", StringComparison.Ordinal)
              ? "而 ①-c 复现了 ⑨ 的“一无所获”，同时把它漏掉的那一个（<code>getDotnetRuntime</code>）指了出来 —— " +
                "所以 ⑨ 的正确表述是<b>「入口不在那几个全局符号上」</b>，而不是<b>「本工程没有入口」</b>。"
              : "①-c 的对照结果另见该行。");

        sb.Append(heapView.Verdict.Contains('✘')
            ? " 但 <code>localHeapViewU8()</code> 没能给出有效堆视图 —— 公开 API 这条路在本工程不通，" +
              "只能继续用 <code>_unsafe_create_view()</code> 那条绕道。"
            : " <code>localHeapViewU8()</code> 给出的是整块堆视图 ✓。");

        if (identity.Verdict.Contains("同一块", StringComparison.Ordinal))
        {
            sb.Append(" ⑤ 已证明三条来路取到的是<b>同一块</b> WASM 线性内存 —— " +
                      "按 marshal.ts:481，<code>_unsafe_create_view()</code> 本就只是它的包装。");
        }

        sb.Append(write.Verdict.Contains("✓", StringComparison.Ordinal)
            ? " ⑥ 用公开 API 直写 C# 钉住的数组成功且<b>不需要引子</b>，所以 Bench_HeapView 那条绕道" +
              "<b>可以整个拆掉</b>，换成公开 API。"
            : " ⑥ 没有写成功 —— 在它修好之前，Bench_HeapView 先别动。");

        sb.Append(" 生存期：").Append(heapView.Verdict.Contains("SharedArrayBuffer", StringComparison.Ordinal)
            ? "② 显示 buffer 是 <b>SharedArrayBuffer</b>，SAB 不可 detach，" +
              "那么 Bench_HeapView ⑦ 担心的 detach 风险不成立。"
            : grow.Verdict.Contains("detach", StringComparison.Ordinal)
                ? "⑧ 由 <code>wasmMemory</code> <b>精确坐实了 detach</b>：堆一涨，旧 buffer 就被摘掉，" +
                  "视图静默失效。所以视图只能在一次同步调用内用完。"
                : "⑧ 本次没撞上 detach（只是没撞上，不是不会撞）—— 视图仍应当用完即弃，" +
                  "别存字段、别跨 await、别跨帧。");

        return sb.ToString();
    }
}
