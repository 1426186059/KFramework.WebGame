using System.Collections.Generic;

namespace KFramework.Example3
{
    internal class Animation
    {
        private SpriteSheet mSpriteSheet;

        private string aniPrefix;
        public readonly List<int> mAniList = new List<int>();
        public bool IsLooping;
        public float FrameTime;

        public int FrameCount
        {
            get { return mAniList.Count; }
        }

        public Animation(SpriteSheet mSpriteSheet,
            string aniPrefix,
            int nBeginIndex,
            int nEndIndex,
            float frameTime,
            bool isLooping = true,
            bool isInvPlay = false)
        {
            this.mSpriteSheet = mSpriteSheet;
            this.aniPrefix = aniPrefix;
            this.FrameTime = frameTime;
            this.IsLooping = isLooping;

            mAniList.Clear();
            for (int i = nBeginIndex; i <= nEndIndex; i++)
            {
                mAniList.Add(i);
            }

            if(isInvPlay)
            {
                for (int i = nEndIndex - 1; i >= nBeginIndex; i--)
                {
                    mAniList.Add(i);
                }
            }
        }

        public Animation(SpriteSheet mSpriteSheet,
            string aniPrefix,
            int[] aniArrayIndex,
            float frameTime,
            bool isLooping = true)
        {
            this.mSpriteSheet = mSpriteSheet;
            this.aniPrefix = aniPrefix;
            this.FrameTime = frameTime;
            this.IsLooping = isLooping;

            mAniList.Clear();
            for (int i = 0; i < aniArrayIndex.Length; i++)
            {
                mAniList.Add(aniArrayIndex[i]);
            }
        }

        public KSprite GetSprite(int nFrameIndex)
        {
            var mSpriteIndex = mAniList[nFrameIndex];
            return new KSprite(mSpriteSheet.Sprite(aniPrefix + mSpriteIndex));
        }

    }
}