using KFramework.MonoGame;
using KFramework.MonoGameExtend;
using System;
using System.Collections.Generic;

namespace KFramework.Editor
{
    /// <summary>
    /// 类 Unity 编辑器主场景：屏幕被划分为 5 块——
    /// 顶部工具栏、左侧层级(Hierarchy)、中央场景视图 + 游戏视图、右侧检视(Inspector) + 资源(Project)。
    /// 默认场景里有一个 Cube 物体，位于相机正前方便于「在屏幕正中」观察；
    /// 在检视面板里用 Unity 式「拖拽改值」编辑其 位置/旋转/缩放，场景与游戏视图实时更新。
    /// </summary>
    public sealed class EditorScene : KSceneBase
    {
        private readonly EditorSceneModel _model = new();
        private readonly List<(Rectangle rect, Action act)> _clickables = new();
        private readonly List<(Rectangle rect, Action<float> apply)> _fields = new();
        private readonly List<(Rectangle rect, EditorObject obj)> _hierarchyRows = new();
        private int _dragField = -1;
        private bool _contextOpen;
        private Vector2 _contextPos;
        private readonly List<(string label, Action act)> _contextItems = new();
        private float _animT;

        // 视图相机参数（透视）
        private const float Fov = 50f, Near = 0.1f, Far = 200f;

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
                    if (hitMenu) return;          // 命中菜单项：本帧不再处理其它点击
                }

                // 优先判定：是否按在某个数值字段上（开始拖拽改值）
                bool hitField = false;
                for (int i = 0; i < _fields.Count; i++)
                {
                    if (_fields[i].rect.Contains(mouse)) { _dragField = i; hitField = true; break; }
                }
                if (!hitField)
                {
                    foreach (var c in _clickables)
                        if (c.rect.Contains(mouse)) { c.act(); break; }
                    _dragField = -1;
                }
            }
            // 拖拽中：按横向位移改值（Unity 风格 scrub）
            if (_dragField >= 0 && Input_Mouse.GetButton(MouseButton.Left))
                _fields[_dragField].apply(Input_Mouse.Delta.X);
            if (Input_Mouse.GetButtonUp(MouseButton.Left)) _dragField = -1;
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

            batch.Begin();

            // 背景
            batch.Draw(tex, new Rectangle(0, 0, W, H), null, new Color(30, 32, 40));

            // ---- 布局 ----
            const int toolbarH = 36;
            const int hierW = 220;
            const int inspW = 300;
            int centerX = hierW;
            int centerW = W - hierW - inspW;
            int centerH = H - toolbarH;

            Rectangle hierRect = new Rectangle(0, toolbarH, hierW, centerH);
            Rectangle inspRect = new Rectangle(W - inspW, toolbarH, inspW, centerH / 2);
            Rectangle projRect = new Rectangle(W - inspW, toolbarH + centerH / 2, inspW, centerH - centerH / 2);
            Rectangle sceneRect = new Rectangle(centerX, toolbarH, centerW / 2, centerH);
            Rectangle gameRect = new Rectangle(centerX + centerW / 2, toolbarH, centerW / 2, centerH);

            DrawPanel(batch, tex, hierRect, "层级 Hierarchy");
            DrawPanel(batch, tex, inspRect, "检视 Inspector");
            DrawPanel(batch, tex, projRect, "资源 Project");

            DrawView(batch, tex, sceneRect, "场景 Scene（固定相机）", false);
            DrawView(batch, tex, gameRect, "游戏 Game（Main Camera）", true);

            // 工具栏
            batch.Draw(tex, new Rectangle(0, 0, W, toolbarH), null, new Color(40, 44, 58));
            batch.DrawString(font, "KFramework 编辑器（类 Unity）  ·  FPS " + (int)KTime.realFps,
                new Vector2(12, 8), new Color(150, 200, 255));

            DrawHierarchy(batch, font, tex, hierRect);
            DrawInspector(batch, font, tex, inspRect);
            DrawProject(batch, font, tex, projRect);

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

        // ===== 面板外框 + 标题 =====
        private static void DrawPanel(SpriteBatch b, Texture2D tex, Rectangle r, string title)
        {
            b.Draw(tex, r, null, new Color(22, 24, 32));
            b.Draw(tex, new Rectangle(r.X, r.Y, r.Width, 18), null, new Color(40, 46, 64));
            b.Draw(tex, new Rectangle(r.X, r.Y, r.Width, 2), null, new Color(70, 90, 130));
            b.DrawString(KDefaultRes.DefaultSpriteFont, title, new Vector2(r.X + 6, r.Y + 1), new Color(170, 200, 255));
        }

        // ===== 视图（场景 / 游戏）=====
        private void DrawView(SpriteBatch b, Texture2D tex, Rectangle r, string title, bool useGameCam)
        {
            b.Draw(tex, r, null, new Color(12, 14, 20));   // 视图背景

            Vector3 camPos = useGameCam ? _model.MainCamera.Position : Vector3.Zero;
            Vector3 camRot = useGameCam ? _model.MainCamera.Rotation : Vector3.Zero;
            foreach (EditorObject o in _model.Objects)
                if (o.Type == EditorObjectType.Cube && o.Visible)
                    CubeView.Draw(b, r, Fov, Near, Far, camPos, camRot, o.Position, o.Rotation, o.Scale, tex);

            // 边框
            b.Draw(tex, new Rectangle(r.X - 1, r.Y - 1, r.Width + 2, 2), null, new Color(90, 150, 220));
            b.Draw(tex, new Rectangle(r.X - 1, r.Y - 1, 2, r.Height + 2), null, new Color(90, 150, 220));
            b.Draw(tex, new Rectangle(r.X - 1, r.Y + r.Height - 1, r.Width + 2, 2), null, new Color(90, 150, 220));
            b.Draw(tex, new Rectangle(r.X + r.Width - 1, r.Y - 1, 2, r.Height + 2), null, new Color(90, 150, 220));
            b.DrawString(KDefaultRes.DefaultSpriteFont, title, new Vector2(r.X + 6, r.Y + 4), new Color(150, 220, 255));
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

        // ===== 检视（位置 / 旋转 / 缩放，拖拽改值）=====
        private void DrawInspector(SpriteBatch b, IFont font, Texture2D tex, Rectangle r)
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

            y = AddVector3Field(b, tex, r, y, rowH, "位置 Position", o.Position, 0.05f,
                v => o.Position = v);
            y = AddVector3Field(b, tex, r, y, rowH, "旋转 Rotation", o.Rotation, 0.02f,
                v => o.Rotation = v);
            y = AddVector3Field(b, tex, r, y, rowH, "缩放 Scale", o.Scale, 0.01f,
                v => o.Scale = v);
        }

        // 画一组三轴的数值字段；每个字段支持横向拖拽改值
        private int AddVector3Field(SpriteBatch b, Texture2D tex, Rectangle r, int y, int rowH,
            string name, Vector3 value, float sens, Action<Vector3> set)
        {
            b.DrawString(KDefaultRes.DefaultSpriteFont, name, new Vector2(r.X + 8, y), new Color(255, 206, 110));
            y += rowH;

            RegAxis(b, tex, r, y, rowH, "X", value.X, sens, x => { Vector3 v = value; v.X = x; set(v); });
            y += rowH;
            RegAxis(b, tex, r, y, rowH, "Y", value.Y, sens, x => { Vector3 v = value; v.Y = x; set(v); });
            y += rowH;
            RegAxis(b, tex, r, y, rowH, "Z", value.Z, sens, x => { Vector3 v = value; v.Z = x; set(v); });
            y += rowH;
            return y + 4;
        }

        private void RegAxis(SpriteBatch b, Texture2D tex, Rectangle r, int y, int rowH, string axis, float cur, float sens, Action<float> set)
        {
            Rectangle valRect = new Rectangle(r.X + r.Width - 150, y, 142, rowH - 4);
            b.Draw(tex, valRect, null, new Color(40, 44, 60));
            b.DrawString(KDefaultRes.DefaultSpriteFont, axis + "   " + cur.ToString("0.00"),
                new Vector2(r.X + r.Width - 144, y + 2), new Color(220, 220, 230));
            float c = cur;
            _fields.Add((valRect, dx => set(c + dx * sens)));
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
