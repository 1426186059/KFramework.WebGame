using KFramework;
using KFramework.Graphics;

namespace MirGame;

/// <summary>
/// 示例小游戏《Star Defender》：一个纵版太空射击。
/// 它完整演示了框架的用法：内容包加载、图集精灵、批处理、输入、粒子、音效、屏幕适配。
/// </summary>
public sealed class StarDefenderGame : Game
{
    private const int DesignWidth = 480;
    private const int DesignHeight = 720;

    private enum State
    {
        Loading,
        Title,
        Playing,
        GameOver,
    }

    private SpriteBatch _batch = null!;
    private SpriteFont _titleFont = null!;
    private SpriteFont _uiFont = null!;
    private SpriteFont _smallFont = null!;

    private Texture2D _playerTexture = null!;
    private Texture2D _flameTexture = null!;
    private Texture2D _bulletTexture = null!;
    private Texture2D _enemyBulletTexture = null!;
    private Texture2D _particleTexture = null!;

    /// <summary>1x1 白色纹理，用于纯色矩形（进度条、遮罩）。不依赖内容包，加载期间即可用。</summary>
    private Texture2D _whiteTexture = null!;
    private Texture2D _starTexture = null!;
    private Texture2D _powerUpTexture = null!;
    private Texture2D _heartTexture = null!;
    private readonly Dictionary<string, Texture2D> _enemyTextures = new(StringComparer.OrdinalIgnoreCase);

    private GameConfig _config = new();
    private Task? _contentTask;
    private float _loadProgress;
    private bool _loadFailed;
    private string _loadError = "";

    private State _state = State.Loading;
    private float _stateTimer;

    private readonly Player _player = new();
    private readonly List<Enemy> _enemies = new();
    private readonly List<Bullet> _bullets = new();
    private readonly List<Particle> _particles = new();
    private readonly List<PowerUp> _powerUps = new();
    private Starfield _stars = null!;

    private readonly Queue<string> _spawnQueue = new();
    private int _waveIndex;
    private int _waveNumber = 1;
    private float _spawnTimer;
    private float _waveRestTimer;
    private float _currentInterval = 0.5f;

    private int _score;
    private int _bestScore;
    private int _combo;
    private float _comboTimer;
    private float _shake;
    private float _time;
    private int _previousTouchCount;

    private float _viewScale = 1f;
    private Vector2 _viewOffset;

    public StarDefenderGame() : base("#game", "content")
    {
        ClearColor = new Color(8, 10, 22);
    }

    protected override void Initialize()
    {
        Window.Title = "Star Defender — KFramework WebGame";
        _stars = new Starfield(DesignWidth, DesignHeight, 110);
    }

    protected override Task LoadContentAsync()
    {
        // 字体与纯色纹理不依赖内容包，先准备好，这样加载进度条可以立刻显示
        _batch = new SpriteBatch(GraphicsDevice);
        _whiteTexture = GraphicsDevice.CreateTexture(1, 1, [255, 255, 255, 255]);
        _titleFont = new SpriteFont(GraphicsDevice, 46f);
        _uiFont = new SpriteFont(GraphicsDevice, 24f);
        _smallFont = new SpriteFont(GraphicsDevice, 17f);

        // 内容包在后台下载，主循环同时渲染加载界面
        _contentTask = Content.LoadAsync(new Progress<float>(value => _loadProgress = value));
        return Task.CompletedTask;
    }

    #region 更新

    protected override void Update(GameTime gameTime)
    {
        float dt = gameTime.Delta;
        _time += dt;

        KeyboardState keyboard = Input.GetKeyboardState();

        if (_state == State.Loading)
        {
            _stars.Update(dt, 0.35f);
            if (_contentTask is { IsCompleted: true })
            {
                if (_contentTask.IsCompletedSuccessfully)
                {
                    FinishLoading();
                    _state = State.Title;
                }
                else
                {
                    _loadFailed = true;
                    _loadError = _contentTask.Exception?.InnerException?.Message ?? "未知错误";
                    Console.Error.WriteLine($"[StarDefender] 内容加载失败：{_loadError}");
                }
                _contentTask = null;
            }
            _previousTouchCount = Input.GetTouchState().Count;
            return;
        }

        if (_shake > 0f) _shake = Math.Max(0f, _shake - dt * 2.5f);
        _stars.Update(dt, _state == State.Playing ? 1f : 0.5f);
        UpdateParticles(dt);

        switch (_state)
        {
            case State.Title:
                _stateTimer += dt;
                if (keyboard.IsKeyPressed(Keys.Space) || keyboard.IsKeyPressed(Keys.Enter) || ConsumePointerPress())
                {
                    Audio.Unlock();
                    Audio.Play(Audio.Sfx.Select);
                    StartGame();
                }
                break;

            case State.Playing:
                UpdatePlaying(dt, keyboard);
                break;

            case State.GameOver:
                _stateTimer += dt;
                if (_stateTimer > 0.7f && (keyboard.IsKeyPressed(Keys.R) ||
                                           keyboard.IsKeyPressed(Keys.Space) || ConsumePointerPress()))
                {
                    Audio.Play(Audio.Sfx.Select);
                    StartGame();
                }
                break;
        }

        _previousTouchCount = Input.GetTouchState().Count;
    }

    private void UpdatePlaying(float dt, KeyboardState keyboard)
    {
        PlayerConfig config = _config.Player;

        // ---- 玩家移动：优先跟随指针（鼠标/触屏），否则用键盘 ----
        Vector2 pointer = PointerDesignPosition();
        bool pointerDown = IsPointerDown();

        if (pointerDown && _state == State.Playing)
        {
            Vector2 delta = pointer - _player.Position;
            float distance = delta.Length();
            if (distance > 2f)
            {
                float step = Math.Min(distance, config.Speed * dt);
                _player.Position += delta / distance * step;
            }
            _player.PointerControlled = true;
        }
        else
        {
            Vector2 direction = keyboard.MovementDirection;
            if (direction != Vector2.Zero)
            {
                _player.Position += direction * config.Speed * dt;
                _player.PointerControlled = false;
            }
        }

        float halfWidth = _playerTexture.Width * 0.5f;
        _player.Position.X = MathHelper.Clamp(_player.Position.X, halfWidth, DesignWidth - halfWidth);
        _player.Position.Y = MathHelper.Clamp(_player.Position.Y, 40f, DesignHeight - 40f);

        if (_player.Invulnerable > 0f) _player.Invulnerable -= dt;

        if (_player.PowerTimer > 0f)
        {
            _player.PowerTimer -= dt;
            if (_player.PowerTimer <= 0f)
            {
                _player.PowerLevel = 1;
                Audio.Play(Audio.Sfx.Hit, 0.4f, 0.6f);
            }
        }

        // ---- 自动开火 ----
        _player.FireTimer -= dt;
        if (_player.FireTimer <= 0f)
        {
            _player.FireTimer = config.FireInterval;
            FirePlayerBullets();
            Audio.Play(Audio.Sfx.Shoot, 0.22f, 1f + Random.Shared.NextSingle() * 0.1f);
        }

        // ---- 敌人 ----
        UpdateWave(dt);
        UpdateEnemies(dt);

        // ---- 子弹 ----
        for (int i = _bullets.Count - 1; i >= 0; i--)
        {
            Bullet bullet = _bullets[i];
            bullet.Position += bullet.Velocity * dt;
            if (bullet.Position.Y < -40f || bullet.Position.Y > DesignHeight + 40f ||
                bullet.Position.X < -40f || bullet.Position.X > DesignWidth + 40f)
            {
                bullet.Dead = true;
            }
        }

        // ---- 道具 ----
        for (int i = _powerUps.Count - 1; i >= 0; i--)
        {
            PowerUp powerUp = _powerUps[i];
            powerUp.Position.Y += _config.Powerup.FallSpeed * dt;

            if (Vector2.Distance(powerUp.Position, _player.Position) < 26f)
            {
                ApplyPowerUp(powerUp.Kind);
                powerUp.Dead = true;
            }
            else if (powerUp.Position.Y > DesignHeight + 30f)
            {
                powerUp.Dead = true;
            }
        }

        // ---- 碰撞 ----
        HandleCollisions();

        // ---- 清理 ----
        _bullets.RemoveAll(static b => b.Dead);
        _enemies.RemoveAll(static e => e.Dead);
        _powerUps.RemoveAll(static p => p.Dead);

        // ---- 连击衰减 ----
        if (_comboTimer > 0f)
        {
            _comboTimer -= dt;
            if (_comboTimer <= 0f) _combo = 0;
        }
    }

    private void UpdateWave(float dt)
    {
        if (_spawnQueue.Count > 0)
        {
            _spawnTimer -= dt;
            if (_spawnTimer <= 0f)
            {
                _spawnTimer = _currentInterval;
                SpawnEnemy(_spawnQueue.Dequeue());
            }
            return;
        }

        if (_enemies.Count > 0) return;

        // 本波清空 → 稍作喘息后进入下一波
        _waveRestTimer -= dt;
        if (_waveRestTimer <= 0f)
        {
            _waveIndex = (_waveIndex + 1) % Math.Max(1, _config.Waves.Count);
            _waveNumber++;
            EnqueueWave();
        }
    }

    private void UpdateEnemies(float dt)
    {
        foreach (Enemy enemy in _enemies)
        {
            enemy.Phase += dt * 1.8f;
            enemy.Position.Y += enemy.Config.Speed * dt;
            enemy.Position.X = enemy.BaseX + MathF.Sin(enemy.Phase) * 42f;
            enemy.HitFlash = Math.Max(0f, enemy.HitFlash - dt * 5f);

            if (enemy.Config.FireInterval > 0f && enemy.Position.Y > 0f)
            {
                enemy.FireTimer -= dt;
                if (enemy.FireTimer <= 0f)
                {
                    enemy.FireTimer = enemy.Config.FireInterval * (0.7f + Random.Shared.NextSingle() * 0.6f);
                    FireEnemyBullets(enemy);
                }
            }

            if (enemy.Position.Y > DesignHeight + 60f) enemy.Dead = true;
        }
    }

    private void HandleCollisions()
    {
        foreach (Bullet bullet in _bullets)
        {
            if (bullet.Dead) continue;

            if (bullet.FromPlayer)
            {
                foreach (Enemy enemy in _enemies)
                {
                    if (enemy.Dead) continue;
                    if (Vector2.Distance(bullet.Position, enemy.Position) > enemy.Radius + bullet.Radius) continue;

                    bullet.Dead = true;
                    enemy.Health -= bullet.Damage;
                    enemy.HitFlash = 1f;

                    SpawnParticles(bullet.Position, 4, new Color(255, 220, 140), 90f, 0.25f);

                    if (enemy.Health <= 0) KillEnemy(enemy);
                    break;
                }
            }
            else if (_player.Invulnerable <= 0f &&
                     Vector2.Distance(bullet.Position, _player.Position) < _player.Radius + bullet.Radius)
            {
                bullet.Dead = true;
                DamagePlayer(1);
            }
        }

        if (_player.Invulnerable > 0f) return;

        foreach (Enemy enemy in _enemies)
        {
            if (enemy.Dead) continue;
            if (Vector2.Distance(enemy.Position, _player.Position) > enemy.Radius + _player.Radius) continue;

            DamagePlayer(enemy.Config.ContactDamage);
            enemy.Health = 0;
            KillEnemy(enemy, awardScore: false);
            break;
        }
    }

    private void KillEnemy(Enemy enemy, bool awardScore = true)
    {
        enemy.Dead = true;
        SpawnExplosion(enemy.Position, (int)(enemy.Radius * 1.5f) + 8, new Color(255, 190, 90));
        Audio.Play(Audio.Sfx.Explosion, enemy.Radius > 18f ? 0.6f : 0.35f, enemy.Radius > 18f ? 0.7f : 1.2f);
        _shake = Math.Min(1f, _shake + enemy.Radius * 0.03f);

        if (!awardScore) return;

        _combo++;
        _comboTimer = 2.5f;
        int multiplier = 1 + _combo / 5;
        _score += enemy.Config.Score * multiplier;

        if (Random.Shared.NextSingle() < _config.Powerup.DropChance)
        {
            _powerUps.Add(new PowerUp
            {
                Position = enemy.Position,
                Kind = Random.Shared.Next(3) switch
                {
                    0 => PowerUpKind.Weapon,
                    1 => PowerUpKind.Heal,
                    _ => PowerUpKind.Shield,
                },
            });
        }
    }

    private void DamagePlayer(int amount)
    {
        if (_player.Invulnerable > 0f) return;

        _player.Lives -= amount;
        _player.Invulnerable = _config.Player.InvulnerableTime;
        _combo = 0;
        _shake = 1f;
        SpawnExplosion(_player.Position, 26, new Color(255, 140, 80));
        Audio.Play(Audio.Sfx.Explosion, 0.5f, 0.8f);

        if (_player.Lives <= 0)
        {
            _player.Lives = 0;
            _bestScore = Math.Max(_bestScore, _score);
            _state = State.GameOver;
            _stateTimer = 0f;
            Audio.Play(Audio.Sfx.GameOver);
        }
    }

    private void ApplyPowerUp(PowerUpKind kind)
    {
        Audio.Play(Audio.Sfx.PowerUp, 0.5f);
        switch (kind)
        {
            case PowerUpKind.Weapon:
                _player.PowerLevel = Math.Min(3, _player.PowerLevel + 1);
                _player.PowerTimer = _config.Powerup.Duration;
                break;
            case PowerUpKind.Heal:
                _player.Lives = Math.Min(_config.Player.MaxLives, _player.Lives + 1);
                break;
            case PowerUpKind.Shield:
                _player.Invulnerable = Math.Max(_player.Invulnerable, 3f);
                break;
        }
        SpawnParticles(_player.Position, 14, new Color(120, 255, 190), 160f, 0.5f);
    }

    #endregion

    #region 生成

    private void StartGame()
    {
        _score = 0;
        _combo = 0;
        _comboTimer = 0f;
        _waveIndex = 0;
        _waveNumber = 1;
        _shake = 0f;

        _enemies.Clear();
        _bullets.Clear();
        _particles.Clear();
        _powerUps.Clear();

        _player.Reset(new Vector2(DesignWidth * 0.5f, DesignHeight - 100f),
                      _config.Player.MaxLives, _config.Player.Radius);
        _player.Invulnerable = 1.5f;

        EnqueueWave();
        _spawnTimer = 0.6f;
        _state = State.Playing;
        _stateTimer = 0f;
    }

    private void EnqueueWave()
    {
        _spawnQueue.Clear();

        if (_config.Waves.Count == 0) return;
        WaveConfig wave = _config.Waves[_waveIndex % _config.Waves.Count];

        var pending = new List<string>();
        foreach (SpawnEntry entry in wave.Spawn)
            for (int i = 0; i < entry.Count; i++)
                pending.Add(entry.Type);

        // 洗牌，避免同种敌人扎堆出现
        for (int i = pending.Count - 1; i > 0; i--)
        {
            int j = Random.Shared.Next(i + 1);
            (pending[i], pending[j]) = (pending[j], pending[i]);
        }

        foreach (string type in pending) _spawnQueue.Enqueue(type);

        _currentInterval = wave.Interval;
        _waveRestTimer = wave.RestAfter;
    }

    private void SpawnEnemy(string type)
    {
        if (!_config.Enemies.TryGetValue(type, out EnemyConfig? config)) return;
        if (!_enemyTextures.TryGetValue(config.Texture, out Texture2D? texture)) return;

        float x = MathHelper.Clamp(45f + Random.Shared.NextSingle() * (DesignWidth - 90f), 30f, DesignWidth - 30f);

        _enemies.Add(new Enemy
        {
            Type = type,
            Config = config,
            Texture = texture,
            Position = new Vector2(x, -40f),
            BaseX = x,
            Health = config.Health,
            FireTimer = config.FireInterval * (0.4f + Random.Shared.NextSingle()),
            Phase = Random.Shared.NextSingle() * MathF.PI * 2f,
        });
    }

    private void FirePlayerBullets()
    {
        Vector2 origin = _player.Position - new Vector2(0f, 14f);
        float speed = _config.Player.BulletSpeed;

        switch (_player.PowerLevel)
        {
            case 1:
                _bullets.Add(CreateBullet(origin, new Vector2(0f, -speed), true, 1));
                break;
            case 2:
                _bullets.Add(CreateBullet(origin - new Vector2(7f, 0f), new Vector2(0f, -speed), true, 1));
                _bullets.Add(CreateBullet(origin + new Vector2(7f, 0f), new Vector2(0f, -speed), true, 1));
                break;
            default:
                _bullets.Add(CreateBullet(origin, new Vector2(0f, -speed), true, 1));
                _bullets.Add(CreateBullet(origin, new Vector2(-140f, -speed * 0.94f), true, 1));
                _bullets.Add(CreateBullet(origin, new Vector2(140f, -speed * 0.94f), true, 1));
                break;
        }
    }

    private void FireEnemyBullets(Enemy enemy)
    {
        Vector2 direction = _player.Position - enemy.Position;
        if (direction.LengthSquared() < 1f) direction = Vector2.UnitY;
        direction = Vector2.Normalize(direction);

        float speed = 230f;
        _bullets.Add(CreateBullet(enemy.Position, direction * speed, false, 1));

        if (enemy.Type == "bomber")
        {
            Vector2 left = Vector2.Rotate(direction, -0.28f) * speed;
            Vector2 right = Vector2.Rotate(direction, 0.28f) * speed;
            _bullets.Add(CreateBullet(enemy.Position, left, false, 1));
            _bullets.Add(CreateBullet(enemy.Position, right, false, 1));
        }
    }

    private static Bullet CreateBullet(Vector2 position, Vector2 velocity, bool fromPlayer, int damage)
        => new()
        {
            Position = position,
            Velocity = velocity,
            FromPlayer = fromPlayer,
            Damage = damage,
            Radius = fromPlayer ? 5f : 6f,
        };

    #endregion

    #region 粒子

    private void SpawnExplosion(Vector2 position, int count, Color color)
    {
        SpawnParticles(position, count, color, 220f, 0.75f);
        SpawnParticles(position, count / 2, new Color(255, 255, 220), 130f, 0.45f);
    }

    private void SpawnParticles(Vector2 position, int count, Color color, float speed, float life)
    {
        for (int i = 0; i < count; i++)
        {
            float angle = Random.Shared.NextSingle() * MathF.PI * 2f;
            float magnitude = speed * (0.25f + Random.Shared.NextSingle() * 0.75f);

            _particles.Add(new Particle
            {
                Position = position,
                Velocity = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * magnitude,
                Life = life * (0.6f + Random.Shared.NextSingle() * 0.6f),
                MaxLife = life,
                Color = color,
                Size = 4f + Random.Shared.NextSingle() * 7f,
                SizeEnd = 0f,
            });
        }
    }

    private void UpdateParticles(float dt)
    {
        for (int i = _particles.Count - 1; i >= 0; i--)
        {
            Particle particle = _particles[i];
            particle.Life -= dt;
            if (particle.Life <= 0f)
            {
                _particles.RemoveAt(i);
                continue;
            }
            particle.Position += particle.Velocity * dt;
            particle.Velocity *= 1f - 2.2f * dt;      // 阻尼
        }
    }

    #endregion

    #region 绘制

    protected override void Draw(GameTime gameTime)
    {
        ComputeView();

        float shakeX = 0f, shakeY = 0f;
        if (_shake > 0f)
        {
            float amount = _shake * _shake * 9f;
            shakeX = (Random.Shared.NextSingle() - 0.5f) * amount;
            shakeY = (Random.Shared.NextSingle() - 0.5f) * amount;
        }

        Matrix4x4 transform = Matrix4x4.CreateScaleTranslation(
            _viewScale, _viewScale,
            _viewOffset.X + shakeX * _viewScale, _viewOffset.Y + shakeY * _viewScale);

        if (_state == State.Loading)
        {
            _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.Linear, transform);
            DrawLoading();
            _batch.End();
            return;
        }

        // 背景星空
        _batch.Begin(SpriteSortMode.Texture, BlendState.AlphaBlend, SamplerState.Linear, transform);
        _stars.Draw(_batch, _starTexture);

        // 游戏实体
        foreach (PowerUp powerUp in _powerUps)
        {
            float bob = MathF.Sin(_time * 4f + powerUp.Position.X) * 3f;
            Color tint = powerUp.Kind switch
            {
                PowerUpKind.Heal => new Color(255, 140, 160),
                PowerUpKind.Shield => new Color(140, 200, 255),
                _ => new Color(150, 255, 200),
            };
            _batch.DrawCentered(_powerUpTexture, powerUp.Position + new Vector2(0f, bob), tint,
                                _time * 1.5f, 1f + MathF.Sin(_time * 6f) * 0.08f);
        }

        foreach (Enemy enemy in _enemies)
        {
            Color tint = enemy.HitFlash > 0f ? Color.Lerp(Color.White, new Color(255, 120, 120), enemy.HitFlash) : Color.White;
            _batch.DrawCentered(enemy.Texture, enemy.Position, tint);
        }

        foreach (Bullet bullet in _bullets)
        {
            Texture2D texture = bullet.FromPlayer ? _bulletTexture : _enemyBulletTexture;
            _batch.DrawCentered(texture, bullet.Position, Color.White,
                                bullet.FromPlayer ? 0f : _time * 6f, bullet.FromPlayer ? 1f : 1f);
        }

        if (_state == State.Playing || _player.Lives > 0)
        {
            bool blink = _player.Invulnerable > 0f && MathF.Sin(_time * 40f) < 0f;
            if (!blink)
            {
                float flameScale = 0.75f + MathF.Sin(_time * 30f) * 0.18f;
                _batch.DrawCentered(_flameTexture, _player.Position + new Vector2(0f, 20f),
                                    new Color(255, 220, 150), 0f, flameScale);
                _batch.DrawCentered(_playerTexture, _player.Position, Color.White);

                if (_player.PowerTimer > 0f)
                {
                    float pulse = 0.6f + MathF.Sin(_time * 10f) * 0.2f;
                    _batch.DrawCentered(_powerUpTexture, _player.Position, new Color(140, 255, 200), 0f, pulse, 0.1f);
                }
            }
        }
        _batch.End();

        // 粒子用叠加混合
        if (_particles.Count > 0)
        {
            _batch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.Linear, transform);
            foreach (Particle particle in _particles)
            {
                float t = particle.Life / Math.Max(0.0001f, particle.MaxLife);
                float size = MathHelper.Lerp(particle.SizeEnd, particle.Size, t) / _particleTexture.Height;
                Color color = new Color(particle.Color.R, particle.Color.G, particle.Color.B, (byte)(255 * t));
                _batch.DrawCentered(_particleTexture, particle.Position, color, 0f, size);
            }
            _batch.End();
        }

        // UI
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.Linear, transform);
        switch (_state)
        {
            case State.Title:
                DrawTitle();
                break;
            case State.Playing:
                DrawHud();
                break;
            case State.GameOver:
                DrawHud();
                DrawGameOver();
                break;
        }
        _batch.End();
    }

    private void DrawLoading()
    {
        DrawTextCentered(_titleFont, "STAR DEFENDER", DesignWidth * 0.5f, DesignHeight * 0.36f, new Color(120, 200, 255));

        if (_loadFailed)
        {
            DrawTextCentered(_uiFont, "内容加载失败", DesignWidth * 0.5f, DesignHeight * 0.47f, new Color(255, 120, 120));
            DrawTextCentered(_smallFont, _loadError, DesignWidth * 0.5f, DesignHeight * 0.51f, new Color(190, 190, 200));
            return;
        }

        DrawTextCentered(_smallFont, "正在加载内容包…", DesignWidth * 0.5f, DesignHeight * 0.47f, new Color(148, 163, 184));

        // 进度条
        float barWidth = 260f;
        float x = (DesignWidth - barWidth) * 0.5f;
        float y = DesignHeight * 0.52f;
        var background = new Rectangle((int)x, (int)y, (int)barWidth, 10);
        var fill = new Rectangle((int)x, (int)y, (int)(barWidth * MathHelper.Clamp(_loadProgress, 0f, 1f)), 10);

        _batch.Draw(_whiteTexture, background, new Color(40, 50, 74));
        if (fill.Width > 0) _batch.Draw(_whiteTexture, fill, new Color(80, 200, 255));
    }

    private void DrawTitle()
    {
        float center = DesignWidth * 0.5f;

        DrawTextCentered(_titleFont, "STAR DEFENDER", center, 150f, new Color(140, 210, 255));

        float pulse = 0.5f + MathF.Sin(_time * 2.4f) * 0.5f;
        DrawTextCentered(_uiFont, "按 空格 或 点击屏幕 开始", center, 300f,
                         Color.Lerp(new Color(90, 120, 160), new Color(230, 245, 255), pulse));

        DrawTextCentered(_smallFont, "WASD / 方向键 移动 · 自动开火", center, 400f, new Color(150, 170, 200));
        DrawTextCentered(_smallFont, "手机可直接拖动飞船", center, 428f, new Color(150, 170, 200));
        DrawTextCentered(_smallFont, "拾取道具可升级火力与回血", center, 456f, new Color(150, 170, 200));

        if (_bestScore > 0)
            DrawTextCentered(_smallFont, $"最高分 {_bestScore}", center, 510f, new Color(250, 210, 120));
    }

    private void DrawHud()
    {
        // 分数
        _batch.DrawString(_uiFont, $"{_score}", new Vector2(16f, 14f), new Color(235, 245, 255));
        _batch.DrawString(_smallFont, $"WAVE {_waveNumber}", new Vector2(16f, 44f), new Color(130, 160, 200));

        if (_bestScore > 0)
            _batch.DrawString(_smallFont, $"BEST {_bestScore}", new Vector2(16f, 66f), new Color(120, 140, 170));

        // 连击
        if (_combo >= 2)
        {
            float t = MathHelper.Clamp(_comboTimer / 2.5f, 0f, 1f);
            Color comboColor = Color.Lerp(new Color(255, 200, 90), new Color(255, 120, 60), 1f - t);
            string text = $"x{_combo} COMBO";
            Vector2 size = _smallFont.Measure(text);
            _batch.DrawString(_smallFont, text, new Vector2(DesignWidth - size.X - 16f, 16f), comboColor);
        }

        // 生命
        for (int i = 0; i < _player.Lives; i++)
            _batch.Draw(_heartTexture, new Vector2(DesignWidth - 34f - i * 24f, DesignHeight - 34f), Color.White);

        // 火力等级
        for (int i = 0; i < _player.PowerLevel; i++)
            _batch.Draw(_powerUpTexture, new Vector2(16f + i * 22f, DesignHeight - 34f), new Color(150, 255, 200));
    }

    private void DrawGameOver()
    {
        float center = DesignWidth * 0.5f;

        // 半透明遮罩
        _batch.Draw(_whiteTexture, new Rectangle(0, 0, DesignWidth, DesignHeight), new Color(4, 6, 14, 190));

        DrawTextCentered(_titleFont, "GAME OVER", center, 250f, new Color(255, 120, 120));
        DrawTextCentered(_uiFont, $"得分  {_score}", center, 340f, new Color(230, 240, 255));
        DrawTextCentered(_smallFont, $"最高分  {_bestScore}", center, 378f, new Color(250, 210, 120));
        DrawTextCentered(_smallFont, $"坚持到第 {_waveNumber} 波", center, 406f, new Color(150, 170, 200));

        if (_stateTimer > 0.7f)
        {
            float pulse = 0.5f + MathF.Sin(_time * 3f) * 0.5f;
            DrawTextCentered(_smallFont, "按 R 或 点击屏幕 再来一局", center, 480f,
                             Color.Lerp(new Color(90, 120, 160), new Color(230, 245, 255), pulse));
        }
    }

    private void DrawTextCentered(SpriteFont font, string text, float centerX, float y, Color color, float scale = 1f)
    {
        Vector2 size = font.Measure(text) * scale;
        _batch.DrawString(font, text, new Vector2(centerX - size.X * 0.5f, y), color, 0f, Vector2.Zero, scale);
    }

    #endregion

    #region 适配与输入辅助

    private void ComputeView()
    {
        float width = Window.Width;
        float height = Window.Height;
        if (width <= 0 || height <= 0)
        {
            _viewScale = 1f;
            _viewOffset = Vector2.Zero;
            return;
        }

        _viewScale = Math.Min(width / DesignWidth, height / DesignHeight);
        _viewOffset = new Vector2((width - DesignWidth * _viewScale) * 0.5f,
                                  (height - DesignHeight * _viewScale) * 0.5f);
    }

    /// <summary>把 CSS 像素坐标换算成 480x720 设计坐标。</summary>
    private Vector2 PointerDesignPosition()
    {
        Vector2 css = Window.CssSize;
        if (css.X <= 0f || css.Y <= 0f) return _player.Position;

        float scale = Math.Min(css.X / DesignWidth, css.Y / DesignHeight);
        Vector2 origin = new((css.X - DesignWidth * scale) * 0.5f, (css.Y - DesignHeight * scale) * 0.5f);

        Vector2 pointer = Input.GetMouseState().Position;
        TouchCollection touches = Input.GetTouchState();
        if (touches.Count > 0) pointer = touches[0].Position;

        return (pointer - origin) / scale;
    }

    private bool IsPointerDown()
    {
        if (Input.GetMouseState().LeftButton) return true;
        return Input.GetTouchState().Count > 0;
    }

    /// <summary>本帧是否发生了"新的按下"（含触摸落指）。</summary>
    private bool ConsumePointerPress()
    {
        MouseState mouse = Input.GetMouseState();
        if (mouse.LeftPressed) return true;

        int count = Input.GetTouchState().Count;
        return count > 0 && _previousTouchCount == 0;
    }

    #endregion

    private void FinishLoading()
    {
        _config = Content.LoadJson<GameConfig>("data/game");

        _playerTexture = Content.LoadTexture("sprites/player");
        _flameTexture = Content.LoadTexture("sprites/flame");
        _bulletTexture = Content.LoadTexture("sprites/bullet");
        _enemyBulletTexture = Content.LoadTexture("sprites/enemy_bullet");
        _particleTexture = Content.LoadTexture("sprites/particle");
        _starTexture = Content.LoadTexture("sprites/star");
        _powerUpTexture = Content.LoadTexture("sprites/powerup");
        _heartTexture = Content.LoadTexture("sprites/heart");

        foreach (KeyValuePair<string, EnemyConfig> pair in _config.Enemies)
        {
            if (_enemyTextures.ContainsKey(pair.Value.Texture)) continue;
            if (Content.Contains(pair.Value.Texture))
                _enemyTextures[pair.Value.Texture] = Content.LoadTexture(pair.Value.Texture);
        }

        Console.WriteLine($"[StarDefender] 内容就绪，共 {Content.AssetNames.Count} 个资源");
    }
}
