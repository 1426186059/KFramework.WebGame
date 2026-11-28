using Client.MirControls;
using Client.MirGraphics;
using Client.MirScenes;
using WebGame.Mir2.MonoGame.Client;
using S = ServerPackets;

namespace Client.MirObjects
{
    public partial class NPCObject : MapObject
    {
        public override void PreLoadBackground()
        {
            if (_originModel > 0)
            {
                Mir2OriginObjectLibrary.PreLoadAction(Mir2ObjectKind.NPC, _originModel, Mir2OriginObjectLibrary.MapAction(CurrentAction), EResLoadPriority.Background);
            }
        }
    }
}
