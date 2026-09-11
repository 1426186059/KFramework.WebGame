// 浏览器端入口由 main.js 通过 [JSExport] Client.Program.Init / Frame / Step 直接驱动，
// 不依赖 .NET 默认托管入口（dotnet.run）。此处仅保留一个合法的托管入口点 Main，
// 不执行任何逻辑——避免默认模板的 Stopwatch 循环去调用未注册的 dom.setInnerText（会触发 unreachable）。
class Program
{
    static void Main() { }
}
