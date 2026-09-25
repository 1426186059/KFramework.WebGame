# Original MirTextBox (Crystal) — Detailed Reference

Source: `D:\OpenSource\Crystal\Client\MirControls\MirTextBox.cs`

The original `MirTextBox` wraps a **native WinForms `TextBox`** (not a custom-drawn one). The WinForms
control is the single source of truth for text, selection, and IME; the `MirTextBox` only mirrors it
onto a Direct3D texture.

## The hidden native TextBox

```csharp
public bool CanLoseFocus;
public readonly TextBox TextBox;
private Pen CaretPen;

private static Point HiddenTextBoxLocation
{
    get { return new Point(-32000, -32000); }   // off-screen, but functional
}

private void ApplyNativeTextBoxState()
{
    if (TextBox == null || TextBox.IsDisposed) return;
    TextBox.Location = HiddenTextBoxLocation;     // off-screen
    TextBox.Visible = Visible && TextBox.Parent != null;
}
```

- The `TextBox` lives at `(-32000, -32000)` — far off-screen but still receives keyboard focus and IME.
- It is attached to `Program.Form` (the main WinForms form) so it can be focused (`TextBox.Focus()`).

## Rendering: DrawToBitmap + static caret

```csharp
protected unsafe override void CreateTexture()
{
    // ... create ControlTexture ...
    Point caret = GetCaretPosition();

    DataRectangle stream = ControlTexture.LockRectangle(0, LockFlags.Discard);
    using (Bitmap bm = new Bitmap(Size.Width, Size.Height, Size.Width * 4,
                                  PixelFormat.Format32bppArgb, stream.Data.DataPointer))
    {
        TextBox.DrawToBitmap(bm, new Rectangle(0, 0, Size.Width, Size.Height));  // text + selection
        using (Graphics graphics = Graphics.FromImage(bm))
        {
            graphics.DrawImage(bm, Point.Empty);
            if (TextBox.Focused)
                graphics.DrawLine(CaretPen,
                    new Point(caret.X, caret.Y),
                    new Point(caret.X, caret.Y + TextBox.Font.Height));          // STATIC caret line
        }
    }
    ControlTexture.UnlockRectangle(0);
    TextureValid = true;
}
```

Key points:

1. **Text and selection are drawn by `TextBox.DrawToBitmap`.** The native control renders its own
   text and selection highlight into the bitmap — the `MirTextBox` does not measure or draw strings.
2. **The caret is a single static vertical line** drawn with `CaretPen` when `TextBox.Focused`. There
   is **no blinking, no timer, no visibility toggle**. This is the faithful original behavior.
3. `DrawToBitmap` does **not** capture the OS blink caret, which is why the manual line exists. The
   manual line is steady, not animated.

## Caret position

```csharp
private Point GetCaretPosition()
{
    Point result = TextBox.GetPositionFromCharIndex(TextBox.SelectionStart);
    if (result.X == 0 && TextBox.Text.Length > 0)
    {
        result = TextBox.GetPositionFromCharIndex(TextBox.Text.Length - 1);
        int s = result.X / TextBox.Text.Length;
        result.X = (int)(result.X + (s * 1.46));
        result.Y = TextBox.GetLineFromCharIndex(TextBox.SelectionStart) * TextBox.Font.Height;
    }
    return result;
}
```

- Caret x comes from `GetPositionFromCharIndex(SelectionStart)` (pixel width of text before the caret).
- The `result.X == 0` branch is a workaround for an empty-prefix edge case (places the caret after the
  last char when the measured x is 0 but text is non-empty).
- Caret y is derived from the line index × font height.

## Text / selection pass-through

```csharp
public string Text
{
    get { return TextBox != null && !TextBox.IsDisposed ? TextBox.Text : null; }
    set
    {
        if (TextBox != null && !TextBox.IsDisposed)
        {
            TextBox.Text = value;
            TextBox_NeedRedraw(this, EventArgs.Empty);   // marks TextureValid=false + Redraw()
        }
    }
}
```

- Setting `Text` writes straight into the native `TextBox`; it does **not** adjust selection. After a
  programmatic `Text` set, the native `TextBox.SelectionStart` stays wherever it was (often 0), so the
  caret sits at the start until the user interacts — this is the original behavior, and any attempt to
  move the caret to the end on restore is a port-side enhancement, not a restoration.

## Redraw triggers

`TextBox_NeedRedraw` is wired to `KeyPress`, `KeyUp`, `TextChanged`, `MouseDown`, `MouseUp`,
`LostFocus`, `GotFocus`, `MouseWheel`, and `Shown`. Each sets `TextureValid = false; Redraw();`. The
original therefore only re-bakes the texture when something actually changes — it never needs a
per-frame redraw because the caret is static.

## IME

There is **no custom IME code** in the original `MirTextBox`. The native WinForms `TextBox` handles
composition, candidate windows, and language switching automatically while focused. The WebGL port must
replace this with a DOM `<input>` overlay (`JSBind_InputHtmlIme` + `input_html_ime.ts`) because a browser
canvas has no native IME — this is the only architecturally forced divergence.
