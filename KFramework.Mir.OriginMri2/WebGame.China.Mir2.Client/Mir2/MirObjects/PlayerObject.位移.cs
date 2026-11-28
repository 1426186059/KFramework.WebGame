using Client.MirGraphics;
using Client.MirNetwork;
using Client.MirScenes;
using Client.MirSounds;
using Client.MirControls;
using S = ServerPackets;
using C = ClientPackets;
using Client.MirScenes.Dialogs;
using WebGame.Mir2.MonoGame.Client;

namespace Client.MirObjects
{
    public partial class PlayerObject : MapObject
    {
        public void GetBackStepDistance(int magicLevel)
        {
            JumpDistance = 0;
            if (InTrapRock) return;

            int travel = 0;
            bool blocked = false;
            int dist = (magicLevel == 0) ? 1 : magicLevel;//3 max
            MirDirection jumpDir = Functions.ReverseDirection(Direction);

            Point location = CurrentLocation;
            for (int i = 0; i < dist; i++)//step 1t/m3
            {
                location = Functions.PointMove(location, jumpDir, 1);
                if (!GameScene.Scene.MapControl.ValidPoint(location)) break;

                CellInfo cInfo = GameScene.Scene.MapControl.M2CellInfo[location.X, location.Y];
                if (cInfo.CellObjects != null)
                    for (int c = 0; c < cInfo.CellObjects.Count; c++)
                    {
                        MapObject ob = cInfo.CellObjects[c];
                        if (!ob.Blocking) continue;
                        blocked = true;
                        if ((cInfo.CellObjects == null) || blocked) break;
                    }
                if (blocked) break;
                travel++;
            }
            JumpDistance = travel;
        }


        public void GetFlashDashDistance(int magicLevel)
        {
            JumpDistance = 0;
            if (InTrapRock) return;

            int travel = 0;
            bool blocked = false;
            int dist = (magicLevel <= 1) ? 0 : 1;
            MirDirection jumpDir = Direction;

            Point location = CurrentLocation;
            for (int i = 0; i < dist; i++)
            {
                location = Functions.PointMove(location, jumpDir, 1);
                if (!GameScene.Scene.MapControl.ValidPoint(location)) break;

                CellInfo cInfo = GameScene.Scene.MapControl.M2CellInfo[location.X, location.Y];
                if (cInfo.CellObjects != null)
                    for (int c = 0; c < cInfo.CellObjects.Count; c++)
                    {
                        MapObject ob = cInfo.CellObjects[c];
                        if (!ob.Blocking) continue;
                        blocked = true;
                        if ((cInfo.CellObjects == null) || blocked) break;
                    }
                if (blocked) break;
                travel++;
            }
            JumpDistance = travel;
        }


        public bool IsDashAttack()
        {
            Point location = CurrentLocation;
            location = Functions.PointMove(location, Direction, 1);

            if (!GameScene.Scene.MapControl.ValidPoint(location)) return false;

            CellInfo cInfo = GameScene.Scene.MapControl.M2CellInfo[location.X, location.Y];

            if (cInfo.CellObjects != null)
            {
                for (int c = 0; c < cInfo.CellObjects.Count; c++)
                {
                    MapObject ob = cInfo.CellObjects[c];
                    if (ob == this) return false;
                    switch (ob.Race)
                    {
                        case ObjectType.Monster:
                        case ObjectType.Player:
                            return true;
                    }
                }
            }

            return false;
        }


    }
}