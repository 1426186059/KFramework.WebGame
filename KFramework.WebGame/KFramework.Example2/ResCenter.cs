using KFramework.MonoGame;

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

    public static ResCenter Load(AssetBundle bundle, GraphicsDevice device)
    {
        return new ResCenter
        {
            Wall = Try(bundle, device, "Map_0"),
            Barriar = Try(bundle, device, "Map_1"),
            Grass = Try(bundle, device, "Map_2"),
            Water = Try(bundle, device, "Map_3"),
            Heart = Try(bundle, device, "Map_5"),

            Explode1 = Try(bundle, device, "Explode1"),
            Explode2 = Try(bundle, device, "Explode2"),
            Flag = Try(bundle, device, "Flag"),
            Shield = Try(bundle, device, "Shield"),

            Player = Range(bundle, device, "Player1_", 32),
            Enemy = Range(bundle, device, "Enemys_", 64),
            Bullet = Range(bundle, device, "bullet_", 4),
            Born = Range(bundle, device, "Born_", 4),
            Bonus = Range(bundle, device, "Bonus_", 6),
        };
    }

    private static Texture2D? Try(AssetBundle bundle, GraphicsDevice device, string name)
    {
        bundle.TryLoadTexture(name, device, out Texture2D? tex);
        return tex;
    }

    private static Texture2D?[] Range(AssetBundle bundle, GraphicsDevice device, string prefix, int count)
    {
        var result = new Texture2D?[count];
        for (int i = 0; i < count; i++) result[i] = Try(bundle, device, prefix + i);
        return result;
    }

    /// <summary>按索引取纹理，越界时回绕；数组为空则返回 null。</summary>
    public static Texture2D? Pick(Texture2D?[] sprites, int index)
    {
        if (sprites.Length == 0) return null;
        return sprites[((index % sprites.Length) + sprites.Length) % sprites.Length];
    }
}
