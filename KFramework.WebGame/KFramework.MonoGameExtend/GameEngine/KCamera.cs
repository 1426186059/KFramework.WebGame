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

    // 屏幕适配偏移（让内容居中显示）
    private Vector2 _screenOffset;

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

        // 缩放后的内容尺寸
        float scaledWidth = DesignResolution.X * ScaleCoef;
        float scaledHeight = DesignResolution.Y * ScaleCoef;

        // 居中偏移
        //_screenOffset = new Vector2(
        //    (ScreenResolution.X - scaledWidth) / 2f,
        //    (ScreenResolution.Y - scaledHeight) / 2f
        //);
    }

    /// <summary>相机矩阵：世界 → 相机（视图）矩阵，等价于 <see cref="World_To_Camera_Matrix"/>。</summary>
    public Matrix4x4 ViewMatrix
    {
        get { return World_To_Camera_Matrix; }
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
            return Matrix4x4.CreateTranslation(-LocalPosition.X, -LocalPosition.Y, 0f) *
                Matrix4x4.CreateRotationZ(-LocalRotation) *
                Matrix4x4.CreateScale(scaleCoef, scaleCoef, 1f) *
                Matrix4x4.CreateTranslation(_screenOffset.X, _screenOffset.Y, 0f);
        }
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
