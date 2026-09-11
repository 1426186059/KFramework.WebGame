// mirengine/core/PrintTool.ts
// JS 端统一日志工具，与 C# 端 PrintTool 对应。
// 输出格式：[TAG] hh/mm/ss, 消息（TAG 默认 "JS"，时间格式 hh/mm/ss）。
// 用法：
//   PrintTool.Write("消息")          -> 默认 TAG = JS
//   PrintTool.Write("TAG", "消息")   -> 指定 TAG
// C# 侧 [JSImport("mir.log")] 也走本工具的 log 导出（与 Write 同实现）。

/**
 * 补零到两位。
 * @param n 待格式化的数字
 */
const pad2 = (n: number | string): string => String(n).padStart(2, '0');

/**
 * 打印一条带时间与 TAG 的日志。
 * @param tagOrMsg 仅一个参数时视为消息内容；两个参数时视为 TAG
 * @param maybeMsg 消息内容
 */
export const Write = (tagOrMsg: string, maybeMsg?: string): void => {
    const tag = maybeMsg === undefined ? '' : tagOrMsg;
    const message = maybeMsg === undefined ? tagOrMsg : maybeMsg;
    const d = new Date();
    const time = `${pad2(d.getHours())}/${pad2(d.getMinutes())}/${pad2(d.getSeconds())}`;
    console.log(`${time} [JS][${tag}], ${message}`);
};

// 供 C# 侧 [JSImport("mir.log")] 绑定（BrowserResource.Log -> 404 等）。
export const log = Write;

export default { Write, log };
