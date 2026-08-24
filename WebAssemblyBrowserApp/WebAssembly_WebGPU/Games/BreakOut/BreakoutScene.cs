using System;
using System.Collections.Generic;
using WebAssemblyBrowserApp.Engine;

namespace WebAssemblyBrowserApp.Games;

/// <summary>
/// 打砖块演示游戏：展示了引擎的渲染、输入、音频、粒子与状态机能力。
/// </summary>
public sealed class BreakoutScene : GameScene
{
    private const int Cols = 9;
    private const int Rows = 5;
    private const double BrickW = 62;
    private const double BrickH = 20;
    private const double BrickGap = 8;
    private const double BoardTop = 78;
    private const double BoardLeft = (GameEngine.Width - Cols * (BrickW + BrickGap) + BrickGap) / 2;

    private static readonly string[] RowColors =
    {
        "#ff6b6b", "#ff9f43", "#feca57", "#48dbfb", "#5f27cd"
    };

    private static readonly int[] RowPoints = { 50, 40, 30, 20, 10 };

    private enum State { Menu, Playing, Won, GameOver }

    private State _state = State.Menu;
    private readonly Paddle _paddle = new();
    private readonly Ball _ball = new();
    private readonly List<Brick> _bricks = new();
    private readonly List<Particle> _particles = new();

    private float _stateTime;
    private float _shake;
    private int _score;
    private int _lives = 3;
    private double _highScore;

    public BreakoutScene() : base("breakout") { }

    public override void Enter()
    {
        _score = 0;
        _lives = 3;
        _state = State.Menu;
        _stateTime = 0;
        _particles.Clear();
        BuildBricks();
        ResetBall();
        double.TryParse(Storage.Get("breakout.high", "0"), out _highScore);
    }

    private void BuildBricks()
    {
        _bricks.Clear();
        for (int r = 0; r < Rows; r++)
        {
            for (int c = 0; c < Cols; c++)
            {
                _bricks.Add(new Brick(
                    BoardLeft + c * (BrickW + BrickGap),
                    BoardTop + r * (BrickH + BrickGap),
                    BrickW, BrickH,
                    RowColors[r], RowPoints[r]));
            }
        }
    }

    private void ResetBall()
    {
        _ball.Reset();
        _ball.Position = new Vector2(_paddle.Position.X, _paddle.Position.Y - _paddle.Height / 2 - _ball.Radius);
    }

    public override void Update(float dt)
    {
        _stateTime += dt;
        _shake = Math.Max(0, _shake - dt);

        if (Input.IsKeyPressed("Escape"))
        {
            GameEngine.Instance.Start("main-menu");
            return;
        }

        _paddle.Update(dt);

        switch (_state)
        {
            case State.Menu:
                if (Input.IsMousePressed() || Input.IsKeyPressed(Input.Space) || Input.IsKeyPressed(Input.Enter))
                    StartGame();
                else
                    UpdateAmbientParticles(dt);
                break;

            case State.Playing:
                UpdatePlaying(dt);
                break;

            case State.Won:
            case State.GameOver:
                if (Input.IsMousePressed() || Input.IsKeyPressed(Input.Space) || Input.IsKeyPressed(Input.Enter))
                    Enter();
                break;
        }
    }

    private void StartGame()
    {
        _state = State.Playing;
        ResetBall();
        Audio.Beep(660, 0.08, "square", 0.08);
    }

    private void UpdatePlaying(float dt)
    {
        // 球粘在挡板上，等待发射
        if (_ball.IsStuck)
        {
            _ball.Position = new Vector2(_paddle.Position.X, _paddle.Position.Y - _paddle.Height / 2 - _ball.Radius);
            if (Input.IsMousePressed() || Input.IsKeyPressed(Input.Space))
            {
                _ball.Launch(90 + MathUtils.Rand(-25, 25));
                Audio.Beep(480, 0.06, "square", 0.07);
            }
        }
        else
        {
            _ball.Update(dt);
            BounceBallOffWalls();
            BounceBallOffPaddle();
            HitBricks();
        }

        // 球落底 → 失去一条生命
        if (_ball.Position.Y - _ball.Radius > GameEngine.Height)
        {
            _lives--;
            if (_lives <= 0)
            {
                _state = State.GameOver;
                _stateTime = 0;
                SaveHighScore();
                Audio.Beep(160, 0.5, "sawtooth", 0.12);
            }
            else
            {
                ResetBall();
                Audio.Beep(200, 0.25, "triangle", 0.1);
            }
        }

        UpdateParticles(dt);
    }

    private void BounceBallOffWalls()
    {
        var p = _ball.Position;
        if (p.X - _ball.Radius < 0)
        {
            _ball.Position = new Vector2(_ball.Radius, p.Y);
            _ball.Velocity = new Vector2(Math.Abs(_ball.Velocity.X), _ball.Velocity.Y);
            WallSound();
        }
        else if (p.X + _ball.Radius > GameEngine.Width)
        {
            _ball.Position = new Vector2(GameEngine.Width - _ball.Radius, p.Y);
            _ball.Velocity = new Vector2(-Math.Abs(_ball.Velocity.X), _ball.Velocity.Y);
            WallSound();
        }

        if (p.Y - _ball.Radius < 0)
        {
            _ball.Position = new Vector2(p.X, _ball.Radius);
            _ball.Velocity = new Vector2(_ball.Velocity.X, Math.Abs(_ball.Velocity.Y));
            WallSound();
        }
    }

    private static void WallSound() => Audio.Beep(320, 0.03, "square", 0.04);

    private void BounceBallOffPaddle()
    {
        var b = _ball;
        if (b.Velocity.Y <= 0) return; // 只在向下运动时处理

        var paddle = _paddle;
        double halfW = paddle.Width / 2;
        double halfH = paddle.Height / 2;

        double nearestX = MathUtils.Clamp(b.Position.X, paddle.Position.X - halfW, paddle.Position.X + halfW);
        double nearestY = MathUtils.Clamp(b.Position.Y, paddle.Position.Y - halfH, paddle.Position.Y + halfH);
        double dx = b.Position.X - nearestX;
        double dy = b.Position.Y - nearestY;

        if (dx * dx + dy * dy <= b.Radius * b.Radius)
        {
            // 按击中位置决定反弹角度（±60°）
            double t = MathUtils.Clamp((b.Position.X - paddle.Position.X) / halfW, -1, 1);
            double angle = t * (Math.PI / 3);
            b.Speed = Math.Min(b.Speed * 1.002, 620);
            b.Velocity = new Vector2(Math.Sin(angle), -Math.Cos(angle)) * b.Speed;
            b.Position = new Vector2(b.Position.X, paddle.Position.Y - halfH - b.Radius - 0.5);

            SpawnBurst(b.Position, "#ffe066", 10, 240);
            Audio.Beep(380, 0.05, "square", 0.06);
        }
    }

    private void HitBricks()
    {
        var b = _ball;

        foreach (var brick in _bricks)
        {
            if (!brick.IsAlive) continue;

            double halfW = brick.W / 2;
            double halfH = brick.H / 2;
            double nearestX = MathUtils.Clamp(b.Position.X, brick.X - halfW, brick.X + halfW);
            double nearestY = MathUtils.Clamp(b.Position.Y, brick.Y - halfH, brick.Y + halfH);
            double dx = b.Position.X - nearestX;
            double dy = b.Position.Y - nearestY;

            if (dx * dx + dy * dy <= b.Radius * b.Radius)
            {
                brick.IsAlive = false;
                _score += brick.Points;

                if (dx * dx + dy * dy > 1e-9)
                {
                    double d = Math.Sqrt(dx * dx + dy * dy);
                    var n = new Vector2(dx / d, dy / d);
                    b.Position = new Vector2(nearestX, nearestY) + n * (b.Radius + 0.5);
                    double dot = b.Velocity.Dot(n);
                    if (dot < 0) b.Velocity -= n * (2 * dot);
                }
                else
                {
                    b.Velocity = new Vector2(-b.Velocity.X, b.Velocity.Y);
                }

                SpawnBurst(new Vector2(brick.X, brick.Y), brick.Color, 16, 300);
                _shake = 0.12f;
                Audio.Beep(300 + brick.Points * 8, 0.05, "square", 0.06);
                break;
            }
        }

        bool anyAlive = false;
        foreach (var brick in _bricks)
        {
            if (brick.IsAlive) { anyAlive = true; break; }
        }

        if (!anyAlive)
        {
            _state = State.Won;
            _stateTime = 0;
            SaveHighScore();
            Audio.Beep(523, 0.12, "square", 0.07);
            Audio.Beep(659, 0.12, "square", 0.07);
            Audio.Beep(784, 0.2, "square", 0.08);
        }
    }

    private void SaveHighScore()
    {
        if (_score > _highScore)
        {
            _highScore = _score;
            Storage.Set("breakout.high", _score.ToString());
        }
    }

    private void SpawnBurst(Vector2 pos, string color, int count, double speed)
    {
        for (int i = 0; i < count; i++)
        {
            double ang = MathUtils.Rand(0, Math.PI * 2);
            double spd = MathUtils.Rand(speed * 0.3, speed);
            _particles.Add(new Particle(
                pos,
                new Vector2(Math.Cos(ang), Math.Sin(ang)) * spd,
                MathUtils.Rand(2, 5),
                (float)MathUtils.Rand(0.3, 0.7),
                color));
        }

        if (_particles.Count > 400)
            _particles.RemoveRange(0, _particles.Count - 400);
    }

    private void UpdateParticles(float dt)
    {
        for (int i = _particles.Count - 1; i >= 0; i--)
        {
            if (!_particles[i].Update(dt))
                _particles.RemoveAt(i);
        }
    }

    private void UpdateAmbientParticles(float dt)
    {
        if (_particles.Count < 60 && Random.Shared.NextDouble() < dt * 30)
        {
            SpawnBurst(
                new Vector2(MathUtils.Rand(0, GameEngine.Width), MathUtils.Rand(GameEngine.Height - 40, GameEngine.Height)),
                "#4d6bff", 1, 40);
        }
        UpdateParticles(dt);
    }

    // ------------------------- 渲染 -------------------------

    public override void Render()
    {
        WebGPU.Clear("#0d1117");
        WebGPU.Save();

        // 屏幕震动
        if (_shake > 0)
        {
            double s = _shake * 14;
            WebGPU.Translate(MathUtils.Rand(-s, s), MathUtils.Rand(-s, s));
        }

        RenderHud();
        RenderBricks();
        RenderParticles();
        RenderPaddle();
        RenderBall();

        switch (_state)
        {
            case State.Menu:
                RenderMenu();
                break;
            case State.Won:
                RenderOverlay("你赢了！", "得分 " + _score, "#48dbfb");
                break;
            case State.GameOver:
                RenderOverlay("游戏结束", "得分 " + _score, "#ff6b6b");
                break;
        }

        WebGPU.Restore();
    }

    private void RenderHud()
    {
        WebGPU.FillText("得分", 40, 30, "bold 13px system-ui, sans-serif", "#8b949e", "left");
        WebGPU.FillText(_score.ToString(), 40, 52, "bold 26px system-ui, sans-serif", "#e6edf3", "left");

        WebGPU.FillText("最高", GameEngine.Width - 40, 30, "bold 13px system-ui, sans-serif", "#8b949e", "right");
        WebGPU.FillText(_highScore.ToString("0"), GameEngine.Width - 40, 52, "bold 26px system-ui, sans-serif", "#f6c445", "right");

        WebGPU.FillText("生命", 30, GameEngine.Height - 44, "bold 12px system-ui, sans-serif", "#8b949e", "left");
        for (int i = 0; i < _lives; i++)
        {
            WebGPU.FillCircle(34 + i * 26, GameEngine.Height - 22, 8, "#ff6b6b");
        }
    }

    private void RenderBricks()
    {
        foreach (var brick in _bricks)
        {
            if (!brick.IsAlive) continue;
            WebGPU.Shadow(brick.Color, 12);
            WebGPU.RoundedRect(brick.X - brick.W / 2, brick.Y - brick.H / 2, brick.W, brick.H, 6, brick.Color);
            WebGPU.RoundedRect(brick.X - brick.W / 2 + 3, brick.Y - brick.H / 2 + 3, brick.W - 6, brick.H * 0.4, 5, "#ffffff26");
        }
        WebGPU.NoShadow();
    }

    private void RenderParticles()
    {
        foreach (var p in _particles)
        {
            WebGPU.Alpha(p.Alpha);
            WebGPU.FillCircle(p.Position.X, p.Position.Y, p.Size * p.Alpha + 0.6, p.Color);
        }
        WebGPU.Alpha(1);
    }

    private void RenderPaddle()
    {
        var p = _paddle;
        WebGPU.Shadow("#4dabf7", 16);
        WebGPU.RoundedRect(p.Position.X - p.Width / 2, p.Position.Y - p.Height / 2, p.Width, p.Height, 8, "#4dabf7");
        WebGPU.NoShadow();
        WebGPU.RoundedRect(p.Position.X - p.Width / 2 + 5, p.Position.Y - p.Height / 2 + 3, p.Width - 10, p.Height * 0.45, 6, "#ffffff33");
    }

    private void RenderBall()
    {
        var b = _ball;
        WebGPU.Shadow("#ffe066", 14);
        WebGPU.FillCircle(b.Position.X, b.Position.Y, b.Radius, "#ffe066");
        WebGPU.NoShadow();
        WebGPU.FillCircle(b.Position.X - 2, b.Position.Y - 2, b.Radius * 0.45, "#fff8d6");
    }

    private void RenderMenu()
    {
        double cx = GameEngine.Width / 2;
        double cy = GameEngine.Height / 2;

        WebGPU.Shadow("#4dabf7", 24);
        WebGPU.FillText("BREAKOUT", cx, cy - 80, "bold 64px system-ui, sans-serif", "#e6edf3", "center");
        WebGPU.NoShadow();

        WebGPU.FillText("基于 .NET 10 + WebAssembly 的 2D 游戏引擎", cx, cy - 22, "18px system-ui, sans-serif", "#8b949e", "center");

        if ((int)(_stateTime * 2) % 2 == 0)
            WebGPU.FillText("点击或按空格键开始", cx, cy + 30, "bold 22px system-ui, sans-serif", "#ffe066", "center");

        WebGPU.FillText("← → / A D 或鼠标移动挡板  ·  空格发射", cx, GameEngine.Height - 70, "15px system-ui, sans-serif", "#6e7681", "center");
    }

    private void RenderOverlay(string title, string sub, string color)
    {
        double cx = GameEngine.Width / 2;
        double cy = GameEngine.Height / 2;

        WebGPU.Alpha(0.55);
        WebGPU.FillRect(0, 0, GameEngine.Width, GameEngine.Height, "#000000");
        WebGPU.Alpha(1);

        WebGPU.Shadow(color, 28);
        WebGPU.FillText(title, cx, cy - 30, "bold 54px system-ui, sans-serif", color, "center");
        WebGPU.NoShadow();

        WebGPU.FillText(sub, cx, cy + 22, "bold 24px system-ui, sans-serif", "#e6edf3", "center");

        if ((int)(_stateTime * 2) % 2 == 0)
            WebGPU.FillText("点击或按空格键重新开始", cx, cy + 70, "16px system-ui, sans-serif", "#8b949e", "center");
    }
}
