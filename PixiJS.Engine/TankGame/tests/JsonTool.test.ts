import { describe, expect, it } from "vitest";
import { JsonTool, GameSaveData } from "../src/KFramework.PixiJS/Tool/JsonTool";

describe("JsonTool - toJson", () => {
  it("序列化对象", () => {
    expect(JsonTool.toJson({ Level: 3, PlayerName: "坦克" })).toBe(
      '{"Level":3,"PlayerName":"坦克"}',
    );
  });

  it("序列化数组", () => {
    expect(JsonTool.toJson([1, 2, 3])).toBe("[1,2,3]");
  });

  it("序列化 null / 布尔 / 数字", () => {
    expect(JsonTool.toJson(null)).toBe("null");
    expect(JsonTool.toJson(true)).toBe("true");
    expect(JsonTool.toJson(12.5)).toBe("12.5");
  });
});

describe("JsonTool - fromJson", () => {
  it("反序列化为带类型的对象", () => {
    const save = JsonTool.fromJson<GameSaveData>(
      '{"Level":3,"PlayerName":"坦克"}',
    );

    expect(save.Level).toBe(3);
    expect(save.PlayerName).toBe("坦克");
  });

  it("反序列化数组", () => {
    expect(JsonTool.fromJson<number[]>("[1,2,3]")).toEqual([1, 2, 3]);
  });

  it("反序列化嵌套结构", () => {
    const data = JsonTool.fromJson<{ list: { id: number }[] }>(
      '{"list":[{"id":1},{"id":2}]}',
    );

    expect(data.list).toHaveLength(2);
    expect(data.list[1].id).toBe(2);
  });

  it("非法 JSON 抛错", () => {
    expect(() => JsonTool.fromJson("{ 不是 JSON")).toThrow();
    expect(() => JsonTool.fromJson("")).toThrow();
  });
});

describe("JsonTool - 往返", () => {
  it("对象 -> 字符串 -> 对象 保持一致", () => {
    const save: GameSaveData = { Level: 7, PlayerName: "坦克大战" };

    expect(JsonTool.fromJson<GameSaveData>(JsonTool.toJson(save))).toEqual(
      save,
    );
  });

  it("数组往返保持一致", () => {
    const rates = [1, 2, 3, 5, 8];

    expect(JsonTool.fromJson<number[]>(JsonTool.toJson(rates))).toEqual(rates);
  });

  it("配合 ContentEncryption 存读档也是通的", () => {
    const save: GameSaveData = { Level: 3, PlayerName: "坦克" };
    const json = JsonTool.toJson(save);
    const back = JsonTool.fromJson<GameSaveData>(json);

    expect(back).toEqual(save);
  });
});
