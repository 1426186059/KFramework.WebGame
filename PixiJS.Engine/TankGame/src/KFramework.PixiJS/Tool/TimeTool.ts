/**
 * 时间工具（移植自 MonoGame 版 TimeTool）。
 *
 * 与 C# 版的差异：
 * - C# 的 `DateTime`（时刻）与 `TimeSpan`（时长）在 JS 里统一成
 *   `Date`（时刻）和 `number`（毫秒时长）
 * - C# 用 `TimeZoneInfo` 在本地时区与 UTC 之间来回转换；JS 的 Date 内部存的
 *   本来就是 UTC 毫秒数，本地/UTC 只在读写字段时体现，所以那几个转换退化成原样返回
 * - 时间戳统一为【秒】
 */

const Second = 1000;

/** 补零到两位 */
function pad2(value: number): string {
  return String(value).padStart(2, "0");
}

export class TimeTool {
  // ==================== 时间戳 ====================

  /** 当前时刻的秒级时间戳 */
  public static getNowTimeStamp(): number {
    return TimeTool.getTimeStampFromLocalTime(new Date());
  }

  /** Date -> 秒级时间戳。Date 本身就是 UTC 时刻，本地/UTC 结果一致 */
  public static getTimeStampFromLocalTime(date: Date): number {
    return Math.floor(date.getTime() / Second);
  }

  /** 同 {@link getTimeStampFromLocalTime}，保留 C# 同名 API */
  public static getTimeStampFromUTCTime(date: Date): number {
    return Math.floor(date.getTime() / Second);
  }

  /** 秒级时间戳 -> Date */
  public static getUTCTimeFromTimeStamp(timeStamp: number): Date {
    return new Date(timeStamp * Second);
  }

  /** 同 {@link getUTCTimeFromTimeStamp}，保留 C# 同名 API */
  public static getLocalTimeFromTimeStamp(timeStamp: number): Date {
    return new Date(timeStamp * Second);
  }

  /** `"yyyy/MM/dd HH:mm:ss"` -> 秒级时间戳 */
  public static getTimeStampFromDateString(text: string): number {
    return TimeTool.getTimeStampFromLocalTime(
      TimeTool.getLocalTimeFromDateString(text),
    );
  }

  // ==================== 格式化 ====================

  /** 时长（毫秒）-> `"hh:mm:ss"`，按累计小时数显示，不按 24 取模 */
  public static getFormatStringByTimeSpan(duration: number): string {
    const totalSecond = Math.max(0, Math.floor(duration / Second));
    const hour = Math.floor(totalSecond / 3600);
    const minute = Math.floor((totalSecond % 3600) / 60);
    const second = totalSecond % 60;

    return [hour, minute, second].map(pad2).join(":");
  }

  /** Date -> `"yyyy/MM/dd HH:mm:ss"`（本地时区） */
  public static getFormatStringByDateTime(date: Date): string {
    const ymd = `${date.getFullYear()}/${pad2(date.getMonth() + 1)}/${pad2(date.getDate())}`;
    const hms = `${pad2(date.getHours())}:${pad2(date.getMinutes())}:${pad2(date.getSeconds())}`;
    return `${ymd} ${hms}`;
  }

  // ==================== 解析 ====================

  /**
   * `"d.hh:mm:ss[.fff]"` 或 `"hh:mm:ss[.fff]"` -> 毫秒。
   * 对应 C# 的 `TimeSpan.ParseExact(timeStr, "g", ...)`。
   */
  public static getTimeSpanFromDateString(text: string): number {
    const negative = text.startsWith("-");
    const body = negative ? text.slice(1) : text;

    // 天数的分隔符 "." 一定出现在第一个 ":" 之前；
    // "00:00:01.5" 里的 "." 是小数秒，不能当成天数
    let day = 0;
    let timePart = body;
    const dot = body.indexOf(".");
    const colon = body.indexOf(":");
    if (dot >= 0 && (colon < 0 || dot < colon)) {
      day = Number(body.slice(0, dot)) || 0;
      timePart = body.slice(dot + 1);
    }

    const [hour = 0, minute = 0, second = 0] = timePart
      .split(":")
      .map((part) => Number(part) || 0);

    const totalSecond = day * 86400 + hour * 3600 + minute * 60 + second;
    return (negative ? -1 : 1) * totalSecond * Second;
  }

  /** `"yyyy/MM/dd HH:mm:ss"` -> Date，按本地时区解释 */
  public static getLocalTimeFromDateString(text: string): Date {
    const matched =
      /^(\d{4})\/(\d{2})\/(\d{2})\s+(\d{2}):(\d{2}):(\d{2})$/.exec(text.trim());

    console.assert(
      matched !== null,
      `TimeTool.getLocalTimeFromDateString: 无法解析 "${text}"`,
    );
    if (matched === null) return new Date(NaN);

    const [, year, month, day, hour, minute, second] = matched;
    const y = Number(year);
    const mo = Number(month) - 1;
    const d = Number(day);
    const h = Number(hour);
    const mi = Number(minute);
    const s = Number(second);

    const date = new Date(y, mo, d, h, mi, s);

    // JS 的 Date 构造器会溢出进位（13 月 -> 次年 1 月），这里回读校验，
    // 保证和 C# 的 ParseExact 一样：格式对但数值越界 -> 视为非法时间
    const valid =
      date.getFullYear() === y &&
      date.getMonth() === mo &&
      date.getDate() === d &&
      date.getHours() === h &&
      date.getMinutes() === mi &&
      date.getSeconds() === s;

    return valid ? date : new Date(NaN);
  }

  // ==================== 本地 / UTC ====================
  // JS 的 Date 内部就是 UTC 毫秒数，下面两个只是保留 C# 同名 API 的语义占位。

  public static getLocalTimeFromUTCTime(date: Date): Date {
    return new Date(date.getTime());
  }

  public static getUtcTimeFromLocalTime(date: Date): Date {
    return new Date(date.getTime());
  }
}
