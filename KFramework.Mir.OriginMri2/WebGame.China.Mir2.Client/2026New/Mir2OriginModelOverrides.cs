// 物品 Index -> model/ 站立帧模型号的覆盖表（手工维护，优先级高于自动生成的 Mir2OriginItemModelMap）。
// 值格式同 cm.item[col17]："男号/女号" 或单个通用号。
//
// 立绘实际取的是 model/{版本}/model/{模型号}/{模型号}994.png（动作 99 + 方向 4），
// 正常情况下 col17 就是正确编号，本表不需要填：
//   木剑 1000624 的 "9501/9502" -> 9501/9502 的 994 帧就是木剑
//   传奇战刃 1011210 的 "9655/9656" -> 男女两帧都存在
// 仅当个别物品的 col17 指错、要改指别的站立帧时，在这里加一行，
// 不必改动自动生成的映射表：
//   { 1000624, "9501/9502" },
using System.Collections.Generic;

namespace Client.MirObjects
{
    internal static class Mir2OriginModelOverrides
    {
        public static readonly Dictionary<int, string> Models = new Dictionary<int, string>
        {
            // 留空表示全部沿用 1config.item 的 col17。
        };
    }
}
