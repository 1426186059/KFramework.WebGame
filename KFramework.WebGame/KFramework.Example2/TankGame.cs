using KFramework;
using KFramework.Graphics;

namespace KFramework.Example2;

/// <summary>
/// 坦克大战移植工程。
/// 当前阶段：打通「内容包 → ContentManager → SpriteBatch」链路，把地图块与玩家坦克画出来。
/// 后续在此基础上补齐：移动 / 开炮 / 敌人 AI / 关卡加载 / UI。
/// </summary>
public sealed class TankGame : Game
{
    private SpriteBatch _batch = null!;
    private readonly List<Texture2D> _mapTiles = new();
    private Texture2D? _tank;
    private bool _contentReady;

    protected override async Task LoadContentAsync()
    {
        _batch = new SpriteBatch(GraphicsDevice);

        await Content.LoadAsync().ConfigureAwait(false);

        // 地形块：Map_0 ~ Map_6（砖 / 钢 / 草 / 水 / 冰 …）
        for (int i = 0; i < 7; i++)
        {
            if (Content.TryLoadTexture($"Map_{i}", out Texture2D? tile) && tile is not null)
                _mapTiles.Add(tile);
        }

        Content.TryLoadTexture("Player1_0", out _tank);

        Console.WriteLine($"[Example2] 加载完成：地形 {_mapTiles.Count} 张，坦克 {(_tank is null ? "缺失" : "就绪")}");
        _contentReady = true;
    }

    protected override void Update(GameTime gameTime)
    {
    }

    protected override void Draw(GameTime gameTime)
    {
        if (!_contentReady) return;

        _batch.Begin();

        for (int i = 0; i < _mapTiles.Count; i++)
        {
            _batch.Draw(_mapTiles[i], new Vector2(60 + i * 40, 60), Color.White);
        }

        if (_tank is not null)
        {
            _batch.Draw(_tank, new Vector2(140, 160), Color.White);
        }

        _batch.End();
    }
}
