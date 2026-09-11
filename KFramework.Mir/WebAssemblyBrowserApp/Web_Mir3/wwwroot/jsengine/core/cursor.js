// mirengine/core/cursor.ts
// 浏览器端鼠标光标。对应 JSBind/BrowserCursor.cs（mir.setCursor）。
//
// 原版通过 Win32 .CUR 文件设置窗体光标，浏览器端无法加载 .CUR，
// 因此这里用**内联 SVG 图片**作为自定义光标（无需额外资源文件，也不会触发网络请求）。
// 想换成自己的图片：把对应项的 url 改成你的图片地址即可，例如
//   default: "url('Data/Cursors/my_arrow.png') 0 0, default"
import { dom } from '../shared.js';
/**
 * 把 SVG 源码包成 CSS cursor 可用的 url(...)。
 * 热点坐标（HOTSPOT）由调用方拼在后面：url(...) <x> <y>, <fallback>
 * @param body SVG 内部图形片段
 * @param w SVG 宽度（px）
 * @param h SVG 高度（px）
 * @returns 可直接拼进 CSS cursor 属性值的 url("data:image/svg+xml,...")
 */
const svgUrl = (body, w, h) => {
    const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="${w}" height="${h}" viewBox="0 0 ${w} ${h}">${body}</svg>`;
    return `url("data:image/svg+xml;utf8,${encodeURIComponent(svg)}")`;
};
// 经典箭头：白填充 + 黑描边，尖端在左上角（热点 0,0）
const ARROW = '<path d="M3 1 L3 17 L7.5 13.2 L10.5 20.5 L13.4 19.2 L10.5 12 L16.5 12 Z" ' +
    'fill="#ffffff" stroke="#000000" stroke-width="1.2" stroke-linejoin="round"/>';
// 攻击：红色准星（热点居中 12,12）
const ATTACK = '<path d="M12 2 L12 22 M2 12 L22 12" stroke="#ff3b30" stroke-width="2"/>' +
    '<circle cx="12" cy="12" r="6" fill="none" stroke="#ff3b30" stroke-width="2"/>';
/**
 * 设置画布的 CSS 光标（mir.setCursor）。
 * @param name 光标名（attack / npc / text / trash，其余走默认箭头）
 */
export const setCursor = (name) => {
    try {
        if (!dom || !dom.canvas)
            return;
        let css;
        switch (name) {
            case 'attack':
                css = `${svgUrl(ATTACK, 24, 24)} 12 12, crosshair`;
                break;
            case 'npc':
                css = 'pointer';
                break;
            case 'text':
                css = 'text';
                break;
            case 'trash':
                css = 'not-allowed';
                break;
            default:
                css = `${svgUrl(ARROW, 24, 24)} 0 0, default`;
                break;
        }
        dom.canvas.style.cursor = css;
    }
    catch (e) { }
};
