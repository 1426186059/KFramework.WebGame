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
        if (Input_KeyBoard.GetKeyDown(Keys.Up) || Input_KeyBoard.GetKeyDown(Keys.W)) Move(Dir.Up, dt, level);
        else if (Input_KeyBoard.GetKeyDown(Keys.Right) || Input_KeyBoard.GetKeyDown(Keys.D)) Move(Dir.Right, dt, level);
        else if (Input_KeyBoard.GetKeyDown(Keys.Down) || Input_KeyBoard.GetKeyDown(Keys.S)) Move(Dir.Down, dt, level);
        else if (Input_KeyBoard.GetKeyDown(Keys.Left) || Input_KeyBoard.GetKeyDown(Keys.A)) Move(Dir.Left, dt, level);

        FireTimer -= dt;
        if (Input_KeyBoard.GetKeyDown(Keys.Space) && FireTimer <= 0f)
        {
            FireTimer = TankConfig.PlayerFireInterval;
            return SpawnShell(true);
        }
        return null;
    }
}
