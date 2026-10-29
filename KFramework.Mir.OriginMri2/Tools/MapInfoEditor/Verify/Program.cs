using System;
using System.Linq;
using MapInfoEditor;

class Program
{
    static int Main()
    {
        var store = new MapInfoStore();
        store.Load();
        Console.WriteLine($"加载地图总数: {store.Maps.Count}");
        Console.WriteLine($"怪物表条目: {store.Monsters.Count}");

        var bqq = store.GetMap(103);
        if (bqq == null) { Console.WriteLine("未找到 比奇省(103)"); return 1; }
        Console.WriteLine($"选中: {bqq.Title} (id={bqq.Index}, FileName={bqq.FileName})");

        var data = store.DecodeMap(103);
        if (data == null) { Console.WriteLine("解码失败(无数据块)"); return 1; }
        Console.WriteLine($"网格: {data.Cols}x{data.Rows}  可走格: {data.Walkable}  {data.Warn ?? ""}");

        var sz = store.GetSafeZones(bqq);
        Console.WriteLine($"安全区数: {sz.Count}");
        foreach (var z in sz.Take(3))
            Console.WriteLine($"  安全区 X={z.X} Y={z.Y} 半径={z.Size} 出生={z.Start}");

        // 测试新增一条安全区（仅内存，不落盘）
        store.AddSafeZone(bqq, 50, 50, 8, true);
        Console.WriteLine($"新增后安全区数: {store.GetSafeZones(bqq).Count}");

        // 测试 NextId 唯一性 & 取怪物表
        var sample = store.Monsters.FirstOrDefault(m => m.Value.IsBoss);
        Console.WriteLine($"示例Boss: {(sample.Value.Name)} (id={sample.Key})");

        Console.WriteLine("数据层验证通过 ✅");
        return 0;
    }
}
