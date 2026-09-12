// 原版 Mir3/Client/TargetForm.cs（WinForms 主窗口，: Form）在浏览器下不可用，
// 这里仅提供 CEnvir / DXSoundManager / DXControl 等引用到的类型外壳。
namespace Client
{
    using System;
    using MirEngine;

    public class TargetForm
    {
        public string Text { get; set; }
        public IntPtr Handle => IntPtr.Zero;
        public object ActiveControl { get; set; }
        public Size ClientSize { get; set; }
        public Rectangle DisplayRectangle => new Rectangle(0, 0, ClientSize.Width, ClientSize.Height);
        public Cursor Cursor { get; set; }

        public void Close() { }
        public void SuspendLayout() { }
        public void ResumeLayout() { }
        public void ResumeLayout(bool performLayout) { }
    }
}
