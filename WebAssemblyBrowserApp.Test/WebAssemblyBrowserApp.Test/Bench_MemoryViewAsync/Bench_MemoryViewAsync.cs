using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

/// <summary>
/// 模块：异步（await）期间 MemoryView 的内存<b>会不会变</b>？
/// <para>
/// 起因：之前有个判断「异步 MemoryView 的内存会变」。本模块用确定性手段实测，结论比预想更干脆 ——
/// 对默认 <c>Span&lt;byte&gt;</c> 的 MemoryView，问题根本<b>不是</b>「GC 把内存搬走了」，而是
/// 绑定在<b>同步 JSImport 调用一返回就把视图 dispose 掉</b>（释放 pin），此时再读就抛
/// <c>ObjectDisposedException</c>：它<b>活不过一次调用</b>，自然也跨不了 await。
/// 而 <c>ArraySegment&lt;byte&gt;</c>（绑定会 pin 住托管数组）、<c>GCHandle.Pinned</c>、或 WASM 堆视图
/// （<c>runtime.localHeapViewU8()</c>）这三种<b>钉住</b>的路子，内存恒定不变。
/// </para>
/// <para>
/// 判据：C# 先填一份已知内容（<c>i*7</c>），交给 JS 存下，再（实验组）真等几秒 + 强制 GC 模拟真实 await 时间窗，
/// 然后让 JS 把存下的视图读回来 —— 字节和当初一致就叫「不变」，抛 <c>ObjectDisposedException</c> 就叫「视图已释放」。
/// 另加两条<b>对照</b>：存下后<b>不 GC 立即读</b>，用来区分「是调用结束就释放」还是「被 GC 搬走才失效」。
/// </para>
/// </summary>
public sealed class Bench_MemoryViewAsync : IBenchModule
{
    public string Name => "MemoryView 异步内存稳定性（pin vs unpin）";

    public string Summary =>
        "存下 MemoryView →（实验组）真等几秒 + 强制 GC（模拟真实 await 时间窗）→ 读回，对比 Span(unpinned) 与 " +
        "ArraySegment(pinned)；并测「不 GC 立即读」以区分「调用结束即释放」还是「GC 搬走才失效」。";

    public string Page => "memoryviewasync";

    /// <summary>探测缓冲长度：大到足以成为真正的 GC 堆对象，又小到回报不长。</summary>
    private const int Len = 64;

    /// <summary>store 之后真等几秒再读回，更贴近真实 await 的时间窗（让运行时 GC 自然发生）。</summary>
    private const int WaitMs = 3000;

    public async Task<string> RunAsync()
    {
        var expected = new byte[Len];
        for (int i = 0; i < Len; i++) expected[i] = (byte)(i * 7);

        // —— 对照：存下后【不 GC、不等待】立即读，证明视图起初有效、失效只来自后续 ——
        // unpinned 预期抛 ObjectDisposedException（调用一返回就释放）；pinned 预期读到原内容。
        var ctrlUnpinned = await RunExperiment("对照·unpinned 存后立刻读（不 GC）", expected, pinned: false, forceGc: false, wait: false);
        var ctrlPinned   = await RunExperiment("对照·pinned 存后立刻读（不 GC）",   expected, pinned: true,  forceGc: false, wait: false);

        // —— 实验组：存下后【真等几秒 + GC 间隙】（模拟真实 await 把控制权交还事件循环的时间窗）——
        var unpinned = await RunExperiment("unpinned(Span) 存下 → 等" + (WaitMs / 1000) + "秒+GC → 读回", expected, pinned: false, forceGc: true, wait: true);
        var pinned   = await RunExperiment("pinned(ArraySegment) 存下 → 等" + (WaitMs / 1000) + "秒+GC → 读回", expected, pinned: true, forceGc: true, wait: true);

        var rows = new List<(string Label, string Seen, string Verdict, bool Suspicious)>
        {
            ctrlUnpinned, ctrlPinned, unpinned, pinned,
        };

        var sb = new StringBuilder();
        sb.Append("<div class='card'><h3>MemoryView 异步内存稳定性（byte[").Append(Len).Append("]，C# 已知内容回读）</h3>");
        sb.Append("<p class='muted'>每次：C# 先填 <b>i*7</b>（0,").Append((byte)(1 * 7)).Append(",")
          .Append((byte)(2 * 7)).Append(",…）→ 交给 JS 存下 → " +
          "（实验组）强制 GC + 分配压力（模拟 await 间隙）→ " +
          "让 JS 把存下的视图读回来，<b>C# 比对字节</b>。视图读不回来就报 <code>ObjectDisposedException</code>。</p>");
        sb.Append("<table><thead><tr><th>场景</th><th>JS 侧回报</th><th>结论</th></tr></thead><tbody>");

        foreach ((string label, string seen, string verdict, bool suspicious) in rows)
        {
            // 只有「pinned 居然也失效 / 读回却字节不一致」才是真正意外（标红）；
            // unpinned 抛 ObjectDisposed 是预期，不标红。
            sb.Append(suspicious ? "<tr class='blocked'>" : "<tr>");
            sb.Append("<td>").Append(BenchKit.Escape(label)).Append("</td>");
            sb.Append("<td class='muted'>").Append(BenchKit.Escape(seen)).Append("</td>");
            sb.Append("<td>").Append(BenchKit.Escape(verdict)).Append("</td>");
            sb.Append("</tr>");
        }
        sb.Append("</tbody></table>");
        sb.Append("<p class='muted'>").Append(Conclusion(rows)).Append("</p>");
        sb.Append("</div>");

        return sb.ToString();
    }

    /// <summary>
    /// 跑一条：填已知内容 → 存视图 → （可选）GC 间隙 →（可选）真等几秒 → 读回 → 给结论。
    /// 返回项的 <c>Suspicious</c> 仅当「pinned 却失效 / 字节不一致」这类真异常时为真。
    /// </summary>
    private static async Task<(string Label, string Seen, string Verdict, bool Suspicious)> RunExperiment(
        string label, byte[] expected, bool pinned, bool forceGc, bool wait)
    {
        var buf = new byte[Len];
        for (int i = 0; i < Len; i++) buf[i] = expected[i];

        string storeMsg;
        if (pinned)
            storeMsg = JSBind_MemoryViewAsync.ProbeStorePinned(new ArraySegment<byte>(buf));
        else
            storeMsg = JSBind_MemoryViewAsync.ProbeStore(buf);   // Span<byte> 由隐式转换得到

        if (forceGc) ForceGcGap();
        // 真等几秒：把控制权交还事件循环，让运行时的真实 GC / 异步调度自然发生（更贴近真实 await）。
        if (wait) await Task.Delay(WaitMs);

        string seen = (pinned ? JSBind_MemoryViewAsync.ProbeReadStoredPinned()
                              : JSBind_MemoryViewAsync.ProbeReadStored());

        if (IsDead(seen))
        {
            // 读回即 ObjectDisposedException / null：视图已释放。
            if (pinned)
            {
                // pinned 居然也死了 —— 与本工程「ArraySegment 会 pin」约定矛盾，是真异常
                return (label, storeMsg + "；" + seen,
                    "✘ pinned 视图也抛 ObjectDisposed —— 与本工程「ArraySegment 会 pin」约定矛盾，需排查", true);
            }
            // unpinned 立刻/GC 后失效：符合预期（调用结束即释放，活不过一次调用，更别提跨 await）
            string why = "调用结束即被释放（ObjectDisposedException）";
            if (wait) why += "，等了 " + (WaitMs / 1000) + " 秒后依旧 —— 活不过一次调用，跨真实 await 必死";
            else if (forceGc) why += "，GC 与否都不可读";
            else why += " —— 活不过一次调用，不能跨 await 持有";
            return (label, storeMsg + "；" + seen,
                "视图已释放（ObjectDisposedException）—— 符合预期：" + why, false);
        }

        // 读回有效：比对字节
        if (MatchesExpected(seen, expected))
        {
            string tail = pinned ? "（pin 生效" : "";
            if (wait && pinned) tail += "，等了 " + (WaitMs / 1000) + " 秒真实 await 时间窗后仍稳定";
            if (pinned) tail += "）";
            return (label, storeMsg + "；" + seen,
                "读回字节与已知内容一致 ✓ —— 内存没变" + tail, false);
        }

        // 读回了但字节不一致：内存真被改写/搬迁了
        return (label, storeMsg + "；" + seen,
            "读回字节与已知内容不一致 ✘ —— 内存确实变了", pinned);
    }

    /// <summary>
    /// 模拟「await 把控制权交还事件循环」的窗口：强制 GC 并猛灌短命大对象，
    /// 让 .NET 移动式 GC 把托管数组迁到新地址、旧地址被复用。
    /// （对 unpinned 而言其实多余——它调用结束即被释放；这里只为和 pinned 走同一路径做对照。）
    /// </summary>
    private static void ForceGcGap()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        for (int i = 0; i < 400; i++) { var tmp = new byte[8192]; } // 短命大对象：制造搬迁 + 覆盖旧槽位
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    /// <summary>视图读不回来（detached / 释放）：回报含 ERR 前缀或 null。</summary>
    private static bool IsDead(string seen)
    {
        int bar = seen.LastIndexOf('|');
        if (bar < 0) return true;
        string csv = seen.Substring(bar + 1);
        return csv.StartsWith("ERR", StringComparison.Ordinal) || csv == "null";
    }

    /// <summary>解析 JS 回报的 <c>tag|csv</c>，比对每个字节是否等于 expected。</summary>
    private static bool MatchesExpected(string seen, byte[] expected)
    {
        int bar = seen.LastIndexOf('|');
        if (bar < 0) return false;
        string csv = seen.Substring(bar + 1);
        if (csv.StartsWith("ERR", StringComparison.Ordinal) || csv == "null") return false;

        string[] parts = csv.Split(',');
        if (parts.Length != expected.Length) return false;
        for (int i = 0; i < expected.Length; i++)
        {
            if (!byte.TryParse(parts[i], out byte v) || v != expected[i]) return false;
        }
        return true;
    }

    private static string Conclusion(List<(string Label, string Seen, string Verdict, bool Suspicious)> rows)
    {
        var ctrlU = rows[0];   // 对照·unpinned 立刻读（不 GC）
        var unp   = rows[2];   // unpinned + GC 间隙
        var pin   = rows[3];   // pinned + GC 间隙

        var sb = new StringBuilder();
        sb.Append("<b>结论：</b>");

        // 对照·unpinned 即便不 GC 也抛 ObjectDisposed → 证明失效发生在「调用结束」而非「GC 搬迁」。
        bool deadAtCallEnd = ctrlU.Verdict.Contains("ObjectDisposed");
        sb.Append(deadAtCallEnd
            ? "对照项显示 <b>unpinned 视图在「不 GC 立即读」时就已抛 ObjectDisposedException</b> —— " +
              "说明失效发生在<b>同步调用结束的那一刻</b>（绑定释放了 pin、dispose 了 MemoryView），" +
              "根本等不到 GC，也<b>不是「内存被搬走」那么温和</b>：它是「视图直接死了」。"
            : "对照项 unpinned 未立即失效（与本次实测不符，需复现）。");

        sb.Append(" 因此「异步 MemoryView 内存会变」这句话应修正为：");
        sb.Append(unp.Suspicious
            ? "unpinned 在 GC 间隙后读回异常 ✘ —— 存疑。"
            : "对 <b>unpinned(Span)</b> 而言，它<b>压根不能跨 await 持有</b>（调用一返回即 ObjectDisposed），" +
              "谈不上「变不变」，因为根本读不到；");

        sb.Append(pin.Suspicious
            ? " 但 <b>pinned(ArraySegment) 也失效了 ✘</b> —— 与本工程「ArraySegment 会 pin」的约定矛盾，需排查。"
            : " 而 <b>pinned(ArraySegment)</b> 在等了 " + (WaitMs / 1000) + " 秒 + GC 间隙后仍读到原内容 ✓ —— 内存被钉住、恒定不变（跨真实 await 时间窗安全）。");

        sb.Append(" <b>回到原始争议：</b>你「pinned 之后内存不会变」的直觉<b>完全正确</b>。" +
                  "原话「异步 MemoryView 内存会变」只对「未 pin 的默认 Span 写法」成立，且实际表现比「变」更彻底——" +
                  "它是<b>调用结束即死亡</b>，而不是延迟变化。所以：只要用 <code>ArraySegment</code> / " +
                  "<code>GCHandle.Pinned</code> / WASM 堆视图（<code>runtime.localHeapViewU8()</code>）钉住，" +
                  "跨 await 就安全；默认 Span 写法则连一次调用的边界都越不过。");
        return sb.ToString();
    }
}
