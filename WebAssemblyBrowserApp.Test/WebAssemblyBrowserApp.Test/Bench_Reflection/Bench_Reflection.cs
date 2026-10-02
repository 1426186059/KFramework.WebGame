using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;      // GetCustomAttribute 是 CustomAttributeExtensions 上的扩展方法
using System.Text;
using System.Threading.Tasks;

/// <summary>
/// 模块：C# 反射性能。一个模块内跑 4 组场景，每组单独出一张结果表。
/// <para>
/// 覆盖自研 ORM / 网络封包序列化框架最常见的几类反射热点：
/// 属性逐个读写、对象创建、按特性反射调用、特性查询。
/// 每组内部都按耗时升序排列，最快的一行排在首位。
/// </para>
/// </summary>
public sealed class Bench_Reflection : IBenchModule
{
    public string Name => "C# 反射性能（属性读写 / 创建 / 调用 / 特性）";

    public string Summary =>
        "模拟 ORM 的 Load/Save 与封包的 Write/Read。四个场景各自出表：" +
        "属性读写、对象创建、方法调用、特性查询。结论通常是缓存元数据 + 编译访问器能快 5~50 倍。";

    public async Task<string> RunAsync()
    {
        var sb = new StringBuilder();
        sb.Append(PropertyAccess());
        await Task.Yield();
        sb.Append(Creation());
        await Task.Yield();
        sb.Append(Invoke());
        await Task.Yield();
        sb.Append(Attributes());
        return sb.ToString();
    }

    // ---------- 场景 1：属性读写 ----------
    private static string PropertyAccess()
    {
        int passes = 200;
        // 单次 action 内的操作数：passes × 样本数 × 属性数 × 2（get + set）
        long ops = (long)passes * BenchModels.Samples.Length * BenchModels.PropCount * 2;

        BenchTiming direct = BenchKit.Measure(() =>
        {
            for (int p = 0; p < passes; p++)
                for (int i = 0; i < BenchModels.Samples.Length; i++)
                    DirectIo(BenchModels.Samples[i]);
        }, ops);

        BenchTiming uncached = BenchKit.Measure(() =>
        {
            for (int p = 0; p < passes; p++)
                for (int i = 0; i < BenchModels.Samples.Length; i++)
                    ReflectionUncached(BenchModels.Samples[i]);
        }, ops);

        BenchTiming cached = BenchKit.Measure(() =>
        {
            for (int p = 0; p < passes; p++)
                for (int i = 0; i < BenchModels.Samples.Length; i++)
                    ReflectionCached(BenchModels.Samples[i]);
        }, ops);

        BenchTiming compiled = BenchKit.Measure(() =>
        {
            for (int p = 0; p < passes; p++)
                for (int i = 0; i < BenchModels.Samples.Length; i++)
                    CompiledIo(BenchModels.Samples[i]);
        }, ops);

        var rows = new List<BenchRow>
        {
            new("直接访问（手写）", "理论下限", direct, ops),
            new("反射-未缓存元数据", "每次 GetProperties + GetCustomAttribute + Get/SetValue", uncached, ops),
            new("反射-缓存元数据", "属性列表已缓存，仍走 PropertyInfo.Get/SetValue", cached, ops),
            new("编译访问器（Expression）", "缓存的 Func/Action，绕开 PropertyInfo", compiled, ops),
        };

        return BenchKit.Section(
            "1. 属性读写",
            "每属性一次 Get + 一次 Set，共 " + BenchKit.Fmt(ops * direct.Iters) + " 次操作",
            rows,
            "光缓存属性列表还不够，大头是 PropertyInfo.Get/SetValue 的装箱与动态派发；" +
            "换成 Expression 编译的强类型访问器后基本能逼近直接访问。");
    }

    private static void DirectIo(BenchModels.Sample s)
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
    private static void ReflectionUncached(BenchModels.Sample s)
    {
        foreach (PropertyInfo p in s.GetType().GetProperties())
        {
            if (p.GetCustomAttribute<BenchModels.IgnoreAttribute>() != null) continue;
            if (!p.CanRead || !p.CanWrite) continue;
            object? v = p.GetValue(s);
            p.SetValue(s, v);
        }
    }

    private static void ReflectionCached(BenchModels.Sample s)
    {
        foreach (PropertyInfo p in BenchModels.Props)
        {
            object? v = p.GetValue(s);
            p.SetValue(s, v);
        }
    }

    private static void CompiledIo(BenchModels.Sample s)
    {
        for (int i = 0; i < BenchModels.Props.Length; i++)
            BenchModels.Setters[i](s, BenchModels.Getters[i](s));
    }

    // ---------- 场景 2：对象创建 ----------
    private static string Creation()
    {
        int n = 5000;

        BenchTiming direct = BenchKit.Measure(() =>
        {
            for (int i = 0; i < n; i++) _ = new BenchModels.Sample();
        }, n);

        BenchTiming activator = BenchKit.Measure(() =>
        {
            for (int i = 0; i < n; i++)
                _ = (BenchModels.Sample)Activator.CreateInstance(typeof(BenchModels.Sample))!;
        }, n);

        BenchTiming factory = BenchKit.Measure(() =>
        {
            for (int i = 0; i < n; i++) _ = (BenchModels.Sample)BenchModels.Factory();
        }, n);

        var rows = new List<BenchRow>
        {
            new("new 直接创建", "理论下限", direct, n),
            new("Activator.CreateInstance", "按 Type 每次查构造函数", activator, n),
            new("Expression 编译工厂", "Type → Func<object> 缓存一次后复用", factory, n),
        };

        return BenchKit.Section(
            "2. 对象创建",
            BenchKit.Fmt(n) + " 次创建",
            rows,
            "Activator 的开销主要来自每次按 Type 查找构造函数；缓存一个 Func<object> 工厂即可基本抹平。");
    }

    // ---------- 场景 3：方法调用 ----------
    private static string Invoke()
    {
        int passes = 2000;
        long ops = (long)passes * BenchModels.Samples.Length;

        BenchTiming direct = BenchKit.Measure(() =>
        {
            for (int p = 0; p < passes; p++)
                for (int i = 0; i < BenchModels.Samples.Length; i++)
                    BenchModels.Samples[i].Finish();
        }, ops);

        BenchTiming invoke = BenchKit.Measure(() =>
        {
            for (int p = 0; p < passes; p++)
                for (int i = 0; i < BenchModels.Samples.Length; i++)
                    BenchModels.FinishMethod.Invoke(BenchModels.Samples[i], null);
        }, ops);

        BenchTiming del = BenchKit.Measure(() =>
        {
            for (int p = 0; p < passes; p++)
                for (int i = 0; i < BenchModels.Samples.Length; i++)
                    BenchModels.FinishDelegate(BenchModels.Samples[i]);
        }, ops);

        var rows = new List<BenchRow>
        {
            new("直接调用", "理论下限", direct, ops),
            new("MethodInfo.Invoke", "每次按 MethodInfo 动态派发", invoke, ops),
            new("Delegate.CreateDelegate", "缓存的强类型委托", del, ops),
        };

        return BenchKit.Section(
            "3. 方法调用",
            BenchKit.Fmt(passes) + "×" + BenchModels.Samples.Length + " 次调用",
            rows,
            "反射调用本身不是瓶颈，慢在每次重新查方法；把 MethodInfo 缓存并转成委托后差距几乎消失。");
    }

    // ---------- 场景 4：特性查询 ----------
    [UnconditionalSuppressMessage("System.Diagnostics.CodeAnalysis", "IL2075",
        Justification = "反射测试：运行期确实要枚举任意实例的所有属性。")]
    private static string Attributes()
    {
        int passes = 500;
        long ops = (long)passes * BenchModels.Samples.Length * BenchModels.PropCount;

        BenchTiming perCall = BenchKit.Measure(() =>
        {
            for (int p = 0; p < passes; p++)
                for (int i = 0; i < BenchModels.Samples.Length; i++)
                    foreach (PropertyInfo pr in BenchModels.Samples[i].GetType().GetProperties())
                        _ = pr.GetCustomAttribute<BenchModels.IgnoreAttribute>();
        }, ops);

        BenchTiming cached = BenchKit.Measure(() =>
        {
            for (int p = 0; p < passes; p++)
                for (int i = 0; i < BenchModels.Samples.Length; i++)
                    foreach (PropertyInfo pr in BenchModels.Props)
                        _ = pr.GetCustomAttribute<BenchModels.IgnoreAttribute>();
        }, ops);

        var rows = new List<BenchRow>
        {
            new("每次 GetProperties + GetCustomAttribute", "完全未缓存", perCall, ops),
            new("仅 GetCustomAttribute", "属性列表已缓存，特性仍每次查", cached, ops),
        };

        return BenchKit.Section(
            "4. 特性查询",
            BenchKit.Fmt(passes) + "×" + BenchModels.Samples.Length + "×" + BenchModels.PropCount + " 次查询",
            rows,
            "GetProperties() 每次重建 PropertyInfo 数组是主要成本；" +
            "属性列表缓存后，剩下的 GetCustomAttribute 也建议一并缓存成 bool 标记。");
    }
}
