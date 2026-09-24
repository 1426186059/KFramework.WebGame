using Client.MirGraphics;
using SlimTexture = SlimDX.Direct3D9.Texture;
using SlimSurface = SlimDX.Direct3D9.Surface;
using EngineDevice = KFramework.MonoGame.GraphicsDevice;
using EngineBatch = KFramework.MonoGame.SpriteBatch;
using EngineColor = KFramework.MonoGame.Color;
using EngineRect = KFramework.MonoGame.Rectangle;
using EngineVector2 = KFramework.MonoGame.Vector2;
using EngineTexture2D = KFramework.MonoGame.Texture2D;
using EngineCaret = KFramework.MonoGame.TextRenderer.TextCaret;
using EngineOverlay = KFramework.MonoGame.TextRenderer.TextInputOverlay;
using EngineText = KFramework.MonoGame.TextRenderer.TextRenderer;

// 端口辅助：在 canvas 上还原 WinForm TextBox 的外观（文字 + 光标）。
// 原版 TextBox 是 WinForm 原生控件、不走 TextRenderer，故不放在 TextRenderer 上，单独提供。
//
// 文字排版复用引擎 TextInputOverlay.DrawTextBox；光标位置复用引擎 TextCaret.GetPosition，
// 与引擎自绘光标（TextCaretMode.Rendered）使用同一套度量，保证两种模式下光标位置一致。
public static class TextBoxRenderer
{
    // 仅用于取光标几何（位置/行高），不做闪烁节拍——闪烁由 MirTextBox 的 _caretVisible 驱动。
    private static readonly EngineCaret _caretGeometry = new EngineCaret();
    private static EngineTexture2D _whitePixel;

    public static void DrawTextBox(SlimTexture texture, int w, int h, string text, Font font, int foreColor, int backColor, int selBackColor, int textColor, int selectionStart, int selectionLength, int caretPos, bool focused, bool multiline)
    {
        if (texture?.RenderTarget == null) return;

        EngineDevice device = DXManager.GDevice;
        if (device == null) return;

        SlimSurface saved = DXManager.CurrentSurface;
        DXManager.SetSurface(new SlimSurface(texture));
        try
        {
            EngineBatch batch = DXManager.Batch;
            batch.Begin(KFramework.MonoGame.SpriteSortMode.Deferred,
                        KFramework.MonoGame.BlendState.NonPremultiplied,
                        KFramework.MonoGame.SamplerState.PointClamp);
            try
            {
                EngineColor fore = EngineColor.FromArgb((uint)textColor);
                EngineRect bounds = new EngineRect(0, 0, w, h);

                // caret 传 null：只画文字，光标单独按 focused 绘制。
                EngineOverlay.DrawTextBox(batch, device, font, text ?? string.Empty, bounds, fore,
                    caretPos, null, multiline, 3f);

                if (focused)
                    DrawCaret(batch, device, font, text, caretPos, bounds, fore, multiline);
            }
            finally
            {
                batch.End();
            }
        }
        finally
        {
            DXManager.SetSurface(saved);
        }
    }

    private static void DrawCaret(EngineBatch batch, EngineDevice device, Font font, string text, int caretPos, EngineRect bounds, EngineColor color, bool multiline)
    {
        try
        {
            EngineVector2 pos = _caretGeometry.GetPosition(device, font, text, caretPos, bounds, multiline, 3f);
            // 行高用引擎公开 API 取（SpriteFont 句柄是引擎内部实现细节，不对外暴露）。
            float lineH = EngineText.MeasureText(device, "W", font).Height;
            if (lineH <= 0f) lineH = font.GetPixelSize() * 4f / 3f;
            float caretW = System.Math.Max(1f, font.GetPixelSize() * 0.08f);

            batch.Draw(WhitePixel(device),
                new EngineRect(
                    bounds.X + (int)System.Math.Round(pos.X),
                    bounds.Y + (int)System.Math.Round(pos.Y),
                    (int)System.Math.Round(caretW),
                    (int)System.Math.Round(lineH)),
                color);
        }
        catch (System.Exception ex)
        {
            KFramework.MonoGame.PrintTool.Log($"[TextBoxRenderer] caret EX: {ex}");
        }
    }

    private static EngineTexture2D WhitePixel(EngineDevice device)
    {
        if (_whitePixel == null)
        {
            _whitePixel = device.CreateTexture(1, 1);
            _whitePixel.SetData(new byte[] { 255, 255, 255, 255 }, 0, 0, 1, 1);
        }
        return _whitePixel;
    }
}
