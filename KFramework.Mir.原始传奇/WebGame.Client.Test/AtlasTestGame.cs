using System;
using System.Threading.Tasks;
using KFramework.MonoGame;
using KFramework.MonoGameExtend;
// 与框架类型同名消歧（避免与 System.Drawing 等隐式导入冲突）。
using Rectangle = KFramework.MonoGame.Rectangle;

namespace WebGame.Client.Test
{
    /// <summary>
    /// 图集贴图颜色对比测试：
    ///   1) 直接复用客户端线上资源加载（CDN 图集），无需本地文件；
    ///   2) 用与游戏完全相同的链路解码成 Texture2D（浏览器原生解码 → RGBA8）；
    ///   3) 取选中帧中心像素的 RGBA（GetData），直接读出贴图本身到底是黑还是白；
    ///   4) 同屏对比三种画法：NonPremultiplied / 黑底 / 白底 / Additive，肉眼即可判断是“贴图被白化”还是“混合过曝”。
    ///
    /// 使用：改下面 SheetName / FrameName 两个常量即可（默认 y_homeUI-sheet / bg_zhukuang_1_tx）。
    /// 首次运行会从 CDN 下载图集（走浏览器 Cache Storage 缓存），重新生成并在浏览器打开即可。
    /// </summary>
    public sealed class AtlasTestGame : Game
    {
        // ===== 要测试的图集（线上 CDN 图集，直接改这里即可，无需本地文件、无额外依赖）=====
        private const string CdnBase = "https://cdn.yscq-wy.yscq.com/fugu_20201203"; // 与游戏同一 CDN
        private const string SheetName = "y_homeUI-sheet";                            // 仅用于界面显示
        // CDN 走哈希布局（allmanifest 解析）：resource/{hash前2}/{hash}_{size}.{ext}
        // 以下两个是 y_homeUI-sheet 在 allmanifest10.25341.1.json 中解析出的真实 CDN 路径。
        private const string SheetJsonCdn = "resource/c6/c6615f93_29900.json";
        private const string SheetPngCdn  = "resource/68/68b4b394_354142.png";
        private const string FrameName = "bg_liaotianzy_06"; // 想看的那一帧；找不到就退化为第一帧

        private Texture2D _white;
        private Texture2D _black;
        private Mir2AtlasSheet _sheet;
        private Texture2D _page;
        private KSpriteInfo _frame;
        private string _frameName = FrameName;
        private string _status = "加载中…";
        private Color _centerPixel = new Color(0, 0, 0, 0);

        public AtlasTestGame() : base(GraphicsBackendKind.WebGL20, "#game", antialias: false)
        {
            // 中性灰背景，便于肉眼分辨“黑 vs 白”。
            ClearColor = new Color(90, 90, 90);
            IsFixedTimeStep = false;
        }

        protected override async Task LoadContentAsync()
        {
            KDefaultRes.DefaultSpriteFont = new SpriteFont(GraphicsDevice, 18f);
            _white = GraphicsDevice.CreateTexture(1, 1, new byte[] { 255, 255, 255, 255 });
            _black = GraphicsDevice.CreateTexture(1, 1, new byte[] { 0, 0, 0, 255 });

            // 直接用 ContentManager 从线上 CDN 拉真实图集（与游戏同一份资源，无需本地文件、无额外依赖）。
            // 与游戏 Mir2OriginResource 完全一致：BaseURL 设成 CDN 基址，path 传【相对路径】；
            // ContentManager 内部 BaseAddress + "/相对路径" 拼出正确 CDN URL（BaseURL 为空会回退页面基址，故必须显式给 CDN 基址）。
            // root 传空：ResolveRooted 不会再加 hot_update_res 前缀。
            // 注：不包 try/catch，任何异常（下载/404/解析/GetData 等）直接抛出 → WASM 会打印到浏览器控制台（带堆栈）。
            var cm = new ContentManager(root: "", BaseURL: CdnBase);

            var jsonText = await cm.LoadTextAsync(SheetJsonCdn).ConfigureAwait(false);
            _sheet = Mir2AtlasSheet.Parse(jsonText);

            // PNG 与 JSON 是同一图集的两份文件，CDN 上都走哈希路径（与游戏一致）。
            _page = await cm.LoadTexture2DAsync(SheetPngCdn, GraphicsDevice).ConfigureAwait(false);
            _sheet.Build(_page);

            // 选帧：优先 FrameName，否则第一帧。
            if (!_sheet.Frames.TryGetValue(FrameName, out _frame))
            {
                foreach (var kv in _sheet.Frames) { _frame = kv.Value; _frameName = kv.Key; break; }
            }

            // 取该帧中心像素的真实 RGBA（贴图本身的数据，与怎么混合无关）。
            // 注意：ContentManager 解码出的纹理默认不可读，直接 GetData 会抛异常；
            // 框架提供 GetPixelsViaRenderTargetAsync 用渲染目标中转得到【可读副本】，再 GetData。
            if (_frame != null)
            {
                var r = _frame.SourceRectangle;
                // 只中转该帧所在子区域（更小更快），返回的可读纹理坐标从 0 开始。
                var readable = await _page.GetPixelsViaRenderTargetAsync(GraphicsDevice, r).ConfigureAwait(false);
                int cx = r.Width / 2;
                int cy = r.Height / 2;
                // 框架 GetData<Color> 因 Color 非基元数组（Buffer.BlockCopy 不支持）而失败，
                // 故改读 byte[]（RGBA8 每像素 4 字节），再手动组装 Color。
                var buf = new byte[4];
                readable.GetData(0, new Rectangle(cx, cy, 1, 1), buf, 0, 4);
                _centerPixel = new Color(buf[0], buf[1], buf[2], buf[3]);
            }

            _status = $"已加载：{SheetName}（{_sheet.Frames.Count} 帧）";
        }

        protected override void Draw(GameTime gameTime)
        {
            var sb = SpriteBatch;

            // —— 第一段：三种底色对比，每个图【下方】都有文字标注 ——
            sb.Begin();
            int x = 20, y = 20;
            if (_page != null)
                sb.Draw(_page, new Rectangle(x, y, 200, (int)(200.0 * _page.Height / _page.Width)), null, Color.White);

            if (_frame != null)
            {
                var r = _frame.SourceRectangle;
                int fw = (int)r.Width, fh = (int)r.Height;

                int xL = x + 240;              // 左：NonPremultiplied
                int xM = xL + fw + 30;         // 中：黑底
                int xR = xM + fw + 30;         // 右：白底
                int xA = xR + fw + 30;         // 最右：Additive（与上面一排）

                // 左：NonPremultiplied（正常画）
                sb.Draw(_page, new Rectangle(xL, y, fw, fh), r, Color.White);
                // 中：画在纯黑块上
                sb.Draw(_black, new Rectangle(xM, y, fw, fh), null, Color.White);
                sb.Draw(_page, new Rectangle(xM, y, fw, fh), r, Color.White);
                // 右：画在纯白块上
                sb.Draw(_white, new Rectangle(xR, y, fw, fh), null, Color.White);
                sb.Draw(_page, new Rectangle(xR, y, fw, fh), r, Color.White);
                // 最右：Additive 图以 Additive 混合绘制（见下方第二段）

                // 每图正下方只标注所用混合方式
                int ly = y + fh + 6;
                sb.DrawString(KDefaultRes.DefaultSpriteFont, "NonPremultiplied", new Vector2(xL, ly), Color.White);
                sb.DrawString(KDefaultRes.DefaultSpriteFont, "AlphaBlend·黑底", new Vector2(xM, ly), Color.White);
                sb.DrawString(KDefaultRes.DefaultSpriteFont, "AlphaBlend·白底", new Vector2(xR, ly), Color.White);
                sb.DrawString(KDefaultRes.DefaultSpriteFont, "Additive", new Vector2(xA, ly), Color.Orange);
            }

            // 信息栏（左侧，图集/帧/中心像素）
            sb.DrawString(KDefaultRes.DefaultSpriteFont, _status, new Vector2(20, 240), Color.White);
            sb.DrawString(KDefaultRes.DefaultSpriteFont,
                $"图集: {SheetName}   帧: {_frameName}   帧数: {(_sheet?.Frames.Count ?? 0)}",
                new Vector2(20, 264), Color.White);
            sb.DrawString(KDefaultRes.DefaultSpriteFont,
                $"★ 中心像素 RGBA = ({_centerPixel.R}, {_centerPixel.G}, {_centerPixel.B}, {_centerPixel.A})  ← 贴图本身到底是黑还是白，看这里",
                new Vector2(20, 288), Color.Yellow);
            sb.End();

            // —— 第二段：Additive 图（与上面一排最右侧，用 Additive 混合绘制）——
            if (_frame != null)
            {
                var r = _frame.SourceRectangle;
                int fw = (int)r.Width, fh = (int)r.Height;
                int xA = (x + 240) + (fw + 30) * 3; // 与上面一排最后一格对齐
                sb.Begin(SpriteSortMode.Deferred, BlendState.Additive);
                sb.Draw(_page, new Rectangle(xA, y, fw, fh), r, Color.White);
                sb.End();
            }
        }
    }
}
