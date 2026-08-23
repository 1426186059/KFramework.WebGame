using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace KFramework.MonoGame
{
    /// <summary>
    /// 指针事件分发器 —— 把鼠标 / 触摸统一成指针事件，派发给已注册的 IClickable
    /// </summary>
    public class KPointerDispatcher : IKInputDevice
    {
        public string Name => "PointerDispatcher";
        public bool Enabled { get; set; } = true;
        public bool IsAvailable => true;

        private readonly KMouseInput _mouse;
        private readonly KTouchInput _touch;

        private readonly List<IClickable> _clickables = new List<IClickable>();
        private bool _sortDirty;

        /// <summary>每个指针 id 当前按住的目标</summary>
        private readonly Dictionary<int, IClickable> _pressTargets = new Dictionary<int, IClickable>();

        /// <summary>每个指针 id 按下时的坐标</summary>
        private readonly Dictionary<int, Vector2> _pressPositions = new Dictionary<int, Vector2>();

        /// <summary>鼠标当前悬停的目标</summary>
        private IClickable _hoverTarget;

        /// <summary>复用的事件参数，避免每帧 GC</summary>
        private readonly KPointerEventArgs _args = new KPointerEventArgs();

        /// <summary>任意位置按下（无论是否命中 IClickable）</summary>
        public event Action<KPointerEventArgs> PointerDown;

        /// <summary>任意位置抬起</summary>
        public event Action<KPointerEventArgs> PointerUp;

        /// <summary>点击完成（按下与抬起命中同一目标，或都未命中）</summary>
        public event Action<KPointerEventArgs> PointerClick;

        /// <summary>鼠标当前悬停的对象</summary>
        public IClickable HoverTarget => _hoverTarget;

        /// <summary>指针是否停在某个 IClickable 上（用于阻挡游戏世界的点击）</summary>
        public bool IsPointerOverUI => _hoverTarget != null;

        private static readonly MouseButton[] DispatchButtons =
        {
            MouseButton.Left,
            MouseButton.Right,
            MouseButton.Middle,
        };

        public KPointerDispatcher(KMouseInput mouse, KTouchInput touch)
        {
            _mouse = mouse;
            _touch = touch;
        }

        public void Init()
        {
        }

        /// <summary>注册一个可点击对象</summary>
        public void Register(IClickable clickable)
        {
            if (clickable == null) return;
            if (_clickables.Contains(clickable)) return;
            _clickables.Add(clickable);
            _sortDirty = true;
        }

        /// <summary>注销一个可点击对象</summary>
        public void Unregister(IClickable clickable)
        {
            if (clickable == null) return;
            if (_clickables.Remove(clickable))
            {
                if (ReferenceEquals(_hoverTarget, clickable)) _hoverTarget = null;

                // 清理仍持有该目标的按下记录
                var keys = new List<int>();
                foreach (var kv in _pressTargets)
                    if (ReferenceEquals(kv.Value, clickable)) keys.Add(kv.Key);
                for (int i = 0; i < keys.Count; i++) _pressTargets.Remove(keys[i]);
            }
        }

        /// <summary>清空所有注册对象</summary>
        public void Clear()
        {
            _clickables.Clear();
            _pressTargets.Clear();
            _pressPositions.Clear();
            _hoverTarget = null;
        }

        /// <summary>优先级变化后调用，下次分发时重新排序</summary>
        public void SetSortDirty() => _sortDirty = true;

        public void Update(GameTime gameTime)
        {
            if (_sortDirty)
            {
                // 优先级高的排前面，先接收事件
                _clickables.Sort((a, b) => b.Priority.CompareTo(a.Priority));
                _sortDirty = false;
            }

            if (_mouse != null && _mouse.Enabled && _mouse.IsAvailable) UpdateMouse();
            if (_touch != null && _touch.Enabled && _touch.IsAvailable) UpdateTouch();
        }

        public void Reset()
        {
            _pressTargets.Clear();
            _pressPositions.Clear();
            _hoverTarget = null;
        }

        // ===== 鼠标 =====

        private void UpdateMouse()
        {
            Vector2 pos = _mouse.Position;

            // 悬停检测
            IClickable hit = Raycast(pos);
            if (!ReferenceEquals(hit, _hoverTarget))
            {
                if (_hoverTarget != null)
                {
                    FillArgs(pos, pos, _mouse.Delta, KPointerSource.Mouse, MouseButton.Left, -1);
                    _hoverTarget.OnPointerExit(_args);
                }
                if (hit != null)
                {
                    FillArgs(pos, pos, _mouse.Delta, KPointerSource.Mouse, MouseButton.Left, -1);
                    hit.OnPointerEnter(_args);
                }
                _hoverTarget = hit;
            }

            // 按键
            for (int i = 0; i < DispatchButtons.Length; i++)
            {
                MouseButton btn = DispatchButtons[i];
                int pointerId = -(int)btn - 1; // 鼠标用负 id，避免和触摸 id 冲突

                if (_mouse.GetButtonDown(btn))
                    HandlePointerDown(pointerId, pos, _mouse.Delta, KPointerSource.Mouse, btn);
                else if (_mouse.GetButtonUp(btn))
                    HandlePointerUp(pointerId, pos, _mouse.Delta, KPointerSource.Mouse, btn);
            }
        }

        // ===== 触摸 =====

        private void UpdateTouch()
        {
            var touches = _touch.Touches;
            for (int i = 0; i < touches.Count; i++)
            {
                KTouch t = touches[i];
                if (t.IsBegan)
                    HandlePointerDown(t.Id, t.Position, t.Delta, KPointerSource.Touch, MouseButton.Left);
                else if (t.IsEnded)
                    HandlePointerUp(t.Id, t.Position, t.Delta, KPointerSource.Touch, MouseButton.Left);
            }
        }

        // ===== 通用处理 =====

        private void HandlePointerDown(int pointerId, Vector2 pos, Vector2 delta, KPointerSource source, MouseButton button)
        {
            _pressPositions[pointerId] = pos;

            IClickable target = Raycast(pos);
            _pressTargets[pointerId] = target;

            FillArgs(pos, pos, delta, source, button, pointerId);
            PointerDown?.Invoke(_args);

            if (target != null && !_args.Used)
            {
                if (target.OnPointerDown(_args)) _args.Use();
            }
        }

        private void HandlePointerUp(int pointerId, Vector2 pos, Vector2 delta, KPointerSource source, MouseButton button)
        {
            Vector2 pressPos = _pressPositions.TryGetValue(pointerId, out var p) ? p : pos;
            _pressTargets.TryGetValue(pointerId, out IClickable pressTarget);

            FillArgs(pos, pressPos, delta, source, button, pointerId);
            PointerUp?.Invoke(_args);

            IClickable target = Raycast(pos);

            if (target != null && !_args.Used)
            {
                if (target.OnPointerUp(_args)) _args.Use();
            }

            // 在别处抬起时，也要通知原按下目标，否则它会卡在按下状态
            if (pressTarget != null && !ReferenceEquals(pressTarget, target))
            {
                pressTarget.OnPointerUp(_args);
            }

            // 按下和抬起在同一目标上才算一次完整点击。
            // 注意：点击判定不受 _args.Used 影响——OnPointerUp 返回 true 只是阻止事件
            // 穿透到下层，并不代表点击被消费，二者是同一目标的两次独立回调。
            if (target != null && ReferenceEquals(target, pressTarget))
            {
                target.OnPointerClick(_args);
            }

            PointerClick?.Invoke(_args);

            _pressTargets.Remove(pointerId);
            _pressPositions.Remove(pointerId);
        }

        /// <summary>
        /// 射线检测：返回坐标命中的最高优先级对象
        /// </summary>
        public IClickable Raycast(Vector2 position)
        {
            for (int i = 0; i < _clickables.Count; i++)
            {
                IClickable c = _clickables[i];
                if (!c.RaycastEnabled) continue;
                if (c.Bounds.Contains(position.ToPoint())) return c;
            }
            return null;
        }

        /// <summary>
        /// 射线检测：返回坐标命中的所有对象（按优先级从高到低）
        /// </summary>
        public void RaycastAll(Vector2 position, List<IClickable> results)
        {
            results.Clear();
            var point = new Point((int)position.X, (int)position.Y);
            for (int i = 0; i < _clickables.Count; i++)
            {
                IClickable c = _clickables[i];
                if (!c.RaycastEnabled) continue;
                if (c.Bounds.Contains(point)) results.Add(c);
            }
        }

        private void FillArgs(Vector2 pos, Vector2 pressPos, Vector2 delta, KPointerSource source, MouseButton button, int pointerId)
        {
            _args.Reset();
            _args.Position = pos;
            _args.PressPosition = pressPos;
            _args.Delta = delta;
            _args.Source = source;
            _args.Button = button;
            _args.PointerId = pointerId;
        }
    }
}
