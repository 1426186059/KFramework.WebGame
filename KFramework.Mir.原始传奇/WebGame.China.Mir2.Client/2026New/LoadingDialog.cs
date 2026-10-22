using Client.MirGraphics;
using KFramework.MonoGame;
using KFramework.MonoGameExtend;
using SlimDX.Direct3D9;
using Color = MirEngine.Color;
using GraphicsDevice = KFramework.MonoGame.GraphicsDevice;
using Point = MirEngine.Point;
using Size = MirEngine.Size;

namespace Client.MirControls
{
    /// <summary>
    /// 盖在 GameScene 之上的加载蒙层（非独立场景）：GameScene 必须立刻 Active 以接收网络包，
    /// 故 GameScene 一显示就创建本蒙层；其下 GameScene 正常收包/跑逻辑。
    /// 蒙层美术直接用原始传奇 H5 资源：bg_loading2n.jpg（全屏背景）+ preload-sheet（进度条/Logo 图集），
    /// 这两个资源在本蒙层创建时按需加载（用到才加载），加载完即显示；并借这段时间预下载主界面 UI 图集
    /// y_homeUI-sheet（正式 RES 资源，进主界面立即要用，先缓存避免卡顿）。不预加载其余主场景游戏资源。
    /// </summary>
    public class LoadingDialog : MirControl
    {
        // 原版 CDN 直链资源（不在 manifest/RES 表内，按相对 CDN 根的路径 + 缓存参数加载）
        private const string BgImageName = "img/bg_loading2n.jpg?qufu_version=229";                          // 加载背景图
        private const string SheetJsonPath = "qufu_resource1/assets/sheet/preload-sheet.json?qufu_version=229"; // 进度条图集 JSON
        
        private const string SheetPngPath = "qufu_resource1/assets/sheet/preload-sheet.png?qufu_version=229";  // 进度条图集 PNG
        private const string BarBgFrameHint = "bar";
        private const string BarFillFrameHint = "loading";
        private const string HomeUiSheetKey = "y_homeUI-sheet";   // 主界面 UI 图集（正式 RES 资源，预加载进缓存，进主界面不卡）

        private readonly MirLabel _titleLabel;
        private readonly MirLabel _statusLabel;
        private readonly MirLabel _percentLabel;
        private readonly MirControl _barTrack;
        private readonly MirControl _barFill;

        private readonly int _trackWidth = 400;
        private readonly int _barHeight = 18;

        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        private Texture2D _bgTexture;
        private Mir2AtlasSheet _sheet;
        private string _barBgFrame;
        private string _barFillFrame;
        private bool _useSheetBar;

        private int _total = 3;
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

            _barTrack = new MirControl
            {
                Parent = this,
                Anchor = EAnchorType.BottomCenter,
                AnchorPos = new Point(0, 70),
                Size = new Size(_trackWidth, _barHeight),
                BackColour = Color.Black,
                DrawControlTexture = true,
            };
            _barTrack.ApplyAnchor();

            _barFill = new MirControl
            {
                Parent = _barTrack,
                Anchor = EAnchorType.BottomCenter,
                Location = new Point(0, 70),
                Size = new Size(1, _barHeight),
                BackColour = Color.Green,
                DrawControlTexture = true,
            };

            _titleLabel = new MirLabel
            {
                Parent = this,
                Anchor = EAnchorType.MiddleCenter,
                AnchorPos = new Point(0, -50),
                Size = new Size(500, 30),
                Text = "资源加载中…",
                ForeColour = Color.White,
                DrawFormat = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter,
            };
            _titleLabel.ApplyAnchor();

            _statusLabel = new MirLabel
            {
                Parent = this,
                Anchor = EAnchorType.MiddleCenter,
                AnchorPos = new Point(0, 100),
                Size = new Size(500, 20),
                Text = _status,
                ForeColour = Color.LightGray,
                DrawFormat = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter,
            };
            _statusLabel.ApplyAnchor();

            _percentLabel = new MirLabel
            {
                Parent = this,
                Anchor = EAnchorType.MiddleCenter,
                AnchorPos = new Point(0, 40),
                Size = new Size(500, 20),
                Text = "0%",
                ForeColour = Color.White,
                DrawFormat = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter,
            };
            _percentLabel.ApplyAnchor();

            var device = DXManager.GDevice;
            _ = Task.Run(async () => await LoadArtAsync(device));

        }

        private async Task LoadArtAsync(GraphicsDevice device)
        {
            try
            {
                _status = "加载背景图…";
                _bgTexture = await Mir2AtlasLibrary.GetImageTextureAsync(BgImageName, device, _cts.Token);
                _loaded = 1;
                _dirty = true;

                _status = "加载进度条资源…";
                _sheet = await Mir2AtlasLibrary.GetSheetByPathsAsync(SheetJsonPath, SheetPngPath, device, _cts.Token);
                _loaded = 2;

                if (_sheet != null)
                {
                    PrintTool.Log("[LoadingDialog] preload-sheet 帧名：" + string.Join(", ", _sheet.Frames.Keys));
                    PickBarFrames();
                    if (_useSheetBar)
                    {
                        _barTrack.Visible = false;
                        _barFill.Visible = false;
                    }
                }
                else
                {
                    PrintTool.Log("[LoadingDialog] preload-sheet 未取到（JSON/PNG 拉取为空），回退纯色进度条。");
                }

                // 预加载主界面 UI 图集（y_homeUI-sheet）：登录/进主界面后立即要用，
                // 趁加载蒙层期间下载并缓存，避免进主界面时再卡一下。失败仅日志，不阻塞进入游戏。
                _status = "预加载主界面 UI…";
                try
                {
                    await Mir2AtlasLibrary.GetSheetAsync(HomeUiSheetKey, device, _cts.Token);
                    PrintTool.Log("[LoadingDialog] y_homeUI-sheet 预加载完成。");
                }
                catch (System.Exception ex)
                {
                    PrintTool.LogError("[LoadingDialog] y_homeUI-sheet 预加载失败（不影响进入游戏）：" + ex);
                }
                _loaded = 3;
                _status = "加载完成，正在进入游戏…";
            }
            catch (System.Exception ex)
            {
                _status = "部分资源加载失败：" + ex.Message;
                PrintTool.LogError("[LoadingDialog] 资源加载异常：" + ex);
                _loaded = _total; // 失败也走完进度，避免卡在 0%
            }
            finally
            {
                _dirty = true;
                _finished = true;
            }
        }

        // 从 preload-sheet 按关键字挑出进度条底图/填充帧（挑不到则回退纯色条）。
        private void PickBarFrames()
        {
            _barBgFrame = null;
            _barFillFrame = null;
            foreach (var key in _sheet.Frames.Keys)
            {
                var k = key.ToLowerInvariant();
                if (_barBgFrame == null && k.Contains(BarBgFrameHint) &&
                    (k.Contains("bg") || k.Contains("back") || k.Contains("track") || k.Contains("kuang") || k.Contains("ground")))
                    _barBgFrame = key;
                if (_barFillFrame == null && (k.Contains(BarFillFrameHint) || k.Contains("fill") || k.Contains("pro") || k.Contains("jd")))
                    _barFillFrame = key;
            }
            if (_barBgFrame == null)
            {
                foreach (var key in _sheet.Frames.Keys)
                    if (key.ToLowerInvariant().Contains(BarBgFrameHint)) { _barBgFrame = key; break; }
            }
            _useSheetBar = _barBgFrame != null || _barFillFrame != null;
        }

        // 把背景图 / 进度条图集帧烘焙进本蒙层的 ControlTexture（全屏），供 DrawControl 上屏。
        // 用 BeginSprite(null) 以 1:1 烘焙（不受全局 RenderTransform 缩放影响）。
        protected override void CreateTexture()
        {
            if (ControlTexture == null || ControlTexture.Disposed)
            {
                DXManager.ControlList.Add(this);
                ControlTexture = DXManager.CreateRenderTarget(Size.Width, Size.Height);
                TextureSize = Size;
            }

            var saved = DXManager.CurrentSurface;
            DXManager.SetSurface(new Surface(ControlTexture));
            DXManager.GDevice.Clear(new KFramework.MonoGame.Color(0, 0, 0, 255));

            var mat = DXManager.GetMaterial(KFramework.MonoGame.BlendState.NonPremultiplied);
            DXManager.BeginSprite(mat, null);
            try
            {
                DrawArtIntoControlTexture();
            }
            finally
            {
                DXManager.EndSprite();
            }

            DXManager.SetSurface(saved);
            TextureValid = true;
        }

        private void DrawArtIntoControlTexture()
        {
            int w = Size.Width, h = Size.Height;
            if (_bgTexture != null)
            {
                DXManager.Batch.Draw(_bgTexture,
                    new KFramework.MonoGame.Rectangle(0, 0, w, h),
                    new KFramework.MonoGame.Rectangle(0, 0, _bgTexture.Width, _bgTexture.Height),
                    new KFramework.MonoGame.Color(255, 255, 255, 255));
            }

            if (_useSheetBar && _sheet != null)
            {
                int x = (Settings.ScreenWidth - _trackWidth) / 2;
                int y = Settings.ScreenHeight / 2 + 70;
                float p = _total > 0 ? (float)_loaded / _total : 1f;
                if (_barBgFrame != null && _sheet.Frames.TryGetValue(_barBgFrame, out var bg))
                    DXManager.Batch.Draw(bg.Texture,
                        new KFramework.MonoGame.Rectangle(x, y, _trackWidth, _barHeight),
                        bg.SourceRectangle,
                        new KFramework.MonoGame.Color(255, 255, 255, 255));
                if (_barFillFrame != null && _sheet.Frames.TryGetValue(_barFillFrame, out var fill))
                    DXManager.Batch.Draw(fill.Texture,
                        new KFramework.MonoGame.Rectangle(x, y, (int)(_trackWidth * p), _barHeight),
                        fill.SourceRectangle,
                        new KFramework.MonoGame.Color(255, 255, 255, 255));
            }
        }

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
            int barW = System.Math.Max(1, (int)(_trackWidth * p));
            if (_barFill.Size.Width != barW)
                _barFill.Size = new Size(barW, _barHeight);
            _percentLabel.Text = (_total > 0 ? (int)(p * 100) : 100) + "%";
            _statusLabel.Text = _status;

            if (_finished)
            {
                KTween.delayedCall(1.0f, () => { Done = true; });
            }
        }

        /// <summary>取消仍在进行的资源加载任务（在 Dispose 前由 GameScene 调用）。</summary>
        public void Cancel() => _cts.Cancel();
    }
}
