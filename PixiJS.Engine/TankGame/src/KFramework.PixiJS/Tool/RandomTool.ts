/**
 * 随机数工具（移植自 MonoGame 版 RandomTool）。
 *
 * 与 C# 版的差异：
 * - C# 用 `[ThreadStatic]` 的 Random 实例，JS 是单线程的，直接用 `Math.random()`
 * - C# 的 `RandomInt64` / `RandomUInt64` 没有移植：JS 的 number 是双精度浮点，
 *   安全整数范围只有 ±2^53，装不下 64 位随机整数，真需要请用 BigInt 自行扩展
 * - C# 的 `RandomArrayIndex` / `RandomInt32` / `RandomUInt32` 都归并到 `randomInt`
 * - 取值区间与 C# 的 `Random.Next(x, y)` 一致，都是 `[min, max)`
 * - `getIndexByRate` 修掉了 C# 版的边界问题（原版取不到最后一个区间的尾部权重）
 */
export class RandomTool {
  /** `[min, max)` 的随机整数 */
  public static randomInt(min: number, max: number): number {
    console.assert(
      min <= max,
      `RandomTool.randomInt: min(${min}) > max(${max})`,
    );
    return min + Math.floor(Math.random() * (max - min));
  }

  /** `[min, max)` 的随机浮点数 */
  public static randomFloat(min: number, max: number): number {
    console.assert(
      min <= max,
      `RandomTool.randomFloat: min(${min}) > max(${max})`,
    );
    return min + Math.random() * (max - min);
  }

  /**
   * 按权重随机取下标。
   * @param rates 每项权重，非正数按 0 处理；总权重 <= 0 时返回 -1
   */
  public static getIndexByRate(rates: readonly number[]): number {
    let total = 0;
    for (const rate of rates) {
      total += rate;
    }
    if (total <= 0) return -1;

    let target = RandomTool.randomInt(0, total);
    for (let i = 0; i < rates.length; i++) {
      target -= rates[i];
      if (target < 0) return i;
    }
    return rates.length - 1;
  }
}
