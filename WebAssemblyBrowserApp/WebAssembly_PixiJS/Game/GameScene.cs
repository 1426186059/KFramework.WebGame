namespace PixiGame;

/// <summary>场景基类：场景对象在 Enter 时构建到 Pixi 舞台，Update 驱动逻辑，Render 提交绘制。</summary>
public abstract class GameScene
{
    public string Name { get; }

    protected GameScene(string name) => Name = name;

    public virtual void Enter() { }
    public virtual void Exit() { }
    public abstract void Update(float dt);
    public abstract void Render();
}
