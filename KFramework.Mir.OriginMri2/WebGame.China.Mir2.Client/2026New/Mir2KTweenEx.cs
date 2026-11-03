using KFramework.MonoGame;
using KFramework.MonoGameExtend;
using Point = MirEngine.Point;

namespace Client.MirControls
{
    public static class Mir2KTweenEx
    {
        public static KTween.TweenItem LocalMoveTo(this MirControl ctrl, Point to, float time)
        {
            if (ctrl == null) return null;
            Point from = ctrl.Location;
            if (time <= 0f) { ctrl.Location = to; return null; }
            return KTween.AddTween(time, fPercent =>
            {
                if (ctrl.IsDisposed) return;
                ctrl.Location = new Point(
                    (int)Math.Round(MathHelper.LerpPrecise(from.X, to.X, fPercent)),
                    (int)Math.Round(MathHelper.LerpPrecise(from.Y, to.Y, fPercent)));
            });
        }

        public static KTween.TweenItem LocalMoveTo(this MirControl ctrl, int x, int y, float time)
        { 
            return LocalMoveTo(ctrl, new Point(x, y), time);
        }

        public static KTween.TweenItem LocalMoveBy(this MirControl ctrl, Point delta, float time)
        {
            if (ctrl == null) return null;
            Point from = ctrl.Location;
            Point to = new Point(from.X + delta.X, from.Y + delta.Y);
            if (time <= 0f) { ctrl.Location = to; return null; }
            return KTween.AddTween(time, fPercent =>
            {
                if (ctrl.IsDisposed) return;
                ctrl.Location = new Point(
                    (int)Math.Round(MathHelper.LerpPrecise(from.X, to.X, fPercent)),
                    (int)Math.Round(MathHelper.LerpPrecise(from.Y, to.Y, fPercent)));
            });
        }

        public static KTween.TweenItem LocalMoveBy(this MirControl ctrl, int dx, int dy, float time)
        { 
            return LocalMoveBy(ctrl, new Point(dx, dy), time);
        }

        public static KTween.TweenItem LocalMoveX(this MirControl ctrl, int x, float time)
        {
            if (ctrl == null) return null;
            Point from = ctrl.Location;
            if (time <= 0f) { ctrl.Location = new Point(x, from.Y); return null; }
            return KTween.AddTween(time, fPercent =>
            {
                if (ctrl.IsDisposed) return;
                ctrl.Location = new Point(
                    (int)Math.Round(MathHelper.LerpPrecise(from.X, x, fPercent)),
                    from.Y);
            });
        }

        public static KTween.TweenItem LocalMoveY(this MirControl ctrl, int y, float time)
        {
            if (ctrl == null) return null;
            Point from = ctrl.Location;
            if (time <= 0f) { ctrl.Location = new Point(from.X, y); return null; }
            return KTween.AddTween(time, fPercent =>
            {
                if (ctrl.IsDisposed) return;
                ctrl.Location = new Point(
                    from.X,
                    (int)Math.Round(MathHelper.LerpPrecise(from.Y, y, fPercent)));
            });
        }
    }
}
