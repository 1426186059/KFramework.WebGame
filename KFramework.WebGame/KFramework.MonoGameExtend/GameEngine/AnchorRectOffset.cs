using System;

namespace KFramework.MonoGameExtend
{
    /// <summary>
    /// 矩形四边偏移（左/右/上/下），用于 Widget 的锚点偏移（对标 Unity 的 RectOffset / 锚点 margin）。
    /// 原名为 KRectangleFOffset，现独立成文件并改名。
    /// </summary>
    public struct AnchorRectOffset
    {
        public float Left;
        public float Right;
        public float Top;
        public float Bottom;

        public AnchorRectOffset(float Left, float Right, float Top, float Bottom)
        {
            this.Left = Left;
            this.Right = Right;
            this.Top = Top;
            this.Bottom = Bottom;
        }

        public static AnchorRectOffset Zero => new AnchorRectOffset(0, 0, 0, 0);

        public static bool operator ==(AnchorRectOffset a, AnchorRectOffset b)
        {
            return a.Left == b.Left && a.Right == b.Right && a.Top == b.Top && a.Bottom == b.Bottom;
        }

        public static bool operator !=(AnchorRectOffset a, AnchorRectOffset b)
        {
            return !(a == b);
        }

        public override bool Equals(object obj)
        {
            throw new NotImplementedException();
        }

        public override int GetHashCode()
        {
            throw new NotImplementedException();
        }

        public override string ToString()
        {
            return "{Left:" + Left + " Right:" + Right + " Top:" + Top + " Bottom:" + Bottom + "}";
        }
    }
}
