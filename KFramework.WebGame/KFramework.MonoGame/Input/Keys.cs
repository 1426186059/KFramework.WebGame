namespace KFramework;

/// <summary>
/// 键位定义。字母与数字沿用 ASCII 码，方向键沿用 37..40，
/// 便于 JS 侧用 <c>event.code</c> 直接映射。
/// </summary>
public enum Keys : byte
{
    None = 0,

    Backspace = 8,
    Tab = 9,
    Enter = 13,
    Shift = 16,
    Control = 17,
    Alt = 18,
    Escape = 27,

    Space = 32,

    Left = 37,
    Up = 38,
    Right = 39,
    Down = 40,

    D0 = 48, D1 = 49, D2 = 50, D3 = 51, D4 = 52,
    D5 = 53, D6 = 54, D7 = 55, D8 = 56, D9 = 57,

    A = 65, B = 66, C = 67, D = 68, E = 69, F = 70, G = 71, H = 72, I = 73,
    J = 74, K = 75, L = 76, M = 77, N = 78, O = 79, P = 80, Q = 81, R = 82,
    S = 83, T = 84, U = 85, V = 86, W = 87, X = 88, Y = 89, Z = 90,
}
