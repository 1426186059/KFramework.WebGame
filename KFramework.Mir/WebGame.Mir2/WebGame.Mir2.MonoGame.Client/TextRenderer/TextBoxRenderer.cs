using SlimDX.Direct3D9;

// 端口辅助：在 canvas 上还原 WinForm TextBox 的外观（文字 + 光标）。
// 原版 TextBox 是 WinForm 原生控件、不走 TextRenderer，故不放在 TextRenderer 上，单独提供。
// 光标定位用同一 SpriteFont 度量（TextRenderer.GetSpriteFont）。
//
// 注意：本文件不 using KFramework.MonoGame，避免 Texture / Color 与 shim 同名类型产生歧义。
public static class TextBoxRenderer
{
    public static void DrawTextBox(Texture texture, int w, int h, string text, Font font, int foreColor, int backColor, int selBackColor, int textColor, int selectionStart, int selectionLength, int caretPos, bool focused, bool multiline)
    {
        if (texture?.RenderTarget == null) return;

        var saved = TextRenderer.Bind(texture);
        var batch = Client.MirGraphics.DXManager.Batch;
        try
        {
            batch.Begin(KFramework.MonoGame.SpriteSortMode.Deferred,
                        KFramework.MonoGame.BlendState.NonPremultiplied,
                        KFramework.MonoGame.SamplerState.PointClamp);

            var sf = TextRenderer.GetSpriteFont(font);
            if (sf == null) return;

            float padLeft = 3f;
            float ty = multiline ? 2f : System.Math.Max(0f, (h - sf.LineHeight) / 2f);
            var fore = KFramework.MonoGame.Color.FromArgb((uint)textColor);

            if (!string.IsNullOrEmpty(text))
            {
                sf.Draw(batch, text, new KFramework.MonoGame.Vector2(padLeft, ty), fore, 0f,
                        KFramework.MonoGame.Vector2.Zero, 1f, KFramework.MonoGame.SpriteEffects.None, 0f);
            }

            // DOM 覆盖层透明后，光标由引擎自绘：focused 时画一条竖线。
            // 光标度量异常绝不能影响上面文字的提交，故单独保护并打印。
            if (focused)
            {
                try
                {
                    string prefix = (text != null && caretPos > 0)
                        ? text.Substring(0, System.Math.Min(caretPos, text.Length))
                        : "";
                    float caretX = padLeft + sf.MeasureString(prefix).X;
                    float caretW = System.Math.Max(1f, font.Size * 0.08f);
                    float caretH = sf.LineHeight;
                    batch.Draw(WhitePixel(),
                               new KFramework.MonoGame.Rectangle(
                                   (int)System.Math.Round(caretX), (int)System.Math.Round(ty),
                                   (int)System.Math.Round(caretW), (int)System.Math.Round(caretH)),
                               fore);
                }
                catch (System.Exception ex)
                {
                    KFramework.MonoGame.PrintTool.Log($"[TextBoxRenderer] caret EX: {ex}");
                }
            }
        }
        finally
        {
            batch.End();
            TextRenderer.BindRestore(saved);
        }
    }

    // 复用的 1x1 白纹理：用于画光标等纯色矩形。
    private static KFramework.MonoGame.Texture2D _whitePixel;
    private static KFramework.MonoGame.Texture2D WhitePixel()
    {
        if (_whitePixel == null)
        {
            var dev = Client.MirGraphics.DXManager.GDevice;
            _whitePixel = dev.CreateTexture(1, 1);
            _whitePixel.SetData(new byte[] { 255, 255, 255, 255 }, 0, 0, 1, 1);
        }
        return _whitePixel;
    }
}
