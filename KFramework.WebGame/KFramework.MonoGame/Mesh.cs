using System;

namespace KFramework.MonoGame
{
    /// <summary>网格顶点：位置 + 法线 + UV（仿 Unity Mesh 的顶点属性）。</summary>
    public struct MeshVertex
    {
        public Vector3 Position;
        public Vector3 Normal;
        public Vector2 UV;
    }

    /// <summary>
    /// 仿 Unity 的 Mesh：保存顶点（位置 / 法线 / UV）与三角形索引。
    /// <para>目前提供 <see cref="CreateCube"/> 等内置网格工厂；之后可扩展 SubMesh、蒙皮、法线重算等。</para>
    /// </summary>
    public sealed class Mesh
    {
        public MeshVertex[]? Vertices;
        public int[]? Indices;

        public int VertexCount => Vertices?.Length ?? 0;
        public int IndexCount => Indices?.Length ?? 0;

        /// <summary>
        /// 创建立方体网格：6 个面、每面 4 个独立法线顶点 + 2 个三角形（共 24 顶点 / 12 三角形），
        /// 与 Unity 默认 Cube 的拓扑一致——每面法线独立，所以 Lambert 光照下各面明暗不同。
        /// </summary>
        /// <param name="size">立方体边长（世界单位）。</param>
        public static Mesh CreateCube(float size = 1f)
        {
            float h = size * 0.5f;
            var v = new MeshVertex[24];
            var idx = new int[36];
            int vi = 0, ii = 0;

            void Face(Vector3 n, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                int start = vi;
                v[vi++] = new MeshVertex { Position = a, Normal = n, UV = new Vector2(0f, 0f) };
                v[vi++] = new MeshVertex { Position = b, Normal = n, UV = new Vector2(1f, 0f) };
                v[vi++] = new MeshVertex { Position = c, Normal = n, UV = new Vector2(1f, 1f) };
                v[vi++] = new MeshVertex { Position = d, Normal = n, UV = new Vector2(0f, 1f) };
                idx[ii++] = start; idx[ii++] = start + 1; idx[ii++] = start + 2;
                idx[ii++] = start; idx[ii++] = start + 2; idx[ii++] = start + 3;
            }

            // 各面顶点按「朝外 CCW」排列；法线为面外法线
            Face(new Vector3(1f, 0f, 0f),  new Vector3(h, -h, -h), new Vector3(h, h, -h), new Vector3(h, h, h), new Vector3(h, -h, h));
            Face(new Vector3(-1f, 0f, 0f), new Vector3(-h, -h, h), new Vector3(-h, h, h), new Vector3(-h, h, -h), new Vector3(-h, -h, -h));
            Face(new Vector3(0f, 1f, 0f),  new Vector3(-h, h, -h), new Vector3(-h, h, h), new Vector3(h, h, h), new Vector3(h, h, -h));
            Face(new Vector3(0f, -1f, 0f), new Vector3(-h, -h, h), new Vector3(-h, -h, -h), new Vector3(h, -h, -h), new Vector3(h, -h, h));
            Face(new Vector3(0f, 0f, 1f),  new Vector3(h, -h, h), new Vector3(h, h, h), new Vector3(-h, h, h), new Vector3(-h, -h, h));
            Face(new Vector3(0f, 0f, -1f), new Vector3(-h, -h, -h), new Vector3(-h, h, -h), new Vector3(h, h, -h), new Vector3(h, -h, -h));

            return new Mesh { Vertices = v, Indices = idx };
        }

        /// <summary>创建一个位于 XZ 平面、朝上（+Y）的地面网格（用于编辑器 Scene 视图）。</summary>
        public static Mesh CreatePlane(float size = 1f)
        {
            float h = size * 0.5f;
            var v = new MeshVertex[4];
            v[0] = new MeshVertex { Position = new Vector3(-h, 0f, -h), Normal = new Vector3(0f, 1f, 0f), UV = new Vector2(0f, 0f) };
            v[1] = new MeshVertex { Position = new Vector3(h, 0f, -h),  Normal = new Vector3(0f, 1f, 0f), UV = new Vector2(1f, 0f) };
            v[2] = new MeshVertex { Position = new Vector3(h, 0f, h),    Normal = new Vector3(0f, 1f, 0f), UV = new Vector2(1f, 1f) };
            v[3] = new MeshVertex { Position = new Vector3(-h, 0f, h),  Normal = new Vector3(0f, 1f, 0f), UV = new Vector2(0f, 1f) };
            var idx = new int[] { 0, 1, 2, 0, 2, 3 };
            return new Mesh { Vertices = v, Indices = idx };
        }
    }
}
