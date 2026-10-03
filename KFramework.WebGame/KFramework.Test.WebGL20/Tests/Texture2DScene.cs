using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KFramework.MonoGame;
using KFramework.MonoGameExtend;

namespace KFramework.Test.WebGL20.Tests
{

    /// <summary>
    /// Texture2D 取像素专项：三张图并排 —— <b>原图</b>，以及两种读法各自读出来的结果。
    /// 两个按钮分别触发一种读法，点了才读，各自更新自己那张结果图：
    /// <list type="bullet">
    ///   <item><description><b>① GetPixelsViaRenderTargetAsync</b>：原图<b>不可读</b>，只能 Blit 到离屏 RT
    ///   再 ReadPixels 读回，是真正的 GPU→CPU（WebGPU 走 copyTextureToBuffer + mapAsync，
    ///   WebGL2 走 glReadPixels）。异步，跨多帧完成。</description></item>
    ///   <item><description><b>② GetData</b>：另一张<b>可读</b>的同图案纹理，SetData 时保留的 CPU 副本
    ///   直接返回，与后端无关，同步完成。</description></item>
    /// </list>
    /// 判据用带【四角方位标记】的程序化图案：左上红 / 右上绿 / 左下蓝 / 右下黄，
    /// 读回一旦发生上下翻转、左右镜像或行错位，对应那张结果图立刻看得出来。
    /// </summary>
    public sealed class Texture2DScene : DemoScene
    {
        public override string Title => "4) Texture2D 取像素（原图 + 两种读法各一张）";

        protected override string Description
            => "点按钮才读：① GetPixelsViaRenderTargetAsync（离屏 Blit + GPU→CPU） ② GetData（CPU 副本），三图并排对比";

        private enum ReadKind { ViaRenderTarget, SyncGetData }

        // 按钮上只放短标签：把 API 全称写上去会把按钮撑得又长又挤，
        // 全称放在画面底部的说明区里，信息不丢。
        private static readonly (string Name, ReadKind Value)[] Actions =
        [
            ("① 中转读回", ReadKind.ViaRenderTarget),
            ("② 同步 GetData", ReadKind.SyncGetData),
        ];

        private const int W = 64;
        private const int H = 64;
        private const int Show = 150;   // 屏上显示的边长
        private const float Gap = 140f; // 图间距（要容得下每张图下方的两行文字，太窄会串行）

        private Texture2D? _srcTex;          // 原图（不可读）→ ① 的读取对象
        private Texture2D? _readableSrc;     // 同图案的可读纹理 → ② 的读取对象

        // ① 中转读回
        private Task<Texture2D>? _rtTask;
        private Texture2D? _rtResult;
        private int _rtDiff = -1;
        private string _rtErr = string.Empty;
        private bool _rtStarted;
        private int _rtRuns;

        // ② 同步 GetData
        private Texture2D? _gdResult;
        private int _gdDiff = -1;
        private string _gdErr = string.Empty;
        private bool _gdStarted;
        private int _gdRuns;

        private readonly List<Rectangle> _buttons = [];

        /// <summary>
        /// 直接重写 Update：本项目的 <c>DemoScene</c> 是旧结构，只有 RenderOffscreen / DrawBody，
        /// 没有 UpdateBody 钩子（WebGPU 那边才有），且已有多个子类自行重写 Update，
        /// 不宜给基类加 sealed 钩子，故这里自行接管。
        /// </summary>
        public override void Update()
        {
            HandleButtons();

            // 原图（不可读）—— ① 读它；② 读的是另一张可读的同图案纹理。
            _srcTex ??= Device.CreateTexture(W, H, GenPattern(W, H));

            // ① 是异步的（WebGPU 的 mapAsync 跨多帧完成），在这里收尾。② 同步，点击时已算完。
            if (_rtStarted && _rtResult is null && _rtTask is not null && _rtTask.IsCompleted)
            {
                try
                {
                    Texture2D rt = _rtTask.Result;
                    byte[] got = new byte[W * H * 4];
                    rt.GetData(got);
                    _rtDiff = Diff(GenPattern(W, H), got);
                    _rtResult = Device.CreateTexture(W, H, got);
                    _rtErr = string.Empty;
                    _rtRuns++;
                }
                catch (Exception e) { _rtErr = e.GetType().Name + ": " + e.Message; }
            }
        }

        /// <summary>按按钮触发一次读取：先清空该方法上一次的结果，读回来才填上。</summary>
        private void StartRead(ReadKind kind)
        {
            if (kind == ReadKind.ViaRenderTarget)
            {
                DropResult(ref _rtResult);
                _rtDiff = -1;
                _rtErr = string.Empty;
                _rtStarted = true;
                // 原图不可读 → 只能 Blit 进离屏 RT 再 ReadPixels 读回（真 GPU→CPU）。
                _rtTask = _srcTex!.GetPixelsViaRenderTargetAsync(Device);
                return;
            }

            DropResult(ref _gdResult);
            _gdDiff = -1;
            _gdErr = string.Empty;
            _gdStarted = true;

            try
            {
                // ② 可读源 → GetData 直接返回 CPU 副本，与后端无关，同步完成。
                _readableSrc ??= CreateReadableSource();
                byte[] got = new byte[W * H * 4];
                _readableSrc.GetData(got);
                _gdDiff = Diff(GenPattern(W, H), got);
                _gdResult = Device.CreateTexture(W, H, got);
                _gdErr = string.Empty;
                _gdRuns++;
            }
            catch (Exception e) { _gdErr = e.GetType().Name + ": " + e.Message; }
        }

        private Texture2D CreateReadableSource()
        {
            var t = new Texture2D(Device, W, H, readable: true);
            t.SetData(GenPattern(W, H));
            return t;
        }

        /// <summary>丢弃一张结果纹理（每次读回都会新建，不回收会持续泄漏显存纹理）。</summary>
        private static void DropResult(ref Texture2D? tex)
        {
            if (tex is null) return;
            tex.Dispose();
            tex = null;
        }

        // ================================================================
        // 顶部按钮：点了才触发一次读取
        // ================================================================

        private void LayoutButtons()
        {
            _buttons.Clear();
            float x = 28f, y = 76f;
            float limit = Math.Max(240f, Device.Viewport.Width - 28f);
            for (int i = 0; i < Actions.Length; i++)
            {
                int w = 56 + EstimateWidth(Actions[i].Name);   // 左右各留 28 内边距，字不贴边
                if (x + w > limit && x > 28f) { x = 28f; y += 44f; }
                _buttons.Add(new Rectangle((int)x, (int)y, w, 38));
                x += w + 16;                                    // 按钮之间的留白
            }
        }

        /// <summary>粗估文字宽度。故意取偏大的值（CJK 22 / 西文 12）：
        /// 估窄了按钮会不够宽，字被挤出去或被下一个按钮的背景盖住，看着像"显示不完整"。</summary>
        private static int EstimateWidth(string s)
        {
            int w = 0;
            foreach (char c in s) w += c > 0x2E80 ? 22 : 12;
            return w;
        }

        private void HandleButtons()
        {
            LayoutButtons();
            if (!Input_Mouse.GetButtonDown(MouseButton.Left)) return;

            Vector2 p = Input_Mouse.Position;
            for (int i = 0; i < _buttons.Count; i++)
            {
                if (!_buttons[i].Contains(p)) continue;
                StartRead(Actions[i].Value);
                return;
            }
        }

        // ================================================================
        // 屏幕绘制：原图 + 两种读法各一张
        // ================================================================

        protected override void DrawBody(SpriteBatch batch, float top)
        {
            for (int i = 0; i < _buttons.Count; i++)
            {
                Rectangle rect = _buttons[i];
                bool done = Actions[i].Value == ReadKind.ViaRenderTarget
                    ? _rtResult is not null
                    : _gdResult is not null;
                bool hover = rect.Contains(Input_Mouse.Position);
                batch.Draw(KDefaultRes.DefaultTexture2D, rect,
                    done ? new Color(58, 92, 150) : hover ? new Color(52, 76, 120) : new Color(34, 44, 68));
                batch.DrawString(Font, Actions[i].Name, new Vector2(rect.X + 28f, rect.Y + 8f),
                    done ? Color.White : new Color(190, 208, 235));
            }

            // 正文区顶边 = 最后一行按钮的底边 + 间距（与 OffscreenScene 的 BodyTop 同一算法）。
            // 直接写死 top 会让按钮换行时图形压到按钮上 —— 按钮与图形在 Y 上必须前后相接而非重叠。
            float by = _buttons.Count > 0 ? _buttons[^1].Bottom + 24f : top + 24f;
            float x1 = 28f;
            float x2 = x1 + Show + Gap;
            float x3 = x2 + Show + Gap;

            // ① 原图
            if (_srcTex is not null)
                batch.Draw(_srcTex, new Rectangle((int)x1, (int)by, Show, Show), Color.White);
            batch.DrawString(Font, "原图（不可读）", new Vector2(x1, by + Show + 6f), new Color(150, 165, 195));

            // ② 中转读回读出来的
            DrawResult(batch, x2, by, _rtResult, _rtStarted, _rtErr, _rtDiff, _rtRuns, "① 中转读回");

            // ③ GetData 读出来的
            DrawResult(batch, x3, by, _gdResult, _gdStarted, _gdErr, _gdDiff, _gdRuns, "② 同步 GetData");

            // 说明区统一放长文字：写死在图下方会串行到隔壁那张图上，故集中在这里。
            float ly = by + Show + 70f;
            ly = DrawLine(batch, "判据：三张图应完全一致 —— 四角标记 左上红 / 右上绿 / 左下蓝 / 右下黄；错位即读回翻转或镜像",
                x1, ly, new Color(255, 206, 110));
            ly = DrawLine(batch, "① GetPixelsViaRenderTargetAsync：原图不可读，Blit 进离屏 RT 再 ReadPixels 读回，真 GPU→CPU，异步",
                x1, ly, new Color(150, 165, 195));
            ly = DrawLine(batch, "  （WebGPU：copyTextureToBuffer + mapAsync；WebGL2：glReadPixels）",
                x1, ly, new Color(110, 130, 170));
            DrawLine(batch, "② GetData：另一张可读的同图案纹理，SetData 保留的 CPU 副本直接返回，与后端无关，同步",
                x1, ly, new Color(150, 165, 195));
        }

        private void DrawResult(SpriteBatch batch, float x, float y, Texture2D? tex, bool started,
                                string err, int diff, int runs, string caption)
        {
            if (tex is not null)
                batch.Draw(tex, new Rectangle((int)x, (int)y, Show, Show), Color.White);
            else
                Frame(batch, x, y, Show, Show, new Color(90, 104, 132));

            batch.DrawString(Font, caption, new Vector2(x, y + Show + 6f), new Color(150, 165, 195));

            string status;
            Color color;
            if (!started)
            {
                status = "未读取（点按钮）";
                color = new Color(150, 165, 195);
            }
            else if (err.Length > 0)
            {
                status = "失败：" + err;
                color = new Color(255, 150, 150);
            }
            else if (tex is null)
            {
                status = "读取中…";
                color = new Color(255, 206, 110);
            }
            else
            {
                bool ok = diff == 0;
                status = ok ? $"偏差 0 ✓  (已读 {runs} 次)" : $"偏差 {diff} ✗";
                color = ok ? new Color(150, 220, 170) : new Color(255, 150, 150);
            }
            batch.DrawString(Font, status, new Vector2(x, y + Show + 32f), color);
        }

        /// <summary>给一块区域描一圈 2px 边框（未读取时当占位框）。</summary>
        private void Frame(SpriteBatch batch, float x, float y, float w, float h, Color color)
        {
            int ix = (int)x, iy = (int)y, iw = (int)w, ih = (int)h, t = 2;
            batch.Draw(KDefaultRes.DefaultTexture2D, new Rectangle(ix, iy, iw, t), color);
            batch.Draw(KDefaultRes.DefaultTexture2D, new Rectangle(ix, iy + ih - t, iw, t), color);
            batch.Draw(KDefaultRes.DefaultTexture2D, new Rectangle(ix, iy, t, ih), color);
            batch.Draw(KDefaultRes.DefaultTexture2D, new Rectangle(ix + iw - t, iy, t, ih), color);
        }

        // ================================================================
        // 程序化图案（带四角方位标记 + 棋盘），作为源与比对基准
        // ================================================================

        private static byte[] GenPattern(int w, int h)
        {
            var data = new byte[w * h * 4];
            int cell = Math.Max(1, w / 8);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    bool even = ((x / cell) + (y / cell)) % 2 == 0;
                    int i = (y * w + x) * 4;
                    data[i] = even ? (byte)42 : (byte)72;
                    data[i + 1] = even ? (byte)50 : (byte)84;
                    data[i + 2] = even ? (byte)70 : (byte)114;
                    data[i + 3] = 255;
                }
            }
            int m = Math.Max(4, w / 10);
            Fill(data, w, 0, 0, m, m, 220, 70, 70, 255);          // 左上 红
            Fill(data, w, w - m, 0, m, m, 70, 200, 120, 255);     // 右上 绿
            Fill(data, w, 0, h - m, m, m, 70, 140, 220, 255);     // 左下 蓝
            Fill(data, w, w - m, h - m, m, m, 230, 200, 80, 255);  // 右下 黄
            return data;
        }

        private static void Fill(byte[] data, int w, int x0, int y0, int cw, int ch,
                                 byte r, byte g, byte b, byte a)
        {
            for (int y = Math.Max(0, y0); y < Math.Min(w, y0 + ch); y++)
                for (int x = Math.Max(0, x0); x < Math.Min(w, x0 + cw); x++)
                {
                    int i = (y * w + x) * 4;
                    data[i] = r; data[i + 1] = g; data[i + 2] = b; data[i + 3] = a;
                }
        }

        /// <summary>逐像素比对（任一通道不同即计为差异）。</summary>
        private static int Diff(byte[] a, byte[] b)
        {
            int diff = 0;
            int pixels = a.Length / 4;
            for (int p = 0; p < pixels; p++)
            {
                int i = p * 4;
                if (a[i] != b[i] || a[i + 1] != b[i + 1] || a[i + 2] != b[i + 2] || a[i + 3] != b[i + 3])
                    diff++;
            }
            return diff;
        }
    }

}
