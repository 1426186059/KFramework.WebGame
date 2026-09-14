using KFramework.MonoGame;
using Microsoft.Xna.Framework;

public class KCamera : KTransform
{
    // 相机视野的垂直半高（世界单位），类似 Unity 的 orthographicSize
    // 默认值 = 设计高度 / 2，让设计分辨率下 1 世界单位 = 1 像素
    public float OrthographicSize = 540f;
    // 背景色（如果需要清屏的话）
    public Color BackgroundColor = Color.CornflowerBlue;
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

    public Matrix ViewMatrix
    {
        get { return World_To_Screen_Matrix; }
    }

    public Matrix World_To_Screen_Matrix
    {
        get
        {
            // 世界 → 屏幕：先相机平移，再缩放，再加屏幕偏移
            // 矩阵乘法顺序（从右到左应用）：
            // 1. 减去相机位置（相机跟随）
            // 2. 乘以缩放系数（适配屏幕）
            // 3. 加上屏幕偏移（居中）
            return Matrix.CreateTranslation(-LocalPosition.X, -LocalPosition.Y, 0f) *
                Matrix.CreateRotationZ(-LocalRotation) *
                Matrix.CreateScale(ScaleCoef, ScaleCoef, 1f) *
                Matrix.CreateTranslation(_screenOffset.X, _screenOffset.Y, 0f);
        }
    }

    // ===== 坐标转换 =====

    /// <summary>
    /// 屏幕坐标 → 世界坐标（鼠标交互用）
    /// </summary>
    public Vector2 ScreenToWorld(Vector2 screenPos)
    {
        return Vector2.Transform(screenPos, Matrix.Invert(World_To_Screen_Matrix));
    }

    /// <summary>
    /// 世界坐标 → 屏幕坐标
    /// </summary>
    public Vector2 WorldToScreen(Vector2 worldPos)
    {
        return Vector2.Transform(worldPos, World_To_Screen_Matrix);
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
            var invParent = Matrix.Invert(Parent.Local_To_World_Matrix);
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
            var invParent = Matrix.Invert(Parent.Local_To_World_Matrix);
            LocalPosition = Vector2.Transform(target.WorldPosition + offset, invParent);
        }
        else
        {
            LocalPosition = target.WorldPosition + offset;
        }
    }

}