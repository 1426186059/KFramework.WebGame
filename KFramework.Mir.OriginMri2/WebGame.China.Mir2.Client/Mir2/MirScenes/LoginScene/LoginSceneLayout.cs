using Client.MirControls;
using Client.MirGraphics;
// 只给 Texture2D 起别名：整体 using KFramework.MonoGame 会让 Size/Point/Color 与 MirEngine 冲突。
using Texture2D = KFramework.MonoGame.Texture2D;

namespace Client.MirScenes
{
    /// <summary>
    /// 登录界面的美术装配与布局常量。
    ///
    /// 这里的坐标/尺寸全部照抄 FGUI 工程（UI微变/assets/UILoading/登录/*.xml 与
    /// assets/UILoadingRes/LoginUI/组件/*.xml 的 component size / displayList xy），
    /// 只是把 XML 里的元素换成我们自己的 MirImageControl / MirButton / MirTextBox。
    /// 不解析 FGUI 包、不引入 FairyGUI 运行时、不加载 package.xml：
    /// 那些 PNG 就是普通图片，放在 Mir2Res/MyRes/Login/，由本地 :5080 资源服务器提供，
    /// 用框架已有的 BrowserResource.Content 加载后赋给 MirImageControl.Texture。
    ///
    /// 想改布局：只改下面这组常量即可，无需碰各个对话框的构造代码。
    /// </summary>
    internal static class LoginSceneLayout
    {
        // 相对 BrowserResource 根（RemoteWebSetting.LibBaseUrl = http://127.0.0.1:5080/）。
        public const string ArtRoot = "MyRes/Login/";

        // ===== 底图（文件名保留 FGUI 原名，便于和 package.xml 的 src id 对照）=====
        public const string BgFullScreen = "Login_00012.png";   // DLoginBg.xml          1280x720
        public const string BgLogin = "Login_00368.png";       // 登录_LoginUI.xml     460x400
        public const string BgCreateAccount = "Login_00047.png"; // 创建账户_CreateAccountUI.xml 918x670
        public const string BgChangePassword = "Login_00048.png"; // 修改密码_DChangePasswordUI.xml 639x452

        // ===== 按钮贴图的 up / down 两态（FGUI Button controller: 0,2=up/over；1,3=down）=====
        public const string BtnCloseUp = "Login_00000.png";
        public const string BtnCloseDown = "Login_00001.png";
        public const string BtnLoginUp = "Login_00373.png";
        public const string BtnLoginDown = "Login_00374.png";
        public const string BtnNewUserUp = "Login_00371.png";
        public const string BtnNewUserDown = "Login_00372.png";
        public const string BtnChangePwdUp = "Login_00369.png";
        public const string BtnChangePwdDown = "Login_00370.png";
        public const string BtnOkUp = "Login_00034.png";        // DBtnOK / 注册确认
        public const string BtnOkDown = "Login_00035.png";
        public const string BtnCancelUp = "Login_00375.png";    // DBtnFaill / DBtnCPFaill
        public const string BtnCancelDown = "Login_00376.png";
        public const string BtnChangePwdOkUp = "Login_00386.png";  // DBtnCPOK
        public const string BtnChangePwdOkDown = "Login_00387.png";

        // ===== 登录框 登录_LoginUI.xml：component size="460,400" =====
        public static readonly Size LoginPanel = new Size(460, 400);
        public static readonly Point LoginAccountAt = new Point(156, 142);
        public static readonly Size LoginAccountSz = new Size(211, 32);
        public static readonly Point LoginPasswordAt = new Point(156, 199);
        public static readonly Size LoginPasswordSz = new Size(211, 26);
        public static readonly Point LoginOkAt = new Point(264, 238);
        public static readonly Size LoginOkSz = new Size(130, 60);
        public static readonly Point LoginNewUserAt = new Point(50, 303);
        public static readonly Size LoginNewUserSz = new Size(162, 60);
        public static readonly Point LoginChangePwdAt = new Point(210, 303);
        public static readonly Size LoginChangePwdSz = new Size(192, 60);
        public static readonly Point LoginCloseAt = new Point(379, 55);
        public static readonly Size LoginCloseSz = new Size(29, 45);
        // FGUI 登录框没有「密保」按钮，我们保留自己的软键盘入口，放在账号框右侧的空位。
        public static readonly Point LoginViewKeyAt = new Point(376, 142);
        public static readonly Size LoginViewKeySz = new Size(64, 32);

        // ===== 注册框 创建账户_CreateAccountUI.xml：component size="918,670" =====
        public static readonly Size CreatePanel = new Size(918, 670);
        public static readonly Point CreateAccountAt = new Point(191, 123);
        public static readonly Size CreateAccountSz = new Size(183, 26);
        public static readonly Point CreatePassword1At = new Point(191, 178);
        public static readonly Size CreatePassword1Sz = new Size(183, 26);
        public static readonly Point CreatePassword2At = new Point(190, 229);
        public static readonly Size CreatePassword2Sz = new Size(187, 26);
        public static readonly Point CreateEMailAt = new Point(191, 302);
        public static readonly Size CreateEMailSz = new Size(186, 29);
        public static readonly Point CreateUserNameAt = new Point(191, 358);
        public static readonly Size CreateUserNameSz = new Size(244, 31);
        public static readonly Point CreateBirthDateAt = new Point(192, 414);
        public static readonly Size CreateBirthDateSz = new Size(243, 31);
        public static readonly Point CreateQuestionAt = new Point(192, 467);
        public static readonly Size CreateQuestionSz = new Size(243, 31);
        public static readonly Point CreateAnswerAt = new Point(192, 523);
        public static readonly Size CreateAnswerSz = new Size(243, 31);
        // richtext FGUIBaseCom14 在右侧留给字段说明
        public static readonly Point CreateDescriptionAt = new Point(514, 119);
        public static readonly Size CreateDescriptionSz = new Size(310, 436);
        public static readonly Point CreateOkAt = new Point(164, 584);
        public static readonly Size CreateOkSz = new Size(122, 49);
        public static readonly Point CreateCancelAt = new Point(665, 579);
        public static readonly Size CreateCancelSz = new Size(159, 60);

        // ===== 改密框 修改密码_DChangePasswordUI.xml：component size="614,432"，底图 639x452 =====
        public static readonly Size ChangePanel = new Size(639, 452);
        public static readonly Point ChangeAccountAt = new Point(281, 123);
        public static readonly Size ChangeAccountSz = new Size(257, 29);
        public static readonly Point ChangeCurrentAt = new Point(281, 178);
        public static readonly Size ChangeCurrentSz = new Size(257, 27);
        public static readonly Point ChangeNew1At = new Point(281, 231);
        public static readonly Size ChangeNew1Sz = new Size(257, 27);
        public static readonly Point ChangeNew2At = new Point(281, 283);
        public static readonly Size ChangeNew2Sz = new Size(257, 27);
        public static readonly Point ChangeOkAt = new Point(158, 336);
        public static readonly Size ChangeOkSz = new Size(130, 60);
        public static readonly Point ChangeCancelAt = new Point(326, 336);
        public static readonly Size ChangeCancelSz = new Size(159, 60);

        /// <summary>用框架现有的本地资源通道取一张 PNG（不经过 CDN，也不经过任何 FGUI 解析）。</summary>
        public static Task<Texture2D> LoadArtAsync(string png)
        {
            // MirScene.ActiveScene 的静态初始化会先构造一个 LoginScene（随后 CMain.Init 再构造一次），
            // 那次可能早于 BrowserResource.Configure / GraphicsDevice 就绪，故做兜底，缺一就静默跳过。
            if (DXManager.GDevice == null) return Task.FromResult<Texture2D>(null);
            var content = BrowserResource.Content
                          ?? new KFramework.MonoGame.ContentManager("", RemoteWebSetting.LibBaseUrl);
            return content.LoadTexture2DAsync(ArtRoot + png, DXManager.GDevice, BrowserResource.mCacheInstance);
        }

        /// <summary>把 FGUI 底图贴到面板上：固定尺寸、异步到位后重绘。</summary>
        public static void SetPanel(MirImageControl control, string png, Size size)
        {
            if (control == null) return;
            control.AutoSize = false;
            control.Size = size;
            control.DrawImage = true;
            _ = ApplyPanelAsync(control, png);
        }

        private static async Task ApplyPanelAsync(MirImageControl control, string png)
        {
            try
            {
                Texture2D tex = await LoadArtAsync(png);
                if (tex == null || control.IsDisposed) return;
                control.Texture = tex;
                control.Redraw();
                control.ApplyAnchor();
            }
            catch (Exception ex)
            {
                KFramework.MonoGame.PrintTool.Log("[LoginUI] 底图加载失败 " + png + ": " + ex.Message);
            }
        }
    }

    /// <summary>
    /// 登录界面用的图片按钮：直接把 FGUI 的 up/down 两张 PNG 贴到 MirButton.Texture，
    /// 按钮本身仍是 MirButton（点击、音效、Enabled、Click 事件全部沿用原框架）。
    /// </summary>
    internal sealed class LoginArtButton : MirButton
    {
        private Texture2D _up, _down;

        public LoginArtButton(string upPng, string downPng, Point location, Size size, MirControl parent)
        {
            AutoSize = false;
            Size = size;
            Location = location;
            Parent = parent;
            Text = string.Empty;   // 图片自带文字，避免再叠一层 Label
            _ = LoadAsync(upPng, downPng);
        }

        private async Task LoadAsync(string upPng, string downPng)
        {
            try
            {
                Texture2D up = await LoginSceneLayout.LoadArtAsync(upPng);
                Texture2D down = await LoginSceneLayout.LoadArtAsync(downPng);
                if (IsDisposed) return;
                _up = up;
                _down = down;
                Texture = _up;
                Redraw();
            }
            catch (Exception ex)
            {
                KFramework.MonoGame.PrintTool.Log("[LoginUI] 按钮贴图加载失败 " + upPng + ": " + ex.Message);
            }
        }

        protected override void Activate()
        {
            if (_down != null) Texture = _down;
            base.Activate();
        }

        protected override void Deactivate()
        {
            if (_up != null) Texture = _up;
            base.Deactivate();
        }

        protected override void OnEnabledChanged()
        {
            ForeColour = Enabled ? Color.White : Color.Gray;
            base.OnEnabledChanged();
        }
    }
}
