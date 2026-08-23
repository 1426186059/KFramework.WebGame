// =====================================================================
// shapes.js（薄 API）：只负责给 C# 层暴露 gl.* 名称空间，
// 所有 gl.* 方法都只是转发到 glCore（真正的合批逻辑在 C# 层 WebGL.cs）。
// =====================================================================

import { glCore } from './renderer.js'

export const gl = {
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
        const data = ArrayBuffer.isView(dataArr) ? dataArr : new Float32Array(dataArr)
        glCore.drawShapeBatch(data, instanceCount)
    },

    // ---- 单实例纹理绘制（图片 / 文本），texId 为数值 id（文本烘焙用） ----
    drawImageInstance(dataArr, texId, uvW, uvH) {
        const data = ArrayBuffer.isView(dataArr) ? dataArr : new Float32Array(dataArr)
        glCore.drawImageInstance(data, texId, uvW, uvH)
    },

    // ---- 图片 DrawImage（通过 string id），C# 侧给行主序矩阵 + alpha ----
    drawImageById(id, dx, dy, dw, dh, matrixArr, alpha) {
        glCore.drawImageById(id, dx, dy, dw, dh, matrixArr, alpha)
    },

    // ---- 图片加载 ----
    loadImage(id, url) {
        return glCore.loadImage(id, url)
    },

    // ---- 文本纹理烘焙：返回 { texId, tw, th, ascent, pad } ----
    bakeTextTexture(text, font, color) {
        return glCore.bakeTextTexture(text, font, color)
    },
}
