using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using KFramework.MonoGame;
using KFramework.MonoGameExtend;
// 与框架类型同名消歧（避免与 System.Drawing 等隐式导入冲突）。
using Rectangle = KFramework.MonoGame.Rectangle;

namespace WebGame.Client.Test
{
    /// <summary>
    /// 图集贴图颜色对比测试：
    ///   1) 从 wwwroot/atlas/ 加载一个 Egret 图集（*-sheet.json + 同目录 *-sheet.png）；
    ///   2) 用与游戏完全相同的链路解码成 Texture2D（浏览器原生解码 → RGBA8）；
    ///   3) 取选中帧中心像素的 RGBA（GetData），直接读出贴图本身到底是黑还是白；
    ///   4) 同屏对比三种画法：NonPremultiplied / 黑底 / 白底 / Additive，肉眼即可判断是“贴图被白化”还是“混合过曝”。
    ///
    /// 使用：把你要测的图集文件（如 y_homeUI-sheet.json 和 y_homeUI-sheet.png）放进本工程 wwwroot/atlas/ 目录，
    /// 然后改下面两个常量即可。重新生成并在浏览器打开即可。
    /// </summary>
    public sealed class AtlasTestGame : Game
    {
        // ===== 要测试的图集（放进 wwwroot/atlas/ 后改这里）=====
        private const string SheetName = "y_homeUI-sheet";   // 不含扩展名；会加载 atlas/y_homeUI-sheet.json + .png
        private const string FrameName = "bg_zhukuang_1_tx"; // 想看的那一帧；找不到就退化为第一帧

        private const string AtlasBase = "./atlas/";

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

            try
            {
                var http = new HttpClient();
                var jsonBytes = await http.GetByteArrayAsync(AtlasBase + SheetName + ".json").ConfigureAwait(false);
                var pngBytes = await http.GetByteArrayAsync(AtlasBase + SheetName + ".png").ConfigureAwait(false);

                _sheet = Mir2AtlasSheet.Parse(Encoding.UTF8.GetString(jsonBytes));

                // 关键：与游戏完全相同的贴图解码链路（浏览器原生解码 → RGBA8 → Texture2D）。
                _page = await ContentManager.Default.LoadTexture2DAsync(pngBytes, GraphicsDevice).ConfigureAwait(false);
                _sheet.Build(_page);

                // 选帧：优先 FrameName，否则第一帧。
                if (!_sheet.Frames.TryGetValue(FrameName, out _frame))
                {
                    foreach (var kv in _sheet.Frames) { _frame = kv.Value; _frameName = kv.Key; break; }
                }

                // 取该帧中心像素的真实 RGBA（贴图本身的数据，与怎么混合无关）。
                if (_frame != null)
                {
                    var r = _frame.SourceRectangle;
                    int cx = r.X + r.Width / 2;
                    int cy = r.Y + r.Height / 2;
                    var buf = new Color[1];
                    _page.GetData(0, new Rectangle(cx, cy, 1, 1), buf, 0, 1);
                    _centerPixel = buf[0];
                }

                _status = $"已加载：{SheetName}（{_sheet.Frames.Count} 帧）";
            }
            catch (Exception ex)
            {
                _status = "加载失败：" + ex.Message;
            }
        }

        protected override void Draw(GameTime gameTime)
        {
            var sb = SpriteBatch;

            // —— 第一段：NonPremultiplied（预期正确的直通 alpha）——
            sb.Begin();
            int x = 20, y = 20;
            if (_page != null)
                sb.Draw(_page, new Rectangle(x, y, 200, (int)(200.0 * _page.Height / _page.Width)), null, Color.White);

            if (_frame != null)
            {
                var r = _frame.SourceRectangle;
                int fw = (int)r.Width, fh = (int)r.Height;

                // 左：NonPremultiplied（正常画）
                sb.Draw(_page, new Rectangle(x + 240, y, fw, fh), r, Color.White);

                // 中：画在纯黑块上（若贴图其实白，会明显发白；若其实黑，则与黑块融合看不见）
                sb.Draw(_black, new Rectangle(x + 240 + fw + 30, y, fw, fh), null, Color.White);
                sb.Draw(_page, new Rectangle(x + 240 + fw + 30, y, fw, fh), r, Color.White);

                // 右：画在纯白块上（若贴图其实黑，会显出深色形状；若其实白，则与白块融合看不见）
                sb.Draw(_white, new Rectangle(x + 240 + 2 * (fw + 30), y, fw, fh), null, Color.White);
                sb.Draw(_page, new Rectangle(x + 240 + 2 * (fw + 30), y, fw, fh), r, Color.White);
            }

            sb.DrawString(KDefaultRes.DefaultSpriteFont, _status, new Vector2(20, 240), Color.White);
            sb.DrawString(KDefaultRes.DefaultSpriteFont,
                $"图集: {SheetName}   帧: {_frameName}   帧数: {(_sheet?.Frames.Count ?? 0)}",
                new Vector2(20, 264), Color.White);
            sb.DrawString(KDefaultRes.DefaultSpriteFont,
                $"★ 中心像素 RGBA = ({_centerPixel.R}, {_centerPixel.G}, {_centerPixel.B}, {_centerPixel.A})  ← 贴图本身到底是黑还是白，看这里",
                new Vector2(20, 288), Color.Yellow);
            sb.DrawString(KDefaultRes.DefaultSpriteFont,
                "左:NonPremultiplied   中:黑底   右:白底", new Vector2(20, 312), Color.LightGray);
            sb.End();

            // —— 第二段：Additive（验证“过曝发白/混合过头”假设）——
            if (_frame != null)
            {
                var r = _frame.SourceRectangle;
                int fw = (int)r.Width, fh = (int)r.Height;
                int ax = 20, ay = 340;
                sb.Begin(SpriteSortMode.Deferred, BlendState.Additive);
                sb.Draw(_page, new Rectangle(ax, ay, fw, fh), r, Color.White);
                sb.End();

                sb.Begin();
                sb.DrawString(KDefaultRes.DefaultSpriteFont,
                    "★ Additive 混合（验证过曝假设）：暗图在亮背景上会被叠亮 → 发白",
                    new Vector2(ax, ay + fh + 8), Color.Orange);
                sb.End();
            }
        }
    }
}
