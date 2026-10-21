using KFramework.MonoGame;
using System;

namespace KFramework.Editor
{
    /// <summary>
    /// 线框 Cube 绘制：CPU 透视投影（与 KCamera 透视数学一致），画进给定的视口矩形。
    /// 引擎目前没有 GPU 3D 网格管线，3D 物体都靠这种「投影顶点 + SpriteBatch 画线」的方式呈现，
    /// 以便用咱们的引擎自验证相机 / 变换正确性。
    /// </summary>
    public static class CubeView
    {
        private const float Half = 3f;   // Cube 基础半边长（再乘物体缩放）

        /// <summary>
        /// 在 <paramref name="rect"/> 内绘制一个线框 Cube。
        /// </summary>
        /// <param name="camPos">相机世界坐标（看向 -Z）</param>
        /// <param name="camRot">相机欧拉旋转（弧度）</param>
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

            var c = new Vector3[8];
            for (int i = 0; i < 8; i++)
            {
                float lx = (i & 1) != 0 ? hx : -hx;
                float ly = (i & 2) != 0 ? hy : -hy;
                float lz = (i & 4) != 0 ? hz : -hz;
                float rx = lx * objRotM.M11 + ly * objRotM.M21 + lz * objRotM.M31;
                float ry = lx * objRotM.M12 + ly * objRotM.M22 + lz * objRotM.M32;
                float rz = lx * objRotM.M13 + ly * objRotM.M23 + lz * objRotM.M33;
                c[i] = new Vector3(objPos.X + rx, objPos.Y + ry, objPos.Z + rz);
            }

            var sp = new (float x, float y, bool ok)[8];
            for (int i = 0; i < 8; i++)
            {
                (float vx, float vy, float vz, float vw) = ClipOf(c[i].X, c[i].Y, c[i].Z, view);
                (float cx, float cy, float cz, float cw) = ClipOf(vx, vy, vz, proj);
                if (cw <= 0f) { sp[i] = (0f, 0f, false); continue; }
                float ndcX = cx / cw;
                float ndcY = cy / cw;
                float sx = (ndcX * 0.5f + 0.5f) * rect.Width + rect.X;
                float sy = (1f - (ndcY * 0.5f + 0.5f)) * rect.Height + rect.Y;
                sp[i] = (sx, sy, true);
            }

            int[] edges = { 0, 1, 1, 2, 2, 3, 3, 0, 4, 5, 5, 6, 6, 7, 7, 4, 0, 4, 1, 5, 2, 6, 3, 7 };
            for (int e = 0; e < edges.Length; e += 2)
            {
                int a = edges[e], d = edges[e + 1];
                if (sp[a].ok && sp[d].ok)
                    DrawLine(b, sp[a].x, sp[a].y, sp[d].x, sp[d].y, new Color(110, 230, 255), 3, tex);
            }

            // 仅画线框；不画角点小方块（避免视图里出现一堆小方块）
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

        private static void DrawLine(SpriteBatch b, float x1, float y1, float x2, float y2, Color c, int thick, Texture2D tex)
        {
            float dx = x2 - x1, dy = y2 - y1;
            float len = MathF.Sqrt(dx * dx + dy * dy);
            float ang = MathF.Atan2(dy, dx);
            b.Draw(tex, new Rectangle(0, 0, (int)len, thick), null, c, ang, new Vector2(0f, thick / 2f));
        }
    }
}
