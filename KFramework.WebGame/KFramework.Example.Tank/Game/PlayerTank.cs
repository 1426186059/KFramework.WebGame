using KFramework.MonoGame;
using KFramework.MonoGameExtend;

namespace KFramework.Example.Tank
{

    /// <summary>玩家坦克：键盘驱动。对应 PixiJS 版的 Tank_My.ts。</summary>
    internal sealed class PlayerTank : TankBase
    {
        protected override float Speed => TankConfig.PlayerSpeed;
        protected override int[] DirBase => TankConfig.PlayerDirBase;
        protected override KSprite[] GetSprites(ResCenter res) => res.Player;

        public override Shell? Update(float dt, TankLevel level)
        {
            if (Input_KeyBoard.GetKey(Keys.ArrowUp) || Input_KeyBoard.GetKey(Keys.KeyW))
            {
                Move(Dir.Up, dt, level);
            }
            else if (Input_KeyBoard.GetKey(Keys.ArrowRight) || Input_KeyBoard.GetKey(Keys.KeyD))
            {
                Move(Dir.Right, dt, level);
            }
            else if (Input_KeyBoard.GetKey(Keys.ArrowDown) || Input_KeyBoard.GetKey(Keys.KeyS))
            {
                Move(Dir.Down, dt, level);
            }
            else if (Input_KeyBoard.GetKey(Keys.ArrowLeft) || Input_KeyBoard.GetKey(Keys.KeyA))
            {
                Move(Dir.Left, dt, level);
            }
            
            FireTimer -= dt;
            if (Input_KeyBoard.GetKeyDown(Keys.Space) && FireTimer <= 0f)
            {
                FireTimer = TankConfig.PlayerFireInterval;
                return SpawnShell(true);
            }
            return null;
        }
    }

}
