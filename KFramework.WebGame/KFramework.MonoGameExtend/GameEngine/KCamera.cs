using KFramework.MonoGameExtend;
using KFramework.MonoGame;
using System;
using System.Collections.Generic;

// 相机主体：视野/屏幕适配/相机矩阵/坐标转换（渲染逻辑见 KCamera.Render.cs 的 partial 分部）。
public partial class KCamera : KTransform
{
    // 相机视野的垂直半高（世界单位），类似 Unity 的 orthographicSize
    // 默认值 = 设计高度 / 2，让设计分辨率下 1 世界单位 = 1 像素
    public float OrthographicSize = 540f;
    // 背景色（清屏色，由 ClearOnRender 决定是否在渲染前清屏）
    public Color BackgroundColor = Color.CornflowerBlue;

    // ===================== 自动合批（v1）：把「渲染目标 + 清屏 + Begin/End」绑成一个单元 =====================
    // 对标 Unity：Camera.targetTexture（null=画布）、camera.depth（渲染顺序）、相机矩阵即 Begin 的 transformMatrix。
    // 具体渲染入口见 KCamera.Render.cs（Render / RenderAuto / RenderRoot / RenderScene）。

    /// <summary>渲染目标；null = 画布（屏幕）。切目标 + 清屏 + Begin/End 全部由渲染入口自动完成，
    /// 业务代码不再手写 SetRenderTarget / Clear / Begin / End。</summary>
    public RenderTarget2D? Target = null;

    /// <summary>渲染顺序，越小越先画（照 Unity camera.depth）。多个相机按此排序，离屏相机通常给负值。</summary>
    public int Depth = 0;

    /// <summary>材质（可为 null → 用默认材质）。一个相机一个材质，批内不变。</summary>
    public Material? Material = null;

    /// <summary>排序模式（默认 Deferred，不打乱插入顺序）。</summary>
    public SpriteSortMode SortMode = SpriteSortMode.Deferred;

    /// <summary>渲染开始前是否清屏（离屏 PreserveContents 场景设 false）。默认 true。</summary>
    public bool ClearOnRender = true;

    /// <summary>在遍历场景之前先画的内容（例如把上一步离屏 RT 当纹理贴到屏幕）。在已开的批内执行。</summary>
    public Action<SpriteBatch>? PreDraw = null;

    // ===================== 层剔除（v2）：每个相机只渲染自己 CullingMask 内的层 =====================

    /// <summary>层遮罩（对标 Unity camera.cullingMask，默认 ~0u = 全部层）。
    /// 仅当 <c>(1u &lt;&lt; node.Layer) &amp; CullingMask != 0</c> 的节点会被本相机绘制。</summary>
    public uint CullingMask = ~0u;

    private Material? _defaultMaterial;

    // 当前实际屏幕尺寸
    public Vector2 ScreenResolution { get; private set; }
    // 宽高比
    public float Aspect => ScreenResolution.X / ScreenResolution.Y;
    // 可视范围（世界单位）
    public float ViewHeight => OrthographicSize * 2f;
    public float ViewWidth => OrthographicSize * 2f * Aspect;

    // 缩放系数：设计分辨率 → 实际屏幕
    public float ScaleCoef { get; private set; }

    // 设计分辨率
    public Vector2 DesignResolution { get; private set; }

    private static KCamera _cacheMainCamera;
    public static KCamera Main
    {
        get
        {
            if (_cacheMainCamera == null)
            {
                _cacheMainCamera = new KCamera();
            }
            return _cacheMainCamera;
        }
    }

    public KCamera()
    {
        DesignResolution = new Vector2(1920, 1080);
    }

    /// <summary>
    /// 初始化相机，设置设计分辨率
    /// </summary>
    public void Initialize(Vector2 designResolution)
    {
        DesignResolution = designResolution;
        // 默认让设计高度的一半 = OrthographicSize
        OrthographicSize = designResolution.Y / 2f;
        UpdateScreenAdaptation();
    }

    /// <summary>
    /// 每帧更新，在绘制前调用
    /// </summary>
    public override void Update()
    {
        var viewport = KSceneMgr.Game.GraphicsDevice.Viewport;
        var newScreenRes = new Vector2(viewport.Width, viewport.Height);

        // 屏幕尺寸变化时重新计算适配
        if (newScreenRes != ScreenResolution)
        {
            ScreenResolution = newScreenRes;
            UpdateScreenAdaptation();
        }

       // SetDirty();
    }

    /// <summary>
    /// 更新屏幕适配参数（缩放系数 + 偏移）
    /// </summary>
    private void UpdateScreenAdaptation()
    {
        if (ScreenResolution == Vector2.Zero) return;

        // 按高度适配：让设计高度始终填满屏幕高度
        // 如果你想按宽度适配，改成 ScreenResolution.X / DesignResolution.X
        ScaleCoef = ScreenResolution.Y / (OrthographicSize * 2f);
        //    (ScreenResolution.Y - scaledHeight) / 2f
        //);
    }

    /// <summary>视图矩阵（世界 → 相机空间，纯视图变换，不含投影）。对标 Unity 的 worldToCameraMatrix。
    /// 离屏 / 自动合批渲染请使用 <see cref="World_To_Camera_Matrix"/>（视图 + 正交投影，直接喂 SpriteBatch）。</summary>
    public Matrix4x4 ViewMatrix
    {
        get
        {
            // 纯视图变换：先把世界平移到「相机位置的反向」，再按相机旋转反向旋转。
            // 行向量约定：p' = p × M。不含任何缩放 / 视口投影，故可用于视锥裁剪与 3D 相机。
            return Matrix4x4.CreateTranslation(-LocalPosition.X, -LocalPosition.Y, -LocalPosition.Z) *
                Matrix4x4.CreateRotationZ(-LocalRotation);
        }
    }

    /// <summary>
    /// 世界 → 相机（视图）矩阵：把世界坐标变换到相机空间，并叠加正交投影（视口缩放 + 居中偏移），
    /// 直接喂给 <see cref="SpriteBatch.Begin"/> 当 transformMatrix（最终的裁剪空间 / 屏幕投影由 SpriteBatch 内部完成）。
    /// 缩放系数取自【当前渲染目标】的视口尺寸（RT 或 屏幕）：渲染到 RT 时自动按 RT 大小适配、
    /// 渲染到屏幕时按屏幕尺寸适配——对标 Unity 的 worldToCameraMatrix（相机矩阵即视图矩阵），无需 ViewMatrixOverride 之类的覆盖项。
    /// </summary>
    public Matrix4x4 World_To_Camera_Matrix
    {
        get
        {
            // 世界 → 相机（视图）矩阵：先相机平移、旋转，再叠加正交投影（视口缩放 + 居中偏移）。
            // 直接作为 SpriteBatch.Begin 的 transformMatrix；最终裁剪空间 / 屏幕投影由 SpriteBatch 内部完成。
            var viewport = KSceneMgr.Game.GraphicsDevice.Viewport;
            float scaleCoef = viewport.Height / (OrthographicSize * 2f);
            // 相机是「中心点」坐标系（对标 Unity 正交相机：相机位置 = 视图中心），
            // 而屏幕/RT 是「左上角」坐标系（原点在左上、Y 向下）。
            // 先以相机位置为中心做 平移/旋转/缩放（视图变换），再把相机中心平移到视口中心，桥接左上角屏幕。
            Matrix4x4 view = Matrix4x4.CreateTranslation(-LocalPosition.X, -LocalPosition.Y, 0f) *
                Matrix4x4.CreateRotationZ(-LocalRotation) *
                Matrix4x4.CreateScale(scaleCoef, scaleCoef, 1f);
            return Matrix4x4.CreateTranslation(viewport.Width / 2f, viewport.Height / 2f, 0f) * view;
        }
    }

    // ===================== 投影矩阵（2D 正交 / 3D 透视），对标 Unity =====================

    /// <summary>是否正交投影。true = 2D 相机（正交，默认）；false = 3D 相机（透视投影）。对标 Unity camera.orthographic。</summary>
    public bool Orthographic = true;

    /// <summary>透视投影下的视野角（垂直方向，单位：度）。仅 <see cref="Orthographic"/>=false 时生效。对标 Unity camera.fieldOfView。</summary>
    public float FieldOfView = 60f;

    /// <summary>近裁剪面距离（&gt;0）。对标 Unity camera.nearClipPlane。</summary>
    public float NearClipPlane = 0.1f;

    /// <summary>远裁剪面距离。对标 Unity camera.farClipPlane。</summary>
    public float FarClipPlane = 1000f;

    /// <summary>当前渲染目标（视口）的宽高比，用于投影矩阵与视锥体计算。</summary>
    private float ViewportAspect
    {
        get
        {
            var vp = KSceneMgr.Game.GraphicsDevice.Viewport;
            return vp.Height == 0 ? 1f : vp.Width / (float)vp.Height;
        }
    }

    /// <summary>
    /// 投影矩阵（相机空间 → 裁剪空间）。正交走 <see cref="Matrix4x4.CreateOrthographicOffCenter"/>，
    /// 透视走 <see cref="Matrix4x4.CreatePerspectiveFieldOfView"/>。对标 Unity camera.projectionMatrix。
    /// </summary>
    public Matrix4x4 ProjectionMatrix
    {
        get
        {
            if (Orthographic)
            {
                float halfH = OrthographicSize;
                float halfW = OrthographicSize * ViewportAspect;
                return Matrix4x4.CreateOrthographicOffCenter(-halfW, halfW, -halfH, halfH, NearClipPlane, FarClipPlane);
            }
            float fovRad = FieldOfView * MathF.PI / 180f;
            return Matrix4x4.CreatePerspectiveFieldOfView(fovRad, ViewportAspect, NearClipPlane, FarClipPlane);
        }
    }

    /// <summary>视图 × 投影 组合矩阵（世界 → 裁剪空间）。对标 Unity worldToCameraMatrix × projectionMatrix。</summary>
    public Matrix4x4 ViewProjectionMatrix => ProjectionMatrix * ViewMatrix;

    // ===================== 相机矩阵裁剪（视锥剔除），对标 Unity GeometryUtility.TestPlanesAABB =====================

    /// <summary>
    /// 世界坐标球体（center + radius）是否在相机视锥体内。
    /// 先把球心变换到相机空间（视图矩阵），再按投影类型做解析判定（正交=视景框；透视=视锥金字塔）。
    /// 判定为「保守」：绝不误剔除可见物体，只会多保留边界外的少量物体。
    /// </summary>
    public bool IsInFrustum(Vector3 worldCenter, float radius)
    {
        Matrix4x4 m = ViewMatrix;
        float vx = worldCenter.X * m.M11 + worldCenter.Y * m.M12 + worldCenter.Z * m.M13 + m.M14;
        float vy = worldCenter.X * m.M21 + worldCenter.Y * m.M22 + worldCenter.Z * m.M23 + m.M24;
        float vz = worldCenter.X * m.M31 + worldCenter.Y * m.M32 + worldCenter.Z * m.M33 + m.M34;

        if (Orthographic)
        {
            float halfH = OrthographicSize;
            float halfW = OrthographicSize * ViewportAspect;
            // 相机看向 -Z：可见 Z 落在 [-Far, -Near]
            return vx >= -halfW - radius && vx <= halfW + radius
                && vy >= -halfH - radius && vy <= halfH + radius
                && vz >= -FarClipPlane - radius && vz <= -NearClipPlane + radius;
        }

        // 透视：必须在相机前方、且介于近远面之间
        if (vz >= 0f) return false;                  // 相机背后
        float depth = -vz;                           // 离相机距离（正数）
        if (depth + radius < NearClipPlane) return false;     // 整体在近面之前
        if (depth - radius > FarClipPlane) return false;      // 整体在远面之后

        float tanH = MathF.Tan(FieldOfView * MathF.PI / 180f / 2f);
        float tanW = tanH * ViewportAspect;
        (Vector3 n, float d)[] planes =
        [
            (new Vector3( 1, 0, -tanW), 0f),
            (new Vector3(-1, 0, -tanW), 0f),
            (new Vector3( 0, 1, -tanH), 0f),
            (new Vector3( 0,-1, -tanH), 0f),
            (new Vector3( 0, 0, -1f), -NearClipPlane),
            (new Vector3( 0, 0,  1f),  FarClipPlane),
        ];
        foreach (var p in planes)
        {
            float dist = p.n.X * vx + p.n.Y * vy + p.n.Z * vz + p.d;
            if (dist + radius < 0f) return false;
        }
        return true;
    }

    /// <summary>世界坐标轴对齐包围盒（center + halfExtents）是否在视锥体内。保守判定：整盒在某一平面之外才剔除。</summary>
    public bool IsInFrustum(Vector3 worldCenter, Vector3 halfExtents)
    {
        Matrix4x4 m = ViewMatrix;
        // 8 个角点 → 相机空间，求视图空间 AABB（旋转后的盒用其外接 AABB 做保守测试）
        float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
        float maxX = -float.MaxValue, maxY = -float.MaxValue, maxZ = -float.MaxValue;
        for (int i = 0; i < 8; i++)
        {
            float cx = (i & 1) != 0 ? halfExtents.X : -halfExtents.X;
            float cy = (i & 2) != 0 ? halfExtents.Y : -halfExtents.Y;
            float cz = (i & 4) != 0 ? halfExtents.Z : -halfExtents.Z;
            Vector3 w = new Vector3(worldCenter.X + cx, worldCenter.Y + cy, worldCenter.Z + cz);
            float vx = w.X * m.M11 + w.Y * m.M12 + w.Z * m.M13 + m.M14;
            float vy = w.X * m.M21 + w.Y * m.M22 + w.Z * m.M23 + m.M24;
            float vz = w.X * m.M31 + w.Y * m.M32 + w.Z * m.M33 + m.M34;
            minX = MathF.Min(minX, vx); maxX = MathF.Max(maxX, vx);
            minY = MathF.Min(minY, vy); maxY = MathF.Max(maxY, vy);
            minZ = MathF.Min(minZ, vz); maxZ = MathF.Max(maxZ, vz);
        }
        Vector3 c = new Vector3((minX + maxX) / 2f, (minY + maxY) / 2f, (minZ + maxZ) / 2f);
        Vector3 he = new Vector3((maxX - minX) / 2f, (maxY - minY) / 2f, (maxZ - minZ) / 2f);
        return IsAabbInFrustum(c, he);
    }

    private bool IsAabbInFrustum(Vector3 c, Vector3 he)
    {
        if (Orthographic)
        {
            float halfH = OrthographicSize;
            float halfW = OrthographicSize * ViewportAspect;
            // 可见 Z 落在 [-Far, -Near]；盒完全在某一侧之外 → 剔除
            if (c.X - he.X > halfW) return false;
            if (c.X + he.X < -halfW) return false;
            if (c.Y - he.Y > halfH) return false;
            if (c.Y + he.Y < -halfH) return false;
            if (c.Z - he.Z > -NearClipPlane) return false;
            if (c.Z + he.Z < -FarClipPlane) return false;
            return true;
        }
        float tanH = MathF.Tan(FieldOfView * MathF.PI / 180f / 2f);
        float tanW = tanH * ViewportAspect;
        (Vector3 n, float d)[] planes =
        [
            (new Vector3( 1, 0, -tanW), 0f),
            (new Vector3(-1, 0, -tanW), 0f),
            (new Vector3( 0, 1, -tanH), 0f),
            (new Vector3( 0,-1, -tanH), 0f),
            (new Vector3( 0, 0, -1f), -NearClipPlane),
            (new Vector3( 0, 0,  1f),  FarClipPlane),
        ];
        foreach (var p in planes)
        {
            float dist = p.n.X * c.X + p.n.Y * c.Y + p.n.Z * c.Z + p.d;
            float r = MathF.Abs(p.n.X) * he.X + MathF.Abs(p.n.Y) * he.Y + MathF.Abs(p.n.Z) * he.Z;
            if (dist + r < 0f) return false;
        }
        return true;
    }

    // ===== 坐标转换 =====

    /// <summary>
    /// 屏幕坐标 → 世界坐标（鼠标交互用）。基于相机矩阵 <see cref="World_To_Camera_Matrix"/> 求逆。
    /// </summary>
    public Vector2 ScreenToWorld(Vector2 screenPos)
    {
        return Vector2.Transform(screenPos, Matrix4x4.Invert(World_To_Camera_Matrix));
    }

    /// <summary>
    /// 世界坐标 → 屏幕坐标（经相机矩阵 <see cref="World_To_Camera_Matrix"/> 变换，输出渲染目标像素坐标）。
    /// </summary>
    public Vector2 WorldToScreen(Vector2 worldPos)
    {
        return Vector2.Transform(worldPos, World_To_Camera_Matrix);
    }

    // ===== 便捷方法 =====

    /// <summary>
    /// 判断世界坐标点是否在屏幕可视范围内
    /// </summary>
    public bool IsVisible(Vector2 worldPos)
    {
        var screenPos = WorldToScreen(worldPos);
        return screenPos.X >= 0 && screenPos.X <= ScreenResolution.X
            && screenPos.Y >= 0 && screenPos.Y <= ScreenResolution.Y;
    }

    /// <summary>
    /// 判断世界坐标矩形是否在屏幕可视范围内
    /// </summary>
    public bool IsVisible(Rectangle worldRect)
    {
        var topLeft = WorldToScreen(new Vector2(worldRect.Left, worldRect.Top));
        var bottomRight = WorldToScreen(new Vector2(worldRect.Right, worldRect.Bottom));

        return bottomRight.X >= 0 && topLeft.X <= ScreenResolution.X
            && bottomRight.Y >= 0 && topLeft.Y <= ScreenResolution.Y;
    }

    public void Follow(KTransform target)
    {
        if (Parent != null)
        {
            // 摄像机一定有父节点，把目标的世界坐标转换到父节点的本地空间
            var invParent = Matrix4x4.Invert(Parent.Local_To_World_Matrix);
            LocalPosition = Vector2.Transform(target.WorldPosition, invParent);
        }
        else
        {
            LocalPosition = target.WorldPosition;
        }
    }

    public void Follow(KTransform target, Vector2 offset)
    {
        if (Parent != null)
        {
            var invParent = Matrix4x4.Invert(Parent.Local_To_World_Matrix);
            LocalPosition = Vector2.Transform(target.WorldPosition + offset, invParent);
        }
        else
        {
            LocalPosition = target.WorldPosition + offset;
        }
    }

}
