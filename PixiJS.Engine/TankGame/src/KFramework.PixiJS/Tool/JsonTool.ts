/**
 * JSON 序列化小工具。
 *
 * C# 版里的 `AppJsonContext`（JsonSerializerContext）是为了让 AOT 能静态生成
 * 序列化代码才存在的；TS 的 JSON 是运行时内建能力，不需要这套注册，已去掉。
 */

/** 存档数据模型（字段名与 C# 版保持一致，方便存档互通） */
export interface GameSaveData {
  Level: number;
  PlayerName: string;
}

export class JsonTool {
  public static fromJson<T>(json: string): T {
    return JSON.parse(json) as T;
  }

  public static toJson(value: unknown): string {
    return JSON.stringify(value);
  }
}
