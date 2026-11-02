using System.Drawing;
using System.Text.Json.Serialization;
namespace Server.MirDatabase
{
    public class SafeZoneInfo
    {
        public Point Location;
        public ushort Size;      // 本地 X 轴半长（旧数据即“半径”）；矩形时即半宽
        public ushort SizeY;     // 本地 Y 轴半长；0 = 正方形（等同 Size）。仅 JSON 承载，二进制遗留格式不含此字段
        public float Rotation;   // 绕 Location 中心旋转角度（度），0 = 轴对齐。仅 JSON 承载
        public bool StartPoint;

        // 反向指向所属 MapInfo，会形成循环引用。序列化时忽略它（不再依赖 $ref），
        // 由服务端加载后统一重新挂回（见 Envir.LoadDB）。
        [JsonIgnore]
        public MapInfo Info;

        public SafeZoneInfo() { }

        public SafeZoneInfo(BinaryReader reader)
        {
            Location = new Point(reader.ReadInt32(), reader.ReadInt32());
            Size = reader.ReadUInt16();
            StartPoint = reader.ReadBoolean();
        }

        public void Save(BinaryWriter writer)
        {
            writer.Write(Location.X);
            writer.Write(Location.Y);
            writer.Write(Size);
            writer.Write(StartPoint);
        }

        public override string ToString()
        {
            return string.Format("Map: {0}- {1}", Functions.PointToString(Location), StartPoint);
        }
    }
}
