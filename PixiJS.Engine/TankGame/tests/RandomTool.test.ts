import { describe, expect, it } from "vitest";
import { RandomTool } from "../src/KFramework.PixiJS/Tool/RandomTool";

const SampleCount = 20000;

describe("RandomTool - randomInt", () => {
  it("取值落在 [min, max)", () => {
    let outOfRange = 0;
    let minSeen = Number.MAX_VALUE;
    let maxSeen = -Number.MAX_VALUE;

    for (let i = 0; i < SampleCount; i++) {
      const value = RandomTool.randomInt(1, 7); // 期望 1..6
      if (value < 1 || value >= 7) outOfRange++;
      minSeen = Math.min(minSeen, value);
      maxSeen = Math.max(maxSeen, value);
    }

    expect(outOfRange).toBe(0);
    expect(minSeen).toBe(1);
    expect(maxSeen).toBe(6);
  });

  it("min === max 时恒定返回 min", () => {
    for (let i = 0; i < 100; i++) {
      expect(RandomTool.randomInt(5, 5)).toBe(5);
    }
  });

  it("支持负数区间", () => {
    let outOfRange = 0;
    for (let i = 0; i < SampleCount; i++) {
      const value = RandomTool.randomInt(-10, -1);
      if (value < -10 || value >= -1) outOfRange++;
    }
    expect(outOfRange).toBe(0);
  });
});

describe("RandomTool - randomFloat", () => {
  it("取值落在 [min, max)", () => {
    let outOfRange = 0;
    for (let i = 0; i < SampleCount; i++) {
      const value = RandomTool.randomFloat(0, 1);
      if (value < 0 || value >= 1) outOfRange++;
    }
    expect(outOfRange).toBe(0);
  });

  it("结果不是整数（浮点插值生效）", () => {
    const value = RandomTool.randomFloat(0, 100);
    expect(Number.isInteger(value)).toBe(false);
  });
});

describe("RandomTool - getIndexByRate 边界", () => {
  it("空数组返回 -1", () => {
    expect(RandomTool.getIndexByRate([])).toBe(-1);
  });

  it("全零权重返回 -1", () => {
    expect(RandomTool.getIndexByRate([0, 0, 0])).toBe(-1);
  });

  it("含负数的权重总和 <= 0 时返回 -1", () => {
    expect(RandomTool.getIndexByRate([-1, 1])).toBe(-1);
  });

  it("只有一项时恒定返回 0", () => {
    for (let i = 0; i < 100; i++) {
      expect(RandomTool.getIndexByRate([5])).toBe(0);
    }
  });

  it("只有一项非零时恒定命中该项", () => {
    for (let i = 0; i < 100; i++) {
      expect(RandomTool.getIndexByRate([0, 10, 0, 0])).toBe(1);
    }
  });

  it("返回的永远是合法下标", () => {
    const rates = [1, 0, 3, 0, 2];
    for (let i = 0; i < SampleCount; i++) {
      const index = RandomTool.getIndexByRate(rates);
      expect(index).toBeGreaterThanOrEqual(0);
      expect(index).toBeLessThan(rates.length);
    }
  });

  it("权重为 0 的项永远不会被抽中", () => {
    const rates = [5, 0, 5];
    const hit = [0, 0, 0];
    for (let i = 0; i < SampleCount; i++) {
      hit[RandomTool.getIndexByRate(rates)]++;
    }
    expect(hit[1]).toBe(0);
  });
});

describe("RandomTool - getIndexByRate 分布", () => {
  it("等权时各下标出现次数大致相同", () => {
    const rates = [1, 1, 1, 1];
    const hit = [0, 0, 0, 0];

    for (let i = 0; i < SampleCount; i++) {
      hit[RandomTool.getIndexByRate(rates)]++;
    }

    const expected = SampleCount / 4;
    for (const count of hit) {
      expect(count).toBeGreaterThan(expected * 0.8);
      expect(count).toBeLessThan(expected * 1.2);
    }
  });

  it("权重比例 1:3 时命中次数大致成 1:3", () => {
    const rates = [1, 3];
    const hit = [0, 0];

    for (let i = 0; i < SampleCount; i++) {
      hit[RandomTool.getIndexByRate(rates)]++;
    }

    expect(hit[1] / hit[0]).toBeGreaterThan(2.6);
    expect(hit[1] / hit[0]).toBeLessThan(3.4);
  });
});
