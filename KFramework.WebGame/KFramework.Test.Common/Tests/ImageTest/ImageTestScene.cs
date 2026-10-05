using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KFramework.MonoGame;

namespace KFramework.Test.Common.Tests.ImageTest
{

    /// <summary>
    /// 图片解析测试：覆盖两种加载路径，并尽量覆盖多种纹理格式。
    /// <para>① 远程加载：直接走 <c>ContentManager.LoadTexture2DAsync(path, device)</c></para>
    ///   —— 从 Content 根（hot_update_res）按相对路径下载原始字节，再借浏览器原生解码器（createImageBitmap）解码为纹理。
    ///   测 jpg / png / webp 三种格式（资源放 Content/raw，构建时复制到 hot_update_res）。
    /// <para>② AssetBundle 加载：遍历所有已加载 Bundle 的纹理条目，逐个 <c>bundle.LoadTexture(path, device)</c></para>
    ///   —— 包内纹理格式（Rgba / Png / Webp / Ktx2）由构建阶段决定并写在 manifest 的 Format 字段；
    ///   本测试按条目声明的 Format 展示，构建时配了几种就覆盖几种（Ktx2 需构建端 basisu）。
    ///   资源放 Content/raw/Bundles/...，构建时打成 .web.lib。
    /// </summary>
    public sealed class ImageTestScene : TestSceneBase
    {
        public override string Title => "图片解析测试";

        private sealed record ImgItem(string Label, Texture2D? Tex, string Status);

        private readonly List<ImgItem> _remote = new();
        private readonly List<ImgItem> _bundle = new();
        private bool _remoteStarted, _bundleStarted;

        public override void Update()
        {
            base.Update();

            // 进入本场景后自动各跑一次（远程走网络/解码，Bundle 走已加载包）。
            if (!_remoteStarted) { _remoteStarted = true; _ = RunRemoteAsync(); }
            if (!_bundleStarted) { _bundleStarted = true; _ = RunBundleAsync(); }

            ClickButtons();
        }

        protected override void DrawBody(SpriteBatch batch, Vector2 origin)
        {
            BeginButtons(68);
            AddUiButton("重新加载远程", () => { _remote.Clear(); _ = RunRemoteAsync(); });
            AddUiButton("重新加载 Bundle", () => { _bundle.Clear(); _ = RunBundleAsync(); });
            DrawButtons(batch);

            float x = origin.X;
            float y = ContentTop;

            y += DrawSection(batch, "① 远程加载：ContentManager.LoadTexture2DAsync（jpg / png / webp，从 hot_update_res 下载后浏览器解码）", new Vector2(x, y));
            y = DrawItems(batch, x, y, _remote);
            if (!_remoteStarted) y += DrawLine(batch, Font, "（自动加载中…）", new Vector2(x, y), Color.LightGray);

            y += 16f;
            y += DrawSection(batch, "② AssetBundle 加载：遍历已加载包内全部纹理条目 → bundle.LoadTexture（Format 来自 manifest，覆盖 Rgba/Png/Webp/Ktx2）", new Vector2(x, y));
            y = DrawItems(batch, x, y, _bundle);
            if (!_bundleStarted) y += DrawLine(batch, Font, "（自动加载中…）", new Vector2(x, y), Color.LightGray);
            if (_bundleStarted && _bundle.Count == 0)
                y += DrawLine(batch, Font, "（当前没有已加载的 Bundle——需把图片打进 .web.lib 并构建 Content 后才会显示）", new Vector2(x, y), new Color(255, 190, 120));
        }

        private float DrawItems(SpriteBatch batch, float x, float y, List<ImgItem> items)
        {
            const float cell = 176f, size = 120f;
            float lineH = Font.LineSpacing;
            float labelDY = size + 6f;
            float statusDY = labelDY + lineH + 2f;
            // 行高 = 图 + label 行 + status 行 + 底部留白，确保下一行图不会压到本行 status 文字
            float rowH = statusDY + lineH + 16f;
            float cx = x, cy = y;
            foreach (var it in items)
            {
                if (cx + cell > Device.Viewport.Width - 20 && cx > x)
                {
                    cx = x;
                    cy += rowH;
                }
                var rect = new Rectangle((int)cx, (int)cy, (int)size, (int)size);
                if (it.Tex != null) batch.Draw(it.Tex, rect, Color.White);
                else DrawRect(batch, rect, new Color(120, 44, 44));
                DrawLine(batch, Font, Fit(Font, it.Label, cell), new Vector2(cx, cy + labelDY), Color.LightGray);
                DrawLine(batch, Font, Fit(Font, it.Status, cell), new Vector2(cx, cy + statusDY),
                    it.Tex != null ? new Color(130, 235, 150) : new Color(255, 150, 150));
                cx += cell;
            }
            return (items.Count == 0 ? cy : cy + rowH);
        }

        /// <summary>把文本截断到 <paramref name="maxW"/> 像素宽（超出加省略号），避免单元格间文字横向重叠。</summary>
        private static string Fit(IFont font, string text, float maxW)
        {
            if (string.IsNullOrEmpty(text) || font.Measure(text).X <= maxW) return text;
            int lo = 0, hi = text.Length;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) >> 1;
                if (font.Measure(text.Substring(0, mid) + "…").X <= maxW) lo = mid;
                else hi = mid - 1;
            }
            return text.Substring(0, lo) + "…";
        }

        private async Task RunRemoteAsync()
        {
            _remote.Clear();
            var files = new (string Label, string Path)[]
            {
                ("jpg", "test.jpg"),
                ("png", "test.png"),
                ("webp", "test.webp"),
                ("jpg", "test2.jpg"),
                ("png", "test2.png"),
                ("webp", "test2.webp"),
            };
            foreach (var f in files)
            {
                try
                {
                    Texture2D tex = await ContentManager.Default.LoadTexture2DAsync(f.Path, Device).ConfigureAwait(false);
                    _remote.Add(new ImgItem($"远程·{f.Label}", tex, $"{tex.Width}×{tex.Height} OK"));
                }
                catch (Exception ex)
                {
                    _remote.Add(new ImgItem($"远程·{f.Label}", null, "失败: " + FirstLine(ex.Message)));
                }
            }
        }

        private async Task RunBundleAsync()
        {
            _bundle.Clear();
            await Task.Yield();
            foreach (string bName in ContentManager.Default.LoadedBundles)
            {
                AssetBundle? bundle = ContentManager.Default.GetBundle(bName);
                if (bundle == null) continue;
                foreach (var e in bundle.Content.Entries)
                {
                    if (!string.Equals(e.Type, "texture", StringComparison.OrdinalIgnoreCase)) continue;
                    try
                    {
                        Texture2D tex = bundle.LoadTexture(e.Path, Device);
                        _bundle.Add(new ImgItem($"{bName}/{e.Path}", tex,
                            $"{e.Width}×{e.Height} {e.Format} OK"));
                    }
                    catch (Exception ex)
                    {
                        _bundle.Add(new ImgItem($"{bName}/{e.Path}", null,
                            $"{e.Format} 失败: " + FirstLine(ex.Message)));
                    }
                }
            }
        }

        private static string FirstLine(string s)
        {
            int i = s.IndexOf('\n');
            string line = i >= 0 ? s.Substring(0, i).Trim() : s.Trim();
            return line.Length > 90 ? line.Substring(0, 90) + "…" : line;
        }
    }
}
