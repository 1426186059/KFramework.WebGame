// 角色面板「人物立绘」分层渲染规则 —— 对齐《原始传奇》页游。
//
// 立绘用的是 model/ 的【精灵站立帧】，不是 uiModel/ 那套大图：
//   assets/resource/{版本}/model/{模型号}/{sourceId}.{png|json}?ver=1.0.2
//   sourceId = 1000*模型号 + 10*动作 + 方向；站定帧 = 动作 99 + 方向 NONE(4)，末尾即 994。
//   例：裸模 9001 -> .../model/9001/9001994.png（金色裸身人物）
//       木剑 9501 -> .../model/9501/9501994.png（真木剑）
//       传奇战刃 9655/9656、裁决之杖 9547 两性别帧都存在
//   uiModel/ 目录里同编号是另一套图（9501 是黑金大刀），用它会导致「穿木剑却显示别的武器」。
// 同名 .json 的 frames[0] 给出 offX/offY（该帧在 1025×1025 画布里的站位）与 w/h：
//   裸模 9001 offX=559 offY=516 w=81 h=145；木剑 9501 offX=542 offY=525 w=29 h=57。
//   各层按 (offX - 611, offY - 650) * 1.2 对齐容器脚底原点后依次覆盖。
//
// 分层：底层常驻裸身 9001(男)/9002(女) + 头发 9498(男)/9499(女)；
//       上层覆盖 衣服 / 头盔(斗笠优先) / 武器 / 面纱 / 盾牌，没装备就不画。
// 模型号来自 cm.item[itemId][17]（"男号/女号"），按性别取一半。
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Client.MirObjects;

/// <summary>
/// 角色面板人物立绘：算分层模型号、拼 model/ 精灵帧路径、按帧 json 的 offX/offY 对齐各层。
/// 帧元数据按模型异步加载并缓存，绘制路径每帧调用 EnsureFrame 是安全的，不阻塞网络。
/// </summary>
public static class Mir2OriginUIModel
{
    public const float Scale = 1.2f;          // 立绘整体缩放
    public const int AnchorX = 611, AnchorY = 650; // 1025×1025 画布的脚底锚点
    public const int MaleBody = 9001, FemaleBody = 9002;
    public const int MaleHair = 9498, FemaleHair = 9499;

    /// <summary>角色面板用的站定帧：动作 99 + 方向 NONE(4) → sourceId 末尾 994。</summary>
    public const int UIStandAction = 99, UIStandDirection = 4;

    // 页游 UISkinIndex 的层号；Armour / HeadGear 是本工程为「裸模常驻 + 装备叠加」新增的两层。
    public const int Circle = 0, Wing = 1, Body = 2, Head = 3, Weapon = 4, Shield = 5, Miansha = 6, Yuyi = 7;
    public const int Armour = 8, HeadGear = 9;
    public const int LayerCount = 10;

    /// <summary>z 序：最底永远是原始裸模 + 原始头发，装备图片逐层覆盖上去，不替换底层。
    /// 衣服 / 头盔(斗笠) / 武器 / 面纱 / 护盾各自独立一层，没装备时该层不画。</summary>
    public static readonly int[] DrawOrder =
    {
        Body,       // 原始裸模（常驻）
        Head,       // 原始头发（常驻）
        Armour,     // 衣服覆盖层
        HeadGear,   // 头盔 / 斗笠覆盖层
        Weapon,     // 武器覆盖层
        Miansha,    // 面纱覆盖层
        Shield,     // 护盾覆盖层
    };

    /// <summary>立绘层的站定精灵帧 png（版本目录由 resourceVersion.json 决定）。</summary>
    public static string Path(int model) =>
        Mir2OriginObjectLibrary.ModelFramePath(model, UIStandAction, UIStandDirection, "png");

    private static string FrameJsonPath(int model) =>
        Mir2OriginObjectLibrary.ModelFramePath(model, UIStandAction, UIStandDirection, "json");

    // —— 帧站位缓存：model -> (offX, offY) ——
    private static readonly object Sync = new object();
    private static readonly Dictionary<int, (int X, int Y)> FrameMap = new Dictionary<int, (int X, int Y)>();
    private static readonly HashSet<int> _inFlight = new HashSet<int>();
    private static readonly Dictionary<int, long> _retryAt = new Dictionary<int, long>();
    private static int _posVersion;   // 帧元数据就绪后 +1，UI 据此重算一次坐标

    public static int PosVersion => Volatile.Read(ref _posVersion);

    /// <summary>模型版本表是否就绪；未就绪时 ModelFramePath 会缺版本段，不宜设置 ImageUrl。</summary>
    public static bool PathsReady => Mir2OriginObjectLibrary.IsReady;

    /// <summary>确保某模型的帧站位已加载（幂等；失败 5 秒冷却，可由 UI 每帧安全调用）。</summary>
    public static void EnsureFrame(int model)
    {
        if (model <= 0) return;
        long now = Environment.TickCount64;
        lock (Sync)
        {
            if (FrameMap.ContainsKey(model) || _inFlight.Contains(model)) return;
            if (_retryAt.TryGetValue(model, out long t) && now < t) return;
            _inFlight.Add(model);
        }
        _ = LoadFrameAsync(model);
    }

    /// <summary>对整组分层模型批量触发帧元数据加载。</summary>
    public static void EnsureFrames(int[] models)
    {
        if (models == null) return;
        foreach (int model in models)
            if (model > 0) EnsureFrame(model);
    }

    private static async Task LoadFrameAsync(int model)
    {
        bool ok = false;
        try
        {
            // 常驻层的预热可能早于版本表加载，先等它就绪，否则会拼出无版本段的 404 路径。
            if (!Mir2OriginObjectLibrary.IsReady)
                await Mir2OriginObjectLibrary.InitializeAsync(CancellationToken.None);

            // JsonDocument 是读取器 API，不走反射式序列化（浏览器 WASM/AOT 安全）。
            byte[] bytes = await Mir2OriginResource.GetBytesAsync(FrameJsonPath(model), CancellationToken.None);
            using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(bytes));
            if (doc.RootElement.TryGetProperty("frames", out JsonElement frames) && frames.GetArrayLength() > 0)
            {
                JsonElement f = frames[0];
                if (f.TryGetProperty("offX", out JsonElement xe) && f.TryGetProperty("offY", out JsonElement ye))
                {
                    int x = xe.GetInt32(), y = ye.GetInt32();
                    lock (Sync) FrameMap[model] = (x, y);
                    ok = true;
                }
            }
        }
        catch (Exception)
        {
            // CDN 失联/JSON 异常：保持无站位，UI 走「图片居中」回落，不阻断面板绘制。
        }
        finally
        {
            lock (Sync)
            {
                _inFlight.Remove(model);
                if (ok) _retryAt.Remove(model);
                else _retryAt[model] = Environment.TickCount64 + 5000;
            }
            if (ok) Interlocked.Increment(ref _posVersion);
        }
    }

    /// <summary>取该模型相对「容器脚底原点」的左上角偏移（已含 Scale）。无站位数据返回 false。</summary>
    public static bool TryGetOffset(int model, out int x, out int y)
    {
        lock (Sync)
        {
            if (FrameMap.TryGetValue(model, out var p))
            {
                x = (int)Math.Round((p.X - AnchorX) * Scale);
                y = (int)Math.Round((p.Y - AnchorY) * Scale);
                return true;
            }
        }
        x = 0; y = 0;
        return false;
    }

    /// <summary>该模型的站立帧元数据是否已就绪。</summary>
    public static bool HasModelPos(int model)
    {
        if (model <= 0) return false;
        lock (Sync) return FrameMap.ContainsKey(model);
    }

    /// <summary>
    /// 复刻 UIModelUtil.setFirstPlayerModel / showPlayerModel：由装备槽的页游物品 id 解算各层 uiModel 模型号。
    /// itemId 均为 cm.item 的键（本工程 ItemInfo.Index）；0 表示该槽为空。
    /// 未装备的装饰层返回 0（不绘制）。
    /// </summary>
    public static int[] ResolvePlayerModels(bool female, int armour, int weapon, int helmet,
                                            int douli, int shield, int miansha)
    {
        int[] models = new int[LayerCount];
        int nakedBody = female ? FemaleBody : MaleBody;
        int hair = female ? FemaleHair : MaleHair;

        // 最底两层永远在：原始裸模 + 原始头发。装备只是叠在它们上面的独立图片，绝不替换底层。
        models[Body] = nakedBody;
        models[Head] = hair;

        if (armour > 0) models[Armour] = Mir2OriginModelResolver.GetAvailableModel(armour, female, 0);
        if (weapon > 0) models[Weapon] = Mir2OriginModelResolver.GetAvailableModel(weapon, female, 0);
        if (miansha > 0) models[Miansha] = Mir2OriginModelResolver.GetAvailableModel(miansha, female, 0);
        if (shield > 0) models[Shield] = Mir2OriginModelResolver.GetAvailableModel(shield, female, 0);

        // 头部覆盖层：斗笠优先、其次头盔；都没有就不画，头发保持可见。
        int headGear = douli > 0 ? Mir2OriginModelResolver.GetAvailableModel(douli, female, 0) : 0;
        if (headGear <= 0 && helmet > 0) headGear = Mir2OriginModelResolver.GetAvailableModel(helmet, female, 0);
        if (headGear > 0) models[HeadGear] = headGear;

        return models;
    }
}
