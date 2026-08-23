using Microsoft.Xna.Framework;
using System;

namespace KFramework.MonoGame
{
    /// <summary>
    /// LeanTween 兼容 API 层 — 内部调用 KTween 实现
    /// 所有方法返回 TweenItem，支持链式调用
    /// </summary>
    public static class KTweenEx
    {
        public static KTween.TweenItem move(KTransform obj, Vector2 to, float time)
        {
            Vector2 from = obj.WorldPosition;
            return KTween.AddTween(obj, time, fPercent =>
            {
                obj.WorldPosition = Vector2.LerpPrecise(from, to, fPercent);
            });
        }

        /// <summary>
        /// 沿路径点数组移动 — 总时间均匀分配到每一段
        /// path.Length = 4 → 3 段，每段占 1/3 时间
        /// </summary>
        public static KTween.TweenItem move(KTransform obj, Vector2[] path, float time)
        {
            int segCount = path.Length - 1;
            if (segCount <= 0) return null;
            Vector2[] p = path; // 捕获副本
            return KTween.AddTween(obj, time, fPercent =>
            {
                if (fPercent >= 1f) { obj.WorldPosition = p[p.Length - 1]; return; }
                float t = fPercent * segCount;
                int idx = (int)t;
                if (idx >= segCount) idx = segCount - 1;
                float segT = t - idx;
                obj.WorldPosition = Vector2.LerpPrecise(p[idx], p[idx + 1], segT);
            });
        }

        /// <summary>
        /// 贝塞尔曲线路径移动 — 三次贝塞尔串联
        /// 路径长度必须为 3n+1（4, 7, 10, 13...），每 4 个点 = 一段贝塞尔
        /// </summary>
        public static KTween.TweenItem moveBezier(KTransform obj, Vector2[] path, float time)
        {
            int segCount = (path.Length <= 0) ? 0 : (path.Length - 1) / 3;
            if (segCount <= 0 || (path.Length - 1) % 3 != 0) return null;
            Vector2[] p = path;
            return KTween.AddTween(obj, time, fPercent =>
            {
                if (fPercent >= 1f) { obj.WorldPosition = p[p.Length - 1]; return; }
                float t = fPercent * segCount;
                int segIdx = (int)t;
                if (segIdx >= segCount) segIdx = segCount - 1;
                float bt = t - segIdx; // 段内 t [0,1]

                int i = segIdx * 3; // P0, P1, P2, P3 起始下标
                float u = 1f - bt;
                obj.WorldPosition =
                    u * u * u * p[i] +
                    3f * u * u * bt * p[i + 1] +
                    3f * u * bt * bt * p[i + 2] +
                    bt * bt * bt * p[i + 3];
            });
        }

        public static KTween.TweenItem moveX(KTransform obj, float x, float time)
        {
            Vector2 from = obj.WorldPosition;
            return KTween.AddTween(obj, time, fPercent =>
            {
                obj.WorldPosition = new Vector2(
                    MathHelper.LerpPrecise(from.X, x, fPercent),
                    from.Y);
            });
        }

        public static KTween.TweenItem moveY(KTransform obj, float y, float time)
        {
            Vector2 from = obj.WorldPosition;
            return KTween.AddTween(obj, time, fPercent =>
            {
                obj.WorldPosition = new Vector2(
                    from.X,
                    MathHelper.LerpPrecise(from.Y, y, fPercent));
            });
        }

        public static KTween.TweenItem moveLocal(KTransform obj, Vector2 to, float time)
        {
            Vector2 from = obj.LocalPosition;
            return KTween.AddTween(obj, time, fPercent =>
            {
                obj.LocalPosition = Vector2.LerpPrecise(from, to, fPercent);
            });
        }

        public static KTween.TweenItem moveLocal(KTransform obj, Vector2[] path, float time)
        {
            int segCount = path.Length - 1;
            if (segCount <= 0) return null;
            Vector2[] p = path;
            return KTween.AddTween(obj, time, fPercent =>
            {
                if (fPercent >= 1f) { obj.LocalPosition = p[p.Length - 1]; return; }
                float t = fPercent * segCount;
                int idx = (int)t;
                if (idx >= segCount) idx = segCount - 1;
                float segT = t - idx;
                obj.LocalPosition = Vector2.LerpPrecise(p[idx], p[idx + 1], segT);
            });
        }

        public static KTween.TweenItem moveLocalBezier(KTransform obj, Vector2[] path, float time)
        {
            int segCount = (path.Length - 1) / 3;
            if (segCount <= 0 || (path.Length - 1) % 3 != 0) return null;
            Vector2[] p = path;
            return KTween.AddTween(obj, time, fPercent =>
            {
                if (fPercent >= 1f) { obj.LocalPosition = p[p.Length - 1]; return; }
                float t = fPercent * segCount;
                int segIdx = (int)t;
                if (segIdx >= segCount) segIdx = segCount - 1;
                float bt = t - segIdx;

                int i = segIdx * 3;
                float u = 1f - bt;
                obj.LocalPosition =
                    u * u * u * p[i] +
                    3f * u * u * bt * p[i + 1] +
                    3f * u * bt * bt * p[i + 2] +
                    bt * bt * bt * p[i + 3];
            });
        }

        public static KTween.TweenItem moveLocalX(KTransform obj, float x, float time)
        {
            Vector2 from = obj.LocalPosition;
            return KTween.AddTween(obj, time, fPercent =>
            {
                obj.LocalPosition = new Vector2(
                    MathHelper.LerpPrecise(from.X, x, fPercent),
                    from.Y);
            });
        }

        public static KTween.TweenItem moveLocalY(KTransform obj, float y, float time)
        {
            Vector2 from = obj.LocalPosition;
            return KTween.AddTween(obj, time, fPercent =>
            {
                obj.LocalPosition = new Vector2(
                    from.X,
                    MathHelper.LerpPrecise(from.Y, y, fPercent));
            });
        }

        public static KTween.TweenItem scale(KTransform obj, Vector2 to, float time)
        {
            Vector2 from = obj.LocalScale;
            return KTween.AddTween(obj, time, fPercent =>
            {
                obj.LocalScale = Vector2.LerpPrecise(from, to, fPercent);
            });
        }

        public static KTween.TweenItem rotateAround(KTransform obj, float angle, float time)
        {
            float startRot = obj.LocalRotation;
            float delta = angle;
            return KTween.AddTween(obj, time, fPercent =>
            {
                obj.LocalRotation = startRot + delta * fPercent;
            });
        }

        public static KTween.TweenItem rotateAroundLocal(KTransform obj, float angle, float time)
        {
            float startRot = obj.LocalRotation;
            float delta = angle;
            return KTween.AddTween(obj, time, fPercent =>
            {
                obj.LocalRotation = startRot + delta * fPercent;
            });
        }

        public static KTween.TweenItem color(SpriteRenderer obj, Color to, float time)
        {
            Color from = obj.Color;
            return KTween.AddTween(obj, time, fPercent =>
            {
                obj.Color = Color.LerpPrecise(from, to, fPercent);
            });
        }

        public static KTween.TweenItem color(KImage obj, Color to, float time)
        {
            Color from = obj.Color;
            return KTween.AddTween(obj, time, fPercent =>
            {
                obj.Color = Color.LerpPrecise(from, to, fPercent);
            });
        }

    }
}
