using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace KFramework.MonoGame
{
    public unsafe class KBufferCache : IDisposable
    {
        byte* cache;
        int nAllocLength;
        int nUseLength;

        public readonly int MaxAllocLength;

        public KBufferCache(int nInitSize = 1024, int nMaxSize = 0)
        {
            PrintTool.Assert(IsPowerOf2(nInitSize));
            if(nMaxSize > 0)
            {
                PrintTool.Assert(nInitSize <= nMaxSize);
                PrintTool.Assert(IsPowerOf2(nMaxSize));
            }

            this.MaxAllocLength = nMaxSize;
            cache = (byte*)NativeMemory.Alloc((nuint)nInitSize);
            nAllocLength = nInitSize;
            nUseLength = 0;
        }

        public void CopyFrom(ReadOnlySpan<byte> data)
        {
            if (nUseLength + data.Length > nAllocLength)
            {
                while(true)
                {
                    nAllocLength *= 2;
                    if (nAllocLength >= nUseLength + data.Length)
                    {
                        break;
                    }

                    if(MaxAllocLength > 0 && nAllocLength > MaxAllocLength)
                    {
                        throw new Exception($"KBufferCache: Exceed MaxAllocLength {MaxAllocLength}");
                    }
                }
                cache = (byte*)NativeMemory.Alloc((nuint)nAllocLength);
            }
            data.CopyTo(new Span<byte>(cache + nUseLength, data.Length));
            nUseLength += data.Length;
        }

        public void Clear()
        {
            nUseLength = 0;
        }

        public Span<byte> GetSpan()
        {
            return new Span<byte>(cache, nUseLength);
        }

        public int Length => nUseLength;

        public void Dispose()
        {
           NativeMemory.Free(cache);
           cache = null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsPowerOf2(int n)
        {
            return n > 0 && (n & (n - 1)) == 0;
        }
    }
}








