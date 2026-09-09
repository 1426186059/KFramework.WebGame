// =====================================================================
// SkiaSharp 呈现层：
//   .NET 侧用 SKBitmap + SKCanvas 软件光栅化，每帧把整块 RGBA 像素
//   （byte[] → Uint8Array）交给这里，再用 putImageData 贴到 <canvas>。
//   浏览器侧不做任何绘制，只负责像素上传与自适应缩放。
// =====================================================================

let _canvas = null
let _ctx = null
let _w = 0
let _h = 0
let _img = null

export const skia = {
    init(selector, width, height) {
        const el = document.querySelector(selector)
        el.width = width
        el.height = height
        _canvas = el
        _w = width
        _h = height
        _ctx = el.getContext('2d', { alpha: false })
        // 复用同一个 ImageData，避免每帧分配约 1.92MB 对象带来的 GC 抖动
        _img = new ImageData(_w, _h)
    },

    // pixels: Uint8Array，长度 = w * h * 4，内存布局 RGBA（与 Skia Rgba8888 一致）
    present(pixels) {
        if (!_ctx) return
        _img.data.set(pixels)
        _ctx.putImageData(_img, 0, 0)
    },

    getCanvas() { return _canvas },
    getContext() { return _ctx },
}
