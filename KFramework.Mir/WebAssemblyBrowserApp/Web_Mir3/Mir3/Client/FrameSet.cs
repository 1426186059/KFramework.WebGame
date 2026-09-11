using System;
using System.Collections.Generic;

namespace MirClient.Assets;

/// <summary>
/// 从原版 Zircon <c>LibraryCore/FrameSet.cs</c> + <c>Enum.cs</c> 忠实移植的动画帧表。
/// 字段名、枚举值、索引常量与原版逐一对应，便于后续模块化移植时直接对照原码。
/// 仅去掉了 DX / WinForms 依赖，纯数据，可在 WebAssembly 下编译运行。
/// </summary>

/// <summary>动画枚举（原版 <c>Library.MirAnimation</c>，值顺序保持一致）。</summary>
public enum MirAnimation : byte
{
    Standing,
    Walking,
    CreepStanding,
    CreepWalkSlow,
    CreepWalkFast,
    Running,
    Pushed,
    Combat1,
    Combat2,
    Combat3,
    Combat4,
    Combat5,
    Combat6,
    Combat7,
    Combat8,
    Combat9,
    Combat10,
    Combat11,
    Combat12,
    Combat13,
    Combat14,
    Combat15,
    Harvest,
    Stance,
    Struck,
    Die,
    Dead,
    Skeleton,
    Show,
    Hide,

    HorseStanding,
    HorseWalking,
    HorseRunning,
    HorseStruck,
    HorseLeaping,

    StoneStanding,

    DragonRepulseStart,
    DragonRepulseMiddle,
    DragonRepulseEnd,

    ChannellingStart,
    ChannellingMiddle,
    ChannellingEnd,

    FishingCast,
    FishingWait,
    FishingReel,

    TamingCast,
    TamingWait
}

/// <summary>
/// 单组动画帧（原版 <c>Library.Frame</c>）：起始索引、帧数、方向步进、各帧延时。
/// 与 Zircon 的 <c>index = StartIndex + Direction * OffSet + Frame</c> 公式配合。
/// </summary>
public sealed class Frame
{
    public static Frame EmptyFrame = new(0, 0, 0, TimeSpan.Zero);

    public int StartIndex;
    public int FrameCount;
    public int OffSet;

    public bool Reversed, StaticSpeed;

    /// <summary>每帧冻结时长；索引即帧序号。</summary>
    public TimeSpan[] Delays;

    public double Sum
    {
        get
        {
            TimeSpan sum = TimeSpan.Zero;
            foreach (var timeSpan in Delays)
                sum = sum.Add(timeSpan);
            return sum.TotalMilliseconds;
        }
    }

    public Frame(int startIndex, int frameCount, int offSet, TimeSpan frameDelay)
    {
        StartIndex = startIndex;
        FrameCount = frameCount;
        OffSet = offSet;

        Delays = new TimeSpan[FrameCount];
        for (int i = 0; i < Delays.Length; i++)
            Delays[i] = frameDelay;
    }

    public Frame(Frame frame)
    {
        StartIndex = frame.StartIndex;
        FrameCount = frame.FrameCount;
        OffSet = frame.OffSet;

        Delays = new TimeSpan[FrameCount];
        for (int i = 0; i < Delays.Length; i++)
            Delays[i] = frame.Delays[i];
    }

    /// <summary>按起始时间与当前时间求当前帧（支持倒放 / 双倍速）。</summary>
    public int GetFrame(DateTime start, DateTime now, bool doubleSpeed)
    {
        TimeSpan enlapsed = now - start;

        if (doubleSpeed && !StaticSpeed)
            enlapsed += enlapsed;

        if (Reversed)
        {
            for (int i = 0; i < Delays.Length; i++)
            {
                enlapsed -= Delays[Delays.Length - 1 - i];
                if (enlapsed >= TimeSpan.Zero) continue;

                return i;
            }

            return Delays.Length - 1;
        }

        for (int i = 0; i < Delays.Length; i++)
        {
            enlapsed -= Delays[i];
            if (enlapsed >= TimeSpan.Zero) continue;

            return i;
        }

        return Delays.Length - 1;
    }
}

/// <summary>
/// 动画帧表集合（原版 <c>Library.FrameSet</c> 的静态字段）。
/// 这里只搬入通用基线：玩家 / 通用怪物 / 通用 NPC / 通用物品；
/// 具体怪物（ForestYeti、ZumaGuardian 等）的专属表可按需在后续移植中补入。
/// </summary>
public static class FrameSet
{
    public static Dictionary<MirAnimation, Frame> Players;
    public static Dictionary<MirAnimation, Frame> DefaultItem;
    public static Dictionary<MirAnimation, Frame> DefaultNPC;
    public static Dictionary<MirAnimation, Frame> DefaultMonster;

    static FrameSet()
    {
        Players = new Dictionary<MirAnimation, Frame>
        {
            [MirAnimation.Standing] = new Frame(0, 4, 10, TimeSpan.FromMilliseconds(500)),
            [MirAnimation.Walking] = new Frame(80, 6, 10, TimeSpan.FromMilliseconds(100)),
            [MirAnimation.Running] = new Frame(160, 6, 10, TimeSpan.FromMilliseconds(100)),
            [MirAnimation.CreepStanding] = new Frame(1680, 4, 10, TimeSpan.FromMilliseconds(500)),
            [MirAnimation.CreepWalkFast] = new Frame(1760, 6, 10, TimeSpan.FromMilliseconds(100)),
            [MirAnimation.CreepWalkSlow] = new Frame(1760, 6, 10, TimeSpan.FromMilliseconds(200)),
            [MirAnimation.Pushed] = new Frame(240, 6, 10, TimeSpan.FromMilliseconds(50)) { Reversed = true, StaticSpeed = true },
            [MirAnimation.Harvest] = new Frame(480, 2, 10, TimeSpan.FromMilliseconds(300)),
            [MirAnimation.Combat1] = new Frame(560, 5, 10, TimeSpan.FromMilliseconds(100)),
            [MirAnimation.Combat2] = new Frame(640, 5, 10, TimeSpan.FromMilliseconds(100)),
            [MirAnimation.Combat3] = new Frame(720, 6, 10, TimeSpan.FromMilliseconds(100)),
            [MirAnimation.Combat4] = new Frame(800, 6, 10, TimeSpan.FromMilliseconds(100)),
            [MirAnimation.Combat5] = new Frame(880, 10, 10, TimeSpan.FromMilliseconds(60)),
            [MirAnimation.Combat6] = new Frame(960, 10, 10, TimeSpan.FromMilliseconds(60)),
            [MirAnimation.Combat7] = new Frame(1040, 10, 10, TimeSpan.FromMilliseconds(60)),
            [MirAnimation.Combat8] = new Frame(1120, 10, 10, TimeSpan.FromMilliseconds(60)),
            [MirAnimation.Combat9] = new Frame(1200, 10, 10, TimeSpan.FromMilliseconds(60)),
            [MirAnimation.Combat10] = new Frame(1280, 10, 10, TimeSpan.FromMilliseconds(60)),
            [MirAnimation.Combat11] = new Frame(1360, 10, 10, TimeSpan.FromMilliseconds(60)),
            [MirAnimation.Combat12] = new Frame(1440, 10, 10, TimeSpan.FromMilliseconds(60)),
            [MirAnimation.Combat13] = new Frame(1520, 10, 10, TimeSpan.FromMilliseconds(60)),
            [MirAnimation.Combat14] = new Frame(1600, 10, 10, TimeSpan.FromMilliseconds(60)),
            [MirAnimation.Combat15] = new Frame(1680, 10, 10, TimeSpan.FromMilliseconds(60)),
            [MirAnimation.Stance] = new Frame(560, 3, 10, TimeSpan.FromMilliseconds(500)),
            [MirAnimation.Struck] = new Frame(1840, 3, 10, TimeSpan.FromMilliseconds(100)),
            [MirAnimation.Die] = new Frame(1920, 10, 10, TimeSpan.FromMilliseconds(100)),
            [MirAnimation.Dead] = new Frame(1929, 1, 10, TimeSpan.FromMilliseconds(1000)),
            [MirAnimation.Skeleton] = new Frame(880, 1, 10, TimeSpan.FromMilliseconds(1000)),
            [MirAnimation.Show] = new Frame(640, 10, 10, TimeSpan.FromMilliseconds(100)),
            [MirAnimation.Hide] = new Frame(640, 10, 10, TimeSpan.FromMilliseconds(100)) { Reversed = true },
            [MirAnimation.HorseStanding] = new Frame(2240, 4, 10, TimeSpan.FromMilliseconds(500)),
            [MirAnimation.HorseWalking] = new Frame(2320, 6, 10, TimeSpan.FromMilliseconds(100)),
            [MirAnimation.HorseRunning] = new Frame(2400, 6, 10, TimeSpan.FromMilliseconds(100)),
            [MirAnimation.HorseStruck] = new Frame(2480, 3, 10, TimeSpan.FromMilliseconds(100)),
            [MirAnimation.HorseLeaping] = new Frame(2240, 4, 10, TimeSpan.FromMilliseconds(125)),
            [MirAnimation.ChannellingStart] = new Frame(560, 4, 10, TimeSpan.FromMilliseconds(100)),
            [MirAnimation.ChannellingMiddle] = new Frame(563, 1, 10, TimeSpan.FromMilliseconds(1000)),
            [MirAnimation.ChannellingEnd] = new Frame(0, 1, 10, TimeSpan.FromMilliseconds(60)),
            [MirAnimation.FishingCast] = new Frame(2000, 8, 10, TimeSpan.FromMilliseconds(100)),
            [MirAnimation.FishingWait] = new Frame(2080, 6, 10, TimeSpan.FromMilliseconds(120)),
            [MirAnimation.FishingReel] = new Frame(2160, 8, 10, TimeSpan.FromMilliseconds(100)),
        };

        DefaultItem = new Dictionary<MirAnimation, Frame>
        {
            [MirAnimation.Standing] = new Frame(0, 1, 0, TimeSpan.FromMilliseconds(1000)),
        };

        DefaultNPC = new Dictionary<MirAnimation, Frame>
        {
            [MirAnimation.Standing] = new Frame(0, 4, 0, TimeSpan.FromMilliseconds(1000)),
        };

        DefaultMonster = new Dictionary<MirAnimation, Frame>
        {
            [MirAnimation.Standing] = new Frame(0, 4, 10, TimeSpan.FromMilliseconds(500)),
            [MirAnimation.Walking] = new Frame(80, 6, 10, TimeSpan.FromMilliseconds(100)),
            [MirAnimation.Pushed] = new Frame(80, 6, 10, TimeSpan.FromMilliseconds(50)) { Reversed = true, StaticSpeed = true },
            [MirAnimation.Combat1] = new Frame(160, 6, 10, TimeSpan.FromMilliseconds(100)),
            [MirAnimation.Combat2] = new Frame(160, 6, 10, TimeSpan.FromMilliseconds(100)),
            [MirAnimation.Struck] = new Frame(1840, 3, 10, TimeSpan.FromMilliseconds(100)),
            [MirAnimation.Die] = new Frame(1920, 10, 10, TimeSpan.FromMilliseconds(100)),
            [MirAnimation.Dead] = new Frame(1929, 1, 10, TimeSpan.FromMilliseconds(1000)),
            [MirAnimation.Skeleton] = new Frame(880, 1, 10, TimeSpan.FromMilliseconds(1000)),
            [MirAnimation.Show] = new Frame(640, 10, 10, TimeSpan.FromMilliseconds(100)),
            [MirAnimation.Hide] = new Frame(640, 10, 10, TimeSpan.FromMilliseconds(100)) { Reversed = true },
            [MirAnimation.StoneStanding] = new Frame(640, 1, 10, TimeSpan.FromMilliseconds(500)),
        };
    }
}
