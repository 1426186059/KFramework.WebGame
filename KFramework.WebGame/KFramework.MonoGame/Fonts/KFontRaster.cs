using System;

namespace KFramework.MonoGame
{
    /// <summary>
    /// 字形光栅化（纯 C#，替代 Canvas2D 的 fillText + getImageData）：
    /// 把 TrueType 的二次贝塞尔轮廓展平成线段，再用「扫描线 + 纵向超采样」算每像素覆盖率，
    /// 输出白色 + alpha 的 RGBA8，交给 <see cref="KFont"/> 上传纹理，之后由 SpriteBatch（WebGL）绘制。
    /// 填充规则为 TrueType 约定的 nonzero winding。
    /// </summary>
    internal static class KFontRaster
    {
        private struct Segment
        {
            public float X0;
            public float Y0;
            public float X1;
            public float Y1;
        }

        /// <summary>纵向子扫描线数量（等效 4x 过采样抗锯齿）。</summary>
        private const int SubSamples = 4;

        /// <summary>
        /// 把一个字形光栅化进 <paramref name="rgba"/>（宽高为 <paramref name="width"/> × <paramref name="height"/> 的 RGBA8）。
        /// </summary>
        /// <param name="shape">字形轮廓（字体单位）。</param>
        /// <param name="scale">字体单位 → 像素的缩放（size / unitsPerEm）。</param>
        /// <param name="skewTan">斜切系数（tan(角度)），0 为正体。</param>
        /// <param name="originX">字形 x = 0 在格子里的像素位置。</param>
        /// <param name="baselineY">基线在格子里的像素位置。</param>
        /// <param name="boldPixels">加粗：对 alpha 做半径 N 的膨胀（0 为不加粗）。</param>
        public static void Rasterize(GlyphShape shape, float scale, float skewTan, float originX, float baselineY,
                                     int width, int height, byte[] rgba, int boldPixels)
        {
            Array.Clear(rgba, 0, width * height * 4);
            if (shape.IsEmpty) return;

            var segments = new System.Collections.Generic.List<Segment>(256);
            int start = 0;
            foreach (int end in shape.ContourEnds)
            {
                FlattenContour(shape, start, end, scale, skewTan, originX, baselineY, segments);
                start = end + 1;
            }
            if (segments.Count == 0) return;

            var crossings = new System.Collections.Generic.List<(float X, int Dir)>(64);
            var row = new float[width];

            for (int py = 0; py < height; py++)
            {
                Array.Clear(row, 0, width);
                for (int s = 0; s < SubSamples; s++)
                {
                    float yf = py + (s + 0.5f) / SubSamples;
                    crossings.Clear();
                    for (int i = 0; i < segments.Count; i++)
                    {
                        Segment seg = segments[i];
                        // 半开区间判定避免顶点被重复计数；向上穿越记 +1（nonzero winding）
                        if (seg.Y0 > yf && seg.Y1 <= yf)
                            crossings.Add((seg.X0 + (yf - seg.Y0) * (seg.X1 - seg.X0) / (seg.Y1 - seg.Y0), 1));
                        else if (seg.Y1 > yf && seg.Y0 <= yf)
                            crossings.Add((seg.X1 + (yf - seg.Y1) * (seg.X0 - seg.X1) / (seg.Y0 - seg.Y1), -1));
                    }
                    if (crossings.Count < 2) continue;

                    crossings.Sort((a, b) => a.X.CompareTo(b.X));
                    int winding = 0;
                    for (int i = 0; i + 1 < crossings.Count; i++)
                    {
                        winding += crossings[i].Dir;
                        if (winding != 0) AddSpan(row, width, crossings[i].X, crossings[i + 1].X);
                    }
                }

                int rowOffset = py * width * 4;
                for (int px = 0; px < width; px++)
                {
                    int alpha = (int)(row[px] * (255f / SubSamples) + 0.5f);
                    if (alpha > 255) alpha = 255;
                    int o = rowOffset + px * 4;
                    rgba[o] = 255;
                    rgba[o + 1] = 255;
                    rgba[o + 2] = 255;
                    rgba[o + 3] = (byte)alpha;
                }
            }

            if (boldPixels > 0) Dilate(rgba, width, height, boldPixels);
        }

        /// <summary>把水平区间 [x0, x1] 的覆盖长度按像素列累加（面积精确，端点按小数覆盖）。</summary>
        private static void AddSpan(float[] row, int width, float x0, float x1)
        {
            if (x1 <= 0f || x0 >= width) return;
            if (x0 < 0f) x0 = 0f;
            if (x1 > width) x1 = width;
            if (x1 <= x0) return;

            int ix0 = (int)MathF.Floor(x0);
            int ix1 = (int)MathF.Ceiling(x1);
            float leftGap = x0 - ix0;
            float rightGap = ix1 - x1;

            if (ix1 - ix0 <= 1)
            {
                row[ix0] += x1 - x0;
                return;
            }

            row[ix0] += 1f - leftGap;
            for (int i = ix0 + 1; i < ix1 - 1; i++) row[i] += 1f;
            row[ix1 - 1] += 1f - rightGap;
        }

        #region 轮廓展平

        /// <summary>把一条闭合轮廓（点下标 [start, end]）展平成像素坐标的线段。</summary>
        private static void FlattenContour(GlyphShape shape, int start, int end, float scale, float skewTan,
                                          float originX, float baselineY, System.Collections.Generic.List<Segment> output)
        {
            if (start < 0 || end >= shape.Points.Length) return; // 兜底：轮廓下标越界时跳过，不拖垮整串排版
            int count = end - start + 1;
            if (count < 2) return;

            // 1) 旋转到以「曲线上的点」开头，使后续能按 on-off-on 成对处理
            int head = start;
            if (!shape.Points[start].OnCurve)
            {
                head = -1;
                for (int i = end; i >= start; i--)
                {
                    if (shape.Points[i].OnCurve) { head = i; break; }
                }
                if (head < 0) head = start; // 全是控制点：由下面的中点补齐规则兜底
            }

            // 2) 复制一份旋转后的点，并在连续两个控制点之间补一个中点（TrueType 的隐含 on-curve 点）
            var points = new System.Collections.Generic.List<ShapePoint>(count * 2);
            for (int i = 0; i < count; i++) points.Add(shape.Points[start + ((head - start + i) % count)]);
            var normalized = new System.Collections.Generic.List<ShapePoint>(points.Count * 2);
            for (int i = 0; i < points.Count; i++)
            {
                ShapePoint cur = points[i];
                normalized.Add(cur);
                ShapePoint next = points[(i + 1) % points.Count];
                if (!cur.OnCurve && !next.OnCurve)
                {
                    normalized.Add(new ShapePoint
                    {
                        X = (cur.X + next.X) * 0.5f,
                        Y = (cur.Y + next.Y) * 0.5f,
                        OnCurve = true,
                    });
                }
            }
            while (normalized.Count > 0 && !normalized[0].OnCurve)
            {
                ShapePoint first = normalized[0];
                normalized.RemoveAt(0);
                normalized.Add(first);
            }
            if (normalized.Count < 2) return;

            // 3) 逐段展平：相邻两点都是 on-curve → 直线；on-off-on → 二次贝塞尔细分
            for (int i = 0; i < normalized.Count; i++)
            {
                ShapePoint p0 = normalized[i];
                ShapePoint p1 = normalized[(i + 1) % normalized.Count];
                if (!p0.OnCurve) continue;

                if (p1.OnCurve)
                {
                    AddLine(Project(p0, scale, skewTan, originX, baselineY),
                            Project(p1, scale, skewTan, originX, baselineY), output);
                    continue;
                }

                ShapePoint p2 = normalized[(i + 2) % normalized.Count];
                AddQuad(Project(p0, scale, skewTan, originX, baselineY),
                        Project(p1, scale, skewTan, originX, baselineY),
                        Project(p2, scale, skewTan, originX, baselineY), output);
                i++;
            }
        }

        /// <summary>字体单位 → 像素（y 轴翻转；斜切绕基线）。</summary>
        private static (float X, float Y) Project(ShapePoint p, float scale, float skewTan, float originX, float baselineY)
        {
            float sy = p.Y * scale;
            float sx = p.X * scale;
            return (originX + sx + skewTan * sy, baselineY - sy);
        }

        private static void AddLine((float X, float Y) a, (float X, float Y) b,
                                    System.Collections.Generic.List<Segment> output)
        {
            if (a.Y == b.Y) return; // 水平边不产生扫描线交点
            output.Add(new Segment { X0 = a.X, Y0 = a.Y, X1 = b.X, Y1 = b.Y });
        }

        private static void AddQuad((float X, float Y) p0, (float X, float Y) c, (float X, float Y) p1,
                                    System.Collections.Generic.List<Segment> output)
        {
            float approx = MathF.Abs(c.X - p0.X) + MathF.Abs(c.Y - p0.Y) + MathF.Abs(p1.X - c.X) + MathF.Abs(p1.Y - c.Y);
            int steps = (int)(approx * 0.5f);
            if (steps < 4) steps = 4;
            else if (steps > 32) steps = 32;

            float prevX = p0.X;
            float prevY = p0.Y;
            for (int i = 1; i <= steps; i++)
            {
                float t = i / (float)steps;
                float inv = 1f - t;
                float x = inv * inv * p0.X + 2f * inv * t * c.X + t * t * p1.X;
                float y = inv * inv * p0.Y + 2f * inv * t * c.Y + t * t * p1.Y;
                AddLine((prevX, prevY), (x, y), output);
                prevX = x;
                prevY = y;
            }
        }

        #endregion

        #region 加粗

        /// <summary>对 alpha 通道做半径 <paramref name="radius"/> 的膨胀（水平 + 垂直两次滑动最大值）。</summary>
        private static void Dilate(byte[] rgba, int width, int height, int radius)
        {
            int count = width * height;
            byte[] alpha = new byte[count];
            byte[] temp = new byte[count];
            for (int i = 0; i < count; i++) alpha[i] = rgba[i * 4 + 3];

            // 水平
            for (int y = 0; y < height; y++)
            {
                int row = y * width;
                for (int x = 0; x < width; x++)
                {
                    byte max = 0;
                    int from = Math.Max(0, x - radius);
                    int to = Math.Min(width - 1, x + radius);
                    for (int k = from; k <= to; k++)
                    {
                        byte v = alpha[row + k];
                        if (v > max) max = v;
                    }
                    temp[row + x] = max;
                }
            }

            // 垂直
            for (int y = 0; y < height; y++)
            {
                int row = y * width;
                for (int x = 0; x < width; x++)
                {
                    byte max = 0;
                    int from = Math.Max(0, y - radius);
                    int to = Math.Min(height - 1, y + radius);
                    for (int k = from; k <= to; k++)
                    {
                        byte v = temp[k * width + x];
                        if (v > max) max = v;
                    }
                    alpha[row + x] = max;
                }
            }

            for (int i = 0; i < count; i++)
            {
                if (alpha[i] == 0) continue;
                rgba[i * 4] = 255;
                rgba[i * 4 + 1] = 255;
                rgba[i * 4 + 2] = 255;
                rgba[i * 4 + 3] = alpha[i];
            }
        }

        #endregion
    }
}
