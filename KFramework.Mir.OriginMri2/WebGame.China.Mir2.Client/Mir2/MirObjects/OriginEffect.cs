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
        /// <summary>调试开关：在特效锚点画一个青色十字，用于核对子弹终点/命中特效落点是否重合。</summary>
        public static bool DebugDrawAnchor = false;

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
                                            EResLoadPriority.Visible);
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

            if (DebugDrawAnchor) DrawAnchorMark(screenX, screenY, new KFramework.MonoGame.Color(255, 60, 60));
        }

        /// <summary>调试：在锚点画一个 11x11 十字（红=命中特效锚点，青=子弹终点锚点）。</summary>
        internal static void DrawAnchorMark(int x, int y, KFramework.MonoGame.Color color)
        {
            var src = new KFramework.MonoGame.Rectangle(0, 0, 1, 1);
            DXManager.Draw(DXManager.WhitePixel, src,
                new RectangleF(x - 5, y, 11, 1), color);
            DXManager.Draw(DXManager.WhitePixel, src,
                new RectangleF(x, y - 5, 1, 11), color);
        }
    }

    /// <summary>
    /// 原始传奇数据驱动"飞行道具"特效：与 OriginEffect 同样读 5config.effect（model/action/dir/intval/blend），
    /// 但沿 Source→Destination 插值飞行（替代已移除的经典 .Lib Missile），抵达时触发 Complete 供命中特效挂载。
    /// 灵魂火符(Lv1 effect 653=model13003符纸 / Lv5 752=model13024) 用它画飞行符。
    /// </summary>
    public sealed class OriginMissile : Effect
    {
        private readonly int _model;
        private readonly int _action;
        private readonly int _direction;
        private readonly int _intval;
        private readonly bool _addBlend;
        private readonly Point _source;         // 构造时施法者格子坐标（仅估算飞行时长用）
        private readonly Point _screenStart;    // 构造时施法者屏幕坐标：弹道起点固定，避免施法者移动导致起点漂移
        private readonly int _flightDuration;   // 飞行总时长(ms)：0=按距离估算；>0=用配置 delaytime
        private readonly int _startYOffset;     // 起点/终点屏幕 Y 偏移（抬到手部）
        private int _totalFrames;
        private long _readyAt;   // CDN 帧首次就绪时刻；飞行计时以它为准，避免资源异步加载期间就被判定飞完

        public MapObject Target;
        public Point Destination;

        /// <param name="flightDuration">飞行时长(ms)。原始传奇 5config.skillEffects 里灵魂火符 type=2 子弹 delaytime=400。</param>
        /// <param name="startYOffset">起点/终点屏幕像素 Y 偏移（抬到手部）。默认取原始传奇 GameDefine.SKILL_HitTestY。</param>
        public OriginMissile(int effectId, MapObject owner, MapObject target, Point destination,
                             long duration = 500, int flightDuration = 0, int startYOffset = GameDefine.SKILL_HitTestY)
            : base(null, 0, 1, (int)duration, owner)
        {
            Target = target;
            Destination = destination;
            _source = owner.CurrentLocation;
            _screenStart = owner != null ? owner.DrawLocation : _source;
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
                                            EResLoadPriority.Visible);

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

            // 直接用施法者/目标的 DrawLocation（屏幕坐标，含 Movement 平滑插值、OffSetMove、ManualLocationOffset），
            // 终点与命中特效(OriginEffect 用 Owner.DrawLocation)完全一致——之前用格子坐标 CurrentLocation 自己算，
            // 与目标的 DrawLocation 存在偏差，子弹停下后命中特效跳到另一位置，看起来像突然位移。
            Point startScreen = _screenStart;
            Point endScreen = Target != null ? Target.DrawLocation : Destination;
            int screenX = startScreen.X + (int)((endScreen.X - startScreen.X) * t);
            int screenY = startScreen.Y + (int)((endScreen.Y - startScreen.Y) * t);
            // 原始传奇 JS SkillBullet：起点(t.y-60)与终点(target.y-60)都抬 SKILL_HitTestY，
            // tween 在这两点间移动、rotation 用这两点算。起点终点同步抬保证弹道是两点直连、朝向一致。
            // 之前让终点不抬(lift=60*(1-t))，导致弹道与 rotation 基准不一致 → 弹道稍微歪。
            screenY -= _startYOffset;

            // 原始传奇 SkillBullet：用 effect.dir(4) 的精灵帧，靠 t.rotation=orientation-90 旋转朝向目标，
            // 位置由 tween 平滑驱动。这里完全照搬：固定用 dir=_direction 的帧，按 source→目标算旋转角。
            // 注意原始子弹动画是多帧（同一火球自转），这里按飞行进度推进帧，不是固定帧 0。
            int frame = CurrentFrame % _totalFrames;

            // 旋转角必须用屏幕坐标方向算：格子是 48x32 的菱形(非等比)，用格子 delta 算 atan2 会导致
            // 视觉弹道(屏幕 48:32)与精灵朝向(格子 1:1)不一致 → 看起来"45度歪"。
            // 精灵默认朝上(dir=4)，故屏幕方向角再减 π/2（MonoGame 弧度、y 轴向下与 egret 一致）。
            float rotation = 0f;
            {
                float dx = endScreen.X - startScreen.X;
                float dy = endScreen.Y - startScreen.Y;
                if (dx != 0 || dy != 0)
                    rotation = (float)Math.Atan2(dy, dx) - (float)(Math.PI / 2);
            }

            // 与灵魂火符同款锚定：各用自己 model 的 stand，不做覆盖。
            Mir2OriginObjectLibrary.DrawAnchored(
                Mir2ObjectKind.Monster, _model, _action, _direction, frame,
                screenX, screenY, CommonColor.White, scale: 1f,
                blend: _addBlend ? MirBlend.Additive : MirBlend.Normal, rotation: rotation);

            if (OriginEffect.DebugDrawAnchor)
                OriginEffect.DrawAnchorMark(screenX, screenY, new KFramework.MonoGame.Color(60, 220, 220));
        }
    }
}
