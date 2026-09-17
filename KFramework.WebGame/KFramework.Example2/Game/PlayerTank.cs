using KFramework.MonoGame;
using KFramework.MonoGameExtend;

namespace KFramework.Example2;

/// <summary>玩家坦克：键盘驱动。对应 PixiJS 版的 Tank_My.ts。</summary>
internal sealed class PlayerTank : TankBase
{
    protected override float Speed => TankConfig.PlayerSpeed;
    protected override int[] DirBase => TankConfig.PlayerDirBase;
    protected override KSprite[] GetSprites(ResCenter res) => res.Player;

    public override Shell? Update(float dt, TankLevel level)
    {
        var keyboard = Input.GetKeyboardState();

        if (keyboard.IsKeyDown(Keys.Up) || keyboard.IsKeyDown(Keys.W)) Move(Dir.Up, dt, level);
        else if (keyboard.IsKeyDown(Keys.Right) || keyboard.IsKeyDown(Keys.D)) Move(Dir.Right, dt, level);
        else if (keyboard.IsKeyDown(Keys.Down) || keyboard.IsKeyDown(Keys.S)) Move(Dir.Down, dt, level);
        else if (keyboard.IsKeyDown(Keys.Left) || keyboard.IsKeyDown(Keys.A)) Move(Dir.Left, dt, level);

        FireTimer -= dt;
        if (keyboard.IsKeyDown(Keys.Space) && FireTimer <= 0f)
        {
            FireTimer = TankConfig.PlayerFireInterval;
            return SpawnShell(true);
        }
        return null;
    }
}
