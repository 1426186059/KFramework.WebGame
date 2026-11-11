using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Client.MirControls;
using Client.MirGraphics;
using Client.MirNetwork;
using Client.MirSounds;
using S = ServerPackets;
using C = ClientPackets;
using WebGame.Mir2.MonoGame.Client;
using Client;
using Client.MirScenes;

namespace Client.MirScenes
{
    public sealed partial class LoginScene
    {
        public sealed class LoginDialog : MirImageControl
        {
            public MirLabel TitleLabel, AccountIDLabel, PassLabel;
            public MirButton AccountButton, CloseButton, OKButton, PassButton, ViewKeyButton;
            public MirTextBox AccountIDTextBox, PasswordTextBox;
            private bool _accountIDValid, _passwordValid;

            public LoginDialog()
            {
                // 无 Lib 依赖：纯色面板 + 文字标签 + 带文字的按钮（“默认图片”默认样式）。
                AutoSize = false;
                Size = new Size(320, 215);
                BackColour = Color.FromArgb(210, 24, 24, 24);
                Border = true;
                BorderColour = Color.Gray;
                PixelDetect = false;

                TitleLabel = new MirLabel
                {
                    Text = "登录",
                    AutoSize = true,
                    ForeColour = Color.Gold,
                    Parent = this,
                };
                TitleLabel.Location = new Point((Size.Width - TitleLabel.Size.Width) / 2, 12);

                AccountIDLabel = new MirLabel
                {
                    Text = "账号",
                    AutoSize = true,
                    ForeColour = Color.White,
                    Parent = this,
                    Location = new Point(40, 82),
                };

                PassLabel = new MirLabel
                {
                    Text = "密码",
                    AutoSize = true,
                    ForeColour = Color.White,
                    Parent = this,
                    Location = new Point(40, 108),
                };

                OKButton = new MirButton
                {
                    Text = "登录",
                    Size = new Size(80, 30),
                    Location = new Point(210, 80),
                    Parent = this,
                    BackColour = Color.FromArgb(60, 90, 150),
                    Border = true,
                    BorderColour = Color.LightBlue,
                };
                OKButton.Click += (o, e) => Login();

                AccountButton = new MirButton
                {
                    Text = "注册",
                    Size = new Size(90, 28),
                    Location = new Point(40, 160),
                    Parent = this,
                    BackColour = Color.FromArgb(60, 90, 150),
                    Border = true,
                };

                PassButton = new MirButton
                {
                    Text = "改密",
                    Size = new Size(90, 28),
                    Location = new Point(150, 160),
                    Parent = this,
                    BackColour = Color.FromArgb(60, 90, 150),
                    Border = true,
                };

                ViewKeyButton = new MirButton
                {
                    Text = "密保",
                    Size = new Size(90, 28),
                    Location = new Point(40, 192),
                    Parent = this,
                    BackColour = Color.FromArgb(60, 90, 150),
                    Border = true,
                };

                CloseButton = new MirButton
                {
                    Text = "关闭",
                    Size = new Size(90, 28),
                    Location = new Point(150, 192),
                    Parent = this,
                    BackColour = Color.FromArgb(90, 50, 50),
                    Border = true,
                };
                CloseButton.Click += (o, e) => CMain.Instance.Close();

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

                // 账号密码记忆加固：LoginDialog 作为 MirScene.ActiveScene 的静态初始化一部分，
                // 可能在 CMain.Init 的 await Settings.Load() 完成之前就构造，此时 Settings.AccountID/Password
                // 还是空，构造期预填会落空。故在首次显示（Draw 触发 OnShown→Shown，必晚于 Settings.Load）
                // 时再带出一次已保存值，保证“记住账号密码”生效。Shown 仅触发一次，不干扰后续手动输入。
                Shown += (s, e) =>
                {
                    if (AccountIDTextBox != null) AccountIDTextBox.Text = Settings.AccountID;
                    if (PasswordTextBox != null) PasswordTextBox.Text = Settings.Password;
                    // 恢复存档后强制按当前文本重算有效性与登录按钮状态：
                    // Text 赋值在 text==value 时会提前返回、不触发 TextChanged，导致 _valid 仍是初始 false、
                    // 按钮停留在禁用态（需增删一字再恢复才生效）。这里直接重算一次，保证开局即可点击登录。
                    AccountIDTextBox_TextChanged(AccountIDTextBox, EventArgs.Empty);
                    PasswordTextBox_TextChanged(PasswordTextBox, EventArgs.Empty);
                };

                // 资源（贴图库）异步加载：构造时图像尺寸可能为 0，导致初始 Location 偏移到屏幕中心附近。
                // 订阅库加载完成事件，库就绪后按真实尺寸重新居中；构造与显示时也兜底计算一次。
                Anchor = EAnchorType.MiddleCenter;
                AnchorPos = new Point(0, 0);
                ApplyAnchor();
                Libraries.LibraryLoaded += Libraries_LibraryLoaded;
            }

            private void Libraries_LibraryLoaded() => ApplyAnchor();

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
                // 登录按钮可用态取决于账号与密码两者，账号变化也需刷新按钮（之前只在密码框变更时刷新）。
                RefreshLoginButton();
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
                if (sender == null || e.KeyCode != Keys.Enter) return;

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
                // 无 Lib 依赖的默认登录界面：始终允许点击登录，凭据合法性交由服务端校验。
                // 否则在本地正则不通过（或没有已保存凭据）时按钮会停在禁用态、点击“没反应”。
                OKButton.Enabled = true;
            }

            private async void Login()
            {
                OKButton.Enabled = false;
                // 记住账号密码：写回 Settings 并持久化到浏览器 IndexedDB（下次启动自动预填）。
                Settings.AccountID = AccountIDTextBox.Text;
                Settings.Password = PasswordTextBox.Text;
                await Settings.Save();
                Network.Enqueue(new C.Login { AccountID = AccountIDTextBox.Text, Password = PasswordTextBox.Text });
            }

            public override void Show()
            {
                if (Visible) return;
                Visible = true;
                AccountIDTextBox.SetFocus();

                //2026-09-20 我现在不让他自动登陆
                //if (Settings.Password != string.Empty && Settings.AccountID != string.Empty)
                //{
                //    Login();
                //}
            }
            public void Clear()
            {
                AccountIDTextBox.Text = string.Empty;
                PasswordTextBox.Text = string.Empty;
            }


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

        }
    }
}
