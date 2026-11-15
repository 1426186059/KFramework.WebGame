using Client.MirGraphics;
using KFramework.MonoGame;
using KFramework.MonoGameExtend;
using Color = MirEngine.Color;
using GraphicsDevice = KFramework.MonoGame.GraphicsDevice;
using Point = MirEngine.Point;
using Size = MirEngine.Size;

namespace Client.MirControls
{
    /// <summary>
    /// 全屏加载蒙层（非独立场景）：GameScene 必须立刻 Active 以接收网络包，故进入游戏时由 GameScene 触发
    /// CMain.ShowLoadingDialog() 创建本蒙层；其下 GameScene 正常收包/跑逻辑。
    /// 本蒙层【不挂在任何场景控件树下】，而是交由 CMain 在场景绘制之后单独 Draw（见 RenderEnvironment），
    /// 因此层级最高、稳定盖在所有 UI / Debug / Hint 浮层之上（避免被场景内对话框压住）。
    /// 蒙层美术直接用原始传奇 H5 资源：bg_loading2n.jpg（全屏背景）+ preload-sheet（进度条/Logo 图集），
    /// 这两个资源在本蒙层创建时按需加载（用到才加载），加载完即显示；并借这段时间预下载主界面 UI 图集
    /// y_homeUI-sheet（正式 RES 资源，进主界面立即要用，先缓存避免卡顿）。不预加载其余主场景游戏资源。
    /// </summary>
    public class LoadingDialog : MirControl
    {
        // 原版 CDN 直链资源（不在 manifest/RES 表内，按相对 CDN 根的路径 + 缓存参数加载）
        private const string BgImageName = "img/bg_loading2n.jpg?qufu_version=229";                          // 加载背景图
        private const string SheetJsonPath = "qufu_resource1/assets/sheet/preload-sheet.json?qufu_version=229"; // 进度条图集 JSON（preload-sheet）
        private const string BarTrackFrame = "qufu_ProgressBg4";   // preload-sheet 里的进度条底槽帧（AtlasNameKey 让组件内部下载/管理图集）
        private const string BarFillFrame = "qufu_ProgressBg5";   // preload-sheet 里的进度条填充帧
        private const string HomeUiSheetKey = "y_homeUI-sheet";   // 主界面 UI 图集（正式 RES 资源，预加载进缓存，进主界面不卡）

        // 背景图用 MirImageControl（ImageKey 经 manifest 解析，等价于原 GetImageTextureAsync）；
        // 进度条用现成的 MirProgressBar 组件（自身=轨道、内嵌 Fill=填充，进度由 Value/Maximum 驱动）。
        private readonly MirImageControl _bgImage;
        private readonly MirProgressBar _bar;
        private readonly MirLabel _statusLabel;
        private readonly MirLabel _percentLabel;

        // 进度条宽度随屏宽自适应（封顶 520，下限 320），竖直位置在屏幕偏下 1/5 处、居中。
        private readonly int _trackWidth = System.Math.Min(520, System.Math.Max(320, Settings.ScreenWidth * 3 / 5));
        private readonly int _barHeight = 16;
        private readonly int _barCenterOffset = (int)(Settings.ScreenHeight * 0.18);  // 距屏幕中心向下偏移（进度条所在行）

        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        private int _total = 1;
        private int _loaded;
        private bool _finished;
        private string _status = "正在准备…";
        private bool _dirty;

        public bool Done { get; private set; }

        public LoadingDialog()
        {
            Location = new Point(0, 0);
            Size = new Size(Settings.ScreenWidth, Settings.ScreenHeight);
            BackColour = Color.Black;
            DrawControlTexture = true;

            // 背景图：用 MirImageControl（ImageKey 经 manifest 解析加载，组件自带异步下载/解码/绘制）
            _bgImage = new MirImageControl
            {
                Parent = this,
                AutoSize = false,
                Size = new Size(Settings.ScreenWidth, Settings.ScreenHeight),  // 占位，Tick 里按 cover 重算
                DrawControlTexture = false,   // 直接实时绘制 Texture，不烤自身 ControlTexture（避免黑底方块）
            };
            _bgImage.ImageKey = BgImageName;   // 交由 MirImageControl 异步下载/解码/绘制（完成事件推进进度）

            // 进度条：用现成的 MirProgressBar（自身=轨道、内嵌 Fill=填充，进度由 Value 驱动）。
            // 轨道/填充均取自 preload-sheet 图集（AtlasNameKey 让组件内部下载/管理图集并取帧）。
            _bar = new MirProgressBar
            {
                Parent = this,
                Anchor = EAnchorType.MiddleCenter,
                AnchorPos = new Point(0, _barCenterOffset),
                AutoSize = false,
                Size = new Size(_trackWidth, _barHeight),
                DrawControlTexture = false,   // 轨道直接用图集帧实时绘制
                Minimum = 0,
                Maximum = _total,
            };
            _bar.ApplyAnchor();
            _bar.AtlasNameKey = SheetJsonPath + "#" + BarTrackFrame;       // 轨道（底槽）
            _bar.Fill.AtlasNameKey = SheetJsonPath + "#" + BarFillFrame;  // 填充（与轨道同图集，组件按 sheet key 去重只下载一次）
            _bar.Fill.DrawControlTexture = false;                         // 填充用图集帧，不走纯色兜底

            _statusLabel = new MirLabel
            {
                Parent = this,
                Anchor = EAnchorType.MiddleCenter,
                AnchorPos = new Point(0, _barCenterOffset - 46),
                Size = new Size(640, 24),
                Text = _status,
                ForeColour = Color.FromArgb(235, 235, 235),
                DrawFormat = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter,
            };
            _statusLabel.ApplyAnchor();

            _percentLabel = new MirLabel
            {
                Parent = this,
                Anchor = EAnchorType.MiddleCenter,
                AnchorPos = new Point(0, _barCenterOffset + 28),
                Size = new Size(640, 28),
                Text = "0%",
                ForeColour = Color.White,
                DrawFormat = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter,
            };
            _percentLabel.ApplyAnchor();

            _ = Task.Run(async () => await PreLoadNeedWaitRes());
            _ = Task.Run(async () => await PreLoad_NotNeedWaitRes());
        }

        /// 在登陆界面里，预加载自身UI的资源
        public static void PreLoadSelfRes()
        {
            _ = Mir2OriginResource.GetTextureAsync(BgImageName);
            _ = Mir2AtlasLibrary.GetSheetAsync(SheetJsonPath, default, Mir2OriginObjectLibrary.PriorityBackground);
        }

        //加载阻塞等待的资源
        private async Task PreLoadNeedWaitRes()
        {
            try
            {
                // 预加载主界面 UI 图集（y_homeUI-sheet）：进主界面后立即要用，属 UI 资源，按最高优先级（PriorityUI）。

                {
                    _status = "预加载主界面 UI…";
                    await Mir2AtlasLibrary.GetSheetAsync(HomeUiSheetKey, _cts.Token, Mir2OriginObjectLibrary.PriorityUI);
                    _loaded = 1;
                }

                _status = "加载完成，正在进入游戏…";
                _finished = true;
            }
            catch (System.Exception ex)
            {
                PrintTool.LogError("[LoadingDialog] y_homeUI-sheet 预加载失败（不影响进入游戏）：" + ex);
                _status = "部分资源加载失败：" + ex.Message;
            }
        }

        //加载非阻塞等待的资源
        private async Task PreLoad_NotNeedWaitRes()
        {
            Mir2OriginObjectLibrary.Request(Mir2ObjectKind.Monster, 15033, 1, 4, Mir2OriginObjectLibrary.PriorityVisible);
        }

        // 背景图 / 进度条现已作为子 MirImageControl 实时绘制（见构造函数），无需再手动烘焙 ControlTexture：
        // 本控件自身 ControlTexture 仅作黑色底，子控件（背景/进度条/文本）每帧实时叠加在最上层。

        /// <summary>每帧由 GameScene.Process 在主线程调用：更新进度条/百分比，并标记 Done。</summary>
        public void Tick()
        {
            if (Size.Width != Settings.ScreenWidth || Size.Height != Settings.ScreenHeight)
            {
                Size = new Size(Settings.ScreenWidth, Settings.ScreenHeight);
                _dirty = true;
            }
            if (_dirty)
            {
                _dirty = false;
                TextureValid = false; // 触发 ControlTexture 重烘焙（主线程安全）
            }

            float p = _total > 0 ? (float)_loaded / _total : 1f;

            // 进度条由 MirProgressBar 组件驱动（Value/Maximum 自动换算填充宽度）
            _bar.Value = _loaded;

            // 背景图按 cover 等比铺满（保持比例、居中、超出裁掉），避免拉伸变形
            if (_bgImage.Texture != null)
            {
                int iw = _bgImage.Texture.Width, ih = _bgImage.Texture.Height;
                int w = Settings.ScreenWidth, h = Settings.ScreenHeight;
                float scale = System.Math.Max((float)w / iw, (float)h / ih);
                int dw = (int)(iw * scale), dh = (int)(ih * scale);
                int dx = (w - dw) / 2, dy = (h - dh) / 2;
                if (_bgImage.Location.X != dx || _bgImage.Location.Y != dy ||
                    _bgImage.Size.Width != dw || _bgImage.Size.Height != dh)
                {
                    _bgImage.Location = new Point(dx, dy);
                    _bgImage.Size = new Size(dw, dh);
                }
            }

            _percentLabel.Text = (_total > 0 ? (int)(p * 100) : 100) + "%";
            _statusLabel.Text = _status;

            if (_finished)
            {
                KTween.delayedCall(1.0f, () => { Done = true; });
            }
        }

        /// <summary>取消仍在进行的资源加载任务（在 Dispose 前由 GameScene 调用）。</summary>
        public void Cancel()
        {
            _cts.Cancel();
        }
    }
}
