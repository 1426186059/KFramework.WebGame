using System;

namespace KFramework.Example3
{
    //敌人发射器
    internal class EnemyEmitter:TileBase
    {
        Level mLevel;
        EEnemyType nEnemyType;
        KTimer mTimer;

        EnemyBase mCurEnemy;
        public override Rectangle Collider2DZone
        {
            get
            {
                return Rectangle.Empty;
            }
        }

        public EnemyEmitter(Level level, Vector2 position, EEnemyType nType)
        {
            this.nEnemyType = nType;
            this.WorldPosition = position;
            this.mLevel = level;

            mTimer = KTimer.New(this, Emit, 6f, -1, false);
            mTimer.Start();
        }

        private void Emit()
        {
            if (mCurEnemy == null)
            {
                EnemyBase mEnemy = null;
                switch (nEnemyType)
                {
                    case EEnemyType.Goomba:
                        mEnemy = new Enemy_Goomba(this.mLevel, WorldPosition);
                        break;
                    case EEnemyType.Koopas:
                        mEnemy = new Enemy_Koopas(this.mLevel, WorldPosition);
                        break;
                    default:
                        throw new  NotSupportedException();
                }

                mCurEnemy = mEnemy;
            }
            else
            {
                if(mCurEnemy.IsDispose)
                {
                    mCurEnemy = null;
                }
            }
        }

        public override void Update()
        {
            base.Update();
        }

        public override void Dispose()
        {
            base.Dispose();
            mTimer.Stop();
        }

    }
}