namespace KFramework.MonoGame
{
    /// <summary>鼠标按键</summary>
    public enum MouseButton
    {
        Left,
        Right,
        Middle,
        XButton1,
        XButton2,
    }

    /// <summary>按键的瞬时状态</summary>
    public enum KPressState
    {
        /// <summary>未按下</summary>
        None,
        /// <summary>本帧刚按下</summary>
        Down,
        /// <summary>持续按住</summary>
        Held,
        /// <summary>本帧刚抬起</summary>
        Up,
    }

    /// <summary>
    /// 按键枚举。成员名与浏览器 <see href="https://www.w3.org/TR/uievents-code/">KeyboardEvent.code</see>
    /// 一一对应（KeyA / Digit0 / ArrowLeft / ShiftLeft / Numpad0 / F1 …），即"用 HTML code 的字符串作为键名"。
    /// C# 枚举不能赋字符串，故此处不写 <c>=</c> 后的数值，成员按声明顺序从 0 自动编号；
    /// TS 层 input_keyboard.ts 的 CODE_TO_KEYS 按相同顺序把 e.code 字符串映射到这里的序号，供 C# 索引按键状态。
    /// 想看某个键对应哪个 HTML code，直接读成员名即可（如 Keys.KeyA ⇔ "KeyA"）。
    /// </summary>
    public enum Keys : byte
    {
        // ===== 编辑 / 控制键 =====
        None,
        Backspace,
        Tab,
        Enter,
        Escape,
        Space,
        Delete,
        Insert,
        Home,
        End,
        PageUp,
        PageDown,
        ContextMenu,
        PrintScreen,
        Pause,
        CapsLock,
        NumLock,
        ScrollLock,

        // ===== 字母键（code: "KeyA".."KeyZ"）=====
        KeyA, KeyB, KeyC, KeyD, KeyE, KeyF, KeyG, KeyH, KeyI, KeyJ, KeyK, KeyL, KeyM,
        KeyN, KeyO, KeyP, KeyQ, KeyR, KeyS, KeyT, KeyU, KeyV, KeyW, KeyX, KeyY, KeyZ,

        // ===== 数字键（主键盘，code: "Digit0".."Digit9"）=====
        Digit0, Digit1, Digit2, Digit3, Digit4, Digit5, Digit6, Digit7, Digit8, Digit9,

        // ===== 标点 / 符号键（code 同名）=====
        Minus, Equal, BracketLeft, BracketRight, Backslash, Semicolon, Quote, Backquote, Comma, Period, Slash,

        // ===== 方向键 =====
        ArrowUp, ArrowDown, ArrowLeft, ArrowRight,

        // ===== 修饰键（左右区分）=====
        ShiftLeft, ShiftRight, ControlLeft, ControlRight, AltLeft, AltRight, MetaLeft, MetaRight,

        // ===== 功能键 F1..F24 =====
        F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12,
        F13, F14, F15, F16, F17, F18, F19, F20, F21, F22, F23, F24,

        // ===== 小键盘数字 =====
        Numpad0, Numpad1, Numpad2, Numpad3, Numpad4, Numpad5, Numpad6, Numpad7, Numpad8, Numpad9,

        // ===== 小键盘运算键 =====
        NumpadMultiply, NumpadAdd, NumpadSubtract, NumpadDecimal, NumpadDivide, NumpadEnter,
        NumpadEqual, NumpadComma, NumpadParenLeft, NumpadParenRight, NumpadSign, NumpadClear, NumpadClearEntry, NumpadBackspace,

        // ===== 浏览器键 =====
        BrowserBack, BrowserForward, BrowserHome, BrowserRefresh, BrowserSearch, BrowserStop, BrowserFavorites,

        // ===== 媒体键 =====
        MediaTrackNext, MediaTrackPrevious, MediaPlayPause, MediaStop, MediaSelect, MediaEject,

        // ===== 音频键 =====
        AudioVolumeMute, AudioVolumeDown, AudioVolumeUp,

        // ===== 启动键 =====
        LaunchApp1, LaunchApp2, LaunchApp3, LaunchMail, LaunchMediaPlayer, LaunchMusicPlayer,
        LaunchCalculator, LaunchFileBrowser, LaunchInternet, LaunchContacts, LaunchPhone,
        LaunchSpellChecker, LaunchWordProcessor, LaunchApplication1, LaunchApplication2,

        // ===== 电源键 =====
        Power, Sleep, WakeUp, Hibernate,

        // ===== IME / 各国键 =====
        IntlBackslash, IntlRo, IntlYen, Convert, NonConvert, KanaMode, KanjiMode,
        Hankaku, Zenkaku, Eisu, Lang1, Lang2, Lang3, Lang4, Lang5,
        Romaji, CodeInput, Compose, PrevCandidate, RomanCharacters,

        // ===== 杂项编辑键 =====
        Again, Copy, Cut, Paste, Undo, Redo, Find, Close, New, Open, Print, Save,
        SpellCheck, MailForward, MailReply, MailSend, Separator, Props, Select, Execute,
        Clear, Help, Cancel, CrSel, ExSel, EraseEof, Accept,

        // ===== Fn / 符号锁 =====
        Fn, FnLock, Symbol, SymbolLock, Hyper, Super,
    }
}
