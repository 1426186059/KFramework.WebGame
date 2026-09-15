using System.Buffers.Binary;

namespace KFramework.MonoGame
{
    /// <summary>触摸点状态。由事件直接给出，不需要上层做 id 差分。</summary>
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
        public bool IsMoved => State == KTouchState.Moved;
        public bool IsEnded => State == KTouchState.Ended;

        public Vector2 TotalDrag => Position - StartPosition;
        public float TotalDistance => TotalDrag.Length();
    }

    /// <summary>轻点（Tap）</summary>
    public readonly struct KTapGesture(int id, Vector2 position, float duration)
    {
        public readonly int Id = id;
        public readonly Vector2 Position = position;
        public readonly float Duration = duration;
    }

    /// <summary>滑动（Swipe）</summary>
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

        /// <summary>四向吸附的主方向。屏幕 Y 向下为正，故 Down 表示手指向下划。</summary>
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

    /// <summary>滑动的主方向</summary>
    public enum KSwipeDirection
    {
        None,
        Up,
        Down,
        Left,
        Right,
    }

    /// <summary>
    /// 触摸 —— 对原始输入事件的封装（手机 / 平板的主要输入来源）。
    ///
    /// 自己 poll 自己的事件队列（<c>input_touch</c> 模块），维护触点表与 Began/Moved/Ended 阶段，
    /// 并识别手势：Tap / LongPress / Swipe / Pinch。所有容器复用，运行期零 GC。
    /// </summary>
    public sealed class Input_Touch : IDisposable
    {
        // 事件类型（与 input_touch.ts 一致）
        private const int EvStart = 7;
        private const int EvMove = 8;
        private const int EvEnd = 9;

        private const int Stride = 16;           // 每条 4 个 i32
        private const int MaxEvents = 64;

        private static readonly byte[] _buffer = new byte[4 + MaxEvents * Stride];

        private static readonly List<TouchPoint> _touches = new List<TouchPoint>();
        private static readonly List<TouchPoint> _began = new List<TouchPoint>();
        private static readonly List<TouchPoint> _moved = new List<TouchPoint>();
        private static readonly List<TouchPoint> _ended = new List<TouchPoint>();
        private static readonly List<KTouch> _frame = new List<KTouch>();

        private static readonly Dictionary<int, Vector2> _startPositions = new Dictionary<int, Vector2>();
        private static readonly Dictionary<int, Vector2> _lastPositions = new Dictionary<int, Vector2>();
        private static readonly Dictionary<int, float> _startTimes = new Dictionary<int, float>();
        private static readonly HashSet<int> _longPressed = new HashSet<int>();

        // 手势时长用自包含的计时，不依赖外部时间源（基石层拿不到上层的 KTime）
        private static readonly long _startTicks = Environment.TickCount64;
        private static float _elapsed;

        // ===== 手势阈值 =====
        public static float TapMaxDistance { get; set; } = 24f;
        public static float TapMaxDuration { get; set; } = 0.3f;
        public static float LongPressDuration { get; set; } = 0.6f;
        public static float SwipeMinDistance { get; set; } = 40f;
        public static float SwipeMaxDuration { get; set; } = 1.0f;
        public static bool GesturesEnabled { get; set; } = true;

        /// <summary>当前留在屏上的触点</summary>
        public static IReadOnlyList<TouchPoint> Touches => _touches;

        /// <summary>本帧包装后的触点（含 Ended）</summary>
        public static IReadOnlyList<KTouch> FrameTouches => _frame;

        public static event Action<KTouch> TouchBegan;
        public static event Action<KTouch> TouchMoved;
        public static event Action<KTouch> TouchEnded;
        public static event Action<KTapGesture> Tap;
        public static event Action<KTouch> LongPress;
        public static event Action<KSwipeGesture> Swipe;

        private static int ReadInt(int offset)
            => BinaryPrimitives.ReadInt32LittleEndian(_buffer.AsSpan(offset, 4));

        /// <summary>每帧调用一次：取回本模块的事件队列并更新状态。</summary>
        public static void Poll()
        {
            _elapsed = (Environment.TickCount64 - _startTicks) / 1000f;

            _began.Clear();
            _moved.Clear();
            _ended.Clear();
            _frame.Clear();

            JSBind_Input.PollTouch(_buffer);

            int count = ReadInt(0);
            if (count > 0)
            {
                if (count > MaxEvents) count = MaxEvents;

                for (int i = 0; i < count; i++)
                {
                    int off = 4 + i * Stride;
                    int type = ReadInt(off);
                    int id = ReadInt(off + 4);
                    int x = ReadInt(off + 8);
                    int y = ReadInt(off + 12);

                    switch (type)
                    {
                        case EvStart: AddTouch(id, x, y); break;
                        case EvMove: MoveTouch(id, x, y); break;
                        case EvEnd: RemoveTouch(id); break;
                    }
                }
            }

            BuildFrame();
        }

        private static void AddTouch(int id, int x, int y)
        {
            var p = new TouchPoint(id, new Vector2(x, y));
            _touches.Add(p);
            _began.Add(p);
            _startPositions[id] = p.Position;
            _lastPositions[id] = p.Position;
            _startTimes[id] = _elapsed;
            _longPressed.Remove(id);
        }

        private static void MoveTouch(int id, int x, int y)
        {
            for (int i = 0; i < _touches.Count; i++)
            {
                if (_touches[i].Id != id) continue;

                var moved = new TouchPoint(id, new Vector2(x, y));
                _touches[i] = moved;
                _moved.Add(moved);
                return;
            }
        }

        private static void RemoveTouch(int id)
        {
            for (int i = 0; i < _touches.Count; i++)
            {
                if (_touches[i].Id != id) continue;

                _ended.Add(_touches[i]);
                _touches.RemoveAt(i);
                return;
            }
        }

        private static void BuildFrame()
        {
            // 仍在屏上的
            for (int i = 0; i < _touches.Count; i++)
            {
                TouchPoint p = _touches[i];
                Vector2 start = _startPositions.TryGetValue(p.Id, out var s) ? s : p.Position;
                Vector2 last = _lastPositions.TryGetValue(p.Id, out var l) ? l : p.Position;
                float duration = _elapsed - (_startTimes.TryGetValue(p.Id, out var t) ? t : _elapsed);

                KTouchState state = KTouchState.Stationary;
                if (ContainsId(_began, p.Id)) state = KTouchState.Began;
                else if (ContainsId(_moved, p.Id)) state = KTouchState.Moved;

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
                _frame.Add(touch);

                if (state == KTouchState.Began) TouchBegan?.Invoke(touch);
                else if (state == KTouchState.Moved) TouchMoved?.Invoke(touch);

                if (GesturesEnabled && duration >= LongPressDuration && _longPressed.Add(p.Id))
                    LongPress?.Invoke(touch);
            }

            // 本帧抬起的
            for (int i = 0; i < _ended.Count; i++)
            {
                TouchPoint p = _ended[i];
                Vector2 start = _startPositions.TryGetValue(p.Id, out var s) ? s : p.Position;
                float duration = _elapsed - (_startTimes.TryGetValue(p.Id, out var t) ? t : _elapsed);

                var touch = new KTouch
                {
                    Id = p.Id,
                    Position = p.Position,
                    StartPosition = start,
                    Delta = Vector2.Zero,
                    State = KTouchState.Ended,
                    Duration = duration,
                };

                _frame.Add(touch);
                TouchEnded?.Invoke(touch);

                if (GesturesEnabled) RecognizeReleaseGesture(p.Id, touch);

                _startPositions.Remove(p.Id);
                _lastPositions.Remove(p.Id);
                _startTimes.Remove(p.Id);
                _longPressed.Remove(p.Id);
            }
        }

        private static bool ContainsId(List<TouchPoint> list, int id)
        {
            for (int i = 0; i < list.Count; i++)
                if (list[i].Id == id) return true;
            return false;
        }

        private static void RecognizeReleaseGesture(int id, KTouch touch)
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

        public static void Reset()
        {
            _touches.Clear();
            _began.Clear();
            _moved.Clear();
            _ended.Clear();
            _frame.Clear();
            _startPositions.Clear();
            _lastPositions.Clear();
            _startTimes.Clear();
            _longPressed.Clear();
        }

        /// <summary>解绑 JS 侧监听。</summary>
        public static void Unbind()
        {
            JSBind_Input.UnbindTouch();
            Reset();
        }

        private bool _disposed;

        /// <summary>供生命周期统一管理的单例实例（实现了 <see cref="IDisposable"/>）。</summary>
        public static Input_Touch Instance { get; } = new Input_Touch();

        /// <summary>
        /// 释放底层资源：解绑 JS 侧触摸监听并清空状态。幂等，可安全重复调用。
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Unbind();
        }

        // ===== 查询 =====

        public static int TouchCount => _frame.Count;

        public static KTouch GetTouch(int index) => _frame[index];

        public static bool TryGetTouchById(int id, out KTouch touch)
        {
            for (int i = 0; i < _frame.Count; i++)
            {
                if (_frame[i].Id == id)
                {
                    touch = _frame[i];
                    return true;
                }
            }
            touch = default;
            return false;
        }

        public static bool AnyTouchBegan()
        {
            for (int i = 0; i < _frame.Count; i++)
                if (_frame[i].IsBegan) return true;
            return false;
        }

        /// <summary>双指缩放比例（相对上一帧）。少于两指时返回 1</summary>
        public static float GetPinchScale()
        {
            if (_frame.Count < 2) return 1f;

            KTouch a = _frame[0];
            KTouch b = _frame[1];

            float prevDist = Vector2.Distance(a.Position - a.Delta, b.Position - b.Delta);
            float currDist = Vector2.Distance(a.Position, b.Position);

            if (prevDist <= 0.0001f) return 1f;
            return currDist / prevDist;
        }

        /// <summary>双指中心点，通常作为缩放锚点</summary>
        public static Vector2 GetPinchCenter()
        {
            if (_frame.Count == 0) return Vector2.Zero;
            if (_frame.Count < 2) return _frame[0].Position;
            return (_frame[0].Position + _frame[1].Position) * 0.5f;
        }

        /// <summary>两指当前间距。少于两指返回 0。</summary>
        public static float GetPinchDistance()
        {
            if (_frame.Count < 2) return 0f;
            return Vector2.Distance(_frame[0].Position, _frame[1].Position);
        }
    }
}
