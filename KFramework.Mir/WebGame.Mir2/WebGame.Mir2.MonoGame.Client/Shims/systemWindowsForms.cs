// 宿主工程专用 shim：仅保留依赖 Client 业务层的类型。
// 公共 WinForms shim（TextBox / BorderStyle / SystemInformation / TextFormatFlags / Cursor /
// Timer / Application 等）已统一由通用库 MirEngine.Browser 提供
// （注：TextRenderer 已移除，文本度量改为直接走 TextRenderer.MeasureText，不再依赖通用库）
// （见 MirEngine.Browser/MirEngine/Shims/Forms/），本工程不得再定义同名类型。
namespace MirEngine
{
    using System;
    using WebGame.Mir2.MonoGame.Client;

    public static class Control
    {
        public static Keys ModifierKeys
        {
            get
            {
                Keys k = 0;
                if (CMain.Shift) k |= Keys.Shift;
                if (CMain.Ctrl) k |= Keys.Control;
                if (CMain.Alt) k |= Keys.Alt;
                return k;
            }
        }
    }
}
