using Client.MirControls;
using Client.MirGraphics;
using KFramework.MonoGameExtend;
using WebGame.Mir2.MonoGame.Client;

namespace Client.MirObjects
{
    /// <summary>
    /// 原始传奇（Egret 页游）战斗飘血数字，继承 <see cref="Damage"/> 以便直接放入 MapObject.Damages。
    ///
    /// 移植依据（资源参考 main10.25341.1.min.js）：
    /// - BloodMovie.addNumber：按 "hurt" + number 样式号逐字符取帧 hurt{style}_{char}，横向拼接；0 不绘制。
    /// - EntityInfo.showBloodEffect：随机偏移(random_x/y_min/max) + 上浮(startarr/stoparr/endarr) +
    ///   performance=0 时起始放大 scaling/100 回弹、末段 alpha 淡出。
    ///
    /// 图集：default.res.json 的 BloodFont_json（血条在 MapObject.EnsureOriginBloodSheet 已预加载，这里也兜底触发）。
    /// 帧缺失（图集未就绪 / 该样式无此字符）时回退到 MirLabel 文本，保证 Miss/Crit 等文字照常显示。
    /// </summary>
    public sealed class OriginMir2Damage : Damage
    {
        /// <summary>原始 hurt 样式号（对应 JS cm.damage[].number）。</summary>
        public int HurtStyle;

        /// <summary>随机散布范围（原始 random_x/y_min/max，像素）。</summary>
        public int RandomX, RandomY;

        /// <summary>起始放大倍数（原始 scaling/100），1 表示不弹。</summary>
        public float StartScale = 1f;

        private static int _sheetRequested;

        private readonly int _jitterX, _jitterY;
        private MirLabel _fallbackLabel;

        public OriginMir2Damage(string text, int duration, Color colour, int hurtStyle,
            int distance = 50, int randomX = 8, int randomY = 6, float startScale = 1.35f)
            : base(text, duration, colour, distance)
        {
            HurtStyle = hurtStyle;
            RandomX = Math.Max(0, randomX);
            RandomY = Math.Max(0, randomY);
            StartScale = startScale > 1f ? startScale : 1f;

            _jitterX = RandomX > 0 ? CMain.Random.Next(-RandomX, RandomX + 1) : 0;
            _jitterY = RandomY > 0 ? CMain.Random.Next(-RandomY, RandomY + 1) : 0;

            if (System.Threading.Interlocked.Exchange(ref _sheetRequested, 1) == 0)
                _ = Mir2AtlasLibrary.GetSheetAsync("BloodFont_json");
        }

        private static KSpriteInfo GetGlyph(int style, char ch)
        {
            string key;
            if (ch == '+') key = "hurt" + style + "_+";
            else if (ch == '-') key = "hurt" + style + "_-";
            else if (ch >= '0' && ch <= '9') key = "hurt" + style + "_" + ch;
            else return null; // 逗号/空格/字母：原始 addNumber 不绘制，交给回退文本

            return Mir2AtlasLibrary.TryGetFrame("BloodFont_json", key, out var frame) ? frame : null;
        }

        public override void Draw(Point displayLocation)
        {
            long timeRemaining = ExpireTime - CMain.Time;
            if (timeRemaining < 0) timeRemaining = 0;

            float progress = 1f - (timeRemaining / (float)Math.Max(1, ExpireTime - StartTime));
            if (progress < 0f) progress = 0f;
            if (progress > 1f) progress = 1f;

            // 上浮：从 0 到 -Distance（原始 stoparr/endarr 的 dy）。
            int rise = (int)(progress * Distance);
            int x = displayLocation.X + _jitterX;
            int y = displayLocation.Y - 75 - Offset - rise + _jitterY;

            float scale;
            int alpha;
            if (progress < 0.18f)
            {
                float t = progress / 0.18f;
                scale = StartScale + (1f - StartScale) * t * t; // 弹出回弹（sineIn 近似）
                alpha = 255;
            }
            else if (progress > 0.72f)
            {
                float t = (progress - 0.72f) / 0.28f;
                scale = 1f;
                alpha = (int)(255 * (1f - t)); // 尾部淡出
            }
            else
            {
                scale = 1f;
                alpha = 255;
            }

            if (DrawOrigin(x, y, scale, alpha)) return;
            DrawFallback(x, y, alpha);
        }

        /// <summary>逐字符取图集帧横拼（对齐原始 BloodMovie.addNumber）。false=帧未就绪，回退文本。</summary>
        private bool DrawOrigin(int x, int y, float scale, int alpha)
        {
            // 先用 '0' 探测图集是否已缓存；未就绪直接回退，下一帧再试。
            if (!Mir2AtlasLibrary.TryGetFrame("BloodFont_json", "hurt" + HurtStyle + "_0", out var probe) || probe?.Texture == null)
                return false;

            int cursor = x;
            bool any = false;
            Color tint = new Color(Colour.R, Colour.G, Colour.B, (byte)alpha);

            foreach (char ch in Text)
            {
                KSpriteInfo glyph = GetGlyph(HurtStyle, ch);
                if (glyph == null) continue; // 0/逗号/空格：原始逻辑不绘制

                float w = glyph.Size.X * scale;
                float h = glyph.Size.Y * scale;
                if (w <= 0 || h <= 0) continue;

                DXManager.Draw(glyph.Texture, glyph.SourceRectangle,
                    new RectangleF(cursor, y, w, h), tint, MirBlend.Normal.ToBlendState());
                cursor += (int)Math.Ceiling(w);
                any = true;
            }

            return any; // 全是可忽略字符(如 "Miss")时回退文本
        }

        private void DrawFallback(int x, int y, int alpha)
        {
            if (_fallbackLabel == null)
            {
                _fallbackLabel = new MirLabel
                {
                    AutoSize = true,
                    BackColour = CommonColor.Transparent,
                    ForeColour = Colour,
                    OutLine = true,
                    OutLineColour = CommonColor.Black,
                    Text = Text,
                    Font = FontConst.F8Bold
                };
            }

            _fallbackLabel.Location = new Point(x - _fallbackLabel.Size.Width / 2, y);
            _fallbackLabel.ForeColour = new Color(Colour.R, Colour.G, Colour.B, (byte)alpha);
            _fallbackLabel.Draw();
        }
    }
}
