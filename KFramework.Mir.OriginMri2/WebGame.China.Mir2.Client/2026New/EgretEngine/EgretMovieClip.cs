using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using KFramework.MonoGame;

using Rectangle = KFramework.MonoGame.Rectangle;

// 白鹭(Egret) MovieClip 解析：与 egret.MovieClipDataFactory / egret.MovieClipData / egret.MovieClip 一一对应。
//
// 白鹭引擎里“普通图集”与“Movie 图集”的类关系：
//   * 普通图集 = SpriteSheet（TextureAtlas）：只有 res/frames 子图矩形 + 整页贴图，无时间线。
//     对应本工程 EgretAtlas.Frames（res 子图矩形）—— 即 Egret 的 SpriteSheet。
//   * Movie 图集 = 在普通图集 JSON 基础上额外带 "mc" 段：mc 是若干“时间线”，每条时间线是一组
//     按顺序排列的帧；每帧引用一个 res 子图，并带相对注册点的偏移(x,y)与停留时长(duration)。
//     MovieClipDataFactory 把 "mc" 时间线 + "res" 子图矩形绑定到整页贴图，产出 MovieClipData；
//     MovieClip(display) 再消费 MovieClipData 播放。
//
// 所以本工程分成三类、职责互不混：
//   EgretAtlas            普通图集（静态坐标映射）
//   EgretMovieClipAtlas    MovieClip 图集（继承 EgretAtlas，多一份 mc 时序）
//   EgretMovieClip         时序对象，【引入（持有）一个 EgretAtlas】，负责播放序列 + 统一注册点

/// <summary>白鹭 MovieClip 的一帧（对应 egret.MovieClipDataFrame）。</summary>
public sealed class EgretMovieClipDataFrame
{
    /// <summary>该帧引用的 res 子图名（对应 "mc" 帧里的 res 字段）。</summary>
    public string ResId;

    /// <summary>该帧相对 MovieClip 注册点的偏移 X（Egret 把此偏移并入子图的 offset）。</summary>
    public int X;

    /// <summary>该帧相对 MovieClip 注册点的偏移 Y。</summary>
    public int Y;

    /// <summary>该帧停留帧数（Egret duration，缺省 1）。</summary>
    public int Duration = 1;
}

/// <summary>白鹭 MovieClip 的命名标签（对应 Egret MovieClipData.labels，可选）。</summary>
public sealed class EgretMovieClipLabel
{
    public string Name;
    public int Frame;
}

/// <summary>白鹭 MovieClip 的帧事件（对应 Egret MovieClipData.events，可选）。</summary>
public sealed class EgretMovieClipEvent
{
    public string Name;
    public int Frame;
}

/// <summary>白鹭 MovieClip 的一条时间线（对应 egret.MovieClipData）。只含时序，不含贴图。</summary>
public sealed class EgretMovieClipData
{
    public string Name;

    /// <summary>帧率（Egret frameRate，缺省 24）。</summary>
    public int FrameRate = 24;

    /// <summary>每帧间隔(ms) = 1000 / FrameRate。</summary>
    public long DelayMs => FrameRate > 0 ? 1000L / FrameRate : 250L;

    /// <summary>该时间线帧序列（未展开 duration）。</summary>
    public List<EgretMovieClipDataFrame> Frames = new();

    /// <summary>命名标签（Egret labels，可选）。</summary>
    public List<EgretMovieClipLabel> Labels = new();

    /// <summary>帧事件（Egret events，可选）。</summary>
    public List<EgretMovieClipEvent> Events = new();

    /// <summary>未展开的时间线 res 引用序列（每帧一个 res）。</summary>
    public List<string> TimelineResIds
    {
        get
        {
            var list = new List<string>(Frames.Count);
            foreach (var f in Frames) list.Add(f.ResId);
            return list;
        }
    }

    /// <summary>展开 duration 后的播放序列（每个 res 重复 Duration 次），用于严格按帧率还原定格。</summary>
    public List<string> PlayOrder
    {
        get
        {
            var list = new List<string>();
            foreach (var f in Frames)
                for (int i = 0; i < Math.Max(1, f.Duration); i++)
                    list.Add(f.ResId);
            return list;
        }
    }

    /// <summary>首帧相对注册点偏移（MovieClip 绘制起点用）。</summary>
    public (int X, int Y) FirstFrameOffset =>
        Frames.Count > 0 ? (Frames[0].X, Frames[0].Y) : (0, 0);
}

/// <summary>
/// 白鹭 MovieClip 播放对象：把【时间线 mc】与【图集 EgretAtlas】绑在一起。
/// 必须持有图集才能工作：mc 每帧只有 x/y 偏移和 res 名，子图的真实 w/h 在图集里；
/// 只有两者结合才能算出「所有帧共用的统一注册点」，保证脚底/人物不随帧跳动。
/// </summary>
public sealed class EgretMovieClip
{
    /// <summary>本 MovieClip 引入的图集（子图矩形 + 整页贴图）。</summary>
    public EgretAtlas Atlas { get; }

    public EgretMovieClipData Data { get; }

    public string Name => Data.Name;

    /// <summary>每帧间隔(ms)。</summary>
    public long DelayMs => Data.DelayMs;

    /// <summary>展开 duration 后的播放帧序列（res 名）。</summary>
    public List<string> PlayOrder { get; private set; }

    /// <summary>未展开的时间线 res 序列。缓存一份，避免每帧取帧名时重建列表。</summary>
    public List<string> TimelineResIds { get; private set; }

    /// <summary>
    /// res 名 → 该帧在【统一注册点坐标系】里的完整绘制矩形 (x,y,w,h)，逻辑像素、未缩放。
    /// 左下脚底与水平中心在所有帧里严格对齐；控件只需整体乘一次缩放即可绘制。
    /// </summary>
    public Dictionary<string, Rectangle> Layouts { get; private set; }
        = new Dictionary<string, Rectangle>(StringComparer.OrdinalIgnoreCase);

    /// <summary>所有帧的包围盒（已归一化到 0,0 起点），用于控件布局框。</summary>
    public Rectangle Bounds { get; private set; }

    public EgretMovieClip(EgretAtlas atlas, EgretMovieClipData data)
    {
        Atlas = atlas ?? throw new ArgumentNullException(nameof(atlas));
        Data = data ?? throw new ArgumentNullException(nameof(data));
        TimelineResIds = Data.TimelineResIds;
        PlayOrder = Data.PlayOrder;
    }

    /// <summary>
    /// 用图集子图的真实 w/h + 时间线每帧 x/y 重算统一注册点。
    /// 白鹭原始语义：每帧子图画在 (frame.x, frame.y)，尺寸就是子图自己的 w/h。
    /// 实测 carrer1_1 里 frame.y + h 恒为 348（脚底对齐），frame.x + w/2 只在 185~188 间
    /// （原动画自带的轻微呼吸）。因此直接取所有帧的外接框左上角作为统一原点即可，
    /// 不做拉伸、不做逐帧比例换算 —— 那样才会出现「忽左忽右 / 脚在动」。
    /// </summary>
    public void RebuildLayouts(EgretAtlas atlas)
    {
        Layouts.Clear();
        if (Data.Frames.Count == 0) return;

        var sizes = new Dictionary<string, (int w, int h)>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in Data.Frames)
        {
            if (string.IsNullOrEmpty(f.ResId)) continue;
            if (atlas?.Frames != null && atlas.Frames.TryGetValue(f.ResId, out var sprite))
                sizes[f.ResId] = (sprite.SourceRectangle.Width, sprite.SourceRectangle.Height);
            else if (atlas is EgretMovieClipAtlas mca && mca.MovieFactory != null
                     && mca.MovieFactory.ResRects.TryGetValue(f.ResId, out var r))
                sizes[f.ResId] = (r.Width, r.Height);
        }

        // 第一轮：求所有帧的外接框，确定统一注册点原点。
        int minX = int.MaxValue, minY = int.MaxValue, maxR = int.MinValue, maxB = int.MinValue;
        foreach (var f in Data.Frames)
        {
            if (!sizes.TryGetValue(f.ResId, out var wh) || wh.w <= 0 || wh.h <= 0) continue;
            minX = Math.Min(minX, f.X);
            minY = Math.Min(minY, f.Y);
            maxR = Math.Max(maxR, f.X + wh.w);
            maxB = Math.Max(maxB, f.Y + wh.h);
        }
        if (minX == int.MaxValue) return;   // 图集还没绑定贴图，等 Build 后 OnBuilt 再算

        // 第二轮：每帧归一化到统一原点，矩形用子图原始尺寸（不拉伸）。
        foreach (var f in Data.Frames)
        {
            if (!sizes.TryGetValue(f.ResId, out var wh) || wh.w <= 0 || wh.h <= 0) continue;
            Layouts[f.ResId] = new Rectangle(f.X - minX, f.Y - minY, wh.w, wh.h);
        }
        Bounds = new Rectangle(0, 0, maxR - minX, maxB - minY);
    }
}

/// <summary>
/// 白鹭 MovieClip 数据工厂（对应 egret.MovieClipDataFactory）。
/// 输入一个图集 JSON 的 "res"(子图矩形) + "mc"(时间线/标签/事件) 两段，
/// 产出每个时间线一个 EgretMovieClipData。普通图集（无 "mc"）工厂为空。
/// </summary>
public sealed class EgretMovieClipDataFactory
{
    private readonly Dictionary<string, EgretMovieClipData> _movies =
        new(StringComparer.OrdinalIgnoreCase);

    private string _firstName;

    /// <summary>res 子图矩形（仅 Movie 图集有，对应 Egret 的 textureData.frames / SpriteSheet）。</summary>
    public Dictionary<string, Rectangle> ResRects { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, EgretMovieClipData> Movies => _movies;

    public int Count => _movies.Count;

    /// <summary>是否为 Movie 图集（含 mc 时间线）。</summary>
    public bool IsMovie => _movies.Count > 0;

    /// <summary>首条时间线（与旧逻辑一致：PlayOrder 取首个时间线）。</summary>
    public EgretMovieClipData First =>
        _firstName != null && _movies.TryGetValue(_firstName, out var m) ? m : null;

    public EgretMovieClipData GetMovie(string name) =>
        _movies.TryGetValue(name, out var m) ? m : null;

    /// <summary>从图集 JSON 根节点解析。res 与 mc 都可选；两者皆无则工厂为空（普通图集）。</summary>
    public static EgretMovieClipDataFactory Parse(JsonNode root)
    {
        var factory = new EgretMovieClipDataFactory();
        if (root is not JsonObject obj) return factory;

        // res 子图矩形（白鹭 SpriteSheet 段）。
        if (obj["res"] is JsonObject resNode)
        {
            foreach (var kv in resNode)
            {
                if (kv.Value is JsonObject o)
                {
                    int x = JInt(o, "x"), y = JInt(o, "y"), w = JInt(o, "w"), h = JInt(o, "h");
                    factory.ResRects[kv.Key] = new Rectangle(x, y, w, h);
                }
            }
        }

        // mc 时间线（白鹭 MovieClip 段）。
        if (obj["mc"] is JsonObject mcNode)
        {
            foreach (var kv in mcNode)
            {
                var movie = new EgretMovieClipData { Name = kv.Key };
                if (kv.Value is JsonObject m)
                {
                    if (m["frameRate"] is JsonValue fr && fr.TryGetValue<int>(out var fps) && fps > 0)
                        movie.FrameRate = fps;

                    if (m["labels"] is JsonArray labels)
                        foreach (var la in labels)
                            if (la is JsonObject lo)
                            {
                                var lab = new EgretMovieClipLabel
                                {
                                    Name = lo["name"]?.GetValue<string>(),
                                    Frame = JInt(lo, "frame")
                                };
                                if (!string.IsNullOrEmpty(lab.Name)) movie.Labels.Add(lab);
                            }

                    // 帧事件：labels 之外的 events（部分 Movie 用 events:[{name,frame}]）。
                    if (m["events"] is JsonArray events)
                        foreach (var ev in events)
                            if (ev is JsonObject eo)
                            {
                                var e = new EgretMovieClipEvent
                                {
                                    Name = eo["name"]?.GetValue<string>(),
                                    Frame = JInt(eo, "frame")
                                };
                                if (!string.IsNullOrEmpty(e.Name)) movie.Events.Add(e);
                            }
                    // 兼容 events 为 {"1":"eventName"} 的形式。
                    else if (m["events"] is JsonObject eventDict)
                        foreach (var eventPair in eventDict)
                            if (int.TryParse(eventPair.Key, out var frame))
                                movie.Events.Add(new EgretMovieClipEvent
                                {
                                    Name = eventPair.Value?.GetValue<string>(),
                                    Frame = frame
                                });

                    if (m["frames"] is JsonArray frames)
                        foreach (var f in frames)
                            if (f is JsonObject fo)
                            {
                                var df = new EgretMovieClipDataFrame
                                {
                                    ResId = fo["res"]?.GetValue<string>(),
                                    X = JInt(fo, "x"),
                                    Y = JInt(fo, "y"),
                                    Duration = JInt(fo, "duration", 1)
                                };
                                if (df.Duration < 1) df.Duration = 1;
                                if (!string.IsNullOrEmpty(df.ResId)) movie.Frames.Add(df);
                            }
                }
                factory._movies[kv.Key] = movie;
                if (factory._firstName == null) factory._firstName = kv.Key;
            }
        }
        return factory;
    }

    private static int JInt(JsonObject o, string k, int fallback = 0)
        => o[k] is JsonValue v && v.TryGetValue<int>(out var i) ? i : fallback;
}
