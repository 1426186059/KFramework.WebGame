using KFramework;
using System;
using System.Collections.Generic;

namespace KFramework.MonoGameExtend
{
    /// <summary>
    /// 触摸点状态。
    /// MonoGame 用 TouchLocationState 由底层驱动给出，而 KFramework 的 TouchPoint
    /// 只有 Id / Position，没有状态字段，因此这里通过「本帧与上一帧的 id 差分」推断。
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
        /// <summary>本帧抬起（上一帧存在、本帧消失）</summary>
        Ended,
        Canceled,
    }

    /// <summary>
    /// 单个触摸点信息
    /// </summary>
    public struct KTouch
    {
        public int Id;
        public Vector2 Position;
        public Vector2 StartPosition;
        public Vector2 Delta;
        public KTouchState State;

        public bool IsBegan => State == KTouchState.Began;
        public bool IsMoved => State == KTouchState.Moved;
        public bool IsEnded => State == KTouchState.Ended;
    }

    /// <summary>
    /// 触摸输入设备
    /// </summary>
    public class KTouchInput : IKInputDevice
    {
        public string Name => "Touch";
        public bool Enabled { get; set; } = true;

        // KFramework 没有「是否支持触摸」的能力查询；GetTouchState 在无触点时返回 Count=0，
        // 因此这里恒定可用，由 Update 自行判断有没有触点。
        public bool IsAvailable => true;

        private readonly List<KTouch> _touches = new List<KTouch>();
        private readonly Dictionary<int, Vector2> _startPositions = new Dictionary<int, Vector2>();
        private readonly Dictionary<int, Vector2> _lastPositions = new Dictionary<int, Vector2>();
        private readonly HashSet<int> _currentIds = new HashSet<int>();
        private readonly List<int> _endedIds = new List<int>();

        /// <summary>当前所有触摸点</summary>
        public IReadOnlyList<KTouch> Touches => _touches;

        /// <summary>触摸点数量</summary>
        public int TouchCount => _touches.Count;

        /// <summary>触摸开始</summary>
        public event Action<KTouch> TouchBegan;

        /// <summary>触摸移动</summary>
        public event Action<KTouch> TouchMoved;

        /// <summary>触摸结束</summary>
        public event Action<KTouch> TouchEnded;

        public void Init()
        {
            _touches.Clear();
            _startPositions.Clear();
            _lastPositions.Clear();
        }

        public void Update(GameTime gameTime)
        {
            _touches.Clear();
            _currentIds.Clear();
            _endedIds.Clear();

            TouchCollection collection = Input.GetTouchState();

            // 1) 本帧存在的触点：新 id = Began，位置变了 = Moved，否则 Stationary
            for (int i = 0; i < collection.Count; i++)
            {
                TouchPoint point = collection[i];
                _currentIds.Add(point.Id);

                bool isNew = !_lastPositions.ContainsKey(point.Id);
                if (isNew)
                {
                    _startPositions[point.Id] = point.Position;
                    _lastPositions[point.Id] = point.Position;
                }

                Vector2 start = _startPositions.TryGetValue(point.Id, out var s) ? s : point.Position;
                Vector2 last = _lastPositions.TryGetValue(point.Id, out var l) ? l : point.Position;

                KTouchState state = isNew
                    ? KTouchState.Began
                    : (point.Position != last ? KTouchState.Moved : KTouchState.Stationary);

                var touch = new KTouch
                {
                    Id = point.Id,
                    Position = point.Position,
                    StartPosition = start,
                    Delta = point.Position - last,
                    State = state,
                };

                _lastPositions[point.Id] = point.Position;
                _touches.Add(touch);

                if (state == KTouchState.Began) TouchBegan?.Invoke(touch);
                else if (state == KTouchState.Moved) TouchMoved?.Invoke(touch);
            }

            // 2) 上一帧有、本帧消失的触点 = Ended
            foreach (var pair in _lastPositions)
            {
                if (_currentIds.Contains(pair.Key)) continue;
                _endedIds.Add(pair.Key);
            }

            for (int i = 0; i < _endedIds.Count; i++)
            {
                int id = _endedIds[i];
                Vector2 start = _startPositions.TryGetValue(id, out var s) ? s : _lastPositions[id];

                var touch = new KTouch
                {
                    Id = id,
                    Position = _lastPositions[id],
                    StartPosition = start,
                    Delta = Vector2.Zero,
                    State = KTouchState.Ended,
                };

                _touches.Add(touch);
                TouchEnded?.Invoke(touch);
            }

            for (int i = 0; i < _endedIds.Count; i++)
            {
                _startPositions.Remove(_endedIds[i]);
                _lastPositions.Remove(_endedIds[i]);
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

        /// <summary>
        /// 双指缩放比例（相对上一帧）。少于两指时返回 1
        /// </summary>
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
    }
}
