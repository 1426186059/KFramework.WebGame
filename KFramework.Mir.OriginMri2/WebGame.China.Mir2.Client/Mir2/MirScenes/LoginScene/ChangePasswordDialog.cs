using Client.MirControls;
using Client.MirGraphics;
using Client.MirNetwork;
using System.Text.RegularExpressions;
using C = ClientPackets;

namespace Client.MirScenes
{
    public sealed partial class LoginScene
    {
        public sealed class ChangePasswordDialog : MirImageControl
        {
            public readonly MirButton OKButton,
                                      CancelButton;

            public readonly MirTextBox AccountIDTextBox,
                                       CurrentPasswordTextBox,
                                       NewPassword1TextBox,
                                       NewPassword2TextBox;

            private bool _accountIDValid,
                         _currentPasswordValid,
                         _newPassword1Valid,
                         _newPassword2Valid;

            public ChangePasswordDialog()
            {
                Index = 50;
                Library = Libraries.Prguse;

                CancelButton = new MirButton
                {
                    HoverIndex = 111,
                    Index = 110,
                    Library = Libraries.Title,
                    Location = new Point(222, 236),
                    Parent = this,
                    PressedIndex = 112
                };
                CancelButton.Click += (o, e) => Dispose();

                OKButton = new MirButton
                {
                    Enabled = false,
                    HoverIndex = 108,
                    Index = 107,
                    Library = Libraries.Title,
                    Location = new Point(80, 236),
                    Parent = this,
                    PressedIndex = 109,
                };
                OKButton.Click += (o, e) => ChangePassword();


                AccountIDTextBox = new MirTextBox
                {
                    Border = true,
                    BorderColour = Color.Gray,
                    Location = new Point(178, 75),
                    MaxLength = Globals.MaxAccountIDLength,
                    Parent = this,
                    Size = new Size(136, 18),
                };
                AccountIDTextBox.SetFocus();
                AccountIDTextBox.TextBox.MaxLength = Globals.MaxAccountIDLength;
                AccountIDTextBox.TextBox.TextChanged += AccountIDTextBox_TextChanged;

                CurrentPasswordTextBox = new MirTextBox
                {
                    Border = true,
                    BorderColour = Color.Gray,
                    Location = new Point(178, 113),
                    MaxLength = Globals.MaxPasswordLength,
                    Parent = this,
                    Password = true,
                    Size = new Size(136, 18),
                    TextBox = { MaxLength = Globals.MaxPasswordLength },
                };
                CurrentPasswordTextBox.TextBox.TextChanged += CurrentPasswordTextBox_TextChanged;

                NewPassword1TextBox = new MirTextBox
                {
                    Border = true,
                    BorderColour = Color.Gray,
                    Location = new Point(178, 151),
                    MaxLength = Globals.MaxPasswordLength,
                    Parent = this,
                    Password = true,
                    Size = new Size(136, 18),
                    TextBox = { MaxLength = Globals.MaxPasswordLength },
                };
                NewPassword1TextBox.TextBox.TextChanged += NewPassword1TextBox_TextChanged;

                NewPassword2TextBox = new MirTextBox
                {
                    Border = true,
                    BorderColour = Color.Gray,
                    Location = new Point(178, 188),
                    MaxLength = Globals.MaxPasswordLength,
                    Parent = this,
                    Password = true,
                    Size = new Size(136, 18),
                    TextBox = { MaxLength = Globals.MaxPasswordLength },
                };
                NewPassword2TextBox.TextBox.TextChanged += NewPassword2TextBox_TextChanged;

                // 浏览器端资源异步加载，MirImageControl 在 AutoSize 下依赖的 GetTrueSize 可能返回 0，
                // 导致 Recenter 把对话框定位到屏幕正中、子控件全被甩到屏幕外（点击“新账户/修改密码”无反应）。
                // 显式关闭 AutoSize 并给出确定尺寸：优先用真实贴图尺寸，否则用子控件包围盒兜底，确保对话框可见且居中。
                AutoSize = false;
                Size = (Library != null && Index >= 0) ? Library.GetTrueSize(Index) : Size.Empty;
                if (Size.IsEmpty)
                {
                    int maxX = 0, maxY = 0;
                    foreach (MirControl c in Controls)
                    {
                        if (c == null || c.IsDisposed) continue;
                        maxX = Math.Max(maxX, c.Location.X + c.Size.Width);
                        maxY = Math.Max(maxY, c.Location.Y + c.Size.Height);
                    }
                    Size = new Size(Math.Max(maxX, 1), Math.Max(maxY, 1));
                }

                Anchor = EAnchorType.MiddleCenter;
                ApplyAnchor();
                Libraries.LibraryLoaded += Libraries_LibraryLoaded;
            }

            private void Libraries_LibraryLoaded() => ApplyAnchor();

            void RefreshConfirmButton()
            {
                OKButton.Enabled = _accountIDValid && _currentPasswordValid && _newPassword1Valid && _newPassword2Valid;
            }

            private void AccountIDTextBox_TextChanged(object sender, EventArgs e)
            {
                Regex reg = new Regex(@"^[A-Za-z0-9]{" + Globals.MinAccountIDLength + "," + Globals.MaxAccountIDLength + "}$");

                if (string.IsNullOrEmpty(AccountIDTextBox.Text) || !reg.IsMatch(AccountIDTextBox.Text))
                {
                    _accountIDValid = false;
                    AccountIDTextBox.BorderColour = Color.Red;
                }
                else
                {
                    _accountIDValid = true;
                    AccountIDTextBox.BorderColour = Color.Green;
                }
                RefreshConfirmButton();
            }
            private void CurrentPasswordTextBox_TextChanged(object sender, EventArgs e)
            {
                Regex reg = new Regex(@"^[A-Za-z0-9]{" + Globals.MinPasswordLength + "," + Globals.MaxPasswordLength + "}$");

                if (string.IsNullOrEmpty(CurrentPasswordTextBox.Text) || !reg.IsMatch(CurrentPasswordTextBox.Text))
                {
                    _currentPasswordValid = false;
                    CurrentPasswordTextBox.BorderColour = Color.Red;
                }
                else
                {
                    _currentPasswordValid = true;
                    CurrentPasswordTextBox.BorderColour = Color.Green;
                }
                RefreshConfirmButton();
            }
            private void NewPassword1TextBox_TextChanged(object sender, EventArgs e)
            {
                Regex reg = new Regex(@"^[A-Za-z0-9]{" + Globals.MinPasswordLength + "," + Globals.MaxPasswordLength + "}$");

                if (string.IsNullOrEmpty(NewPassword1TextBox.Text) || !reg.IsMatch(NewPassword1TextBox.Text))
                {
                    _newPassword1Valid = false;
                    NewPassword1TextBox.BorderColour = Color.Red;
                }
                else
                {
                    _newPassword1Valid = true;
                    NewPassword1TextBox.BorderColour = Color.Green;
                }
                NewPassword2TextBox_TextChanged(sender, e);
            }
            private void NewPassword2TextBox_TextChanged(object sender, EventArgs e)
            {
                if (NewPassword1TextBox.Text == NewPassword2TextBox.Text)
                {
                    _newPassword2Valid = _newPassword1Valid;
                    NewPassword2TextBox.BorderColour = NewPassword1TextBox.BorderColour;
                }
                else
                {
                    _newPassword2Valid = false;
                    NewPassword2TextBox.BorderColour = Color.Red;
                }
                RefreshConfirmButton();
            }

            private void ChangePassword()
            {
                OKButton.Enabled = false;

                Network.Enqueue(new C.ChangePassword
                {
                    AccountID = AccountIDTextBox.Text,
                    CurrentPassword = CurrentPasswordTextBox.Text,
                    NewPassword = NewPassword1TextBox.Text
                });
            }

            public override void Show()
            {
                if (Visible) return;
                Visible = true;
                AccountIDTextBox.SetFocus();
            }

            protected override void Dispose(bool disposing)
            {
                Libraries.LibraryLoaded -= Libraries_LibraryLoaded;

                base.Dispose(disposing);
            }
        }
    }
}
