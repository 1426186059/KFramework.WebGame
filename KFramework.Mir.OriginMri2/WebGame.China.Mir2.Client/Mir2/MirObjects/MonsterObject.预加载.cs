using Client.MirControls;
using Client.MirGraphics;
using Client.MirScenes;
using Client.MirSounds;
using WebGame.Mir2.MonoGame.Client;
using S = ServerPackets;

namespace Client.MirObjects
{
    partial class MonsterObject : MapObject
    {
        public override void PreLoadBackground()
        {
            if (_originModel > 0)
            {
                Mir2OriginObjectLibrary.PreLoadAction(Mir2ObjectKind.Monster, _originModel, Mir2OriginObjectLibrary.MapAction(CurrentAction), EResLoadPriority.Background);
            }
        }
    }
}
