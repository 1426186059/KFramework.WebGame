
import { getCanvas } from './html_canvas.js';
import { Point } from './Point.js';


let currentCanvasId = '';
export function setCanvasId(id: string): void {
    currentCanvasId = id;
}

/** 取当前输入画布（供各输入模块与坐标换算共用）。 */
export function getInputCanvas(): HTMLCanvasElement | null {
    return getCanvas(currentCanvasId);
}

const pointOut: Point = { x: 0, y: 0 };
export function canvasPoint(clientX: number, clientY: number): Point 
{
    const canvas = getInputCanvas();
    if (!canvas) 
    {
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