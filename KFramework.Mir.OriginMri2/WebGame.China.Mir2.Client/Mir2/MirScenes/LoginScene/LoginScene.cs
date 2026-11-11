using Client.MirControls;
using Client.MirNetwork;
using Client.MirSounds;
using WebGame.Mir2.MonoGame.Client;
using C = ClientPackets;
using S = ServerPackets;

namespace Client.MirScenes
{
    public sealed partial class LoginScene : MirScene
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

            // 默认背景：无 Lib 依赖的深蓝底（“默认图片”背景），不再用黑屏。
            BackColour = Color.FromArgb(16, 20, 34);

            _background = new MirAnimatedControl
            {
                Animated = false,
                Parent = this,
                Size = new Size(Settings.ScreenWidth, Settings.ScreenHeight),
                BackColour = Color.FromArgb(26, 32, 54),
                Border = true,
                BorderColour = Color.FromArgb(70, 84, 130),
            };

            _background.Anchor = EAnchorType.MiddleCenter;
            _background.ApplyAnchor();

            _login = new LoginDialog { Parent = _background, Visible = false };
            _login.AccountButton.Click += (o, e) =>
                {
                    try
                    {
                        _login.Hide();
                        if (_ViewKey != null && !_ViewKey.IsDisposed) _ViewKey.Dispose();
                        _account = new NewAccountDialog { Parent = _background };
                        _account.Show();
                        _account.Disposing += (o1, e1) => _login.Show();
                    }
                    catch (Exception ex)
                    {
                        //KFramework.MonoGame.PrintTool.Log("[Login] 打开注册对话框失败: " + ex);
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
                        //KFramework.MonoGame.PrintTool.Log("[Login] 打开修改密码对话框失败: " + ex);
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
                Parent = this,
                Location = new Point(Settings.ScreenWidth - 116, 10),
                Visible = Settings.UseTestConfig
            };

            _connectBox = new MirMessageBox(GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.AttemptingConnectServer), MirMessageBoxButtons.Cancel);
            _connectBox.CancelButton.Click += (o, e) => CMain.Instance.Close();
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
                _connectBox.Label.Text = GameLanguage.ClientTextMap.GetLocalization((ClientTextKeys.AttemptingConnect), "\n\n", Network.ConnectAttempt);

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
                    ClientVersion((S.ClientVersion)p);
                    break;
                case (short)ServerPacketIds.NewAccount:
                    NewAccount((S.NewAccount)p);
                    break;
                case (short)ServerPacketIds.ChangePassword:
                    ChangePassword((S.ChangePassword)p);
                    break;
                case (short)ServerPacketIds.ChangePasswordBanned:
                    ChangePassword((S.ChangePasswordBanned)p);
                    break;
                case (short)ServerPacketIds.Login:
                    Login((S.Login)p);
                    break;
                case (short)ServerPacketIds.LoginBanned:
                    Login((S.LoginBanned)p);
                    break;
                case (short)ServerPacketIds.LoginSuccess:
                    Login((S.LoginSuccess)p);
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
                _connectBox.CancelButton.Click += (o, e) => CMain.Instance.Close();
                _connectBox.Show();
            }
        }

        private void SendVersion()
        {
            EnsureConnectBox();

            _connectBox.Label.Text = GameLanguage.ClientTextMap.GetLocalization(ClientTextKeys.SendingClientVersion);

            C.ClientVersion p = new C.ClientVersion();
            // 浏览器 WASM 无本地 exe（Application.ExecutablePath 为空串），无法做 exe 哈希校验；
            // 直接发送 16 字节占位哈希（MD5 长度），由服务器在 VersionHash 未强制时放行。
            p.VersionHash = new byte[16];

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

        /// <summary>
        /// 画布尺寸变化后：背景必须重新铺满整屏。
        /// ApplyAnchor 只重算 Location、不改 Size，所以尺寸在这里单独同步；
        /// 其余 UI 交给基类按锚点重算（base.ApplyAnchors）。
        /// </summary>
        public override void ApplyAnchors()
        {
            if (_background != null && !_background.IsDisposed)
                _background.Size = new Size(Settings.ScreenWidth, Settings.ScreenHeight);

            base.ApplyAnchors();
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
                                             p.ExpiryDate, Math.Floor(d.TotalHours), d.Minutes, d.Seconds));
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
            if (_ViewKey != null && !_ViewKey.IsDisposed) _ViewKey.Dispose();
            SoundManager.PlaySound(SoundList.LoginEffect);
            _pendingLogin = p;
            // 原过场动画依赖已删除的 ChrSel 库，不再可用；改为短延时后由 Process() 兜底切换到选人界面。
            _switchToSelectAt = CMain.Time + 400;
        }

        // 切到选人界面：销毁登录场景并创建 SelectScene。重入安全（动画回调与到点兜底只会生效一次）。
        private void SwitchToSelectScene()
        {
            if (_selectCreated) return;
            _selectCreated = true;

            var p = _pendingLogin;
            //KFramework.MonoGame.PrintTool.Log("[Login] 切换到选人界面，角色数: " + (p != null && p.Characters != null ? p.Characters.Count : 0));
            Dispose();
            //KFramework.MonoGame.PrintTool.Log($"[Scene] 切换: {(ActiveScene == null ? "null" : ActiveScene.GetType().Name)} → SelectScene  时刻={DateTime.Now:HH:mm:ss.fff}");
            ActiveScene = new SelectScene(p != null ? p.Characters : null);
        }

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
    }
}
