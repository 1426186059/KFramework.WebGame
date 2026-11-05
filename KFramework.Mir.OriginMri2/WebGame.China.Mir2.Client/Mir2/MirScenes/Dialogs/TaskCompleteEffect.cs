using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Client.MirControls;                  // MirControl / MirAtlasAnimatedControl
using Client.MirGraphics;                 // DXManager
using Color = MirEngine.Color;
using Point = MirEngine.Point;
using Size = MirEngine.Size;

namespace Client.MirScenes.Dialogs
{
    /// <summary>
    /// 任务完成时在屏幕中央播放的序列帧特效。
    /// 组合（包含）一个 MirAtlasAnimatedControl 子组件来播放，自身作为容器（不直接继承动画控件）。
    /// 复用现有“原始传奇 Atlas”解析能力：按 manifest key 加载 task_complete 图集，帧矩形由描述文件给出，无需手算。
    /// 多个任务可能连续完成：用 _pending 计数排队，当前播放结束（AfterAnimation）后自动接着播下一个，不重叠、不丢。
    /// 触发：GameScene.ChangeQuest 检测到某子任务刚完成时调用 Play()。
    /// </summary>
    public sealed class TaskCompleteEffect : MirControl
    {
        // 原始传奇“完成任务”特效图集（manifest key，运行时由 GetSheetAsync 解析成 CDN 哈希路径）。
        private const string SheetKey = "task_complete.json";

        private MirAtlasAnimatedControl _ani;
        private bool _sheetReady;
        private bool _playing;
        private int _pending;   // 排队等待播放的次数（连续多个任务完成时 > 0）

        public TaskCompleteEffect()
        {
            NotControl = true;               // 容器本身不拦截鼠标
            Visible = false;
            Size = DXManager.FullScreenSize; // 仅用于避免被绘制管线因零尺寸裁剪跳过子组件

            _ani = new MirAtlasAnimatedControl
            {
                Parent = this,
                NotControl = true,
                Location = new Point(0, 0),  // 跟随容器定位，容器居中即动画居中
                Loop = false,                // 只播一次
                AnimationDelay = 70,         // 每帧毫秒（约 16 帧 ≈ 1.1s）
                Blending = true,             // 叠加混合，适合光效
                ForeColour = Color.White,
                Animated = false,
            };
            _ani.AfterAnimation += (s, e) => OnOneFinished();
            _ani.OffSetChanged += (s, e) => CenterToCurrentFrame();

            _ = LoadSheetAsync();
        }

        private async Task LoadSheetAsync()
        {
            try
            {
                // 用现成的 Atlas 解析 API：按 manifest key 加载整张图集，
                // GetSheetAsync 内部会 ResolveManifestAssetPath 解析 JSON 并按 sheet.file 拼出 PNG 路径解析贴图。
                var sheet = await Mir2AtlasLibrary.GetSheetAsync(SheetKey, DXManager.GDevice, CancellationToken.None);
                if (sheet == null) return;

                _ani.AtlasSheet = sheet;
                // 帧名按末尾数字后缀排序，保证播放顺序正确（命名无所谓，直接取图集里的全部帧）。
                var frames = sheet.Frames.Keys.OrderBy(FrameIndex).ToList();
                _ani.SetFrames(frames.ToArray());

                _sheetReady = true;
                if (_pending > 0) StartOne();   // 加载期间排队的，现在开播
            }
            catch (Exception ex)
            {
                KFramework.MonoGame.PrintTool.LogError("[TaskCompleteEffect] 图集加载失败: " + ex);
            }
        }

        // 取帧名末尾连续数字作排序键（取不到则记 0）。
        private static int FrameIndex(string name)
        {
            int i = name.Length - 1;
            while (i >= 0 && char.IsDigit(name[i])) i--;
            return int.TryParse(name.Substring(i + 1), out var n) ? n : 0;
        }

        public void Play()
        {
            if (!_sheetReady) { _pending++; return; }   // 图集未就绪，记一笔，加载完自动播
            if (_playing) { _pending++; return; }        // 正在播，排队等下一轮
            StartOne();
        }

        private void StartOne()
        {
            _playing = true;
            _ani.OffSet = 0;
            _ani.Animated = true;
            Visible = true;
            CenterToCurrentFrame();
            Redraw();
        }

        // 一段播放结束：有排队的就接着播，否则收起。
        private void OnOneFinished()
        {
            _playing = false;
            if (_pending > 0)
            {
                _pending--;
                StartOne();
            }
            else
            {
                _ani.Animated = false;
                Visible = false;
            }
        }

        // 屏幕居中：按当前帧原始尺寸定位容器（子动画在容器内 0,0，故定位容器即定位动画）。
        private void CenterToCurrentFrame()
        {
            string key = _ani.AtlasFrame;
            if (string.IsNullOrEmpty(key) || !_ani.AtlasSheet.Frames.TryGetValue(key, out var s)) return;
            int w = (int)s.Size.X, h = (int)s.Size.Y;
            var screen = DXManager.FullScreenSize;
            Location = new Point((screen.Width - w) / 2, (screen.Height - h) / 2);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (!disposing) return;
            if (_ani != null)
            {
                _ani.Dispose();
                _ani = null;
            }
            _pending = 0;
            _playing = false;
            _sheetReady = false;
        }
    }
}
