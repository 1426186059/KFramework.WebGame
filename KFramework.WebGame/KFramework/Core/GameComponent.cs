using KFramework.Graphics;

namespace KFramework;

/// <summary>可挂载到 <see cref="Game"/> 上的逻辑组件。</summary>
public abstract class GameComponent
{
    protected GameComponent(Game game)
    {
        ArgumentNullException.ThrowIfNull(game);
        Game = game;
    }

    public Game Game { get; }

    public bool Enabled { get; set; } = true;

    /// <summary>更新顺序，小的先更新。</summary>
    public int UpdateOrder { get; set; }

    public virtual void Initialize() { }

    public virtual void Update(GameTime gameTime) { }
}

/// <summary>带绘制能力的组件，自带一个 <see cref="SpriteBatch"/>。</summary>
public abstract class DrawableGameComponent : GameComponent
{
    protected DrawableGameComponent(Game game) : base(game) { }

    public bool Visible { get; set; } = true;

    /// <summary>绘制顺序，小的先绘制。</summary>
    public int DrawOrder { get; set; }

    public GraphicsDevice GraphicsDevice => Game.GraphicsDevice;

    protected SpriteBatch SpriteBatch { get; private set; } = null!;

    public virtual void LoadContent()
    {
        SpriteBatch ??= new SpriteBatch(GraphicsDevice);
    }

    public virtual void Draw(GameTime gameTime) { }
}

/// <summary>按 UpdateOrder / DrawOrder 排序维护组件列表。</summary>
public sealed class GameComponentCollection
{
    private readonly List<GameComponent> _components = new();
    private readonly List<DrawableGameComponent> _drawable = new();
    private bool _updateDirty;
    private bool _drawDirty;

    public int Count => _components.Count;

    public void Add(GameComponent component)
    {
        ArgumentNullException.ThrowIfNull(component);
        _components.Add(component);
        if (component is DrawableGameComponent drawable) _drawable.Add(drawable);
        _updateDirty = true;
        _drawDirty = true;
    }

    public bool Remove(GameComponent component)
    {
        if (!_components.Remove(component)) return false;
        if (component is DrawableGameComponent drawable) _drawable.Remove(drawable);
        return true;
    }

    public void Clear()
    {
        _components.Clear();
        _drawable.Clear();
    }

    internal void Initialize()
    {
        foreach (GameComponent component in _components) component.Initialize();
    }

    internal void LoadContent()
    {
        foreach (DrawableGameComponent component in _drawable) component.LoadContent();
    }

    internal void Update(GameTime gameTime)
    {
        if (_updateDirty)
        {
            _components.Sort(static (a, b) => a.UpdateOrder.CompareTo(b.UpdateOrder));
            _updateDirty = false;
        }
        for (int i = 0; i < _components.Count; i++)
        {
            GameComponent component = _components[i];
            if (component.Enabled) component.Update(gameTime);
        }
    }

    internal void Draw(GameTime gameTime)
    {
        if (_drawDirty)
        {
            _drawable.Sort(static (a, b) => a.DrawOrder.CompareTo(b.DrawOrder));
            _drawDirty = false;
        }
        for (int i = 0; i < _drawable.Count; i++)
        {
            DrawableGameComponent component = _drawable[i];
            if (component.Visible) component.Draw(gameTime);
        }
    }
}
