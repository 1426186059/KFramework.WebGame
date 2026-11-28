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
        public virtual void SetAction()
        {
            if (NextAction != null && !GameScene.CanMove)
            {
                switch (NextAction.Action)
                {
                    case MirAction.Walking:
                    case MirAction.Running:
                    case MirAction.MountWalking:
                    case MirAction.MountRunning:
                    case MirAction.Pushed:
                    case MirAction.DashL:
                    case MirAction.DashR:
                    case MirAction.Sneek:
                    case MirAction.Jump:
                    case MirAction.DashAttack:
                        return;
                }
            }

            if (User == this && !GameScene.Observing && CMain.Time < MapControl.NextAction)
                return;

            if (ActionFeed.Count == 0)
            {
                CurrentAction = MirAction.Standing;

                CurrentAction = CMain.Time > BlizzardStopTime ? CurrentAction : MirAction.Stance2;

                if (RidingMount)
                {
                    switch (CurrentAction)
                    {
                        case MirAction.Standing:
                            CurrentAction = MirAction.MountStanding;
                            break;
                        case MirAction.Walking:
                            CurrentAction = MirAction.MountWalking;
                            break;
                        case MirAction.Running:
                            CurrentAction = MirAction.MountRunning;
                            break;
                        case MirAction.Struck:
                            CurrentAction = MirAction.MountStruck;
                            break;
                        case MirAction.Attack1:
                            CurrentAction = MirAction.MountAttack;
                            break;
                    }
                }

                if (CurrentAction == MirAction.Standing)
                {
                    if (Class == MirClass.Archer && HasClassWeapon)
                        CurrentAction = MirAction.Standing;
                    else
                        CurrentAction = CMain.Time > StanceTime ? MirAction.Standing : MirAction.Stance;
                }

                if (Fishing) CurrentAction = MirAction.FishingWait;

                Frames.TryGetValue(CurrentAction, out Frame);
                FrameIndex = 0;
                EffectFrameIndex = 0;

                if (MapLocation != CurrentLocation)
                {
                    GameScene.Scene.MapControl.RemoveObject(this);
                    MapLocation = CurrentLocation;
                    GameScene.Scene.MapControl.AddObject(this);
                }

                if (Frame == null) return;

                FrameInterval = Frame.Interval;
                // 跟随原始传奇：用该身体模型该动作的 intval（逐模型逐动作每帧毫秒，站250/走90/攻100…）
                // 覆盖帧间隔；无配置（非 origin 玩家/缺失动作）时回落 Frame.Interval。攻击/施法的全局速度倍率仍在其后叠加。
                if (_originModel > 0 && Mir2OriginObjectLibrary.TryGetInterval(_originModel, Mir2OriginObjectLibrary.MapAction(CurrentAction), out int oiv) && oiv > 0)
                    FrameInterval = oiv;
                EffectFrameInterval = Frame.EffectInterval;

                SetLibraries();
            }
            else
            {
                QueuedAction action = ActionFeed[0];
                ActionFeed.RemoveAt(0);

                CurrentAction = action.Action;

                if (RidingMount)
                {
                    switch (CurrentAction)
                    {
                        case MirAction.Standing:
                            CurrentAction = MirAction.MountStanding;
                            break;
                        case MirAction.Walking:
                            CurrentAction = MirAction.MountWalking;
                            break;
                        case MirAction.Running:
                            CurrentAction = MirAction.MountRunning;
                            break;
                        case MirAction.Struck:
                            CurrentAction = MirAction.MountStruck;
                            break;
                        case MirAction.Attack1:
                            CurrentAction = MirAction.MountAttack;
                            break;
                    }
                }

                // 野蛮冲撞：服务端只回一个终点包，Params[0] 是冲撞起点；算出整段格数，
                // 让一次 action4 动画覆盖全程（temp 直接回退到起点），不再逐格重播。
                if (CurrentAction == MirAction.DashL || CurrentAction == MirAction.DashR)
                {
                    Point dashStart = action.Params != null && action.Params.Count > 0 && action.Params[0] is Point p
                        ? p : CurrentLocation;
                    DashSteps = Math.Max(1, Functions.MaxDistance(dashStart, action.Location));
                }

                CurrentLocation = action.Location;
                MirDirection olddirection = Direction;
                Direction = action.Direction;

                Point temp;
                switch (CurrentAction)
                {
                    case MirAction.Walking:
                    case MirAction.Running:
                    case MirAction.MountWalking:
                    case MirAction.MountRunning:
                    case MirAction.Pushed:
                    case MirAction.Sneek:
                        var steps = 0;
                        if (CurrentAction == MirAction.MountRunning) steps = 3;
                        else if (CurrentAction == MirAction.Running) steps = (Sprint && !Sneaking ? 3 : 2);
                        else steps = 1;

                        temp = Functions.PointMove(CurrentLocation, Direction, CurrentAction == MirAction.Pushed ? 0 : -steps);

                        break;
                    case MirAction.DashL:
                    case MirAction.DashR:
                        // 冲撞绘制起点 = 服务端回包里记录的冲撞起点（动画全程一次走完）。
                        temp = Functions.PointMove(CurrentLocation, Direction, -DashSteps);
                        break;
                    case MirAction.Jump:
                    case MirAction.DashAttack:
                        temp = Functions.PointMove(CurrentLocation, Direction, JumpDistance);
                        break;
                    default:
                        temp = CurrentLocation;
                        break;
                }

                if (MapLocation != temp)
                {
                    GameScene.Scene.MapControl.RemoveObject(this);
                    MapLocation = temp;
                    GameScene.Scene.MapControl.AddObject(this);
                }


                bool ArcherLayTrap = false;

                switch (CurrentAction)
                {
                    case MirAction.Pushed:
                        if (this == User)
                            MapControl.InputDelay = CMain.Time + 500;
                        Frames.TryGetValue(MirAction.Walking, out Frame);
                        break;
                    case MirAction.DashL:
                    case MirAction.DashR:
                        // 冲撞逐格动作：原始传奇 skill.action=0 → setAction(4) 前冲姿态，不是跑步帧。
                        Frames.TryGetValue(MirAction.Attack1, out Frame);
                        break;
                    case MirAction.DashAttack:
                        Frames.TryGetValue(MirAction.DashAttack, out Frame);
                        break;
                    case MirAction.DashFail:
                        Frames.TryGetValue(RidingMount ? MirAction.MountStanding : MirAction.Standing, out Frame);
                        break;
                    case MirAction.Jump:
                        Frames.TryGetValue(MirAction.Jump, out Frame);
                        break;
                    case MirAction.Attack1:
                        switch (Class)
                        {
                            case MirClass.Archer:
                                Frames.TryGetValue(CurrentAction, out Frame);
                                break;
                            case MirClass.Assassin:
                                if(GameScene.User.DoubleSlash)
                                    Frames.TryGetValue(MirAction.Attack1, out Frame);
                                else if (CMain.Shift)
                                    Frames.TryGetValue(CMain.Random.Next(100) >= 20 ? (CMain.Random.Next(100) > 40 ? MirAction.Attack1 : MirAction.Attack4) : (CMain.Random.Next(100) > 10 ? MirAction.Attack2 : MirAction.Attack3), out Frame);
                                else
                                    Frames.TryGetValue(CMain.Random.Next(100) >= 40 ? MirAction.Attack1 : MirAction.Attack4, out Frame);
                                break;
                            default:
                                if (CMain.Shift && TargetObject == null)
                                    Frames.TryGetValue(CMain.Random.Next(100) >= 20 ? MirAction.Attack1 : MirAction.Attack3, out Frame);
                                else
                                    Frames.TryGetValue(CurrentAction, out Frame);
                                break;
                        }
                        break;
                    case MirAction.Attack4:
                        Spell = (Spell)action.Params[0];
                        Frames.TryGetValue(Spell == Spell.双烈火剑 || Spell == Spell.烈火剑法 ? MirAction.Attack1 : CurrentAction, out Frame);
                        break;
                    case MirAction.Spell:
                        Spell = (Spell)action.Params[0];
                        switch (Spell)
                        {
                            case Spell.野蛮冲撞: // 野蛮冲撞
                            {
                                // 起手不预测位移、不直接进 DashL：服务端只回一个终点包，
                                // 冲撞表现全部由那一个包驱动的一次 action4 动画完成，避免播两次。
                                Frames.TryGetValue(MirAction.Attack1, out Frame);

                                // 有选中目标就朝目标真实位置冲；没有目标才用鼠标/键盘给出的方向。
                                MapObject dashTarget = MapObject.CanAttackTargetObject ?? MapObject.SelectionTarget;
                                if (dashTarget != null && !dashTarget.Dead)
                                {
                                    Direction = Functions.DirectionFromPoint(CurrentLocation, dashTarget.CurrentLocation);
                                }
                                else
                                {
                                    // 没有选中目标：按鼠标当前位置方向冲撞（不是施法前的朝向）。
                                    Direction = MapControl.MouseDirection();
                                    dashTarget = null;
                                }

                                if (this == User)
                                {
                                    // 记录真实起点：服务端终点包到达前，CurrentLocation 可能已被心跳同步到终点。
                                    DashStartLocation = CurrentLocation;
                                    // 不设置 MapControl.NextAction：服务端终点包要能立即入 ActionFeed。
                                    MapControl.NextAction = 0;
                                    GameScene.SpellTime = CMain.Time + 2500; //Spell Delay

                                    Network.Enqueue(new C.Magic
                                    {
                                        ObjectID = GameScene.User.ObjectID,
                                        Spell = Spell,
                                        Direction = Direction,
                                        TargetID = dashTarget != null ? dashTarget.ObjectID : 0,
                                    });
                                }
                                break;
                            }
                            case Spell.剑雨: // 剑刃风暴
                                Frames.TryGetValue(MirAction.Attack3, out Frame);
                                if (this == User)
                                {
                                    MapControl.NextAction = CMain.Time + 2500;
                                    GameScene.SpellTime = CMain.Time + 1500; //Spell Delay
                                    MapControl.InputDelay = CMain.Time + 1000;
                                }
                                break;
                            case Spell.逐日剑法: // 逐日剑法
                                 Frames.TryGetValue(MirAction.Attack1, out Frame);
                                if (this == User)
                                {
                                    MapControl.NextAction = CMain.Time + 2000; // 80%
                                    GameScene.SpellTime = CMain.Time + 1500; //Spell Delay
                                }
                                break;
                            case Spell.反击: // 反击
                                Frames.TryGetValue(MirAction.Attack1, out Frame);
                                if (this == User)
                                {
                                    GameScene.AttackTime = CMain.Time + User.AttackSpeed;
                                    MapControl.NextAction = CMain.Time + 100; // 80%
                                    GameScene.SpellTime = CMain.Time + 100; //Spell Delay
                                }
                                break;
                            case Spell.噬魂毒: // 毒剑
                                Frames.TryGetValue(MirAction.Attack1, out Frame);
                                if (this == User)
                                {
                                    MapControl.NextAction = CMain.Time + 2000; // 80%
                                    GameScene.SpellTime = CMain.Time + 1500; //Spell Delay
                                }
                                break;
                            case Spell.开天斩: // 开天斩
                                Frames.TryGetValue(MirAction.Attack2, out Frame);
                                if (this == User)
                                {
                                    MapControl.NextAction = CMain.Time + 1200;
                                    GameScene.SpellTime = CMain.Time + 1200; //Spell Delay
                                }
                                break;
                            case Spell.新月斩: // 新月斩
                                Frames.TryGetValue(MirAction.Attack3, out Frame);
                                if (this == User)
                                {
                                    MapControl.NextAction = CMain.Time + 2500;
                                    GameScene.SpellTime = CMain.Time + 1500; //Spell Delay
                                }
                                break;
                            case Spell.十步一杀: // 十步一杀
                                {
                                    int sLevel = (byte)action.Params[3];

                                    GetFlashDashDistance(sLevel);

                                    if (JumpDistance != 0)
                                    {
                                        Frames.TryGetValue(MirAction.DashAttack, out Frame);
                                        CurrentAction = MirAction.DashAttack;
                                        CurrentLocation = Functions.PointMove(CurrentLocation, Direction, JumpDistance);
                                    }
                                    else
                                    {
                                        Frames.TryGetValue(CMain.Random.Next(100) >= 40 ? MirAction.Attack1 : MirAction.Attack4, out Frame);
                                    }

                                    if (this == User)
                                    {
                                        MapControl.NextAction = CMain.Time;
                                        GameScene.SpellTime = CMain.Time + 250; //Spell Delay
                                        if (JumpDistance != 0) Network.Enqueue(new C.Magic { Spell = Spell, Direction = Direction });
                                    }
                                }
                                break;
                            case Spell.直射击: // 直射击
                                Frames.TryGetValue(MirAction.AttackRange2, out Frame);
                                CurrentAction = MirAction.AttackRange2;
                                if (this == User)
                                {
                                    MapControl.NextAction = CMain.Time + 1000;
                                    GameScene.SpellTime = CMain.Time + 1500; //Spell Delay
                                }
                                break;
                            case Spell.双箭: // 双重射击
                                Frames.TryGetValue(MirAction.AttackRange2, out Frame);
                                CurrentAction = MirAction.AttackRange2;
                                if (this == User)
                                {
                                    MapControl.NextAction = CMain.Time + 1000;
                                    GameScene.SpellTime = CMain.Time + 500; //Spell Delay
                                }
                                break;
                            case Spell.爆炸陷阱: // 爆炸陷阱
                                Frames.TryGetValue(MirAction.Harvest, out Frame);
                                CurrentAction = MirAction.Harvest;
                                ArcherLayTrap = true;
                                if (this == User)
                                {
                                    uint targetID = (uint)action.Params[1];
                                    Point location = (Point)action.Params[2];
                                    Network.Enqueue(new C.Magic { ObjectID = GameScene.User.ObjectID, Spell = Spell, Direction = Direction, TargetID = targetID, Location = location });
                                    MapControl.NextAction = CMain.Time + 1000;
                                    GameScene.SpellTime = CMain.Time + 1500; //Spell Delay
                                }
                                break;
                            case Spell.延迟爆炸: // 延迟爆炸
                                Frames.TryGetValue(MirAction.AttackRange2, out Frame);
                                CurrentAction = MirAction.AttackRange2;
                                if (this == User)
                                {
                                    MapControl.NextAction = CMain.Time + 1000;
                                    GameScene.SpellTime = CMain.Time + 1500; //Spell Delay
                                }
                                break;
                            case Spell.后退: // 后跃
                                {
                                    int sLevel = (byte)action.Params[3];
                                    GetBackStepDistance(sLevel);
                                    Frames.TryGetValue(MirAction.Jump, out Frame);
                                    CurrentAction = MirAction.Jump;
                                    CurrentLocation = Functions.PointMove(CurrentLocation, Functions.ReverseDirection(Direction), JumpDistance);
                                    if (this == User)
                                    {
                                        MapControl.NextAction = CMain.Time + 800;
                                        GameScene.SpellTime = CMain.Time + 2500; //Spell Delay
                                        Network.Enqueue(new C.Magic { ObjectID = GameScene.User.ObjectID, Spell = Spell, Direction = Direction });
                                    }
                                    break;
                                }
                            case Spell.元素射击: // 元素箭
                                if (HasElements && !ElementCasted)
                                {
                                    Frames.TryGetValue(MirAction.AttackRange2, out Frame);
                                    CurrentAction = MirAction.AttackRange2;
                                    if (this == User)
                                    {
                                        MapControl.NextAction = CMain.Time + 1000;
                                        GameScene.SpellTime = CMain.Time + 1500; //Spell Delay
                                    }
                                }
                                else Frames.TryGetValue(CurrentAction, out Frame);
                                if (ElementCasted) ElementCasted = false;
                                break;
                            case Spell.束缚射击: // 束缚箭
                            case Spell.吸血箭: // 吸血箭
                            case Spell.毒箭: // 毒箭
                            case Spell.残废射击: // 残废箭
                            case Spell.凝固汽油弹: // 燃烧箭
                            case Spell.召唤吸血鬼: // 召唤吸血鬼
                            case Spell.召唤蟾蜍: // 召唤蟾蜍
                            case Spell.召唤蛇: // 召唤蛇
                            case Spell.石陷阱: // 石陷阱
                                Frames.TryGetValue(MirAction.AttackRange2, out Frame);
                                CurrentAction = MirAction.AttackRange2;
                                if (this == User)
                                {
                                    MapControl.NextAction = CMain.Time + 1000;
                                    GameScene.SpellTime = CMain.Time + 1000; //Spell Delay
                                }
                                break;
                            default:
                                Frames.TryGetValue(CurrentAction, out Frame);
                                break;
                        }
                        
                        break;
                    default:
                        Frames.TryGetValue(CurrentAction, out Frame);
                        break;

                }

                //ArcherTest - Need to check for bow weapon only
                if (Class == MirClass.Archer && HasClassWeapon)
                {
                    switch (CurrentAction)
                    {
                        case MirAction.Walking:
                            Frames.TryGetValue(MirAction.WalkingBow, out Frame);
                            break;
                        case MirAction.Running:
                            Frames.TryGetValue(MirAction.RunningBow, out Frame);
                            break;
                    }
                }

                //Assassin sneekyness
                if (Class == MirClass.Assassin && Sneaking && (CurrentAction == MirAction.Walking || CurrentAction == MirAction.Running))
                {
                    Frames.TryGetValue(MirAction.Sneek, out Frame);
                }

                SetLibraries();

                FrameIndex = 0;
                EffectFrameIndex = 0;
                Spell = Spell.无;
                SpellLevel = 0;
                //NextMagic = null;

                ClientMagic magic;

                if (Frame == null) return;

                FrameInterval = Frame.Interval;
                // 跟随原始传奇：用该身体模型该动作的 intval（逐模型逐动作每帧毫秒，站250/走90/攻100…）
                // 覆盖帧间隔；无配置（非 origin 玩家/缺失动作）时回落 Frame.Interval。攻击/施法的全局速度倍率仍在其后叠加。
                if (_originModel > 0 && Mir2OriginObjectLibrary.TryGetInterval(_originModel, Mir2OriginObjectLibrary.MapAction(CurrentAction), out int oiv) && oiv > 0)
                    FrameInterval = oiv;

                // 野蛮冲撞：服务端一次回终点包，一次 action4 动画（6 帧）走完整段。
                // 用 modelInfo 的原始 intval（默认 100ms/帧，全程约 600ms），不再压缩、也不逐格重播。
                EffectFrameInterval = Frame.EffectInterval;

                if (this == User)
                {
                    switch (CurrentAction)
                    {
                        case MirAction.DashFail:
                            //CanSetAction = false;
                            break;
                        case MirAction.Standing:
                        case MirAction.MountStanding:
                            Network.Enqueue(new C.Turn { Direction = Direction });
                            MapControl.NextAction = CMain.Time + 2500;
                            GameScene.CanRun = false;
                            break;
                        case MirAction.Walking:
                        case MirAction.MountWalking:
                        case MirAction.Sneek:
                            GameScene.LastRunTime = CMain.Time;
                            Network.Enqueue(new C.Walk { Direction = Direction });
                            GameScene.Scene.MapControl.FloorValid = false;
                            GameScene.CanRun = true;
                            MapControl.NextAction = CMain.Time + 2500;
                            break;
                        case MirAction.Running:
                        case MirAction.MountRunning:
                            GameScene.LastRunTime = CMain.Time;
                            Network.Enqueue(new C.Run { Direction = Direction });
                            GameScene.Scene.MapControl.FloorValid = false;
                            MapControl.NextAction = CMain.Time + (Sprint ? 1000 : 2500);
                            break;
                        case MirAction.Pushed:
                            GameScene.LastRunTime = CMain.Time;
                            GameScene.Scene.MapControl.FloorValid = false;
                            MapControl.InputDelay = CMain.Time + 500;
                            GameScene.CanRun = false;
                            GameScene.CanMove = false;
                            break;
                        case MirAction.DashL:
                        case MirAction.DashR:
                        case MirAction.Jump:
                        case MirAction.DashAttack:
                            GameScene.LastRunTime = CMain.Time;
                            GameScene.Scene.MapControl.FloorValid = false;
                            GameScene.CanRun = false;
                            //CanSetAction = false;
                            break;
                        case MirAction.Mine:
                            Network.Enqueue(new C.Attack { Direction = Direction, Spell = Spell.无 });
                            GameScene.AttackTime = CMain.Time + (1400 - Math.Min(370, (User.Level * 14)));
                            MapControl.NextAction = CMain.Time + 2500;
                            break;
                        case MirAction.Attack1:
                        case MirAction.MountAttack:

                            // 所有攻击技能改为即时释放后，普攻一律 Spell.无；
                            // 攻杀剑术由服务端概率被动结算，主动技能走 C.Magic 施法分支。
                            Network.Enqueue(new C.Attack { Direction = Direction, Spell = Spell });

                            GameScene.AttackTime = CMain.Time + User.AttackSpeed;
                            MapControl.NextAction = CMain.Time + 2500;
                            break;
                        case MirAction.Attack2:
                            //Network.Enqueue(new C.Attack2 { Direction = Direction });
                            break;
                        case MirAction.Attack3:
                            //Network.Enqueue(new C.Attack3 { Direction = Direction });
                            break;
                        //case MirAction.Attack4:
                        //    GameScene.AttackTime = CMain.Time;// + User.AttackSpeed;
                        //    MapControl.NextAction = CMain.Time;
                        //    break;

                        case MirAction.AttackRange1:
                            {
                                GameScene.AttackTime = CMain.Time + User.AttackSpeed + 200;

                                uint targetID = (uint)action.Params[0];
                                Point location = (Point)action.Params[1];

                                Network.Enqueue(new C.RangeAttack { Direction = Direction, Location = CurrentLocation, TargetID = targetID, TargetLocation = location });
                            }
                            break;
                        case MirAction.AttackRange2:
                        case MirAction.Spell:
                            {
                                Spell = (Spell)action.Params[0];
                                uint targetID = (uint)action.Params[1];
                                Point location = (Point)action.Params[2];
                                // 自己施法时 SpellLevel 在 SetAction 开头被清零，必须在这里从 QueuedAction 取回，
                                // 否则 PlayOriginCastEffects/HitEffects 恒按 Lv1 查 skillEffects，高等级技能特效全是 Lv1。
                                SpellLevel = (byte)action.Params[3];

                                Network.Enqueue(new C.Magic { ObjectID = GameScene.User.ObjectID, Spell = Spell, Direction = Direction, TargetID = targetID, Location = location });

                                if (Spell == Spell.十步一杀)
                                {
                                    GameScene.SpellTime = CMain.Time + 250;
                                    MapControl.NextAction = CMain.Time;
                                }
                                else
                                {
                                    GameScene.SpellTime = Spell == Spell.狂雷紫电 ? CMain.Time + 2500 : CMain.Time + 1800;
                                    MapControl.NextAction = CMain.Time + 2500;
                                }
                            }
                            break;                         
                        case MirAction.Harvest:
                            if (ArcherLayTrap)
                            {
                                ArcherLayTrap = false;
                                SoundManager.PlaySound(20000 + 124 * 10);
                            }
                            else
                            {
                                Network.Enqueue(new C.Harvest { Direction = Direction });
                                MapControl.NextAction = CMain.Time + 2500;
                            }
                            break;

                    }
                }


                switch (CurrentAction)
                {
                    case MirAction.Pushed:
                        FrameIndex = Frame.Count - 1;
                        EffectFrameIndex = Frame.EffectCount - 1;
                        GameScene.Scene.Redraw();
                        break;
                    case MirAction.DashL:
                        // 野蛮冲撞等位移技能走的是 DashL 而非 Spell 分支，显式接入数据驱动起手特效。
                        PlayOriginCastEffects(Spell, SpellLevel);
                        FrameIndex = 0;
                        EffectFrameIndex = 0;
                        GameScene.Scene.Redraw();
                        break;
                    case MirAction.Jump:
                        FrameIndex = 0;
                        EffectFrameIndex = 0;
                        GameScene.Scene.Redraw();
                        break;
                    case MirAction.DashR:
                        FrameIndex = 3;
                        EffectFrameIndex = 3;
                        GameScene.Scene.Redraw();
                        break;
                    case MirAction.Walking:
                    case MirAction.Running:
                    case MirAction.MountWalking:
                    case MirAction.MountRunning:
                    case MirAction.Sneek:
                        GameScene.Scene.Redraw();
                        break;
                    case MirAction.DashAttack:
                        //FrameIndex = 0;
                        //EffectFrameIndex = 0;
                        GameScene.Scene.Redraw();

                        // 十步一杀走 DashAttack，不经过 Spell 分支，显式接入数据驱动起手特效。
                        PlayOriginCastEffects(Spell, SpellLevel);

                        if (IsDashAttack())
                        {
                            action = new QueuedAction { Action = MirAction.Attack4, Direction = Direction, Location = CurrentLocation, Params = new List<object>() };
                            action.Params.Add(Spell.十步一杀);
                            ActionFeed.Insert(0, action);
                        }
                        break;
                    case MirAction.Attack1:
                        if (this != User)
                        {
                            Spell = (Spell)action.Params[0];
                            SpellLevel = (byte)action.Params[1];
                        }

                        switch (Spell)
                        {
                            case Spell.攻杀剑术: // 攻杀剑术
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10 + (Gender == MirGender.Male ? 0 : 1));
                                break;
                            case Spell.双斩: // 双斩
                                FrameInterval = (int)(FrameInterval * 0.46f); //46% Animation Speed
                                EffectFrameInterval = (int)(EffectFrameInterval * 0.46f);

                                action = new QueuedAction { Action = MirAction.Attack4, Direction = Direction, Location = CurrentLocation, Params = new List<object>() };
                                action.Params.Add(Spell);
                                ActionFeed.Insert(0, action);

                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;
                            case Spell.刺杀剑术: // 刺杀剑术
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;
                            case Spell.半月弯刀: // 半月弯刀
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;

                            case Spell.双烈火剑: // 双烈火剑
                                //FrameInterval = FrameInterval * 9 / 10; //70% Faster Animation
                                //EffectFrameInterval = EffectFrameInterval * 9 / 10;
                                //action = new QueuedAction { Action = MirAction.Attack4, Direction = Direction, Location = CurrentLocation, Params = new List<object>() };
                                //action.Params.Add(Spell);
                                //ActionFeed.Insert(0, action);
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;

                            case Spell.双半月: // 半月弯刀（双）
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;

                            case Spell.烈火剑法: // 烈火剑法
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10 + 1);
                                break;

                            
                        }
                        break;
                    case MirAction.Attack4:
                        Spell = (Spell)action.Params[0];
                        switch (Spell)
                        {
                            case Spell.双斩: // 双斩
                                FrameInterval = (int)(FrameInterval * 0.46f); //46% Animation Speed
                                EffectFrameInterval = (int)(EffectFrameInterval * 0.46f);

                                SoundManager.PlaySound(20000 + (ushort)Spell * 10 + 1);
                                break;
                            case Spell.双烈火剑: // 双烈火剑
                                FrameInterval = FrameInterval * 9 / 10; //80% Animation Speed
                                EffectFrameInterval = EffectFrameInterval * 9 / 10;
                                break;
                            case Spell.十步一杀: // 十步一杀
                                int attackDelay = (User.AttackSpeed - 120) <= 300 ? 300 : (User.AttackSpeed - 120);

                                float attackRate = (float)(attackDelay / 300F * 10F);
                                FrameInterval = FrameInterval * (int)attackRate / 20;
                                EffectFrameInterval = EffectFrameInterval * (int)attackRate / 20;
                                break;
                        }
                        break;
                    case MirAction.Struck:
                    case MirAction.MountStruck:
                        uint attackerID = (uint)action.Params[0];
                        StruckWeapon = -2;

                        if (action.Params.Count > 1)
                        {
                            StruckWeapon = (int)action.Params[1];
                        }
                        else if (MapControl.Objects.TryGetValue(attackerID, out MapObject ob))
                            if (ob.Race == ObjectType.Player)
                            {
                                PlayerObject player = (PlayerObject)ob;
                                StruckWeapon = player.Weapon;
                                if (player.Class == MirClass.Assassin && StruckWeapon != -1)
                                    StruckWeapon = 1;
                            }

                        PlayStruckSound();
                        PlayFlinchSound();
                        break;
                    case MirAction.AttackRange1: //ArcherTest - Assign Target for other users
                        if (this != User)
                        {
                            TargetID = (uint)action.Params[0];
                            TargetPoint = (Point)action.Params[1];
                            Spell = (Spell)action.Params[2];
                        }
                        break;
                    case MirAction.AttackRange2:
                    case MirAction.Spell:
                        if (this != User)
                        {
                            Spell = (Spell)action.Params[0];
                            TargetID = (uint)action.Params[1];
                            TargetPoint = (Point)action.Params[2];
                            Cast = (bool)action.Params[3];
                            SpellLevel = (byte)action.Params[4];
                            SecondaryTargetIDs = (List<uint>)action.Params[5];
                        }

                        // 原始传奇数据驱动起手特效优先：有 skillEffects 配置的技能走 CDN 特效，
                        // 跳过下面全部 Libraries.Magic* 旧帧（旧 Lib 已移除，雷电术等旧帧看不到）。
                        if (PlayOriginCastEffects(Spell, SpellLevel))
                        {
                        }
                        else
                        switch (Spell)
                        {

                            case Spell.火球术: // 火球术
                                Effects.Add(new Effect(Libraries.Magic, 0, 10, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;



                            case Spell.治愈术: // 治愈术
                                Effects.Add(new Effect(Libraries.Magic, 200, 10, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;



                            case Spell.抗拒火环: // 抗拒火环
                                Effects.Add(new Effect(Libraries.Magic, 900, 6, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;



                            case Spell.电击: // 电击
                                Effects.Add(new Effect(Libraries.Magic, 1560, 10, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;



                            case Spell.施毒术: // 施毒术
                                Effects.Add(new Effect(Libraries.Magic, 600, 10, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;



                            case Spell.大火球: // 大火球
                                Effects.Add(new Effect(Libraries.Magic, 400, 10, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;



                            case Spell.地狱火: // 地狱火
                                Effects.Add(new Effect(Libraries.Magic, 920, 10, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;



                            case Spell.雷电术: // 雷电术
                                Effects.Add(new Effect(Libraries.Magic2, 20, 3, 300, this));
                                break;



                            case Spell.灵魂火符: // 灵魂火符
                                // 起手帧静默：原始传奇 5config.skillEffects[1304101] 只有 type=2 子弹带 sound=20025，
                                // 起手/命中帧都无独立音效。该音效统一在下方符纸飞出帧播放，避免同一技能响两遍。
                                break;



                            case Spell.召唤骷髅: // 召唤骷髅
                                // 特效/音效一律由 5config.skillEffects[13051xx] 驱动（见上方 PlayOriginCastEffects），
                                // 此处不再硬编码 effect 655 / sound 200171。
                                break;

                            case Spell.风暴逃脱: // 风暴逃脱
                                Effects.Add(new Effect(Libraries.Magic3, 590, 10, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;

                            case Spell.瞬间移动: // 瞬间移动
                                Effects.Add(new Effect(Libraries.Magic, 1590, 10, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;



                            case Spell.瞬移: // 瞬移
                                Effects.Add(new Effect(Libraries.Magic, 1590, 10, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;



                            case Spell.隐身术: // 隐身术
                                Effects.Add(new Effect(Libraries.Magic, 1520, 10, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;



                            case Spell.加速: // 疾速
                                Effects.Add(new Effect(Libraries.Magic2, 2140 + (int)Direction * 10, 6, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;



                            case Spell.狂暴: // 狂暴
                                Effects.Add(new Effect(Libraries.Magic3, 200, 8, 8 * FrameInterval, this));
                                Effects.Add(new Effect(Libraries.Magic3, 187, 10, 10 * FrameInterval, this));
                                //i don't know sound
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;


                            case Spell.金刚不坏: // 金刚护体
                                Effects.Add(new Effect(Libraries.Magic3, 550, 17, Frame.Count * FrameInterval * 4, this));
                                Effects.Add(new Effect(Libraries.Magic3, 570, 5, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;


                            case Spell.爆裂火焰: // 爆裂火焰
                                Effects.Add(new Effect(Libraries.Magic, 1650, 10, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;



                            case Spell.火墙: // 火墙
                                Effects.Add(new Effect(Libraries.Magic, 1620, 10, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;



                            case Spell.治疗圈: // 治愈术（范围）
                                Effects.Add(new Effect(Libraries.Magic3, 620, 10, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;



                            case Spell.月雾: // 月雾
                                MapControl.Effects.Add(new Effect(Libraries.Magic3, 680, 25, 1800, CurrentLocation));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;



                            case Spell.困魔咒_13111: // 困魔咒
                                Effects.Add(new Effect(Libraries.Magic, 1380, 10, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;



                            case Spell.气功波: // 气功波
                                Effects.Add(new Effect(Libraries.Magic2, 190, 6, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;



                            case Spell.赤龙斩: // 爆炎斩
                                Effects.Add(new Effect(Libraries.Magic2, 2320, 10, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;



                            case Spell.灭天火: // 灭天火
                                Effects.Add(new Effect(Libraries.Magic2, 130, 6, Frame.Count * FrameInterval, this));
                                break;



                            case Spell.召唤神兽: // 召唤神兽
                                // 特效/音效一律由 5config.skillEffects[13131xx] 驱动（见上方 PlayOriginCastEffects），
                                // 此处不再硬编码 effect 652/759/765/771 与 sound 20031。
                                break;



                            case Spell.终极强化: // 强化术
                                Effects.Add(new Effect(Libraries.Magic2, 160, 15, 1000, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;



                            case Spell.寒冰掌: // 寒冰掌
                                Effects.Add(new Effect(Libraries.Magic2, 400, 10, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;



                            case Spell.净化: // 净化术
                                Effects.Add(new Effect(Libraries.Magic2, 600, 10, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;



                            case Spell.狂雷紫电: // 狂雷紫电
                                MapControl.Effects.Add(new Effect(Libraries.Magic2, 910, 23, 1800, CurrentLocation));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;



                            case Spell.玄冰刃: // 陷阱
                                Effects.Add(new Effect(Libraries.Magic2, 2340, 11, 11 * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;



                            case Spell.月光: // 月光斩
                                Effects.Add(new Effect(Libraries.Magic2, 2380, 10, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;



                            case Spell.疾风步: // 疾风步
                                Effects.Add(new Effect(Libraries.Magic2, 2440, 16, 16 * EffectFrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;



                            case Spell.轻身术: // 轻身术
                                Effects.Add(new Effect(Libraries.Magic2, 2470, 10, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;




                            case Spell.噬魂毒: // 毒剑
                                Effects.Add(new Effect(Libraries.Magic2, 2490 + ((int)Direction * 10), 10, Frame.Count * FrameInterval + 500, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;



                            case Spell.暗黑化身: // 暗影术
                                Effects.Add(new Effect(Libraries.Magic2, 2580, 10, 10 * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;



                            case Spell.地狱雷光: // 地狱雷光
                                MapControl.Effects.Add(new Effect(Libraries.Magic, 1680, 10, Frame.Count * FrameInterval, CurrentLocation));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;



                            case Spell.群体治愈术: // 群体治愈术
                                Effects.Add(new Effect(Libraries.Magic, 1790, 10, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;



                            case Spell.冰咆哮: // 冰咆哮
                                Effects.Add(new Effect(Libraries.Magic, 3840, 10, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;



                            case Spell.魔法盾: // 魔法盾
                                Effects.Add(new Effect(Libraries.Magic, 3880, 10, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;



                            case Spell.驱魔: // 驱尸术（复活死尸）
                                Effects.Add(new Effect(Libraries.Magic, 3920, 10, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;



                            case Spell.魔力增幅: // 魔法强化
                                Effects.Add(new Effect(Libraries.Magic3, 80, 9, 9 * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;



                            case Spell.宠物强化: // 宠物强化
                                Effects.Add(new Effect(Libraries.Magic3, 200, 8, 8 * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;



                            case Spell.心灵启示: // 心灵启示
                                Effects.Add(new Effect(Libraries.Magic, 3960, 20, 1200, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;



                            case Spell.护体神盾: // 护体神盾
                                Effects.Add(new Effect(Libraries.Magic2, 1520, 10, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;



                            case Spell.无极真气: // 无极真气
                                Effects.Add(new Effect(Libraries.Magic2, 1510, 10, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;




                            case Spell.噬血术: // 噬血术
                                Effects.Add(new Effect(Libraries.Magic2, 1040, 7, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;



                            case Spell.狮子吼: // 狮子吼
                            case Spell.战吼: // 战吼
                                Effects.Add(new Effect(Libraries.Magic2, 710, 20, 1200, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10 + (Gender == MirGender.Male ? 0 : 1));
                                break;



                            case Spell.双烈火剑: // 双烈火剑
                                Effects.Add(new Effect(Libraries.Magic2, 210, 6, 500, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;



                            case Spell.困魔咒: // 困魔咒
                                Effects.Add(new Effect(Libraries.Magic2, 990, 10, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;



                            case Spell.剑雨: // 剑刃风暴
                                Effects.Add(new Effect(Libraries.Magic2, 740 + (int)Direction * 20, 15, 15 * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;



                            case Spell.逐日剑法: // 逐日剑法
                                MapControl.Effects.Add(new Effect(Libraries.Magic2, 1700 + (int)Direction * 10, 9, 9 * FrameInterval, CurrentLocation, CMain.Time + 300));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                SlashingBurstTime = CMain.Time + 2000;
                                break;



                            case Spell.反击: // 反击
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10 + 5);
                                Effects.Add(new Effect(Libraries.Magic, 3480 + (int)Direction * 10, 10, 10 * FrameInterval, this));
                                Effects.Add(new Effect(Libraries.Magic3, 140, 2, 2 * FrameInterval, this));
                                break;



                            case Spell.新月斩: // 新月斩
                                Effects.Add(new Effect(Libraries.Magic2, 2620 + (int)Direction * 20, 20, 20 * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10 + (Gender == MirGender.Male ? 0 : 1));

                               
                                break;



                            case Spell.十步一杀: // 十步一杀
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10 + (Gender == MirGender.Male ? 0 : 1));
                                int attackDelay = (User.AttackSpeed - 120) <= 300 ? 300 : (User.AttackSpeed - 120);

                                float attackRate = (float)(attackDelay / 300F * 10F);
                                FrameInterval = FrameInterval * (int)attackRate / 20;
                                EffectFrameInterval = EffectFrameInterval * (int)attackRate / 20;
                                break;


                            case Spell.分身术: // 分身术
                                Effects.Add(new Effect(Libraries.Magic2, 650, 10, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;



                            case Spell.冰风暴: // 暴风雪
                                Effects.Add(new Effect(Libraries.Magic2, 1540, 8, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                BlizzardStopTime = CMain.Time + 3000;
                                break;



                            case Spell.流星火雨: // 流星火雨
                                Effects.Add(new Effect(Libraries.Magic2, 1590, 10, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                BlizzardStopTime = CMain.Time + 3000;
                                break;



                            case Spell.重生: // 重生术
                                ReincarnationStopTime = CMain.Time + 6000;
                                break;



                            case Spell.开天斩: // 开天斩
                                Effects.Add(new Effect(Libraries.Magic2, 2230 + ((int)Direction * 10), 8, 800, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                break;



                            case Spell.元素屏障: // 元素屏障
                                if (HasElements && !ElementalBarrier)
                                {
                                    Effects.Add(new Effect(Libraries.Magic3, 1880, 8, Frame.Count * FrameInterval, this));
                                    SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                }
                                break;


                            case Spell.毒箭: // 毒箭
                                Effects.Add(new Effect(Libraries.Magic3, 2300, 8, 1000, this));
                                break;

                            case Spell.天人合一: // 天人合一
                                MapControl.Effects.Add(new Effect(Libraries.Magic3, 2710, 8, 1200, CurrentLocation));
                                SoundManager.PlaySound(20000 + 139 * 10);
                                break;



                            case Spell.火弹跳: // 火焰弹
                                Effects.Add(new Effect(Libraries.Magic, 400, 10, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell.大火球 * 10);
                                break;



                            case Spell.流星雨: // 流星雨
                                Effects.Add(new Effect(Libraries.Magic, 400, 10, Frame.Count * FrameInterval, this));
                                SoundManager.PlaySound(20000 + (ushort)Spell.大火球 * 10);
                                break;


                        }


                        break;
                    case MirAction.Dead:
                        GameScene.Scene.Redraw();
                        GameScene.Scene.MapControl.SortObject(this);
                        if (MouseObject == this) MouseObjectID = 0;
                        if (SelectionTarget == this || TargetObject == this || MagicObject == this)
                            SelectTarget(null);
                        DeadTime = CMain.Time;
                        break;

                }

            }

            GameScene.Scene.MapControl.TextureValid = false;

            NextMotion = CMain.Time + FrameInterval;
            NextMotion2 = CMain.Time + EffectFrameInterval;

            if (ElementalBarrier)
            {
                switch (CurrentAction)
                {
                    case MirAction.Struck:
                    case MirAction.MountStruck:
                        if (ElementalBarrierEffect != null)
                        {
                            ElementalBarrierEffect.Clear();
                            ElementalBarrierEffect.Remove();
                        }

                        Effects.Add(ElementalBarrierEffect = new Effect(Libraries.Magic3, 1910, 5, 600, this));
                        ElementalBarrierEffect.Complete += (o, e) => Effects.Add(ElementalBarrierEffect = new Effect(Libraries.Magic3, 1890, 16, 3200, this) { Repeat = true });
                        break;
                    default:
                        if (ElementalBarrierEffect == null)
                            Effects.Add(ElementalBarrierEffect = new Effect(Libraries.Magic3, 1890, 16, 3200, this) { Repeat = true });
                        break;
                }
            }

            if (MagicShield)
            {
                switch (CurrentAction)
                {
                    case MirAction.Struck:
                    case MirAction.MountStruck:
                        if (ShieldEffect != null)
                        {
                            ShieldEffect.Clear();
                            ShieldEffect.Remove();
                        }

                        Effects.Add(ShieldEffect = new Effect(Libraries.Magic, 3900, 3, 600, this));
                        ShieldEffect.Complete += (o, e) => Effects.Add(ShieldEffect = new Effect(Libraries.Magic, 3890, 3, 600, this) { Repeat = true });
                        break;
                    default:
                        if (ShieldEffect == null)
                            Effects.Add(ShieldEffect = new Effect(Libraries.Magic, 3890, 3, 600, this) { Repeat = true });
                        break;
                }
            }

        }


        public virtual void ProcessFrames()
        {
            if (Frame == null) return;

            switch (CurrentAction)
            {
                case MirAction.Walking:
                case MirAction.Running:
                case MirAction.MountWalking:
                case MirAction.MountRunning:
                case MirAction.Sneek:
                case MirAction.DashAttack:
                    if (!GameScene.CanMove) return;

                    GameScene.Scene.MapControl.TextureValid = false;

                    if (this == User) GameScene.Scene.MapControl.FloorValid = false;
                    if (SkipFrames) FrameIndex = Frame.Count;

                    if (UpdateFrame(false) >= Frame.Count)
                    {
                        FrameIndex = Frame.Count - 1;
                        SetAction();
                    }
                    else
                    {
                        if (this == User)
                        {
                            if (FrameIndex == 1 || FrameIndex == 4)
                                PlayStepSound();
                        }
                    }

                    UpdateWingEffect();
                    break;
                 case MirAction.Jump:
                    if (!GameScene.CanMove) return;
                    GameScene.Scene.MapControl.TextureValid = false;
                    if (this == User) GameScene.Scene.MapControl.FloorValid = false;
                    if (SkipFrames) UpdateFrame();
                    if (UpdateFrame() >= Frame.Count)
                    {
                        FrameIndex = Frame.Count - 1;
                        SetAction();
                    }
                    else
                    {
                        if (FrameIndex == 1)
                            SoundManager.PlaySound(20000 + 127 * 10 + (Gender == MirGender.Male ? 5 : 6));
                        if (FrameIndex == 7)
                            SoundManager.PlaySound(20000 + 127 * 10 + 7);
                    }
                    //Backstep wingeffect
                    UpdateWingEffect();
                    break;
                case MirAction.DashL:
                case MirAction.DashR:
                    // 冲撞是服务端驱动的固定动画，不能受玩家移动输入脉冲(CanMove)限制，
                    // 否则松手瞬间 CanMove=false 会跳过帧推进、动画不播而位置已在终点 = 瞬移。
                    // 按 FrameInterval 节流推进，一次动画走完全程。
                    if (CMain.Time >= NextMotion)
                    {
                        GameScene.Scene.MapControl.TextureValid = false;

                        if (this == User) GameScene.Scene.MapControl.FloorValid = false;

                        if (SkipFrames) FrameIndex = Frame.Count;
                        if (UpdateFrame() >= Frame.Count)
                        {
                            FrameIndex = Frame.Count - 1;
                            SetAction();
                        }
                        else
                        {
                            NextMotion = CMain.Time + FrameInterval;
                        }

                        if (UpdateFrame2() >= Frame.EffectCount) EffectFrameIndex = Frame.EffectCount - 1;
                    }
                    break;
                case MirAction.Pushed:
                    if (!GameScene.CanMove) return;

                    GameScene.Scene.MapControl.TextureValid = false;

                    if (this == User) GameScene.Scene.MapControl.FloorValid = false;

                    FrameIndex -= 2;
                    EffectFrameIndex -= 2;

                    if (FrameIndex < 0)
                    {
                        FrameIndex = 0;
                        SetAction();
                    }

                    if (EffectFrameIndex < 0) EffectFrameIndex = 0;
                    break;

                case MirAction.Standing:
                case MirAction.MountStanding:
                case MirAction.DashFail:
                case MirAction.Harvest:
                case MirAction.Stance:
                case MirAction.Stance2:
                    if (CMain.Time >= NextMotion)
                    {
                        GameScene.Scene.MapControl.TextureValid = false;

                        if (SkipFrames) UpdateFrame();

                        if (UpdateFrame() >= Frame.Count)
                        {
                            FrameIndex = Frame.Count - 1;
                            SetAction();
                        }
                        else
                        {
                            NextMotion += FrameInterval;
                        }
                    }

                    UpdateWingEffect();
                    break;  


                case MirAction.FishingCast:             
                case MirAction.FishingReel:
                case MirAction.FishingWait:
                    if (CMain.Time >= NextMotion)
                    {
                        GameScene.Scene.MapControl.TextureValid = false;

                        if (SkipFrames) UpdateFrame();

                        if (UpdateFrame() >= Frame.Count)
                        {
                            FrameIndex = Frame.Count - 1;
                            SetAction();
                        }
                        else
                        {
                            switch (FrameIndex)
                            {
                                case 1:
                                    switch (CurrentAction)
                                    {
                                        case MirAction.FishingCast:
                                            SoundManager.PlaySound(SoundList.FishingThrow);
                                            ((MirAnimatedButton)GameScene.Scene.FishingStatusDialog.FishButton).Visible = false;
                                            break;
                                        case MirAction.FishingReel:
                                            SoundManager.PlaySound(SoundList.FishingPull);
                                            break;
                                        case MirAction.FishingWait:
                                            if (FoundFish)
                                            {
                                                MapControl.Effects.Add(new Effect(Libraries.Effect, 671, 6, 720, FishingPoint) { Light = 0 });
                                                MapControl.Effects.Add(new Effect(Libraries.Effect, 665, 6, 720, FishingPoint) { Light = 0 });

                                                SoundManager.PlaySound(SoundList.Fishing);
                                                Effects.Add(new Effect(Libraries.Prguse, 1350, 2, 720, this) { Light = 0, Blend = false });

                                                ((MirAnimatedButton)GameScene.Scene.FishingStatusDialog.FishButton).Visible = true;
                                            }
                                            else
                                            {
                                                MapControl.Effects.Add(new Effect(Libraries.Effect, 650, 6, 720, FishingPoint) { Light = 0 });
                                                MapControl.Effects.Add(new Effect(Libraries.Effect, 640, 6, 720, FishingPoint) { Light = 0 });
                                                ((MirAnimatedButton)GameScene.Scene.FishingStatusDialog.FishButton).Visible = false;
                                            }

                                            ((MirAnimatedButton)GameScene.Scene.FishingStatusDialog.FishButton).AnimationCount = FoundFish ? 10 : 1;
                                            break;
                                    }
                                    break;
                            }
                            NextMotion += FrameInterval;
                        }
                    }

                    UpdateWingEffect();
                    break;     

                case MirAction.Attack1:
                case MirAction.Attack2:
                case MirAction.Attack3:
                case MirAction.Attack4:
                case MirAction.MountAttack:
                case MirAction.Mine:
                    if (CMain.Time >= NextMotion)
                    {
                        GameScene.Scene.MapControl.TextureValid = false;

                        if (SkipFrames) UpdateFrame();

                        if (UpdateFrame() >= Frame.Count)
                        {
                            //if (ActionFeed.Count == 0)
                            //    ActionFeed.Add(new QueuedAction { Action = MirAction.Stance, Direction = Direction, Location = CurrentLocation });

                            StanceTime = CMain.Time + StanceDelay;
                            FrameIndex = Frame.Count - 1;
                            SetAction();
                        }
                        else
                        {
                            if (FrameIndex == 1) PlayAttackSound();
                            NextMotion += FrameInterval;
                        }
                    }

                    UpdateWingEffect();
                    break;

                case MirAction.AttackRange1:
                    if (CMain.Time >= NextMotion)
                    {
                        GameScene.Scene.MapControl.TextureValid = false;

                        if (SkipFrames) UpdateFrame();

                        if (UpdateFrame() >= Frame.Count)
                        {
                            FrameIndex = Frame.Count - 1;
                            SetAction();
                        }
                        else
                        {
                            if (FrameIndex == 1) PlayAttackSound();
                            Missile missile;
                            switch (FrameIndex)
                            {
                                case 6:
                                    switch (Spell)
                                    {
                                        case Spell.专注: // 专注
                                            Effects.Add(new Effect(Libraries.Magic3, 2730, 10, Frame.Count * FrameInterval, this));
                                            SoundManager.PlaySound(20000 + 121 * 10 + 5);
                                            break;
                                    }

                                    break;
                                case 5:
                                    missile = CreateProjectile(1030, Libraries.Magic3, true, 5, 30, 5);
                                    StanceTime = CMain.Time + StanceDelay;
                                    SoundManager.PlaySound(20000 + 121 * 10);
                                    if (missile.Target != null)
                                    {
                                        missile.Complete += (o, e) =>
                                        {
                                            SoundManager.PlaySound(20000 + 121 * 10 + 2);
                                        };
                                    }
                                    break;
                            }

                            NextMotion += FrameInterval;
                        }
                    }

                    UpdateWingEffect();
                    break;

                case MirAction.AttackRange2:
                    if (CMain.Time >= NextMotion)
                    {
                        GameScene.Scene.MapControl.TextureValid = false;

                        if (SkipFrames) UpdateFrame();

                        if (UpdateFrame() >= Frame.Count)
                        {
                            if (Cast)
                            {
                                MapObject ob = MapControl.GetObject(TargetID);

                                Missile missile;
                                switch (Spell)
                                {
                                    case Spell.直射击: // 直射击
                                        SoundManager.PlaySound(20000 + (ushort)Spell * 10 + 0);
                                        missile = CreateProjectile(1210, Libraries.Magic3, true, 5, 30, 5);

                                        if (missile.Target != null)
                                        {
                                            missile.Complete += (o, e) =>
                                            {
                                                if (missile.Target.CurrentAction == MirAction.Dead) return;
                                                missile.Target.Effects.Add(new Effect(Libraries.Magic3, 1370, 7, 600, missile.Target));
                                                SoundManager.PlaySound(20000 + (ushort)Spell.直射击 * 10 + 2);
                                            };
                                        }
                                        break;
                                }


                                Cast = false;
                            }

                            StanceTime = CMain.Time + StanceDelay;
                            FrameIndex = Frame.Count - 1;
                            SetAction();

                        }
                        else
                        {
                            NextMotion += FrameInterval;

                            Missile missile;

                            switch (Spell)
                            {
                                case Spell.双箭: // 双重射击
                                    switch (FrameIndex)
                                    {
                                        case 7:
                                        case 5:
                                            missile = CreateProjectile(1030, Libraries.Magic3, true, 5, 30, 5);//normal arrow
                                            StanceTime = CMain.Time + StanceDelay;
                                            SoundManager.PlaySound(20000 + 121 * 10);
                                            if (missile.Target != null)
                                            {
                                                missile.Complete += (o, e) =>
                                                {
                                                    SoundManager.PlaySound(20000 + 121 * 10 + 2);
                                                };
                                            }
                                            break;
                                    }
                                    break;
                                case Spell.元素射击: // 元素箭
                                    if (HasElements && !ElementCasted)
                                        switch (FrameIndex)
                                        {
                                            case 7:
                                                missile = CreateProjectile(1690, Libraries.Magic3, true, 6, 30, 4);//elemental arrow
                                                StanceTime = CMain.Time + StanceDelay;
                                                if (missile.Target != null)
                                                {
                                                    missile.Complete += (o, e) =>
                                                    {
                                                        SoundManager.PlaySound(20000 + (ushort)Spell * 10 + 2);//sound M128-2
                                                    };
                                                }
                                                break;
                                            case 1:
                                                Effects.Add(new Effect(Libraries.Magic3, 1681, 5, Frame.Count * FrameInterval, this));
                                                SoundManager.PlaySound(20000 + (ushort)Spell * 10 + 0);//sound M128-0
                                                break;
                                        }
                                    break;
                                case Spell.束缚射击: // 束缚箭
                                case Spell.召唤吸血鬼: // 召唤吸血鬼
                                case Spell.召唤蟾蜍: // 召唤蟾蜍
                                case Spell.召唤蛇: // 召唤蛇
                                    switch (FrameIndex)
                                    {
                                        case 7:
                                            SoundManager.PlaySound(20000 + 121 * 10);
                                            missile = CreateProjectile(2750, Libraries.Magic3, true, 5, 10, 5);
                                            StanceTime = CMain.Time + StanceDelay;
                                            if (missile.Target != null)
                                            {
                                                missile.Complete += (o, e) =>
                                                {
                                                    SoundManager.PlaySound(20000 + (ushort)Spell * 10 + 7);//sound M130-7
                                                };
                                            }
                                            break;
                                    }
                                    break;
                                case Spell.石陷阱: // 石陷阱
                                    switch (FrameIndex)
                                    {
                                        case 7:
                                            SoundManager.PlaySound(20000 + 121 * 10);
                                            missile = CreateProjectile(2750, Libraries.Magic3, true, 5, 20, 5);
                                            StanceTime = CMain.Time + StanceDelay;
                                            missile.Explode = true;

                                            missile.Complete += (o, e) =>
                                            {
                                                SoundManager.PlaySound(20000 + (ushort)Spell * 10 + 7);//sound M130-7
                                            };

                                            break;
                                    }
                                    break;
                                case Spell.延迟爆炸: // 延迟爆炸
                                    switch (FrameIndex)
                                    {
                                        case 5:
                                            missile = CreateProjectile(1030, Libraries.Magic3, true, 5, 30, 5);//normal arrow
                                            StanceTime = CMain.Time + StanceDelay;
                                            SoundManager.PlaySound(20000 + 121 * 10);
                                            if (missile.Target != null)
                                            {
                                                missile.Complete += (o, e) =>
                                                {
                                                    SoundManager.PlaySound(20000 + 121 * 10 + 2);
                                                };
                                            }
                                            break;
                                    }
                                    break;
                                case Spell.吸血箭: // 吸血箭
                                case Spell.毒箭: // 毒箭
                                case Spell.残废射击: // 残废箭
                                    MapObject ob = MapControl.GetObject(TargetID);
                                    Effect eff;
                                    int exFrameStart = 0;
                                    if (Spell == Spell.毒箭) exFrameStart = 200;
                                    if (Spell == Spell.残废射击) exFrameStart = 400;
                                    switch (FrameIndex)
                                    {
                                        case 7:
                                            SoundManager.PlaySound(20000 + ((Spell == Spell.残废射击) ? 136 : 121) * 10);//M136-0
                                            missile = CreateProjectile(1930 + exFrameStart, Libraries.Magic3, true, 5, 10, 5);
                                            StanceTime = CMain.Time + StanceDelay;
                                            if (missile.Target != null)
                                            {
                                                missile.Complete += (o, e) =>
                                                {
                                                    if (ob != null)
                                                    {
                                                        if (Spell == Spell.残废射击)
                                                        {
                                                            int exIdx = 0;
                                                            if (this == User)
                                                            {
                                                                if (GameScene.Scene.BuffsDialog.Buffs.Any(x => x.Type == BuffType.VampireShot)) exIdx = 20;
                                                                if (GameScene.Scene.BuffsDialog.Buffs.Any(x => x.Type == BuffType.PoisonShot)) exIdx = 10;
                                                            }
                                                            else
                                                            {
                                                                if (Buffs.Any(x => x == BuffType.VampireShot)) exIdx = 20;
                                                                if (Buffs.Any(x => x == BuffType.PoisonShot)) exIdx = 10;
                                                            }

                                                            ob.Effects.Add(eff = new Effect(Libraries.Magic3, 2490 + exIdx, 7, 1000, ob));
                                                            SoundManager.PlaySound(20000 + 136 * 10 + 5 + (exIdx / 10));//sound M136-5/7

                                                            if (exIdx == 20)
                                                                eff.Complete += (o1, e1) =>
                                                                {
                                                                    SoundManager.PlaySound(20000 + 45 * 10 + 2);//sound M45-2
                                                                    Effects.Add(new Effect(Libraries.Magic3, 2100, 8, 1000, this));
                                                                };
                                                        }

                                                        if (Spell == Spell.吸血箭 || Spell == Spell.毒箭)
                                                        {
                                                            ob.Effects.Add(eff = new Effect(Libraries.Magic3, 2090 + exFrameStart, 6, 1000, ob));
                                                            SoundManager.PlaySound(20000 + (133 + (exFrameStart / 100)) * 10 + 2);//sound M133-2 or M135-2
                                                            if (Spell == Spell.吸血箭)
                                                                eff.Complete += (o1, e1) =>
                                                                {
                                                                    SoundManager.PlaySound(20000 + 45 * 10 + 2);//sound M45-2
                                                                    Effects.Add(new Effect(Libraries.Magic3, 2100 + exFrameStart, 8, 1000, this));
                                                                };
                                                        }
                                                    }
                                                    //SoundManager.PlaySound(20000 + 121 * 10 + 2);//sound M121-2
                                                };
                                            }
                                            break;
                                    }
                                    break;
                                case Spell.凝固汽油弹: // 燃烧箭
                                    switch (FrameIndex)
                                    {
                                        case 7:
                                            SoundManager.PlaySound(20000 + 138 * 10);//M138-0
                                            missile = CreateProjectile(2530, Libraries.Magic3, true, 6, 50, 4);
                                            StanceTime = CMain.Time + StanceDelay;
                                            if (missile.Target != null)
                                            {
                                                missile.Complete += (o, e) =>
                                                {
                                                    SoundManager.PlaySound(20000 + 138 * 10 + 2);//M138-2
                                                    MapControl.Effects.Add(new Effect(Libraries.Magic3, 2690, 10, 1000, TargetPoint));
                                                };
                                            }
                                            break;
                                    }
                                    break;
                            }
                        }
                    }

                    UpdateWingEffect();
                    break;

                case MirAction.Struck:
                case MirAction.MountStruck:
                    if (CMain.Time >= NextMotion)
                    {
                        GameScene.Scene.MapControl.TextureValid = false;

                        if (SkipFrames) UpdateFrame();

                        if (UpdateFrame() >= Frame.Count)
                        {
                            FrameIndex = Frame.Count - 1;
                            SetAction();
                        }
                        else
                        {
                            NextMotion += FrameInterval;
                        }
                    }

                    UpdateWingEffect();
                    break;
                case MirAction.Spell:
                    if (CMain.Time >= NextMotion)
                    {
                        GameScene.Scene.MapControl.TextureValid = false;

                        if (SkipFrames) UpdateFrame();

                        if (UpdateFrame() >= Frame.Count)
                        {
                            if (Cast)
                            {

                                MapObject ob = MapControl.GetObject(TargetID);

                                Missile missile;
                                Effect effect;

                                // 原始传奇数据驱动命中/飞行特效优先：有 skillEffects 配置的技能走 CDN 特效，
                                // 不再用已移除/缺失的 Libraries.Magic* 旧帧（雷电术等旧 Lib 特效看不到即因此）。
                                // 未配置的技能（纯经典技能/无特效数据）回退下面的旧 switch。
                                if (PlayOriginHitEffects(Spell, SpellLevel, ob, TargetPoint))
                                {
                                    // 特效已按原始 skillEffects 播放
                                }
                                else
                                {
                                    switch (Spell)
                                    {

                                        case Spell.火球术: // 火球术
                                            SoundManager.PlaySound(20000 + (ushort)Spell * 10 + 1);
                                            missile = CreateProjectile(10, Libraries.Magic, true, 6, 30, 4);

                                            if (missile.Target != null)
                                            {
                                                missile.Complete += (o, e) =>
                                                {
                                                    if (missile.Target.CurrentAction == MirAction.Dead) return;
                                                    missile.Target.Effects.Add(new Effect(Libraries.Magic, 170, 10, 600, missile.Target));
                                                    SoundManager.PlaySound(20000 + (ushort)Spell * 10 + 2);
                                                };
                                            }
                                            break;



                                        case Spell.大火球: // 大火球
                                            SoundManager.PlaySound(20000 + (ushort)Spell * 10 + 1);
                                            missile = CreateProjectile(410, Libraries.Magic, true, 6, 30, 4);

                                            if (missile.Target != null)
                                            {
                                                missile.Complete += (o, e) =>
                                                {
                                                    if (missile.Target.CurrentAction == MirAction.Dead) return;
                                                    missile.Target.Effects.Add(new Effect(Libraries.Magic, 570, 10, 600, missile.Target));
                                                    SoundManager.PlaySound(20000 + (ushort)Spell.大火球 * 10 + 2);
                                                };
                                            }
                                            break;



                                        case Spell.治愈术: // 治愈术
                                            SoundManager.PlaySound(20000 + (ushort)Spell * 10 + 1);
                                            if (ob == null)
                                                MapControl.Effects.Add(new Effect(Libraries.Magic, 370, 10, 800, TargetPoint));
                                            else
                                                ob.Effects.Add(new Effect(Libraries.Magic, 370, 10, 800, ob));
                                            break;



                                        case Spell.电击: // 电击
                                            SoundManager.PlaySound(20000 + (ushort)Spell * 10 + 1);
                                            if (ob == null)
                                                MapControl.Effects.Add(new Effect(Libraries.Magic, 1570, 10, 1000, TargetPoint));
                                            else
                                                ob.Effects.Add(new Effect(Libraries.Magic, 1570, 10, 1000, ob));
                                            break;


                                        case Spell.施毒术: // 施毒术
                                            SoundManager.PlaySound(20000 + (ushort)Spell * 10 + 1);
                                            if (ob != null)
                                                ob.Effects.Add(new Effect(Libraries.Magic, 770, 10, 1000, ob));
                                            break;


                                        case Spell.地狱火: // 地狱火
                                            SoundManager.PlaySound(20000 + (ushort)Spell * 10 + 1);


                                            Point dest = CurrentLocation;
                                            for (int i = 0; i < 4; i++)
                                            {
                                                dest = Functions.PointMove(dest, Direction, 1);
                                                if (!GameScene.Scene.MapControl.ValidPoint(dest)) break;
                                                effect = new Effect(Libraries.Magic, 930, 6, 500, dest) { Rate = 0.7F };

                                                effect.SetStart(CMain.Time + i * 50);
                                                MapControl.Effects.Add(effect);
                                            }

                                            if (SpellLevel == 3)
                                            {
                                                MirDirection dir = (MirDirection)(((int)Direction + 1) % 8);
                                                dest = CurrentLocation;
                                                for (int r = 0; r < 4; r++)
                                                {
                                                    dest = Functions.PointMove(dest, dir, 1);
                                                    if (!GameScene.Scene.MapControl.ValidPoint(dest)) break;
                                                    effect = new Effect(Libraries.Magic, 930, 6, 500, dest) { Rate = 0.7F };

                                                    effect.SetStart(CMain.Time + r * 50);
                                                    MapControl.Effects.Add(effect);
                                                }

                                                dir = (MirDirection)(((int)Direction - 1 + 8) % 8);
                                                dest = CurrentLocation;
                                                for (int r = 0; r < 4; r++)
                                                {
                                                    dest = Functions.PointMove(dest, dir, 1);
                                                    if (!GameScene.Scene.MapControl.ValidPoint(dest)) break;
                                                    effect = new Effect(Libraries.Magic, 930, 6, 500, dest) { Rate = 0.7F };

                                                    effect.SetStart(CMain.Time + r * 50);
                                                    MapControl.Effects.Add(effect);
                                                }
                                            }
                                            break;



                                        case Spell.雷电术: // 雷电术

                                            SoundManager.PlaySound(20000 + (ushort)Spell * 10);

                                            if (ob == null)
                                                MapControl.Effects.Add(new Effect(Libraries.Magic2, 10, 5, 400, TargetPoint));
                                            else
                                                ob.Effects.Add(new Effect(Libraries.Magic2, 10, 5, 400, ob));
                                            break;



                                        case Spell.灵魂火符: // 灵魂火符
                                                                 // 符纸飞出瞬间播原始传奇音效：5config.skillEffects[1304101].sound=20025
                                            SoundManager.PlayOriginSound(20025);
                                            // 原始传奇 5config.effect 核对过 CDN 实际画面：
                                            //   653/model13003 = 符纸（多张小符），752/model13024 = Lv5 符纸  → 飞行道具
                                            //   654/model13023 = 火焰爆炸，754/model13026 = Lv5 火焰      → 抵达目标后的爆炸
                                            // 之前把 654/754（火焰）当成飞行符从玩家身上发射，看起来就是“爆炸直接在玩家身上播放”；
                                            // 正确顺序是符纸先飞、命中时才在目标身上炸出火焰。
                                            //
                                            // 权威时序来自 5config.skillEffects[1304101/1304105]（原始传奇 JS 同款）：
                                            //   type=2 子弹 effect653/752 delaytime=400 → 飞行时长 400ms
                                            //   type=1 命中 effect654/754 delaytime=200 → 命中后火焰
                                            //   skill.hurtDelay=500 → 伤害在施法后 500ms 结算
                                            // 起点由 OriginMissile 内部按 GameDefine.SKILL_HitTestY=60 抬到手部。
                                            {
                                                int flyEffect = SpellLevel >= 5 ? 752 : 653;
                                                int hitEffect = SpellLevel >= 5 ? 754 : 654;
                                                const int flyDelay = 400;  // skillEffects type=2 delaytime
                                                Point flyDest = ob != null ? ob.CurrentLocation : TargetPoint;
                                                OriginMissile originMissile = new OriginMissile(flyEffect, this, ob, flyDest,
                                                                                                flightDuration: flyDelay);
                                                MapObject hitTarget = ob;
                                                originMissile.Complete += (o, e) =>
                                                {
                                                    if (hitTarget == null || hitTarget.CurrentAction == MirAction.Dead) return;
                                                    hitTarget.Effects.Add(new OriginEffect(hitEffect, hitTarget));
                                                    // 原始传奇命中特效(effect654/754)配置无独立 sound，命中不再叠加经典音效。
                                                };
                                                Effects.Add(originMissile);
                                            }
                                            break;



                                        case Spell.能量护盾: // 能量护盾

                                            //Effects.Add(new Effect(Libraries.Magic2, 1880, 9, Frame.Count * FrameInterval, this));
                                            //SoundManager.PlaySound(20000 + (ushort)Spell * 9);
                                            break;



                                        case Spell.爆裂火焰: // 爆裂火焰

                                            SoundManager.PlaySound(20000 + (ushort)Spell * 10 + 1);
                                            MapControl.Effects.Add(new Effect(Libraries.Magic, 1660, 10, 1000, TargetPoint));
                                            break;



                                        case Spell.集体隐身术: // 集体隐身术
                                            SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                            missile = CreateProjectile(1160, Libraries.Magic, true, 3, 30, 7);
                                            missile.Explode = true;

                                            missile.Complete += (o, e) =>
                                            {
                                                MapControl.Effects.Add(new Effect(Libraries.Magic, 1540, 10, 800, TargetPoint));
                                                SoundManager.PlaySound(20000 + (ushort)Spell.集体隐身术 * 10 + 1);
                                            };
                                            break;



                                        case Spell.幽灵盾: // 幽灵盾
                                            SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                            missile = CreateProjectile(1160, Libraries.Magic, true, 3, 30, 7);
                                            missile.Explode = true;

                                            missile.Complete += (o, e) =>
                                            {
                                                MapControl.Effects.Add(new Effect(Libraries.Magic, 1320, 15, 1200, TargetPoint));
                                                SoundManager.PlaySound(20000 + (ushort)Spell.幽灵盾 * 10 + 1);
                                            };
                                            break;



                                        case Spell.神圣战甲术: // 神圣战甲术
                                            SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                            missile = CreateProjectile(1160, Libraries.Magic, true, 3, 30, 7);
                                            missile.Explode = true;

                                            missile.Complete += (o, e) =>
                                            {
                                                MapControl.Effects.Add(new Effect(Libraries.Magic, 1340, 15, 1200, TargetPoint));
                                                SoundManager.PlaySound(20000 + (ushort)Spell.神圣战甲术 * 10 + 1);
                                            };
                                            break;



                                        case Spell.火墙: // 火墙
                                            SoundManager.PlaySound(20000 + (ushort)Spell * 10 + 1);
                                            break;




                                        case Spell.治疗圈: // 治愈术（范围）
                                            SoundManager.PlaySound(20000 + (ushort)Spell * 10 + 1);
                                            MapControl.Effects.Add(new Effect(Libraries.Magic3, 620, 10, 1200, TargetPoint));
                                            break;



                                        case Spell.群体治愈术: // 群体治愈术

                                            SoundManager.PlaySound(20000 + (ushort)Spell * 10 + 1);
                                            MapControl.Effects.Add(new Effect(Libraries.Magic, 1800, 10, 1000, TargetPoint));
                                            break;



                                        case Spell.冰咆哮: // 冰咆哮

                                            SoundManager.PlaySound(20000 + (ushort)Spell * 10 + 1);
                                            MapControl.Effects.Add(new Effect(Libraries.Magic, 3850, 20, 1300, TargetPoint));
                                            break;



                                        case Spell.驱魔: // 驱尸术（复活死尸）
                                            SoundManager.PlaySound(20000 + (ushort)Spell * 10 + 1);
                                            if (ob == null)
                                                MapControl.Effects.Add(new Effect(Libraries.Magic, 3930, 15, 1000, TargetPoint));
                                            else
                                                ob.Effects.Add(new Effect(Libraries.Magic, 3930, 15, 1000, ob));
                                            break;


                                        case Spell.冰刺: // 冰刺

                                            Point location = Functions.PointMove(CurrentLocation, Direction, 1);

                                            MapControl.Effects.Add(new Effect(Libraries.Magic2, 1790 + (int)Direction * 10, 10, 10 * FrameInterval, location));
                                            SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                            break;



                                        case Spell.心灵启示: // 心灵启示
                                            SoundManager.PlaySound(20000 + (ushort)Spell * 10 + 1);
                                            if (ob == null)
                                                MapControl.Effects.Add(new Effect(Libraries.Magic, 3990, 10, 1000, TargetPoint));
                                            else
                                                ob.Effects.Add(new Effect(Libraries.Magic, 3990, 10, 1000, ob));
                                            break;


                                        case Spell.灭天火: // 灭天火

                                            SoundManager.PlaySound(20000 + (ushort)Spell * 10);

                                            if (ob == null)
                                                MapControl.Effects.Add(new Effect(Libraries.Magic2, 140, 10, 600, TargetPoint));
                                            else
                                                ob.Effects.Add(new Effect(Libraries.Magic2, 140, 10, 600, ob));
                                            break;


                                        case Spell.cat舌: // 猫舌
                                            SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                            missile = CreateProjectile(260, Libraries.Magic3, true, 6, 30, 4);

                                            if (missile.Target != null)
                                            {
                                                missile.Complete += (o, e) =>
                                                {
                                                    if (missile.Target.CurrentAction == MirAction.Dead) return;
                                                    missile.Target.Effects.Add(new Effect(Libraries.Magic3, 420, 8, 800, missile.Target));
                                                    SoundManager.PlaySound(20000 + (ushort)Spell.cat舌 * 10 + 1);
                                                };
                                            }
                                            break;



                                        case Spell.寒冰掌: // 寒冰掌
                                            SoundManager.PlaySound(20000 + (ushort)Spell * 10 + 1);
                                            missile = CreateProjectile(410, Libraries.Magic2, true, 4, 30, 6);

                                            if (missile.Target != null)
                                            {
                                                missile.Complete += (o, e) =>
                                                {
                                                    if (missile.Target.CurrentAction == MirAction.Dead) return;
                                                    missile.Target.Effects.Add(new Effect(Libraries.Magic2, 570, 8, 600, missile.Target));
                                                    SoundManager.PlaySound(20000 + (ushort)Spell.寒冰掌 * 10 + 2);
                                                };
                                            }
                                            break;



                                        case Spell.净化: // 净化术
                                            if (ob == null)
                                                MapControl.Effects.Add(new Effect(Libraries.Magic2, 620, 10, 800, TargetPoint));
                                            else
                                                ob.Effects.Add(new Effect(Libraries.Magic2, 620, 10, 800, ob));
                                            break;



                                        case Spell.诅咒: // 诅咒术
                                            missile = CreateProjectile(1160, Libraries.Magic, true, 3, 30, 7);
                                            missile.Explode = true;

                                            missile.Complete += (o, e) =>
                                            {
                                                MapControl.Effects.Add(new Effect(Libraries.Magic2, 950, 24, 2000, TargetPoint));
                                                SoundManager.PlaySound(20000 + (ushort)Spell.诅咒 * 10);
                                            };
                                            break;



                                        case Spell.心灵启示_76: // 幻象术
                                            missile = CreateProjectile(1160, Libraries.Magic, true, 3, 48, 7);

                                            if (missile.Target != null)
                                            {
                                                missile.Complete += (o, e) =>
                                                {
                                                    if (missile.Target.CurrentAction == MirAction.Dead) return;
                                                    missile.Target.Effects.Add(new Effect(Libraries.Magic2, 1110, 10, 1000, missile.Target));
                                                    SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                                };
                                            }
                                            break;



                                        case Spell.疾光电影: // 疾光电影
                                            Effects.Add(new Effect(Libraries.Magic, 970 + (int)Direction * 20, 6, Frame.Count * FrameInterval, this));
                                            SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                            break;



                                        case Spell.噬血术: // 噬血术

                                            SoundManager.PlaySound(20000 + (ushort)Spell * 10 + 1);

                                            if (ob == null)
                                                MapControl.Effects.Add(new Effect(Libraries.Magic2, 1060, 20, 1000, TargetPoint));
                                            else
                                            {
                                                ob.Effects.Add(effect = new Effect(Libraries.Magic2, 1060, 20, 1000, ob));
                                                effect.Complete += (o, e) =>
                                                {
                                                    SoundManager.PlaySound(20000 + (ushort)Spell.噬血术 * 10 + 2);
                                                    Effects.Add(new Effect(Libraries.Magic2, 1090, 10, 500, this));
                                                };
                                            }
                                            break;



                                        case Spell.毒云: // 毒云术
                                            missile = CreateProjectile(1160, Libraries.Magic, true, 3, 30, 7);
                                            missile.Explode = true;

                                            missile.Complete += (o, e) =>
                                                {
                                                    SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                                };

                                            break;



                                        case Spell.冰风暴: // 暴风雪
                                            SoundManager.PlaySound(20000 + (ushort)Spell * 10 + 1);
                                            //BlizzardFreezeTime = CMain.Time + 3000;
                                            break;



                                        case Spell.流星火雨: // 流星火雨
                                            SoundManager.PlaySound(20000 + (ushort)Spell * 10 + 1);
                                            SoundManager.PlaySound(20000 + (ushort)Spell * 10 + 2);
                                            //BlizzardFreezeTime = CMain.Time + 3000;
                                            break;



                                        case Spell.重生: // 重生术
                                            ReincarnationStopTime = 0;
                                            break;



                                        case Spell.召唤骨龙: // 召唤骨龙
                                            Effects.Add(new Effect(Libraries.Magic, 1500, 10, Frame.Count * FrameInterval, this));
                                            SoundManager.PlaySound(20000 + (ushort)Spell * 10);
                                            break;



                                        case Spell.终极强化: // 强化术
                                            if (ob != null && ob != User)
                                                ob.Effects.Add(new Effect(Libraries.Magic2, 160, 15, 1000, ob));
                                            break;



                                        case Spell.瘟疫: // 瘟疫术
                                                           //SoundManager.PlaySound(20000 + (ushort)Spell.幽灵盾 * 10);
                                            missile = CreateProjectile(1160, Libraries.Magic, true, 3, 30, 7);
                                            missile.Explode = true;

                                            missile.Complete += (o, e) =>
                                            {
                                                MapControl.Effects.Add(new Effect(Libraries.Magic3, 110, 10, 1200, TargetPoint));
                                                SoundManager.PlaySound(20000 + (ushort)Spell.瘟疫 * 10 + 3);
                                            };
                                            break;



                                        case Spell.困魔咒_13111: // 困魔咒
                                            if (ob != null)
                                                SoundManager.PlaySound(20000 + (ushort)Spell.困魔咒_13111 * 10 + 1);
                                            break;



                                        case Spell.玄冰刃: // 陷阱
                                            if (ob != null)
                                                SoundManager.PlaySound(20000 + (ushort)Spell.玄冰刃 * 10 + 1);
                                            break;



                                        case Spell.新月斩: // 新月斩
                                            SoundManager.PlaySound(20000 + (ushort)Spell * 10 + 2);
                                            break;



                                        case Spell.凝固汽油弹: // 燃烧箭

                                            SoundManager.PlaySound(20000 + (ushort)Spell * 10 + 1);
                                            MapControl.Effects.Add(new Effect(Libraries.Magic3, 1660, 10, 1000, TargetPoint));
                                            break;




                                        case Spell.火弹跳: // 火焰弹
                                            SoundManager.PlaySound(20000 + (ushort)Spell.大火球 * 10 + 1);

                                            missile = CreateProjectile(410, Libraries.Magic, true, 6, 30, 4);

                                            if (missile.Target != null)
                                            {
                                                missile.Complete += (o, e) =>
                                                {
                                                    if (missile.Target.CurrentAction == MirAction.Dead) return;
                                                    missile.Target.Effects.Add(new Effect(Libraries.Magic, 570, 10, 600, missile.Target));
                                                    SoundManager.PlaySound(20000 + (ushort)Spell.大火球 * 10 + 2);
                                                };
                                            }
                                            break;



                                        case Spell.流星雨: // 流星雨

                                            SoundManager.PlaySound(20000 + (ushort)Spell.大火球 * 10 + 1);

                                            var targetIDs = new List<uint> { TargetID };

                                            if (SecondaryTargetIDs != null)
                                            {
                                                targetIDs.AddRange(SecondaryTargetIDs);
                                            }

                                            foreach (var targetID in targetIDs)
                                            {
                                                missile = CreateProjectile(410, Libraries.Magic, true, 6, 30, 4, targetID: targetID);

                                                if (missile.Target != null)
                                                {
                                                    missile.Complete += (o, e) =>
                                                    {
                                                        var sender = (Missile)o;

                                                        if (sender.Target.CurrentAction == MirAction.Dead) return;
                                                        sender.Target.Effects.Add(new Effect(Libraries.Magic, 570, 10, 600, sender.Target));
                                                        SoundManager.PlaySound(20000 + (ushort)Spell.大火球 * 10 + 2);
                                                    };
                                                }
                                            }

                                            break;


                                    }
                                }


                                Cast = false;
                            }
                            //if (ActionFeed.Count == 0)
                            //    ActionFeed.Add(new QueuedAction { Action = MirAction.Stance, Direction = Direction, Location = CurrentLocation });

                            StanceTime = CMain.Time + StanceDelay;
                            FrameIndex = Frame.Count - 1;
                            SetAction();

                        }
                        else
                        {
                            NextMotion += FrameInterval;

                        }
                    }

                    UpdateWingEffect();
                    break;
                case MirAction.Die:
                    if (CMain.Time >= NextMotion)
                    {
                        GameScene.Scene.MapControl.TextureValid = false;

                        if (SkipFrames) UpdateFrame();

                        if (UpdateFrame() >= Frame.Count)
                        {
                            FrameIndex = Frame.Count - 1;
                            ActionFeed.Clear();
                            ActionFeed.Add(new QueuedAction { Action = MirAction.Dead, Direction = Direction, Location = CurrentLocation });
                            SetAction();
                        }
                        else
                        {
                            if (FrameIndex == 1)
                                PlayDieSound();

                            NextMotion += FrameInterval;
                        }
                    }

                    UpdateWingEffect();
                    break;
                case MirAction.Dead:
                    break;
                case MirAction.Revive:
                    if (CMain.Time >= NextMotion)
                    {
                        GameScene.Scene.MapControl.TextureValid = false;

                        if (SkipFrames) UpdateFrame();

                        if (UpdateFrame() >= Frame.Count)
                        {
                            FrameIndex = Frame.Count - 1;
                            ActionFeed.Clear();
                            ActionFeed.Add(new QueuedAction { Action = MirAction.Standing, Direction = Direction, Location = CurrentLocation });
                            SetAction();
                        }
                        else
                        {
                            NextMotion += FrameInterval;
                        }
                    }
                    break;

            }

            if (this == User) return;

            if ((CurrentAction == MirAction.Standing || CurrentAction == MirAction.MountStanding || CurrentAction == MirAction.Stance || CurrentAction == MirAction.Stance2 || CurrentAction == MirAction.DashFail) && NextAction != null)
                SetAction();
            //if Revive and dead set action

        }

        public int UpdateFrame(bool skip = true)
        {
            if (Frame == null) return 0;
            if (Poison.HasFlag(PoisonType.Slow) && !skip)
            {
                SkipFrameUpdate++;
                if (SkipFrameUpdate == 2)
                    SkipFrameUpdate = 0;
                else
                    return FrameIndex;
            }
            if (Frame.Reverse) return Math.Abs(--FrameIndex);

            return ++FrameIndex;
        }


        public int UpdateFrame2()
        {
            if (Frame == null) return 0;

            if (Frame.Reverse) return Math.Abs(--EffectFrameIndex);

            return ++EffectFrameIndex;
        }


    }
}