using System.Threading;
using System.Threading.Tasks;
using Client.MirGraphics;
using KFramework.MonoGame;
using Size = MirEngine.Size;
using Point = MirEngine.Point;
using Color = MirEngine.Color;
using GraphicsDevice = KFramework.MonoGame.GraphicsDevice;

namespace Client.MirControls
{
    /// <summary>
    /// 盖在 GameScene 之上的加载蒙层（不是独立场景）：GameScene 必须立刻 Active 起来接收网络包，
    /// 否则早期包（StartGame/角色/地图…）命中 null 会报错。故 GameScene 一显示就创建本蒙层，
    /// 在它下面 GameScene 正常收包/跑逻辑；本蒙层只负责把主场景所需 H5 图集预加载进
    /// Mir2AtlasLibrary 缓存，并显示进度，加载完成（或失败兜底）由 GameScene 调用 Dispose 移除。
    /// 对应原始传奇 Egret 的 loadingState / RES.loadGroup 分阶段预加载。
    /// </summary>
    public class LoadingDialog : MirControl
    {
        // 主场景预加载图集清单（原版 Egret 资源短名，如 "y_homeUI-sheet_json"）。
        // 先放主界面 HUD 图集，后续按需补充（地图/角色/怪物精灵等），
        // 补充时直接往这个数组加 sheet 短名即可；也可改用 Mir2AtlasLibrary.GetGroupResourceNames("分组名") 整组预加载。
        private static readonly string[] PreloadSheets = new[]
        {
            "y_homeUI-sheet_json", // 主界面 HUD（MainDialogUI 使用）
        };

        private readonly MirControl _panel;
        private readonly MirLabel _titleLabel;
        private readonly MirLabel _statusLabel;
        private readonly MirLabel _percentLabel;
        private readonly MirControl _barTrack;
        private readonly MirControl _barFill;

        private readonly int _trackWidth = 400;
        private readonly int _barHeight = 18;

        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private int _total;
        private int _loaded;
        private bool _finished;
        private string _status = "正在准备…";

        /// <summary>由 GameScene 在每帧 Tick 时读取：预加载是否已结束（成功或失败）。</summary>
        public bool Done { get; private set; }

        public LoadingDialog()
        {
            // 全屏不透明黑色蒙层，盖住下方正在初始化的 GameScene。
            Location = new Point(0, 0);
            Size = new Size(Settings.ScreenWidth, Settings.ScreenHeight);
            BackColour = Color.Black;
            DrawControlTexture = true;

            _panel = new MirControl
            {
                Parent = this,
                Size = new Size(440, 150),
                BackColour = Color.DarkGray,
                DrawControlTexture = true,
                Anchor = EAnchorType.MiddleCenter,
                AnchorPos = new Point(0, 0),
            };
            _panel.ApplyAnchor();

            _titleLabel = new MirLabel
            {
                Parent = _panel,
                Location = new Point(0, 16),
                Size = new Size(440, 26),
                Text = "资源加载中…",
                ForeColour = Color.White,
                DrawFormat = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter,
            };

            _statusLabel = new MirLabel
            {
                Parent = _panel,
                Location = new Point(16, 52),
                Size = new Size(408, 20),
                Text = _status,
                ForeColour = Color.LightGray,
                DrawFormat = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter,
            };

            _barTrack = new MirControl
            {
                Parent = _panel,
                Location = new Point(20, 86),
                Size = new Size(_trackWidth, _barHeight),
                BackColour = Color.Black,
                DrawControlTexture = true,
            };

            _barFill = new MirControl
            {
                Parent = _panel,
                Location = new Point(20, 86),
                Size = new Size(1, _barHeight),
                BackColour = Color.Green,
                DrawControlTexture = true,
            };

            _percentLabel = new MirLabel
            {
                Parent = _panel,
                Location = new Point(16, 114),
                Size = new Size(408, 20),
                Text = "0%",
                ForeColour = Color.White,
                DrawFormat = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter,
            };

            var device = DXManager.GDevice;
            _ = Task.Run(async () => await PreloadAsync(device));
        }

        private async Task PreloadAsync(GraphicsDevice device)
        {
            var names = PreloadSheets;
            _total = names.Length;
            _loaded = 0;
            try
            {
                for (int i = 0; i < names.Length; i++)
                {
                    if (_cts.IsCancellationRequested) break;
                    var name = names[i];
                    _status = string.Format("加载中 ({0}/{1})：{2}", i + 1, names.Length, name);
                    await Mir2AtlasLibrary.GetSheetAsync(name, device, _cts.Token);
                    _loaded = i + 1;
                }
                _status = _loaded >= _total ? "加载完成，正在进入游戏…" : "部分资源已加载";
            }
            catch (System.Exception ex)
            {
                _status = "部分资源加载失败：" + ex.Message;
                PrintTool.LogError("[LoadingDialog] 预加载异常：" + ex);
            }
            finally
            {
                _finished = true;
            }
        }

        /// <summary>每帧由 GameScene.Process 在主线程调用：更新进度条/百分比，并标记 Done。</summary>
        public void Tick()
        {
            if (Size.Width != Settings.ScreenWidth || Size.Height != Settings.ScreenHeight)
                Size = new Size(Settings.ScreenWidth, Settings.ScreenHeight);

            float p = _total > 0 ? (float)_loaded / _total : 1f;
            int barW = System.Math.Max(1, (int)(_trackWidth * p));
            if (_barFill.Size.Width != barW)
                _barFill.Size = new Size(barW, _barHeight);
            _percentLabel.Text = (_total > 0 ? (int)(p * 100) : 100) + "%";
            _statusLabel.Text = _status;

            if (_finished) Done = true;
        }

        /// <summary>取消仍在进行的预加载任务（在 Dispose 前由 GameScene 调用）。</summary>
        public void Cancel() => _cts.Cancel();
    }
}
