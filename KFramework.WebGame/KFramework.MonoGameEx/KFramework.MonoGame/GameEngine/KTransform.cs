using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace KFramework.MonoGame
{
    public class KTransform : IDisposable
    {
        // 本地属性
        private readonly LinkedListNode<KTransform> mEntry;
        private Vector2 _cacheLocalPosition;
        private Vector2 _cacheLocalScale;
        private float _cacheLocalRotation;
        private readonly LinkedList<KTransform> _cacheChildList = new LinkedList<KTransform>();
        private KTransform _cacheParent;
        private bool _cacheActive;
        private bool _cacheDispose;

        // 缓存的世界矩阵（脏标记更新）
        private Matrix _worldMatrix;
        private bool _isDirty = true;
        public string Name { get; set; } = "GameObject";
        public bool IsDispose { get { return _cacheDispose; } }

        public KTransform()
        {
            mEntry = new LinkedListNode<KTransform>(this);
            _cacheLocalScale = Vector2.One;
            _cacheActive = true;
            SetDirty();
        }

        // 标记自身及所有子节点需要更新
        private void SetDirty()
        {
            _isDirty = true;
            foreach (var child in _cacheChildList)
            {
                child.SetDirty();
            }
        }

        public T GetParent<T>() where T : KTransform
        {
            return Parent as T;
        }

        public LinkedList<KTransform> ChildList
        {
            get { return _cacheChildList; }
        }

        public KTransform Parent
        {
            get { return _cacheParent; }
            set
            {
                if (_cacheParent != value)
                {
                    KTransform oldParent = _cacheParent;
                    _cacheParent = value;

                    if (oldParent != null)
                    {
                        oldParent._cacheChildList.Remove(this.mEntry);
                    }

                    if (_cacheParent != null)
                    {
                        _cacheParent._cacheChildList.AddLast(this.mEntry);
                    }

                    SetDirty();
                    this.OnParentChanged();
                }
            }
        }

        public bool activeSelf
        {
            get { return _cacheActive; }
            set
            {
                if (value != _cacheActive)
                {
                    _cacheActive = value;
                    SetDirty();
                }
            }
        }

        public bool activeInHierarchy
        {
            get
            {
                if (Parent != null)
                {
                    return _cacheActive && Parent.activeInHierarchy;
                }
                else
                {
                    return _cacheActive && Parent == null;
                }
            }
        }

        public Vector2 LocalPosition
        {
            get { return _cacheLocalPosition; }
            set
            {
                if (value != _cacheLocalPosition)
                {
                    _cacheLocalPosition = value;
                    SetDirty();
                    OnLocalPositionChanged();
                }
            }
        }

        public float LocalRotation
        {
            get { return _cacheLocalRotation; }
            set
            {
                if (value != _cacheLocalRotation)
                {
                    _cacheLocalRotation = value;
                    SetDirty();
                }
            }
        }

        public Vector2 LocalScale
        {
            get { return _cacheLocalScale; }
            set
            {
                if (value != _cacheLocalScale)
                {
                    _cacheLocalScale = value;
                    SetDirty();
                }
            }
        }


        // 获取世界矩阵（懒计算）
        public Matrix Local_To_World_Matrix
        {
            get
            {
                if (_isDirty)
                {
                    // 构建本地矩阵：缩放 → 旋转 → 平移
                    Matrix localMatrix = Matrix.CreateScale(LocalScale.X, LocalScale.Y, 1f)
                                       * Matrix.CreateRotationZ(LocalRotation)
                                       * Matrix.CreateTranslation(LocalPosition.X, LocalPosition.Y, 0f);

                    // 有父节点则乘以父节点世界矩阵，否则就是自身
                    _worldMatrix = Parent != null
                        ? localMatrix * Parent.Local_To_World_Matrix
                        : localMatrix;

                    _isDirty = false;
                }
                return _worldMatrix;
            }
        }

        // ===== 世界属性（只读） =====

        public Vector2 WorldPosition
        {
            get
            {
                Matrix m = Local_To_World_Matrix;
                return new Vector2(m.M41, m.M42);
            }

            set
            {
                // 设置世界位置：LocalPosition 是相对父节点的局部坐标，
                // 故用【父节点】世界矩阵求逆把世界点转回局部。
                // 不能用 WorldToLocal（它用自身矩阵，会多减一次自身 LocalPosition）。
                Matrix parentWorld = Parent != null ? Parent.Local_To_World_Matrix : Matrix.Identity;
                var inv = Matrix.Invert(parentWorld);
                LocalPosition = Vector2.Transform(value, inv);
            }
        }

        public float WorldRotation
        {
            get
            {
                Matrix m = Local_To_World_Matrix;
                // MonoGame 的 CreateRotationZ 把 sin 放在 M12、M21=-sin，
                // 故旋转角 = Atan2(M12, M11)（注意不是 M21）。
                return MathF.Atan2(m.M12, m.M11);
            }
        }

        public Vector2 WorldScale
        {
            get
            {
                Matrix m = Local_To_World_Matrix;
                float scaleX = MathF.Sqrt(m.M11 * m.M11 + m.M12 * m.M12);
                float scaleY = MathF.Sqrt(m.M21 * m.M21 + m.M22 * m.M22);
                return new Vector2(scaleX, scaleY);
            }
        }

        // 本地坐标 → 世界坐标
        //注意，这是父物体的孩子孩子节点的本地坐标，得用父物体的 矩阵变换
        public Vector2 LocalToWorld(Vector2 localPoint)
        {
            return Vector2.Transform(localPoint, Local_To_World_Matrix);
        }

        // 世界坐标 → 本地坐标（通用，对标 Transform.InverseTransformPoint）
        // 使用【自身】世界矩阵求逆：把世界点变换到本节点局部坐标系。
        // 注意：WorldPosition.setter 设置世界位置用的是【父】矩阵，不要与本方法混淆。
        public Vector2 WorldToLocal(Vector2 worldPoint)
        {
            var inv = Matrix.Invert(Local_To_World_Matrix);
            return Vector2.Transform(worldPoint, inv);
        }

        protected virtual void OnLocalPositionChanged()
        {

        }

        protected virtual void OnParentChanged()
        {

        }

        public virtual void Update()
        {

        }

        public virtual void Draw()
        {

        }

        public virtual void Dispose()
        {
            if (!_cacheDispose)
            {
                _cacheDispose = true;
                this.Parent = null;
            }
        }

        public static bool operator ==(KTransform a, KTransform b)
        {
            if (ReferenceEquals(a, null) && ReferenceEquals(b, null))
            {
                return true;
            }
            else if (ReferenceEquals(a, null))
            {
                return b.IsDispose;
            }
            else if (ReferenceEquals(b, null))
            {
                return a.IsDispose;
            }
            else
            {
                return ReferenceEquals(a, b);
            }
        }

        public static bool operator !=(KTransform a, KTransform b)
        {
            return !(a == b);
        }
    }

}
