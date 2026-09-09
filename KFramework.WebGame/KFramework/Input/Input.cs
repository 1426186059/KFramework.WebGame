using System.Buffers.Binary;
using System.Runtime.InteropServices.JavaScript;

namespace KFramework;

/// <summary>
/// 输入状态。JS 侧维护原始状态，每帧由 <see cref="Poll"/> 一次性拉回，
/// 布局见 <see cref="Layout"/>。
/// </summary>
public static partial class Input
{
    internal static class Layout
    {
        public const int KeysOffset = 0;
        public const int KeysLength = 256;
        public const int MouseX = 256;
        public const int MouseY = 260;
        public const int MouseButtons = 264;
        public const int MouseWheel = 268;
        public const int TouchCount = 272;
        public const int Touches = 276;
        public const int TouchStride = 12;      // id:i32, x:i32, y:i32
        public const int MaxTouches = 8;
        public const int Size = Touches + TouchStride * MaxTouches;   // 372
    }

    private static readonly byte[] _current = new byte[Layout.Size];
    private static readonly byte[] _previous = new byte[Layout.Size];

    internal static byte[] Current => _current;
    internal static byte[] Previous => _previous;

    /// <summary>每帧由 <see cref="Game"/> 调用一次：交换缓冲并从 JS 拉取最新状态。</summary>
    internal static void Poll()
    {
        Array.Copy(_current, _previous, Layout.Size);
        PollCore(_current);
    }

    [JSImport("pollInput", "platform")]
    private static partial void PollCore([JSMarshalAs<JSType.MemoryView>] Span<byte> state);

    private static int ReadInt(byte[] buffer, int offset)
        => BinaryPrimitives.ReadInt32LittleEndian(buffer.AsSpan(offset, 4));

    public static KeyboardState GetKeyboardState() => new(_current, _previous);

    public static MouseState GetMouseState()
        => new(ReadInt(_current, Layout.MouseX),
               ReadInt(_current, Layout.MouseY),
               ReadInt(_current, Layout.MouseButtons),
               ReadInt(_current, Layout.MouseWheel),
               ReadInt(_previous, Layout.MouseButtons));

    public static TouchCollection GetTouchState()
        => new(_current, Math.Min(ReadInt(_current, Layout.TouchCount), Layout.MaxTouches));

    /// <summary>触屏/鼠标当前是否按住（用于移动端虚拟摇杆等统一处理）。</summary>
    public static bool IsPointerDown
    {
        get
        {
            var mouse = GetMouseState();
            if (mouse.LeftButton) return true;
            return GetTouchState().Count > 0;
        }
    }
}

public struct KeyboardState(byte[] current, byte[] previous)
{
    private readonly byte[] _current = current;
    private readonly byte[] _previous = previous;

    public bool IsKeyDown(Keys key) => key != Keys.None && _current[(int)key] != 0;

    public bool IsKeyUp(Keys key) => !IsKeyDown(key);

    /// <summary>本帧刚按下（边沿触发）。</summary>
    public bool IsKeyPressed(Keys key)
        => key != Keys.None && _current[(int)key] != 0 && _previous[(int)key] == 0;

    /// <summary>本帧刚松开（边沿触发）。</summary>
    public bool IsKeyReleased(Keys key)
        => key != Keys.None && _current[(int)key] == 0 && _previous[(int)key] != 0;

    /// <summary>归一化后的移动方向（WASD / 方向键）。</summary>
    public Vector2 MovementDirection
    {
        get
        {
            float x = 0f, y = 0f;
            if (IsKeyDown(Keys.A) || IsKeyDown(Keys.Left)) x -= 1f;
            if (IsKeyDown(Keys.D) || IsKeyDown(Keys.Right)) x += 1f;
            if (IsKeyDown(Keys.W) || IsKeyDown(Keys.Up)) y -= 1f;
            if (IsKeyDown(Keys.S) || IsKeyDown(Keys.Down)) y += 1f;
            if (x == 0f && y == 0f) return Vector2.Zero;
            return Vector2.Normalize(new Vector2(x, y));
        }
    }
}

public struct MouseState(int x, int y, int buttons, int wheel, int previousButtons)
{
    public readonly int X = x;
    public readonly int Y = y;
    public readonly int Buttons = buttons;
    public readonly int Wheel = wheel;
    private readonly int _previousButtons = previousButtons;

    public Vector2 Position => new(X, Y);
    public bool LeftButton => (Buttons & 1) != 0;
    public bool MiddleButton => (Buttons & 2) != 0;
    public bool RightButton => (Buttons & 4) != 0;
    public bool LeftPressed => LeftButton && (_previousButtons & 1) == 0;
    public bool LeftReleased => !LeftButton && (_previousButtons & 1) != 0;
    public int ScrollDelta => Wheel;
}

public struct TouchCollection(byte[] buffer, int count)
{
    private readonly byte[] _buffer = buffer;

    public readonly int Count = count;

    public TouchPoint this[int index]
    {
        get
        {
            int off = Input.Layout.Touches + index * Input.Layout.TouchStride;
            return new TouchPoint(
                BinaryPrimitives.ReadInt32LittleEndian(_buffer.AsSpan(off, 4)),
                new Vector2(
                    BinaryPrimitives.ReadInt32LittleEndian(_buffer.AsSpan(off + 4, 4)),
                    BinaryPrimitives.ReadInt32LittleEndian(_buffer.AsSpan(off + 8, 4))));
        }
    }
}

public readonly struct TouchPoint(int id, Vector2 position)
{
    /// <summary>触点 ID，同一根手指在按下期间保持不变。</summary>
    public readonly int Id = id;
    public readonly Vector2 Position = position;
}
