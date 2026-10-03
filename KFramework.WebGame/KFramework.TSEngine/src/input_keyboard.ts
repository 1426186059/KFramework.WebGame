// 【依赖 C#】由 KFramework.MonoGame.JSBind_Input_Keyboard 经 [JSImport(module: "input_keyboard")] 调用；产物 input_keyboard.js 由 SyncJsEngine 复制。
import { getCanvas, focusCanvas } from './html_canvas.js';
import { E_HTML_Event_Type, FrameDataStream } from './html_event_type.js';

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

let m_Canvas: HTMLCanvasElement | null = null;
let m_CanvasId: string | null = null;

/**
 * 装置是否<b>激活</b>（= 是否绑着监听在采集）：由 bindKeyboard / unbindKeyboard 维护，
 * 与 C# 侧 <c>Input_KeyBoard.Active</c> 一一对应。
 *
 * 写入前必须先看它 —— 未激活时不仅不写，还要把攒下的清空：
 * 停用期间残留的按键数据到重新激活时<b>早已陈旧</b>（窗外按下的键，keyup 永远收不到），
 * 留着就会在激活后的第一帧一次性涌出，表现为"刚启用就有个键卡在按住"。
 *
 * 只有这一个开关：C# 的 Activate / Deactivate 与这里的
 * bind / unbind 成对调用，"绑着监听"就是"在采集"，两个标志表达同一件事，迟早有一处忘了同步。
 */
let enabled = false;

// 失焦不再由本模块处理：一律汇到 input_window_event 的 SysFocusLost，
// 由 takeFrameData 判定后单独上报一条，并调本模块的 discardKeyboardEvents 清空这里。
// 本模块因此不再监听 blur —— 与画布 blur 重复（那边已监听），window 失焦也有 hasFocus 每帧兜底。

const pending = new Map<string, number>();
const scratch = new Uint8Array(1);      // 每条事件的 data 只有 1 字节（Keys 序号），逐条 put

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
        m_Canvas = getCanvas();
        m_CanvasId = canvasId ?? null;
        if(m_Canvas)
        {
            focusCanvas(m_CanvasId);
            m_Canvas.addEventListener('keydown', Process_KeyDown);
            m_Canvas.addEventListener('keyup', Process_KeyUp);
            m_Canvas.addEventListener('pointerdown', Process_Pointerdown);
            enabled = true;                      // 监听真正挂上了才算激活
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
        enabled = true;
    }
}

export function unbindKeyboard(): void 
{
    enabled = false;                // 先置未激活：此后攒下的数据一律不再上报
    if (m_Canvas)
    {
        m_Canvas.removeEventListener('keydown', Process_KeyDown);
        m_Canvas.removeEventListener('keyup', Process_KeyUp);
        m_Canvas.removeEventListener('pointerdown', Process_Pointerdown);
        focusCanvas(m_CanvasId, false);
    }
    else
    {
        window.removeEventListener('keydown', Process_KeyDown);
        window.removeEventListener('keyup', Process_KeyUp);
    }
    
    m_Canvas = null
    m_CanvasId = null
    pending.clear();
}

/**
 * 把本帧的键盘事件写入【统一事件流】（由 game_frame_take_js_data 每帧调用）。
 * 每条 = EvType（KeyDown / KeyUp）+ 1 字节 Keys 序号，条数由总入口统计。
 */
export function writeKeyboardEvents(w: FrameDataStream): void
{
    if (!enabled)
    {
        discardKeyboardEvents();            // 未激活：不采集，也不留陈旧数据（理由见 active 的注释）
        return;
    }

    let n = 0;
    for (const [code, flag] of pending)
    {
        if (n >= MAX_EVENTS) break;
        scratch[0] = codeToKeys(code);
        w.put(flag === 2 ? E_HTML_Event_Type.KeyUp : E_HTML_Event_Type.KeyDown, scratch, 1);
        n++;
    }

    pending.clear();
}

/**
 * 作废本帧攒下的键盘事件（失焦帧由 game_frame_take_js_data 调用）。
 *
 * 必须真丢掉、不能留到下一帧：失焦前按下的键若在窗外松手，keyup 永远收不到，
 * 把那条"按下"留到下一帧发出去，C# 侧就会在没有 keyup 的情况下一直显示按住。
 */
export function discardKeyboardEvents(): void
{
    pending.clear();
}
