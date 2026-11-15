using System;
using System.Threading.Tasks;
using Client.MirControls;
using Client.MirGraphics;
using WebGame.Mir2.MonoGame.Client;
using Color = MirEngine.Color;
using Point = MirEngine.Point;
using Size = MirEngine.Size;

namespace Client.MirScenes
{
    /// <summary>
    /// 初始化 Loading 场景：游戏初始化（加载配置表 / 本地数据库 / 本地配置等）期间显示。
    /// 中间一个持续旋转的圆圈（用 ◐◓◑◒ 字符循环模拟，零依赖、无需图片资源），
    /// 上方一行字显示当前正在加载的内容。
    /// 本场景由 CMain.Init 在末尾设为 ActiveScene：渲染循环（LoadContentAsync 返回后即启动）一启动即被绘制；
    /// 真正的重活（配置表 / 图集 / 首屏资源）由本场景在渲染循环里异步推进（见 RunInitializationAsync），
    /// 每完成一步通过 SetStatus 更新状态文本，全部完成后切到 LoginScene。
    /// </summary>
    public sealed class InitLoadingScene : MirScene
    {
        // 四个四分之一圆字符循环，模拟旋转的圆圈（主流字体均支持，U+25D0~25D3）。
        private static readonly char[] Spinner = { '◐', '◓', '◑', '◒' };

        private readonly MirLabel _spinnerLabel;   // 中间旋转圆圈
        private readonly MirLabel _statusLabel;    // 上方状态文本
        private int _spinIndex;
        private long _lastSpin;
        private bool _started;

        public InitLoadingScene()
        {
            // 登录/选人类场景覆为黑底；初始化场景同样用纯黑，避免透出底层（ActiveScene 默认透明洋红）。
            BackColour = Color.Black;

            _spinnerLabel = new MirLabel
            {
                Parent = this,
                Anchor = EAnchorType.MiddleCenter,
                AnchorPos = new Point(0, 30),
                Size = new Size(64, 64),
                Text = Spinner[0].ToString(),
                ForeColour = Color.White,
                DrawFormat = new TextStyle(TextAnchor.MiddleCenter),
            };
            _spinnerLabel.ApplyAnchor();

            _statusLabel = new MirLabel
            {
                Parent = this,
                Anchor = EAnchorType.MiddleCenter,
                AnchorPos = new Point(0, -60),
                Size = new Size(800, 30),
                Text = "正在初始化…",
                ForeColour = Color.LightGray,
                DrawFormat = new TextStyle(TextAnchor.MiddleCenter),
            };
            _statusLabel.ApplyAnchor();
        }

        /// <summary>由异步初始化任务调用：更新当前加载内容提示（如「加载配置表…」）。</summary>
        public void SetStatus(string text)
        {
            _statusLabel.Text = text ?? string.Empty;
        }

        public override void Process()
        {
            // 首帧启动异步初始化：此时渲染循环已在跑，await 会让出事件循环，圆圈可继续转。
            if (!_started)
            {
                _started = true;
                _ = RunInitializationAsync();
            }

            // 推进旋转动画（约 8 帧切换一次，旋转顺滑）。
            long now = Environment.TickCount64;
            if (now - _lastSpin >= 120)
            {
                _lastSpin = now;
                _spinIndex = (_spinIndex + 1) % Spinner.Length;
                _spinnerLabel.Text = Spinner[_spinIndex].ToString();
            }
        }

        private async Task RunInitializationAsync()
        {
            try
            {
                await CMain.RunGameInitializationAsync(SetStatus).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                SetStatus("初始化失败：" + ex.Message);
                //PrintTool.LogError("[InitLoading] 初始化异常：" + ex);
            }
            finally
            {
                // RunGameInitializationAsync 内已切换到 LoginScene；此处释放本场景（ControlTexture 等）。
                Dispose();
            }
        }
    }
}
