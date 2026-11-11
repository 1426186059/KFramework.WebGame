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
                // 布局照 FGUI「修改密码_DChangePasswordUI.xml」：底图 639x452 + 4 个输入框 + 确定/取消。
                BackColour = Color.FromArgb(0, 0, 0, 0);
                Border = false;
                LoginSceneLayout.SetPanel(this, LoginSceneLayout.BgChangePassword, LoginSceneLayout.ChangePanel);

                CancelButton = new LoginArtButton(LoginSceneLayout.BtnCancelUp, LoginSceneLayout.BtnCancelDown,
                    LoginSceneLayout.ChangeCancelAt, LoginSceneLayout.ChangeCancelSz, this);
                CancelButton.Click += (o, e) => Dispose();

                OKButton = new LoginArtButton(LoginSceneLayout.BtnChangePwdOkUp, LoginSceneLayout.BtnChangePwdOkDown,
                    LoginSceneLayout.ChangeOkAt, LoginSceneLayout.ChangeOkSz, this) { Enabled = false };
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

                // 面板尺寸由 FGUI 组件固定；4 个输入框坐标集中在 ApplyLayout()。
                ApplyLayout();

                Anchor = EAnchorType.MiddleCenter;
                ApplyAnchor();
                Libraries.LibraryLoaded += Libraries_LibraryLoaded;
            }

            private void Libraries_LibraryLoaded() => ApplyAnchor();

            /// <summary>把 4 个输入框摆到 FGUI 修改密码界面的对应槽位。</summary>
            private void ApplyLayout()
            {
                AccountIDTextBox.Location = LoginSceneLayout.ChangeAccountAt;
                AccountIDTextBox.Size = LoginSceneLayout.ChangeAccountSz;
                CurrentPasswordTextBox.Location = LoginSceneLayout.ChangeCurrentAt;
                CurrentPasswordTextBox.Size = LoginSceneLayout.ChangeCurrentSz;
                NewPassword1TextBox.Location = LoginSceneLayout.ChangeNew1At;
                NewPassword1TextBox.Size = LoginSceneLayout.ChangeNew1Sz;
                NewPassword2TextBox.Location = LoginSceneLayout.ChangeNew2At;
                NewPassword2TextBox.Size = LoginSceneLayout.ChangeNew2Sz;
            }

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
