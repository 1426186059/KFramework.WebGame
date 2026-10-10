using System.Text;
using KFramework.MonoGame;
using KFramework.MonoGameExtend;

namespace KFramework.Test.Common.Tests.FontTest
{

    /// <summary>
    /// 字体测试模块：一页里对比四条字体路线，按钮切模式、<b>空格</b>切字数档位。
    /// <para>① 系统字体 <see cref="SpriteFont"/>：浏览器 Canvas2D 光栅化，不需要任何字体文件；图集页满会自动开新页（多图集）。</para>
    /// <para>② 矢量字体 <see cref="KFont"/>：引擎自己解析 ttf（TrueTypeFace）→ 光栅化 → 写入动态字形图集，
    /// 与精灵一样合并进 SpriteBatch 的 WebGL 批次，全程不借浏览器。</para>
    /// <para>③ 位图字体 <see cref="BitmapFont"/>：包内没有 .fnt，故运行时按 BMFont 格式烘焙一份 ASCII 字集 ——
    /// 字形用包内 <c>arial.ttf</c>（注册成 FontFace 后由 Canvas2D 光栅化），再拼出 .fnt 文本交给
    /// <see cref="BitmapFont.FromFnt"/>。正式项目应由 kfc / Content.Cli 在构建期产出 .fnt + png。</para>
    /// <para>④ MSDF：只放按钮占位，暂未实现。</para>
    ///
    /// <para>档位：1000 → 9000 个字共 9 档，空格循环。汉字样本刻意用互不相同的字，
    /// 这样能直观看到图集翻页 / 单页装不下 / 首帧光栅化开销的差别。</para>
    ///
    /// <para>字体文件放在 <c>Content/raw/Bundles/fonts</c>（Arial / Arial Bold / Consolas / Georgia / SimHei，
    /// 取自 Windows 系统字体目录），kfc 构建时打成 AssetBundle「bundles/fonts」，运行时从包里取字节构造。</para>
    /// </summary>
    public sealed class FontTestScene : TestSceneBase
    {
        public override string Title => "字体测试：SpriteFont / KFont / BitmapFont / MSDF（按钮切模式 · 空格切字数档位）";

        // ── 模式与档位 ──────────────────────────────────────────────────────

        private enum FontMode { SpriteFont = 0, KFont = 1, BitmapFont = 2, Msdf = 3 }

        private static readonly string[] ModeLabels =
        [
            "SpriteFont（Canvas2D 光栅化）",
            "KFont（引擎自解析 ttf）",
            "BitmapFont（位图字体）",
            "MSDF（未实现 · 占位）",
        ];

        /// <summary>9 个档位：1000 → 9000 个字。</summary>
        private static readonly int[] CountOptions = [1000, 2000, 3000, 4000, 5000, 6000, 7000, 8000, 9000];

        private FontMode _mode = FontMode.SpriteFont;
        private int _countIndex;

        /// <summary>分档样本（按 模式 + 档位 缓存，只在切换时重建）。</summary>
        private string _sample = string.Empty;
        private int _sampleMode = -1;
        private int _sampleCount = -1;

        // ── ① 系统字体（Canvas2D 光栅化）──
        private SpriteFont? _sys24;
        private SpriteFont? _sysBold;
        private SpriteFont? _sysItalic;

        // ── ② 包内矢量字体（引擎自解析 + 自光栅化）──
        private KFont? _simhei20;    // 主字体：20px + 2048² 单页约 5000 余字（覆盖到 5000 档）
        private KFont? _simhei28;
        private KFont? _arial28;
        private KFont? _arialBold;

        // ── ③ 位图字体（运行时烘焙，见 BakeAsciiBitmapFont）──
        private BitmapFont? _bitmapFont;
        private bool _bitmapBakeFailed;
        private string _bitmapStatus = "未烘焙（切到本模式的当帧烘焙）";

        private const string BundleKeyword = "fonts";
        private const string BundlePath = "bundles/fonts";
        private const string AssetDir = "bundles/fonts/";

        private readonly Dictionary<string, byte[]> _fontBytes = new(StringComparer.Ordinal);
        private readonly List<IDisposable> _owned = [];
        private readonly List<string> _log = [];
        private string _status = "正在从 AssetBundle 加载字体…";

        // 布局（每帧重算）
        private float _infoY;
        private Rectangle _viewRect;
        private float _detailY;

        // 大块文本的分行缓存（按 模式 + 档位 + 面板宽 缓存；折行走逐字宽度累加）
        private readonly List<string> _lines = [];
        private int _linesMode = -1;
        private int _linesCount = -1;
        private int _linesWidth = -1;
        private int _drawnLines;

        public override void LoadContent()
        {
            GraphicsDevice device = Device;

            // ① 系统字体：直接构造即可（浏览器已有 family；引擎内多图集，页满自动开新页）
            _sys24 = Own(new SpriteFont(device, 24f));
            _sysBold = Own(new SpriteFont(device, 24f, "system-ui, sans-serif", FontStyle.Bold));
            _sysItalic = Own(new SpriteFont(device, 24f, "system-ui, sans-serif", FontStyle.Italic));

            // ② 包内矢量字体：等包加载完再构造（解析 ttf 表需要字体字节）
            _ = LoadBundleFontsAsync();
        }

        private async Task LoadBundleFontsAsync()
        {
            try
            {
                AssetBundle? bundle = ContentManager.Default.GetBundle(BundleKeyword, strict: false)
                                      ?? await ContentManager.Default.LoadBundleAsync(BundlePath).ConfigureAwait(false);

                LoadBytes(bundle, "simhei.ttf");
                LoadBytes(bundle, "arial.ttf");
                LoadBytes(bundle, "arialbd.ttf");

                _simhei20 = Create("simhei.ttf", 20f, atlasSize: 2048);
                _simhei28 = Create("simhei.ttf", 28f);
                _arial28 = Create("arial.ttf", 28f);
                _arialBold = Create("arialbd.ttf", 28f);

                // 位图字体模式要烘 arial.ttf：先把字体字节注册进 document.fonts（异步），烘焙时按 family 名使用
                if (_fontBytes.TryGetValue(BakeSourceFile, out byte[]? bakeBytes))
                    _bakeFontTask = SpriteFont.RegisterFontAsync(BakeFamily, bakeBytes);

                _status = _fontBytes.Count == 0
                    ? "AssetBundle 里没有找到字体文件（请先执行 kfc 构建 Content/raw）"
                    : $"包内字体加载完成：{string.Join("、", _fontBytes.Keys)}";
            }
            catch (Exception ex)
            {
                _status = "字体加载失败：" + ex.Message;
                Log(_status);
            }
            finally
            {
                // 包（以及可能失败的 ttf 注册）到此为止：位图字体模式可以开始烘焙了
                _bundleSettled = true;
            }
        }

        private void LoadBytes(AssetBundle bundle, string file)
        {
            if (bundle.TryGetAsset(AssetDir + file, out byte[] bytes, strict: true) ||
                bundle.TryGetAsset(file, out bytes, strict: false))
            {
                _fontBytes[file] = bytes;
                return;
            }

            Log($"包内缺少字体：{AssetDir}{file}");
        }

        private KFont? Create(string file, float size, int atlasSize = 1024, float obliqueDegrees = 0f, int boldPixels = 0)
        {
            if (!_fontBytes.TryGetValue(file, out byte[]? bytes))
                return null;

            try
            {
                return Own(new KFont(Device, size, bytes, atlasSize, 0f, obliqueDegrees, boldPixels));
            }
            catch (Exception ex)
            {
                Log($"{file}@{size} 构造失败：{ex.Message}");
                return null;
            }
        }

        private T Own<T>(T resource) where T : IDisposable
        {
            _owned.Add(resource);
            return resource;
        }

        private void Log(string message)
        {
            Console.WriteLine($"[FontTest] {message}");
            if (_log.Count < 16) _log.Add(message);
        }

        public override void Dispose()
        {
            foreach (IDisposable resource in _owned) resource.Dispose();
            _owned.Clear();
            base.Dispose();
        }

        // ── 交互 ───────────────────────────────────────────────────────────

        public override void Update()
        {
            base.Update();
            Layout();

            // 空格：9 个档位循环（1000 → 9000 → 1000）
            if (Input_KeyBoard.GetKeyDown(Keys.Space))
                _countIndex = (_countIndex + 1) % CountOptions.Length;

            if (_mode == FontMode.BitmapFont) EnsureBitmapFont();

            ClickButtons();
        }

        private void SetMode(FontMode mode) => _mode = mode;

        private void Layout()
        {
            BeginButtons(68);
            for (int i = 0; i < ModeLabels.Length; i++)
            {
                FontMode mode = (FontMode)i;
                AddUiButton(ModeLabels[i], () => SetMode(mode), _mode == mode);
            }

            // 按钮区下面一行说明，再往下是铺满剩余高度的大块文本面板，面板底下留 4 行信息
            _infoY = ContentTop;
            int panelTop = (int)_infoY + (int)(Font.LineSpacing + 6f);
            int panelBottom = Device.Viewport.Height - 4 * (int)(Font.LineSpacing + 6f) - 16;
            _viewRect = new Rectangle(28, panelTop,
                                      Math.Max(160, Device.Viewport.Width - 56),
                                      Math.Max(80, panelBottom - panelTop));
            _detailY = _viewRect.Bottom + 6f;
        }

        // ── 绘制 ───────────────────────────────────────────────────────────

        protected override void DrawBody(SpriteBatch batch, Vector2 origin)
        {
            Layout();

            string text = Sample();
            IFont? font = CurrentFont();

            DrawLine(batch, Font,
                     $"当前：{ModeLabels[(int)_mode]}    字数 {CountOptions[_countIndex]:N0}    ← 空格切下一档（{CountOptions[0]:N0}…{CountOptions[^1]:N0}）",
                     new Vector2(28f, _infoY), new Color(180, 220, 255));

            DrawRect(batch, _viewRect, new Color(16, 20, 32));

            Rectangle textRect = new(_viewRect.X + 10, _viewRect.Y + 8, _viewRect.Width - 20, _viewRect.Height - 16);

            if (_mode == FontMode.Msdf)
            {
                TextRenderer.DrawText(batch,
                    "MSDF 模式尚未实现（占位）。\n\n"
                    + "这里将来放多通道有符号距离场（MSDF）字体的对比：同一份字形数据在任意字号 / 缩放下都清晰，"
                    + "描边、外发光、阴影几乎不额外增加图集开销 —— 代价是生成麻烦、极小字号会发虚。",
                    Font, textRect, new Color(255, 206, 110), TextFormatFlags.WordBreak);
            }
            else if (font is null)
            {
                TextRenderer.DrawText(batch, NotReadyReason(), Font, textRect, Color.DarkGray, TextFormatFlags.WordBreak);
            }
            else
            {
                float textTop = textRect.Y;
                textTop += DrawVariants(batch, textRect.X, textTop);

                Rectangle main = new(textRect.X, (int)textTop, textRect.Width, Math.Max(1, textRect.Bottom - (int)textTop));
                DrawWrapped(batch, font, text, main);
            }

            DrawDetails(batch, font, text);
            DrawButtons(batch);
        }

        /// <summary>面板顶部的变体行（粗体 / 斜体 / 另一 family）；返回占用高度。</summary>
        private float DrawVariants(SpriteBatch batch, float x, float y)
        {
            switch (_mode)
            {
                case FontMode.SpriteFont:
                {
                    float cx = x;
                    cx = DrawInline(batch, _sys24!, "24px 常规：", cx, y, Color.LightGray);
                    cx = DrawInline(batch, _sysBold!, "粗体 Bold 你好", cx, y, Color.White);
                    DrawInline(batch, _sysItalic!, "斜体 Italic 你好", cx, y, Color.White);
                    return Font.LineSpacing + 8f;
                }

                case FontMode.KFont:
                {
                    KFont? zh = _simhei28 ?? _simhei20;
                    float cx = x;
                    cx = DrawInline(batch, zh!, "SimHei 28：黑体 你好世界", cx, y, new Color(255, 226, 150));
                    if (_arial28 != null) cx = DrawInline(batch, _arial28, "Arial 28：The quick fox", cx, y, Color.White);
                    if (_arialBold != null) DrawInline(batch, _arialBold, "Arial Bold 28：Bold", cx, y, Color.White);
                    return Font.LineSpacing + 8f;
                }

                default:
                    return 0f;
            }
        }

        /// <summary>
        /// 把整段样本折行后逐行绘制。只画面板装得下的行；<b>超出部分照样参与度量与字形光栅化</b>
        /// —— 也就是档位始终是"全量 N 个字"，只是不往面板外画。
        /// <para>
        /// 折行刻意不用 <see cref="TextRenderer"/> 的 WordBreak：它是"逐字 measure 不断变长的当前串"，
        /// 长文本会退化成 O(n²)（9000 字直接卡死）。这里按逐字宽度累加，O(n) 且字形已缓存。
        /// </para>
        /// </summary>
        private void DrawWrapped(SpriteBatch batch, IFont font, string text, Rectangle area)
        {
            List<string> lines = Lines(font, area.Width, text);
            float lineH = font.LineSpacing;
            int maxLines = Math.Max(1, (int)(area.Height / lineH));
            _drawnLines = Math.Min(lines.Count, maxLines);

            for (int i = 0; i < _drawnLines; i++)
            {
                font.Draw(batch, lines[i], new Vector2(area.X, area.Y + i * lineH), Color.White,
                          0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
            }
        }

        /// <summary>按面板宽度折行（结果缓存，仅在 模式 / 档位 / 宽度 变化时重建）。</summary>
        private List<string> Lines(IFont font, int maxWidth, string text)
        {
            if (_linesMode == (int)_mode && _linesCount == text.Length && _linesWidth == maxWidth)
                return _lines;

            _lines.Clear();
            var sb = new StringBuilder();
            float width = 0f;

            foreach (char c in text)
            {
                float charWidth = font.Measure(c.ToString()).X;
                if (width + charWidth > maxWidth && sb.Length > 0)
                {
                    _lines.Add(sb.ToString());
                    sb.Clear();
                    width = 0f;
                }
                sb.Append(c);
                width += charWidth;
            }
            if (sb.Length > 0) _lines.Add(sb.ToString());

            _linesMode = (int)_mode;
            _linesCount = text.Length;
            _linesWidth = maxWidth;
            return _lines;
        }

        /// <summary>面板下方的信息区：测量结果 + 当前字体的元信息 + 加载状态。</summary>
        private void DrawDetails(SpriteBatch batch, IFont? font, string text)
        {
            float y = _detailY;

            if (font is null || text.Length == 0)
            {
                y += DrawLine(batch, Font, "Measure：—", new Vector2(28f, y), Color.LightGray);
            }
            else
            {
                Vector2 size = font.Measure(text);
                y += DrawLine(batch, Font,
                              $"Measure({text.Length:N0} 字) = {size.X:F0} × {size.Y:F0}    行高 {font.LineSpacing:F1}    折行 {_lines.Count} 行（面板内画 {_drawnLines} 行）",
                              new Vector2(28f, y), Color.LightGray);
            }

            y += DrawLine(batch, Font, DetailLine(), new Vector2(28f, y), new Color(150, 165, 195));
            y += DrawLine(batch, Font, _status, new Vector2(28f, y), Color.Orange);
            if (_log.Count > 0)
                DrawLine(batch, Font, "日志：" + _log[^1], new Vector2(28f, y), new Color(200, 160, 120));
        }

        private string DetailLine() => _mode switch
        {
            FontMode.SpriteFont =>
                $"SpriteFont（Canvas2D 光栅化）：图集 {_sys24?.AtlasCount ?? 0} 页 × 1024²，字形按需光栅化、页满自动开新页",
            FontMode.KFont =>
                "KFont（引擎自解析 ttf）：SimHei 20px + 2048² 单页图集，单页约 5000 余字 —— 6000 档起会出现退化的空白字形（KFont 尚无多图集）",
            FontMode.BitmapFont =>
                "BitmapFont：" + _bitmapStatus + "；字集仅 ASCII 32~126，不做任何运行时光栅化",
            _ => "MSDF：未实现（已占位）",
        };

        private string NotReadyReason() => _mode switch
        {
            FontMode.KFont => "KFont 未就绪：" + _status,
            FontMode.BitmapFont => "BitmapFont 未就绪：" + _bitmapStatus,
            _ => "字体未就绪：" + _status,
        };

        private IFont? CurrentFont() => _mode switch
        {
            FontMode.SpriteFont => _sys24,
            FontMode.KFont => _simhei20 ?? _simhei28,
            FontMode.BitmapFont => _bitmapFont,
            _ => null,
        };

        // ── 样本生成 ───────────────────────────────────────────────────────

        private string Sample()
        {
            if (_mode == FontMode.Msdf) return string.Empty;

            int count = CountOptions[_countIndex];
            if (_sampleMode != (int)_mode || _sampleCount != count)
            {
                _sample = BuildSample(count, _mode);
                _sampleMode = (int)_mode;
                _sampleCount = count;
            }
            return _sample;
        }

        /// <summary>
        /// 造一段 count 个字的样本：位图字体只有 ASCII（烘焙字集），其余模式用互不相同的汉字
        /// —— 汉字不重复才真压到图集：SpriteFont 会翻到第 2、3… 页。
        /// </summary>
        private static string BuildSample(int count, FontMode mode)
        {
            var sb = new StringBuilder(count);

            if (mode == FontMode.BitmapFont)
            {
                const string ascii = "The quick brown fox jumps over the lazy dog 0123456789 ";
                while (sb.Length < count) sb.Append(ascii);
                return sb.ToString(0, count);
            }

            for (int i = 0; i < count; i++) sb.Append((char)(0x4E00 + i % 0x2000));
            return sb.ToString();
        }

        // ── ③ 位图字体：运行时烘焙（ASCII）────────────────────────────────

        private const int BakeAtlasSize = 1024;
        private const int BakePadding = 2;

        /// <summary>烘焙用的字号（像素）。</summary>
        private const float BakeFontPx = 24f;

        /// <summary>把包里的 ttf 注册进 document.fonts 后使用的 family 名。</summary>
        private const string BakeFamily = "FontTestBake";

        /// <summary>优先用包里这份字体烘焙（Content/raw/Bundles/fonts 下的 arial.ttf）。</summary>
        private const string BakeSourceFile = "arial.ttf";

        private Task<bool>? _bakeFontTask;
        private bool _bundleSettled;
        private string _bakeFontCss = $"{(int)BakeFontPx}px system-ui, sans-serif";
        private string _bakeFace = "system-ui";

        private void EnsureBitmapFont()
        {
            if (_bitmapFont is not null || _bitmapBakeFailed) return;

            // 包还没读完（或包内的 ttf 还在注册）就先不烘：否则会先用系统字体烘一份、随后作废
            if (!_bundleSettled)
            {
                _bitmapStatus = $"等待包内字体（{BakeSourceFile}）…";
                return;
            }
            if (_bakeFontTask is not null && !_bakeFontTask.IsCompleted)
            {
                _bitmapStatus = $"正在注册包内字体（{BakeSourceFile}）…";
                return;
            }

            if (_bakeFontTask is not null && _bakeFontTask.IsCompletedSuccessfully && _bakeFontTask.Result)
            {
                _bakeFontCss = $"{BakeFontPx:0}px \"{BakeFamily}\"";
                _bakeFace = BakeSourceFile;
            }
            else if (_bakeFontTask is { IsCompleted: true })
            {
                Log($"包内 {BakeSourceFile} 注册失败，位图字体退回系统字体烘焙");
            }

            try
            {
                _bitmapFont = BakeAsciiBitmapFont();
                _bitmapStatus = $"已烘焙：{_bakeFace} {BakeFontPx:0}px，ASCII 32~126（95 字），图集 {BakeAtlasSize}²";
                Log(_bitmapStatus);
            }
            catch (Exception ex)
            {
                _bitmapBakeFailed = true;
                _bitmapStatus = "烘焙失败：" + ex.Message;
                Log(_bitmapStatus);
            }
        }

        /// <summary>
        /// 运行时烘焙一份 ASCII 位图字体：用 <see cref="JSBind_Text"/> 逐字光栅化进一张图集页，
        /// 再按 BMFont 文本格式拼出 .fnt，最后走 <see cref="BitmapFont.FromFnt"/> ——
        /// 这样本页不附带二进制资源也能对比"位图字体"这条路（正式项目应由构建期工具产出 .fnt + png）。
        /// <para>
        /// 偏移按 <see cref="BitmapFont"/> 的约定给：<c>xoffset</c> 相对画笔（即格子左留白的负值），
        /// <c>yoffset</c> 相对基线（即格子顶边到基线的距离取负）。
        /// </para>
        /// </summary>
        private BitmapFont BakeAsciiBitmapFont()
        {
            Texture2D atlas = Own(Device.CreateTexture(BakeAtlasSize, BakeAtlasSize));
            var chars = new StringBuilder();
            Span<short> metrics = stackalloc short[4];

            int shelfX = 0, shelfY = 0, shelfHeight = 0;
            int lineHeight = 0, baseLine = 0, count = 0;

            for (int code = 32; code <= 126; code++)
            {
                string glyphText = ((char)code).ToString();
                JSBind_Text.Measure(glyphText, _bakeFontCss, 0f, metrics);

                int advance = metrics[0];
                int ascent = metrics[2];

                // 格子 = 字形推进宽 + 左右留白；高度 = 行高 + 上下留白
                int cellWidth = advance + BakePadding * 2;
                int cellHeight = metrics[1] + BakePadding * 2;
                if (cellWidth <= 0 || cellHeight <= 0) continue;

                if (shelfX + cellWidth > BakeAtlasSize)
                {
                    shelfX = 0;
                    shelfY += shelfHeight;
                    shelfHeight = 0;
                }
                if (shelfY + cellHeight > BakeAtlasSize)
                    throw new InvalidOperationException($"烘焙图集 {BakeAtlasSize}² 装不下 ASCII 字集。");

                byte[] pixels = new byte[cellWidth * cellHeight * 4];
                JSBind_Text.Render(glyphText, _bakeFontCss, 0f, BakePadding, BakePadding + ascent,
                                   cellWidth, cellHeight, pixels);
                atlas.SetData(pixels, shelfX, shelfY, cellWidth, cellHeight);

                chars.Append($"char id={code} x={shelfX} y={shelfY} width={cellWidth} height={cellHeight} ")
                     .Append($"xoffset={-BakePadding} yoffset={-(BakePadding + ascent)} xadvance={advance} page=0\n");

                lineHeight = Math.Max(lineHeight, cellHeight);
                baseLine = Math.Max(baseLine, BakePadding + ascent);
                shelfX += cellWidth;
                shelfHeight = Math.Max(shelfHeight, cellHeight);
                count++;
            }

            string fnt =
                $"info face=\"{_bakeFace}\" size={(int)BakeFontPx} bold=0 italic=0 charset=\"\" unicode=1 stretchH=100 smooth=1 aa=1 padding=2,2,2,2 spacing=1,1 outline=0\n" +
                $"common lineHeight={lineHeight} base={baseLine} scaleW={BakeAtlasSize} scaleH={BakeAtlasSize} pages=1 packed=0 alphaChnl=1 redChnl=0 greenChnl=0 blueChnl=0\n" +
                "page id=0 file=\"arial-baked.png\"\n" +
                $"chars count={count}\n" +
                chars +
                "kernings count=0\n";

            Log($"位图字体烘焙：{count} 字，行高 {lineHeight}，基线 {baseLine}");
            return BitmapFont.FromFnt(fnt, atlas);
        }
    }

}
