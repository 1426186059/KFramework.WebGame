using Client.MirGraphics;
using Client.MirScenes;
using Client.MirSounds;
using WebGame.Mir2.MonoGame.Client;
using S = ServerPackets;

namespace Client.MirObjects
{
    class SpellObject : MapObject
    {
        public override ObjectType Race
        {
            get { return ObjectType.Spell; }
        }

        public override bool Blocking
        {
            get { return false; }
        }

        public Spell Spell;
        public Point AnimationOffset = new(0, 0);
        public int FrameCount, FrameInterval, FrameIndex;
        public bool Repeat, Ended;

        // 安全区边沿光柱：原始传奇 initSafeZone 对 safeArea 每格 addEffect(261)=model 11081。
        // 服务端 SafeZoneBorder 已把这些边框格下发为 Spell.困魔咒_13111 装饰，但经典 .Lib(1390) 已废弃；
        // 这里改走 CDN model 11081 的精灵帧，让现成的安全区边框逻辑重新可见。
        private int _originModel;          // >0 表示用 CDN 精灵绘制
        private int _originAction = 1;
        private int _originDir = 4;
        private int _originStandX = -1, _originStandY = -1;  // <0 表示用模型自身枢轴/画布中心
        private const int OriginIntval = 86;   // 5config.effect[261].intval
        private const int OriginDir = 4;       // effect[261].dir（totaldir=1）
        

        public SpellObject(uint objectID) : base(objectID)
        {
        }

        /// <summary>
        /// 地面持续技能原始数据驱动：取 skillEffects 的 type=0 effect→model 走 CDN 循环渲染。
        /// 返回 false 表示无原始数据，调用方回退旧 .Lib switch。
        /// </summary>
        private bool TryLoadOriginGroundEffect()
        {
            if (!ExcelTableMgr.TryGetSkillEffects((int)Spell, 1, out var steps) || steps == null) return false;

            var step = steps.FirstOrDefault(s => s.type == 0 && s.effect > 0);
            if (step == null || !ExcelTableMgr.TryGetEffect(step.effect, out var effect) || effect.model <= 0)
                return false;

            _originModel = effect.model;
            _originAction = effect.action > 0 ? effect.action : 1;
            _originDir = effect.dir;
            FrameInterval = effect.intval > 0 ? effect.intval : 100;
            FrameCount = 1;  // Process 里按实际帧数刷新
            Blend = effect.blendmode == "add";
            Repeat = true;
            BodyLibrary = null;
            if (step.sound > 0) SoundManager.PlayOriginSound(step.sound);

            Mir2OriginObjectLibrary.Request(Mir2ObjectKind.Monster, _originModel, _originAction, _originDir,
                                            EResLoadPriority.Visible);
            return true;
        }

        public void Load(S.ObjectSpell info)
        {
            CurrentLocation = info.Location;
            MapLocation = info.Location;
            GameScene.Scene.MapControl.AddObject(this);
            Spell = info.Spell;
            Direction = info.Direction;
            Repeat = true;
            Ended = false;

            // 原始传奇数据驱动：地面持续技能（火墙/毒云/暴风雪/流星火雨等）用 skillEffects type=0 的
            // effect→model 走 CDN 精灵循环渲染，替代已移除的 Libraries.Magic* 旧帧。
            // TrapHexagon 是安全区光柱(effect261)，已有专用的 model 11081 + 枢轴，跳过通用逻辑。
            if (Spell != Spell.困魔咒_13111 && TryLoadOriginGroundEffect())
            {
                return;
            }

            switch (Spell)
            {
                case Spell.困魔咒_13111: // 困魔咒
                    // 安全区边沿光柱：用原始传奇 CDN model 11081（effect 261），不再用废弃的 Libraries.Magic 1390。
                    _originModel = 11081;
                    FrameInterval = OriginIntval;
                    FrameCount = 8;
                    Blend = true;
                    Mir2OriginObjectLibrary.Request(Mir2ObjectKind.Monster, _originModel, 1, OriginDir,
                                                    EResLoadPriority.Visible);
                    break;
                case Spell.火墙: // 火墙
                    BodyLibrary = Libraries.Magic;
                    DrawFrame = 1630;
                    FrameInterval = 120;
                    FrameCount = 6;
                    Light = 3;
                    Blend = true;
                    break;
                case Spell.毒云: // 毒云术
                    BodyLibrary = Libraries.Magic2;
                    DrawFrame = 1650;
                    FrameInterval = 120;
                    FrameCount = 20;
                    Light = 3;
                    Blend = true;
                    break;
                case Spell.挖地僵尸: // 地图事件·掘墓僵尸
                    BodyLibrary = (ushort)Monster.DigOutZombie < Libraries.Monsters.Count() ? Libraries.Monsters[(ushort)Monster.DigOutZombie] : Libraries.Magic;
                    DrawFrame = 304 + (byte) Direction;
                    FrameCount = 0;
                    Blend = false;
                    break;
                case Spell.冰风暴: // 暴风雪
                    AnimationOffset = new Point(0, -20);
                    BodyLibrary = Libraries.Magic2;
                    DrawFrame = 1550;
                    FrameInterval = 100;
                    FrameCount = 30;
                    Light = 3;
                    Blend = true;
                    Repeat = false;
                    break;
                case Spell.流星火雨: // 流星火雨
                    AnimationOffset = new Point(0, -20);
                    MapControl.Effects.Add(new Effect(Libraries.Magic2, 1600, 10, 800, CurrentLocation) { Repeat = true, RepeatUntil = CMain.Time + 3000 });
                    BodyLibrary = Libraries.Magic2;
                    DrawFrame = 1610;
                    FrameInterval = 100;
                    FrameCount = 30;
                    Light = 3;
                    Blend = true;
                    Repeat = false;
                    break;
                case Spell.碎石: // 地图事件·落石
                    if (Direction == 0)
                        BodyLibrary = null;
                    else
                    {
                        BodyLibrary = Libraries.Effect;
                        DrawFrame = 64 + Math.Min(4, (int)(Direction - 1));
                        FrameCount = 1;
                        FrameInterval = 10000;
                    }
                    break;
                case Spell.重生: // 重生术
                    BodyLibrary = Libraries.Magic2;
                    DrawFrame = 1680;
                    FrameInterval = 100;
                    FrameCount = 10;
                    Light = 1;
                    Blend = true;
                    Repeat = true;
                    break;
                case Spell.爆炸陷阱: // 爆炸陷阱
                    BodyLibrary = Libraries.Magic3;
                    if (info.Param)
                    {
                        DrawFrame = 1570;
                        FrameInterval = 100;
                        FrameCount = 9;
                        Repeat = false;
                        SoundManager.PlaySound(20000 + 124 * 10 + 5);//Boom for all players in range
                    }
                    else
                    {
                        DrawFrame = 1560;
                        FrameInterval = 100;
                        FrameCount = 10;
                        Repeat = true;
                    }
                    //Light = 1;
                    Blend = true;
                    break;
                case Spell.玄冰刃: // 陷阱
                    BodyLibrary = Libraries.Magic2;
                    DrawFrame = 2360;
                    FrameInterval = 100;
                    FrameCount = 8;
                    Blend = true;
                    break;
                case Spell.地图闪电: // 地图事件·闪电
                    MapControl.Effects.Add(new Effect(Libraries.Dragon, 400 + (CMain.Random.Next(3) * 10), 5, 600, CurrentLocation));
                    SoundManager.PlaySound(8301);
                    break;
                case Spell.地图岩浆: // 地图事件·熔岩
                    MapControl.Effects.Add(new Effect(Libraries.Dragon, 440, 20, 1600, CurrentLocation) { Blend = false });
                    MapControl.Effects.Add(new Effect(Libraries.Dragon, 470, 10, 800, CurrentLocation));
                    SoundManager.PlaySound(8302);
                    break;
                case Spell.地图地震1: // 地图事件·地震一
                    MapControl.Effects.Add(new Effect(Libraries.Monsters[(ushort)Monster.HellLord], 27, 12, 1200, CurrentLocation) { Blend = false });
                    SoundManager.PlaySound(8304);
                    break;
                case Spell.地图地震2: // 地图事件·地震二
                    MapControl.Effects.Add(new Effect(Libraries.Monsters[(ushort)Monster.HellLord], 39, 13, 1300, CurrentLocation) { Blend = false });
                    SoundManager.PlaySound(8304);
                    break;
                case Spell.挖地穿山甲: // 地图事件·掘地犰狳
                    BodyLibrary = Libraries.Monsters[(ushort)Monster.Armadillo];
                    DrawFrame = 472 + (byte)Direction;
                    FrameCount = 0;
                    Blend = false;
                    break;
                case Spell.将军喵喵雷: // 地图事件·喵喵将军雷击
                    MapControl.Effects.Add(new Effect(Libraries.Monsters[(ushort)Monster.GeneralMeowMeow], 522, 7, 700, CurrentLocation) { Blend = true });
                    SoundManager.PlaySound(8321);
                    break;
                case Spell.石巨人地震: // 地图事件·石巨人地震
                    BodyLibrary = Libraries.Monsters[(ushort)Monster.StoneGolem];
                    DrawFrame = 368 + (int)Direction * 8;
                    FrameInterval = 100;
                    FrameCount = 8;
                    Light = 0;
                    Blend = false;
                    Repeat = false;
                    SoundManager.PlaySound(8304);
                    break;
                case Spell.土巨人堆: // 地图事件·土巨石堆
                    BodyLibrary = Libraries.Monsters[(ushort)Monster.EarthGolem];
                    DrawFrame = 441;
                    FrameInterval = 100;
                    FrameCount = 8;
                    Light = 0;
                    Blend = false;
                    Repeat = false;
                    SoundManager.PlaySound(8331);
                    break;
                case Spell.树后群根: // 地图事件·树王群根
                    BodyLibrary = Libraries.Monsters[(ushort)Monster.TreeQueen];
                    DrawFrame = 82;
                    FrameInterval = 100;
                    FrameCount = 15;
                    Blend = false;
                    Repeat = false;
                    SoundManager.PlaySound(8341);
                    MapControl.Effects.Add(new Effect(Libraries.Monsters[(ushort)Monster.TreeQueen], 97, 14, 1400, CurrentLocation) { Blend = true });
                    break;
                case Spell.树后地根: // 地图事件·树王地根
                    BodyLibrary = Libraries.Monsters[(ushort)Monster.TreeQueen];
                    DrawFrame = 48;
                    FrameInterval = 100;
                    FrameCount = 9;
                    Blend = false;
                    Repeat = false;
                    SoundManager.PlaySound(8342);
                    MapControl.Effects.Add(new Effect(Libraries.Monsters[(ushort)Monster.TreeQueen], 57, 9, 900, CurrentLocation) { Blend = true });
                    break;
                case Spell.树后根: // 地图事件·树王根须
                    BodyLibrary = Libraries.Monsters[(ushort)Monster.TreeQueen];
                    DrawFrame = 111;
                    FrameInterval = 100;
                    FrameCount = 15;
                    Blend = false;
                    Repeat = false;
                    SoundManager.PlaySound(8343);
                    break;
                case Spell.图森将军石: // 地图事件·石像将军落石
                    MapControl.Effects.Add(new Effect(Libraries.Monsters[(ushort)Monster.TucsonGeneral], 552, 20, 2000, CurrentLocation) { Repeat = false, Blend = false });
                    BodyLibrary = Libraries.Monsters[(ushort)Monster.TucsonGeneral];
                    DrawFrame = 572;
                    FrameInterval = 100;
                    FrameCount = 20;
                    Light = 1;
                    Blend = true;
                    Repeat = false;
                    break;
                case Spell.传送门: // 传送门
                    BodyLibrary = Libraries.Magic2;
                    DrawFrame = 2360;
                    FrameInterval = 100;
                    FrameCount = 8;
                    Blend = true;
                    break;
                case Spell.治疗圈: // 治愈术（范围）
                    BodyLibrary = Libraries.Magic3;
                    DrawFrame = 630;
                    FrameInterval = 80;
                    FrameCount = 11;
                    Light = 3;
                    Blend = true;
                    break;
                case Spell.飞雕像冰旋风: // 地图事件·飞像冰龙卷
                    BodyLibrary = Libraries.Monsters[(ushort)Monster.FlyingStatue];
                    DrawFrame = 314;
                    FrameInterval = 100;
                    FrameCount = 20;
                    Blend = true;
                    Repeat = false;
                    SoundManager.PlaySound(8303);
                    break;
                case Spell.暗黑沃玛王怒: // 地图事件·暗黑奥玛王核爆
                    BodyLibrary = Libraries.Monsters[(ushort)Monster.DarkOmaKing];
                    DrawFrame = 1630 + (int)Direction * 9;
                    FrameInterval = 100;
                    FrameCount = 9;
                    Blend = true;
                    Repeat = false;
                    SoundManager.PlaySound(((ushort)Monster.DarkOmaKing * 10) + 9);
                    break;
                case Spell.角术士尘旋风: // 地图事件·角巫师沙暴
                    BodyLibrary = Libraries.Monsters[(ushort)Monster.HornedSorceror];
                    DrawFrame = 634;
                    FrameInterval = 100;
                    FrameCount = 10;
                    Blend = true;
                    Repeat = true;
                    SoundManager.PlaySound(8306);
                    break;
                case Spell.角指挥官落石: // 地图事件·角统帅落石
                    BodyLibrary = Libraries.Monsters[(ushort)Monster.HornedCommander];
                    DrawFrame = 1066;
                    FrameInterval = 100;
                    FrameCount = 12;
                    Blend = true;
                    Repeat = true;
                    SoundManager.PlaySound(8456);
                    break;
                case Spell.角指挥官石刺: // 地图事件·角统帅石刺
                    BodyLibrary = Libraries.Monsters[(ushort)Monster.HornedCommander];
                    DrawFrame = 1190;
                    FrameInterval = 100;
                    FrameCount = 9;
                    Blend = false;
                    Repeat = true;
                    SoundManager.PlaySound(8457);
                    MapControl.Effects.Add(new Effect(Libraries.Monsters[(ushort)Monster.HornedCommander], 1199, 9, 900, CurrentLocation) { Blend = true });
                    break;
            }

            NextMotion = CMain.Time + FrameInterval;
            NextMotion -= NextMotion % 100;
        }

        public override void Process()
        {
            // CDN 精灵的安全区光柱：资源就绪后按实际帧数循环（model 11081 单动作多帧）
            if (_originModel > 0 &&
                Mir2OriginObjectLibrary.TryGetFrameCount(Mir2ObjectKind.Monster, _originModel, _originAction, _originDir, out int originFrames) &&
                originFrames > 0)
                FrameCount = originFrames;

            if (CMain.Time >= NextMotion)
            {
                if (++FrameIndex >= FrameCount && Repeat)
                {
                    FrameIndex = 0;
                    Ended = true;
                }

                NextMotion = CMain.Time + FrameInterval;

                switch (Spell)
                {
                    case Spell.图森将军石: // 地图事件·石像将军落石
                        if (FrameIndex == 10) SoundManager.PlaySound(8305);
                        break;
                    case Spell.角术士尘旋风: // 地图事件·角巫师沙暴
                        if (FrameIndex == 0 && CMain.Random.Next(3) == 0) SoundManager.PlaySound(8306);
                        break;
                    case Spell.角指挥官石刺: // 地图事件·角统帅石刺
                        if (Ended)
                        {
                            DrawFrame = 1198;
                            FrameCount = 1;
                            FrameIndex = 0;
                        }
                        break;
                }
            }

            DrawLocation = new Point((CurrentLocation.X - User.Movement.X + MapControl.OffSetX) * MapControl.CellWidth, (CurrentLocation.Y - User.Movement.Y + MapControl.OffSetY) * MapControl.CellHeight);
            DrawLocation.Offset(GlobalDisplayLocationOffset);
            DrawLocation.Offset(User.OffSetMove);
        }

        public override void Draw()
        {
            if (FrameIndex >= FrameCount && !Repeat) return;

            // 安全区边沿光柱：走 CDN model 11081 精灵帧（替代废弃的 Libraries.Magic）。
            // 用 DrawAnchored 按 modelInfo 枢轴(standx=30/standy=165)把脚底钉在格子点上——
            // 它每帧按各自宽高算偏移但基准点固定，不会像手算矩形那样因帧尺寸变化而脚底乱跳。
            if (_originModel > 0)
            {
                int? sx = _originStandX >= 0 ? _originStandX : null;
                int? sy = _originStandY >= 0 ? _originStandY : null;
                Mir2OriginObjectLibrary.DrawAnchored(Mir2ObjectKind.Monster, _originModel, _originAction, _originDir,
                                                     FrameIndex, DrawLocation.X, DrawLocation.Y, CommonColor.White,
                                                     standX: sx, standY: sy);
                return;
            }

            if (BodyLibrary == null) return;

            if (Blend)
            {
                BodyLibrary.DrawBlend(
                    DrawFrame + FrameIndex,
                    AnimationOffset == default ? DrawLocation : GetDrawWithOffset(),
                    DrawColour, true,
                    0.8F);
            }
            else
            {
                BodyLibrary.Draw(DrawFrame + FrameIndex,
                    AnimationOffset == default ? DrawLocation : GetDrawWithOffset(),
                    DrawColour,
                    true);
            }
        }

        public override bool MouseOver(Point p)
        {
            return false;
        }

        public override void DrawBehindEffects(bool effectsEnabled)
        {
        }

        public override void DrawEffects(bool effectsEnabled)
        { 
        }

        private Point GetDrawWithOffset()
        {
            Point newDrawLocation = new (
                (CurrentLocation.X + AnimationOffset.X - User.Movement.X + MapControl.OffSetX) * MapControl.CellWidth,
                (CurrentLocation.Y + AnimationOffset.Y - User.Movement.Y + MapControl.OffSetY) * MapControl.CellHeight);

            newDrawLocation.Offset(GlobalDisplayLocationOffset);
            newDrawLocation.Offset(User.OffSetMove);

            return newDrawLocation;
        }
    }
}
