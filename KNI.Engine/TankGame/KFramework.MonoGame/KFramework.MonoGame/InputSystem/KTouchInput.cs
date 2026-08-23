using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input.Touch;
using System;
using System.Collections.Generic;

namespace KFramework.MonoGame
{
    /// <summary>
    /// 单个触摸点信息
    /// </summary>
    public struct KTouch
    {
        public int Id;
        public Vector2 Position;
        public Vector2 StartPosition;
        public Vector2 Delta;
        public TouchLocationState State;

        public bool IsBegan => State == TouchLocationState.Pressed;
        public bool IsMoved => State == TouchLocationState.Moved;
        public bool IsEnded => State == TouchLocationState.Released;
    }

    /// <summary>
    /// 触摸输入设备
    /// </summary>
    public class KTouchInput : IKInputDevice
    {
        public string Name => "Touch";
        public bool Enabled { get; set; } = true;
        public bool IsAvailable => TouchPanel.GetCapabilities().IsConnected;

        private readonly List<KTouch> _touches = new List<KTouch>();
        private readonly Dictionary<int, Vector2> _startPositions = new Dictionary<int, Vector2>();
        private readonly Dictionary<int, Vector2> _lastPositions = new Dictionary<int, Vector2>();

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

            TouchCollection collection = TouchPanel.GetState();
            for (int i = 0; i < collection.Count; i++)
            {
                TouchLocation loc = collection[i];
                Vector2 pos = loc.Position;

                if (loc.State == TouchLocationState.Pressed)
                {
                    _startPositions[loc.Id] = pos;
                    _lastPositions[loc.Id] = pos;
                }

                Vector2 start = _startPositions.TryGetValue(loc.Id, out var s) ? s : pos;
                Vector2 last = _lastPositions.TryGetValue(loc.Id, out var l) ? l : pos;

                var touch = new KTouch
                {
                    Id = loc.Id,
                    Position = pos,
                    StartPosition = start,
                    Delta = pos - last,
                    State = loc.State,
                };

                _lastPositions[loc.Id] = pos;
                _touches.Add(touch);

                switch (loc.State)
                {
                    case TouchLocationState.Pressed:
                        TouchBegan?.Invoke(touch);
                        break;
                    case TouchLocationState.Moved:
                        TouchMoved?.Invoke(touch);
                        break;
                    case TouchLocationState.Released:
                        TouchEnded?.Invoke(touch);
                        _startPositions.Remove(loc.Id);
                        _lastPositions.Remove(loc.Id);
                        break;
                }
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
