namespace MirGame;

/// <summary>来自 content 包的 <c>data/game</c> 配置。</summary>
public sealed class GameConfig
{
    public DesignConfig Design { get; set; } = new();
    public PlayerConfig Player { get; set; } = new();
    public Dictionary<string, EnemyConfig> Enemies { get; set; } = new();
    public List<WaveConfig> Waves { get; set; } = new();
    public PowerUpConfig Powerup { get; set; } = new();
}

public sealed class DesignConfig
{
    public int Width { get; set; } = 480;
    public int Height { get; set; } = 720;
}

public sealed class PlayerConfig
{
    public float Speed { get; set; } = 300f;
    public float BulletSpeed { get; set; } = 700f;
    public float FireInterval { get; set; } = 0.15f;
    public int MaxLives { get; set; } = 3;
    public float InvulnerableTime { get; set; } = 1.6f;
    public float Radius { get; set; } = 11f;
}

public sealed class EnemyConfig
{
    public string Texture { get; set; } = "";
    public int Health { get; set; } = 1;
    public float Speed { get; set; } = 100f;
    public int Score { get; set; } = 100;
    public float Radius { get; set; } = 10f;
    public float FireInterval { get; set; } = 0f;
    public int ContactDamage { get; set; } = 1;
}

public sealed class WaveConfig
{
    public List<SpawnEntry> Spawn { get; set; } = new();
    public float Interval { get; set; } = 0.5f;
    public float RestAfter { get; set; } = 1.5f;
}

public sealed class SpawnEntry
{
    public string Type { get; set; } = "";
    public int Count { get; set; } = 1;
}

public sealed class PowerUpConfig
{
    public float DropChance { get; set; } = 0.14f;
    public float FallSpeed { get; set; } = 90f;
    public float Duration { get; set; } = 8f;
}
