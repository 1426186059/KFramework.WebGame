// 平台层：画布尺寸自适应、requestAnimationFrame 主循环、输入采集。

import { getCanvasElement } from './gl.js';

let frameCallback = null;
let running = false;

export function setFrameCallback(callback) {
    frameCallback = callback;
}

export function startRenderLoop() {
    if (running) return;
    running = true;

    const tick = (timestamp) => {
        if (!running) return;
        try {
            frameCallback?.(timestamp);
        } catch (error) {
            console.error('[platform] 帧回调异常:', error);
        }
        requestAnimationFrame(tick);
    };

    requestAnimationFrame(tick);
}

export function stopRenderLoop() {
    running = false;
}

// ---------- 画布尺寸 ----------

function writeInts(view, values) {
    const array = new Int32Array(values);
    if (view instanceof Int32Array) {
        view.set(array);
        return;
    }
    if (typeof view.set === 'function') {
        view.set(array, 0);
        return;
    }
    // 兜底：逐元素写
    for (let i = 0; i < values.length; i++) view[i] = values[i];
}

export function getCanvasSize(view) {
    const canvas = getCanvasElement();
    if (!canvas) {
        writeInts(view, [1, 1, 1, 1, 1000]);
        return;
    }

    const dpr = Math.min(window.devicePixelRatio || 1, 2);
    const rect = canvas.getBoundingClientRect();
    const cssWidth = Math.max(1, Math.round(rect.width || canvas.clientWidth || 1));
    const cssHeight = Math.max(1, Math.round(rect.height || canvas.clientHeight || 1));
    const drawWidth = Math.max(1, Math.round(cssWidth * dpr));
    const drawHeight = Math.max(1, Math.round(cssHeight * dpr));

    if (canvas.width !== drawWidth || canvas.height !== drawHeight) {
        canvas.width = drawWidth;
        canvas.height = drawHeight;
    }

    writeInts(view, [cssWidth, cssHeight, drawWidth, drawHeight, Math.round(dpr * 1000)]);
}

export function setTitle(title) {
    document.title = title;
}

export function getQueryParameter(name) {
    return new URLSearchParams(window.location.search).get(name) ?? '';
}

export function isMobile() {
    return /Android|iPhone|iPad|iPod|Mobile/i.test(navigator.userAgent);
}

export function getBaseUri() {
    return document.baseURI || window.location.href;
}

// ---------- 输入 ----------

const STATE_SIZE = 372;
const KEYS_OFFSET = 0;
const MOUSE_X = 256;
const MOUSE_Y = 260;
const MOUSE_BUTTONS = 264;
const MOUSE_WHEEL = 268;
const TOUCH_COUNT = 272;
const TOUCHES = 276;
const TOUCH_STRIDE = 12;
const MAX_TOUCHES = 8;

const keys = new Uint8Array(256);
const scratch = new Uint8Array(STATE_SIZE);
const scratchView = new DataView(scratch.buffer);

let mouseX = 0;
let mouseY = 0;
let mouseButtons = 0;
let mouseWheel = 0;
const activeTouches = [];

function mapKey(code) {
    if (code.startsWith('Key') && code.length === 4) return code.charCodeAt(3);
    if (code.startsWith('Digit') && code.length === 6) return 48 + (code.charCodeAt(5) - 48);

    switch (code) {
        case 'Space': return 32;
        case 'ArrowLeft': return 37;
        case 'ArrowUp': return 38;
        case 'ArrowRight': return 39;
        case 'ArrowDown': return 40;
        case 'Enter':
        case 'NumpadEnter': return 13;
        case 'Escape': return 27;
        case 'Backspace': return 8;
        case 'Tab': return 9;
        case 'ShiftLeft':
        case 'ShiftRight': return 16;
        case 'ControlLeft':
        case 'ControlRight': return 17;
        case 'AltLeft':
        case 'AltRight': return 18;
        default: return 0;
    }
}

function localPoint(clientX, clientY) {
    const canvas = getCanvasElement();
    if (!canvas) return [0, 0];
    const rect = canvas.getBoundingClientRect();
    return [Math.round(clientX - rect.left), Math.round(clientY - rect.top)];
}

function bindInput() {
    window.addEventListener('keydown', (e) => {
        const key = mapKey(e.code);
        if (key) {
            keys[key] = 1;
            // 防止空格/方向键滚动页面
            if (key === 32 || (key >= 37 && key <= 40)) e.preventDefault();
        }
    });

    window.addEventListener('keyup', (e) => {
        const key = mapKey(e.code);
        if (key) keys[key] = 0;
    });

    window.addEventListener('blur', () => {
        keys.fill(0);
        mouseButtons = 0;
        activeTouches.length = 0;
    });

    const canvas = getCanvasElement();
    if (!canvas) return;

    canvas.addEventListener('mousemove', (e) => {
        [mouseX, mouseY] = localPoint(e.clientX, e.clientY);
    });

    canvas.addEventListener('mousedown', (e) => {
        [mouseX, mouseY] = localPoint(e.clientX, e.clientY);
        mouseButtons |= (1 << e.button);
        e.preventDefault();
    });

    window.addEventListener('mouseup', (e) => {
        mouseButtons &= ~(1 << e.button);
    });

    canvas.addEventListener('wheel', (e) => {
        mouseWheel += Math.sign(e.deltaY);
        e.preventDefault();
    }, { passive: false });

    canvas.addEventListener('contextmenu', (e) => e.preventDefault());

    const readTouches = (e) => {
        activeTouches.length = 0;
        for (let i = 0; i < e.touches.length && i < MAX_TOUCHES; i++) {
            const touch = e.touches[i];
            const [x, y] = localPoint(touch.clientX, touch.clientY);
            activeTouches.push({ id: touch.identifier, x, y });
        }
    };

    const touchOptions = { passive: false };
    canvas.addEventListener('touchstart', (e) => { readTouches(e); e.preventDefault(); }, touchOptions);
    canvas.addEventListener('touchmove', (e) => { readTouches(e); e.preventDefault(); }, touchOptions);
    canvas.addEventListener('touchend', (e) => { readTouches(e); e.preventDefault(); }, touchOptions);
    canvas.addEventListener('touchcancel', (e) => { readTouches(e); }, touchOptions);
}

let inputBound = false;

export function pollInput(view) {
    // 画布由 C# 在 GraphicsDevice 构造时初始化，这里首次 poll 时才绑定事件
    if (!inputBound) {
        bindInput();
        inputBound = true;
    }

    scratch.fill(0);
    scratch.set(keys, KEYS_OFFSET);

    scratchView.setInt32(MOUSE_X, mouseX, true);
    scratchView.setInt32(MOUSE_Y, mouseY, true);
    scratchView.setInt32(MOUSE_BUTTONS, mouseButtons, true);
    scratchView.setInt32(MOUSE_WHEEL, mouseWheel, true);
    mouseWheel = 0;

    const count = Math.min(activeTouches.length, MAX_TOUCHES);
    scratchView.setInt32(TOUCH_COUNT, count, true);
    for (let i = 0; i < count; i++) {
        const offset = TOUCHES + i * TOUCH_STRIDE;
        scratchView.setInt32(offset, activeTouches[i].id, true);
        scratchView.setInt32(offset + 4, activeTouches[i].x, true);
        scratchView.setInt32(offset + 8, activeTouches[i].y, true);
    }

    if (view instanceof Uint8Array) {
        view.set(scratch);
        return;
    }
    if (typeof view.set === 'function') {
        view.set(scratch, 0);
        return;
    }
    for (let i = 0; i < STATE_SIZE; i++) view[i] = scratch[i];
}
