// partial 文件：原始传奇传送员（net.DeliverModel.ins().send1(deliverId)）的服务端实现。
using Server.ExcelConfig;
using Server.MirEnvir;
using System.Drawing;
using System.Text.Json;

namespace Server.MirObjects
{
    public partial class PlayerObject
    {
        private const int FlyingBootsIndex = 8015; // 小飞鞋：原始传奇 3config.deliver.cost="8015#1" 的传送消耗品

        /// <summary>
        /// 按原始传奇 3config.deliver 表的 deliverId 传送玩家。
        /// 配置已由 Envir.LoadOriginMir2Configs 全量加载在 OriginConfigs[3].ExtraTables["deliver"]，
        /// 不另建配置表。配置缺失/地图不存在一律抛异常，不静默失败。
        /// toMapId 缺失时按原始传奇 deliverToFindNpc 的 toNpcId 分支，传送到目标 NPC 的第一个摆放点。
        /// </summary>
        public void DeliverTo(int deliverId)
        {
            if (!Envir.OriginConfigs.TryGetValue(3, out OriginConfigRoot cfg3) || cfg3 == null)
            {
                throw new InvalidOperationException($"[传送员] 3config 未加载，无法处理 deliverId={deliverId}");
            }

            if (!cfg3.ExtraTables.TryGetValue("deliver", out JsonElement deliverTable))
            {
                throw new InvalidOperationException($"[传送员] 3config.deliver 表不存在，无法处理 deliverId={deliverId}");
            }

            if (!deliverTable.TryGetProperty(deliverId.ToString(), out JsonElement row) || row.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException($"[传送员] deliverId={deliverId} 在 3config.deliver 中不存在");
            }

            string name = row.TryGetProperty("showName", out JsonElement nameElement)
                ? nameElement.GetString() ?? string.Empty
                : string.Empty;

            int toMapId, x, y;
            if (row.TryGetProperty("toMapId", out JsonElement mapElement))
            {
                toMapId = mapElement.GetInt32();
                x = row.TryGetProperty("x", out JsonElement xe) ? xe.GetInt32() : 0;
                y = row.TryGetProperty("y", out JsonElement ye) ? ye.GetInt32() : 0;
            }
            else if (row.TryGetProperty("toNpcId", out JsonElement npcElement))
            {
                int toNpcId = npcElement.GetInt32();
                if (!Envir.MapNpcPlacements.TryGetValue(toNpcId, out List<NPCPlacement> placements) || placements.Count == 0)
                {
                    throw new InvalidOperationException($"[传送员] deliverId={deliverId}({name}) 的 toNpcId={toNpcId} 没有摆放点");
                }

                NPCPlacement placement = placements[0];
                toMapId = placement.MapId;
                x = placement.X;
                y = placement.Y;
            }
            else
            {
                throw new InvalidOperationException($"[传送员] deliverId={deliverId}({name}) 既没有 toMapId 也没有 toNpcId");
            }

            Map map = Envir.GetMap(toMapId);
            if (map == null)
            {
                throw new InvalidOperationException($"[传送员] deliverId={deliverId}({name}) 指向地图 {toMapId} 不存在");
            }

            Point location = new Point(x, y);
            if (!map.ValidPoint(location))
            {
                throw new InvalidOperationException($"[传送员] deliverId={deliverId}({name}) 目标坐标 {x},{y} 在地图 {toMapId} 无效");
            }

            MessageQueue.Enqueue($"[传送员诊断] deliverId={deliverId} name={name} map={toMapId} pos={x},{y}");

            // 死亡是唯一需要拦下的状态，且必须给玩家可见反馈；不再用 ActionTime 静默 return。
            if (Dead)
            {
                ReceiveChat("死亡状态无法传送。", ChatType.System);
                return;
            }

            // 与原始传奇保持一致：3config.deliver.cost="8015#1"，传送统一只消耗 1 个小飞鞋(ItemIndex 8015)。
            // 其它道具（玛雅卷轴 8245 等）一律不扣，等级/转生条件也只展示不拦截。
            UserItem boots = null;
            for (int i = 0; i < Info.Inventory.Length; i++)
            {
                UserItem item = Info.Inventory[i];
                if (item != null && item.Info != null && item.Info.Index == FlyingBootsIndex)
                {
                    boots = item;
                    break;
                }
            }
            if (boots == null)
            {
                ReceiveChat("没有小飞鞋，无法传送。", ChatType.System);
                return;
            }
            ConsumeItem(boots, 1);

            Teleport(map, location);
            ReceiveChat($"已传送至 {name}", ChatType.System);
        }
    }
}
