using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

// 一个独立的 C# 反射性能测试。
// 覆盖自研 ORM / 网络封包序列化框架最常见的几类反射热点：
//   1) 属性逐个 GetValue/SetValue（DBObject.Load/Save、Packet.WriteObject）
//   2) 每次调用都重新 GetProperties() + GetCustomAttribute（未缓存元数据）
//   3) Activator.CreateInstance 创建对象（Packet.Receive）
//   4) MethodInfo.Invoke 调用后处理方法（[CompleteObject]）
//   5) 用 Expression 编译出强类型访问器 / 工厂，绕开 PropertyInfo 反射调用
public static class ReflectionBenchmark
{
    // 测试用的数据模型，属性类型尽量多样（int/string/double/bool/enum/DateTime）
    public enum State { None, Idle, Moving, Combat }

    [AttributeUsage(AttributeTargets.Property)]
    public sealed class IgnoreAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class CompleteAttribute : Attribute { }

    public sealed class Sample
    {
        public int Id { get; set; } = 1;
        public string Name { get; set; } = "abc";
        public double Value { get; set; } = 3.14;
        public bool Flag { get; set; } = true;
        public State Mode { get; set; } = State.Combat;
        public DateTime Time { get; set; } = DateTime.Now;

        [Ignore] public string Skip { get; set; } = "skip";

        [Complete]
        public void Finish() { }
    }

    // 参与读写测试的属性（排除带 [Ignore] 的）
    private static readonly PropertyInfo[] _props = typeof(Sample)
        .GetProperties()
        .Where(p => p.CanRead && p.CanWrite && p.GetCustomAttribute<IgnoreAttribute>() == null)
        .ToArray();

    // 编译出来的强类型访问器（绕开 PropertyInfo 调用开销）
    private static readonly Func<object, object>[] _getters = _props.Select(MakeGetter).ToArray();
    private static readonly Action<object, object>[] _setters = _props.Select(MakeSetter).ToArray();

    // 缓存的创建工厂（Expression.New）
    private static readonly Func<object> _factory = MakeFactory(typeof(Sample));

    // 缓存的方法调用委托
    private static readonly MethodInfo _finishMethod = typeof(Sample).GetMethods()
        .First(m => m.GetCustomAttribute<CompleteAttribute>() != null);
    private static readonly Action<Sample> _finishDelegate = (Action<Sample>)
        Delegate.CreateDelegate(typeof(Action<Sample>), _finishMethod);

    private static readonly Sample[] _samples = Enumerable.Range(0, 128)
        .Select(_ => new Sample())
        .ToArray();

    private static int PropCount => _props.Length;

    public static async Task RunAsync()
    {
        var sb = new StringBuilder();
        sb.Append("<h2>C# 反射性能基准测试</h2>");
        sb.Append("<p class='muted'>每个场景先 warmup，再自适应放大迭代到约 60ms 后计时。数字越低越好；\"x\" 列为相对基线（直接访问）的加速倍数。</p>");

        Host.SetInnerHTML("#results", sb.ToString());
        Host.SetInnerText("#status", "运行中：属性读写...");
        await Task.Delay(16);

        sb.Append(BenchPropertyAccess());
        Host.SetInnerHTML("#results", sb.ToString());

        Host.SetInnerText("#status", "运行中：对象创建...");
        await Task.Delay(16);
        sb.Append(BenchCreation());
        Host.SetInnerHTML("#results", sb.ToString());

        Host.SetInnerText("#status", "运行中：方法调用 / 特性查询...");
        await Task.Delay(16);
        sb.Append(BenchInvoke());
        sb.Append(BenchAttributes());
        Host.SetInnerHTML("#results", sb.ToString());

        sb.Append("<p class='muted'>结论：把 ① 每次 GetProperties/GetCustomAttribute 缓存到静态字典，② 用 Expression/Delegate 编译出强类型访问器，通常能把反射热点降低 5~50 倍，逼近直接访问。</p>");
        Host.SetInnerHTML("#results", sb.ToString());
        Host.SetInnerText("#status", "完成 ✓");
    }

    // ---------- 1. 属性读写 ----------
    private static string BenchPropertyAccess()
    {
        int passes = 200;
        // 单次 action 内的操作数：passes * samples * PropCount * 2(get+set)
        long opsPerAction = (long)passes * _samples.Length * PropCount * 2;

        var direct = Measure(() =>
        {
            for (int p = 0; p < passes; p++)
                for (int i = 0; i < _samples.Length; i++)
                    DirectIo(_samples[i]);
        }, opsPerAction);

        var reflectedUncached = Measure(() =>
        {
            for (int p = 0; p < passes; p++)
                for (int i = 0; i < _samples.Length; i++)
                    ReflectionUncached(_samples[i]);
        }, opsPerAction);

        var reflectedCached = Measure(() =>
        {
            for (int p = 0; p < passes; p++)
                for (int i = 0; i < _samples.Length; i++)
                    ReflectionCached(_samples[i]);
        }, opsPerAction);

        var compiled = Measure(() =>
        {
            for (int p = 0; p < passes; p++)
                for (int i = 0; i < _samples.Length; i++)
                    CompiledIo(_samples[i]);
        }, opsPerAction);

        var rows = new List<(string, string, Timing)>
        {
            ("直接访问（手写）", "理论下限", direct),
            ("反射-未缓存元数据", "每次 GetProperties + GetCustomAttribute + Get/SetValue", reflectedUncached),
            ("反射-缓存元数据", "属性列表缓存，仍走 PropertyInfo.Get/SetValue", reflectedCached),
            ("编译访问器（Expression）", "缓存的 Func/Action，绕开 PropertyInfo", compiled),
        };

        return Section(
            "1. 属性读写（模拟 DBObject.Load/Save、Packet.WriteObject）",
            "每属性一次 Get + 一次 Set；共 " + (opsPerAction * direct.Iters).ToString("N0", CultureInfo.InvariantCulture) + " 次操作",
            rows, direct);
    }

    private static void DirectIo(Sample s)
    {
        s.Id = s.Id;
        s.Name = s.Name;
        s.Value = s.Value;
        s.Flag = s.Flag;
        s.Mode = s.Mode;
        s.Time = s.Time;
    }

    [UnconditionalSuppressMessage("System.Diagnostics.CodeAnalysis", "IL2075",
        Justification = "反射测试：运行期确实要枚举任意实例的所有属性。")]
    private static void ReflectionUncached(Sample s)
    {
        foreach (var p in s.GetType().GetProperties())
        {
            if (p.GetCustomAttribute<IgnoreAttribute>() != null) continue;
            if (!p.CanRead || !p.CanWrite) continue;
            var v = p.GetValue(s);
            p.SetValue(s, v);
        }
    }

    private static void ReflectionCached(Sample s)
    {
        foreach (var p in _props)
        {
            var v = p.GetValue(s);
            p.SetValue(s, v);
        }
    }

    private static void CompiledIo(Sample s)
    {
        for (int i = 0; i < _props.Length; i++)
            _setters[i](s, _getters[i](s));
    }

    // ---------- 2. 对象创建 ----------
    private static string BenchCreation()
    {
        int n = 5000;
        long opsPerAction = n;

        var direct = Measure(() =>
        {
            for (int i = 0; i < n; i++)
                _ = new Sample();
        }, opsPerAction);

        var activator = Measure(() =>
        {
            for (int i = 0; i < n; i++)
                _ = (Sample)Activator.CreateInstance(typeof(Sample));
        }, opsPerAction);

        var factory = Measure(() =>
        {
            for (int i = 0; i < n; i++)
                _ = (Sample)_factory();
        }, opsPerAction);

        var rows = new List<(string, string, Timing)>
        {
            ("new 直接创建", "理论下限", direct),
            ("Activator.CreateInstance", "Packet.Receive 现状", activator),
            ("Expression 编译工厂", "Type -> Func<object> 缓存", factory),
        };

        return Section(
            "2. 对象创建（模拟 Packet.Receive 的实例化）",
            n.ToString("N0", CultureInfo.InvariantCulture) + " 次创建",
            rows, direct);
    }

    // ---------- 3. 方法调用 ----------
    private static string BenchInvoke()
    {
        int passes = 2000;
        long opsPerAction = (long)passes * _samples.Length;

        var direct = Measure(() =>
        {
            for (int p = 0; p < passes; p++)
                for (int i = 0; i < _samples.Length; i++)
                    _samples[i].Finish();
        }, opsPerAction);

        var invoke = Measure(() =>
        {
            for (int p = 0; p < passes; p++)
                for (int i = 0; i < _samples.Length; i++)
                    _finishMethod.Invoke(_samples[i], null);
        }, opsPerAction);

        var del = Measure(() =>
        {
            for (int p = 0; p < passes; p++)
                for (int i = 0; i < _samples.Length; i++)
                    _finishDelegate(_samples[i]);
        }, opsPerAction);

        var rows = new List<(string, string, Timing)>
        {
            ("直接调用", "理论下限", direct),
            ("MethodInfo.Invoke", "Packet.ReadObject 后处理现状", invoke),
            ("Delegate.CreateDelegate", "缓存的强类型委托", del),
        };

        return Section(
            "3. 方法调用（模拟 [CompleteObject] 后处理）",
            passes.ToString("N0", CultureInfo.InvariantCulture) + "×" + _samples.Length + " 次调用",
            rows, direct);
    }

    // ---------- 4. 特性查询 ----------
    [UnconditionalSuppressMessage("System.Diagnostics.CodeAnalysis", "IL2075",
        Justification = "反射测试：运行期确实要枚举任意实例的所有属性。")]
    private static string BenchAttributes()
    {
        int passes = 500;
        long opsPerAction = (long)passes * _samples.Length * PropCount;

        var perCall = Measure(() =>
        {
            for (int p = 0; p < passes; p++)
                for (int i = 0; i < _samples.Length; i++)
                    foreach (var pr in _samples[i].GetType().GetProperties())
                        _ = pr.GetCustomAttribute<IgnoreAttribute>();
        }, opsPerAction);

        var cached = Measure(() =>
        {
            for (int p = 0; p < passes; p++)
                for (int i = 0; i < _samples.Length; i++)
                    foreach (var pr in _props)
                        _ = pr.GetCustomAttribute<IgnoreAttribute>();
        }, opsPerAction);

        var rows = new List<(string, string, Timing)>
        {
            ("每次 GetProperties + GetCustomAttribute", "未缓存元数据", perCall),
            ("仅 GetCustomAttribute（属性列表已缓存）", "元数据缓存一半", cached),
        };

        return Section(
            "4. 特性查询开销（模拟逐属性 [Ignore] 判定）",
            passes.ToString("N0", CultureInfo.InvariantCulture) + "×" + _samples.Length + "×" + PropCount + " 次查询",
            rows, perCall);
    }

    // ---------- 工具 ----------
    private sealed record Timing(double Ms, long Iters)
    {
        public double NsPerOp(long opsTotal) => opsTotal > 0 ? Ms * 1_000_000.0 / opsTotal : 0;
    }

    private static string Section(string title, string subtitle, List<(string, string, Timing)> rows, Timing baseline)
    {
        var sb = new StringBuilder();
        sb.Append("<div class='card'><h3>").Append(Escape(title)).Append("</h3>");
        sb.Append("<p class='muted'>").Append(Escape(subtitle)).Append("</p>");
        sb.Append("<table><thead><tr><th>方式</th><th>说明</th><th>耗时(ms)</th><th>ns/操作</th><th>加速 x</th></tr></thead><tbody>");
        foreach (var (name, note, t) in rows)
        {
            double x = baseline.Ms > 0 ? baseline.Ms / t.Ms : 0;
            double ns = t.NsPerOp(t.Iters * opsPerOneCall);
            sb.Append("<tr>")
                .Append("<td>").Append(Escape(name)).Append("</td>")
                .Append("<td class='muted'>").Append(Escape(note)).Append("</td>")
                .Append("<td>").Append(t.Ms.ToString("F2", CultureInfo.InvariantCulture)).Append("</td>")
                .Append("<td>").Append(ns.ToString("F1", CultureInfo.InvariantCulture)).Append("</td>")
                .Append("<td>").Append(x.ToString("F2", CultureInfo.InvariantCulture)).Append("</td>")
                .Append("</tr>");
        }
        sb.Append("</tbody></table></div>");
        return sb.ToString();
    }

    // opsPerOneCall：单次 action 内的操作数，用于把 ms 换算成 ns/操作
    private static long opsPerOneCall = 1;

    // 自适应校准计时：先 warmup，再 2 倍放大迭代直到超过 targetMs
    private static Timing Measure(Action action, long opsPerAction, int warmup = 3, int targetMs = 60, int maxIters = 1 << 18)
    {
        for (int i = 0; i < warmup; i++) action();

        int iters = 1;
        long ticks = 0;
        while (true)
        {
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iters; i++) action();
            sw.Stop();
            ticks = sw.ElapsedTicks;
            if (iters >= maxIters || sw.ElapsedMilliseconds >= targetMs) break;
            iters *= 2;
        }
        opsPerOneCall = opsPerAction;
        double ms = (ticks / (double)Stopwatch.Frequency) * 1000.0;
        return new Timing(ms, iters);
    }

    private static Func<object, object> MakeGetter(PropertyInfo p)
    {
        var param = Expression.Parameter(typeof(object), "o");
        var cast = Expression.Convert(param, p.DeclaringType!);
        var prop = Expression.Property(cast, p);
        var box = Expression.Convert(prop, typeof(object));
        return Expression.Lambda<Func<object, object>>(box, param).Compile();
    }

    private static Action<object, object> MakeSetter(PropertyInfo p)
    {
        var paramO = Expression.Parameter(typeof(object), "o");
        var paramV = Expression.Parameter(typeof(object), "v");
        var castO = Expression.Convert(paramO, p.DeclaringType!);
        var castV = Expression.Convert(paramV, p.PropertyType);
        var assign = Expression.Assign(Expression.Property(castO, p), castV);
        return Expression.Lambda<Action<object, object>>(assign, paramO, paramV).Compile();
    }

    private static Func<object> MakeFactory([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type t)
    {
        var ctor = Expression.New(t);
        return Expression.Lambda<Func<object>>(ctor).Compile();
    }

    private static string Escape(string s) => s
        .Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}

// 与 Program.cs 中的 JSImport 宿主对应的局部类
internal partial class Host
{
    [System.Runtime.InteropServices.JavaScript.JSImport("dom.setInnerHTML", "main.js")]
    internal static partial void SetInnerHTML(string selector, string html);

    [System.Runtime.InteropServices.JavaScript.JSImport("dom.setInnerText", "main.js")]
    internal static partial void SetInnerText(string selector, string text);
}
