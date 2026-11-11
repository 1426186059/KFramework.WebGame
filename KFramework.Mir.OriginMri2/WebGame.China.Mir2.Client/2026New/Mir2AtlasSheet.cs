using System;
using System.Collections.Generic;
using KFramework.MonoGame;
using KFramework.MonoGameExtend;

// MirEngine 全局 using 也导出了 Vector2；这里用别名消歧，明确使用 KFramework.MonoGame 版本。
using Vector2 = KFramework.MonoGame.Vector2;

/// <summary>
/// 图集门面（旧代码统一使用这个名字）：【不继承】任何图集类，而是把能力组合进来：
///   Atlas      = EgretAtlas       普通图集（静态坐标映射 + 整页贴图）
///   MovieClip  = EgretMovieClip   播放对象，内部持有 EgretMovieClipJson 核心数据
///
/// 因此不再有「MovieClip 专用图集子类」这种中间层；MovieClip 是包含进来的时序部件，
/// 而不是靠继承叠加出来的图集类型。对外成员名保持不变，逐个转发到被包含的 Atlas。
/// </summary>
public sealed class Mir2AtlasSheet
{
    /// <summary>被包含的普通图集。</summary>
    public EgretAtlas Atlas { get; private set; }

    /// <summary>MovieClip 播放对象；普通图集为 null。</summary>
    public EgretMovieClip MovieClip { get; private set; }

    public Mir2AtlasSheet()
    {
        
    }

    // ── 普通图集成员转发 ────────────────────────────────────────────────
    public EgretAtlasJson Json => Atlas.Json;
    public string File => Atlas.File;
    public Texture2D Texture => Atlas.Texture;
    public Dictionary<string, KSpriteInfo> Frames => Atlas.Frames;
    public Dictionary<string, Vector2> Anchors => Atlas.Anchors;
    public int SourceW => Atlas.SourceW;
    public int SourceH => Atlas.SourceH;

    /// <summary>是否含 mc 时间线。</summary>
    public bool IsMovieClip => MovieClip != null;

    /// <summary>绑定整页贴图；之后用真实子图尺寸重算 MovieClip 的每帧绘制矩形。</summary>
    public void Build(Texture2D texture)
    {
        Atlas.Build(texture);
        MovieClip?.RebuildLayouts(Atlas);
    }

    // ── MovieClip 时序层 ────────────────────────────────────────────────
    /// <summary>兼容旧调用：首个时间线未展开的 res 序列。</summary>
    public List<string> PlayOrder => MovieClip?.TimelineResIds;

    /// <summary>实际可播放帧数：取时间线长度（同一子图会被重复引用做定格），不用 Frames.Count。</summary>
    public int PlayableFrameCount => (PlayOrder != null && PlayOrder.Count > 0)
        ? PlayOrder.Count
        : (Frames != null ? Frames.Count : 0);

    /// <summary>MovieClip 按时间线取帧名；普通图集回落到图集的帧名字典序。</summary>
    public bool TryGetFrameName(int frameIndex, out string frameName)
    {
        var order = MovieClip?.TimelineResIds;
        if (order != null && order.Count > 0)
        {
            frameName = order[Math.Abs(frameIndex) % order.Count];
            return true;
        }
        return Atlas.TryGetFrameName(frameIndex, out frameName);
    }

    /// <summary>解析 JSON（不含贴图），之后仍需 Build(整页贴图)。</summary>
    public static Mir2AtlasSheet Parse(string json)
    {
        var sheet = new Mir2AtlasSheet();

        // 先判别类型，分流到普通图集 / MovieClip 图集；其它类型走 else 报警。
        var kind = EgretJsonDetector.Detect(json);
        if (kind == EgretJsonKind.Atlas)
        {
            sheet.Atlas = new EgretAtlas();
            sheet.Atlas.ParseJson(json);
        }
        else if (kind == EgretJsonKind.MovieClip)
        {
            var movieJson = EgretMovieClipJson.TryDeserialize(json);
            if (movieJson == null) return sheet;

            // movieJson 已包含完整图集信息（file + res 子图矩形），直接用它构建 Atlas，
            // 不必再对整份含 mc 的 json 走一遍 EgretAtlasJson。
            sheet.Atlas = new EgretAtlas();
            sheet.Atlas.UseMovieClip(movieJson);

            // 与旧逻辑一致：取 mc 里的首条时间线。
            foreach (var kv in movieJson.Mc)
            {
                if (kv.Value == null) continue;
                sheet.MovieClip = new EgretMovieClip(sheet.Atlas, movieJson, kv.Key, kv.Value);
                break;
            }
            // 贴图还没绑定，先用 res 矩形算一版绘制矩形；Build 后会再校准一次。
            sheet.MovieClip?.RebuildLayouts(sheet.Atlas);
        }
        else
        {
            PrintTool.Assert(false, $"Mir2AtlasSheet.Parse: 非白鹭图集 JSON，kind={kind}，json={json}");
        }
        return sheet;
    }
}
