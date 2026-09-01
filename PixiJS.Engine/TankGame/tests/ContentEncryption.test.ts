import { describe, expect, it } from "vitest";
import { ContentEncryption } from "../src/KFramework.PixiJS/Tool/ContentEncryption";

describe("ContentEncryption - 混淆效果", () => {
  it("编码结果里看不到明文", () => {
    const encoded = ContentEncryption.encode("Level=3;PlayerName=坦克");

    expect(encoded).not.toContain("Level");
    expect(encoded).not.toContain("坦克");
  });

  it("输出是合法 Base64", () => {
    const encoded = ContentEncryption.encode("任意内容 abc 123");

    expect(encoded).toMatch(/^[A-Za-z0-9+/]*={0,2}$/);
    expect(encoded.length % 4).toBe(0);
  });

  it("同样的明文每次编码结果一致（无随机 salt）", () => {
    expect(ContentEncryption.encode("abc")).toBe(
      ContentEncryption.encode("abc"),
    );
  });
});

describe("ContentEncryption - 往返一致", () => {
  it.each([
    ["空串", ""],
    ["纯英文", "Hello World"],
    ["纯中文", "关卡完成"],
    ["中英混合", 'Level=3;名字="坦克大战"'],
    ["JSON", '{"Level":3,"PlayerName":"坦克"}'],
    ["JSON 中文转义一致", '{"a":"\\u4e2d\\u6587"}'],
    ["换行与制表符", "line1\nline2\tend"],
    ["Emoji", "🎮🚀"],
    ["长文本", "A".repeat(1000) + "中文".repeat(500)],
  ])("decode(encode(%s)) === 原文", (_name, plain) => {
    expect(ContentEncryption.decode(ContentEncryption.encode(plain))).toBe(
      plain,
    );
  });
});

describe("ContentEncryption - 与 C# 版结果一致", () => {
  // 这几个期望值是按 C# 版算法（固定 16 字节密钥逐字节 XOR，再 Base64）手算出来的，
  // 用来锁死跨语言行为一致：改了密钥或算法顺序，这里会立刻失败。
  it.each([
    ["", ""],
    ["H", "dA=="],
    ["Hello", "dB/9OY0="],
    ["A", "fQ=="],
  ])('encode("%s") === "%s"', (plain, expected) => {
    expect(ContentEncryption.encode(plain)).toBe(expected);
  });
});

describe("ContentEncryption - 边界输入", () => {
  it("全是 0 的字节不会被吞掉", () => {
    // "\u0000" 编码后仍要能还原，不能因为中间出现 0 就截断
    const plain = "a\u0000b\u0000\u0000c";

    expect(ContentEncryption.decode(ContentEncryption.encode(plain))).toBe(
      plain,
    );
  });

  it("非法 Base64 会抛错", () => {
    expect(() => ContentEncryption.decode("!!!not base64!!!")).toThrow();
  });
});
