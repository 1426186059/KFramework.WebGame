using Client.MirGraphics;
using Client.MirScenes;
using KFramework.MonoGame;
using WebGame.Mir2.MonoGame.Client;   // CMain
using Color = MirEngine.Color;

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

            DXManager.Draw(sprite.Texture, src, dst, Color.White,
                           (_addBlend ? MirBlend.Additive : MirBlend.Normal).ToBlendState(), SpriteEffects.None);
        }
    }

    /// <summary>
    /// 原始传奇数据驱动"飞行道具"特效：与 OriginEffect 同样读 5config.effect（model/action/dir/intval/blend），
    /// 但沿 Source→Destination 插值飞行（替代已移除的经典 .Lib Missile），抵达时触发 Complete 供命中特效挂载。
    /// 灵魂火符(Lv1 effect 654=model13023 / Lv5 754=model13026) 用它画飞行符。
    /// </summary>
    public sealed class OriginMissile : Effect
    {
        private readonly int _model;
        private readonly int _action;
        private readonly int _direction;
        private readonly int _intval;
        private readonly bool _addBlend;
        private readonly Point _source;
        private readonly Point _destination;
        private int _totalFrames;

        public MapObject Target;
        public Point Destination;

        public OriginMissile(int effectId, MapObject owner, MapObject target, Point destination, long duration = 500)
            : base(null, 0, 1, (int)duration, owner)
        {
            Target = target;
            Destination = destination;
            _source = owner.CurrentLocation;

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
        }

        public override void Process()
        {
            if (_model <= 0) { Remove(); return; }
            if (CMain.Time < NextFrame) return;

            if (Target != null) Destination = Target.CurrentLocation;

            CurrentFrame++;
            NextFrame = CMain.Time + _intval;
            GameScene.Scene.MapControl.TextureValid = false;

            int travel = Math.Max(1, Functions.MaxDistance(_source, Destination) * 50);
            if (CMain.Time >= Start + travel)
                Remove();
        }

        public override void Draw()
        {
            if (_model <= 0 || CMain.Time < Start) return;

            int travel = Math.Max(1, Functions.MaxDistance(_source, Destination) * 50);
            long elapsed = CMain.Time - Start;
            float t = Math.Min(1f, elapsed / (float)travel);

            int cellX = _source.X + (int)((Destination.X - _source.X) * t);
            int cellY = _source.Y + (int)((Destination.Y - _source.Y) * t);

            int screenX = (cellX - MapObject.User.Movement.X + MapControl.OffSetX) * MapControl.CellWidth;
            int screenY = (cellY - MapObject.User.Movement.Y + MapControl.OffSetY) * MapControl.CellHeight;
            screenX += MapObject.User.OffSetMove.X;
            screenY += MapObject.User.OffSetMove.Y;

            int frame = CurrentFrame % _totalFrames;
            if (!Mir2OriginObjectLibrary.TryGetSprite(Mir2ObjectKind.Monster, _model, _action, _direction, frame, out var sprite) || sprite == null)
                return;

            Rectangle src = sprite.SourceRectangle;
            int x = screenX - src.Width / 2;
            int y = screenY - src.Height / 2;
            Rectangle dst = new Rectangle(x, y, src.Width, src.Height);

            DXManager.Draw(sprite.Texture, src, dst, Color.White,
                           (_addBlend ? MirBlend.Additive : MirBlend.Normal).ToBlendState(), SpriteEffects.None);
        }
    }
}
