import { describe, expect, it } from "vitest";
import { TimeTool } from "../src/KFramework.PixiJS/Tool/TimeTool";

describe("TimeTool - 时长格式化", () => {
  it("零时长", () => {
    expect(TimeTool.getFormatStringByTimeSpan(0)).toBe("00:00:00");
  });

  it("1 小时 1 分 1 秒", () => {
    const ms = ((1 * 60 + 1) * 60 + 1) * 1000;
    expect(TimeTool.getFormatStringByTimeSpan(ms)).toBe("01:01:01");
  });

  it("不足两位补零", () => {
    expect(TimeTool.getFormatStringByTimeSpan(65 * 1000)).toBe("00:01:05");
  });

  it("超过 24 小时不取模（与 C# 的 hh 不同，这里显示累计小时）", () => {
    expect(TimeTool.getFormatStringByTimeSpan(25 * 3600 * 1000)).toBe(
      "25:00:00",
    );
  });

  it("负数夹到 0", () => {
    expect(TimeTool.getFormatStringByTimeSpan(-5000)).toBe("00:00:00");
  });

  it("毫秒部分被截断而不是四舍五入", () => {
    expect(TimeTool.getFormatStringByTimeSpan(1999)).toBe("00:00:01");
  });
});

describe("TimeTool - 日期格式化与解析", () => {
  it("Date -> yyyy/MM/dd HH:mm:ss", () => {
    const date = new Date(2026, 8, 1, 14, 5, 9);
    expect(TimeTool.getFormatStringByDateTime(date)).toBe(
      "2026/09/01 14:05:09",
    );
  });

  it("格式化 -> 解析 往返一致", () => {
    const date = new Date(2026, 8, 1, 14, 5, 9);
    const text = TimeTool.getFormatStringByDateTime(date);

    expect(TimeTool.getLocalTimeFromDateString(text).getTime()).toBe(
      date.getTime(),
    );
  });

  it("解析按本地时区解释", () => {
    const date = TimeTool.getLocalTimeFromDateString("2026/09/01 14:05:09");

    expect(date.getFullYear()).toBe(2026);
    expect(date.getMonth()).toBe(8);
    expect(date.getDate()).toBe(1);
    expect(date.getHours()).toBe(14);
    expect(date.getMinutes()).toBe(5);
    expect(date.getSeconds()).toBe(9);
  });

  it("格式不对时返回 Invalid Date", () => {
    expect(
      TimeTool.getLocalTimeFromDateString("2026-09-01 14:05:09").getTime(),
    ).toBeNaN();
    expect(TimeTool.getLocalTimeFromDateString("").getTime()).toBeNaN();
  });

  it("数值越界时返回 Invalid Date（JS 的 Date 构造器会溢出进位，这里做了回读校验）", () => {
    expect(
      TimeTool.getLocalTimeFromDateString("2026/13/01 10:00:00").getTime(),
    ).toBeNaN();
    expect(
      TimeTool.getLocalTimeFromDateString("2026/09/32 10:00:00").getTime(),
    ).toBeNaN();
    expect(
      TimeTool.getLocalTimeFromDateString("2026/09/01 25:00:00").getTime(),
    ).toBeNaN();
  });

  it("闰年 2 月 29 日是合法日期", () => {
    const date = TimeTool.getLocalTimeFromDateString("2024/02/29 00:00:00");
    expect(date.getTime()).not.toBeNaN();
    expect(date.getDate()).toBe(29);
  });
});

describe("TimeTool - 时长解析", () => {
  it("带天数：1.02:03:04", () => {
    const expected = (1 * 86400 + 2 * 3600 + 3 * 60 + 4) * 1000;
    expect(TimeTool.getTimeSpanFromDateString("1.02:03:04")).toBe(expected);
  });

  it("不带天数：02:03:04", () => {
    const expected = (2 * 3600 + 3 * 60 + 4) * 1000;
    expect(TimeTool.getTimeSpanFromDateString("02:03:04")).toBe(expected);
  });

  it("带小数秒", () => {
    expect(TimeTool.getTimeSpanFromDateString("00:00:01.5")).toBe(1500);
  });

  it("负号", () => {
    expect(TimeTool.getTimeSpanFromDateString("-00:00:05")).toBe(-5000);
  });

  it("格式化与解析互为逆运算", () => {
    const text = "01:02:03";
    expect(
      TimeTool.getFormatStringByTimeSpan(
        TimeTool.getTimeSpanFromDateString(text),
      ),
    ).toBe(text);
  });
});

describe("TimeTool - 时间戳", () => {
  it("getNowTimeStamp 与 Date.now() 一致（秒级）", () => {
    const now = TimeTool.getNowTimeStamp();
    const expected = Math.floor(Date.now() / 1000);

    expect(Math.abs(now - expected)).toBeLessThanOrEqual(1);
  });

  it("时间戳 -> Date -> 时间戳 往返一致", () => {
    const stamp = 1767225909;

    expect(
      TimeTool.getTimeStampFromLocalTime(
        TimeTool.getUTCTimeFromTimeStamp(stamp),
      ),
    ).toBe(stamp);
  });

  it("本地时间与 UTC 时间得到同一个时间戳", () => {
    const date = new Date(2026, 8, 1, 14, 5, 9);

    expect(TimeTool.getTimeStampFromLocalTime(date)).toBe(
      TimeTool.getTimeStampFromUTCTime(date),
    );
  });

  it("getLocalTimeFromTimeStamp 与 getUTCTimeFromTimeStamp 得到同一时刻", () => {
    const stamp = 1767225909;

    expect(TimeTool.getLocalTimeFromTimeStamp(stamp).getTime()).toBe(
      TimeTool.getUTCTimeFromTimeStamp(stamp).getTime(),
    );
  });

  it("日期字符串 -> 时间戳", () => {
    const text = "2026/09/01 14:05:09";
    const date = TimeTool.getLocalTimeFromDateString(text);

    expect(TimeTool.getTimeStampFromDateString(text)).toBe(
      TimeTool.getTimeStampFromLocalTime(date),
    );
  });
});

describe("TimeTool - 本地 / UTC 转换", () => {
  it("两个方向的转换都保持时刻不变", () => {
    const date = new Date(2026, 8, 1, 14, 5, 9);

    expect(TimeTool.getLocalTimeFromUTCTime(date).getTime()).toBe(
      date.getTime(),
    );
    expect(TimeTool.getUtcTimeFromLocalTime(date).getTime()).toBe(
      date.getTime(),
    );
  });
});
