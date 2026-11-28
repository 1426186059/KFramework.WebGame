using Client.MirControls;
using Client.MirGraphics;
using Client.MirNetwork;
using Client.MirObjects;
using Client.MirScenes.Dialogs;
using Client.MirSounds;
using SlimDX;
using WebGame.Mir2.MonoGame.Client;
using C = ClientPackets;
using Effect = Client.MirObjects.Effect;
using S = ServerPackets;

namespace Client.MirScenes
{
    public sealed partial class GameScene : MirScene
    {
        private void KeepAlive(S.KeepAlive p)
        {
            if (p.Time == 0) return;
            CMain.PingTime = (CMain.Time - p.Time);
        }

        private void WorldMapSetup(S.WorldMapSetupInfo info)
        {
            TeleportToNPCCost = info.TeleportToNPCCost;
        }

        private void NewMapInfo(S.NewMapInfo info)
        {
            BigMapRecord newRecord = new BigMapRecord() { Index = info.MapIndex, MapInfo = info.Info };
            CreateBigMapButtons(newRecord);
            MapInfoList.Add(info.MapIndex, newRecord);
        }

        private void CreateBigMapButtons(BigMapRecord record)
        {
            record.MovementButtons.Clear();

            foreach (ClientMovementInfo mInfo in record.MapInfo.Movements)
            {
                // 地名图标改为不可点击的文字标签：去掉图标与点击音效，仅以文字标明地点名
                MirLabel label = new MirLabel()
                {
                    Text = mInfo.Title,
                    Parent = BigMapDialog.ViewPort,
                    AutoSize = true,
                    ForeColour = CommonColor.Gold,
                    OutLine = true,
                    Visible = false,
                    NotControl = true,
                };
                record.MovementButtons.Add(mInfo, label);
            }
        }

        private void RecreateBigMapButtons()
        {
            foreach (var record in MapInfoList.Values)
                CreateBigMapButtons(record);
        }

        private void SearchMapResult(S.SearchMapResult info)
        {
        }
        private void UserInformation(S.UserInformation p)
        {
            User = new UserObject(p.ObjectID);
            User.Load(p);
            MainDialog.PModeLabel.Visible = User.Class == MirClass.Wizard || User.Class == MirClass.Taoist;
            HasHero = p.HasHero;
            HeroBehaviourPanel.UpdateBehaviour(p.HeroBehaviour);
            Gold = p.Gold;
            Credit = p.Credit;
            LegendCoin = p.LegendCoin;

            CharacterDialog = new CharacterDialog(MirGridType.Equipment, User) { Parent = this, Visible = false };
            CharacterSkillDialog = new CharacterSkillDialog { Parent = this, Visible = false };
            InventoryDialog.RefreshInventory();
            SkillBarDialogs.UpdateUI();
            AllowObserve = p.AllowObserve;
            Observing = p.Observer;
        }
        private void UserSlotsRefresh(S.UserSlotsRefresh p)
        {
            User.SetSlots(p);
        }

        private void UserLocation(S.UserLocation p)
        {
            MapControl.NextAction = 0;
            if (User.CurrentLocation == p.Location && User.Direction == p.Direction) return;

            // 临时诊断：自动战斗跑动被服务端校正时，打印本地预测位置 vs 服务端位置。
            if (GameScene.Scene != null && GameScene.Scene.MapControl != null && GameScene.Scene.MapControl.AutoFight)
                PrintTool.Log($"DBG:AutoFight校正 本地预测={User.CurrentLocation} 服务端={p.Location} 动作={User.CurrentAction} 本地朝向={User.Direction} 服务端朝向={p.Direction}");

            MapControl.RemoveObject(User);
            User.CurrentLocation = p.Location;
            User.MapLocation = p.Location;
            MapControl.AddObject(User);

            MapControl.FloorValid = false;
            MapControl.InputDelay = CMain.Time + 400;

            if (User.Dead) return;

            User.ClearMagic();
            User.QueuedAction = null;

            for (int i = User.ActionFeed.Count - 1; i >= 0; i--)
            {
                if (User.ActionFeed[i].Action == MirAction.Pushed) continue;
                User.ActionFeed.RemoveAt(i);
            }

            User.SetAction();
        }
        private void ReceiveChat(S.Chat p)
        {
            ChatDialog.ReceiveChat(p.Message, p.Type);
        }
        private void ObjectPlayer(S.ObjectPlayer p)
        {
            PlayerObject player = new PlayerObject(p.ObjectID);
            player.Load(p);
        }

        private void ObjectHero(S.ObjectHero p)
        {
            HeroObject hero = new HeroObject(p.ObjectID);
            hero.Load(p);

            if (p.ObjectID == Hero?.ObjectID)
                HeroObject = hero;
        }

        private void ObjectRemove(S.ObjectRemove p)
        {
            if (p.ObjectID == User.ObjectID) return;

            if (MapControl.Objects.TryGetValue(p.ObjectID, out MapObject ob))
            {
                if (ob.Race == ObjectType.Monster) PrintTool.Log($"DBG:ObjectRemove id={p.ObjectID}");
                ob.Remove();
            }
        }
        private void ObjectTurn(S.ObjectTurn p)
        {
            if (p.ObjectID == User.ObjectID && !Observing) return;

            if (MapControl.Objects.TryGetValue(p.ObjectID, out MapObject ob))
                ob.ActionFeed.Add(new QueuedAction { Action = MirAction.Standing, Direction = p.Direction, Location = p.Location });
        }

        private void ObjectWalk(S.ObjectWalk p)
        {
            if (p.ObjectID == User.ObjectID && !Observing) return;

            if (p.ObjectID == Hero?.ObjectID)
                Hero.CurrentLocation = p.Location;

            if (MapControl.Objects.TryGetValue(p.ObjectID, out MapObject ob))
                ob.ActionFeed.Add(new QueuedAction { Action = MirAction.Walking, Direction = p.Direction, Location = p.Location });
        }

        private void ObjectRun(S.ObjectRun p)
        {
            if (p.ObjectID == User.ObjectID && !Observing) return;

            if (p.ObjectID == Hero?.ObjectID)
                Hero.CurrentLocation = p.Location;

            if (MapControl.Objects.TryGetValue(p.ObjectID, out MapObject ob))
                ob.ActionFeed.Add(new QueuedAction { Action = MirAction.Running, Direction = p.Direction, Location = p.Location });
        }

        private void ObjectChat(S.ObjectChat p)
        {
            // 说话都进聊天窗口；头顶气泡只给玩家，怪物/NPC 不显示。
            ChatDialog.ReceiveChat(p.Text, p.Type);

            if (!MapControl.Objects.TryGetValue(p.ObjectID, out MapObject ob)) return;
            if (!(ob is PlayerObject)) return;

            ob.Chat(RegexFunctions.CleanChatString(p.Text));
        }

        private void NewMonsterInfo(S.NewMonsterInfo info)
        {
            GameScene.MonsterInfoList.RemoveAll(x => x.Index == info.Info.Index);
            GameScene.MonsterInfoList.Add(info.Info);
            GameScene.OnMonsterInfoReceived(info.Info.Index);
        }

        private void NewNPCInfo(S.NewNPCInfo info)
        {
            GameScene.NPCInfoList.RemoveAll(x => x.Index == info.Info.Index);
            GameScene.NPCInfoList.Add(info.Info);
            GameScene.OnNPCInfoReceived(info.Info.Index);
        }


        private void MapChanged(S.MapChanged p)
        {
            var isCurrentMap = (MapControl.Index == p.MapIndex);

            if (isCurrentMap)
                MapControl.ResetMap(false);
            else
            {
                MapControl.Index = p.MapIndex;
                MapControl.FileName = Path.Combine(Settings.MapPath, p.FileName + ".map");
                MapControl.Title = p.Title;
                MapControl.MiniMap = p.MiniMap;
                MapControl.BigMap = p.BigMap;
                MapControl.Music = p.Music;
                MapLoadStart = DateTime.Now;   // 采样：整条地图加载链路的起点
                //KFramework.MonoGame.PrintTool.Log($"[Map] 收到 MapChanged: {p.FileName}(Index={p.MapIndex})  切到={Path.GetFileNameWithoutExtension(MapControl.FileName)}  时刻={DateTime.Now:HH:mm:ss.fff}");
                _ = MapControl.LoadMapAsync();
            }

            MapControl.NextAction = 0;
            Scene.MapControl.AutoPath = false;
            User.CurrentLocation = p.Location;
            User.MapLocation = p.Location;
            MapControl.AddObject(User);

            User.Direction = p.Direction;

            User.QueuedAction = null;
            User.ActionFeed.Clear();
            User.ClearMagic();
            User.SetAction();

            GameScene.CanRun = false;

            MapControl.FloorValid = false;
            MapControl.InputDelay = CMain.Time + 400;
        }

        private void MapInformation(S.MapInformation p)
        {
            if (MapControl != null && !MapControl.IsDisposed)
                MapControl.Dispose();
            MapControl = new MapControl
            {
                Index = p.MapIndex,
                FileName = Path.Combine(Settings.MapPath, p.FileName + ".map"),
                Title = p.Title,
                MiniMap = p.MiniMap,
                BigMap = p.BigMap,
                Lightning = p.Lightning,
                Fire = p.Fire,
                Music = p.Music
            };
            // 登录进游戏的第一张地图（服务器按角色存档下发），随后可能紧跟一个 MapChanged 纠正到实际所在图。
            MapLoadStart = DateTime.Now;   // 采样：整条地图加载链路的起点
            //KFramework.MonoGame.PrintTool.Log($"[Map] 收到 MapInformation: {p.FileName}(Index={p.MapIndex})  时刻={DateTime.Now:HH:mm:ss.fff}");
            _ = MapControl.LoadMapAsync();
            InsertControl(0, MapControl);
        }

        private void TeleportIn()
        {
            User.Effects.Add(new Effect(Libraries.Magic, 260, 10, 500, User));
            SoundManager.PlaySound(SoundList.Teleport);
        }

        private void MapEffect(S.MapEffect p)
        {
            switch (p.Effect)
            {
                case SpellEffect.Mine:
                    SoundManager.PlaySound(10091);
                    Effect HitWall = new Effect(Libraries.Effect, 8 * p.Value, 3, 240, p.Location) { Light = 0 };
                    MapControl.Effects.Add(HitWall);
                    break;
                case SpellEffect.Tester:
                    Effect eff = new Effect(Libraries.Effect, 328, 10, 500, p.Location) { Light = 0 };
                    MapControl.Effects.Add(eff);
                    break;
            }
        }

    }
}