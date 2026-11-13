// 角色面板「人物立绘」分层渲染规则 —— 对齐《原始传奇》页游（main10.25487.1.min.js）。
//
// 逆向结论（js/ 参考包）：
//   UISkinIndex: Circle0 / Wing1 / Body2 / Head3 / Weapon4 / Shield5 / Miansha6 / Yuyi7
//   UIModelUtil.setFirstPlayerModel（自己的角色面板）：
//     Body   = 衣服槽 getEquipId(sex, itemId, 0, fashionId)；无衣服回落裸身 9001(男)/9002(女)
//     Weapon = 武器槽 getEquipId(sex, itemId, 1, fashionWeapon)；无武器返回 0（不画）
//     Head   = 斗笠(401) > 头盔(4) > 头发 9498(男)/9499(女)   ← 头部只有一层，戴帽即替换头发
//     Shield = 盾牌(402)；Miansha = 面纱(406)
//     getModel / getEquipId 都取 cm.item[itemId][17]（"男号/女号"），按性别取一半。
//   UIModelSkin.load: ResUrl.url(model, 16) = assets/resource/uiModel/{model}.png
//   cm.posJson = ResUrl.url("pos", 17) = assets/resource/uiModel/pos.json，
//     形如 {"9001":{"width":1025,"height":1025,"x":560,"y":515}, ...}：
//     每张 uiModel 单图尺寸不同，需按 x/y 摆进统一 1025×1025 画布，
//     整体缩放 1.2 后以 (611,650)（脚底中心）对齐 UIStaticModel 容器原点。
//     故各层左上角相对容器原点 = (x - 611, y - 650) * 1.2。
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Client.MirObjects;

/// <summary>
/// 页游 UIStaticModel / UIModelUtil 的 .NET 对应：算分层模型号、拼 uiModel 图片路径、
/// 按 uiModel/pos.json 的站位点对齐各层。所有方法都不阻塞网络（pos.json 异步加载，未就绪时居中回落）。
/// </summary>
public static class Mir2OriginUIModel
{
    public const string Dir = "assets/resource/uiModel/";
    public const float Scale = 1.2f;          // UIModelSkin.loadedComplete 的 bitmap.scaleX/scaleY
    public const int AnchorX = 611, AnchorY = 650; // 画布脚底锚点（c=611,d=650，先乘 r 再取负）
    public const int MaleBody = 9001, FemaleBody = 9002;
    public const int MaleHair = 9498, FemaleHair = 9499;

    // 页游 UISkinIndex 的层号。
    public const int Circle = 0, Wing = 1, Body = 2, Head = 3, Weapon = 4, Shield = 5, Miansha = 6, Yuyi = 7;
    public const int LayerCount = 8;

    /// <summary>实际 z 序：身体 → 头(头发/头盔/斗笠) → 武器 → 面纱 → 盾牌。
    /// 页游 sortIndex() 只把 0..3 排到最底，其余按 addSkin 先后叠加，与此处顺序一致。</summary>
    public static readonly int[] DrawOrder = { Body, Head, Weapon, Miansha, Shield };

    /// <summary>uiModel 单图路径（与 ResUrl.url(model,16) 一致）。</summary>
    public static string Path(int model) => $"{Dir}{model}.png?ver={Mir2OriginResource.ResourceVersion}";
    private static string PosPath => $"{Dir}pos.json?ver={Mir2OriginResource.ResourceVersion}";

    // —— uiModel/pos.json 站位点缓存 ——
    private static readonly object Sync = new object();
    private static readonly Dictionary<int, (int X, int Y)> PosMap = new Dictionary<int, (int X, int Y)>();
    private static int _state;        // 0=未加载 1=加载中 2=成功 3=失败（冷却后可重试）
    private static int _posVersion;   // 加载结束 +1，UI 据此重算一次坐标
    private static long _nextRetryTick;

    public static int PosVersion => Volatile.Read(ref _posVersion);

    /// <summary>触发 pos.json 异步加载（幂等；失败后 5 秒内不重试，可由 UI 每帧安全调用）。</summary>
    public static void EnsurePos()
    {
        int state = Volatile.Read(ref _state);
        if (state == 1 || state == 2) return;
        if (state == 3 && Environment.TickCount64 < Interlocked.Read(ref _nextRetryTick)) return;
        lock (Sync)
        {
            if (_state == 1 || _state == 2) return;
            if (_state == 3 && Environment.TickCount64 < _nextRetryTick) return;
            _state = 1;
        }
        _ = LoadPosAsync();
    }

    private static async Task LoadPosAsync()
    {
        bool ok = false;
        try
        {
            // JsonDocument 是读取器 API，不走反射式序列化（浏览器 WASM/AOT 安全）。
            byte[] bytes = await Mir2OriginResource.GetBytesAsync(PosPath, CancellationToken.None);
            using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(bytes));
            foreach (JsonProperty prop in doc.RootElement.EnumerateObject())
            {
                if (!int.TryParse(prop.Name, out int model)) continue;
                JsonElement v = prop.Value;
                if (v.ValueKind != JsonValueKind.Object) continue;
                if (!v.TryGetProperty("x", out JsonElement xe) || !v.TryGetProperty("y", out JsonElement ye)) continue;
                if (!xe.TryGetInt32(out int x) || !ye.TryGetInt32(out int y)) continue;
                lock (Sync) PosMap[model] = (x, y);
            }
            ok = true;
        }
        catch (Exception)
        {
            // CDN 失联/JSON 异常：保持空表，UI 走「图片居中」回落，不阻断面板绘制。
        }
        finally
        {
            if (ok)
            {
                Volatile.Write(ref _state, 2);
            }
            else
            {
                Interlocked.Exchange(ref _nextRetryTick, Environment.TickCount64 + 5000);
                Volatile.Write(ref _state, 3);
            }
            Interlocked.Increment(ref _posVersion);
        }
    }

    /// <summary>取该模型相对「容器脚底原点」的左上角偏移（已含 Scale）。无站位点返回 false。</summary>
    public static bool TryGetOffset(int model, out int x, out int y)
    {
        lock (Sync)
        {
            if (PosMap.TryGetValue(model, out var p))
            {
                x = (int)Math.Round((p.X - AnchorX) * Scale);
                y = (int)Math.Round((p.Y - AnchorY) * Scale);
                return true;
            }
        }
        x = 0; y = 0;
        return false;
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

        // Body：穿衣用 cm.item[衣服][17]；无衣服/查不到模型 → 裸身。
        int body = armour > 0 ? Mir2OriginModelResolver.GetModel(armour, female, nakedBody) : nakedBody;
        models[Body] = body > 0 ? body : nakedBody;

        // Weapon：无武器不画（页游 getEquipId 的 type=1 分支返回 0）。
        if (weapon > 0) models[Weapon] = Mir2OriginModelResolver.GetModel(weapon, female, 0);

        // Head：斗笠优先、其次头盔，都没有才画头发（头部是单层，戴帽即替换头发）。
        int head = douli > 0 ? Mir2OriginModelResolver.GetModel(douli, female, 0) : 0;
        if (head <= 0 && helmet > 0) head = Mir2OriginModelResolver.GetModel(helmet, female, 0);
        models[Head] = head > 0 ? head : hair;

        if (shield > 0) models[Shield] = Mir2OriginModelResolver.GetModel(shield, female, 0);
        if (miansha > 0) models[Miansha] = Mir2OriginModelResolver.GetModel(miansha, female, 0);

        return models;
    }
}
