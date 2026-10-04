using Client;
using Client.MirControls;
using Client.MirGraphics;


/// <summary>
/// 调试浮层（FPS / DC / Sprites / DPS / Ping …），对应原版 CMain.CreateDebugLabel 的浮层，
/// 但收成独立 sealed 控件并天生走离屏纹理缓存（DrawControlTexture）：
/// 整块 UI（灰底 + 边框 + 文本）只在文本变化时重新烘焙一次，平时每帧只把缓存纹理当 1 个
/// 精灵贴出来，避免原实现每帧重建字符串 + 重绘文本带来的分配与 GC 尖峰（问题 B）。
/// 由 CMain 在 Settings.DebugMode（F12）或 Tab 时创建，MirScene.Draw 在场景上屏后单独 Draw。
/// </summary>
public sealed class FPSDialog : MirControl
{
    private string _text = string.Empty;
    public string Text
    {
        get { return _text; }
        set
        {
            if (_text == value) return;           // 文本未变：命中离屏缓存，不重烘焙
            _text = value;
            OnTextChanged();
        }
    }

    private Font _font;
    public Font Font
    {
        get { return _font; }
        set { _font = ScaleFont(value); OnFontChanged(); }
    }

    public FPSDialog()
    {
        DrawControlTexture = true;                // 离屏纹理缓存：整块烘焙一次
        BackColour = Color.FromArgb(50, 50, 50);
        Border = true;
        BorderColour = Color.Black;
        ForeColour = Color.White;
        NotControl = true;
        Opacity = 1F;
        _font = ScaleFont(new Font(Settings.FontName, 8F));
    }

    private void OnTextChanged()
    {
        TextureValid = false;
        UpdateSize();
    }

    private void OnFontChanged()
    {
        TextureValid = false;
        UpdateSize();
    }

    // AutoSize：按当前文本测量尺寸（与 MirLabel 同款）。尺寸变化经基类 Size setter
    // 标记 TextureValid=false，下一帧 Draw 时触发 CreateTexture 重烘焙。
    private void UpdateSize()
    {
        if (string.IsNullOrEmpty(_text))
        {
            Size = Size.Empty;
            return;
        }
        Size = TextRenderer.MeasureText(_text, _font);
    }

    protected override void CreateTexture()
    {
        if (string.IsNullOrEmpty(_text)) return;
        if (Size.Width == 0 || Size.Height == 0) return;

        if (TextureSize != Size)
            DisposeTexture();

        if (ControlTexture == null || ControlTexture.Disposed)
        {
            DXManager.ControlList.Add(this);
            ControlTexture = DXManager.CreateRenderTarget(Size.Width, Size.Height);
            TextureSize = Size;
        }

        TextRenderer.Clear(ControlTexture, BackColour.ToArgb());
        TextRenderer.DrawText(ControlTexture, _text, _font,
            new MirEngine.Rectangle(0, 0, Size.Width, Size.Height), ForeColour.ToArgb(),
            TextFormatFlags.Left);

        TextureValid = true;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;

        if (_font != null)
        {
            _font.Dispose();
            _font = null;
        }
        _text = null;
    }
}
