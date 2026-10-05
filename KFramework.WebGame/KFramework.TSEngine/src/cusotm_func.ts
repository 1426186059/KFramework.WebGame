// 引擎统一硬断言。
//
// 为什么不用 console.assert：本引擎发布 Release 时会剥离所有 console.*（drop_console /
// 把 console 替换成空 stub），console.assert 属于 console.*，会被一并删除 ——
// 断言逻辑（条件为假才报错）随之整体消失。故所有契约检查统一走这里：
// 它只 throw，不依赖 console，Release 下依然生效。
//
// 用法：
//   import { assert } from './cusotm_func.js';
//   assert(cond, '出错信息');

/**
 * 硬断言：条件为假则抛 {@link Error}。
 * @param cond 必须满足的条件
 * @param msg  失败时的错误信息（建议带上下文，便于定位）
 */
export function assert(cond: boolean, msg: string): void {
    if (!cond) throw new Error('[assert] ' + msg);
}
