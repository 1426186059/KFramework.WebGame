---
name: mir2-original-reference
description: Authoritative reference for how the original Legend of Mir 2 (Crystal) WinForms client implemented UI and input features such as TextBox, caret, IME composition, and dialogs. This skill should be used when porting or matching original client behavior in the MonoGame/WebGL client (KFramework.WebGame), or when debugging a behavior that diverges from the original client. The original source of truth lives at D:\OpenSource\Crystal.
---

# Mir2 Original (Crystal) Reference

The original Legend of Mir 2 client (D:\OpenSource\Crystal, solution "Legend of Mir.sln") is a
WinForms + SlimDX/Direct3D9 application. The MonoGame/WebGL port under D:\OpenSource\KFramework.WebGame
re-implements the same UI on a different stack. When a ported control behaves differently from the
original, consult the original source first. Many "bugs" in the port are actually deviations from
(original) behavior that was intentionally simple.

## When to use this skill

- Porting a MirControl subclass or matching original client behavior.
- Debugging input/UI regressions (caret position, IME, focus, text rendering).
- Deciding whether a behavior is a bug or the intended original design.

## How to use

1. Locate the original control under D:\OpenSource\Crystal\Client\MirControls\ (e.g. MirTextBox.cs).
2. Read it alongside the ported version under
   D:\OpenSource\KFramework.WebGame\KFramework.Mir\WebGame.Mir2\WebGame.Mir2.MonoGame.Client\Mir2\MirControls\.
3. For the TextBox/caret/IME specifics, load references/mirtextbox.md. It captures the key
   architectural decisions that differ most from a naive port.

## Critical original-behavior facts (easy to get wrong)

- The caret does NOT blink in the original. MirTextBox.CreateTexture renders a hidden native
  WinForms TextBox to a bitmap via DrawToBitmap, then draws a single static vertical line
  (CaretPen) at the caret position only while TextBox.Focused. There is no timer, no blink
  interval, no on/off toggle. A ported blinking caret (e.g. TextCaret with a 530ms period) is a
  deviation, not a restoration. If the port suffers "caret stops blinking when idle", note that the
  original never had that requirement. A static, always-visible-when-focused caret is the faithful
  behavior and also needs no per-frame redraw.
- IME is handled entirely by the native WinForms TextBox. The original has no custom IME layer,
  no DOM bridge, no [JSExport] input shim. The hidden TextBox (placed off-screen at
  (-32000, -32000)) owns composition, candidate windows, and keyboard input natively.
  The WebGL port replaces this with a transparent DOM input overlay (JSBind_InputHtmlIme) because
  browsers give no native IME inside a canvas. That is the one place the port legitimately diverges.
- Text/selection is owned by the native TextBox. MirTextBox.Text, .SelectionStart, .Lines are thin
  pass-throughs to the inner TextBox. The control itself draws nothing but the bitmap of that inner
  box plus the static caret.

## Key original files

| Concern | Original (Crystal) | Ported (WebGL) |
|---------|--------------------|----------------|
| Text input box | Client/MirControls/MirTextBox.cs | ...MirControls/MirTextBox.cs |
| Hidden native TextBox + DrawToBitmap | same file, CreateTexture() | replaced by engine TextBox + TextBoxRenderer/TextCaret |
| IME | native WinForms TextBox (automatic) | KFramework.MonoGame/JSBind/JSBind_InputHtmlIme.cs + TS overlay |
| Caret rendering | static line in CreateTexture | TextRenderer/TextCaret.cs (blinking) + TextCaret/TextBoxRenderer |
