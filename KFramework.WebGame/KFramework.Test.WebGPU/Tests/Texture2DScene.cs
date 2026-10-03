using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KFramework.MonoGame;
using KFramework.MonoGameExtend;

namespace KFramework.Test.WebGPU.Tests
{

    /// <summary>
    /// Texture2D 取像素专项：用一张带【四角方位标记】的程序化图案作判据（能立刻暴露读回时的上下翻转 /
    /// 左右镜像 / 行错位），验证两条取像素通路：
    /// <list type="bullet">
    ///   <item><description><b>A 中转读回</b>：任意（不可读）纹理经离屏 Blit 再读回 GPU→CPU，正是
    ///   <see cref="Texture2D.GetPixelsViaRenderTargetAsync"/> 的实现 —— WebGPU 走 copyTextureToBuffer + mapAsync，
    ///   WebGL2 走 glReadPixels。这是本场景的核心判据。</description></item>
    ///   <item><description><b>B 同步往返</b>：可读纹理 SetData 上传后 GetData 直接返回 CPU 副本（与后端无关），
    ///   验证 CreateTexture / SetData / GetData 通路本身。</description></item>
    /// </list>
    /// </summary>
    public sealed class Texture2DScene : DemoScene
    {
        public override string Title => "4) Texture2D 取像素（中转读回 / 同步往返）";

        protected override string Description
            => "A 中转读回：任意纹理经离屏 Blit 再读回，验证 WebGPU/WebGL 的 GPU→CPU；B 同步往返：可读纹理 SetData→GetData";

        private enum Sub { Readback, SyncGet }

        private static readonly (string Name, Sub Value)[] Screens =
        [
            ("A 中转读回", Sub.Readback),
            ("B 同步往返", Sub.SyncGet),
        ];

        private const int W = 64;
        private const int H = 64;
        private const int Show = 160;   // 屏上显示的边长

        private Texture2D? _srcTex;          // 源（不可读，Color）
        private Task<Texture2D>? _readbackTask;
        private Texture2D? _readbackResult;  // 中转结果（可读 RT）
        private int _diffA = -1;
        private string _errA = string.Empty;

        private Texture2D? _syncTex;         // 可读源（子测试 B）
        private Texture2D? _syncResultTex;   // GetData 结果上传成纹理，便于并排显示
        private int _diffB = -1;

        private Sub _sub = Sub.Readback;
        private readonly List<Rectangle> _buttons = [];

        protected override void UpdateBody()
        {
            HandleButtons();

            if (_sub == Sub.Readback)
            {
                _srcTex ??= Device.CreateTexture(W, H, GenPattern(W, H));
                if (_readbackTask is null)
                    _readbackTask = _srcTex.GetPixelsViaRenderTargetAsync(Device);

                if (_readbackTask.IsCompleted && _readbackResult is null)
                {
                    try
                    {
                        _readbackResult = _readbackTask.Result;
                        byte[] got = new byte[W * H * 4];
                        _readbackResult.GetData(got);
                        _diffA = Diff(GenPattern(W, H), got);
                        _errA = string.Empty;
                    }
                    catch (Exception e) { _errA = e.GetType().Name + ": " + e.Message; }
                }
            }
            else // SyncGet
            {
                if (_syncTex is null)
                {
                    byte[] pattern = GenPattern(W, H);
                    _syncTex = new Texture2D(Device, W, H, readable: true);
                    _syncTex.SetData(pattern);
                    byte[] got = new byte[W * H * 4];
                    _syncTex.GetData(got);
                    _diffB = Diff(pattern, got);
                    _syncResultTex = Device.CreateTexture(W, H, got);
                }
            }
        }

        // ================================================================
        // 顶部按钮：切换子测试（不是开关）
        // ================================================================

        private void LayoutButtons()
        {
            _buttons.Clear();
            float x = 28f, y = 76f;
            float limit = Math.Max(240f, Device.Viewport.Width - 28f);
            for (int i = 0; i < Screens.Length; i++)
            {
                int w = 28 + EstimateWidth(Screens[i].Name);
                if (x + w > limit && x > 28f) { x = 28f; y += 36f; }
                _buttons.Add(new Rectangle((int)x, (int)y, w, 30));
                x += w + 8;
            }
        }

        private static int EstimateWidth(string s)
        {
            int w = 0;
            foreach (char c in s) w += c > 0x2E80 ? 20 : 10;
            return w;
        }

        private void HandleButtons()
        {
            LayoutButtons();
            if (!Input_Mouse.GetButtonDown(MouseButton.Left)) return;

            Vector2 p = Input_Mouse.Position;
            for (int i = 0; i < _buttons.Count; i++)
            {
                if (_buttons[i].Contains(p))
                {
                    if (_sub != Screens[i].Value) _sub = Screens[i].Value;
                    return;
                }
            }
        }

        // ================================================================
        // 屏幕绘制
        // ================================================================

        protected override void DrawBody(SpriteBatch batch, float top)
        {
            for (int i = 0; i < _buttons.Count; i++)
            {
                Rectangle rect = _buttons[i];
                bool current = Screens[i].Value == _sub;
                bool hover = rect.Contains(Input_Mouse.Position);
                batch.Draw(KDefaultRes.DefaultTexture2D, rect,
                    current ? new Color(58, 92, 150) : hover ? new Color(52, 76, 120) : new Color(34, 44, 68));
                batch.DrawString(Font, Screens[i].Name, new Vector2(rect.X + 12f, rect.Y + 5f),
                    current ? Color.White : new Color(190, 208, 235));
            }

            float bx = 28f;
            float by = top + 24f;

            if (_sub == Sub.Readback)
            {
                if (_srcTex is not null) batch.Draw(_srcTex, new Rectangle((int)bx, (int)by, Show, Show), Color.White);
                if (_readbackResult is not null)
                    batch.Draw(_readbackResult, new Rectangle((int)(bx + Show + 40f), (int)by, Show, Show), Color.White);

                float ly = by + Show + 18f;
                ly = DrawLine(batch, "左：源纹理（不可读）   右：中转读回（GPU→CPU）", bx, ly, new Color(150, 165, 195));
                if (_readbackResult is null)
                {
                    ly = DrawLine(batch,
                        _errA.Length > 0 ? $"读回失败：{_errA}" : "读回中…（WebGPU 为 copyTextureToBuffer + mapAsync 异步）",
                        bx, ly, _errA.Length > 0 ? new Color(255, 150, 150) : new Color(255, 206, 110));
                }
                else
                {
                    bool ok = _diffA == 0;
                    ly = DrawLine(batch, $"像素偏差：{_diffA} / {W * H}    {(_diffA == 0 ? "完全一致 ✓" : "不一致 ✗")}",
                        bx, ly, ok ? new Color(150, 220, 170) : new Color(255, 150, 150));
                    DrawLine(batch, "判据：四角标记 左上红 / 右上绿 / 左下蓝 / 右下黄；若错位说明读回翻转或镜像",
                        bx, ly, new Color(150, 165, 195));
                }
            }
            else
            {
                if (_syncTex is not null) batch.Draw(_syncTex, new Rectangle((int)bx, (int)by, Show, Show), Color.White);
                if (_syncResultTex is not null)
                    batch.Draw(_syncResultTex, new Rectangle((int)(bx + Show + 40f), (int)by, Show, Show), Color.White);

                float ly = by + Show + 18f;
                ly = DrawLine(batch, "左：可读源纹理   右：GetData 读回再上传", bx, ly, new Color(150, 165, 195));
                ly = DrawLine(batch, $"像素偏差：{_diffB} / {W * H}    {(_diffB == 0 ? "完全一致 ✓" : "不一致 ✗")}",
                    bx, ly, _diffB == 0 ? new Color(150, 220, 170) : new Color(255, 150, 150));
                DrawLine(batch, "纯 CPU 镜像往返（SetData 保留副本，GetData 直接返回），与后端无关", bx, ly, new Color(150, 165, 195));
            }
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
