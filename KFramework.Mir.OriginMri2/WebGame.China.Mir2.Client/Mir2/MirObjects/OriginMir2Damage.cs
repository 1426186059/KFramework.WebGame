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
    /// - startarr/stoparr/endarr 的 [dx,dy,ms] 按受击方向 side 取第 0/1/2 组（1=攻击者在右，2=攻击者在左）；
    /// - performance=0 按原始四阶段：起点移动并放大 → 50ms 缩放回弹 → 等待 → 飘向终点并淡出；
    /// - performance=1 直接起点飘向终点并淡出。
    /// 帧缺失（图集未就绪 / 该样式无此字符）时回退 MirLabel 文本，保证 Miss/Crit 等照常显示。
    /// </summary>
    public sealed class OriginMir2Damage : Damage
    {
        /// <summary>客户端按 DamageType 映射的典型原始 damage id（原版服务端带 id，本工程协议没带）。</summary>
        public const int DefaultHitDamageId = 1;     // number=1（hurt1_* 白色普通伤害，71 项主流）
        public const int DefaultCritDamageId = 12;   // number=1 + 起始放大
        public const int DefaultHealDamageId = 15;   // number=3（JS 取绝对值，回血）

        /// <summary>飘血基准高度：相对脚底 DrawLocation 上移像素（75 基础上 +20 上移）。可配置。</summary>
        public static int BaseYOffset = 95;

        private const int ScaleBackMs = 50;
        private const float FadeHoldRatio = 0.10f;
        private const float FadeInRatio = 0.12f;

        private static int _sheetRequested;

        private readonly DamageTable _cfg;
        private readonly int _side;
        private readonly int _hurtStyle;
        private int _jitterX, _jitterY;
        private readonly long _totalMs;
        private readonly int _offsetX, _offsetY;
        private readonly int _startDx, _startDy, _phaseStart;
        private readonly int _stopDx, _stopDy, _phaseWait;
        private readonly int _endDx, _endDy, _phaseEnd;
        private MirLabel _fallbackLabel;

        public OriginMir2Damage(string text, int duration, Color colour, int damageId, int side = 0)
            : base(text, duration, colour, 50)
        {
            _side = side;
            ExcelTableMgr.TryGetDamage(damageId, out _cfg);

            _hurtStyle = _cfg != null && _cfg.number >= 0 ? _cfg.number : 1;
            _offsetX = ParseInt(_cfg?.offsetX);
            _offsetY = ParseInt(_cfg?.offsetY);

            GetPoint(_cfg?.startarr, _side, out _startDx, out _startDy, out _phaseStart);
            GetPoint(_cfg?.stoparr, _side, out _stopDx, out _stopDy, out _phaseWait);
            GetPoint(_cfg?.endarr, _side, out _endDx, out _endDy, out _phaseEnd);

            int configuredTotal = 0;
            if (_cfg != null)
            {
                configuredTotal = _cfg.performance == 0
                    ? _phaseStart + ScaleBackMs + _phaseWait + _phaseEnd
                    : _phaseStart + _phaseEnd;
            }

            _totalMs = configuredTotal > 0 ? configuredTotal : Math.Max(1, ExpireTime - StartTime);
            ExpireTime = StartTime + _totalMs;
            Factor = _totalMs / (double)Math.Max(1, Distance);

            SetJitter();

            if (System.Threading.Interlocked.Exchange(ref _sheetRequested, 1) == 0)
            {
                _ = Mir2AtlasLibrary.GetSheetAsync("BloodFont_json");
            }
        }

        private void SetJitter()
        {
            int rxMin = _cfg?.random_x_min ?? 0;
            int rxMax = _cfg?.random_x_max ?? 0;
            int ryMin = _cfg?.random_y_min ?? 0;
            int ryMax = _cfg?.random_y_max ?? 0;

            // 原始 JS：先按同一个 random 选 min/max 组合，再分别乘两个独立 random。
            double pick = CMain.Random.NextDouble();
            double mx = CMain.Random.NextDouble();
            double my = CMain.Random.NextDouble();
            int baseX = pick > 0.5 ? Math.Max(rxMin, rxMax) : Math.Min(rxMin, rxMax);
            int baseY = pick > 0.5 ? Math.Max(ryMin, ryMax) : Math.Min(ryMin, ryMax);
            _jitterX = (int)(baseX * mx);
            _jitterY = (int)(baseY * my);
        }

        private static int ParseInt(string s)
        {
            return int.TryParse(s, out int v) ? v : 0;
        }

        private static KSpriteInfo GetGlyph(int style, char ch, out string key)
        {
            key = string.Empty;
            if (ch == '+')
            {
                key = "hurt" + style + "_+";
            }
            else if (ch == '-')
            {
                key = "hurt" + style + "_-";
            }
            else if (ch >= '0' && ch <= '9')
            {
                key = "hurt" + style + "_" + ch;
            }
            else
            {
                return null; // 逗号/空格/字母：原始 addNumber 不绘制，交给回退文本
            }

            return Mir2AtlasLibrary.TryGetFrame("BloodFont_json", key, out var frame) ? frame : null;
        }

        public override void Draw(Point displayLocation)
        {
            float progress = Math.Clamp((CMain.Time - StartTime) / (float)_totalMs, 0f, 1f);
            float elapsed = progress * _totalMs;

            GetMotion(elapsed, out int dx, out int dy, out float scale, out float motionAlpha);

            int x = displayLocation.X + _offsetX + dx;
            int y = displayLocation.Y + _offsetY + dy - BaseYOffset - Offset;
            int alpha = (int)(GetUserAlpha(progress) * motionAlpha);

            if (DrawOrigin(x, y, scale, alpha))
            {
                return;
            }

            DrawFallback(x, y, alpha);
        }

        /// <summary>按原始 Tween 四阶段计算当前位置、缩放与运动透明度。</summary>
        private void GetMotion(float elapsed, out int dx, out int dy, out float scale, out float motionAlpha)
        {
            dx = _startDx;
            dy = _startDy;
            scale = 1f;
            motionAlpha = 1f;

            if (_cfg == null)
            {
                float t = Math.Clamp(elapsed / Math.Max(1, _totalMs), 0f, 1f);
                dx = (int)(t * 40) - 20;
                dy = -(int)(t * 50);
                motionAlpha = 1f - t;
                return;
            }

            float targetScale = Math.Max(1f, _cfg.scaling / 100f);
            int stopX = _startDx + _stopDx + _jitterX;
            int stopY = _startDy + _stopDy + _jitterY;
            int endX = _startDx + _endDx + _jitterX;
            int endY = _startDy + _endDy + _jitterY;

            if (_cfg.performance == 0)
            {
                if (elapsed < _phaseStart)
                {
                    float t = SineIn(_phaseStart > 0 ? elapsed / _phaseStart : 0f);
                    dx = Lerp(_startDx, stopX, t);
                    dy = Lerp(_startDy, stopY, t);
                    scale = LerpFloat(1f, targetScale, t);
                    return;
                }

                float afterStart = elapsed - _phaseStart;
                if (afterStart < ScaleBackMs)
                {
                    float t = SineIn((float)afterStart / ScaleBackMs);
                    dx = stopX;
                    dy = stopY;
                    scale = LerpFloat(targetScale, 1f, t);
                    return;
                }

                float afterScale = afterStart - ScaleBackMs;
                if (afterScale < _phaseWait)
                {
                    dx = stopX;
                    dy = stopY;
                    return;
                }

                float endT = _phaseEnd > 0 ? Math.Clamp((afterScale - _phaseWait) / _phaseEnd, 0f, 1f) : 1f;
                float e = SineIn(endT);
                dx = Lerp(stopX, endX, e);
                dy = Lerp(stopY, endY, e);
                motionAlpha = 1f - e;
                return;
            }

            float moveT = SineIn(_phaseStart > 0 ? Math.Clamp(elapsed / _phaseStart, 0f, 1f) : 1f);
            dx = Lerp(_startDx, endX, moveT);
            dy = Lerp(_startDy, endY, moveT);
            motionAlpha = 1f - moveT;
        }

        /// <summary>用户要求的额外淡入：开头先完全透明保持一段时间，再淡入到不透明。</summary>
        private static int GetUserAlpha(float progress)
        {
            float holdEnd = FadeHoldRatio;
            float fadeEnd = FadeHoldRatio + FadeInRatio;
            if (progress < holdEnd)
            {
                return 0;
            }

            if (progress < fadeEnd)
            {
                return (int)(255 * ((progress - holdEnd) / FadeInRatio));
            }

            return 255;
        }

        /// <summary>按 side 取 [dx,dy,ms]；side=0/1/2 对应原始三组，越界时回落到第 0 组。</summary>
        private static void GetPoint(List<List<string>> seg, int side, out int dx, out int dy, out int duration)
        {
            dx = 0;
            dy = 0;
            duration = 0;
            if (seg == null || seg.Count == 0)
            {
                return;
            }

            int index = side;
            if (index < 0 || index >= seg.Count)
            {
                index = 0;
            }

            List<string> row = seg[index];
            if (row.Count > 0)
            {
                dx = ParseInt(row[0]);
            }

            if (row.Count > 1)
            {
                dy = ParseInt(row[1]);
            }

            if (row.Count > 2)
            {
                duration = Math.Max(0, ParseInt(row[2]));
            }
        }

        private static float SineIn(float t)
        {
            t = Math.Clamp(t, 0f, 1f);
            return (float)(1.0 - Math.Cos(t * Math.PI / 2.0));
        }

        private static int Lerp(int a, int b, float t)
        {
            return (int)(a + (b - a) * t);
        }

        private static float LerpFloat(float a, float b, float t)
        {
            return a + (b - a) * t;
        }

        /// <summary>逐字符取图集帧横拼（对齐原始 BloodMovie.addNumber）。false=帧未就绪，回退文本。</summary>
        private bool DrawOrigin(int x, int y, float scale, int alpha)
        {
            if (!Mir2AtlasLibrary.TryGetFrame("BloodFont_json", "hurt" + _hurtStyle + "_0", out var probe) || probe?.Texture == null)
            {
                return false;
            }

            int cursor = x;
            bool any = false;
            Color tint = new Color(Colour.R, Colour.G, Colour.B, (byte)alpha);

            foreach (char ch in Text)
            {
                KSpriteInfo glyph = GetGlyph(_hurtStyle, ch, out string key);
                if (glyph == null)
                {
                    continue; // 0/逗号/空格：原始逻辑不绘制
                }

                // 用逻辑宽度推进光标；绘制时用裁剪后的真实矩形 + offX/offY，
                // 否则 hurt1_- 这种 15x5 的裁剪帧会被拉伸到 15x19，出现黑块/阴影。
                float advance = glyph.Size.X * scale;
                if (advance <= 0)
                {
                    continue;
                }

                DrawGlyphFrame(glyph, key, cursor, y, scale, tint);
                cursor += (int)Math.Ceiling(advance);
                any = true;
            }

            // title 图标（plus_{title}）：原始在数字之前叠加
            if (_cfg != null && _cfg.title > 0)
            {
                string titleKey = "plus_" + _cfg.title;
                if (Mir2AtlasLibrary.TryGetFrame("BloodFont_json", titleKey, out var titleFrame) && titleFrame?.Texture != null)
                {
                    float titleWidth = titleFrame.Size.X * scale;
                    DrawGlyphFrame(titleFrame, titleKey, x - titleWidth - 2, y, scale, tint);
                }
            }

            return any;
        }

        /// <summary>按 Egret 裁剪帧的真实尺寸与 offX/offY 绘制，避免把 Trim 后的子图拉伸回虚拟画布。</summary>
        private static void DrawGlyphFrame(KSpriteInfo glyph, string key, float x, float y, float scale, Color tint)
        {
            if (glyph == null || glyph.Texture == null)
            {
                return;
            }

            float w = glyph.SourceRectangle.Width * scale;
            float h = glyph.SourceRectangle.Height * scale;
            if (w <= 0 || h <= 0)
            {
                return;
            }

            Mir2AtlasLibrary.TryGetFrameAnchor("BloodFont_json", key, out var anchor);
            DXManager.Draw(glyph.Texture, glyph.SourceRectangle,
                new RectangleF(x + anchor.X * scale, y + anchor.Y * scale, w, h),
                tint, MirBlend.Normal.ToBlendState());
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
