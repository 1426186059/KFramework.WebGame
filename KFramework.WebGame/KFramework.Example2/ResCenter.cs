using KFramework.Content;
using KFramework.Graphics;

namespace KFramework.Example2;

/// <summary>
/// 资源中心：集中持有本游戏用到的全部纹理，避免各处零散加载。
/// 对应 PixiJS 版的 ResCenter.ts。
/// </summary>
internal sealed class ResCenter
{
    public Texture2D? Wall { get; private init; }
    public Texture2D? Barriar { get; private init; }
    public Texture2D? Grass { get; private init; }
    public Texture2D? Water { get; private init; }
    public Texture2D? Heart { get; private init; }

    public Texture2D? Explode1 { get; private init; }
    public Texture2D? Explode2 { get; private init; }
    public Texture2D? Flag { get; private init; }
    public Texture2D? Shield { get; private init; }

    public Texture2D?[] Player { get; private init; } = Array.Empty<Texture2D?>();
    public Texture2D?[] Enemy { get; private init; } = Array.Empty<Texture2D?>();
    public Texture2D?[] Bullet { get; private init; } = Array.Empty<Texture2D?>();
    public Texture2D?[] Born { get; private init; } = Array.Empty<Texture2D?>();

    /// <summary>道具图标，下标对应 <see cref="PowerUpKind"/>。</summary>
    public Texture2D?[] Bonus { get; private init; } = Array.Empty<Texture2D?>();

    public static ResCenter Load(ContentManager content)
    {
        return new ResCenter
        {
            Wall = Try(content, "Map_0"),
            Barriar = Try(content, "Map_1"),
            Grass = Try(content, "Map_2"),
            Water = Try(content, "Map_3"),
            Heart = Try(content, "Map_5"),

            Explode1 = Try(content, "Explode1"),
            Explode2 = Try(content, "Explode2"),
            Flag = Try(content, "Flag"),
            Shield = Try(content, "Shield"),

            Player = Range(content, "Player1_", 32),
            Enemy = Range(content, "Enemys_", 64),
            Bullet = Range(content, "bullet_", 4),
            Born = Range(content, "Born_", 4),
            Bonus = Range(content, "Bonus_", 6),
        };
    }

    private static Texture2D? Try(ContentManager content, string name)
    {
        content.TryLoadTexture(name, out Texture2D? tex);
        return tex;
    }

    private static Texture2D?[] Range(ContentManager content, string prefix, int count)
    {
        var result = new Texture2D?[count];
        for (int i = 0; i < count; i++) result[i] = Try(content, prefix + i);
        return result;
    }

    /// <summary>按索引取纹理，越界时回绕；数组为空则返回 null。</summary>
    public static Texture2D? Pick(Texture2D?[] sprites, int index)
    {
        if (sprites.Length == 0) return null;
        return sprites[((index % sprites.Length) + sprites.Length) % sprites.Length];
    }
}
