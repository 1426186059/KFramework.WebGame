// =====================================================================
// shapes.js（薄 API）：只负责给 C# 层暴露 gl.* 名称空间，
// 所有 gl.* 方法都只是转发到 glCore（真正的合批逻辑在 C# 层 WebGL.cs）。
// =====================================================================

import { glCore } from './renderer.js'

/**
 * 把任意数值数组严格转换成 Float32Array 后再交给 WebGL 使用。
 * 背景：
 *   1) .NET 10 JSInterop 对 double[] 传参，在 JS 侧收到的是 Float64Array
 *      （ArrayBuffer.isView(Float64Array)=true）。若直接交给
 *      gl.bufferSubData，会把 64 位原始字节按 32 位 float 上传，
 *      导致矩形位置/颜色/矩阵全部错乱（表现：什么都看不见）。
 *   2) 对 JS Array / Float32Array / Float64Array / Int32Array 等都统一
 *      做一次「数值语义」转换，保证 WebGL 真正收到的是 float32。
 */
function _toFloat32(arr) {
    if (arr instanceof Float32Array) return arr
    if (arr == null || arr.length === undefined) {
        console.warn('[shapes._toFloat32] 非法数组:', arr)
        return new Float32Array(0)
    }
    return new Float32Array(arr)
}

export const gl = {
    // ---- 着色器预加载（main.js 启动时调用，与 dotnet 并行） ----
    preloadShaders() {
        return glCore.preloadShaders()
    },

    // ---- 初始化 ----
    init(selector, width, height) {
        glCore.init(selector, width, height)
    },

    // ---- 清屏（直接传 rgba float，C# 侧完成 hex→float） ----
    clear(r, g, b, a) {
        glCore.clear(r, g, b, a)
    },

    // ---- 形状批量绘制（C# 侧组装好 instData，紧凑布局每实例 FLOATS_PER_INST） ----
    drawShapeBatch(dataArr, instanceCount) {
        const data = _toFloat32(dataArr)
        glCore.drawShapeBatch(data, instanceCount)
    },

    // ---- 单实例纹理绘制（图片 / 文本），texId 为数值 id（文本烘焙用） ----
    drawImageInstance(dataArr, texId, uvW, uvH) {
        const data = _toFloat32(dataArr)
        glCore.drawImageInstance(data, texId, uvW, uvH)
    },

    // ---- 图片 DrawImage（通过 string id），C# 侧给行主序矩阵 + alpha ----
    drawImageById(id, dx, dy, dw, dh, matrixArr, alpha) {
        glCore.drawImageById(id, dx, dy, dw, dh, _toFloat32(matrixArr), alpha)
    },

    // ---- 图片加载 ----
    loadImage(id, url) {
        return glCore.loadImage(id, url)
    },

    // ---- 动态纹理（Texture2D：像素重传） ----
    uploadTexture(id, w, h, argb) {
        glCore.uploadTexture(id, w, h, argb)
    },
    disposeTexture(id) {
        glCore.disposeTexture(id)
    },

    // ---- 文本纹理烘焙：返回 { texId, tw, th, ascent, pad } ----
    bakeTextTexture(text, font, color) {
        return glCore.bakeTextTexture(text, font, color)
    },
}
