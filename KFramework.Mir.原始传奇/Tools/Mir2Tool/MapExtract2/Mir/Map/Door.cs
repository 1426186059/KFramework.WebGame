namespace Mir.Map
{
    /// <summary>
    /// 移植自 Unity 工程 Common.cs 内的 Door（去掉 UnityEngine.Vector2 依赖）。
    /// </summary>
    public class Door
    {
        public byte index;
        public byte DoorState;      // 0: closed, 1: opening, 2: open, 3: closing
        public byte ImageIndex;
        public long LastTick;
    }
}
