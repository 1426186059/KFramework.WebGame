using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using KFramework.MonoGame;
// MirEngine 全局 using 也导出了 Rectangle；这里用别名消歧，明确使用 KFramework.MonoGame.Rectangle。
using Rectangle = KFramework.MonoGame.Rectangle;

/// <summary>
/// 原始传奇（移动版）精灵表（Egret 图集）解析：一张图集 = 一个 -sheet.json（帧信息）+ 同目录 -sheet.png（整页贴图）。
/// sheet.json 形如：
///   {"file":"advance-sheet.png","frames":{"adv_fail":{"x":0,"y":0,"w":229,"h":72,"offX":0,"offY":0,"sourceW":229,"sourceH":74}, ...}}
/// 帧即整页贴图上的一个子矩形 + 裁剪偏移 + 原始尺寸；绘制时整页贴图 + source rect 即可（见 Mir2AtlasLibrary.DrawFrame）。
/// 这是网页版 Egret 图集，与桌面端 WIL/WIX（MLibrary/MImage）是两套体系。
/// </summary>
public sealed class Mir2AtlasFrame
{
    public string Name = "";
    public int X, Y, W, H;       // 在整页贴图中的子矩形
    public int OffX, OffY;       // 裁剪偏移（trim），绘制时加到位
    public int SourceW, SourceH; // 原始（未裁剪）尺寸
    public Rectangle SourceRect => new Rectangle(X, Y, W, H);
}

public sealed class Mir2AtlasSheet
{
    public string File = ""; // 同目录整页贴图文件名（png）
    public Dictionary<string, Mir2AtlasFrame> Frames { get; } = new(StringComparer.OrdinalIgnoreCase);

    public static Mir2AtlasSheet Parse(string json)
    {
        var sheet = new Mir2AtlasSheet();
        var node = JsonNode.Parse(json);
        if (node is JsonObject obj)
        {
            if (obj["file"] is JsonValue fv && fv.TryGetValue<string>(out var f)) sheet.File = f;
            if (obj["frames"] is JsonObject fr)
            {
                foreach (var kv in fr)
                {
                    if (kv.Value is JsonObject o)
                        sheet.Frames[kv.Key] = new Mir2AtlasFrame
                        {
                            Name = kv.Key,
                            X = GetInt(o, "x"),
                            Y = GetInt(o, "y"),
                            W = GetInt(o, "w"),
                            H = GetInt(o, "h"),
                            OffX = GetInt(o, "offX"),
                            OffY = GetInt(o, "offY"),
                            SourceW = GetInt(o, "sourceW", GetInt(o, "w")),
                            SourceH = GetInt(o, "sourceH", GetInt(o, "h")),
                        };
                }
            }
        }
        return sheet;
    }

    private static int GetInt(JsonObject o, string k, int fallback = 0)
        => o[k] is JsonValue v && v.TryGetValue<int>(out var i) ? i : fallback;
}
