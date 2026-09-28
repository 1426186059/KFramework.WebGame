// 【依赖 C#】由 KFramework.MonoGame.JSBind_Input_Keyboard 经 [JSImport(module: "input_keyboard")] 调用；产物 input_keyboard.js 由 SyncJsEngine 复制。
import { getCanvas, focusCanvas } from './html_canvas.js';

// 浏览器 KeyboardEvent.code → C# Keys 枚举序号。
// 必须与 Input/InputDefine.cs 的 Keys 声明顺序严格一致（成员名即 HTML code，顺序自动编号：
// None=0, Backspace=1, Tab=2 … Super=212）。C# 侧按此序号索引按键状态。
// 不在表内的键返回 0（= Keys.None），C# 侧直接忽略（k<=0）。
const CODE_TO_KEYS: { [code: string]: number } = {
    // 编辑 / 控制键
    'Backspace': 1, 'Tab': 2, 'Enter': 3, 'Escape': 4, 'Space': 5, 'Delete': 6,
    'Insert': 7, 'Home': 8, 'End': 9, 'PageUp': 10, 'PageDown': 11, 'ContextMenu': 12,
    'PrintScreen': 13, 'Pause': 14, 'CapsLock': 15, 'NumLock': 16, 'ScrollLock': 17,

    // 字母
    'KeyA': 18, 'KeyB': 19, 'KeyC': 20, 'KeyD': 21, 'KeyE': 22, 'KeyF': 23, 'KeyG': 24,
    'KeyH': 25, 'KeyI': 26, 'KeyJ': 27, 'KeyK': 28, 'KeyL': 29, 'KeyM': 30, 'KeyN': 31,
    'KeyO': 32, 'KeyP': 33, 'KeyQ': 34, 'KeyR': 35, 'KeyS': 36, 'KeyT': 37, 'KeyU': 38,
    'KeyV': 39, 'KeyW': 40, 'KeyX': 41, 'KeyY': 42, 'KeyZ': 43,

    // 数字（主键盘）
    'Digit0': 44, 'Digit1': 45, 'Digit2': 46, 'Digit3': 47, 'Digit4': 48, 'Digit5': 49,
    'Digit6': 50, 'Digit7': 51, 'Digit8': 52, 'Digit9': 53,

    // 标点 / 符号
    'Minus': 54, 'Equal': 55, 'BracketLeft': 56, 'BracketRight': 57, 'Backslash': 58,
    'Semicolon': 59, 'Quote': 60, 'Backquote': 61, 'Comma': 62, 'Period': 63, 'Slash': 64,

    // 方向键
    'ArrowUp': 65, 'ArrowDown': 66, 'ArrowLeft': 67, 'ArrowRight': 68,

    // 修饰键（左右区分）
    'ShiftLeft': 69, 'ShiftRight': 70, 'ControlLeft': 71, 'ControlRight': 72,
    'AltLeft': 73, 'AltRight': 74, 'MetaLeft': 75, 'MetaRight': 76,

    // 功能键
    'F1': 77, 'F2': 78, 'F3': 79, 'F4': 80, 'F5': 81, 'F6': 82, 'F7': 83, 'F8': 84,
    'F9': 85, 'F10': 86, 'F11': 87, 'F12': 88,
    'F13': 89, 'F14': 90, 'F15': 91, 'F16': 92, 'F17': 93, 'F18': 94, 'F19': 95,
    'F20': 96, 'F21': 97, 'F22': 98, 'F23': 99, 'F24': 100,

    // 小键盘数字
    'Numpad0': 101, 'Numpad1': 102, 'Numpad2': 103, 'Numpad3': 104, 'Numpad4': 105,
    'Numpad5': 106, 'Numpad6': 107, 'Numpad7': 108, 'Numpad8': 109, 'Numpad9': 110,

    // 小键盘运算键
    'NumpadMultiply': 111, 'NumpadAdd': 112, 'NumpadSubtract': 113, 'NumpadDecimal': 114,
    'NumpadDivide': 115, 'NumpadEnter': 116, 'NumpadEqual': 117, 'NumpadComma': 118,
    'NumpadParenLeft': 119, 'NumpadParenRight': 120, 'NumpadSign': 121,
    'NumpadClear': 122, 'NumpadClearEntry': 123, 'NumpadBackspace': 124,

    // 浏览器键
    'BrowserBack': 125, 'BrowserForward': 126, 'BrowserHome': 127, 'BrowserRefresh': 128,
    'BrowserSearch': 129, 'BrowserStop': 130, 'BrowserFavorites': 131,

    // 媒体键
    'MediaTrackNext': 132, 'MediaTrackPrevious': 133, 'MediaPlayPause': 134,
    'MediaStop': 135, 'MediaSelect': 136, 'MediaEject': 137,

    // 音频键
    'AudioVolumeMute': 138, 'AudioVolumeDown': 139, 'AudioVolumeUp': 140,

    // 启动键
    'LaunchApp1': 141, 'LaunchApp2': 142, 'LaunchApp3': 143, 'LaunchMail': 144,
    'LaunchMediaPlayer': 145, 'LaunchMusicPlayer': 146, 'LaunchCalculator': 147,
    'LaunchFileBrowser': 148, 'LaunchInternet': 149, 'LaunchContacts': 150,
    'LaunchPhone': 151, 'LaunchSpellChecker': 152, 'LaunchWordProcessor': 153,
    'LaunchApplication1': 154, 'LaunchApplication2': 155,

    // 电源键
    'Power': 156, 'Sleep': 157, 'WakeUp': 158, 'Hibernate': 159,

    // IME / 各国键
    'IntlBackslash': 160, 'IntlRo': 161, 'IntlYen': 162, 'Convert': 163, 'NonConvert': 164,
    'KanaMode': 165, 'KanjiMode': 166, 'Hankaku': 167, 'Zenkaku': 168, 'Eisu': 169,
    'Lang1': 170, 'Lang2': 171, 'Lang3': 172, 'Lang4': 173, 'Lang5': 174,
    'Romaji': 175, 'CodeInput': 176, 'Compose': 177, 'PrevCandidate': 178, 'RomanCharacters': 179,

    // 杂项编辑键
    'Again': 180, 'Copy': 181, 'Cut': 182, 'Paste': 183, 'Undo': 184, 'Redo': 185,
    'Find': 186, 'Close': 187, 'New': 188, 'Open': 189, 'Print': 190, 'Save': 191,
    'SpellCheck': 192, 'MailForward': 193, 'MailReply': 194, 'MailSend': 195,
    'Separator': 196, 'Props': 197, 'Select': 198, 'Execute': 199, 'Clear': 200,
    'Help': 201, 'Cancel': 202, 'CrSel': 203, 'ExSel': 204, 'EraseEof': 205, 'Accept': 206,

    // Fn / 符号锁
    'Fn': 207, 'FnLock': 208, 'Symbol': 209, 'SymbolLock': 210, 'Hyper': 211, 'Super': 212,
};

// KeyboardEvent.code → Keys 数值（仅查表；未列出的键返回 0，C# 侧忽略）。
function codeToKeys(code: string): number {
    return CODE_TO_KEYS[code] ?? 0;
}

const MAX_EVENTS = 32;
const STRIDE = 2;
const SIZE = 1 + MAX_EVENTS * STRIDE;
const EvBlur = 10; // 与 C# Input_KeyBoard.EvBlur 一致：失焦事件

let m_Canvas: HTMLCanvasElement | null = null;
let m_CanvasId: string | null = null;
// 失焦标记：canvas 失去焦点时置 true，下一次 pollKeyboard 写入一条 Blur 事件（flag=EvBlur），
// 让 C# 侧清空键盘状态，避免“按住键在窗外松手 → 卡住”。
let m_Blurred = false;

const pending = new Map<string, number>();
const scratch = new Uint8Array(SIZE);

function Process_KeyDown(e: KeyboardEvent): void
{
    // 阻止浏览器默认行为，否则游戏收不到这些键：
    //   Tab → 移走焦点（画布随即 blur，下一次 poll 发 Blur 清空按键，表现为“Tab 抓不到”）；
    //   Space / 方向键 → 滚动页面。
    // 仅在按键确实在映射表内时拦截；带 Ctrl/Alt/Meta 的组合键不拦，保留 Ctrl+R / Ctrl+Shift+I 等浏览器快捷键。
    if (!e.ctrlKey && !e.altKey && !e.metaKey && codeToKeys(e.code) !== 0)
    {
        e.preventDefault();
    }
    pending.set(e.code, 1)
}

function Process_KeyUp(e: KeyboardEvent): void
{
    pending.set(e.code, 2)
}

// 失焦：仅置标记。真正清空延到 pollKeyboard 随 Blur 事件发往 C#，保证与一次 poll 时序对齐，
// 且能顺带丢弃失焦前残留的“按下”事件（否则会卡成一直按住）。
function Process_Blur(): void
{
    m_Blurred = true;
}

//失焦后，点击屏幕恢复焦点
function Process_Pointerdown():void
{
    m_Canvas?.focus();
}

// 键盘监听绑在 canvas 上（依赖画布获焦才会收到 key 事件）。
// <canvas> 默认不可获焦，所以绑监听前必须先 focusCanvas 让它可获焦并聚焦；同时挂一个 pointerdown 重新聚焦，
// 这样切走窗口 / 在输入框打字后点回游戏，键盘依然有效。IME 输入框获焦时不会触发 canvas 的 key 事件，不会误报游戏键。
export function bindKeyboard(canvasId?: string): void
{
    if (canvasId)
    {
        console.log("Canvas bindKeyboard ");
        m_Canvas = getCanvas(canvasId);
        m_CanvasId = canvasId ?? null;
        if(m_Canvas)
        {
            focusCanvas(m_CanvasId);
            m_Canvas.addEventListener('keydown', Process_KeyDown);
            m_Canvas.addEventListener('keyup', Process_KeyUp);
            m_Canvas.addEventListener('blur', Process_Blur);
            m_Canvas.addEventListener('pointerdown', Process_Pointerdown);
        }
        else
        {
            console.error("bindKeyboard canvas Find Error");
        }
    }
    else
    {
        console.log("window bindKeyboard ");
        // 找不到画布（极少见）才回落到 window，保证至少有输入。
        window.addEventListener('keydown', Process_KeyDown);
        window.addEventListener('keyup', Process_KeyUp);
        window.addEventListener('blur', Process_Blur);
    }
}

export function unbindKeyboard(): void 
{
    if (m_Canvas)
    {
        m_Canvas.removeEventListener('keydown', Process_KeyDown);
        m_Canvas.removeEventListener('keyup', Process_KeyUp);
        m_Canvas.removeEventListener('blur', Process_Blur);
        m_Canvas.removeEventListener('pointerdown', Process_Pointerdown);
        focusCanvas(m_CanvasId, false);
    }
    else
    {
        window.removeEventListener('keydown', Process_KeyDown);
        window.removeEventListener('keyup', Process_KeyUp);
        window.removeEventListener('blur', Process_Blur);
    }
    
    m_Canvas = null
    m_CanvasId = null
    pending.clear();
}

export function pollKeyboard(target: MemoryView_Span): void 
{
    // 先写入真正的 Uint8Array（scratch），再经由 MemoryView.set 写回 C# 缓冲。
    // 注意：MemoryView_Span 不是 Uint8Array、没有 [] 索引器，不能直接 target[i]=x。
    //
    // 布局：scratch[0] = 事件条数；其后每 2 字节一对 (keyCode, flag)，flag 1=按下 / 2=抬起 / 10=失焦。
    // 失焦时先写入一条 Blur 事件，并丢弃失焦前残留的“按下”事件，避免“卡键”。

    let off = 1;
    let nEvents = 0;

    if (m_Blurred)
    {
        m_Blurred = false;

        scratch[off++] = 0;      // keyCode 对 Blur 无意义
        scratch[off++] = EvBlur; // 10
        nEvents++;
    }
    else
    {
        for (const [key, flag] of pending) 
        {
            if (off + STRIDE > SIZE) 
            {
                break;
            }
            scratch[off++] = codeToKeys(key);
            scratch[off++] = flag;
            nEvents++;
        }
    }

    pending.clear();
    scratch[0] = nEvents;
    target.set(scratch.subarray(0, off));
}
