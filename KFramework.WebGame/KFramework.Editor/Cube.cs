using KFramework.MonoGame;
using System;
using System.Collections.Generic;

namespace KFramework.Editor
{
    /// <summary>
    /// 立方体渲染：CPU 透视投影 + 画家算法（远面先画、近面覆盖）+ Lambert 明暗。
    /// <para>
    /// 说明：本引擎没有“GPU 三角网格 + 硬件深度缓冲”的立方体渲染器（KCamera 的不透明排序本质也是画家算法）。
    /// 因此这里对标 Unity 的做法是：把 6 个面全部画出来、按深度排序、用面法线做 Lambert 光照，
    /// 让同一白色立方体因朝向不同呈现 6 个面的明暗层次（Unity 默认 Lit 材质的观感）。
    /// 实心不透明立方体从任何角度看本来就只露出 3 个面（另 3 个被遮挡），画家算法保证遮挡正确。
    /// </para>
    /// </summary>
    public static class Cube
    {
        private const float Half = 3f;   // Cube 基础半边长（再乘物体缩放）

        /// <summary>在 <paramref name="rect"/> 内绘制一个实心立方体（6 面填充 + 画家算法 + 棱线）。</summary>
        public static void Draw(SpriteBatch b, Rectangle rect, float fovDeg, float near, float far,
            Vector3 camPos, Vector3 camRot, Vector3 objPos, Vector3 objRot, Vector3 objScale, Texture2D tex)
        {
            float aspect = rect.Width / (float)rect.Height;
            float fov = fovDeg * MathF.PI / 180f;
            Matrix4x4 proj = Matrix4x4.CreatePerspectiveFieldOfView(fov, aspect, near, far);

            // 视图矩阵 = 相机世界变换的逆（行向量约定：view = T(-camPos) · R(-camRot)）
            Matrix4x4 rotInv = Matrix4x4.CreateRotationZ(-camRot.Z) * Matrix4x4.CreateRotationY(-camRot.Y) * Matrix4x4.CreateRotationX(-camRot.X);
            Matrix4x4 view = Matrix4x4.CreateTranslation(-camPos.X, -camPos.Y, -camPos.Z) * rotInv;

            Matrix4x4 objRotM = Matrix4x4.CreateRotationX(objRot.X) * Matrix4x4.CreateRotationY(objRot.Y) * Matrix4x4.CreateRotationZ(objRot.Z);
            float hx = Half * objScale.X, hy = Half * objScale.Y, hz = Half * objScale.Z;

            var sp = new (float x, float y, bool ok)[8];   // 屏幕投影
            var vs = new Vector3[8];                        // 视图空间坐标（用于深度排序）
            for (int i = 0; i < 8; i++)
            {
                float lx = (i & 1) != 0 ? hx : -hx;
                float ly = (i & 2) != 0 ? hy : -hy;
                float lz = (i & 4) != 0 ? hz : -hz;
                float rx = lx * objRotM.M11 + ly * objRotM.M21 + lz * objRotM.M31;
                float ry = lx * objRotM.M12 + ly * objRotM.M22 + lz * objRotM.M32;
                float rz = lx * objRotM.M13 + ly * objRotM.M23 + lz * objRotM.M33;
                Vector3 wp = new Vector3(objPos.X + rx, objPos.Y + ry, objPos.Z + rz);

                (float vx, float vy, float vz, float vw) = ClipOf(wp.X, wp.Y, wp.Z, view);
                vs[i] = new Vector3(vx, vy, vz);
                (float cx, float cy, float cz, float cw) = ClipOf(vx, vy, vz, proj);
                if (cw <= 0f) { sp[i] = (0f, 0f, false); continue; }
                float ndcX = cx / cw;
                float ndcY = cy / cw;
                float sx = (ndcX * 0.5f + 0.5f) * rect.Width + rect.X;
                float sy = (1f - (ndcY * 0.5f + 0.5f)) * rect.Height + rect.Y;
                sp[i] = (sx, sy, true);
            }

            // 6 个面：4 个角点（CCW 绕面一圈）+ 世界空间（物体局部）外法线
            var faces = new[]
            {
                (a:1,b:3,c:7,d:5, n:new Vector3( 1,0,0)),   // +X
                (a:0,b:2,c:6,d:4, n:new Vector3(-1,0,0)),   // -X
                (a:2,b:3,c:7,d:6, n:new Vector3(0, 1,0)),   // +Y
                (a:0,b:4,c:5,d:1, n:new Vector3(0,-1,0)),   // -Y
                (a:4,b:5,c:7,d:6, n:new Vector3(0,0, 1)),   // +Z
                (a:0,b:1,c:3,d:2, n:new Vector3(0,0,-1)),   // -Z
            };

            // 收集全部 6 个面（跳过有顶点落在相机背面的面），按深度从远到近排序（画家算法）
            var order = new List<(float depth, int fi)>();
            for (int fi = 0; fi < faces.Length; fi++)
            {
                var f = faces[fi];
                if (!sp[f.a].ok || !sp[f.b].ok || !sp[f.c].ok || !sp[f.d].ok) continue;
                float depth = (vs[f.a].Z + vs[f.b].Z + vs[f.c].Z + vs[f.d].Z) * 0.25f;
                order.Add((depth, fi));
            }
            order.Sort((p, q) => p.depth.CompareTo(q.depth));   // 升序：最负（最远）先画

            // 平行光方向（世界空间），用于 Lambert 明暗，让 6 个面呈现层次
            Vector3 lightDir = Normalize(new Vector3(0.4f, 1f, 0.6f));

            foreach (var o in order)
            {
                var f = faces[o.fi];
                // 面法线经历物体自身旋转，得到世界法线
                (float nx, float ny, float nz) = XFormDir(objRotM, f.n.X, f.n.Y, f.n.Z);
                float nl = Math.Max(0f, nx * lightDir.X + ny * lightDir.Y + nz * lightDir.Z);
                float bright = 0.35f + 0.65f * nl;   // 环境光 0.35 + 漫反射
                byte cr = (byte)(220f * bright), cg = (byte)(225f * bright), cb = (byte)(235f * bright);
                FillQuad(b, tex, rect, sp[f.a], sp[f.b], sp[f.c], sp[f.d], new Color(cr, cg, cb));
            }

            // 棱（画在填充之上，便于分辨各个面）
            int[] edges = { 0,1,1,2,2,3,3,0,4,5,5,6,6,7,7,4,0,4,1,5,2,6,3,7 };
            for (int e = 0; e < edges.Length; e += 2)
            {
                int a = edges[e], d = edges[e + 1];
                if (sp[a].ok && sp[d].ok)
                    DrawLine(b, sp[a].x, sp[a].y, sp[d].x, sp[d].y, new Color(60, 80, 120), 2, tex);
            }
        }

        // ===== 场景辅助：地面网格 + XYZ 坐标轴（编辑器 Scene 视图用，区分于 Game 视图） =====
        public static void DrawGrid(SpriteBatch b, Rectangle rect, float fovDeg, float near, float far,
            Vector3 camPos, Vector3 camRot, Texture2D tex)
        {
            float aspect = rect.Width / (float)rect.Height;
            float fov = fovDeg * MathF.PI / 180f;
            Matrix4x4 proj = Matrix4x4.CreatePerspectiveFieldOfView(fov, aspect, near, far);
            Matrix4x4 rotInv = Matrix4x4.CreateRotationZ(-camRot.Z) * Matrix4x4.CreateRotationY(-camRot.Y) * Matrix4x4.CreateRotationX(-camRot.X);
            Matrix4x4 view = Matrix4x4.CreateTranslation(-camPos.X, -camPos.Y, -camPos.Z) * rotInv;

            Vector3? Project(Vector3 wp)
            {
                var (vx, vy, vz, vw) = ClipOf(wp.X, wp.Y, wp.Z, view);
                var (cx, cy, cz, cw) = ClipOf(vx, vy, vz, proj);
                if (cw <= 0f) return null;
                float sx = (cx / cw * 0.5f + 0.5f) * rect.Width + rect.X;
                float sy = (1f - (cy / cw * 0.5f + 0.5f)) * rect.Height + rect.Y;
                return new Vector3(sx, sy, 0f);
            }

            const float floorY = -4f, span = 30f, step = 3f;
            for (float t = -span; t <= span; t += step)
            {
                var a = Project(new Vector3(t, floorY, -span));
                var c = Project(new Vector3(t, floorY, span));
                var d = Project(new Vector3(-span, floorY, t));
                var e = Project(new Vector3(span, floorY, t));
                if (a.HasValue && c.HasValue) DrawLine(b, a.Value.X, a.Value.Y, c.Value.X, c.Value.Y, new Color(40, 55, 80), 1, tex);
                if (d.HasValue && e.HasValue) DrawLine(b, d.Value.X, d.Value.Y, e.Value.X, e.Value.Y, new Color(40, 55, 80), 1, tex);
            }
            // 坐标轴（原点出发）：X 红 / Y 绿 / Z 蓝
            var o = Project(Vector3.Zero);
            if (o.HasValue)
            {
                var xx = Project(new Vector3(6, 0, 0));
                var yy = Project(new Vector3(0, 6, 0));
                var zz = Project(new Vector3(0, 0, 6));
                if (xx.HasValue) DrawLine(b, o.Value.X, o.Value.Y, xx.Value.X, xx.Value.Y, new Color(220, 90, 90), 2, tex);
                if (yy.HasValue) DrawLine(b, o.Value.X, o.Value.Y, yy.Value.X, yy.Value.Y, new Color(90, 220, 110), 2, tex);
                if (zz.HasValue) DrawLine(b, o.Value.X, o.Value.Y, zz.Value.X, zz.Value.Y, new Color(90, 130, 230), 2, tex);
            }
        }

        // 行向量约定：p × M（输入点 w=1）。返回 (x,y,z,w)。
        private static (float x, float y, float z, float w) ClipOf(float x, float y, float z, Matrix4x4 m)
        {
            return (
                x * m.M11 + y * m.M12 + z * m.M13 + m.M14,
                x * m.M21 + y * m.M22 + z * m.M23 + m.M24,
                x * m.M31 + y * m.M32 + z * m.M33 + m.M34,
                x * m.M41 + y * m.M42 + z * m.M43 + m.M44);
        }

        // 仅用 3×3 部分变换方向向量（忽略平移）
        private static (float, float, float) XFormDir(Matrix4x4 m, float x, float y, float z) =>
            (x * m.M11 + y * m.M12 + z * m.M13,
             x * m.M21 + y * m.M22 + z * m.M23,
             x * m.M31 + y * m.M32 + z * m.M33);

        private static Vector3 Normalize(Vector3 v)
        {
            float l = MathF.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z);
            return l > 1e-6f ? new Vector3(v.X / l, v.Y / l, v.Z / l) : new Vector3(0f, 1f, 0f);
        }

        private static void FillQuad(SpriteBatch b, Texture2D tex, Rectangle clip,
            (float x, float y, bool ok) p0, (float x, float y, bool ok) p1,
            (float x, float y, bool ok) p2, (float x, float y, bool ok) p3, Color col)
        {
            FillTriangle(b, tex, clip, p0, p1, p2, col);
            FillTriangle(b, tex, clip, p0, p2, p3, col);
        }

        // 扫描线填充三角面（用 1px 高的矩形叠加成实心面，裁剪到视口内）
        private static void FillTriangle(SpriteBatch b, Texture2D tex, Rectangle clip,
            (float x, float y, bool ok) p0, (float x, float y, bool ok) p1, (float x, float y, bool ok) p2, Color col)
        {
            if (p0.y > p1.y) (p0, p1) = (p1, p0);
            if (p0.y > p2.y) (p0, p2) = (p2, p0);
            if (p1.y > p2.y) (p1, p2) = (p2, p1);
            float y0 = p0.y, y1 = p1.y, y2 = p2.y;
            if (y2 - y0 < 0.5f) return;

            for (float y = MathF.Ceiling(y0); y <= y2; y += 1f)
            {
                if (y < clip.Y || y >= clip.Y + clip.Height) continue;
                float tA = (y - y0) / (y2 - y0);
                float xA = p0.x + (p2.x - p0.x) * tA;
                float xB;
                if (y < y1)
                {
                    float tB = (y - y0) / (y1 - y0);
                    xB = p0.x + (p1.x - p0.x) * tB;
                }
                else
                {
                    float tB = (y - y1) / (y2 - y1);
                    xB = p1.x + (p2.x - p1.x) * tB;
                }
                float xs = Math.Min(xA, xB), xe = Math.Max(xA, xB);
                xs = Math.Max(xs, clip.X);
                xe = Math.Min(xe, clip.X + clip.Width);
                int ix = (int)xs;
                int iw = (int)(xe - xs);
                if (iw > 0) b.Draw(tex, new Rectangle(ix, (int)y, iw, 1), null, col);
            }
        }

        private static void DrawLine(SpriteBatch b, float x1, float y1, float x2, float y2, Color c, int thick, Texture2D tex)
        {
            float dx = x2 - x1, dy = y2 - y1;
            float len = MathF.Sqrt(dx * dx + dy * dy);
            float ang = MathF.Atan2(dy, dx);
            // 目标矩形定位到线段起点 (x1,y1)，再以左中点为轴旋转 len 长度 → 线段落在两顶点之间
            b.Draw(tex, new Rectangle((int)x1, (int)y1, (int)len, thick), null, c, ang, new Vector2(0f, thick / 2f));
        }
    }
}
