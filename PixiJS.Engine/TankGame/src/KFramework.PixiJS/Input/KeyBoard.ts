import { IDisposable } from "../Tool/IDisposable";

export class KeyBoard implements IDisposable
{
    private readonly LastKeys: { [key: string]: boolean } = {};
    private readonly Keys: Record<string, boolean> = {};
    private readonly OnKeyDownFunc = this.OnKeyDown.bind(this);
    private readonly OnKeyUpFunc = this.OnKeyUp.bind(this);

    constructor()
    {
        this.AddKeyboard();
    }

    public Dispose(): void 
    {
        this.RemoveKeyboard();
    }

    private AddKeyboard():void
    {
        window.addEventListener('keydown', this.OnKeyDownFunc);
        window.addEventListener('keyup', this.OnKeyUpFunc);
    }

    private RemoveKeyboard():void
    {
        window.removeEventListener('keydown', this.OnKeyDownFunc);
        window.removeEventListener('keyup', this.OnKeyUpFunc);
    }

    //event.key → 值是 字符，受 Shift/CapsLock 影响
    //event.code → 值是 物理键位名，永远不变
    private OnKeyDown(e:KeyboardEvent):void
    {
        this.LastKeys[e.code] = this.Keys[e.code];
        this.Keys[e.code] = true;
    }

    private OnKeyUp(e:KeyboardEvent):void
    {
        this.LastKeys[e.code] = this.Keys[e.code];
        this.Keys[e.code] = false;
    }

    public GetKeyDown(key:string):boolean
    {
        return this.Keys[key] && !this.LastKeys[key];
    }

    public GetKey(key:string):boolean
    {
       return this.Keys[key];
    }

    public GetKeyUp(key:string):boolean
    {
       return !this.Keys[key] && this.LastKeys[key];
    }
    
}

export enum KeyCode 
{
    // 字母键
    KeyA = 'KeyA',
    KeyB = 'KeyB',
    KeyC = 'KeyC',
    KeyD = 'KeyD',
    KeyE = 'KeyE',
    KeyF = 'KeyF',
    KeyG = 'KeyG',
    KeyH = 'KeyH',
    KeyI = 'KeyI',
    KeyJ = 'KeyJ',
    KeyK = 'KeyK',
    KeyL = 'KeyL',
    KeyM = 'KeyM',
    KeyN = 'KeyN',
    KeyO = 'KeyO',
    KeyP = 'KeyP',
    KeyQ = 'KeyQ',
    KeyR = 'KeyR',
    KeyS = 'KeyS',
    KeyT = 'KeyT',
    KeyU = 'KeyU',
    KeyV = 'KeyV',
    KeyW = 'KeyW',
    KeyX = 'KeyX',
    KeyY = 'KeyY',
    KeyZ = 'KeyZ',

    // 数字键（主键盘）
    Digit0 = 'Digit0',
    Digit1 = 'Digit1',
    Digit2 = 'Digit2',
    Digit3 = 'Digit3',
    Digit4 = 'Digit4',
    Digit5 = 'Digit5',
    Digit6 = 'Digit6',
    Digit7 = 'Digit7',
    Digit8 = 'Digit8',
    Digit9 = 'Digit9',

    // 方向键
    ArrowUp = 'ArrowUp',
    ArrowDown = 'ArrowDown',
    ArrowLeft = 'ArrowLeft',
    ArrowRight = 'ArrowRight',

    // 功能键
    Escape = 'Escape',
    Enter = 'Enter',
    Space = 'Space',
    Backspace = 'Backspace',
    Tab = 'Tab',
    ShiftLeft = 'ShiftLeft',
    ShiftRight = 'ShiftRight',
    ControlLeft = 'ControlLeft',
    ControlRight = 'ControlRight',
    AltLeft = 'AltLeft',
    AltRight = 'AltRight',

    // F 键
    F1 = 'F1',
    F2 = 'F2',
    F3 = 'F3',
    F4 = 'F4',
    F5 = 'F5',
    F6 = 'F6',
    F7 = 'F7',
    F8 = 'F8',
    F9 = 'F9',
    F10 = 'F10',
    F11 = 'F11',
    F12 = 'F12',
}