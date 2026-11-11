using System.Text.Json.Nodes;
using Rectangle = KFramework.MonoGame.Rectangle;

// 白鹭(Egret) MovieClip 解析：与 egret.MovieClipDataFactory / egret.MovieClipData / egret.MovieClip 一一对应。
//
// 白鹭引擎里“普通图集”与“Movie 图集”的类关系：
//   * 普通图集 = SpriteSheet（TextureAtlas）：只有 res/frames 子图矩形 + 整页贴图，无时间线。
//     对应本工程 Mir2AtlasSheet.Frames（res 子图矩形）—— 即 Egret 的 SpriteSheet。
//   * Movie 图集 = 在普通图集 JSON 基础上额外带 "mc" 段：mc 是若干“时间线”，每条时间线是一组
//     按顺序排列的帧；每帧引用一个 res 子图，并带相对注册点的偏移(x,y)与停留时长(duration)。
//     MovieClipDataFactory 把 "mc" 时间线 + "res" 子图矩形绑定到整页贴图，产出 MovieClipData；
//     MovieClip(display) 再消费 MovieClipData 播放。
//
// 本文件把上述解析独立出来（去掉原来散落在 Mir2AtlasSheet.Parse 与 SelectScene.ReadMovieMeta 的内联解析），
// 让“白鹭 Movie”成为可复用的独立单元。

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

/// <summary>白鹭 MovieClip 的一条时间线（对应 egret.MovieClipData）。</summary>
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

    /// <summary>未展开的时间线 res 引用序列（每帧一个 res）。可直接作为 AtlasAnimationFrames。</summary>
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
/// 白鹭 MovieClip 数据工厂（对应 egret.MovieClipDataFactory）。
/// 输入一个图集 JSON 的 "res"(子图矩形) + "mc"(时间线) 两段，
/// 产出每个时间线一个 EgretMovieClipData。普通图集（无 "mc"）工厂为空。
/// 与 Egret 一致：res 子图矩形 = 普通图集的 SpriteSheet，mc 时间线 = MovieClipData，本工厂 = MovieClipDataFactory。
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
                    {
                        foreach (var la in labels)
                        {
                            if (la is JsonObject lo)
                            {
                                var lab = new EgretMovieClipLabel
                                {
                                    Name = lo["name"]?.GetValue<string>(),
                                    Frame = JInt(lo, "frame")
                                };
                                if (!string.IsNullOrEmpty(lab.Name)) movie.Labels.Add(lab);
                            }
                        }
                    }

                    if (m["frames"] is JsonArray frames)
                    {
                        foreach (var f in frames)
                        {
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
                    }
                }
                factory._movies[kv.Key] = movie;
                if (factory._firstName == null) factory._firstName = kv.Key;
            }
        }
        return factory;
    }

    private static int JInt(JsonObject o, string k, int fallback = 0) =>
        o[k] is JsonValue v && v.TryGetValue<int>(out var i) ? i : fallback;
}
