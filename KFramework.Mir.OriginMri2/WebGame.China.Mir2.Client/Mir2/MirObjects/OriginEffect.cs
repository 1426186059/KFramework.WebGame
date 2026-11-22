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

        public OriginEffect(int effectId, MapObject owner, long starttime = 0)
            : base(null, 0, 1, 0, owner, starttime)
        {
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

            if (!Mir2OriginObjectLibrary.TryGetSprite(Mir2ObjectKind.Monster, _model, _action, _direction, CurrentFrame, out var sprite) || sprite == null)
                return;

            Rectangle src = sprite.SourceRectangle;
            int x = DrawLocation.X - src.Width / 2;
            int y = DrawLocation.Y - src.Height / 2;
            Rectangle dst = new Rectangle(x, y, src.Width, src.Height);

            DXManager.Draw(sprite.Texture, src, dst, CommonColor.White,
                           (_addBlend ? MirBlend.Additive : MirBlend.Normal).ToBlendState(), SpriteEffects.None);
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

            int frame = CurrentFrame % _totalFrames;
            if (!Mir2OriginObjectLibrary.TryGetSprite(Mir2ObjectKind.Monster, _model, _action, _direction, frame, out var sprite) || sprite == null)
                return;

            Rectangle src = sprite.SourceRectangle;
            int x = screenX - src.Width / 2;
            int y = screenY - src.Height / 2;
            Rectangle dst = new Rectangle(x, y, src.Width, src.Height);

            DXManager.Draw(sprite.Texture, src, dst, CommonColor.White,
                           (_addBlend ? MirBlend.Additive : MirBlend.Normal).ToBlendState(), SpriteEffects.None);
        }
    }
}
