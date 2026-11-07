using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using KFramework.MonoGame;
using KFramework.MonoGameExtend;

// MirEngine 全局 using 也导出了 Rectangle / Vector2；这里用别名消歧，明确使用 KFramework.MonoGame 版本。
using Rectangle = KFramework.MonoGame.Rectangle;
using Vector2 = KFramework.MonoGame.Vector2;

/// <summary>
/// 原始传奇（移动版）精灵表（Egret 图集）解析：一张图集 = 一个图集 json（帧信息）+ 同目录 png（整页贴图）。
/// 解析后每帧产出框架标准的 KSpriteInfo（整页贴图 + 源矩形 + 原始尺寸），可直接交给 SpriteBatch 绘制。
/// 构建方式对齐 KFramework.MonoGameExtend.KTexturePacker.SpriteSheetLoader：pivot 默认左上、不旋转。
/// 这是网页版 Egret 图集，与桌面端 WIL/WIX（MLibrary/MImage）是两套体系。
///
/// 支持三种线上格式：
///   1) UI/通用图集 -sheet.json：{"file":"x.png","frames":{"帧名":{x,y,w,h,sourceW,sourceH}}}   ← frames 是字典
///   2) 模型独立图集 model/{id}/{srcId}.json：
///      {"count":4,"file":"4171010.png","frames":[{x,y,w,h,offX,offY,sourceW,sourceH}, ...]}   ← frames 是数组
///      帧名按数组序号 "0","1"...；offX/offY 是该帧在虚拟画布 sourceW×sourceH 里的注册点偏移，
///      绘制时须按画布中心还原（见 Anchors / SourceW / SourceH），否则人物会飘出格子。
///   3) Egret MovieClip：无 "frames"，用 "res"（子图矩形）+ "mc"（时间线帧序列），PlayOrder 取首条时间线。
/// </summary>
public sealed class Mir2AtlasSheet
{
    public string File = ""; // 同目录整页贴图文件名（png）

    /// <summary>已加载的整页贴图（Build 后可用）。</summary>
    public Texture2D Texture { get; private set; }

    /// <summary>帧名 → KSpriteInfo（整页贴图上的子图，Build 后可用）。</summary>
    public Dictionary<string, KSpriteInfo> Frames { get; private set; }

    /// <summary>MovieClip 播放顺序（取 mc 首个时间线 frames 的 res 引用序列）；普通精灵图集为空。</summary>
    public List<string> PlayOrder { get; private set; }

    /// <summary>帧名 → (offX, offY) 注册点偏移。仅模型帧（frames 数组）格式非零，UI 图集全为 0。</summary>
    public Dictionary<string, Vector2> Anchors { get; private set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>模型帧的虚拟画布尺寸（frames 数组元素里的 sourceW/sourceH），用于还原注册点；UI 图集为 0。</summary>
    public int SourceW, SourceH;

    private List<FrameDef> _defs = new();

    private sealed class FrameDef
    {
        public string Name;
        public Rectangle SourceRect;
        public Vector2 Size;       // 原始（未裁剪）尺寸
        public int OffX, OffY;     // 模型帧注册点偏移
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
            // 模型独立图集：frames 是数组，每帧自带 offX/offY 注册点与虚拟画布 sourceW/sourceH。
            else if (obj["frames"] is JsonArray modelFrames)
            {
                int i = 0;
                foreach (var frameNode in modelFrames)
                {
                    if (frameNode is JsonObject o)
                    {
                        int x = GetInt(o, "x"), y = GetInt(o, "y"), w = GetInt(o, "w"), h = GetInt(o, "h");
                        int sw = GetInt(o, "sourceW", w), sh = GetInt(o, "sourceH", h);
                        if (sheet.SourceW == 0 && sw > 0 && sh > 0)
                        {
                            sheet.SourceW = sw;
                            sheet.SourceH = sh;
                        }
                        sheet._defs.Add(new FrameDef
                        {
                            Name = i.ToString(),
                            SourceRect = new Rectangle(x, y, w, h),
                            Size = new Vector2(w, h),
                            OffX = GetInt(o, "offX"),
                            OffY = GetInt(o, "offY")
                        });
                    }
                    i++;
                }
            }
            // Egret MovieClip 导出格式：无 "frames"，改用 "res"（子图在大图上的矩形）+ "mc"（时间线帧序列）。
            else if (obj["res"] is JsonObject resNode)
            {
                foreach (var kv in resNode)
                {
                    if (kv.Value is JsonObject o)
                    {
                        int x = GetInt(o, "x"), y = GetInt(o, "y"), w = GetInt(o, "w"), h = GetInt(o, "h");
                        sheet._defs.Add(new FrameDef
                        {
                            Name = kv.Key,
                            SourceRect = new Rectangle(x, y, w, h),
                            Size = new Vector2(w, h)
                        });
                    }
                }
                // 取首个 mc 时间线的帧序列作为播放顺序（如 mc["1"].frames = [{res:"0001"}, ...]）。
                if (obj["mc"] is JsonObject mcNode && mcNode.Count > 0)
                {
                    JsonObject first = null;
                    foreach (var kvp in mcNode)
                    {
                        first = kvp.Value as JsonObject;
                        break;
                    }
                    if (first != null && first["frames"] is JsonArray mframes)
                    {
                        var order = new List<string>();
                        foreach (var mf in mframes)
                        {
                            if (mf is JsonObject fo && fo["res"] is JsonValue rv && rv.TryGetValue<string>(out var resId))
                                order.Add(resId);
                        }
                        sheet.PlayOrder = order;
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
        var anchors = new Dictionary<string, Vector2>(StringComparer.OrdinalIgnoreCase);
        foreach (var d in _defs)
        {
            frames[d.Name] = new KSpriteInfo(d.Name, pageTexture, d.SourceRect, d.Size, Vector2.Zero, false);
            anchors[d.Name] = new Vector2(d.OffX, d.OffY);
        }
        Frames = frames;
        Anchors = anchors;
    }

    /// <summary>
    /// 按帧序取帧名：MovieClip 走 PlayOrder 时间线；其余按帧名字典序（模型帧名 "0","1","2"... 正好是播放顺序）。
    /// </summary>
    public bool TryGetFrameName(int frameIndex, out string frameName)
    {
        frameName = null;
        if (Frames == null || Frames.Count == 0) return false;
        int n = Math.Abs(frameIndex);
        if (PlayOrder != null && PlayOrder.Count > 0)
        {
            frameName = PlayOrder[n % PlayOrder.Count];
            return true;
        }
        var names = new List<string>(Frames.Keys);
        names.Sort(StringComparer.Ordinal);
        frameName = names[n % names.Count];
        return true;
    }

    private static int GetInt(JsonObject o, string k, int fallback = 0)
    { 
        return o[k] is JsonValue v && v.TryGetValue<int>(out var i) ? i : fallback;
    }

}
