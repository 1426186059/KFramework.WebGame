using KFramework.MonoGame;

namespace MirGame.Tests.ImageTest;

/// <summary>
/// 图片测试模块（占位）：本页只给出加载入口说明与当前视口信息，
/// 具体的整图 / 图集测试后续在这里补：
/// <para>· 整图：<c>bundle.LoadTexture("bundles/xxx/a.png", device)</c>；</para>
/// <para>· 图集：<c>SpriteSheetLoader</c> 按 .atlas 切片后绘制；</para>
/// <para>· 包外松散图：<c>Content.LoadTexture2DAsync(path, device)</c>。</para>
/// 资源仍放在 Content/raw/Bundles 下（kfc 构建时打包）。
/// </summary>
public sealed class ImageTestScene : TestSceneBase
{
    public override string Title => "图片测试（待实现）";

    protected override void DrawBody(SpriteBatch batch, Vector2 origin)
    {
        float x = origin.X;
        float y = origin.Y;

        y += DrawSection(batch, "① 后续要测的内容", new Vector2(x, y));
        y += DrawLine(batch, Font, "· AssetBundle 整图纹理：bundle.LoadTexture(name, device)", new Vector2(x, y), Color.LightGray);
        y += DrawLine(batch, Font, "· 自动图集：SpriteSheetLoader 按 .atlas 切片并合批绘制", new Vector2(x, y), Color.LightGray);
        y += DrawLine(batch, Font, "· 包外松散图片：Content.LoadTexture2DAsync(path, device)", new Vector2(x, y), Color.LightGray);
        y += DrawLine(batch, Font, "· 各格式对比：PNG / WebP / KTX2（构建端 textureFormat 配置）", new Vector2(x, y), Color.LightGray);

        y += 16f;
        y += DrawSection(batch, "② 当前环境", new Vector2(x, y));
        y += DrawLine(batch, Font, $"视口：{Device.Viewport.Width} × {Device.Viewport.Height}", new Vector2(x, y), Color.LightGray);
    }
}
