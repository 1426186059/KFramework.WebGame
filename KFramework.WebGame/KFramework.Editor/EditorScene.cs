using KFramework.MonoGame;
using KFramework.MonoGameExtend;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace KFramework.Editor
{
    /// <summary>
    /// 类 Unity 编辑器主场景：
    /// 顶部工具栏（含「场景/游戏」视图切换按钮）、左侧资源(Project)、中央单一视图（场景/游戏切换）、
    /// 右侧层级(Hierarchy) + 检视(Inspector)。
    /// 默认场景里有一个 Cube 物体，位于相机正前方便于「在屏幕正中」观察；
    /// 检视面板里 位置/旋转/缩放 既可横向拖拽改值，也可单击聚焦后键入（含中文 IME）。
    /// </summary>
    public sealed class EditorScene : KSceneBase
    {
        private readonly EditorSceneModel _model = new();
        private readonly List<(Rectangle rect, Action act)> _clickables = new();
        private readonly List<(Rectangle rect, Action<float> apply)> _fields = new();
        private readonly List<(Rectangle rect, EditorObject obj)> _hierarchyRows = new();
        private readonly TextBox[] _numBoxes = new TextBox[9];   // 位置/旋转/缩放 各 3 个分量

        // 拖拽改值 / 聚焦输入的状态
        private int _pendingField = -1;
        private float _dragStartX;
        private bool _dragging;

        // 右键上下文菜单
        private bool _contextOpen;
        private Vector2 _contextPos;
        private readonly List<(string label, Action act)> _contextItems = new();

        private bool _showGameView;   // false=场景视图, true=游戏视图
        private Rectangle _viewToggleRect;
        private float _animT;

        // 视图相机参数（透视）
        private const float Fov = 70f, Near = 0.1f, Far = 200f;
        // 复用同一个立方体网格（边长 6）与渲染器实例
        private readonly KFramework.MonoGameExtend.MeshRenderer _cubeRenderer = new KFramework.MonoGameExtend.MeshRenderer
        {
            Mesh = Mesh.CreateCube(6f),
            MeshColor = new Color(228, 232, 240),
        };

        public EditorScene()
        {
            for (int i = 0; i < _numBoxes.Length; i++)
            {
                _numBoxes[i] = new TextBox
                {
                    Multiline = false,
                    MaxLength = 12,
                    OverlayScale = 1f,
                    OverlayFontPx = 18f,
                    OverlayFontCss = "18px sans-serif",
                };
            }
        }

        public override void Update()
        {
            _animT += KTime.deltaTime;
            var mouse = Input_Mouse.Position;

            // 右键：在层级行上弹出上下文菜单（删除等）
            if (Input_Mouse.GetButtonDown(MouseButton.Right))
                OpenContextMenuAt(mouse);

            // 左键
            if (Input_Mouse.GetButtonDown(MouseButton.Left))
            {
                // 菜单打开时：先判定是否点中菜单项（命中则执行并关闭；点其它地方也关闭）
                if (_contextOpen)
                {
                    bool hitMenu = false;
                    for (int i = 0; i < _contextItems.Count; i++)
                    {
                        if (MenuRectFor(i).Contains(mouse)) { _contextItems[i].act(); hitMenu = true; break; }
                    }
                    _contextOpen = false;
                    if (hitMenu) return;
                }

                // 是否按在某个数值字段上（记录为待定：拖拽=改值，单击=聚焦输入）
                _pendingField = -1;
                for (int i = 0; i < _fields.Count; i++)
                {
                    if (_fields[i].rect.Contains(mouse)) { _pendingField = i; _dragStartX = mouse.X; _dragging = false; break; }
                }
                if (_pendingField == -1)
                {
                    BlurAllBoxes();   // 点字段以外的地方：取消输入焦点
                    foreach (var c in _clickables)
                        if (c.rect.Contains(mouse)) { c.act(); break; }
                }
            }

            // 按住左键：超过阈值判定为拖拽改值（并立即取消输入焦点，避免与键入冲突）
            if (Input_Mouse.GetButton(MouseButton.Left) && _pendingField >= 0)
            {
                if (Math.Abs(mouse.X - _dragStartX) > 3f)
                {
                    if (!_dragging) { _dragging = true; BlurAllBoxes(); }
                    _fields[_pendingField].apply(Input_Mouse.Delta.X);
                }
            }

            // 松开左键：未拖拽=聚焦该字段（进入键入模式），拖拽过=保持取消焦点
            if (Input_Mouse.GetButtonUp(MouseButton.Left))
            {
                if (_pendingField >= 0)
                {
                    if (_dragging) BlurAllBoxes();
                    else FocusBox(_pendingField);
                }
                _pendingField = -1;
                _dragging = false;
            }
        }

        private void FocusBox(int i)
        {
            for (int j = 0; j < _numBoxes.Length; j++)
                if (j == i) _numBoxes[j].Focus(); else _numBoxes[j].Blur();
        }

        private void BlurAllBoxes()
        {
            foreach (var b in _numBoxes) b.Blur();
        }

        // 在鼠标位置弹出上下文菜单（仅当落在某个层级行上、且不是 Main Camera 时提供删除）
        private void OpenContextMenuAt(Vector2 p)
        {
            EditorObject? target = null;
            foreach (var row in _hierarchyRows)
                if (row.rect.Contains(p)) { target = row.obj; break; }

            _contextItems.Clear();
            if (target != null && target.Type != EditorObjectType.Camera)
            {
                EditorObject captured = target;
                _contextItems.Add(("删除 Delete", () => DeleteObject(captured)));
                _contextPos = p;
                _contextOpen = true;
            }
            else
            {
                _contextOpen = false;
            }
        }

        private Rectangle MenuRectFor(int i)
        {
            const int w = 150, h = 26;
            return new Rectangle((int)_contextPos.X, (int)_contextPos.Y + i * h, w, h);
        }

        private void DeleteObject(EditorObject o)
        {
            _model.Objects.Remove(o);
            if (ReferenceEquals(_model.Selected, o)) _model.Selected = null;
        }

        public override void Draw()
        {
            var device = KSceneMgr.Game.GraphicsDevice;
            int W = device.Viewport.Width, H = device.Viewport.Height;
            var batch = KSceneMgr.SpriteBatch;
            var font = KDefaultRes.DefaultSpriteFont;
            var tex = KDefaultRes.DefaultTexture2D;

            _clickables.Clear();
            _fields.Clear();
            _hierarchyRows.Clear();

            // ---- 布局 ----
            const int toolbarH = 36;
            const int leftW = 240;     // 左侧：资源(Project)
            const int rightW = 300;    // 右侧：层级 + 检视
            int viewX = leftW;
            int viewW = W - leftW - rightW;
            int viewH = H - toolbarH;

            Rectangle projectRect = new Rectangle(0, toolbarH, leftW, viewH);
            Rectangle viewRect = new Rectangle(viewX, toolbarH, viewW, viewH);
            Rectangle hierRect = new Rectangle(W - rightW, toolbarH, rightW, viewH / 2);
            Rectangle inspRect = new Rectangle(W - rightW, toolbarH + viewH / 2, rightW, viewH - viewH / 2);

            // 窗口过小直接跳过视图
            if (viewW <= 0 || viewH <= 0) return;

            // 两个视图共用 Main Camera（位置 / 旋转）
            Vector3 camPos = _model.MainCamera.Position;
            Vector3 camRot = _model.MainCamera.Rotation;
            float aspect = viewRect.Width / (float)viewRect.Height;
            Matrix4x4 proj = Matrix4x4.CreatePerspectiveFieldOfView(Fov * MathF.PI / 180f, aspect, Near, Far);
            Matrix4x4 rotInv = Matrix4x4.CreateRotationZ(-camRot.Z) * Matrix4x4.CreateRotationY(-camRot.Y) * Matrix4x4.CreateRotationX(-camRot.X);
            Matrix4x4 view = Matrix4x4.CreateTranslation(-camPos.X, -camPos.Y, -camPos.Z) * rotInv;

            // ============ 2D 背景层（必须在 3D 之前）============
            batch.Begin();
            batch.Draw(tex, new Rectangle(0, 0, W, H), null, new Color(30, 32, 40));
            DrawPanel(batch, tex, projectRect, "资源 Project");
            DrawPanel(batch, tex, hierRect, "层级 Hierarchy");
            DrawPanel(batch, tex, inspRect, "检视 Inspector");
            batch.Draw(tex, viewRect, null, new Color(12, 14, 20));   // 视图背景（3D 在其上绘制）

            // 工具栏
            batch.Draw(tex, new Rectangle(0, 0, W, toolbarH), null, new Color(40, 44, 58));
            batch.DrawString(font, "KFramework 编辑器（类 Unity）  ·  FPS " + (int)KTime.realFps,
                new Vector2(12, 8), new Color(150, 200, 255));

            // 视图切换按钮
            _viewToggleRect = new Rectangle(W - 186, 6, 174, 24);
            batch.Draw(tex, _viewToggleRect, null, new Color(60, 90, 140));
            batch.DrawString(font, "视图: " + (_showGameView ? "游戏 Game" : "场景 Scene"),
                new Vector2(_viewToggleRect.X + 10, _viewToggleRect.Y + 3), new Color(225, 235, 255));
            _clickables.Add((_viewToggleRect, () => _showGameView = !_showGameView));

            DrawProject(batch, font, tex, projectRect);
            DrawHierarchy(batch, font, tex, hierRect);
            DrawInspector(batch, font, tex, inspRect, device);
            batch.End();

            // ============ 3D 视图层（硬件深度缓冲；viewport 裁剪到 viewRect）============
            Render3DView(device, viewRect, view, proj, tex, _showGameView);

            // ============ 2D 前景层（在 3D 之后：视图边框 + 标题 + 上下文菜单）============
            batch.Begin();
            string title = _showGameView ? "游戏 Game（Main Camera）" : "场景 Scene（Main Camera + 网格）";
            batch.Draw(tex, new Rectangle(viewRect.X - 1, viewRect.Y - 1, viewRect.Width + 2, 2), null, new Color(90, 150, 220));
            batch.Draw(tex, new Rectangle(viewRect.X - 1, viewRect.Y - 1, 2, viewRect.Height + 2), null, new Color(90, 150, 220));
            batch.Draw(tex, new Rectangle(viewRect.X - 1, viewRect.Y + viewRect.Height - 1, viewRect.Width + 2, 2), null, new Color(90, 150, 220));
            batch.Draw(tex, new Rectangle(viewRect.X + viewRect.Width - 1, viewRect.Y - 1, 2, viewRect.Height + 2), null, new Color(90, 150, 220));
            batch.DrawString(KDefaultRes.DefaultSpriteFont, title, new Vector2(viewRect.X + 6, viewRect.Y + 4), new Color(150, 220, 255));

            // 右键上下文菜单（最上层绘制）
            if (_contextOpen)
            {
                var m = Input_Mouse.Position;
                const int w = 150, h = 26;
                int menuH = _contextItems.Count * h;
                batch.Draw(tex, new Rectangle((int)_contextPos.X, (int)_contextPos.Y, w, menuH), null, new Color(30, 34, 44));
                batch.Draw(tex, new Rectangle((int)_contextPos.X, (int)_contextPos.Y, w, 2), null, new Color(90, 130, 200));
                batch.Draw(tex, new Rectangle((int)_contextPos.X, (int)_contextPos.Y, 2, menuH), null, new Color(90, 130, 200));
                batch.Draw(tex, new Rectangle((int)_contextPos.X + w - 2, (int)_contextPos.Y, 2, menuH), null, new Color(90, 130, 200));
                batch.Draw(tex, new Rectangle((int)_contextPos.X, (int)_contextPos.Y + menuH - 2, w, 2), null, new Color(90, 130, 200));
                for (int i = 0; i < _contextItems.Count; i++)
                {
                    Rectangle r = MenuRectFor(i);
                    if (r.Contains(m)) batch.Draw(tex, r, null, new Color(60, 90, 140));   // 悬停高亮
                    batch.DrawString(font, _contextItems[i].label, new Vector2(r.X + 12, r.Y + 4), new Color(230, 230, 240));
                }
            }
            batch.End();
        }

        /// <summary>
        /// 3D 视图渲染：把 viewport 裁剪到 viewRect（WebGL 视口原点在左下，故 y 需换算成 y 朝上），
        /// 开启硬件深度缓冲，先画地面网格 / 坐标轴再画立方体（立方体由 GPU 深度做遮挡），
        /// 最后复位视口与深度状态。
        /// </summary>
        private void Render3DView(GraphicsDevice device, Rectangle r, Matrix4x4 view, Matrix4x4 proj, Texture2D tex, bool useGameCam)
        {
            int glY = device.Viewport.Height - r.Y - r.Height;   // GL 视口 y 朝上
            var savedVp = device.Viewport;
            device.Viewport = new Viewport(r.X, glY, r.Width, r.Height);
            device.DepthStencilState = DepthStencilState.Default;
            if (!useGameCam) KFramework.MonoGameExtend.MeshRenderer.DrawGrid(device, view, proj, tex);
            foreach (EditorObject o in _model.Objects)
                if (o.Type == EditorObjectType.Cube && o.Visible)
                {
                    _cubeRenderer.LocalPosition = o.Position;
                    _cubeRenderer.MeshRotation = o.Rotation;
                    _cubeRenderer.MeshScale = o.Scale;
                    _cubeRenderer.RenderMesh(device, view, proj, tex);
                }
            device.DepthStencilState = DepthStencilState.None;
            device.Viewport = savedVp;
        }

        // ===== 面板外框 + 标题 =====
        private static void DrawPanel(SpriteBatch b, Texture2D tex, Rectangle r, string title)
        {
            b.Draw(tex, r, null, new Color(22, 24, 32));
            b.Draw(tex, new Rectangle(r.X, r.Y, r.Width, 18), null, new Color(40, 46, 64));
            b.Draw(tex, new Rectangle(r.X, r.Y, r.Width, 2), null, new Color(70, 90, 130));
            b.DrawString(KDefaultRes.DefaultSpriteFont, title, new Vector2(r.X + 6, r.Y + 1), new Color(170, 200, 255));
        }

        // ===== 层级 =====
        private void DrawHierarchy(SpriteBatch b, IFont font, Texture2D tex, Rectangle r)
        {
            int y = r.Y + 22;
            const int rowH = 22;
            foreach (EditorObject o in _model.Objects)
            {
                Rectangle row = new Rectangle(r.X + 4, y, r.Width - 8, rowH - 2);
                bool sel = ReferenceEquals(o, _model.Selected);
                if (sel) b.Draw(tex, row, null, new Color(60, 90, 140));
                b.DrawString(font, (sel ? "> " : "  ") + o.Name, new Vector2(r.X + 10, y + 2),
                    sel ? new Color(220, 235, 255) : new Color(200, 200, 210));
                EditorObject captured = o;
                _clickables.Add((row, () => _model.Selected = captured));
                _hierarchyRows.Add((row, captured));
                y += rowH;
            }
        }

        // ===== 检视（位置 / 旋转 / 缩放：拖拽改值 + 单击聚焦键入）=====
        private void DrawInspector(SpriteBatch b, IFont font, Texture2D tex, Rectangle r, GraphicsDevice device)
        {
            EditorObject? o = _model.Selected;
            int y = r.Y + 22;
            const int rowH = 24;
            if (o == null)
            {
                b.DrawString(font, "（未选中物体）", new Vector2(r.X + 8, y), new Color(160, 160, 170));
                return;
            }
            b.DrawString(font, o.Name + "  (" + o.Type + ")", new Vector2(r.X + 8, y), new Color(200, 220, 255));
            y += rowH + 6;

            int boxIdx = 0;
            y = InsGroup(b, font, tex, r, y, rowH, "位置 Position", o.Position, 0.05f, v => o.Position = v, ref boxIdx, device);
            y = InsGroup(b, font, tex, r, y, rowH, "旋转 Rotation", o.Rotation, 0.02f, v => o.Rotation = v, ref boxIdx, device);
            y = InsGroup(b, font, tex, r, y, rowH, "缩放 Scale", o.Scale, 0.01f, v => o.Scale = v, ref boxIdx, device);
        }

        // 画一组三轴字段：标题 + 三个数值框（每个对应一个 TextBox）。
        private int InsGroup(SpriteBatch b, IFont font, Texture2D tex, Rectangle r, int y, int rowH,
            string name, Vector3 value, float sens, Action<Vector3> set, ref int boxIdx, GraphicsDevice device)
        {
            b.DrawString(font, name, new Vector2(r.X + 8, y), new Color(255, 206, 110));
            y += rowH;

            for (int axis = 0; axis < 3; axis++)
            {
                Rectangle valRect = new Rectangle(r.X + r.Width - 150, y, 142, rowH - 4);
                b.Draw(tex, valRect, null, new Color(40, 44, 60));
                b.DrawString(font, "XYZ"[axis] + " ", new Vector2(r.X + r.Width - 146, y + 2), new Color(180, 200, 230));

                TextBox box = _numBoxes[boxIdx];
                box.Font = font;
                box.Location = new Point(valRect.X, valRect.Y);
                box.Size = new Size(valRect.Width, valRect.Height);

                float cur = axis == 0 ? value.X : axis == 1 ? value.Y : value.Z;
                if (!box.Focused)
                {
                    string txt = cur.ToString("0.00", CultureInfo.InvariantCulture);
                    if (box.Text != txt) box.Text = txt;
                }
                else if (float.TryParse(box.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out float f))
                {
                    Vector3 v = value;
                    if (axis == 0) v.X = f; else if (axis == 1) v.Y = f; else v.Z = f;
                    set(v);
                }

                // 拖拽改值（单击聚焦则由 TextBox 接管键入）
                int ax = axis; float s = sens; Vector3 snap = value;
                _fields.Add((valRect, dx =>
                {
                    Vector3 v = snap;
                    if (ax == 0) v.X = snap.X + dx * s;
                    else if (ax == 1) v.Y = snap.Y + dx * s;
                    else v.Z = snap.Z + dx * s;
                    set(v);
                }));

                // 引擎自绘文字 + 光标/选区（聚焦时由 DOM 覆盖层承接 IME 输入）
                box.DrawTextBox(b, device, font, box.Text, valRect, new Color(225, 225, 235),
                    box.SelectionStart, false, box.Focused, TextBox.DefaultPadLeft, box.CompositionString, box.SelectionStart, box.SelectionLength);

                boxIdx++;
                y += rowH;
            }
            return y + 4;
        }

        // ===== 资源（点击创建物体）=====
        private void DrawProject(SpriteBatch b, IFont font, Texture2D tex, Rectangle r)
        {
            int y = r.Y + 22;
            const int rowH = 24;
            b.DrawString(font, "资源（点击创建）", new Vector2(r.X + 8, y), new Color(255, 206, 110));
            y += rowH;
            DrawAssetRow(b, tex, r, y, rowH, "+ Cube", EditorObjectType.Cube); y += rowH;
            DrawAssetRow(b, tex, r, y, rowH, "+ 空物体", EditorObjectType.Empty); y += rowH;
        }

        private void DrawAssetRow(SpriteBatch b, Texture2D tex, Rectangle r, int y, int rowH, string label, EditorObjectType type)
        {
            Rectangle row = new Rectangle(r.X + 4, y, r.Width - 8, rowH - 2);
            b.Draw(tex, row, null, new Color(36, 40, 56));
            b.DrawString(KDefaultRes.DefaultSpriteFont, label, new Vector2(r.X + 10, y + 2), new Color(200, 210, 230));
            EditorObjectType t = type;
            _clickables.Add((row, () => _model.AddObject(t)));
        }
    }
}
