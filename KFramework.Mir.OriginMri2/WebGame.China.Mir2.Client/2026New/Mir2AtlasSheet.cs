using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using KFramework.MonoGame;
using KFramework.MonoGameExtend;

// MirEngine 全局 using 也导出了 Rectangle / Vector2；这里用别名消歧，明确使用 KFramework.MonoGame 版本。
using Rectangle = KFramework.MonoGame.Rectangle;
using Vector2 = KFramework.MonoGame.Vector2;

/// <summary>
/// 原始传奇（移动版）精灵表（Egret 图集）解析：一张图集 = 一个 -sheet.json（帧信息）+ 同目录 -sheet.png（整页贴图）。
/// sheet.json 形如：
///   {"file":"advance-sheet.png","frames":{"adv_fail":{"x":0,"y":0,"w":229,"h":72,"offX":0,"offY":0,"sourceW":229,"sourceH":74}, ...}}
/// 解析后每帧产出框架标准的 KSpriteInfo（整页贴图 + 源矩形 + 原始尺寸），可直接交给 SpriteBatch 绘制。
/// 构建方式对齐 KFramework.MonoGameExtend.KTexturePacker.SpriteSheetLoader：pivot 默认左上、不旋转。
/// 这是网页版 Egret 图集，与桌面端 WIL/WIX（MLibrary/MImage）是两套体系。
/// </summary>
public sealed class Mir2AtlasSheet
{
    public string File = ""; // 同目录整页贴图文件名（png）

    /// <summary>已加载的整页贴图（Build 后可用）。</summary>
    public Texture2D Texture { get; private set; }

    /// <summary>帧名 → KSpriteInfo（整页贴图上的子图，Build 后可用）。</summary>
    public Dictionary<string, KSpriteInfo> Frames { get; private set; }

    private List<FrameDef> _defs = new();

    private sealed class FrameDef
    {
        public string Name;
        public Rectangle SourceRect;
        public Vector2 Size; // 原始（未裁剪）尺寸
    }

    /// <summary>仅解析 JSON（不含贴图）。返回的实例需再调用 Build(整页贴图) 才能得到 Frames。</summary>
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
                    {
                        int x = GetInt(o, "x"), y = GetInt(o, "y"), w = GetInt(o, "w"), h = GetInt(o, "h");
                        int sw = GetInt(o, "sourceW", w), sh = GetInt(o, "sourceH", h);
                        sheet._defs.Add(new FrameDef
                        {
                            Name = kv.Key,
                            SourceRect = new Rectangle(x, y, w, h),
                            Size = new Vector2(sw, sh)
                        });
                    }
                }
            }
        }
        return sheet;
    }

    /// <summary>绑定整页贴图，构建每帧的 KSpriteInfo（pivot 默认左上、不旋转，与框架 SpriteSheetLoader 一致）。</summary>
    public void Build(Texture2D pageTexture)
    {
        Texture = pageTexture;
        var frames = new Dictionary<string, KSpriteInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var d in _defs)
        {
            frames[d.Name] = new KSpriteInfo(d.Name, pageTexture, d.SourceRect, d.Size, Vector2.Zero, false);
        }
        Frames = frames;
    }

    private static int GetInt(JsonObject o, string k, int fallback = 0)
    { 
        return o[k] is JsonValue v && v.TryGetValue<int>(out var i) ? i : fallback;
    }

}
