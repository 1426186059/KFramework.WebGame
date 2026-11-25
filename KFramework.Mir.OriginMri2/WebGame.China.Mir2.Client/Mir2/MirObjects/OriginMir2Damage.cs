using Client.MirControls;
using Client.MirGraphics;
using KFramework.MonoGameExtend;
using WebGame.Mir2.MonoGame.Client;

namespace Client.MirObjects
{
    /// <summary>
    /// 原始传奇（Egret 页游）战斗飘血数字，继承 <see cref="Damage"/> 以便直接放入 MapObject.Damages。
    ///
    /// 数据驱动：按 damageId 从 ExcelTableMgr.TryGetDamage（5config.damage）取配置，
    /// 忠实还原 BloodMovie.show / EntityInfo.showBloodEffect：
    /// - "hurt" + number 逐字符取 BloodFont_json 的 hurt{style}_{char} 帧横拼，数值 0 不显示；
    /// - title&gt;0 时叠加 plus_{title} 图标；
    /// - startarr → stoparr → endarr 三段位移 [dx,dy,ms]，random_x/y_min/max 散布，
    ///   performance=0 时 scaling/100 起始放大回弹、末段 alpha 淡出。
    /// 帧缺失（图集未就绪 / 该样式无此字符）时回退 MirLabel 文本，保证 Miss/Crit 等照常显示。
    /// </summary>
    public sealed class OriginMir2Damage : Damage
    {
        /// <summary>客户端按 DamageType 映射的典型原始 damage id（原版服务端带 id，本工程协议没带）。</summary>
        public const int DefaultHitDamageId = 1;     // number=1（hurt1_* 白色普通伤害，71 项主流）
        public const int DefaultCritDamageId = 12;   // number=1 + 起始放大
        public const int DefaultHealDamageId = 15;   // number=3（JS 取绝对值，回血）

        private static int _sheetRequested;

        private readonly DamageTable _cfg;
        private readonly int _hurtStyle;
        private readonly int _jitterX, _jitterY;
        private readonly long _totalMs;
        private readonly int _offsetX, _offsetY;
        private MirLabel _fallbackLabel;

        public OriginMir2Damage(string text, int duration, Color colour, int damageId)
            : base(text, duration, colour, 50)
        {
            ExcelTableMgr.TryGetDamage(damageId, out _cfg);

            _hurtStyle = _cfg != null && _cfg.number >= 0 ? _cfg.number : 1;
            _offsetX = ParseInt(_cfg?.offsetX);
            _offsetY = ParseInt(_cfg?.offsetY);

            int rxMin = _cfg?.random_x_min ?? 0, rxMax = _cfg?.random_x_max ?? 0;
            int ryMin = _cfg?.random_y_min ?? 0, ryMax = _cfg?.random_y_max ?? 0;
            _jitterX = rxMin != rxMax ? CMain.Random.Next(Math.Min(rxMin, rxMax), Math.Max(rxMin, rxMax) + 1) : rxMin;
            _jitterY = ryMin != ryMax ? CMain.Random.Next(Math.Min(ryMin, ryMax), Math.Max(ryMin, ryMax) + 1) : ryMin;

            _totalMs = Math.Max(1, ExpireTime - StartTime);

            if (System.Threading.Interlocked.Exchange(ref _sheetRequested, 1) == 0)
                _ = Mir2AtlasLibrary.GetSheetAsync("BloodFont_json");
        }

        private static int ParseInt(string s)
        {
            return int.TryParse(s, out int v) ? v : 0;
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
            float progress = Math.Clamp((CMain.Time - StartTime) / (float)_totalMs, 0f, 1f);

            // 三段轨迹：startarr → stoparr → endarr，每段 [dx,dy,ms] 按原始时序线性插值。
            int dx = 0, dy = 0;
            if (_cfg != null)
            {
                GetSegmentOffset(_cfg.startarr, _cfg.stoparr, _cfg.endarr, progress, out dx, out dy);
            }
            else
            {
                // 无配置兜底：左下→右上斜飘
                dx = (int)(progress * 40) - 20;
                dy = -(int)(progress * 50);
            }

            int x = displayLocation.X + _jitterX + dx + _offsetX;
            int y = displayLocation.Y + _jitterY + dy + _offsetY - 75 - Offset;

            // 原始尺寸绘制；performance=0 时起始 scaling 回弹，末段 alpha 淡出。
            float scale = 1f;
            int alpha = 255;
            if (_cfg != null && _cfg.performance == 0 && progress < 0.18f)
            {
                float target = Math.Max(1f, _cfg.scaling / 100f);
                float t = progress / 0.18f;
                scale = target + (1f - target) * t * t;
                alpha = (int)(255 * (progress / 0.06f > 1f ? 1f : progress / 0.06f)); // 开头淡入
            }
            else if (progress > 0.72f)
            {
                float t = (progress - 0.72f) / 0.28f;
                alpha = (int)(255 * (1f - t));
            }
            else if (progress < 0.06f)
            {
                alpha = (int)(255 * (progress / 0.06f));
            }

            if (DrawOrigin(x, y, scale, alpha)) return;
            DrawFallback(x, y, alpha);
        }

        /// <summary>按原始 startarr/stoparr/endarr 的三段 [dx,dy,ms] 时序计算当前累计偏移。</summary>
        private static void GetSegmentOffset(List<List<string>> start, List<List<string>> stop, List<List<string>> end,
            float progress, out int dx, out int dy)
        {
            dx = dy = 0;
            int t1 = SegmentDuration(start), t2 = SegmentDuration(stop), t3 = SegmentDuration(end);
            int total = Math.Max(1, t1 + t2 + t3);
            float elapsed = progress * total;

            var (sDx, sDy) = FirstPoint(start);
            var (pDx, pDy) = FirstPoint(stop);
            var (eDx, eDy) = FirstPoint(end);

            if (elapsed < t1)
            {
                float t = t1 > 0 ? elapsed / t1 : 0f;
                dx = Lerp(0, sDx, t); dy = Lerp(0, sDy, t);
            }
            else if (elapsed < t1 + t2)
            {
                float t = t2 > 0 ? (elapsed - t1) / t2 : 0f;
                dx = Lerp(sDx, pDx, t); dy = Lerp(sDy, pDy, t);
            }
            else
            {
                float t = t3 > 0 ? (elapsed - t1 - t2) / t3 : 0f;
                dx = Lerp(pDx, eDx, t); dy = Lerp(pDy, eDy, t);
            }
        }

        private static int SegmentDuration(List<List<string>> seg)
        {
            if (seg == null || seg.Count == 0 || seg[0].Count < 3) return 0;
            return ParseInt(seg[0][2]);
        }

        private static (int dx, int dy) FirstPoint(List<List<string>> seg)
        {
            if (seg == null || seg.Count == 0 || seg[0].Count < 2) return (0, 0);
            return (ParseInt(seg[0][0]), ParseInt(seg[0][1]));
        }

        private static int Lerp(int a, int b, float t) => (int)(a + (b - a) * t);

        /// <summary>逐字符取图集帧横拼（对齐原始 BloodMovie.addNumber）。false=帧未就绪，回退文本。</summary>
        private bool DrawOrigin(int x, int y, float scale, int alpha)
        {
            if (!Mir2AtlasLibrary.TryGetFrame("BloodFont_json", "hurt" + _hurtStyle + "_0", out var probe) || probe?.Texture == null)
                return false;

            int cursor = x;
            bool any = false;
            Color tint = new Color(Colour.R, Colour.G, Colour.B, (byte)alpha);

            foreach (char ch in Text)
            {
                KSpriteInfo glyph = GetGlyph(_hurtStyle, ch);
                if (glyph == null) continue; // 0/逗号/空格：原始逻辑不绘制

                float w = glyph.Size.X * scale;
                float h = glyph.Size.Y * scale;
                if (w <= 0 || h <= 0) continue;

                DXManager.Draw(glyph.Texture, glyph.SourceRectangle,
                    new RectangleF(cursor, y, w, h), tint, MirBlend.Normal.ToBlendState());
                cursor += (int)Math.Ceiling(w);
                any = true;
            }

            // title 图标（plus_{title}）：原始在数字之前叠加
            if (_cfg != null && _cfg.title > 0 &&
                Mir2AtlasLibrary.TryGetFrame("BloodFont_json", "plus_" + _cfg.title, out var titleFrame) && titleFrame?.Texture != null)
            {
                int tw = (int)titleFrame.Size.X, th = (int)titleFrame.Size.Y;
                DXManager.Draw(titleFrame.Texture, titleFrame.SourceRectangle,
                    new RectangleF(x - tw - 2, y, tw, th), tint, MirBlend.Normal.ToBlendState());
            }

            return any;
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
