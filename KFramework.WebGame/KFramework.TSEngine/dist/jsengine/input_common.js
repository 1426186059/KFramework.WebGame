import { getCanvas } from './html_canvas.js';
/** 取当前输入画布（供各输入模块与坐标换算共用）。 */
export function getInputCanvas() {
    return getCanvas();
}
const pointOut = { x: 0, y: 0 };
export function canvasPoint(clientX, clientY) {
    const canvas = getInputCanvas();
    if (!canvas) {
        pointOut.x = 0;
        pointOut.y = 0;
        return pointOut;
    }
    const rect = canvas.getBoundingClientRect();
    const scaleX = canvas.width / (rect.width || 1);
    const scaleY = canvas.height / (rect.height || 1);
    pointOut.x = Math.round((clientX - rect.left) * scaleX);
    pointOut.y = Math.round((clientY - rect.top) * scaleY);
    return pointOut;
}
