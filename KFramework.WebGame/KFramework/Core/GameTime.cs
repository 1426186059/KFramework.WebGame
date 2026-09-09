namespace KFramework;

/// <summary>一帧的时间信息。为结构体，每帧零分配。</summary>
public readonly struct GameTime
{
    public readonly TimeSpan TotalGameTime;
    public readonly TimeSpan ElapsedGameTime;

    public GameTime(TimeSpan totalGameTime, TimeSpan elapsedGameTime)
    {
        TotalGameTime = totalGameTime;
        ElapsedGameTime = elapsedGameTime;
    }

    /// <summary>距离上一帧的秒数。</summary>
    public float Delta => (float)ElapsedGameTime.TotalSeconds;

    /// <summary>自启动以来的总秒数。</summary>
    public float Total => (float)TotalGameTime.TotalSeconds;

    public static GameTime Zero => new(TimeSpan.Zero, TimeSpan.Zero);
}
