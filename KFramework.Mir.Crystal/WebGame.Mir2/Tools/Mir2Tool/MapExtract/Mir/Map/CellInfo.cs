namespace Mir.Map
{
    /// <summary>
    /// 移植自 Unity 工程的 Mir.Map.CellInfo（去掉 Unity 依赖，纯数据）。
    /// </summary>
    public class CellInfo
    {
        public short BackIndex;
        public int BackImage;
        public short MiddleIndex;
        public int MiddleImage;
        public short FrontIndex;
        public int FrontImage;

        public byte DoorIndex;
        public byte DoorOffset;

        public byte FrontAnimationFrame;
        public byte FrontAnimationTick;

        public byte MiddleAnimationFrame;
        public byte MiddleAnimationTick;

        public short TileAnimationImage;
        public short TileAnimationOffset;
        public byte TileAnimationFrames;

        public byte Light;
        public byte Unknown;
        public bool FishingCell;
    }
}
