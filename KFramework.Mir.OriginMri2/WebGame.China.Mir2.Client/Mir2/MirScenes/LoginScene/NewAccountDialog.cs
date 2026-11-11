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
                Index = 63;
                Library = Libraries.Prguse;
                Size = new Size();

                CancelButton = new MirButton
                {
                    HoverIndex = 204,
                    Index = 203,
                    Library = Libraries.Title,
                    Location = new Point(409, 425),
                    Parent = this,
                    PressedIndex = 205
                };
                CancelButton.Click += (o, e) => Dispose();

                OKButton = new MirButton
                {
                    Enabled = false,
                    HoverIndex = 201,
                    Index = 200,
                    Library = Libraries.Title,
                    Location = new Point(135, 425),
                    Parent = this,
                    PressedIndex = 202,
                };
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
