using KFramework.MonoGame;
using KFramework.MonoGameExtend;

namespace KFramework.Example2;

/// <summary>
/// 资源中心：集中持有本游戏用到的全部精灵，避免各处零散加载。
/// 对应 PixiJS 版的 ResCenter.ts（mapAtlas: Map&lt;string, Spritesheet&gt;）。
/// 图集统一通过 <see cref="SpriteSheetLoader"/> 加载（KFramework 的资源管线只认 AtlasData 格式），
/// 单帧以 KSpriteInfo 形式存在页纹理上，用 KSprite 包装后即可直接赋给 KImage.Sprite。
/// </summary>
internal sealed class ResCenter
{
    // ===== 地图瓦片（取 AtlasData 中的具体帧，映射与原版 LoadTile 完全一致）=====
    public KSprite Wall { get; private init; }      // '#' -> Map_0
    public KSprite Barriar { get; private init; }   // '*' -> Map_1
    public KSprite Grass { get; private init; }     // '^' -> Map_2
    public KSprite Water { get; private init; }     // '~' -> Map_3
    public KSprite Heart { get; private init; }     // '@' -> Map_5

    // ===== 散图（不在图集内，作为独立纹理存在 MyRes/Textures）=====
    public KSprite Explode1 { get; private init; }
    public KSprite Explode2 { get; private init; }
    public KSprite Flag { get; private init; }
    public KSprite Shield { get; private init; }

    // ===== 序列帧（按帧名前缀 + 序号构造，顺序与原版 animations 一致）=====
    public KSprite[] Player { get; private init; }   // Player1_0 .. Player1_31
    public KSprite[] Enemy { get; private init; }    // Enemys_0 .. Enemys_63
    public KSprite[] Bullet { get; private init; }   // bullet_0 .. bullet_3
    public KSprite[] Born { get; private init; }     // Born_0 .. Born_3
    public KSprite[] Bonus { get; private init; }    // Bonus_0 .. Bonus_5

    // ===== 原始图集（供 HUD / UI 按需取帧，对应原版 mapAtlas）=====
    public SpriteSheet Map { get; private init; }
    public SpriteSheet Player1 { get; private init; }
    public SpriteSheet Enemys { get; private init; }
    public SpriteSheet BonusSheet { get; private init; }
    public SpriteSheet BornSheet { get; private init; }
    public SpriteSheet BulletSheet { get; private init; }
    public SpriteSheet ShieldSheet { get; private init; }
    public SpriteSheet Characters { get; private init; }
    public SpriteSheet Misc3 { get; private init; }
    public SpriteSheet UIView { get; private init; }

    public static ResCenter Load(AssetBundle bundle, GraphicsDevice device)
    {
        var loader = new SpriteSheetLoader(bundle, device);

        SpriteSheet map = loader.Load("main/MyRes/Atlas/Map.atlas.txt");
        SpriteSheet player1 = loader.Load("main/MyRes/Atlas/Player1.atlas.txt");
        SpriteSheet enemys = loader.Load("main/MyRes/Atlas/Enemys.atlas.txt");
        SpriteSheet bonus = loader.Load("main/MyRes/Atlas/Bonus.atlas.txt");
        SpriteSheet born = loader.Load("main/MyRes/Atlas/Born.atlas.txt");
        SpriteSheet bullet = loader.Load("main/MyRes/Atlas/Bullect.atlas.txt");
        SpriteSheet shield = loader.Load("main/MyRes/Atlas/Shield.atlas.txt");
        SpriteSheet characters = loader.Load("main/MyRes/Atlas/characters.atlas.txt");
        SpriteSheet misc3 = loader.Load("main/MyRes/Atlas/misc-3.atlas.txt");
        SpriteSheet uiView = loader.Load("main/MyRes/Atlas/UIView.atlas.txt");

        return new ResCenter
        {
            Map = map,
            Player1 = player1,
            Enemys = enemys,
            BonusSheet = bonus,
            BornSheet = born,
            BulletSheet = bullet,
            ShieldSheet = shield,
            Characters = characters,
            Misc3 = misc3,
            UIView = uiView,

            // 地图瓦片：取 atlas 内对应帧（帧名与原版一一对应）
            Wall = map.Sprite("Map_0"),
            Barriar = map.Sprite("Map_1"),
            Grass = map.Sprite("Map_2"),
            Water = map.Sprite("Map_3"),
            Heart = map.Sprite("Map_5"),

            // 散图：独立纹理，经资源管线打包后按完整路径取用
            Explode1 = LoadTexture(bundle, device, "main/MyRes/Textures/Explode1.png"),
            Explode2 = LoadTexture(bundle, device, "main/MyRes/Textures/Explode2.png"),
            Flag = LoadTexture(bundle, device, "main/MyRes/Textures/Flag.png"),
            Shield = LoadTexture(bundle, device, "main/MyRes/Textures/Shield.png"),

            // 序列帧数组：帧名前缀 + 序号，顺序即原版动画帧顺序
            Player = Range(player1, "Player1_", 32),
            Enemy = Range(enemys, "Enemys_", 64),
            Bullet = Range(bullet, "bullet_", 4),
            Born = Range(born, "Born_", 4),
            Bonus = Range(bonus, "Bonus_", 6),
        };
    }

    private static KSprite LoadTexture(AssetBundle bundle, GraphicsDevice device, string name)
    {
        bundle.TryLoadTexture(name, device, out Texture2D? tex);
        return tex is null ? default : new KSprite(tex);
    }

    private static KSprite[] Range(SpriteSheet sheet, string prefix, int count)
    {
        var result = new KSprite[count];
        for (int i = 0; i < count; i++)
        {
            // SpriteSheet.Sprite 返回 KSpriteInfo，隐式转为 KSprite
            result[i] = sheet.Sprite(prefix + i);
        }
        return result;
    }

    /// <summary>按索引取精灵，越界时回绕；数组为空则返回 default。</summary>
    public static KSprite Pick(KSprite[] sprites, int index)
    {
        if (sprites.Length == 0) return default;
        return sprites[((index % sprites.Length) + sprites.Length) % sprites.Length];
    }
}
