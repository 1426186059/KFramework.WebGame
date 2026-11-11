using Client.MirControls;
using Client.MirGraphics;
using Client.MirNetwork;
using System.Text.RegularExpressions;
using C = ClientPackets;

namespace Client.MirScenes
{
    public sealed partial class LoginScene
    {
        public sealed class NewAccountDialog : MirImageControl
        {
            public MirButton OKButton, CancelButton;

            public MirTextBox AccountIDTextBox,
                              Password1TextBox,
                              Password2TextBox,
                              EMailTextBox,
                              UserNameTextBox,
                              BirthDateTextBox,
                              QuestionTextBox,
                              AnswerTextBox;

            public MirLabel Description;

            private bool _accountIDValid,
                         _password1Valid,
                         _password2Valid,
                         _eMailValid = true,
                         _userNameValid = true,
                         _birthDateValid = true,
                         _questionValid = true,
                         _answerValid = true;


            public NewAccountDialog()
            {
                // 布局照 FGUI「创建账户_CreateAccountUI.xml」(918x670)：底图 + 8 个输入框 + 确定/取消。
                // 具体坐标集中在 LoginSceneLayout 与 ApplyLayout()，改样式不必翻遍整个构造函数。
                BackColour = Color.FromArgb(0, 0, 0, 0);
                Border = false;
                LoginSceneLayout.SetPanel(this, LoginSceneLayout.BgCreateAccount, LoginSceneLayout.CreatePanel);

                CancelButton = new LoginArtButton(LoginSceneLayout.BtnCancelUp, LoginSceneLayout.BtnCancelDown,
                    LoginSceneLayout.CreateCancelAt, LoginSceneLayout.CreateCancelSz, this);
                CancelButton.Click += (o, e) => Dispose();

                OKButton = new LoginArtButton(LoginSceneLayout.BtnOkUp, LoginSceneLayout.BtnOkDown,
                    LoginSceneLayout.CreateOkAt, LoginSceneLayout.CreateOkSz, this) { Enabled = false };
                OKButton.Click += (o, e) => CreateAccount();

                Password1TextBox = new MirTextBox
                {
                    Border = true,
                    BorderColour = Color.Gray,
                    Location = new Point(226, 129),
                    MaxLength = Globals.MaxPasswordLength,
                    Parent = this,
                    Password = true,
                    Size = new Size(136, 18),
                    TextBox = { MaxLength = Globals.MaxPasswordLength },
                };
                Password1TextBox.TextBox.TextChanged += Password1TextBox_TextChanged;
                Password1TextBox.TextBox.GotFocus += PasswordTextBox_GotFocus;

                Password2TextBox = new MirTextBox
                {
                    Border = true,
                    BorderColour = Color.Gray,
                    Location = new Point(226, 155),
                    MaxLength = Globals.MaxPasswordLength,
                    Parent = this,
                    Password = true,
                    Size = new Size(136, 18),
                    TextBox = { MaxLength = Globals.MaxPasswordLength },
                };
                Password2TextBox.TextBox.TextChanged += Password2TextBox_TextChanged;
                Password2TextBox.TextBox.GotFocus += PasswordTextBox_GotFocus;

                UserNameTextBox = new MirTextBox
                {
                    Border = true,
                    BorderColour = Color.Gray,
                    Location = new Point(226, 189),
                    MaxLength = 20,
                    Parent = this,
                    Size = new Size(136, 18),
                    TextBox = { MaxLength = 20 },
                };
                UserNameTextBox.TextBox.TextChanged += UserNameTextBox_TextChanged;
                UserNameTextBox.TextBox.GotFocus += UserNameTextBox_GotFocus;


                BirthDateTextBox = new MirTextBox
                {
                    Border = true,
                    BorderColour = Color.Gray,
                    Location = new Point(226, 215),
                    MaxLength = 10,
                    Parent = this,
                    Size = new Size(136, 18),
                    TextBox = { MaxLength = 10 },
                };
                BirthDateTextBox.TextBox.TextChanged += BirthDateTextBox_TextChanged;
                BirthDateTextBox.TextBox.GotFocus += BirthDateTextBox_GotFocus;

                QuestionTextBox = new MirTextBox
                {
                    Border = true,
                    BorderColour = Color.Gray,
                    Location = new Point(226, 250),
                    MaxLength = 30,
                    Parent = this,
                    Size = new Size(190, 18),
                    TextBox = { MaxLength = 30 },
                };
                QuestionTextBox.TextBox.TextChanged += QuestionTextBox_TextChanged;
                QuestionTextBox.TextBox.GotFocus += QuestionTextBox_GotFocus;

                AnswerTextBox = new MirTextBox
                {
                    Border = true,
                    BorderColour = Color.Gray,
                    Location = new Point(226, 276),
                    MaxLength = 30,
                    Parent = this,
                    Size = new Size(190, 18),
                    TextBox = { MaxLength = 30 },
                };
                AnswerTextBox.TextBox.TextChanged += AnswerTextBox_TextChanged;
                AnswerTextBox.TextBox.GotFocus += AnswerTextBox_GotFocus;

                EMailTextBox = new MirTextBox
                {
                    Border = true,
                    BorderColour = Color.Gray,
                    Location = new Point(226, 311),
                    MaxLength = 50,
                    Parent = this,
                    Size = new Size(136, 18),
                    TextBox = { MaxLength = 50 },
                };
                EMailTextBox.TextBox.TextChanged += EMailTextBox_TextChanged;
                EMailTextBox.TextBox.GotFocus += EMailTextBox_GotFocus;


                Description = new MirLabel
                {
                    Border = true,
                    BorderColour = Color.Gray,
                    Location = new Point(15, 340),
                    Parent = this,
                    Size = new Size(300, 70),
                    Visible = false
                };

                AccountIDTextBox = new MirTextBox
                {
                    Border = true,
                    BorderColour = Color.Gray,
                    Location = new Point(226, 103),
                    MaxLength = Globals.MaxAccountIDLength,
                    Parent = this,
                    Size = new Size(136, 18),
                };

                AccountIDTextBox.TextBox.MaxLength = Globals.MaxAccountIDLength;
                AccountIDTextBox.TextBox.TextChanged += AccountIDTextBox_TextChanged;
                AccountIDTextBox.TextBox.GotFocus += AccountIDTextBox_GotFocus;

                // 面板尺寸已由 FGUI 组件固定；子控件坐标统一在 ApplyLayout() 里按 displayList 摆放。
                ApplyLayout();

                Anchor = EAnchorType.MiddleCenter;
                ApplyAnchor();
                Libraries.LibraryLoaded += Libraries_LibraryLoaded;
            }

            private void Libraries_LibraryLoaded() => ApplyAnchor();

            /// <summary>把 8 个输入框与字段说明摆到 FGUI 创建账户界面的对应槽位。</summary>
            private void ApplyLayout()
            {
                AccountIDTextBox.Location = LoginSceneLayout.CreateAccountAt;
                AccountIDTextBox.Size = LoginSceneLayout.CreateAccountSz;
                Password1TextBox.Location = LoginSceneLayout.CreatePassword1At;
                Password1TextBox.Size = LoginSceneLayout.CreatePassword1Sz;
                Password2TextBox.Location = LoginSceneLayout.CreatePassword2At;
                Password2TextBox.Size = LoginSceneLayout.CreatePassword2Sz;
                EMailTextBox.Location = LoginSceneLayout.CreateEMailAt;
                EMailTextBox.Size = LoginSceneLayout.CreateEMailSz;
                UserNameTextBox.Location = LoginSceneLayout.CreateUserNameAt;
                UserNameTextBox.Size = LoginSceneLayout.CreateUserNameSz;
                BirthDateTextBox.Location = LoginSceneLayout.CreateBirthDateAt;
                BirthDateTextBox.Size = LoginSceneLayout.CreateBirthDateSz;
                QuestionTextBox.Location = LoginSceneLayout.CreateQuestionAt;
                QuestionTextBox.Size = LoginSceneLayout.CreateQuestionSz;
                AnswerTextBox.Location = LoginSceneLayout.CreateAnswerAt;
                AnswerTextBox.Size = LoginSceneLayout.CreateAnswerSz;

                // richtext FGUIBaseCom14 在右侧，用作字段说明（GotFocus 时才显示）。
                Description.Location = LoginSceneLayout.CreateDescriptionAt;
                Description.Size = LoginSceneLayout.CreateDescriptionSz;
                Description.Border = false;
                Description.ForeColour = Color.White;
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
            private void Password1TextBox_TextChanged(object sender, EventArgs e)
            {
                Regex reg = new Regex(@"^[A-Za-z0-9]{" + Globals.MinPasswordLength + "," + Globals.MaxPasswordLength + "}$");

                if (string.IsNullOrEmpty(Password1TextBox.Text) || !reg.IsMatch(Password1TextBox.Text))
                {
                    _password1Valid = false;
                    Password1TextBox.BorderColour = Color.Red;
                }
                else
                {
                    _password1Valid = true;
                    Password1TextBox.BorderColour = Color.Green;
                }
                Password2TextBox_TextChanged(sender, e);
            }
            private void Password2TextBox_TextChanged(object sender, EventArgs e)
            {
                Regex reg = new Regex(@"^[A-Za-z0-9]{" + Globals.MinPasswordLength + "," + Globals.MaxPasswordLength + "}$");

                if (string.IsNullOrEmpty(Password2TextBox.Text) || !reg.IsMatch(Password2TextBox.Text) ||
                    Password1TextBox.Text != Password2TextBox.Text)
                {
                    _password2Valid = false;
                    Password2TextBox.BorderColour = Color.Red;
                }
                else
                {
                    _password2Valid = true;
                    Password2TextBox.BorderColour = Color.Green;
                }
                RefreshConfirmButton();
            }
            private void EMailTextBox_TextChanged(object sender, EventArgs e)
            {
                Regex reg = new Regex(@"\w+([-+.]\w+)*@\w+([-.]\w+)*\.\w+([-.]\w+)*");
                if (string.IsNullOrEmpty(EMailTextBox.Text))
                {
                    _eMailValid = true;
                    EMailTextBox.BorderColour = Color.Gray;
                }
                else if (!reg.IsMatch(EMailTextBox.Text) || EMailTextBox.Text.Length > 50)
                {
                    _eMailValid = false;
                    EMailTextBox.BorderColour = Color.Red;
                }
                else
                {
                    _eMailValid = true;
                    EMailTextBox.BorderColour = Color.Green;
                }
                RefreshConfirmButton();
            }
            private void UserNameTextBox_TextChanged(object sender, EventArgs e)
            {
                if (string.IsNullOrEmpty(UserNameTextBox.Text))
                {
                    _userNameValid = true;
                    UserNameTextBox.BorderColour = Color.Gray;
                }
                else if (UserNameTextBox.Text.Length > 20)
                {
                    _userNameValid = false;
                    UserNameTextBox.BorderColour = Color.Red;
                }
                else
                {
                    _userNameValid = true;
                    UserNameTextBox.BorderColour = Color.Green;
                }
                RefreshConfirmButton();
            }
            private void BirthDateTextBox_TextChanged(object sender, EventArgs e)
            {
                DateTime dateTime;
                if (string.IsNullOrEmpty(BirthDateTextBox.Text))
                {
                    _birthDateValid = true;
                    BirthDateTextBox.BorderColour = Color.Gray;
                }
                else if (!DateTime.TryParse(BirthDateTextBox.Text, out dateTime) || BirthDateTextBox.Text.Length > 10)
                {
                    _birthDateValid = false;
                    BirthDateTextBox.BorderColour = Color.Red;
                }
                else
                {
                    _birthDateValid = true;
                    BirthDateTextBox.BorderColour = Color.Green;
                }
                RefreshConfirmButton();
            }
            private void QuestionTextBox_TextChanged(object sender, EventArgs e)
            {
                if (string.IsNullOrEmpty(QuestionTextBox.Text))
                {
                    _questionValid = true;
                    QuestionTextBox.BorderColour = Color.Gray;
                }
                else if (QuestionTextBox.Text.Length > 30)
                {
                    _questionValid = false;
                    QuestionTextBox.BorderColour = Color.Red;
                }
                else
                {
                    _questionValid = true;
                    QuestionTextBox.BorderColour = Color.Green;
                }
                RefreshConfirmButton();
            }
            private void AnswerTextBox_TextChanged(object sender, EventArgs e)
            {
                if (string.IsNullOrEmpty(AnswerTextBox.Text))
                {
                    _answerValid = true;
                    AnswerTextBox.BorderColour = Color.Gray;
                }
                else if (AnswerTextBox.Text.Length > 30)
                {
                    _answerValid = false;
                    AnswerTextBox.BorderColour = Color.Red;
                }
                else
                {
                    _answerValid = true;
                    AnswerTextBox.BorderColour = Color.Green;
                }
                RefreshConfirmButton();
            }

            private void AccountIDTextBox_GotFocus(object sender, EventArgs e)
            {
                Description.Visible = true;
                Description.Text = GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.AccountIdDescription), Globals.MinAccountIDLength, Globals.MaxAccountIDLength);
            }
            private void PasswordTextBox_GotFocus(object sender, EventArgs e)
            {
                Description.Visible = true;
                Description.Text = GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.PasswordDescription), Globals.MinPasswordLength, Globals.MaxPasswordLength);
            }
            private void EMailTextBox_GotFocus(object sender, EventArgs e)
            {
                Description.Visible = true;
                Description.Text = GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.EmailAddressDescription);
            }
            private void UserNameTextBox_GotFocus(object sender, EventArgs e)
            {
                Description.Visible = true;
                Description.Text = GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.UserNameDescription);
            }
            private void BirthDateTextBox_GotFocus(object sender, EventArgs e)
            {
                Description.Visible = true;
                Description.Text = GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.BirthDateDescription),
                                  Thread.CurrentThread.CurrentCulture.DateTimeFormat.ShortDatePattern.ToUpper());
            }
            private void QuestionTextBox_GotFocus(object sender, EventArgs e)
            {
                Description.Visible = true;
                Description.Text = GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.SecretQuestionDescription);
            }
            private void AnswerTextBox_GotFocus(object sender, EventArgs e)
            {
                Description.Visible = true;
                Description.Text = GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.SecretAnswerDescription);
            }

            private void RefreshConfirmButton()
            {
                OKButton.Enabled = _accountIDValid && _password1Valid && _password2Valid && _eMailValid &&
                                        _userNameValid && _birthDateValid && _questionValid && _answerValid;
            }
            private void CreateAccount()
            {
                OKButton.Enabled = false;

                Network.Enqueue(new C.NewAccount
                {
                    AccountID = AccountIDTextBox.Text,
                    Password = Password1TextBox.Text,
                    EMailAddress = EMailTextBox.Text,
                    BirthDate = !string.IsNullOrEmpty(BirthDateTextBox.Text)
                                        ? DateTime.Parse(BirthDateTextBox.Text)
                                        : DateTime.MinValue,
                    UserName = UserNameTextBox.Text,
                    SecretQuestion = QuestionTextBox.Text,
                    SecretAnswer = AnswerTextBox.Text,
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

                if (disposing)
                {
                    OKButton = null;
                    CancelButton = null;

                    AccountIDTextBox = null;
                    Password1TextBox = null;
                    Password2TextBox = null;
                    EMailTextBox = null;
                    UserNameTextBox = null;
                    BirthDateTextBox = null;
                    QuestionTextBox = null;
                    AnswerTextBox = null;

                    Description = null;

                }

                base.Dispose(disposing);
            }
        }
    }
}
