using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Client.MirControls;
using Client.MirGraphics;
using Client.MirNetwork;
using Client.MirSounds;
using S = ServerPackets;
using C = ClientPackets;

namespace Client.MirScenes
{
    public sealed class LoginScene : MirScene
    {
        private MirAnimatedControl _background;
        public MirLabel Version;

        private LoginDialog _login;
        private NewAccountDialog _account;
        private ChangePasswordDialog _password;

        private MirMessageBox _connectBox;

        private InputKeyDialog _ViewKey;

        // 登录成功后的过场：动画播完才切到选人界面。浏览器端 MirAnimatedControl.AfterAnimation
        // 存在不触发的风险（一旦不触发就会永久卡在"登录框已销毁、选人界面不出来"的空场景），
        // 因此额外记录切换时刻，由 Process() 到点兜底切换。
        private S.LoginSuccess _pendingLogin;
        private long _switchToSelectAt = -1;
        private bool _selectCreated;

        public MirImageControl TestLabel, ViolenceLabel, MinorLabel, YouthLabel; 

        public LoginScene()
        {
            SoundManager.PlayMusic(SoundList.IntroMusic, true);
            Disposing += (o, e) => SoundManager.StopMusic();

            _background = new MirAnimatedControl
                {
                    Animated = false,
                    AnimationCount = 19,
                    AnimationDelay = 100,
                    Index = 0,
                    Library = Libraries.ChrSel,
                    Loop = false,
                    Parent = this,
                };

            // 关键：背景层必须铺满整屏。它作为登录框（_login 等）的父容器，
            // 命中测试只在“父控件 IsMouseOver 为真”时才会递归到子控件（见 MirControl.OnMouseMove）。
            // 而 _background 的尺寸来自 ChrSel 贴图库（异步加载），库未就绪时 Size 为 0，
            // 导致整层 IsMouseOver 恒为 false，登录框内的按钮/输入框永远收不到鼠标与键盘焦点（表现即为“点击/输入无反应”）。
            // 因此显式铺满屏幕并关闭 AutoSize，使其不依赖贴图库加载即可参与命中测试。
            _background.AutoSize = false;
            _background.Size = new Size(Settings.ScreenWidth, Settings.ScreenHeight);

            _login = new LoginDialog {Parent = _background, Visible = false};
            _login.AccountButton.Click += (o, e) =>
                {
                    try
                    {
                        _login.Hide();
                        if(_ViewKey != null && !_ViewKey.IsDisposed) _ViewKey.Dispose();
                        _account = new NewAccountDialog { Parent = _background };
                        _account.Show();
                        _account.Disposing += (o1, e1) => _login.Show();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("[Login] 打开注册对话框失败: " + ex);
                        _login.Show();
                    }
                };

            _login.PassButton.Click += (o, e) =>
                {
                    try
                    {
                        OpenPasswordChangeDialog(string.Empty, string.Empty);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("[Login] 打开修改密码对话框失败: " + ex);
                    }
                };

            _login.ViewKeyButton.Click += (o, e) =>     //ADD
            {
                if (_ViewKey != null && !_ViewKey.IsDisposed) return;

                _ViewKey = new InputKeyDialog(_login) { Parent = _background };
            };

            Version = new MirLabel
            {
                AutoSize = true,
                BackColour = Color.FromArgb(200, 50, 50, 50),
                Border = true,
                BorderColour = Color.Black,
                Location = new Point(5, Settings.ScreenHeight - 20),
                Parent = _background,
                Text = string.Format("Build: {0}.{1}.{2}", Globals.ProductCodename, Settings.UseTestConfig ? "Debug" : "Release", Application.ProductVersion),
            };

            TestLabel = new MirImageControl
            {
                Index = 79,
                Library = Libraries.Prguse,
                Parent = this,
                Location = new Point(Settings.ScreenWidth - 116, 10),
                Visible = Settings.UseTestConfig
            };

            _connectBox = new MirMessageBox(GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.AttemptingConnectServer), MirMessageBoxButtons.Cancel);
            _connectBox.CancelButton.Click += (o, e) => Program.Form.Close();
            Shown += (sender, args) =>
                {
                    Network.Connect();
                    _connectBox.Show();
                };
        }

        public override void Process()
        {
            EnsureConnectBox();
            if (!Network.Connected && _connectBox.Label != null)
                _connectBox.Label.Text = GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.AttemptingConnect),"\n\n", Network.ConnectAttempt);

            // 兜底：即便 AfterAnimation 未触发，到点也直接切换，避免卡在"登录框消失、选人界面不出来"。
            if (_switchToSelectAt > 0 && CMain.Time >= _switchToSelectAt)
                SwitchToSelectScene();
        }
        public override void ProcessPacket(Packet p)
        {
            switch (p.Index)
            {
                case (short)ServerPacketIds.Connected:
                    Network.Connected = true;
                    SendVersion();
                    break;
                case (short)ServerPacketIds.ClientVersion:
                    ClientVersion((S.ClientVersion) p);
                    break;
                case (short)ServerPacketIds.NewAccount:
                    NewAccount((S.NewAccount) p);
                    break;
                case (short)ServerPacketIds.ChangePassword:
                    ChangePassword((S.ChangePassword) p);
                    break;
                case (short)ServerPacketIds.ChangePasswordBanned:
                    ChangePassword((S.ChangePasswordBanned) p);
                    break;
                case (short)ServerPacketIds.Login:
                    Login((S.Login) p);
                    break;
                case (short)ServerPacketIds.LoginBanned:
                    Login((S.LoginBanned) p);
                    break;
                case (short)ServerPacketIds.LoginSuccess:
                    Login((S.LoginSuccess) p);
                    break;
                default:
                    base.ProcessPacket(p);
                    break;
            }
        }

        private void EnsureConnectBox()
        {
            // 仅在“尚未连上”时维护连接框：
            // 1) 连接成功分支（ClientVersion case 1）会 _connectBox.Dispose() 并 _login.Show()，
            //    此时 Network.Connected 已为 true，必须跳过重建，否则下一帧 Process() 又会把连接框建出来盖住登录框。
            // 2) WS 断开（Network.Connected=false）后才会重建，用于网络抖动重连提示。
            if (!Network.Connected && (_connectBox == null || _connectBox.IsDisposed))
            {
                _connectBox = new MirMessageBox(GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.AttemptingConnectServer), MirMessageBoxButtons.Cancel);
                _connectBox.CancelButton.Click += (o, e) => Program.Form.Close();
                _connectBox.Show();
            }
        }

        private  void SendVersion()
        {
            EnsureConnectBox();

            _connectBox.Label.Text = GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.SendingClientVersion);

            C.ClientVersion p = new C.ClientVersion();
            try
            {
                byte[] sum;
                // 浏览器 WASM 不支持 MD5.Create()（SubtleCrypto 不含 MD5），改用自包含托管实现。
                using (FileStream stream = File.OpenRead(Application.ExecutablePath))
                    sum = ManagedMD5.ComputeHash(stream);

                p.VersionHash = sum;
            }
            catch
            {
                // 浏览器 WASM 中 Application.ExecutablePath 为空串（shim 返回空），无法做 exe 哈希校验；
                // 发送 16 字节占位哈希（MD5 长度），由服务器在 VersionHash 未强制时放行。
                p.VersionHash = new byte[16];
            }

            Network.Enqueue(p);
        }
        private void ClientVersion(S.ClientVersion p)
        {
            switch (p.Result)
            {
                case 0:
                    MirMessageBox.Show(GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.WrongVersionPleaseUpdateGame), true);

                    Network.Disconnect();
                    break;
                case 1:
                    _connectBox.Dispose();
                    _login.Show();
                    break;
            }
        }

        private void OpenPasswordChangeDialog(string autoFillID, string autoFillPassword)
        {
            _login.Hide();
            if (_ViewKey != null && !_ViewKey.IsDisposed) _ViewKey.Dispose();
            _password = new ChangePasswordDialog { Parent = _background };
            _password.Show();
            _password.AccountIDTextBox.Text = autoFillID;
            _password.CurrentPasswordTextBox.Text = autoFillPassword;
            _password.Disposing += (o1, e1) => _login.Show();
        }
        private void NewAccount(S.NewAccount p)
        {
            _account.OKButton.Enabled = true;
            switch (p.Result)
            {
                case 0:
                    MirMessageBox.Show(GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.AccountCreationDisabled));
                    _account.Dispose();
                    break;
                case 1:
                    MirMessageBox.Show(GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.AccountIdNotAcceptable));
                    _account.AccountIDTextBox.SetFocus();
                    break;
                case 2:
                    MirMessageBox.Show(GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.PasswordNotAcceptable));
                    _account.Password1TextBox.SetFocus();
                    break;
                case 3:
                    MirMessageBox.Show(GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.EmailAddressNotAcceptable));
                    _account.EMailTextBox.SetFocus();
                    break;
                case 4:
                    MirMessageBox.Show(GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.UserNameNotAcceptable));
                    _account.UserNameTextBox.SetFocus();
                    break;
                case 5:
                    MirMessageBox.Show(GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.SecretQuestionNotAcceptable));
                    _account.QuestionTextBox.SetFocus();
                    break;
                case 6:
                    MirMessageBox.Show(GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.SecretAnswerNotAcceptable));
                    _account.AnswerTextBox.SetFocus();
                    break;
                case 7:
                    MirMessageBox.Show(GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.AccountIdAlreadyExists));
                    _account.AccountIDTextBox.Text = string.Empty;
                    _account.AccountIDTextBox.SetFocus();
                    break;
                case 8:
                    MirMessageBox.Show(GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.AccountCreatedSuccessfully));
                    _account.Dispose();
                    break;
            }
        }
        private void ChangePassword(S.ChangePassword p)
        {
            _password.OKButton.Enabled = true;

            switch (p.Result)
            {
                case 0:
                    MirMessageBox.Show(GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.PasswordChangingDisabled));
                    _password.Dispose();
                    break;
                case 1:
                    MirMessageBox.Show(GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.AccountIdNotAcceptable));
                    _password.AccountIDTextBox.SetFocus();
                    break;
                case 2:
                    MirMessageBox.Show(GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.CurrentPasswordNotAcceptable));
                    _password.CurrentPasswordTextBox.SetFocus();
                    break;
                case 3:
                    MirMessageBox.Show(GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.NewPasswordNotAcceptable));
                    _password.NewPassword1TextBox.SetFocus();
                    break;
                case 4:
                    MirMessageBox.Show(GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.NoAccountID));
                    _password.AccountIDTextBox.SetFocus();
                    break;
                case 5:
                    MirMessageBox.Show(GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.IncorrectPasswordAccountID));
                    _password.CurrentPasswordTextBox.SetFocus();
                    _password.CurrentPasswordTextBox.Text = string.Empty;
                    break;
                case 6:
                    MirMessageBox.Show(GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.PasswordChangedSuccessfully));
                    _password.Dispose();
                    break;
            }
        }
        private void ChangePassword(S.ChangePasswordBanned p)
        {
            _password.Dispose();

            TimeSpan d = p.ExpiryDate - CMain.Now;
            MirMessageBox.Show(GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.AccountBannedReasonDuration), p.Reason,
                                             p.ExpiryDate, Math.Floor(d.TotalHours), d.Minutes, d.Seconds ));
        }
        private void Login(S.Login p)
        {
            _login.OKButton.Enabled = true;
            switch (p.Result)
            {
                case 0:
                    MirMessageBox.Show(GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.LoginDisabled));
                    _login.Clear();
                    break;
                case 1:
                    MirMessageBox.Show(GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.AccountIdNotAcceptable));
                    _login.AccountIDTextBox.SetFocus();
                    break;
                case 2:
                    MirMessageBox.Show(GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.PasswordNotAcceptable));
                    _login.PasswordTextBox.SetFocus();
                    break;
                case 3:
                    MirMessageBox.Show(GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.NoAccountID));
                    _login.PasswordTextBox.SetFocus();
                    break;
                case 4:
                    MirMessageBox.Show(GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.IncorrectPasswordAccountID));
                    _login.PasswordTextBox.Text = string.Empty;
                    _login.PasswordTextBox.SetFocus();
                    break;
                case 5:
                    MirMessageBox.Show(GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.AccountPasswordMustChangeBeforeLogin));                    
                    OpenPasswordChangeDialog(_login.AccountIDTextBox.Text, _login.PasswordTextBox.Text);
                    _login.PasswordTextBox.Text = string.Empty;
                    break;
            }
        }
        private void Login(S.LoginBanned p)
        {
            _login.OKButton.Enabled = true;

            TimeSpan d = p.ExpiryDate - CMain.Now;
            MirMessageBox.Show(GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.AccountBannedReasonDuration), p.Reason,
                                             p.ExpiryDate, Math.Floor(d.TotalHours), d.Minutes, d.Seconds));
        }
        private void Login(S.LoginSuccess p)
        {
            Enabled = false;
            _login.Dispose();
            if(_ViewKey != null && !_ViewKey.IsDisposed) _ViewKey.Dispose();

            SoundManager.PlaySound(SoundList.LoginEffect);
            _background.Animated = true;

            _pendingLogin = p;
            _switchToSelectAt = CMain.Time + (_background.AnimationCount + 2) * Math.Max(1, _background.AnimationDelay);

            _background.AfterAnimation += (o, e) => SwitchToSelectScene();
        }

        // 切到选人界面：销毁登录场景并创建 SelectScene。重入安全（动画回调与到点兜底只会生效一次）。
        private void SwitchToSelectScene()
        {
            if (_selectCreated) return;
            _selectCreated = true;

            var p = _pendingLogin;
            Console.WriteLine("[Login] 切换到选人界面，角色数: " + (p != null && p.Characters != null ? p.Characters.Count : 0));
            Dispose();
            ActiveScene = new SelectScene(p != null ? p.Characters : null);
        }

        public sealed class LoginDialog : MirImageControl
        {
            public MirImageControl TitleLabel, AccountIDLabel, PassLabel;
            public MirButton AccountButton, CloseButton, OKButton, PassButton, ViewKeyButton;
            public MirTextBox AccountIDTextBox, PasswordTextBox;
            private bool _accountIDValid, _passwordValid;

            public LoginDialog()
            {
                Index = 1084;
                Library = Libraries.Prguse;
                PixelDetect = false;
                Size = new Size(328, 220);

                TitleLabel = new MirImageControl
                    {
                        Index = 30,
                        Library = Libraries.Title,
                        Parent = this,
                    };
                TitleLabel.Location = new Point((Size.Width - TitleLabel.Size.Width)/2, 12);

                AccountIDLabel = new MirImageControl
                    {
                        Index = 31,
                        Library = Libraries.Title,
                        Parent = this,
                        Location = new Point(52, 83),
                    };

                PassLabel = new MirImageControl
                    {
                        Index = 32,
                        Library = Libraries.Title,
                        Parent = this,
                        Location = new Point(43, 105)
                    };

                OKButton = new MirButton
                    {
                        Enabled = false,
                        Size = new Size(42,42),
                        HoverIndex = 321,
                        Index = 320,
                        Library = Libraries.Title,
                        Location = new Point(227, 81),
                        Parent = this,
                        PressedIndex = 322
                    };
                OKButton.Click += (o, e) => Login();

                AccountButton = new MirButton
                    {
                        HoverIndex = 324,
                        Index = 323,
                        Library = Libraries.Title,
                        Location = new Point(60, 163),
                        Parent = this,
                        PressedIndex = 325,
                    };

                PassButton = new MirButton
                    {
                        HoverIndex = 327,
                        Index = 326,
                        Library = Libraries.Title,
                        Location = new Point(166, 163),
                        Parent = this,
                        PressedIndex = 328,
                    };

                ViewKeyButton = new MirButton
                {
                    HoverIndex = 333,
                    Index = 332,
                    Library = Libraries.Title,
                    Location = new Point(60, 189),
                    Parent = this,
                    PressedIndex = 334,
                };

                CloseButton = new MirButton
                    {
                        HoverIndex = 330,
                        Index = 329,
                        Library = Libraries.Title,
                        Location = new Point(166, 189),
                        Parent = this,
                        PressedIndex = 331,
                    };
                CloseButton.Click += (o, e) => Program.Form.Close();

                PasswordTextBox = new MirTextBox
                {
                    Location = new Point(85, 108),
                    Parent = this,
                    Password = true,
                    Size = new Size(136, 15),
                    MaxLength = Globals.MaxPasswordLength
                };

                PasswordTextBox.TextBox.TextChanged += PasswordTextBox_TextChanged;
                PasswordTextBox.TextBox.KeyPress += TextBox_KeyPress;
                PasswordTextBox.Text = Settings.Password;

                AccountIDTextBox = new MirTextBox
                {
                    Location = new Point(85, 85),
                    Parent = this,
                    Size = new Size(136, 15),
                    MaxLength = Globals.MaxAccountIDLength
                };

                AccountIDTextBox.TextBox.TextChanged += AccountIDTextBox_TextChanged;
                AccountIDTextBox.TextBox.KeyPress += TextBox_KeyPress;
                AccountIDTextBox.Text = Settings.AccountID;

                // 资源（贴图库）异步加载：构造时图像尺寸可能为 0，导致初始 Location 偏移到屏幕中心附近。
                // 订阅库加载完成事件，库就绪后按真实尺寸重新居中；构造与显示时也兜底计算一次。
                Recenter();
                Libraries.LibraryLoaded += Libraries_LibraryLoaded;
            }

            private void Libraries_LibraryLoaded() => Recenter();

            private void Recenter()
            {
                if (IsDisposed) return;
                Location = new Point((Settings.ScreenWidth - Size.Width) / 2, (Settings.ScreenHeight - Size.Height) / 2);
            }

            private void AccountIDTextBox_TextChanged(object sender, EventArgs e)
            {
                Regex reg =
                    new Regex(@"^[A-Za-z0-9]{" + Globals.MinAccountIDLength + "," + Globals.MaxAccountIDLength + "}$");

                if (string.IsNullOrEmpty(AccountIDTextBox.Text) || !reg.IsMatch(AccountIDTextBox.TextBox.Text))
                {
                    _accountIDValid = false;
                    AccountIDTextBox.Border = !string.IsNullOrEmpty(AccountIDTextBox.Text);
                    AccountIDTextBox.BorderColour = Color.Red;
                }
                else
                {
                    _accountIDValid = true;
                    AccountIDTextBox.Border = true;
                    AccountIDTextBox.BorderColour = Color.Green;
                }
            }
            private void PasswordTextBox_TextChanged(object sender, EventArgs e)
            {
                Regex reg =
                    new Regex(@"^[A-Za-z0-9]{" + Globals.MinPasswordLength + "," + Globals.MaxPasswordLength + "}$");

                if (string.IsNullOrEmpty(PasswordTextBox.TextBox.Text) || !reg.IsMatch(PasswordTextBox.TextBox.Text))
                {
                    _passwordValid = false;
                    PasswordTextBox.Border = !string.IsNullOrEmpty(PasswordTextBox.TextBox.Text);
                    PasswordTextBox.BorderColour = Color.Red;
                }
                else
                {
                    _passwordValid = true;
                    PasswordTextBox.Border = true;
                    PasswordTextBox.BorderColour = Color.Green;
                }

                RefreshLoginButton();
            }
            public void TextBox_KeyPress(object sender, KeyPressEventArgs e)
            {
                if (sender == null || e.KeyChar != (char) Keys.Enter) return;

                e.Handled = true;

                if (!_accountIDValid)
                {
                    AccountIDTextBox.SetFocus();
                    return;
                }
                if (!_passwordValid)
                {
                    PasswordTextBox.SetFocus();
                    return;
                }

                if (OKButton.Enabled)
                    OKButton.InvokeMouseClick(null);
            }
            private void RefreshLoginButton()
            {
                OKButton.Enabled = _accountIDValid && _passwordValid;
            }
            
            private void Login()
            {
                OKButton.Enabled = false;
                Network.Enqueue(new C.Login {AccountID = AccountIDTextBox.Text, Password = PasswordTextBox.Text});
            }

            public override void Show()
            {
                if (Visible) return;
                Visible = true;
                AccountIDTextBox.SetFocus();

                if (Settings.Password != string.Empty && Settings.AccountID != string.Empty)
                {
                    Login();
                }
            }
            public void Clear()
            {
                AccountIDTextBox.Text = string.Empty;
                PasswordTextBox.Text = string.Empty;
            }

            #region Disposable

            protected override void Dispose(bool disposing)
            {
                Libraries.LibraryLoaded -= Libraries_LibraryLoaded;

                if (disposing)
                {
                    TitleLabel = null;
                    AccountIDLabel = null;
                    PassLabel = null;
                    AccountButton = null;
                    CloseButton = null;
                    OKButton = null;
                    PassButton = null;
                    AccountIDTextBox = null;
                    PasswordTextBox = null;

                }

                base.Dispose(disposing);
            }

            #endregion
        }

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

                Index = 1080;
                Library = Libraries.Prguse;
                Visible = true;

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
                    KeyPressEventArgs arg = new KeyPressEventArgs((char)Keys.Enter);

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

                // 浏览器端资源异步加载，MirImageControl 在 AutoSize 下依赖的 GetTrueSize 可能返回 0，
                // 导致 Recenter 把对话框定位到屏幕正中、子控件全被甩到屏幕外（点击“输入密保”无反应）。
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

                Recenter();
                Libraries.LibraryLoaded += Libraries_LibraryLoaded;
            }

            private void Libraries_LibraryLoaded() => Recenter();

            private void Recenter()
            {
                if (IsDisposed) return;
                Location = new Point((Settings.ScreenWidth - Size.Width) / 2 + 285, (Settings.ScreenHeight - Size.Height) / 2 + 150);
            }

            private void DisposeKeys()
            {
                foreach(MirButton button in _buttons)
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

            #region Disposable
            protected override void Dispose(bool disposing)
            {
                Libraries.LibraryLoaded -= Libraries_LibraryLoaded;

                base.Dispose(disposing);

                DisposeKeys();
            }
            #endregion
        }

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

                Recenter();
                Libraries.LibraryLoaded += Libraries_LibraryLoaded;
            }

            private void Libraries_LibraryLoaded() => Recenter();

            private void Recenter()
            {
                if (IsDisposed) return;
                Location = new Point((Settings.ScreenWidth - Size.Width) / 2, (Settings.ScreenHeight - Size.Height) / 2);
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

            #region Disposable
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
            #endregion
        }

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

                Recenter();
                Libraries.LibraryLoaded += Libraries_LibraryLoaded;
            }

            private void Libraries_LibraryLoaded() => Recenter();

            private void Recenter()
            {
                if (IsDisposed) return;
                Location = new Point((Settings.ScreenWidth - Size.Width) / 2, (Settings.ScreenHeight - Size.Height) / 2);
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

            #region Disposable
            protected override void Dispose(bool disposing)
            {
                Libraries.LibraryLoaded -= Libraries_LibraryLoaded;

                base.Dispose(disposing);
            }
            #endregion
        }

        #region Disposable
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _background = null;
                Version = null;

                _login = null;
                _account = null;
                _password = null;

                _connectBox = null;
            }

            base.Dispose(disposing);
        }
        #endregion
    }
}
