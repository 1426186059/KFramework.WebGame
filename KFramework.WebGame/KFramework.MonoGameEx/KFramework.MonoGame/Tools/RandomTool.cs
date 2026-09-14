using System;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;

namespace KFramework.MonoGame
{
    public static class RandomTool
    {
        [ThreadStatic]
        private static Random m_Random;

        private static Random RandomGenerator
        {
            get
            {
                if (m_Random == null)
                {
                    m_Random = new Random(RandomNumberGenerator.GetInt32(int.MaxValue));
                }
                return m_Random;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int RandomArrayIndex(int x, int y)
        {
            return RandomGenerator.Next(x, y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static long InnerRandomInt64(long x, long y)
        {
            return (long)(x + RandomGenerator.NextDouble() * (y - x));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong InnerRandomUInt64(ulong x, ulong y)
        {
            return (ulong)(x + RandomGenerator.NextDouble() * (y - x));
        }

        public static int RandomInt32(int x, int y)
        {
            PrintTool.Assert(x >= int.MinValue);
            PrintTool.Assert(y <= int.MaxValue);
            PrintTool.Assert(x <= y);
            return (int)InnerRandomInt64(x, y);
        }

        public static uint RandomUInt32(uint x, uint y)
        {
            PrintTool.Assert(x >= uint.MinValue);
            PrintTool.Assert(y <= uint.MaxValue);
            PrintTool.Assert(x <= y);
            return (uint)InnerRandomInt64(x, y);
        }

        public static ulong RandomUInt64(ulong x, ulong y)
        {
            PrintTool.Assert(x >= ulong.MinValue);
            PrintTool.Assert(y <= ulong.MaxValue);
            PrintTool.Assert(x <= y);
            return InnerRandomUInt64(x, y);
        }

        public static long RandomInt64(long x, long y)
        {
            PrintTool.Assert(x >= long.MinValue);
            PrintTool.Assert(y <= long.MaxValue);
            PrintTool.Assert(x <= y);
            return InnerRandomInt64(x, y);
        }

        public static int GetIndexByRate(int[] mRateList)
        {
            int nSumRate = 0;
            foreach (var nRate in mRateList)
            {
                nSumRate = nSumRate + nRate;
            }

            int nTempTargetRate = nSumRate + 1;
            if (nSumRate >= 1)
            {
                nTempTargetRate = RandomInt32(1, nSumRate);
            }

            int nTempRate = 0;
            int nTargetIndex = -1;
            for (int i = 0; i < mRateList.Length; i++)
            {
                nTempRate = nTempRate + mRateList[i];
                if (nTempRate >= nTempTargetRate)
                {
                    nTargetIndex = i;
                    break;
                }
            }

            return nTargetIndex;
        }
    }
}


