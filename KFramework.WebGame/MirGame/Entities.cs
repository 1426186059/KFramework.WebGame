using KFramework;
using KFramework.Graphics;

namespace MirGame;

public enum PowerUpKind
{
    Weapon,
    Heal,
    Shield,
}

public sealed class Player
{
    public Vector2 Position;
    public int Lives;
    public int PowerLevel = 1;
    public float PowerTimer;
    public float FireTimer;
    public float Invulnerable;
    public float Radius = 11f;
    public bool PointerControlled;

    public void Reset(Vector2 start, int lives, float radius)
    {
        Position = start;
        Lives = lives;
        PowerLevel = 1;
        PowerTimer = 0f;
        FireTimer = 0f;
        Invulnerable = 0f;
        Radius = radius;
        PointerControlled = false;
    }
}

public sealed class Enemy
{
    public string Type = "";
    public EnemyConfig Config = new();
    public Texture2D Texture = null!;
    public Vector2 Position;
    public float BaseX;
    public float Health;
    public float FireTimer;
    public float Phase;
    public float HitFlash;
    public bool Dead;

    public float Radius => Config.Radius;
}

public sealed class Bullet
{
    public Vector2 Position;
    public Vector2 Velocity;
    public float Radius = 4f;
    public bool FromPlayer;
    public int Damage = 1;
    public bool Dead;
}

public sealed class Particle
{
    public Vector2 Position;
    public Vector2 Velocity;
    public float Life;
    public float MaxLife;
    public Color Color = Color.White;
    public float Size = 6f;
    public float SizeEnd = 0f;
    public bool Dead => Life <= 0f;
}

public sealed class PowerUp
{
    public Vector2 Position;
    public PowerUpKind Kind;
    public bool Dead;
}

/// <summary>三层视差星空背景。</summary>
public sealed class Starfield
{
    private struct Star
    {
        public Vector2 Position;
        public float Speed;
        public float Size;
        public Color Color;
    }

    private readonly Star[] _stars;
    private readonly float _width;
    private readonly float _height;

    public Starfield(float width, float height, int count)
    {
        _width = width;
        _height = height;
        _stars = new Star[count];

        for (int i = 0; i < count; i++)
        {
            int layer = i % 3;
            _stars[i] = new Star
            {
                Position = new Vector2(Random.Shared.NextSingle() * width, Random.Shared.NextSingle() * height),
                Speed = 24f + layer * 42f + Random.Shared.NextSingle() * 18f,
                Size = 0.25f + layer * 0.22f + Random.Shared.NextSingle() * 0.12f,
                Color = new Color(255, 255, 255, 70 + layer * 55),
            };
        }
    }

    public void Update(float dt, float boost)
    {
        for (int i = 0; i < _stars.Length; i++)
        {
            _stars[i].Position.Y += _stars[i].Speed * boost * dt;
            if (_stars[i].Position.Y > _height)
            {
                _stars[i].Position.Y = -4f;
                _stars[i].Position.X = Random.Shared.NextSingle() * _width;
            }
        }
    }

    public void Draw(SpriteBatch batch, Texture2D texture)
    {
        foreach (Star star in _stars)
            batch.DrawCentered(texture, star.Position, star.Color, 0f, star.Size);
    }
}
