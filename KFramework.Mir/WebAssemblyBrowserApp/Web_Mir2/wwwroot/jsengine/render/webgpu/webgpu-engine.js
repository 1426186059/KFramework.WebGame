// mirengine/render/webgpu/webgpu-engine.ts
// WebGPU 渲染后端（占位 / 待实现）。
//
// 架构上对应 canvas/ 的拆分：当接入 WebGPU 时，应拆为：
//   - webgpu2d.ts   ：mir.createImage / drawImage / drawBatch / fillRect / drawText / measureText / clear / setStatus
//                     对应 C# 端 MirClient/MirCanvas.cs；
//   - webgpu-engine.ts：mir.cr* 控件树引擎
//                     对应 C# 端 JSBind/BrowserCanvas.cs。
//
// 本文件先以占位形式导出完整渲染 API；待实现时再拆分，并接入 main.ts 的 mir 聚合。
// 当前 main.ts 仅聚合 canvas/ 后端，本文件尚未被加载。
// 各函数的参数与同名的 WebGL 实现完全一致，见 render/webgl/webgl2d.ts 与 render/webgl/webgl-engine.ts。
/** 占位实现：一律抛出，提示后端未接入。 */
const notImpl = (name) => { throw new Error(`WebGPU 渲染后端未实现: mir.${name}`); };
/** 占位（参数见 webgl2d.ts 的 createImage）。 */
export const createImage = (...a) => notImpl('createImage');
/** 占位（参数见 webgl2d.ts 的 disposeImage）。 */
export const disposeImage = (...a) => notImpl('disposeImage');
/** 占位（参数见 webgl2d.ts 的 drawImage）。 */
export const drawImage = (...a) => notImpl('drawImage');
/** 占位（参数见 webgl2d.ts 的 drawBatch）。 */
export const drawBatch = (...a) => notImpl('drawBatch');
/** 占位（参数见 webgl2d.ts 的 fillRect）。 */
export const fillRect = (...a) => notImpl('fillRect');
/** 占位（参数见 webgl2d.ts 的 drawText）。 */
export const drawText = (...a) => notImpl('drawText');
/** 占位（参数见 webgl2d.ts 的 measureText）。 */
export const measureText = (...a) => notImpl('measureText');
/** 占位（参数见 webgl2d.ts 的 clear）。 */
export const clear = (...a) => notImpl('clear');
/** 占位（参数见 webgl2d.ts 的 setStatus）。 */
export const setStatus = (...a) => notImpl('setStatus');
/** 占位（参数见 webgl-engine.ts 的 crCreateOffscreen）。 */
export const crCreateOffscreen = (...a) => notImpl('crCreateOffscreen');
/** 占位（参数见 webgl-engine.ts 的 crSetTarget）。 */
export const crSetTarget = (...a) => notImpl('crSetTarget');
/** 占位（参数见 webgl-engine.ts 的 crClear）。 */
export const crClear = (...a) => notImpl('crClear');
/** 占位（参数见 webgl-engine.ts 的 crDraw）。 */
export const crDraw = (...a) => notImpl('crDraw');
/** 占位（参数见 webgl-engine.ts 的 crMeasureText）。 */
export const crMeasureText = (...a) => notImpl('crMeasureText');
/** 占位（参数见 webgl-engine.ts 的 crFillRect）。 */
export const crFillRect = (...a) => notImpl('crFillRect');
/** 占位（参数见 webgl-engine.ts 的 crDrawLine）。 */
export const crDrawLine = (...a) => notImpl('crDrawLine');
/** 占位（参数见 webgl-engine.ts 的 crFlush）。 */
export const crFlush = (...a) => notImpl('crFlush');
