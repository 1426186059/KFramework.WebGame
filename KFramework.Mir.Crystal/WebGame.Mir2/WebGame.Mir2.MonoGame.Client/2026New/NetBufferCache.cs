using KFramework.MonoGame;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
{
    private byte* cache;
    private int nAllocLength;
    private int nUseLength;

    private readonly int MaxAllocLength;

    public NetBufferCache(int nInitSize = 1024, int nMaxSize = 0)
    {
        PrintTool.Assert(IsPowerOf2(nInitSize));
        if (nMaxSize > 0)
        {
            PrintTool.Assert(nInitSize <= nMaxSize);
            PrintTool.Assert(IsPowerOf2(nMaxSize));
        }

        this.MaxAllocLength = nMaxSize;
        cache = (byte*)NativeMemory.Alloc((nuint)nInitSize);
        nAllocLength = nInitSize;
        nUseLength = 0;
    }

    public void WriteFrom(ReadOnlySpan<byte> data)
    {
        SureSizeOK(data.Length);
        data.CopyTo(new Span<byte>(cache + nUseLength, data.Length));
        nUseLength += data.Length;
    }

    public int WriteTo(Span<byte> data)
    {
        int nActualLength = Math.Min(data.Length, nUseLength);
        GetSpan().Slice(0, nActualLength).CopyTo(data);
        return nActualLength;
    }

    public void RemoveRange(int nByteCount)
    {
        if (nByteCount > nUseLength)
        {
            throw new Exception($"KBufferCache: RemoveRange Error");
        }

        NativeMemory.Copy(cache + nByteCount, cache, (nuint)(nUseLength - nByteCount));
        nUseLength -= nByteCount;
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

    private void SureSizeOK(int nRequiredLength)
    {
        if (nUseLength + nRequiredLength > nAllocLength)
        {
            while (true)
            {
                nAllocLength *= 2;
                if (nAllocLength >= nUseLength + nRequiredLength)
                {
                    break;
                }

                if (MaxAllocLength > 0 && nAllocLength > MaxAllocLength)
                {
                    throw new Exception($"KBufferCache: Exceed MaxAllocLength {MaxAllocLength}");
                }
            }
            cache = (byte*)NativeMemory.Realloc(cache, (nuint)nAllocLength);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsPowerOf2(int n)
    {
        return n > 0 && (n & (n - 1)) == 0;
    }
}








