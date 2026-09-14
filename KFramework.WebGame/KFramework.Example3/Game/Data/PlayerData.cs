using System;

namespace KFramework.Example3
{
    internal class PlayerData : Singleton<PlayerData>
    {
        public int nJinBiCount = 0;
        public int nScore;
        public TimeSpan nTime;

        public int nLefeCount;
        
        public bool bEat_Mushroom;
        public bool bEat_FireFlower;
        public bool bEat_UltimateStar;
        public float UltimateStarTime = 0;

        public void Init()
        {

        }

        public void Reset()
        {
            nJinBiCount = 0;
            nTime = TimeSpan.Zero;
            nScore = 0;
            nLefeCount = 1;
        }

        public void CollectCoin()
        {
            nJinBiCount++;
            nScore += 5;
            AddScore(200);
        }

        public void AddTime(float deltaTime)
        {
            nTime += TimeSpan.FromSeconds(deltaTime);
        }

        public void AddScore(int nAddScore)
        {
            nScore += nAddScore;
        }

        public void KillEnemy()
        {
            AddScore(200);
        }

        public void Eat_PowerUp_Mushroom()
        {
            AddScore(1000);
            this.bEat_Mushroom = true;
        }

        public void Eat_PowerUp_OneUpMushroom()
        {
            AddScore(1000);
            nLefeCount++;
        }

        public void Eat_PowerUp_FireFlower()
        {
            AddScore(1000);
            this.bEat_FireFlower = true;
        }

        public void Eat_PowerUp_UltimateStar()
        {
            AddScore(1000);
            this.bEat_UltimateStar = true;
            this.UltimateStarTime += 30f;
        }

    }
}
