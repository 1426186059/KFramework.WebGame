using System;
using System.Collections.Generic;

namespace KFramework.MonoGame
{
    /// <summary>
    /// 触摸点状态。阶段由 <see cref="Input"/> 依据浏览器事件直接给出，
    /// 上层不需要再做「本帧与上一帧的 id 差分」。
    /// </summary>
    public enum KTouchState
    {
        Invalid = 0,
        /// <summary>本帧新按下</summary>
        Began,
        /// <summary>位置发生变化</summary>
        Moved,
        /// <summary>按下后位置未变</summary>
        Stationary,
        /// <summary>本帧抬起</summary>
        Ended,
        Canceled,
    }

    /// <summary>单个触摸点信息</summary>
    public struct KTouch
    {
        public int Id;
        public Vector2 Position;
        public Vector2 StartPosition;
        public Vector2 Delta;
        public KTouchState State;

        /// <summary>从按下到此刻的时长（秒）</summary>
        public float Duration;

        public bool IsBegan => State == KTouchState.Began;
        public bool IsMoved => KTouchState.Moved == State;
        public bool IsEnded => State == KTouchState.Ended;

        /// <summary>自按下点算起的总位移</summary>
        public Vector2 TotalDrag => Position - StartPosition;

        /// <summary>自按下点算起的直线距离（像素）</summary>
        public float TotalDistance => TotalDrag.Length();
    }

    /// <summary>轻点（Tap）：按下后很快抬起、且位移很小。</summary>
    public readonly struct KTapGesture(int id, Vector2 position, float duration)
    {
        public readonly int Id = id;
        public readonly Vector2 Position = position;
        public readonly float Duration = duration;
    }

    /// <summary>滑动（Swipe）：按下后朝某方向甩出。</summary>
    public readonly struct KSwipeGesture(int id, Vector2 startPosition, Vector2 endPosition,
                                         Vector2 delta, float duration)
    {
        public readonly int Id = id;
        public readonly Vector2 StartPosition = startPosition;
        public readonly Vector2 EndPosition = endPosition;
        public readonly Vector2 Delta = delta;
        public readonly float Duration = duration;

        public float Distance => Delta.Length();

        public Vector2 Direction => Delta.LengthSquared() > 0f ? Vector2.Normalize(Delta) : Vector2.Zero;

        public float Speed => Duration > 0f ? Distance / Duration : 0f;

        /// <summary>主方向（四向吸附）。屏幕坐标 Y 向下为正，故 Down 表示手指向下划。</summary>
        public KSwipeDirection SwipeDirection
        {
            get
            {
                if (Distance <= 0f) return KSwipeDirection.None;
                return Math.Abs(Delta.X) >= Math.Abs(Delta.Y)
                    ? (Delta.X > 0 ? KSwipeDirection.Right : KSwipeDirection.Left)
                    : (Delta.Y > 0 ? KSwipeDirection.Down : KSwipeDirection.Up);
            }
        }
    }

    /// <summary>滑动的主方向（四向吸附）</summary>
    public enum KSwipeDirection
    {
        None,
        Up,
        Down,
        Left,
        Right,
    }

    /// <summary>
    /// 触摸输入设备（基石层）—— 手机 / 平板的主要输入来源。
    ///
    /// <para>触点与阶段（Began / Moved / Ended）由 <see cref="Input"/> 维护，
    /// 本类负责包装成 <see cref="KTouch"/>，并识别手势：
    /// Tap（轻点）、LongPress（长按）、Swipe（滑动）、Pinch（双指缩放）。</para>
    ///
    /// <para>所有容器预分配并复用（只 Clear 不 new），运行期零 GC。</para>
    /// </summary>
    public class Input_Touch
    {
        public string Name => "Touch";
        public bool Enabled { get; set; } = true;

        // 无触点时底层返回空列表，恒定可用
        public bool IsAvailable => true;

        // ===== 手势阈值 =====

        /// <summary>轻点允许的最大位移（像素）</summary>
        public float TapMaxDistance { get; set; } = 24f;

        /// <summary>轻点允许的最长按住时间（秒）</summary>
        public float TapMaxDuration { get; set; } = 0.3f;

        /// <summary>长按判定时间（秒）</summary>
        public float LongPressDuration { get; set; } = 0.6f;

        /// <summary>滑动判定的最小位移（像素）</summary>
        public float SwipeMinDistance { get; set; } = 40f;

        /// <summary>滑动允许的最长时间（秒）</summary>
        public float SwipeMaxDuration { get; set; } = 1.0f;

        /// <summary>是否启用手势识别</summary>
        public bool GesturesEnabled { get; set; } = true;

        private readonly List<KTouch> _touches = new List<KTouch>();
        private readonly Dictionary<int, Vector2> _startPositions = new Dictionary<int, Vector2>();
        private readonly Dictionary<int, Vector2> _lastPositions = new Dictionary<int, Vector2>();
        private readonly Dictionary<int, float> _startTimes = new Dictionary<int, float>();
        private readonly HashSet<int> _longPressedIds = new HashSet<int>();

        private float _elapsedTotal;

        /// <summary>当前所有触摸点（含本帧刚抬起的，状态为 Ended）</summary>
        public IReadOnlyList<KTouch> Touches => _touches;

        /// <summary>触摸点数量</summary>
        public int TouchCount => _touches.Count;

        /// <summary>触摸开始</summary>
        public event Action<KTouch> TouchBegan;

        /// <summary>触摸移动</summary>
        public event Action<KTouch> TouchMoved;

        /// <summary>触摸结束</summary>
        public event Action<KTouch> TouchEnded;

        /// <summary>轻点</summary>
        public event Action<KTapGesture> Tap;

        /// <summary>长按：按住到阈值触发一次</summary>
        public event Action<KTouch> LongPress;

        /// <summary>滑动</summary>
        public event Action<KSwipeGesture> Swipe;

        public void Init()
        {
            _touches.Clear();
            _startPositions.Clear();
            _lastPositions.Clear();
            _startTimes.Clear();
            _longPressedIds.Clear();
            _elapsedTotal = 0f;
        }

        public void Update(GameTime gameTime)
        {
            _elapsedTotal += (float)gameTime.ElapsedGameTime.TotalSeconds;
            _touches.Clear();

            var active = Input.Touches;
            var began = Input.BeganTouches;
            var ended = Input.EndedTouches;
            var moved = Input.MovedTouches;

            // 1) 新按下的：记下起点与起始时间
            for (int i = 0; i < began.Count; i++)
            {
                int id = began[i].Id;
                _startPositions[id] = began[i].Position;
                _startTimes[id] = _elapsedTotal;
                _lastPositions[id] = began[i].Position;
                _longPressedIds.Remove(id);
            }

            // 2) 仍在屏上的触点
            for (int i = 0; i < active.Count; i++)
            {
                TouchPoint p = active[i];

                Vector2 start = _startPositions.TryGetValue(p.Id, out var s) ? s : p.Position;
                Vector2 last = _lastPositions.TryGetValue(p.Id, out var l) ? l : p.Position;
                float startTime = _startTimes.TryGetValue(p.Id, out var t) ? t : _elapsedTotal;
                float duration = _elapsedTotal - startTime;

                KTouchState state = KTouchState.Stationary;
                if (ContainsId(began, p.Id)) state = KTouchState.Began;
                else if (ContainsId(moved, p.Id)) state = KTouchState.Moved;

                var touch = new KTouch
                {
                    Id = p.Id,
                    Position = p.Position,
                    StartPosition = start,
                    Delta = p.Position - last,
                    State = state,
                    Duration = duration,
                };

                _lastPositions[p.Id] = p.Position;
                _touches.Add(touch);

                if (state == KTouchState.Began) TouchBegan?.Invoke(touch);
                else if (state == KTouchState.Moved) TouchMoved?.Invoke(touch);

                // 长按：按住到阈值就触发一次，不等抬手
                if (GesturesEnabled && duration >= LongPressDuration && _longPressedIds.Add(p.Id))
                {
                    LongPress?.Invoke(touch);
                }
            }

            // 3) 本帧抬起的
            for (int i = 0; i < ended.Count; i++)
            {
                TouchPoint p = ended[i];

                Vector2 start = _startPositions.TryGetValue(p.Id, out var s) ? s : p.Position;
                float duration = _startTimes.TryGetValue(p.Id, out var t) ? _elapsedTotal - t : 0f;

                var touch = new KTouch
                {
                    Id = p.Id,
                    Position = p.Position,
                    StartPosition = start,
                    Delta = Vector2.Zero,
                    State = KTouchState.Ended,
                    Duration = duration,
                };

                _touches.Add(touch);
                TouchEnded?.Invoke(touch);

                if (GesturesEnabled) RecognizeReleaseGesture(p.Id, touch);

                _startPositions.Remove(p.Id);
                _lastPositions.Remove(p.Id);
                _startTimes.Remove(p.Id);
                _longPressedIds.Remove(p.Id);
            }
        }

        private static bool ContainsId(IReadOnlyList<TouchPoint> list, int id)
        {
            for (int i = 0; i < list.Count; i++)
                if (list[i].Id == id) return true;
            return false;
        }

        /// <summary>抬手瞬间判定 Tap 还是 Swipe（位移小的是 Tap，大的是 Swipe）。</summary>
        private void RecognizeReleaseGesture(int id, KTouch touch)
        {
            float distance = touch.TotalDistance;

            if (distance <= TapMaxDistance && touch.Duration <= TapMaxDuration)
            {
                Tap?.Invoke(new KTapGesture(id, touch.Position, touch.Duration));
                return;
            }

            if (distance >= SwipeMinDistance && touch.Duration <= SwipeMaxDuration)
            {
                Swipe?.Invoke(new KSwipeGesture(
                    id, touch.StartPosition, touch.Position, touch.TotalDrag, touch.Duration));
            }
        }

        public void Reset()
        {
            Init();
        }

        /// <summary>获取指定索引的触摸点</summary>
        public KTouch GetTouch(int index) => _touches[index];

        /// <summary>按 id 查找触摸点</summary>
        public bool TryGetTouchById(int id, out KTouch touch)
        {
            for (int i = 0; i < _touches.Count; i++)
            {
                if (_touches[i].Id == id)
                {
                    touch = _touches[i];
                    return true;
                }
            }
            touch = default;
            return false;
        }

        /// <summary>本帧是否有触摸开始</summary>
        public bool AnyTouchBegan()
        {
            for (int i = 0; i < _touches.Count; i++)
                if (_touches[i].IsBegan) return true;
            return false;
        }

        /// <summary>双指缩放比例（相对上一帧）。少于两指时返回 1</summary>
        public float GetPinchScale()
        {
            if (_touches.Count < 2) return 1f;

            KTouch a = _touches[0];
            KTouch b = _touches[1];

            Vector2 prevA = a.Position - a.Delta;
            Vector2 prevB = b.Position - b.Delta;

            float prevDist = Vector2.Distance(prevA, prevB);
            float currDist = Vector2.Distance(a.Position, b.Position);

            if (prevDist <= 0.0001f) return 1f;
            return currDist / prevDist;
        }

        /// <summary>双指中心点，通常作为缩放锚点</summary>
        public Vector2 GetPinchCenter()
        {
            if (_touches.Count == 0) return Vector2.Zero;
            if (_touches.Count < 2) return _touches[0].Position;

            return (_touches[0].Position + _touches[1].Position) * 0.5f;
        }

        /// <summary>两指当前间距（像素）。少于两指时返回 0。</summary>
        public float GetPinchDistance()
        {
            if (_touches.Count < 2) return 0f;
            return Vector2.Distance(_touches[0].Position, _touches[1].Position);
        }
    }
}
