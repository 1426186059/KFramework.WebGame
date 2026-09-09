namespace KFramework.Content.Pipeline;

/// <summary>图集中的一块区域。</summary>
public sealed class AtlasRegion
{
    public required string Name { get; init; }
    public required int Page { get; init; }
    public required int X { get; init; }
    public required int Y { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }
}

/// <summary>一张图集页。</summary>
public sealed class AtlasPage
{
    public required int Index { get; init; }
    public required Bitmap Bitmap { get; init; }
    public required List<AtlasRegion> Regions { get; init; }
}

/// <summary>
/// 货架式（shelf）图集装箱器：先按高度降序排，再逐行摆放，行放不下就开新行／新页。
/// 实现简单、速度快，对尺寸相近的游戏精灵利用率通常在 85% 以上。
/// </summary>
public sealed class AtlasPacker
{
    private readonly List<(string Name, Bitmap Bitmap)> _sources = new();

    public int MaxSize { get; init; } = 2048;

    /// <summary>相邻精灵之间的留白，避免线性采样时渗色。</summary>
    public int Padding { get; init; } = 2;

    public int Count => _sources.Count;

    public void Add(string name, Bitmap bitmap) => _sources.Add((name, bitmap));

    /// <summary>
    /// 装箱。图集边长不是固定用 <see cref="MaxSize"/>，而是按实际需要的面积估算，
    /// 装箱不下再翻倍——小游戏只有十几个精灵时，用 256² 而不是 2048² 能省下大量显存。
    /// </summary>
    public IReadOnlyList<AtlasPage> Pack()
    {
        if (_sources.Count == 0) return Array.Empty<AtlasPage>();

        foreach ((string name, Bitmap bitmap) in _sources)
        {
            if (bitmap.Width > MaxSize || bitmap.Height > MaxSize)
                throw new InvalidOperationException($"资源 “{name}” 尺寸 {bitmap.Width}x{bitmap.Height} 超过图集上限 {MaxSize}。");
        }

        long requiredArea = 0;
        int maxSide = 0;
        foreach ((string _, Bitmap bitmap) in _sources)
        {
            requiredArea += (long)(bitmap.Width + Padding) * (bitmap.Height + Padding);
            maxSide = Math.Max(maxSide, Math.Max(bitmap.Width, bitmap.Height));
        }

        int size = 128;
        while (size < maxSide + Padding && size < MaxSize) size *= 2;
        while (size < MaxSize && (long)size * size < requiredArea * 2) size *= 2;

        while (true)
        {
            IReadOnlyList<AtlasPage> pages = PackWith(size);
            if (pages.Count <= 1 || size >= MaxSize) return pages;
            size = Math.Min(MaxSize, size * 2);
        }
    }

    private IReadOnlyList<AtlasPage> PackWith(int size)
    {
        var pages = new List<AtlasPage>();

        List<(string Name, Bitmap Bitmap)> sorted = _sources
            .OrderByDescending(static s => s.Bitmap.Height)
            .ThenByDescending(static s => s.Bitmap.Width)
            .ToList();

        Bitmap? current = null;
        var regions = new List<AtlasRegion>();
        int penX = 0, penY = 0, shelfHeight = 0;

        void NewPage()
        {
            if (current is not null) pages.Add(TrimPage(new AtlasPage { Index = pages.Count, Bitmap = current, Regions = regions }));
            current = new Bitmap(size, size);
            regions = new List<AtlasRegion>();
            penX = 0; penY = 0; shelfHeight = 0;
        }

        NewPage();

        foreach ((string name, Bitmap bitmap) in sorted)
        {
            if (penX + bitmap.Width + Padding > size)
            {
                penX = 0;
                penY += shelfHeight + Padding;
                shelfHeight = 0;
            }

            if (penY + bitmap.Height + Padding > size) NewPage();

            current!.Blit(bitmap, penX, penY);
            regions.Add(new AtlasRegion
            {
                Name = name,
                Page = pages.Count,
                X = penX,
                Y = penY,
                Width = bitmap.Width,
                Height = bitmap.Height,
            });

            penX += bitmap.Width + Padding;
            shelfHeight = Math.Max(shelfHeight, bitmap.Height);
        }

        pages.Add(TrimPage(new AtlasPage { Index = pages.Count, Bitmap = current!, Regions = regions }));
        return pages;
    }

    /// <summary>裁掉图集右侧/底部完全没有使用的部分（区域坐标保持不变）。</summary>
    private static AtlasPage TrimPage(AtlasPage page)
    {
        int width = 1, height = 1;
        foreach (AtlasRegion region in page.Regions)
        {
            width = Math.Max(width, region.X + region.Width);
            height = Math.Max(height, region.Y + region.Height);
        }

        if (width >= page.Bitmap.Width && height >= page.Bitmap.Height) return page;

        var cropped = new Bitmap(width, height);
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                cropped.SetPixel(x, y, page.Bitmap.GetPixel(x, y));

        return new AtlasPage { Index = page.Index, Bitmap = cropped, Regions = page.Regions };
    }
}
