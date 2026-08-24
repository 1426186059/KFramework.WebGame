namespace WebAssemblyBrowserApp.Engine;

/// <summary>游戏场景基类：菜单、关卡、结算等均可作为独立场景。</summary>
public abstract class GameScene
{
    public string Name { get; }

    protected GameScene(string name) => Name = name;

    public virtual void Enter() { }
    public virtual void Exit() { }

    public abstract void Update(float dt);
    public abstract void Render();
}
