using KFramework.MonoGame;
using KFramework.MonoGameExtend;

namespace MirGame.Tests.FontTest;

/// <summary>
/// 字体测试模块。
/// <para>① 系统字体 <see cref="SpriteFont"/>：由浏览器 Canvas2D 光栅化，不需要任何字体文件。</para>
/// <para>② 矢量字体 <see cref="KFont"/>：引擎自己解析 ttf（TrueTypeFace）→ 光栅化 → 写入动态字形图集，
/// 与精灵一样合并进 SpriteBatch 的 WebGL 批次，全程不借浏览器。</para>
///
/// <para>字体文件放在 <c>Content/raw/Bundles/fonts</c>（Arial / Arial Bold / Consolas / Georgia / SimHei，
/// 取自 Windows 系统字体目录），kfc 构建时打成 AssetBundle「bundles/fonts」，运行时从包里取字节构造。</para>
/// </summary>
public sealed class FontTestScene : TestSceneBase
{
    public override string Title => "字体测试：系统字体 SpriteFont / 包内矢量字体 KFont";

    private const string BundleKeyword = "fonts";
    private const string BundlePath = "bundles/fonts";
    private const string AssetDir = "bundles/fonts/";

    // ① 系统字体（Canvas2D 光栅化）
    private SpriteFont? _sys16;
    private SpriteFont? _sys24;
    private SpriteFont? _sys32;
    private SpriteFont? _sysBold;
    private SpriteFont? _sysItalic;
    private SpriteFont? _sysHeavy;

    // ② 包内矢量字体（引擎自解析 + 自光栅化）
    private KFont? _arial16;
    private KFont? _arial28;
    private KFont? _arial40;
    private KFont? _arialBold;
    private KFont? _arialOblique;
    private KFont? _consolas24;
    private KFont? _georgia28;
    private KFont? _simhei28;
    private KFont? _simhei36;

    private readonly Dictionary<string, byte[]> _fontBytes = new(StringComparer.Ordinal);
    private readonly List<IDisposable> _owned = [];
    private readonly List<string> _log = [];
    private string _status = "正在从 AssetBundle 加载字体…";

    public override void LoadContent()
    {
        GraphicsDevice device = Device;

        // ① 系统字体：直接构造即可（浏览器已有 family）
        _sys16 = Own(new SpriteFont(device, 16f));
        _sys24 = Own(new SpriteFont(device, 24f));
        _sys32 = Own(new SpriteFont(device, 32f));
        _sysBold = Own(new SpriteFont(device, 24f, "system-ui, sans-serif", FontStyle.Bold));
        _sysItalic = Own(new SpriteFont(device, 24f, "system-ui, sans-serif", FontStyle.Italic));
        _sysHeavy = Own(new SpriteFont(device, 24f, "system-ui, sans-serif", FontStyle.Regular, 600));

        // ② 包内矢量字体：等包加载完再构造（解析 ttf 表需要字体字节）
        _ = LoadBundleFontsAsync();
    }

    private async Task LoadBundleFontsAsync()
    {
        try
        {
            Game game = KSceneMgr.Game;
            AssetBundle? bundle = game.Content.GetBundle(BundleKeyword, strict: false)
                                  ?? await game.Content.LoadBundleAsync(BundlePath).ConfigureAwait(false);

            LoadBytes(bundle, "arial.ttf");
            LoadBytes(bundle, "arialbd.ttf");
            LoadBytes(bundle, "consola.ttf");
            LoadBytes(bundle, "georgia.ttf");
            LoadBytes(bundle, "simhei.ttf");

            _arial16 = Create("arial.ttf", 16f);
            _arial28 = Create("arial.ttf", 28f);
            _arial40 = Create("arial.ttf", 40f);
            _arialBold = Create("arialbd.ttf", 28f);
            _arialOblique = Create("arial.ttf", 28f, obliqueDegrees: 12f);
            _consolas24 = Create("consola.ttf", 24f);
            _georgia28 = Create("georgia.ttf", 28f);
            _simhei28 = Create("simhei.ttf", 28f);
            _simhei36 = Create("simhei.ttf", 36f);

            _status = _fontBytes.Count == 0
                ? "AssetBundle 里没有找到字体文件（请先执行 kfc 构建 Content/raw）"
                : $"包内字体加载完成：{string.Join("、", _fontBytes.Keys)}";
        }
        catch (Exception ex)
        {
            _status = "字体加载失败：" + ex.Message;
            Log(_status);
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

    protected override void DrawBody(SpriteBatch batch, Vector2 origin)
    {
        float x = origin.X;
        float y = origin.Y;

        // ── ① 系统字体 ──────────────────────────────────────────────
        y += DrawSection(batch, "① 系统字体 SpriteFont（浏览器 Canvas2D 光栅化，不需要字体文件）", new Vector2(x, y));
        y += DrawLine(batch, Font, "状态：" + _status, new Vector2(x, y), Color.LightGray);
        y += DrawLine(batch, _sys16, "16px  The quick brown fox 0123456789", new Vector2(x, y), Color.White);
        y += DrawLine(batch, _sys24, "24px  The quick brown fox 0123456789", new Vector2(x, y), Color.White);
        y += DrawLine(batch, _sys32, "32px  The quick brown fox 0123", new Vector2(x, y), Color.White);
        y += DrawLine(batch, _sysBold, "24px  粗体 Bold：系统字体", new Vector2(x, y), Color.White);
        y += DrawLine(batch, _sysItalic, "24px  斜体 Italic：系统字体", new Vector2(x, y), Color.White);
        y += DrawLine(batch, _sysHeavy, "24px  字重 600：系统字体", new Vector2(x, y), Color.White);
        y += DrawLine(batch, _sys24, "24px  系统字体也能显示中文：你好，世界！", new Vector2(x, y), new Color(150, 220, 255));

        // 彩色（同一行分三段）
        if (_sys24 != null)
        {
            float cx = x;
            cx = DrawInline(batch, _sys24, "彩色：", cx, y, Color.LightGray);
            cx = DrawInline(batch, _sys24, "Red", cx, y, Color.Red);
            cx = DrawInline(batch, _sys24, "Green", cx, y, Color.Green);
            DrawInline(batch, _sys24, "Cyan", cx, y, Color.Cyan);
            y += _sys24.LineSpacing + 6f;
        }

        // ── ② 包内矢量字体 ──────────────────────────────────────────
        y += 16f;
        y += DrawSection(batch, "② 包内矢量字体 KFont（引擎自解析 ttf → 自光栅化 → 与精灵合批）", new Vector2(x, y));
        y += DrawLine(batch, _arial28, "Arial 28：The quick brown fox jumps over the lazy dog 0123456789", new Vector2(x, y), Color.White);
        y += DrawLine(batch, _arial16, "Arial 16：小字号 abcdefg 0123456789", new Vector2(x, y), Color.White);
        y += DrawLine(batch, _arial40, "Arial 40：Big Title", new Vector2(x, y), Color.White);
        y += DrawLine(batch, _arialBold, "Arial Bold 28：粗体 Bold", new Vector2(x, y), Color.White);
        y += DrawLine(batch, _arialOblique, "Arial 28 斜切 12°：Oblique Sample", new Vector2(x, y), Color.White);
        y += DrawLine(batch, _consolas24, "Consolas 24：等宽 | 0011 | aabb | WWWW |", new Vector2(x, y), new Color(180, 255, 200));
        y += DrawLine(batch, _georgia28, "Georgia 28：衬线 Serif Sample 0123", new Vector2(x, y), new Color(255, 220, 180));
        y += DrawLine(batch, _simhei28, "SimHei 28：中文黑体，你好世界！KFont 自解析 ttf 轮廓并光栅化。", new Vector2(x, y), new Color(255, 226, 150));
        y += DrawLine(batch, _simhei36, "SimHei 36：大号中文 传奇世界 2026", new Vector2(x, y), new Color(255, 226, 150));

        // ── ③ 排版与测量 ────────────────────────────────────────────
        y += 16f;
        y += DrawSection(batch, "③ 排版与测量（Measure 得到的宽高即文字实际占位）", new Vector2(x, y));
        if (_simhei28 != null)
        {
            const string sample = "测量文本 Hello 中文 0123";
            Vector2 size = _simhei28.Measure(sample);
            DrawRect(batch, new Rectangle((int)x, (int)y, (int)MathF.Ceiling(size.X), (int)MathF.Ceiling(size.Y)), new Color(56, 84, 130));
            batch.DrawString(_simhei28, sample, new Vector2(x, y), Color.White);
            y += size.Y + 8f;
            y += DrawLine(batch, Font, $"Measure(\"{sample}\") = {size.X:F1} × {size.Y:F1}（行高 {_simhei28.LineHeight:F1}）",
                          new Vector2(x, y), Color.LightGray);
        }
        else
        {
            y += DrawLine(batch, Font, "（KFont 未就绪，跳过测量示例）", new Vector2(x, y), Color.DarkGray);
        }

        // ── ④ 字体元信息 ────────────────────────────────────────────
        y += 16f;
        y += DrawSection(batch, "④ 字体元信息（KFont 直接从 ttf 的 head / hhea / name 表读出）", new Vector2(x, y));
        y = Meta(batch, "Arial 28", _arial28, x, y);
        y = Meta(batch, "Consolas 24", _consolas24, x, y);
        y = Meta(batch, "Georgia 28", _georgia28, x, y);
        y = Meta(batch, "SimHei 28", _simhei28, x, y);

        if (_log.Count > 0)
        {
            y += 16f;
            y += DrawSection(batch, "日志", new Vector2(x, y));
            foreach (string message in _log.Take(8))
                y += DrawLine(batch, Font, "· " + message, new Vector2(x, y), Color.Orange);
        }
    }

    private float Meta(SpriteBatch batch, string tag, KFont? font, float x, float y)
    {
        if (font is null)
            return y + DrawLine(batch, Font, $"{tag}：[未加载]", new Vector2(x, y), Color.DarkGray);

        string info = $"{tag}：family={font.Family}，upem={font.UnitsPerEm}，"
                      + $"ascent={font.Ascent:F1}，descent={font.Descent:F1}，行高={font.LineHeight:F1}";
        return y + DrawLine(batch, Font, info, new Vector2(x, y), Color.LightGray);
    }
}
