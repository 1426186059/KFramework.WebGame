using System.Drawing;
﻿using Server.MirEnvir;

namespace Server.MirDatabase
{
    public class MovementInfo
    {
        public int MapIndex { get; set; }
        public Point Source { get; set; }
        public Point Destination { get; set; }
        public bool NeedHole { get; set; }
        public bool NeedMove { get; set; }
        public bool ShowOnBigMap { get; set; }
        public int ConquestIndex { get; set; }
        public int Icon { get; set; }

        public MovementInfo()
        {

        }

        public MovementInfo(BinaryReader reader)
        {
            MapIndex = reader.ReadInt32();
            Source = new Point(reader.ReadInt32(), reader.ReadInt32());
            Destination = new Point(reader.ReadInt32(), reader.ReadInt32());

            NeedHole = reader.ReadBoolean();
            NeedMove = reader.ReadBoolean();

            if (Envir.LoadVersion < 69) return;
            ConquestIndex = reader.ReadInt32();

            if (Envir.LoadVersion < 95) return;
            ShowOnBigMap = reader.ReadBoolean();
            Icon = reader.ReadInt32();
        }
        public void Save(BinaryWriter writer)
        {
            writer.Write(MapIndex);
            writer.Write(Source.X);
            writer.Write(Source.Y);
            writer.Write(Destination.X);
            writer.Write(Destination.Y);
            writer.Write(NeedHole);
            writer.Write(NeedMove);
            writer.Write(ConquestIndex);
            writer.Write(ShowOnBigMap);
            writer.Write(Icon);
        }


        public override string ToString()
        {
            return string.Format("{0} -> Map :{1} - {2}", Source, MapIndex, Destination);
        }
    }
}
