using Client.MirGraphics;
using Client.MirScenes;
using KFramework.MonoGame;
using WebGame.Mir2.MonoGame.Client;   // CMain

namespace Client.MirObjects
{
    /// <summary>
    /// 原始传奇数据驱动特效：服务端通过 S.NewMagic 下发 ClientMagic.EffectIds，
    /// 经 ExcelTableMgr.TryGetEffect（5config.effect）查到 effect 对应的 CDN 模型帧，用 Mir2OriginObjectLibrary 渲染。
    /// 继承 Effect 以复用 MapControl 的 Process/Draw 生命周期，但不依赖已废弃的 .Lib 图集。
    /// </summary>
    public sealed class OriginEffect : Effect
    {
        private readonly int _model;
        private readonly int _action;
        private readonly int _direction;
        private readonly int _intval;
        private readonly bool _addBlend;
        private readonly int _xOffset;     // skillEffects.x：相对目标/施法者的屏幕 X 像素偏移
        private readonly int _yOffset;     // 向上为正；skillEffects.y(向下为正)取反，或子弹终点补偿
        private readonly float _scale;     // 原始传奇 action show/100 缩放；<=0 表示不缩放

        public OriginEffect(int effectId, MapObject owner, long starttime = 0,
                            int xOffset = 0, int yOffset = 0, float scale = 1f)
            : base(null, 0, 1, 0, owner, starttime)
        {
            _xOffset = xOffset;
            _yOffset = yOffset;
            if (!ExcelTableMgr.TryGetEffect(effectId, out var effect))
            {
                _model = 0;
                return;
            }

            _model = effect.model;
            _action = effect.action > 0 ? effect.action : 1;
            _direction = effect.dir;
            _intval = effect.intval > 0 ? effect.intval : 50;
            _addBlend = effect.blendmode == "add";
            _scale = scale > 0 ? scale : 1f;

            Mir2OriginObjectLibrary.Request(Mir2ObjectKind.Monster, _model, _action, _direction,
                                            Mir2OriginObjectLibrary.PriorityVisible);
        }

        public override void Process()
        {
            if (_model <= 0) { Remove(); return; }
            if (CMain.Time < NextFrame) return;

            if (!Mir2OriginObjectLibrary.TryGetFrameCount(Mir2ObjectKind.Monster, _model, _action, _direction, out int count) || count <= 0)
            {
                NextFrame = CMain.Time + 100;   // 资源尚未就绪，稍后重试
                return;
            }

            CurrentFrame++;
            if (CurrentFrame >= count) { Remove(); return; }

            NextFrame = CMain.Time + _intval;
            GameScene.Scene.MapControl.TextureValid = false;
        }

        public override void Draw()
        {
            if (_model <= 0 || CMain.Time < Start) return;

            if (Owner != null)
            {
                DrawLocation = Owner.DrawLocation;
            }
            else
            {
                DrawLocation = new Point((Source.X - MapObject.User.Movement.X + MapControl.OffSetX) * MapControl.CellWidth,
                                         (Source.Y - MapObject.User.Movement.Y + MapControl.OffSetY) * MapControl.CellHeight);
                DrawLocation.Offset(DrawOffset);
                DrawLocation.Offset(MapObject.User.OffSetMove);
            }

            // 特效精灵是"虚拟大画布中的一帧"：必须用 model 的 standx/standy 枢轴 + 帧的 OffX/OffY
            // 来定位，这正是 DrawAnchored 做的事。之前用裁剪帧中心锚定是错的——丢了画布偏移和枢轴，
            // 导致雷电术起手/命中位置整体错乱，靠 yOffset 怎么补都不对。
            // 把 model 枢轴点对齐到 (目标脚底 + skillEffects 的 x/y 偏移 + 子弹终点补偿)，
            // 帧内容自然落在原始传奇 JS 中对应的位置（frame.y + h 恒定 = 脚底）。
            int screenX = DrawLocation.X + _xOffset;
            int screenY = DrawLocation.Y - _yOffset;
            Mir2OriginObjectLibrary.DrawAnchored(
                Mir2ObjectKind.Monster, _model, _action, _direction, CurrentFrame,
                screenX, screenY, CommonColor.White, scale: _scale,
                blend: _addBlend ? MirBlend.Additive : MirBlend.Normal);
        }
    }

    /// <summary>
    /// 原始传奇数据驱动"飞行道具"特效：与 OriginEffect 同样读 5config.effect（model/action/dir/intval/blend），
    /// 但沿 Source→Destination 插值飞行（替代已移除的经典 .Lib Missile），抵达时触发 Complete 供命中特效挂载。
    /// 灵魂火符(Lv1 effect 653=model13003符纸 / Lv5 752=model13024) 用它画飞行符。
    /// </summary>
    public sealed class OriginMissile : Effect
    {
        /// <summary>
        /// 原始传奇 GameDefine.SKILL_HitTestY=60：飞行子弹的起点/终点都在实体像素 Y 基础上下移 60px
        /// （JS: addSkillBullet(t.x, t.y - SKILL_HitTestY, ...)，把发射点从脚底抬到手/胸高度）。
        /// 客户端格子 CellHeight=32，与原始传奇 MAP_GRID_HEIGHT=32 一致，故该值可直接作为屏幕像素偏移。
        /// </summary>
        public const int SkillHitTestY = 60;

        private readonly int _model;
        private readonly int _action;
        private readonly int _direction;
        private readonly int _intval;
        private readonly bool _addBlend;
        private readonly Point _source;
        private readonly int _flightDuration;   // 飞行总时长(ms)：0=按距离估算；>0=用配置 delaytime
        private readonly int _startYOffset;     // 起点/终点屏幕 Y 偏移（抬到手部）
        private int _totalFrames;
        private long _readyAt;   // CDN 帧首次就绪时刻；飞行计时以它为准，避免资源异步加载期间就被判定飞完

        public MapObject Target;
        public Point Destination;

        /// <param name="flightDuration">飞行时长(ms)。原始传奇 5config.skillEffects 里灵魂火符 type=2 子弹 delaytime=400。</param>
        /// <param name="startYOffset">起点屏幕像素 Y 偏移（抬到手部）。原始传奇 SKILL_HitTestY=60。</param>
        public OriginMissile(int effectId, MapObject owner, MapObject target, Point destination,
                             long duration = 500, int flightDuration = 0, int startYOffset = SkillHitTestY)
            : base(null, 0, 1, (int)duration, owner)
        {
            Target = target;
            Destination = destination;
            _source = owner.CurrentLocation;
            _flightDuration = flightDuration;
            _startYOffset = startYOffset;

            if (!ExcelTableMgr.TryGetEffect(effectId, out var effect))
            {
                _model = 0;
                return;
            }

            _model = effect.model;
            _action = effect.action > 0 ? effect.action : 1;
            _direction = effect.dir;
            _intval = effect.intval > 0 ? effect.intval : 50;
            _addBlend = effect.blendmode == "add";

            if (!Mir2OriginObjectLibrary.TryGetFrameCount(Mir2ObjectKind.Monster, _model, _action, _direction, out _totalFrames) || _totalFrames <= 0)
                _totalFrames = 1;

            Mir2OriginObjectLibrary.Request(Mir2ObjectKind.Monster, _model, _action, _direction,
                                            Mir2OriginObjectLibrary.PriorityVisible);

            // 基类把 NextFrame 初始化为 Start + Duration(默认500)，而飞行时长通常只有几十~几百毫秒，
            // 导致第一次 Process 就被判定飞完并 Remove，飞行帧从未绘制。这里让它立刻进入推进。
            NextFrame = Start;
        }

        public override void Process()
        {
            if (_model <= 0) { Remove(); return; }

            // 资源异步加载：没帧时只延后重试，不推进也不移除，否则首帧资源未到就被销毁。
            if (!Mir2OriginObjectLibrary.TryGetFrameCount(Mir2ObjectKind.Monster, _model, _action, _direction, out _totalFrames) || _totalFrames <= 0)
            {
                NextFrame = CMain.Time + 100;
                return;
            }
            if (_readyAt == 0) _readyAt = CMain.Time;

            if (CMain.Time < NextFrame) return;

            if (Target != null) Destination = Target.CurrentLocation;

            CurrentFrame++;
            NextFrame = CMain.Time + _intval;
            GameScene.Scene.MapControl.TextureValid = false;

            if (CMain.Time >= _readyAt + GetTravel())
                Remove();
        }

        /// <summary>飞行总时长：优先用配置 delaytime（原始传奇 skillEffects.type=2），否则按距离估算。</summary>
        private int GetTravel()
        {
            if (_flightDuration > 0) return _flightDuration;
            return Math.Max(1, Functions.MaxDistance(_source, Destination) * 50);
        }

        public override void Draw()
        {
            if (_model <= 0 || _readyAt == 0) return;

            long elapsed = CMain.Time - _readyAt;
            float t = Math.Min(1f, elapsed / (float)GetTravel());

            int cellX = _source.X + (int)((Destination.X - _source.X) * t);
            int cellY = _source.Y + (int)((Destination.Y - _source.Y) * t);

            int screenX = (cellX - MapObject.User.Movement.X + MapControl.OffSetX) * MapControl.CellWidth;
            int screenY = (cellY - MapObject.User.Movement.Y + MapControl.OffSetY) * MapControl.CellHeight;
            screenX += MapObject.User.OffSetMove.X;
            screenY += MapObject.User.OffSetMove.Y;
            // 从脚底抬到手/胸高度发射（原始传奇 SKILL_HitTestY=60）：起点与终点同步上移，
            // 保证整条飞行轨迹都落在身体上半部，而不是贴着地面从身上穿过。
            screenY -= _startYOffset;

            // 原始传奇 SkillBullet：用 effect.dir(4) 的精灵帧，靠 t.rotation=orientation-90 旋转朝向目标，
            // 位置由 tween 平滑驱动。这里完全照搬：固定用 dir=_direction 的帧，按 source→目标算旋转角。
            // 注意原始子弹动画是多帧（同一火球自转），这里按飞行进度推进帧，不是固定帧 0。
            int frame = CurrentFrame % _totalFrames;

            // 原始: rotation = DirectionUtil.orientation(x1,y1,x2,y2) - 90 (度)，egret rotation 是度，
            // MonoGame 是弧度：atan2(dy,dx) - π/2 即为等价弧度。
            float rotation = 0f;
            if (Target != null)
            {
                float dx = Destination.X - _source.X;
                float dy = Destination.Y - _source.Y;
                rotation = (float)Math.Atan2(dy, dx) - (float)(Math.PI / 2);
            }

            // 与命中特效(OriginEffect)统一用 DrawAnchored：model 枢轴 + 帧 OffX/OffY 锚定，
            // 保证飞行终点与命中特效落点完全一致，不再有"子弹停下后命中特效突然位移"的错位。
            Mir2OriginObjectLibrary.DrawAnchored(
                Mir2ObjectKind.Monster, _model, _action, _direction, frame,
                screenX, screenY, CommonColor.White, scale: 1f,
                blend: _addBlend ? MirBlend.Additive : MirBlend.Normal, rotation: rotation);
        }
    }
}
