using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

// 反射类测试共用的数据模型，以及预先缓存好的元数据 / 编译访问器。
// 单独成文件，供"属性读写 / 对象创建 / 方法调用 / 特性查询"四个模块共用，
// 避免同一份模型在多个测试文件里各写一遍而逐渐走样。
public static class BenchModels
{
    public enum State { None, Idle, Moving, Combat }

    [AttributeUsage(AttributeTargets.Property)]
    public sealed class IgnoreAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class CompleteAttribute : Attribute { }

    /// <summary>测试用数据模型，属性类型尽量多样（int/string/double/bool/enum/DateTime）。</summary>
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

    // ---- 缓存的元数据（对应"已缓存"档位）----

    public static readonly PropertyInfo[] Props = typeof(Sample)
        .GetProperties()
        .Where(p => p.CanRead && p.CanWrite && p.GetCustomAttribute<IgnoreAttribute>() == null)
        .ToArray();

    public static readonly Func<object, object>[] Getters = Props.Select(MakeGetter).ToArray();
    public static readonly Action<object, object>[] Setters = Props.Select(MakeSetter).ToArray();

    public static readonly Func<object> Factory = MakeFactory(typeof(Sample));

    public static readonly MethodInfo FinishMethod = typeof(Sample).GetMethods()
        .First(m => m.GetCustomAttribute<CompleteAttribute>() != null);

    public static readonly Action<Sample> FinishDelegate = (Action<Sample>)
        Delegate.CreateDelegate(typeof(Action<Sample>), FinishMethod);

    public static readonly Sample[] Samples = Enumerable.Range(0, 128).Select(_ => new Sample()).ToArray();

    public static int PropCount => Props.Length;

    // ---- 用 Expression 编译出强类型访问器 / 工厂 ----

    public static Func<object, object> MakeGetter(PropertyInfo p)
    {
        var param = Expression.Parameter(typeof(object), "o");
        var cast = Expression.Convert(param, p.DeclaringType!);
        var prop = Expression.Property(cast, p);
        var box = Expression.Convert(prop, typeof(object));
        return Expression.Lambda<Func<object, object>>(box, param).Compile();
    }

    public static Action<object, object> MakeSetter(PropertyInfo p)
    {
        var paramO = Expression.Parameter(typeof(object), "o");
        var paramV = Expression.Parameter(typeof(object), "v");
        var castO = Expression.Convert(paramO, p.DeclaringType!);
        var castV = Expression.Convert(paramV, p.PropertyType);
        var assign = Expression.Assign(Expression.Property(castO, p), castV);
        return Expression.Lambda<Action<object, object>>(assign, paramO, paramV).Compile();
    }

    public static Func<object> MakeFactory(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type t)
    {
        var ctor = Expression.New(t);
        return Expression.Lambda<Func<object>>(ctor).Compile();
    }
}
