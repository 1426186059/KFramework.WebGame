using System;
using System.Text.Json;

// 列出 JSON 文件顶层所有键（配置表名）
var path = args.Length > 0 ? args[0] : throw new Exception("用法: ListTables <json>");
using var doc = JsonDocument.Parse(System.IO.File.ReadAllBytes(path));
var root = doc.RootElement;
if (root.ValueKind == JsonValueKind.Object)
{
    Console.WriteLine($"顶层表数: {root.GetPropertyCount()}");
    foreach (var p in root.EnumerateObject())
    {
        var kind = p.Value.ValueKind;
        int count = 0;
        if (kind == JsonValueKind.Object) count = p.Value.GetPropertyCount();
        else if (kind == JsonValueKind.Array) count = p.Value.GetArrayLength();
        Console.WriteLine($"  {p.Name}  [{kind}]  条目={count}");
    }
}
else Console.WriteLine("根不是对象");
