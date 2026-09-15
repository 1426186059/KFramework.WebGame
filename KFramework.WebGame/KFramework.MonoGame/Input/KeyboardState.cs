using KFramework.MonoGame;

namespace KFramework.MonoGame
{
    /// <summary>
    /// 键盘状态快照。
    ///
    /// <para>内部持有 <see cref="Input"/> 的状态数组引用（held / pressed / released），
    /// 所以【不能跨帧缓存】：按下/抬起只在当帧有效，下一帧 <see cref="Input.Poll"/> 会清空。</para>
    ///
    /// <para>热路径推荐直接用 <see cref="Input.IsKeyDown"/> / <see cref="Input.IsKeyPressed"/> /
    /// <see cref="Input.IsKeyReleased"/>，零分配、无需构造本结构。</para>
    ///
    /// <para>键码由 C# 侧从浏览器的 keyCode 映射而来（见 Input.SetKey），与 XNA/MonoGame 的
    /// <see cref="Keys"/> 编码一致。</para>
    /// </summary>
    public struct KeyboardState(bool[] held, bool[] pressed, bool[] released)
    {
        private readonly bool[] _held = held;
        private readonly bool[] _pressed = pressed;
        private readonly bool[] _released = released;

        /// <summary>按键当前是否按住。</summary>
        public bool IsKeyDown(Keys key) => key != Keys.None && _held[(int)key];

        /// <summary>按键当前是否未按下。</summary>
        public bool IsKeyUp(Keys key) => !IsKeyDown(key);

        /// <summary>按键本帧是否刚按下。</summary>
        public bool IsKeyPressed(Keys key) => key != Keys.None && _pressed[(int)key];

        /// <summary>按键本帧是否刚松开。</summary>
        public bool IsKeyReleased(Keys key) => key != Keys.None && _released[(int)key];

        /// <summary>是否有任意键按住。</summary>
        public bool AnyKeyDown
        {
            get
            {
                for (int i = 1; i < Input.KeyCount; i++)
                    if (_held[i]) return true;
                return false;
            }
        }

        /// <summary>
        /// 当前按住的所有键。
        /// 注意每次调用都会 new 一个数组；热路径请改用 <see cref="Input.IsKeyDown"/> 遍历键槽。
        /// </summary>
        public Keys[] GetPressedKeys()
        {
            int count = GetPressedKeyCount();
            var result = new Keys[count];
            int n = 0;
            for (int i = 1; i < Input.KeyCount; i++)
                if (_held[i]) result[n++] = (Keys)i;
            return result;
        }

        /// <summary>当前按住的键数量。</summary>
        public int GetPressedKeyCount()
        {
            int count = 0;
            for (int i = 1; i < Input.KeyCount; i++)
                if (_held[i]) count++;
            return count;
        }

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
}
