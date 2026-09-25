using Client.MirGraphics;
using Client.MirSounds;
using SlimDX;
using SlimDX.Direct3D9;
using WebGame.Mir2.MonoGame.Client;

namespace Client.MirControls
{
    public class MirControl : IDisposable
    {
        public static MirControl ActiveControl, MouseControl;
        
        public virtual Point DisplayLocation { get { return Parent == null ? Location : Parent.DisplayLocation.Add(Location); } }
        public Rectangle DisplayRectangle { get { return new Rectangle(DisplayLocation, Size); } }

        public bool GrayScale { get; set; }
        public bool Blending { get; set; }
        public float BlendingRate { get; set; }
        public BlendMode BlendMode { get; set; }

        private Color _backColour;
        public Color BackColour
        {
            get { return _backColour; }
            set
            {
                if (_backColour == value)
                    return;
                _backColour = value;
                OnBackColourChanged();
            }
        }
        public event EventHandler BackColourChanged;
        protected virtual void OnBackColourChanged()
        {
            TextureValid = false;
            Redraw();
            if (BackColourChanged != null)
                BackColourChanged.Invoke(this, EventArgs.Empty);
        }

        protected Rectangle BorderRectangle;
        private bool _border;
        protected Vector2[] _borderInfo;
        protected virtual Vector2[] BorderInfo
        {
            get
            {
                if (Size == Size.Empty)
                    return null;

                if (BorderRectangle != DisplayRectangle)
                {
                    _borderInfo = new[]
                        {
                            new Vector2(DisplayRectangle.Left - 1, DisplayRectangle.Top - 1),
                            new Vector2(DisplayRectangle.Right, DisplayRectangle.Top - 1),
                            new Vector2(DisplayRectangle.Left - 1, DisplayRectangle.Top - 1),
                            new Vector2(DisplayRectangle.Left - 1, DisplayRectangle.Bottom),
                            new Vector2(DisplayRectangle.Left - 1, DisplayRectangle.Bottom),
                            new Vector2(DisplayRectangle.Right, DisplayRectangle.Bottom),
                            new Vector2(DisplayRectangle.Right, DisplayRectangle.Top - 1),
                            new Vector2(DisplayRectangle.Right, DisplayRectangle.Bottom)
                        };

                    BorderRectangle = DisplayRectangle;
                }
                return _borderInfo;
            }
        }
        public virtual bool Border
        {
            get { return _border; }
            set
            {
                if (_border == value)
                    return;
                _border = value;
                OnBorderChanged();
            }
        }
        public event EventHandler BorderChanged;
        private void OnBorderChanged()
        {
            Redraw();
            if (BorderChanged != null)
                BorderChanged.Invoke(this, EventArgs.Empty);
        }

        private Color _borderColour;
        public Color BorderColour
        {
            get { return _borderColour; }
            set
            {
                if (_borderColour == value)
                    return;
                _borderColour = value;
                OnBorderColourChanged();
            }
        }
        public event EventHandler BorderColourChanged;
        private void OnBorderColourChanged()
        {
            Redraw();
            if (BorderColourChanged != null)
                BorderColourChanged.Invoke(this, EventArgs.Empty);
        }

        public long CleanTime;
        protected Texture ControlTexture;
        protected internal bool TextureValid;
        private bool _drawControlTexture;
        protected Size TextureSize;
        public bool DrawControlTexture
        {
            get { return _drawControlTexture; }
            set
            {
                if (_drawControlTexture == value)
                    return;
                _drawControlTexture = value;
                Redraw();
            }
        }
        protected virtual void CreateTexture()
        {
            if (ControlTexture == null || ControlTexture.Disposed)
            {
                DXManager.ControlList.Add(this);
                ControlTexture = DXManager.CreateRenderTarget(Size.Width, Size.Height);
                TextureSize = Size;
            }

            DXManager.ClearControlTexture(ControlTexture, BackColour);

            TextureValid = true;
        }

        /// <summary>
        /// 该层烘焙时使用的世界→屏幕变换（视口尺寸驱动）。默认按高度统一缩放；
        /// 层容器(世界层/UI 层)可覆写以实现不同映射。
        /// </summary>
        protected virtual KFramework.MonoGame.Matrix4x4 GetLayerTransform(int viewportWidth, int viewportHeight)
        {
            float s = (float)viewportHeight / Settings.ScreenHeight;
            return KFramework.MonoGame.Matrix4x4.CreateScaleTranslation(s, s, 0, 0f);
        }

        internal void DisposeTexture()
        {
            if (ControlTexture == null || ControlTexture.Disposed) return;

            ControlTexture.Dispose();
            ControlTexture = null;
            TextureValid = false;
            TextureSize = Size.Empty;

            DXManager.ControlList.Remove(this);
        }

        public List<MirControl> Controls { get; private set; }
        public event EventHandler ControlAdded , ControlRemoved;
        protected virtual void AddControl(MirControl control)
        {
            Controls.Add(control);
            OnControlAdded();
        }
        public virtual void InsertControl(int index, MirControl control)
        {
            if (control.Parent != this)
            {
                control.Parent = null;
                control._parent = this;
            }

            if (index >= Controls.Count)
                Controls.Add(control);
            else
            {
                Controls.Insert(index, control);
                OnControlAdded();
            }
        }
        private void RemoveControl(MirControl control)
        {
            Controls.Remove(control);
            OnControlRemoved();
        }
        private void OnControlAdded()
        {
            Redraw();
            if (ControlAdded != null)
                ControlAdded.Invoke(this, EventArgs.Empty);
        }
        private void OnControlRemoved()
        {
            Redraw();
            if (ControlRemoved != null)
                ControlRemoved.Invoke(this, EventArgs.Empty);
        }

        private bool _enabled;
        public bool Enabled
        {
            internal get { return Parent == null ? _enabled : Parent.Enabled && _enabled; }
            set
            {
                if (_enabled == value)
                    return;
                _enabled = value;
                OnEnabledChanged();
            }
        }
        public event EventHandler EnabledChanged;
        protected virtual void OnEnabledChanged()
        {
            Redraw();

            if (EnabledChanged != null)
                EnabledChanged.Invoke(this, EventArgs.Empty);

            if (!Enabled && ActiveControl == this)
                ActiveControl.Deactivate();

            if (Controls != null)
                foreach (MirControl control in Controls)
                    control.OnEnabledChanged();
        }
        public bool AllowDisabledMouseOver;

        protected bool HasShown;
        public event EventHandler Click , DoubleClick, BeforeDraw , AfterDraw , MouseEnter , MouseLeave , Shown , BeforeShown, Disposing;
        public event MouseEventHandler MouseWheel,MouseMove, MouseDown, MouseUp;
        public event KeyEventHandler KeyDown , KeyUp;
        public event KeyPressEventHandler KeyPress;

        private Color _foreColour;
        public Color ForeColour
        {
            get { return _foreColour; }
            set
            {
                if (_foreColour == value)
                    return;
                _foreColour = value;
                OnForeColourChanged();
            }
        }
        public event EventHandler ForeColourChanged;
        protected virtual void OnForeColourChanged()
        {
            TextureValid = false;
            if (ForeColourChanged != null)
                ForeColourChanged.Invoke(this, EventArgs.Empty);
        }

        private Point _location;
        public Point Location
        {
            get { return _location; }
            set
            {
                if (_location == value)
                    return;
                _location = value;
                OnLocationChanged();
            }
        }
        public event EventHandler LocationChanged;
        protected virtual void OnLocationChanged()
        {
            Redraw();
            if (Controls != null)
                for (int i = 0; i < Controls.Count; i++)
                    Controls[i].OnLocationChanged();

            if (LocationChanged != null)
                LocationChanged.Invoke(this, EventArgs.Empty);
        }


        /// <summary>
        /// 九宫格锚点（见 EAnchorType）。默认 None —— 不使用锚点，ApplyAnchor 不做任何事。
        /// </summary>
        public EAnchorType Anchor { get; set; } = EAnchorType.None;


        private Point _AnchorPos = Point.Empty;
        /// <summary>相对锚点的固定偏移，用于"贴边/居中后再偏移"这类布局。</summary>
        public Point AnchorPos
        {
            get
            {
                return _AnchorPos;
            }
            set
            {
                _AnchorPos = value;
                ApplyAnchor();
            }
        }

        /// <summary>
        /// 按 Anchor + AnchorPos 重算 Location。
        ///
        /// Anchor 为 None 时【什么都不做】：绝对定位的控件本来就无需变动，
        /// 所以引入本机制不会改变任何未设置锚点的控件的表现。
        ///
        /// 基准位置取自 #region Positions 的九个属性（Center/Top/Bottom/Left/Right/TopLeft/...），
        /// 它们读的是活的 Settings.ScreenWidth/Height，因此窗口尺寸变化后重新调用即可得到新位置。
        /// 布局表达式只写一处（Anchor + AnchorPos），构造时与窗口变化后都走这里，不会两处走样。
        ///
        /// 九宫格表达不了的布局可覆写本方法，例如"右边距固定 170px"而非"贴右边"。
        /// </summary>
        public virtual void ApplyAnchor()
        {
            if (Anchor == EAnchorType.None || IsDisposed) return;

            Point anchorPoint;
            switch (Anchor)
            {
                case EAnchorType.TopLeft: anchorPoint = TopLeft; break;
                case EAnchorType.TopCenter: anchorPoint = Top; break;
                case EAnchorType.TopRight: anchorPoint = TopRight; break;
                case EAnchorType.MiddleLeft: anchorPoint = Left; break;
                case EAnchorType.MiddleCenter: anchorPoint = Center; break;
                case EAnchorType.MiddleRight: anchorPoint = Right; break;
                case EAnchorType.BottomLeft: anchorPoint = BottomLeft; break;
                case EAnchorType.BottomCenter: anchorPoint = Bottom; break;
                case EAnchorType.BottomRight: anchorPoint = BottomRight; break;
                default: anchorPoint = Point.Empty; break;
            }

            Location = new Point(anchorPoint.X + AnchorPos.X, anchorPoint.Y + AnchorPos.Y);
        }


        private string _hint;
        public string Hint
        {
            get { return _hint; }
            set
            {
                if (_hint == value)
                    return;

                _hint = value;
                OnHintChanged(EventArgs.Empty);
            }
        }
        public event EventHandler HintChanged;
        private void OnHintChanged(EventArgs e)
        {
            Redraw();
            if (HintChanged != null)
                HintChanged.Invoke(this, e);
        }

        private bool _modal;
        public bool Modal
        {
            get { return _modal; }
            set
            {
                if (_modal == value)
                    return;
                _modal = value;
                OnModalChanged();
            }
        }
        public event EventHandler ModalChanged;
        private void OnModalChanged()
        {
            Redraw();
            if (ModalChanged != null)
                ModalChanged.Invoke(this, EventArgs.Empty);
        }

        protected internal bool Moving;
        private bool _movable;
        private Point _movePoint;

        public bool Movable
        {
            get { return _movable; }
            set
            {
                if (_movable == value)
                    return;
                _movable = value;
                OnMovableChanged();
            }
        }

        public event EventHandler MovableChanged;
        public event MouseEventHandler OnMoving;

        private void OnMovableChanged()
        {
            Redraw();
            if (MovableChanged != null)
                MovableChanged.Invoke(this, EventArgs.Empty);
        }

        private bool _notControl;
        public bool NotControl
        {
            private get { return _notControl; }
            set
            {
                if (_notControl == value)
                    return;
                _notControl = value;
                OnNotControlChanged();
            }
        }
        public event EventHandler NotControlChanged;
        private void OnNotControlChanged()
        {
            Redraw();
            if (NotControlChanged != null)
                NotControlChanged.Invoke(this, EventArgs.Empty);
        }

        private float _opacity;
        public float Opacity
        {
            get { return _opacity; }
            set
            {
                if (value > 1F)
                    value = 1F;
                if (value < 0F)
                    value = 0;

                if (_opacity == value)
                    return;

                _opacity = value;
                OnOpacityChanged();
            }
        }
        public event EventHandler OpacityChanged;
        private void OnOpacityChanged()
        {
            Redraw();
            if (OpacityChanged != null)
                OpacityChanged.Invoke(this, EventArgs.Empty);
        }

        private MirControl _parent;
        public MirControl Parent
        {
            get { return _parent; }
            set
            {
                if (_parent == value) return;

                if (_parent != null)
                    _parent.RemoveControl(this);
                _parent = value;
                if (_parent != null)
                    _parent.AddControl(this);
                OnParentChanged();
            }
        }
        public event EventHandler ParentChanged;
        protected virtual void OnParentChanged()
        {
            OnLocationChanged();
            if (ParentChanged != null)
                ParentChanged.Invoke(this, EventArgs.Empty);
        }


// ReSharper disable InconsistentNaming
        protected Size _size;
// ReSharper restore InconsistentNaming

        public virtual Size Size
        {
            get { return _size; }
            set
            {
                if (_size == value)
                    return;
                _size = value;
                OnSizeChanged();
            }
        }

        public virtual Size TrueSize
        {
            get { return _size; }
        }

        public event EventHandler SizeChanged;
        protected virtual void OnSizeChanged()
        {
            TextureValid = false;
            Redraw();
            
            if (SizeChanged != null)
                SizeChanged.Invoke(this, EventArgs.Empty);
        }

        private int _sound;
        public int Sound
        {
            get { return _sound; }
            set
            {
                if (_sound == value)
                    return;
                _sound = value;
                OnSoundChanged();
            }
        }
        public event EventHandler SoundChanged;
        private void OnSoundChanged()
        {
            if (SoundChanged != null)
                SoundChanged.Invoke(this, EventArgs.Empty);
        }

        private bool _sort;
        public bool Sort
        {
            get { return _sort; }
            set
            {
                if (_sort == value)
                    return;
                _sort = value;
                OnSortChanged();
            }
        }
        public event EventHandler SortChanged;
        private void OnSortChanged()
        {
            Redraw();
            if (SortChanged != null)
                SortChanged.Invoke(this, EventArgs.Empty);
        }
        public void TrySort()
        {
            if (Parent == null)
                return;

            Parent.TrySort();

            if (Parent.Controls[Parent.Controls.Count - 1] == this)
                return;

            if (!Sort) return;

            Parent.Controls.Remove(this);
            Parent.Controls.Add(this);

            Redraw();
        }

        private bool _visible;
        public virtual bool Visible
        {
            get { return Parent == null ? _visible : Parent.Visible && _visible; }
            set
            {
                if (_visible == value)
                    return;
                _visible = value;
                OnVisibleChanged();
            }
        }
        public event EventHandler VisibleChanged;
        protected virtual void OnVisibleChanged()
        {
            Redraw();
            if (VisibleChanged != null)
                VisibleChanged.Invoke(this, EventArgs.Empty);

            Moving = false;
            _movePoint = Point.Empty;

            if (Sort && Parent != null)
            {
                Parent.Controls.Remove(this);
                Parent.Controls.Add(this);
            }

            if (MouseControl == this && !Visible)
            {
                Dehighlight();
                Deactivate();
            }
            else if (IsMouseOver(CMain.MPoint))
                Highlight();


            if (Controls != null)
                foreach (MirControl control in Controls)
                    control.OnVisibleChanged();
        }
        protected void OnBeforeShown()
        {
            if (HasShown)
                return;

            if (Visible && IsMouseOver(CMain.MPoint))
                Highlight();

            if (BeforeShown != null)
                BeforeShown.Invoke(this, EventArgs.Empty);
        }
        protected void OnShown()
        {
            if (HasShown)
                return;

            if (Shown != null)
                Shown.Invoke(this, EventArgs.Empty);
            
            HasShown = true;
        }


        public virtual void MultiLine()
        {
        }



        protected Size ParentSize
        {
            get
            {
                if (Parent != null)
                {
                    return Parent.Size;
                }
                else
                {
                    return new Size(DXManager.GDevice.Viewport.Width, DXManager.GDevice.Viewport.Height);
                }
            }
        }

        protected Point Center
        {
            get { return new Point((ParentSize.Width - Size.Width) / 2, (ParentSize.Height - Size.Height) / 2); }
        }

        protected Point Left
        {
            get { return new Point(0, (ParentSize.Height - Size.Height) / 2); }
        }

        protected Point Top
        {
            get { return new Point((ParentSize.Width - Size.Width) / 2, 0); }
        }

        protected Point Right
        {
            get { return new Point(ParentSize.Width - Size.Width, (ParentSize.Height - Size.Height) / 2); }
        }

        protected Point Bottom
        {
            get { return new Point((ParentSize.Width - Size.Width) / 2, ParentSize.Height - Size.Height); }
        }

        protected Point TopLeft
        {
            get { return new Point(0, 0); }
        }

        protected Point TopRight
        {
            get { return new Point(ParentSize.Width - Size.Width, 0); }
        }

        protected Point BottomRight
        {
            get { return new Point(ParentSize.Width - Size.Width, ParentSize.Height - Size.Height); }
        }

        protected Point BottomLeft
        {
            get { return new Point(0, ParentSize.Height - Size.Height); }
        }



        public void BringToFront()
        {
            if (Parent == null) return;
            int index = _parent.Controls.IndexOf(this);
            if (index == _parent.Controls.Count - 1) return;

            _parent.Controls.RemoveAt(index);
            _parent.Controls.Add(this);
            Redraw();
        }

        public MirControl()
        {
            Controls = new List<MirControl>();
            _opacity = 1F;
            _enabled = true;
            _foreColour = Color.White;
            _visible = true;
            _sound = SoundList.None;
        }

        public virtual void Show()
        {
            if (Visible) return;
            Visible = true;
        }

        public virtual void Hide()
        {
            if (!Visible) return;
            Visible = false;
        }

        public virtual void Draw()
        {
            //2026-0--25 超出屏幕，我依然让他显示
            if (IsDisposed || !Visible)
                return;

            OnBeforeShown();

            BeforeDrawControl();
            DrawControl();
            DrawChildControls();
            DrawBorder();
            AfterDrawControl();

            CleanTime = CMain.Time + Settings.CleanDelay;

            OnShown();
        }

        protected virtual void BeforeDrawControl()
        {
            if (BeforeDraw != null)
                BeforeDraw.Invoke(this, EventArgs.Empty);
        }
        protected internal virtual void DrawControl()
        {
            if (!DrawControlTexture)
                return;

            if (!TextureValid)
                CreateTexture();

            if (ControlTexture == null || ControlTexture.Disposed)
                return;

            DXManager.DrawOpaque(ControlTexture, new Rectangle(0, 0, Size.Width, Size.Height), new Vector3?(new Vector3((float)(DisplayLocation.X), (float)(DisplayLocation.Y), 0.0f)), Color.White, Opacity);

            CleanTime = CMain.Time + Settings.CleanDelay;
        }
        protected void DrawChildControls()
        {
            if (Controls != null)
                for (int i = 0; i < Controls.Count; i++)
                    if (Controls[i] != null)
                        Controls[i].Draw();
        }
        protected virtual void DrawBorder()
        {
            if (!Border || BorderInfo == null)
                return;
            DXManager.Sprite.Flush();
            DXManager.Line.Draw(BorderInfo, _borderColour);
        }
        protected void AfterDrawControl()
        {
            if (AfterDraw != null)
                AfterDraw.Invoke(this, EventArgs.Empty);
        }

        protected virtual void Deactivate()
        {
            if (ActiveControl != this)
                return;

            ActiveControl = null;
            Moving = false;
            _movePoint = Point.Empty;
        }
        protected virtual void Dehighlight()
        {
            if (MouseControl != this)
                return;
            MouseControl.OnMouseLeave();
            MouseControl = null;
        }
        protected virtual void Activate()
        {
            if (ActiveControl == this)
                return;

            if (ActiveControl != null)
                ActiveControl.Deactivate();

            ActiveControl = this;
        }
        protected virtual void Highlight()
        {
            if (MouseControl == this)
                return;
            if (NotControl)
            {

            }
            if (MouseControl != null)
                MouseControl.Dehighlight();

            if (ActiveControl != null && ActiveControl != this) return;

            OnMouseEnter();
            MouseControl = this;
        }

        public virtual bool IsMouseOver(Point p)
        {
            // p 已是 UI(逻辑)坐标，不能再做 ScreenToWorldPos —— 所有调用点传的都是 CMain.MPoint，
            // 而它在输入入口已转换过一次（CMain_MouseMove / OnMouseDown / OnMouseUp），
            // DisplayRectangle 也是 UI 坐标；此处再转一次会重复扣减视口原点。
            return Visible && (DisplayRectangle.Contains(p) || Moving || Modal) && !NotControl;
        }

        protected virtual void OnMouseEnter()
        {
            if (!_enabled && !AllowDisabledMouseOver)
                return;

            Redraw();

            if (MouseEnter != null)
                MouseEnter.Invoke(this, EventArgs.Empty);
        }
        protected virtual void OnMouseLeave()
        {
            if (!_enabled && !AllowDisabledMouseOver)
                return;

            Redraw();

            if (MouseLeave != null)
                MouseLeave.Invoke(this, EventArgs.Empty);
        }
        public virtual void OnMouseClick(MouseEventArgs e)
        {
            if (!Enabled)
                return;

            if (Sound != SoundList.None)
                SoundManager.PlaySound(Sound);

            if (Click != null)
                InvokeMouseClick(e);
        }
        public virtual void OnMouseDoubleClick(MouseEventArgs e)
        {
            if (!Enabled)
                return;

            if (DoubleClick != null)
            {
                if (Sound != SoundList.None)
                    SoundManager.PlaySound(Sound);
                InvokeMouseDoubleClick(e);
            }
            else
                OnMouseClick(e);
        }
        public void InvokeMouseClick(EventArgs e)
        {
            if (Click != null)
                Click.Invoke(this, e);
        }
        public void InvokeMouseDoubleClick(EventArgs e)
        {
            DoubleClick.Invoke(this, e);
        }
        public virtual void OnMouseMove(MouseEventArgs e)
        {
            if (!_enabled && !AllowDisabledMouseOver)
                return;


            if (Moving)
            {
                // 直接用整数分量计算位移（等价于 MirEngine.Point.Subtract 扩展方法）。
                // 之前“拖不动”的根因不是这里清零，而是父容器 UILayer.TrueSize 恒为 (0,0)，
                // 导致下面 Parent.TrueSize 钳制把位置钳到 (0,0)。已修正 UILayerControl.TrueSize。
                Point tempPoint = new Point(CMain.MPoint.X - _movePoint.X, CMain.MPoint.Y - _movePoint.Y);

                if (Parent == null)
                {
                    if (tempPoint.Y + TrueSize.Height > Settings.ScreenHeight)
                        tempPoint.Y = Settings.ScreenHeight - TrueSize.Height - 1;

                    if (tempPoint.X + TrueSize.Width > Settings.ScreenWidth)
                        tempPoint.X = Settings.ScreenWidth - TrueSize.Width - 1;
                }
                else
                {
                    if (tempPoint.Y + TrueSize.Height > Parent.TrueSize.Height)
                        tempPoint.Y = Parent.TrueSize.Height - TrueSize.Height;

                    if (tempPoint.X + TrueSize.Width > Parent.TrueSize.Width)
                        tempPoint.X = Parent.TrueSize.Width - TrueSize.Width;
                }

                if (tempPoint.X < 0)
                    tempPoint.X = 0;
                if (tempPoint.Y < 0)
                    tempPoint.Y = 0;

                Location = tempPoint;
                if (OnMoving != null)
                    OnMoving.Invoke(this, e);
                return;
            }

            if (Controls != null)
                for (int i = Controls.Count - 1; i >= 0; i--)
                    if (Controls[i].IsMouseOver(CMain.MPoint))
                    {
                        Controls[i].OnMouseMove(e);
                        return;
                    }

            Highlight();

            if (MouseMove != null)
                MouseMove.Invoke(this, e);
        }
        public virtual void OnMouseDown(MouseEventArgs e)
        {
            if (!_enabled)
                return;

            Activate();

            TrySort();

            if (_movable)
            {
                Moving = true;
                _movePoint = new Point(CMain.MPoint.X - Location.X, CMain.MPoint.Y - Location.Y);
            }

            if (MouseDown != null)
                MouseDown.Invoke(this, e);
        }
        public virtual void OnMouseUp(MouseEventArgs e)
        {
            if (!_enabled)
                return;

            if (Moving)
            {
                Moving = false;
                _movePoint = Point.Empty;
            }

            if (ActiveControl != null) ActiveControl.Deactivate();

            if (MouseUp != null)
                MouseUp.Invoke(this, e);
        }
        public virtual void OnMouseWheel(MouseEventArgs e)
        {
            if (!Enabled)
                return;

            MouseWheel?.Invoke(this, e);
        }
        public virtual void OnKeyPress(KeyPressEventArgs e)
        {
            if (!_enabled)
                return;

            if (Controls != null)
                for (int i = Controls.Count - 1; i >= 0; i--)
                    if (e.Handled)
                        return;
                    else
                        Controls[i].OnKeyPress(e);

            if (KeyPress == null)
                return;
            KeyPress.Invoke(this, e);
        }
        public virtual void OnKeyDown(KeyEventArgs e)
        {
            if (!_enabled)
                return;

            if (Controls != null)
                for (int i = Controls.Count - 1; i >= 0; i--)
                    if (e.Handled)
                        return;
                    else
                        Controls[i].OnKeyDown(e);

            if (KeyDown == null)
                return;
            KeyDown.Invoke(this, e);
        }
        public virtual void OnKeyUp(KeyEventArgs e)
        {
            if (!_enabled)
                return;

            if (Controls != null)
                for (int i = Controls.Count - 1; i >= 0; i--)
                    if (e.Handled)
                        return;
                    else
                        Controls[i].OnKeyUp(e);

            if (KeyUp == null)
                return;
            KeyUp.Invoke(this, e);
        }

        public virtual void Redraw()
        {
            if (Parent != null) Parent.Redraw();
        }

        /// <summary>
        /// 递归使本控件及其所有子控件的离屏纹理失效，下一帧重绘时重新烘焙。
        /// 用于资源（库）异步加载完成后刷新场景：场景的离屏纹理只烘焙一次，
        /// 若烘焙时库尚未就绪会卡在默认背景色（整屏粉红），需借此强制重烘焙。
        /// </summary>
        public void Refresh()
        {
            TextureValid = false;
            if (Controls != null)
                for (int i = 0; i < Controls.Count; i++)
                    if (Controls[i] != null)
                        Controls[i].Refresh();
        }

        public static float FontDpiX
        {
            get { return CMain.Graphics == null ? 96f : CMain.Graphics.DpiX; }
        }

        public virtual Font ScaleFont(Font font)
        {
            var theFont = new Font(font.Name, font.Size * 96f / FontDpiX, font.Style);
            font.Dispose();
            
            return theFont;
        }

        public bool IsDisposed { get; private set; }

        public void Dispose()
        {
            if (IsDisposed)
                return;
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (Disposing != null)
                    Disposing.Invoke(this, EventArgs.Empty);

                Disposing = null;

                BackColourChanged = null;
                _backColour = Color.Empty;

                BorderChanged = null;
                _border = false;
                BorderRectangle = Rectangle.Empty;
                _borderInfo = null;

                BorderColourChanged = null;
                _borderColour = Color.Empty;

                DrawControlTexture = false;
                DisposeTexture();

                ControlAdded = null;
                ControlRemoved = null;

                if (Controls != null)
                {
                    for (int i = Controls.Count - 1; i >= 0; i--)
                    {
                        if (Controls[i] != null && !Controls[i].IsDisposed)
                            Controls[i].Dispose();
                    }

                    Controls = null;
                }
                _enabled = false;
                EnabledChanged = null;

                HasShown = false;

                BeforeDraw = null;
                AfterDraw = null;
                Shown = null;
                BeforeShown = null;

                Click = null;
                DoubleClick = null;
                MouseEnter = null;
                MouseLeave = null;
                MouseMove = null;
                MouseDown = null;
                MouseUp = null;
                MouseWheel = null;

                KeyPress = null;
                KeyUp = null;
                KeyDown = null;

                ForeColourChanged = null;
                _foreColour = Color.Empty;

                LocationChanged = null;
                _location = Point.Empty;

                ModalChanged = null;
                _modal = false;

                MovableChanged = null;
                _movePoint = Point.Empty;
                Moving = false;
                OnMoving = null;
                _movable = false;

                NotControlChanged = null;
                _notControl = false;

                OpacityChanged = null;
                _opacity = 0F;

                if (Parent != null && Parent.Controls != null)
                    Parent.Controls.Remove(this);
                ParentChanged = null;
                _parent = null;

                SizeChanged = null;
                _size = Size.Empty;

                SoundChanged = null;
                _sound = 0;

                VisibleChanged = null;
                _visible = false;

                if (ActiveControl == this) ActiveControl = null;
                if (MouseControl == this) MouseControl = null;
            }

            IsDisposed = true;
        }



    }
}
