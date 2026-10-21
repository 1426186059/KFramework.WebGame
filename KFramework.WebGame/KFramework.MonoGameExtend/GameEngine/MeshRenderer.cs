using System;
using System.Collections.Generic;
using System.Numerics;
using KFramework.MonoGame;
// 消除引擎自定义向量/矩阵 与 System.Numerics 的歧义：Mesh/MeshVertex/KTransform 用引擎自定义 Vector2/Vector3/Vector4/Matrix4x4，
// 而 VertexPositionColorTexture 用的是 System.Numerics.Vector4/Vector2，故把前者显式别名，在构造 VPC 处再转回 System.Numerics。
using Vector2 = KFramework.MonoGame.Vector2;
using Vector3 = KFramework.MonoGame.Vector3;
using Matrix4x4 = KFramework.MonoGame.Matrix4x4;

namespace KFramework.MonoGameExtend
{
    /// <summary>
    /// 自动合批渲染器的统一基类（对标 Unity 的 Renderer / MeshRenderer），同时兼具简单 3D 模型渲染能力。
    /// <para>
    /// 2D 部分：所有参与 <see cref="KCamera"/> 自动合批的渲染组件都继承它，相机据此分类 / 动态合批
    ///（材质、<see cref="RenderQueue"/>、<see cref="SortingOrder"/> 见下方属性；<see cref="KTransform.Draw"/> 由子类如 SpriteRenderer 重写）。
    /// </para>
    /// <para>
    /// 3D 部分：渲染一个简单模型（<see cref="Mesh"/>），用透视相机投影并走<b>硬件深度缓冲</b>做遮挡（不再用画家算法排序）。
    /// 位置用 <see cref="KTransform.LocalPosition"/>（整棵树的世界坐标会被累加）；3D 旋转用 <see cref="MeshRotation"/>、3D 缩放用 <see cref="MeshScale"/>
    /// （<see cref="KTransform"/> 本身只支持 2D 旋转 / 缩放）。
    /// </para>
    /// </summary>
    public class MeshRenderer : KTransform
    {
        // ===== 2D 自动合批契约（KCamera 需要，保持引擎原有字段）=====

        /// <summary>本渲染器期望的材质（对标 Unity 每个 Renderer 身上的 Material）。为 null 时相机用默认材质。</summary>
        public Material? Material { get; set; } = null;

        /// <summary>渲染排序顺序（对标 Unity Renderer.sortingOrder）。</summary>
        public int SortingOrder { get; set; } = 0;

        /// <summary>渲染队列（对标 Unity renderQueue）。相机分类的第一优先级：不同队列之间强制断批。</summary>
        public RenderQueue RenderQueue { get; set; } = RenderQueue.Geometry;

        // ===== 3D 网格渲染 =====

        /// <summary>要渲染的模型。默认是一个立方体。</summary>
        public Mesh Mesh { get; set; } = Mesh.CreateCube(6f);

        /// <summary>模型颜色（逐顶点 Lambert 明暗会在此基础上按光照衰减）。</summary>
        public Color MeshColor { get; set; } = new Color(228, 232, 240);

        /// <summary>3D 旋转（绕 X/Y/Z 轴，弧度）。KTransform 只能绕 Z，故这里单独补一套 3D 旋转。</summary>
        public Vector3 MeshRotation { get; set; } = new Vector3(0f, 0f, 0f);

        /// <summary>3D 缩放（X/Y/Z）。KTransform 只有 XY 缩放，故这里单独补一套 3D 缩放。</summary>
        public Vector3 MeshScale { get; set; } = new Vector3(1f, 1f, 1f);

        /// <summary>平行光方向（世界空间）。</summary>
        public static Vector3 LightDir { get; set; } = NormalizeV(new Vector3(-0.4f, -0.8f, -0.5f));

        /// <summary>
        /// 把模型按当前位置 / 3D 旋转 / 3D 缩放变换到世界空间，逐顶点算 Lambert 明暗，
        /// 再以 <paramref name="mvp"/>（= projection × view，模型矩阵已烘焙进顶点）投到裁剪空间、
        /// 由硬件深度缓冲做遮挡，交给 GPU 绘制。
        /// <para>调用方负责把 <see cref="GraphicsDevice.Viewport"/> 设到目标子区域、并复位 <see cref="DepthStencilState"/>。</para>
        /// </summary>
        public void RenderMesh(GraphicsDevice device, Matrix4x4 view, Matrix4x4 projection, Texture2D tex)
        {
            if (Mesh == null || Mesh.Vertices == null || Mesh.Indices == null) return;

            // 行向量约定（与着色器 gl_Position = uMVP * aPosition 一致）：
            // 模型变换必须「先缩放 → 再绕自身中心（模型原点）旋转 → 最后平移到世界位置」。
            // 行向量下 t*r*s 会变成「先平移再绕世界原点旋转」的公转效果，所以这里显式按此顺序做，
            // 彻底绕开矩阵乘法顺序歧义（也避开 Transform/XFormDir 当前的列向量实现）。
            Matrix4x4 r = Matrix4x4.CreateRotationX(MeshRotation.X)
                        * Matrix4x4.CreateRotationY(MeshRotation.Y)
                        * Matrix4x4.CreateRotationZ(MeshRotation.Z);
            // 行向量：mvp = view × projection（先 View 后 Projection）。
            Matrix4x4 mvp = view * projection;

            float sx = MeshScale.X, sy = MeshScale.Y, sz = MeshScale.Z;
            int vc = Mesh.VertexCount;
            var verts = new VertexPositionColorTexture[vc];
            for (int i = 0; i < vc; i++)
            {
                MeshVertex mv = Mesh.Vertices[i];
                // 1) 缩放（绕模型原点）
                Vector3 p = new Vector3(mv.Position.X * sx, mv.Position.Y * sy, mv.Position.Z * sz);
                // 2) 旋转（行向量 p × R：用 R 的第 1/2/3 行），旋转中心即模型原点 = 物体自身中心
                Vector3 rp = new Vector3(
                    p.X * r.M11 + p.Y * r.M12 + p.Z * r.M13,
                    p.X * r.M21 + p.Y * r.M22 + p.Z * r.M23,
                    p.X * r.M31 + p.Y * r.M32 + p.Z * r.M33);
                // 3) 平移到世界位置
                Vector3 wp = new Vector3(rp.X + LocalPosition.X, rp.Y + LocalPosition.Y, rp.Z + LocalPosition.Z);

                // 法线仅旋转（行向量 n × R，均匀缩放下方向无需逆变换）；LightDir 为世界空间
                Vector3 wn = NormalizeV(new Vector3(
                    mv.Normal.X * r.M11 + mv.Normal.Y * r.M12 + mv.Normal.Z * r.M13,
                    mv.Normal.X * r.M21 + mv.Normal.Y * r.M22 + mv.Normal.Z * r.M23,
                    mv.Normal.X * r.M31 + mv.Normal.Y * r.M32 + mv.Normal.Z * r.M33));
                float nl = Math.Max(0f, wn.X * LightDir.X + wn.Y * LightDir.Y + wn.Z * LightDir.Z);
                float shade = 0.35f + 0.65f * nl;
                byte rr = (byte)(MeshColor.R * shade);
                byte gg = (byte)(MeshColor.G * shade);
                byte bb = (byte)(MeshColor.B * shade);
                verts[i] = ToVpc(new Vector4(wp.X, wp.Y, wp.Z, 1f), new Color(rr, gg, bb), mv.UV);
            }

            ushort[] idx = ConvertIndices(Mesh.Indices);
            device.DrawPrimitives3D(verts, idx, mvp, tex);
        }

        /// <summary>
        /// 画参考地面网格 + XYZ 坐标轴（3D 线框，走同一套硬件深度，会被立方体正确遮挡）。
        /// </summary>
        public static void DrawGrid(GraphicsDevice device, Matrix4x4 view, Matrix4x4 projection, Texture2D tex)
        {
            // 行向量约定：先 View 后 Projection，故 mvp = view × projection。
            Matrix4x4 mvp = view * projection;
            var verts = new List<VertexPositionColorTexture>();
            var idx = new List<ushort>();

            const float floorY = -4f, span = 30f, step = 3f;
            Color gridCol = new Color(40, 55, 80);
            for (float t = -span; t <= span; t += step)
            {
                EmitLine(verts, idx, new Vector3(t, floorY, -span), new Vector3(t, floorY, span), new Vector3(0f, 0f, 1f), 0.06f, gridCol);
                EmitLine(verts, idx, new Vector3(-span, floorY, t), new Vector3(span, floorY, t), new Vector3(1f, 0f, 0f), 0.06f, gridCol);
            }
            EmitLine(verts, idx, new Vector3(0f, 0f, 0f), new Vector3(6f, 0f, 0f), new Vector3(0f, 1f, 0f), 0.1f, new Color(220, 90, 90));
            EmitLine(verts, idx, new Vector3(0f, 0f, 0f), new Vector3(0f, 6f, 0f), new Vector3(1f, 0f, 0f), 0.1f, new Color(90, 220, 110));
            EmitLine(verts, idx, new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 6f), new Vector3(0f, 1f, 0f), 0.1f, new Color(90, 130, 230));

            if (verts.Count > 0)
                device.DrawPrimitives3D(verts.ToArray(), idx.ToArray(), mvp, tex);
        }

        private static void EmitLine(List<VertexPositionColorTexture> verts, List<ushort> idx, Vector3 a, Vector3 b, Vector3 perp, float halfW, Color c)
        {
            int baseIdx = verts.Count;
            Vector3 pa = a + perp * halfW;
            Vector3 pb = a - perp * halfW;
            Vector3 pc = b + perp * halfW;
            Vector3 pd = b - perp * halfW;
            Vector2 uv = new Vector2(0f, 0f);
            verts.Add(ToVpc(new Vector4(pa.X, pa.Y, pa.Z, 1f), c, uv));
            verts.Add(ToVpc(new Vector4(pb.X, pb.Y, pb.Z, 1f), c, uv));
            verts.Add(ToVpc(new Vector4(pc.X, pc.Y, pc.Z, 1f), c, uv));
            verts.Add(ToVpc(new Vector4(pd.X, pd.Y, pd.Z, 1f), c, uv));
            idx.Add((ushort)baseIdx); idx.Add((ushort)(baseIdx + 1)); idx.Add((ushort)(baseIdx + 2));
            idx.Add((ushort)baseIdx); idx.Add((ushort)(baseIdx + 2)); idx.Add((ushort)(baseIdx + 3));
        }

        /// <summary>组合顶点：pos/uv 已是 System.Numerics 类型（与 <see cref="VertexPositionColorTexture"/> 一致），此处仅做封装。</summary>
        private static VertexPositionColorTexture ToVpc(Vector4 pos, Color c, Vector2 uv)
            => new VertexPositionColorTexture(pos, c, uv);

        private static ushort[] ConvertIndices(int[] src)
        {
            var dst = new ushort[src.Length];
            for (int i = 0; i < src.Length; i++)
            {
                if (src[i] < 0 || src[i] > 65535)
                    throw new InvalidOperationException("3D 网格顶点数超过 65535，无法用 ushort 索引绘制。");
                dst[i] = (ushort)src[i];
            }
            return dst;
        }

        // ============ 向量小工具 ============

        /// <summary>把点 (w=1) 用矩阵变换。</summary>
        public static Vector3 Transform(Vector3 point, Matrix4x4 m)
            => new Vector3(
                point.X * m.M11 + point.Y * m.M21 + point.Z * m.M31 + m.M41,
                point.X * m.M12 + point.Y * m.M22 + point.Z * m.M32 + m.M42,
                point.X * m.M13 + point.Y * m.M23 + point.Z * m.M33 + m.M43);

        /// <summary>把方向 (w=0) 用矩阵变换（忽略平移）。</summary>
        public static Vector3 XFormDir(Matrix4x4 m, Vector3 dir)
            => new Vector3(
                dir.X * m.M11 + dir.Y * m.M21 + dir.Z * m.M31,
                dir.X * m.M12 + dir.Y * m.M22 + dir.Z * m.M32,
                dir.X * m.M13 + dir.Y * m.M23 + dir.Z * m.M33);

        public static Vector3 NormalizeV(Vector3 v)
        {
            float len = MathF.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z);
            if (len < 1e-6f) return new Vector3(0f, 0f, 0f);
            float inv = 1f / len;
            return new Vector3(v.X * inv, v.Y * inv, v.Z * inv);
        }
    }
}
