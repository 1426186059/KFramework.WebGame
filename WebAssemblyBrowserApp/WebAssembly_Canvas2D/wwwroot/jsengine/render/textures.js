// =====================================================================
// 纹理层：图片 + 动态纹理（Texture2D 像素上传）+ 统一绘制入口。
//
// 「Web 怎么加载 GPU 纹理图」在 Canvas2D 端没有 GPU 概念——2D context
// 由浏览器内部管理位图上传。这里保持与 WebGL/WebGPU 一致的统一桥：
//   loadImage(url)      → { id, w, h }，id=url（图片缓存去重）
//   uploadTexture(...)  → ARGB 像素 → 离屏 canvas（'dyn:<id>' 命名空间）
//   draw(id, ...)       → 图片 → 视频 → 动态纹理
// =====================================================================

import { canvas2d } from './renderer.js'
import { videoTex } from './video.js'

const _images = {}
const _texCanvases = new Map()  // 'dyn:<id>' -> { canvas, ctx, w, h }

// ARGB8888 int[]（C# int[] → JS Int32Array）→ RGBA Uint8ClampedArray（canvas 像素顺序）
function argbToRgba(argb) {
    const n = argb.length
    const rgba = new Uint8ClampedArray(n * 4)
    for (let i = 0; i < n; i++) {
        const c = argb[i]
        rgba[i * 4] = (c >> 16) & 0xff
        rgba[i * 4 + 1] = (c >> 8) & 0xff
        rgba[i * 4 + 2] = c & 0xff
        rgba[i * 4 + 3] = (c >>> 24) & 0xff
    }
    return rgba
}

export const textures = {
    loadImage(url) {
        return new Promise((resolve) => {
            const img = new Image()
            img.onload = () => { _images[url] = img; resolve({ id: url, w: img.width, h: img.height }) }
            img.onerror = () => resolve({ id: -1, w: 0, h: 0 })
            img.src = url
        })
    },

    // Texture2D：把 ARGB 像素重传到离屏 canvas（按 id 缓存，尺寸变化时重建）
    uploadTexture(id, w, h, argb) {
        const key = 'dyn:' + id
        let entry = _texCanvases.get(key)
        if (!entry || entry.w !== w || entry.h !== h) {
            const canvas = document.createElement('canvas')
            canvas.width = w; canvas.height = h
            entry = { canvas, ctx: canvas.getContext('2d'), w, h }
            _texCanvases.set(key, entry)
        }
        const img = entry.ctx.createImageData(w, h)
        img.data.set(argbToRgba(argb))
        entry.ctx.putImageData(img, 0, 0)
    },

    disposeTexture(id) {
        _texCanvases.delete('dyn:' + id)
    },

    // 统一绘制入口：图片（url id）→ 视频（数字 id）→ 动态纹理（'dyn:'+id）
    draw(id, dx, dy, dw, dh) {
        const img = _images[id]
        if (img) { canvas2d.getContext().drawImage(img, dx, dy, dw, dh); return }
        const v = videoTex.get(id)
        if (v) { canvas2d.getContext().drawImage(v, dx, dy, dw, dh); return }
        const entry = _texCanvases.get('dyn:' + id)
        if (entry) canvas2d.getContext().drawImage(entry.canvas, dx, dy, dw, dh)
    },
}
