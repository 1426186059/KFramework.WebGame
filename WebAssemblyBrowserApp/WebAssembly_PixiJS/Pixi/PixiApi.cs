using System.Runtime.InteropServices.JavaScript;

namespace PixiJS;

/// <summary>
/// 原生 Pixi API 桥：与 wwwroot/jsengine/pixi-api.js 一一对应的 [JSImport] 声明。
/// C# 侧对象模型（Container/Graphics/Sprite/Text/Texture）经句柄注册表映射到
/// PixiJS v8 原生对象。
/// </summary>
internal static partial class PixiApi
{
    // ---- 生命周期 ----
    [JSImport("pixiApi.init", "main.js")] public static partial void Init(string selector);
    [JSImport("pixiApi.render", "main.js")] public static partial void Render();
    [JSImport("pixiApi.waitReady", "main.js")] public static partial Task WaitReady();

    // ---- 对象工厂 / 层级 ----
    [JSImport("pixiApi.create", "main.js")] public static partial int Create(string type);
    [JSImport("pixiApi.destroy", "main.js")] public static partial void Destroy(int id);
    [JSImport("pixiApi.addChild", "main.js")] public static partial void AddChild(int parentId, int childId);
    [JSImport("pixiApi.removeChild", "main.js")] public static partial void RemoveChild(int parentId, int childId);
    [JSImport("pixiApi.removeChildren", "main.js")] public static partial void RemoveChildren(int parentId);

    // ---- 通用属性 ----
    [JSImport("pixiApi.setProp", "main.js")] public static partial void SetProp(int id, string prop, double v);
    [JSImport("pixiApi.setProp2", "main.js")] public static partial void SetProp2(int id, string prop, double a, double b);

    // ---- Graphics ----
    [JSImport("pixiApi.gfx", "main.js")]
    public static partial void Gfx(int id, int op, double a0, double a1, double a2, double a3, double a4, double a5, double a6, double a7, double a8, double a9);
    [JSImport("pixiApi.gfxBatch", "main.js")] public static partial void GfxBatch(int id, double[] ops);

    // ---- Text ----
    [JSImport("pixiApi.textSet", "main.js")] public static partial void TextSet(int id, string text);
    [JSImport("pixiApi.textStyle", "main.js")] public static partial void TextStyle(int id, string font, string fill, string align);

    // ---- Sprite / Texture ----
    [JSImport("pixiApi.spriteTex", "main.js")] public static partial void SpriteTex(int id, int texId);
    [JSImport("pixiApi.loadTexture", "main.js")] public static partial Task<JSObject> LoadTexture(string url);
    [JSImport("pixiApi.loadVideo", "main.js")] public static partial Task<JSObject> LoadVideo(string url);
}
