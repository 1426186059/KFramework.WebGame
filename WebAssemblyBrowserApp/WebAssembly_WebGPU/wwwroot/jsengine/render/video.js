// =====================================================================
// 视频纹理（独立文件，三端共用同一份逻辑）。
//
// GPU 解码说明：视频由浏览器硬件解码器解码，这里只负责创建 <video>、
// 分配 id、加载状态管理。绘制时当前解码帧直接作为纹理源：
//   - Canvas2D：ctx.drawImage(video)              —— 浏览器内部上传
//   - WebGL   ：texImage2D(TEXTURE_2D,...,video)  —— 每帧直传当前帧
//   - WebGPU  ：importExternalTexture({source:video}) —— 每帧零拷贝 GPU 导入
// 全程无 CPU 像素解码/拷贝，是 Web 上真正的「GPU 解码」路径。
// 各端 renderer 通过 createVideoElement() 拿 video 元素后自行接入 GPU 管线。
// =====================================================================

const _videos = {}
let _seq = 0

// 创建视频元素（muted + playsInline + 自动播放 + 循环）。
// onReady(video, w, h)：loadedmetadata 后触发；onError()：加载失败。
export function createVideoElement(url, onReady, onError) {
    const video = document.createElement('video')
    video.muted = true
    video.playsInline = true
    video.crossOrigin = 'anonymous'
    video.autoplay = true
    video.loop = true
    video.addEventListener('loadedmetadata', () => {
        onReady(video, video.videoWidth, video.videoHeight)
        video.play().catch(() => {})
    })
    video.addEventListener('error', () => onError())
    video.src = url
    video.load()
    return video
}

export const videoTex = {
    // 加载视频纹理，返回 { id, w, h }（失败 id=-1）
    load(url) {
        return new Promise((resolve) => {
            const id = _seq++
            createVideoElement(url, (video, w, h) => {
                _videos[id] = video
                resolve({ id, w, h })
            }, () => resolve({ id: -1, w: 0, h: 0 }))
        })
    },

    get(id) { return _videos[id] },
}
