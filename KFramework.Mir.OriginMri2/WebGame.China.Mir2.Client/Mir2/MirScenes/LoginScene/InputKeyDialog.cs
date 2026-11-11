using Client.MirControls;
using Client.MirGraphics;
using WebGame.Mir2.MonoGame.Client;

namespace Client.MirScenes
{
    public sealed partial class LoginScene
    {
        public sealed class InputKeyDialog : MirImageControl
        {
            public readonly MirButton KeyEscButton, KeyDelButton, KeyRandButton, KeyEnterButton;

            private LoginDialog _loginDialog;

            private List<MirButton> _buttons = new List<MirButton>();

            private char[] _letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray();
            private char[] _numbers = "0123456789".ToCharArray();

            public InputKeyDialog(LoginDialog loginDialog)
            {
                _loginDialog = loginDialog;

                // FGUI 登录模块没有对应软键盘，这里保留咱们自己的纯色面板（不套 FGUI 底图）。
                Visible = true;
                BackColour = Color.FromArgb(235, 24, 24, 34);
                Border = true;
                BorderColour = Color.FromArgb(160, 190, 170, 120);

                KeyEscButton = new MirButton
                {
                    Text = GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.BtnEsc),
                    HoverIndex = 301,
                    Index = 300,
                    Library = Libraries.Title,
                    Location = new Point(12, 12),
                    Parent = this,
                    PressedIndex = 302,
                    CenterText = true
                };
                KeyEscButton.Click += (o, e) => Dispose();

                KeyDelButton = new MirButton
                {
                    Text = GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.BtnDelete),
                    HoverIndex = 304,
                    Index = 303,
                    Library = Libraries.Title,
                    Location = new Point(140, 76),
                    Parent = this,
                    PressedIndex = 305,
                    CenterText = true
                };
                KeyDelButton.Click += (o, e) => SecureKeyDelete();

                KeyEnterButton = new MirButton
                {
                    Text = GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.BtnEnter),
                    HoverIndex = 307,
                    Index = 306,
                    Library = Libraries.Title,
                    Location = new Point(140, 236),
                    Parent = this,
                    PressedIndex = 308,
                    CenterText = true

                };
                KeyEnterButton.Click += (o, e) =>
                {
                    KeyPressEventArgs arg = new KeyPressEventArgs(Keys.Enter);

                    _loginDialog.TextBox_KeyPress(o, arg);
                };

                KeyRandButton = new MirButton
                {
                    Text = GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.BtnRandom),
                    HoverIndex = 310,
                    Index = 309,
                    Library = Libraries.Title,
                    Location = new Point(76, 236),
                    Parent = this,
                    PressedIndex = 311,
                    CenterText = true
                };
                KeyRandButton.Click += (o, e) =>
                {
                    _letters = new string(_letters.OrderBy(s => Guid.NewGuid()).ToArray()).ToCharArray();
                    _numbers = new string(_numbers.OrderBy(s => Guid.NewGuid()).ToArray()).ToCharArray();

                    UpdateKeys();
                };

                UpdateKeys();

                // 尺寸按软键盘按键包围盒固定，不依赖贴图库真实尺寸。
                AutoSize = false;
                Size = new Size(220, 290);

                Anchor = EAnchorType.MiddleCenter;
                AnchorPos = new Point(285, 150);
                ApplyAnchor();
                Libraries.LibraryLoaded += Libraries_LibraryLoaded;
            }

            private void Libraries_LibraryLoaded() => ApplyAnchor();

            private void DisposeKeys()
            {
                foreach (MirButton button in _buttons)
                {
                    if (button != null && !button.IsDisposed) button.Dispose();
                }
            }

            private void UpdateKeys()
            {
                DisposeKeys();

                for (int i = 0; i < _numbers.Length; i++)
                {
                    char key = _numbers[i];

                    MirButton numButton = new MirButton
                    {
                        HoverIndex = 1082,
                        Index = 1081,
                        Size = new Size(32, 30),
                        Library = Libraries.Prguse,
                        Location = new Point(12 + (i % 6 * 32), 44 + (i / 6 * 32)),
                        Parent = this,
                        PressedIndex = 1083,
                        Text = _numbers[i].ToString(),
                        CenterText = true
                    };
                    numButton.Click += (o, e) => SecureKeyPress(key);

                    _buttons.Add(numButton);
                }

                for (int i = 0; i < _letters.Length; i++)
                {
                    char key = _letters[i];

                    MirButton alphButton = new MirButton
                    {
                        HoverIndex = 1082,
                        Index = 1081,
                        Size = new Size(32, 30),
                        Library = Libraries.Prguse,
                        Location = new Point(12 + (i % 6 * 32), 108 + (i / 6 * 32)),
                        Parent = this,
                        PressedIndex = 1083,
                        Text = _letters[i].ToString(),
                        CenterText = true
                    };

                    alphButton.Click += (o, e) => SecureKeyPress(key);

                    _buttons.Add(alphButton);
                }
            }

            private void SecureKeyPress(char chr)
            {
                MirTextBox currentTextBox = GetFocussedTextBox();

                string keyToAdd = chr.ToString();

                if (CMain.IsKeyLocked(Keys.CapsLock))
                    keyToAdd = keyToAdd.ToUpper();
                else
                    keyToAdd = keyToAdd.ToLower();

                currentTextBox.Text += keyToAdd;
                currentTextBox.TextBox.SelectionLength = 0;
                currentTextBox.TextBox.SelectionStart = currentTextBox.Text.Length;
            }

            private void SecureKeyDelete()
            {
                MirTextBox currentTextBox = GetFocussedTextBox();

                if (currentTextBox.TextBox.SelectionLength > 0)
                {
                    currentTextBox.Text = currentTextBox.Text.Remove(currentTextBox.TextBox.SelectionStart, currentTextBox.TextBox.SelectionLength);
                }
                else if (currentTextBox.Text.Length > 0)
                {
                    currentTextBox.Text = currentTextBox.Text.Remove(currentTextBox.Text.Length - 1);
                }

                currentTextBox.TextBox.SelectionStart = currentTextBox.Text.Length;
            }

            private MirTextBox GetFocussedTextBox()
            {
                if (_loginDialog.AccountIDTextBox.TextBox.Focused)
                    return _loginDialog.AccountIDTextBox;
                else
                    return _loginDialog.PasswordTextBox;
            }

            protected override void Dispose(bool disposing)
            {
                Libraries.LibraryLoaded -= Libraries_LibraryLoaded;

                base.Dispose(disposing);

                DisposeKeys();
            }
        }
    }
}
