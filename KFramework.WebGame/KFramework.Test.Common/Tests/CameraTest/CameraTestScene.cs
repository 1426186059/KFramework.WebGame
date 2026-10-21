using System;
using System.Collections.Generic;
using KFramework.MonoGame;
using KFramework.MonoGameExtend;
using KFramework.Test.Common.Tests;

namespace KFramework.Test.Common.Tests.CameraTest
{
    /// <summary>
    /// 相机专项测试（丰富版）：2D 正交 + 3D 透视 + 视锥裁剪，对标 Unity。
    /// <list type="bullet">
    ///   <item><description>2D 正交：相机矩阵渲染精灵环，越界精灵被 <see cref="KCamera.IsInFrustum"/> 判定后 activeSelf=false 真正剔除（不提交）。</description></item>
    ///   <item><description>3D 透视：手动透视投影把点云画到屏幕，绿点=视锥内 / 红点=视锥外，并绘制视锥线框；可实时调 FOV / 近远裁剪 / 旋转 / 移动。</description></item>
    ///   <item><description>矩阵对照：同一个世界点，(纯) World_To_Camera_Matrix 得到「相机空间坐标(单位=世界)」，World_To_Camera_Projection_Matrix 得到「屏幕像素坐标」——说明两者为何不同。</description></item>
    ///   <item><description>断言：2D 坐标往返、正交剔除、3D 透视 6 个用例全部 PASS/FAIL 上屏。</description></item>
    /// </list>
    /// </summary>
    public sealed class CameraTestScene : TestSceneBase
    {
        public override string Title => "相机测试：2D 正交 / 3D 透视 / 视锥裁剪（对标 Unity）";

        private readonly KCameraManager _manager = new();
        private KCamera _cam2d = null!;

        // 3D 透视相机：仅用于测试矩阵 / 视锥裁剪（看向 -Z），不参与 2D 渲染
        private readonly KCamera _cam3d = new()
        {
            Orthographic = false,
            FieldOfView = 60f,
            NearClipPlane = 0.1f,
            FarClipPlane = 1000f,
            LocalPosition = Vector3.Zero,
        };

        private readonly List<SpriteRenderer> _sprites = new();
        private readonly List<(Vector3 pos, bool expected, string name)> _cases = new();
        private readonly List<Vector3> _pts3d = new();

        private bool _perspective;          // false=2D 正交模式；true=3D 透视模式
        private bool _showFrustum = true;
        private bool _autoRotate;
        private bool _autoMove;
        private float _animT;

        private int _inCount, _total;
        private bool _roundTripOk;

        public override void LoadContent()
        {
            _cam2d = new KCamera
            {
                Target = null,
                Depth = 0,
                BackgroundColor = new Color(20, 24, 36),
                LocalPosition = new Vector3(960, 540, 0),   // 相机是中心点坐标系：放到设计分辨率中心，世界坐标=屏幕像素
                Orthographic = true,
                OrthographicSize = 540f,
            };

            // 一圈精灵：半径递增，越界者会被正交视锥剔除
            for (int i = 0; i < 60; i++)
            {
                var sr = new SpriteRenderer(KDefaultRes.DefaultTexture2D)
                {
                    Size = new Vector2(28, 28),
                    Color = Hsv(i * 360f / 60f, 0.6f, 1f),
                    Layer = 0,
                };
                float a = i / 60f * MathF.PI * 2f;
                float r = 350f + (i % 10) * 120f;
                sr.LocalPosition = new Vector3(960f + MathF.Cos(a) * r, 540f + MathF.Sin(a) * r, 0f);
                sr.Parent = SceneNodeRoot;
                _sprites.Add(sr);
            }

            // 3D 透视相机视锥裁剪用例（相机在原点看向 -Z）
            _cases.Add((new Vector3(0, 0, -5f), true, "前方(0,0,-5)"));
            _cases.Add((new Vector3(0, 0, 5f), false, "背后(0,0,5)"));
            _cases.Add((new Vector3(0, 0, -2000f), false, "超远(0,0,-2000)"));
            _cases.Add((new Vector3(0, 0, -0.05f), false, "过近(0,0,-0.05)"));
            _cases.Add((new Vector3(1000f, 0, -5f), false, "FOV外(1000,0,-5)"));
            _cases.Add((new Vector3(2.5f, 0, -5f), true, "FOV内(2.5,0,-5)"));

            // 3D 可视化点云：分布在相机前方，部分会落在 FOV 外 / 远面外
            var rnd = new Random(12345);
            float tanHalf = MathF.Tan(60f * MathF.PI / 180f / 2f);
            for (int i = 0; i < 48; i++)
            {
                float z = -rnd.Next(2, 60);                 // 前方 2~59
                float spread = -z * tanHalf * 1.7f;
                _pts3d.Add(new Vector3((rnd.NextSingle() * 2f - 1f) * spread,
                                       (rnd.NextSingle() * 2f - 1f) * spread, z));
            }

            _manager.Add(_cam2d);
        }

        public override void Update()
        {
            base.Update();
            _animT += KTime.deltaTime;
            if (_autoRotate) _cam3d.LocalRotation = _animT * 0.5f;
            if (_autoMove) _cam3d.LocalPosition = new Vector3(MathF.Sin(_animT * 0.6f) * 10f, MathF.Cos(_animT * 0.4f) * 7f, 0f);

            foreach (SpriteRenderer sr in _sprites)
            {
                sr.LocalRotation += 0.01f;
                // 正交视锥剔除：越界精灵直接置为非激活（真正不提交渲染）
                sr.activeSelf = _cam2d.IsInFrustum(sr.LocalPosition, 14f);
            }

            ClickButtons();
        }

        public override void Draw()
        {
            if (!_perspective)
            {
                // 2D 正交模式：相机管理器渲染精灵环（含视锥剔除）
                _manager.RenderAllScene(this);
            }
            else
            {
                // 3D 透视模式：自己铺底 + 手动透视投影点云
                SpriteBatch b = Batch;
                b.Begin();
                b.Draw(KDefaultRes.DefaultTexture2D,
                       new Rectangle(0, 0, Device.Viewport.Width, Device.Viewport.Height), null, new Color(14, 16, 26));
                Draw3D(b);
                b.End();
            }

            // 屏幕叠加层：返回 + 标题 + 按钮 + 报告
            SpriteBatch ui = Batch;
            ui.Begin();
            ui.Draw(KDefaultRes.DefaultTexture2D, new Rectangle(20, 16, 96, 34), null, new Color(38, 52, 92));
            ui.DrawString(Font, "← 返回", new Vector2(32, 22), new Color(170, 210, 255));
            ui.DrawString(Font, Title, new Vector2(128, 20), new Color(126, 200, 255));

            BeginButtons(64);
            AddUiButton(_perspective ? "模式: 3D透视" : "模式: 2D正交", () => _perspective = !_perspective);
            AddUiButton("视锥线框: " + (_showFrustum ? "开" : "关"), () => _showFrustum = !_showFrustum);
            if (_perspective)
            {
                AddUiButton("FOV +", () => _cam3d.FieldOfView = Math.Min(120f, _cam3d.FieldOfView + 5f));
                AddUiButton("FOV -", () => _cam3d.FieldOfView = Math.Max(10f, _cam3d.FieldOfView - 5f));
                AddUiButton("近裁剪 +", () => _cam3d.NearClipPlane = Math.Min(50f, _cam3d.NearClipPlane + 0.5f));
                AddUiButton("近裁剪 -", () => _cam3d.NearClipPlane = Math.Max(0.01f, _cam3d.NearClipPlane - 0.5f));
                AddUiButton("远裁剪 +", () => _cam3d.FarClipPlane = Math.Min(200f, _cam3d.FarClipPlane + 20f));
                AddUiButton("远裁剪 -", () => _cam3d.FarClipPlane = Math.Max(20f, _cam3d.FarClipPlane - 20f));
                AddUiButton(_autoRotate ? "旋转: 开" : "旋转: 关", () => _autoRotate = !_autoRotate);
                AddUiButton(_autoMove ? "移动: 开" : "移动: 关", () => _autoMove = !_autoMove);
            }
            else
            {
                AddUiButton("正交尺寸 +", () => _cam2d.OrthographicSize = Math.Min(900f, _cam2d.OrthographicSize + 40f));
                AddUiButton("正交尺寸 -", () => _cam2d.OrthographicSize = Math.Max(200f, _cam2d.OrthographicSize - 40f));
                AddUiButton(_cam2d.LocalRotation != 0f ? "相机旋转: 开" : "相机旋转: 关",
                            () => _cam2d.LocalRotation = _cam2d.LocalRotation == 0f ? 0.5f : 0f);
                AddUiButton("相机跟随", () => _cam2d.LocalPosition = new Vector3(960f + MathF.Sin(_animT) * 320f, 540f + MathF.Cos(_animT) * 220f, 0f));
                AddUiButton("相机复位", () => _cam2d.LocalPosition = new Vector3(960, 540, 0));
            }
            AddUiButton("重置", ResetAll);
            DrawButtons(ui);

            DrawReport(ui);
            ui.End();
        }

        // 基类要求的抽象成员；本场景在 Draw() 内自行管理多批渲染，故此处为空。
        protected override void DrawBody(SpriteBatch batch, Vector2 origin) { }

        // ===== 3D 透视可视化 =====

        private void Draw3D(SpriteBatch b)
        {
            int w = Device.Viewport.Width, h = Device.Viewport.Height;
            float aspect = w / (float)h;
            float fovRad = _cam3d.FieldOfView * MathF.PI / 180f;
            float tanH = MathF.Tan(fovRad / 2f);

            // 视锥线框：显示深度取「近→远之间一段」，便于观察
            if (_showFrustum)
            {
                float dFar = Math.Min(_cam3d.FarClipPlane, 45f);
                float halfH = tanH * dFar;
                float halfW = halfH * aspect;
                bool C(float vx, float vy, out float sx, out float sy) => ProjectView(vx, vy, -dFar, out sx, out sy);
                C(-halfW, -halfH, out float a1x, out float a1y);
                C(halfW, -halfH, out float a2x, out float a2y);
                C(halfW, halfH, out float a3x, out float a3y);
                C(-halfW, halfH, out float a4x, out float a4y);
                float cx = w / 2f, cy = h / 2f;   // 相机（视锥顶点）投影到屏幕中心
                DrawLine(b, cx, cy, a1x, a1y, new Color(120, 200, 255), 2);
                DrawLine(b, cx, cy, a2x, a2y, new Color(120, 200, 255), 2);
                DrawLine(b, cx, cy, a3x, a3y, new Color(120, 200, 255), 2);
                DrawLine(b, cx, cy, a4x, a4y, new Color(120, 200, 255), 2);
                DrawLine(b, a1x, a1y, a2x, a2y, new Color(120, 200, 255), 2);
                DrawLine(b, a2x, a2y, a3x, a3y, new Color(120, 200, 255), 2);
                DrawLine(b, a3x, a3y, a4x, a4y, new Color(120, 200, 255), 2);
                DrawLine(b, a4x, a4y, a1x, a1y, new Color(120, 200, 255), 2);
            }

            // 点云：先变换到视图空间，再用投影矩阵投到屏幕
            _inCount = 0;
            _total = _pts3d.Count;
            foreach (Vector3 p in _pts3d)
            {
                bool inside = _cam3d.IsInFrustum(p, 0f);
                if (inside) _inCount++;

                var view = ClipOf(p.X, p.Y, p.Z, _cam3d.World_To_Camera_Matrix);   // 世界 → 视图空间（w=1）
                var clip = ClipOf(view.x, view.y, view.z, _cam3d.ProjectionMatrix);
                if (clip.w <= 0f) continue;                            // 在相机背后，不画
                float ndcX = clip.x / clip.w;
                float ndcY = clip.y / clip.w;
                float sx = (ndcX * 0.5f + 0.5f) * w;
                float sy = (1f - (ndcY * 0.5f + 0.5f)) * h;
                if (sx < -20f || sx > w + 20f || sy < -20f || sy > h + 20f) continue;
                Color c = inside ? new Color(80, 230, 120) : new Color(230, 80, 80);
                b.Draw(KDefaultRes.DefaultTexture2D, new Rectangle((int)(sx - 3f), (int)(sy - 3f), 7, 7), null, c);
            }
        }

        // 视图空间点 (vx,vy,vz) → 屏幕像素；w<=0 返回 false
        private bool ProjectView(float vx, float vy, float vz, out float sx, out float sy)
        {
            sx = sy = 0f;
            var clip = ClipOf(vx, vy, vz, _cam3d.ProjectionMatrix);
            if (clip.w <= 0f) return false;
            float ndcX = clip.x / clip.w;
            float ndcY = clip.y / clip.w;
            sx = (ndcX * 0.5f + 0.5f) * Device.Viewport.Width;
            sy = (1f - (ndcY * 0.5f + 0.5f)) * Device.Viewport.Height;
            return true;
        }

        // 行向量约定：p × M（输入点 w=1）。返回 (x,y,z,w)。
        private static (float x, float y, float z, float w) ClipOf(float x, float y, float z, Matrix4x4 m)
        {
            return (
                x * m.M11 + y * m.M12 + z * m.M13 + m.M14,
                x * m.M21 + y * m.M22 + z * m.M23 + m.M24,
                x * m.M31 + y * m.M32 + z * m.M33 + m.M34,
                x * m.M41 + y * m.M42 + z * m.M43 + m.M44);
        }

        private static void DrawLine(SpriteBatch b, float x1, float y1, float x2, float y2, Color c, int thick = 2)
        {
            float dx = x2 - x1, dy = y2 - y1;
            float len = MathF.Sqrt(dx * dx + dy * dy);
            float ang = MathF.Atan2(dy, dx);
            b.Draw(KDefaultRes.DefaultTexture2D, new Rectangle(0, 0, (int)len, thick), null, c, ang, new Vector2(0f, thick / 2f));
        }

        // ===== 报告 / 状态 =====

        private void DrawReport(SpriteBatch ui)
        {
            float y = ContentTop + 6f;
            float x = 28f;

            if (!_perspective)
            {
                int total = _sprites.Count, inside = 0;
                foreach (SpriteRenderer sp in _sprites) if (sp.activeSelf) inside++;
                ui.DrawString(Font, $"2D 正交：精灵可见 {inside}/{total}（视锥外被剔除）  正交尺寸={_cam2d.OrthographicSize:0}  相机旋转={_cam2d.LocalRotation:0.00}",
                              new Vector2(x, y), new Color(150, 220, 255));
                y += 24f;
            }
            else
            {
                ui.DrawString(Font, $"3D 透视：点云可见 {_inCount}/{_total}   FOV={_cam3d.FieldOfView:0}°  近={_cam3d.NearClipPlane:0.0}  远={_cam3d.FarClipPlane:0}  旋转={_cam3d.LocalRotation:0.00}",
                              new Vector2(x, y), new Color(150, 220, 255));
                y += 24f;
            }

            // 2D 坐标往返
            Vector2 p = new Vector2(300, 200);
            Vector2 s = _cam2d.WorldToScreen(p);
            Vector2 p2 = _cam2d.ScreenToWorld(s);
            _roundTripOk = MathF.Abs(p2.X - p.X) < 0.5f && MathF.Abs(p2.Y - p.Y) < 0.5f;
            ui.DrawString(Font, $"2D 坐标往返 WorldToScreen→ScreenToWorld(300,200): {(_roundTripOk ? "PASS" : "FAIL")}  →({p2.X:0},{p2.Y:0})",
                          new Vector2(x, y), _roundTripOk ? new Color(80, 230, 120) : new Color(230, 80, 80));
            y += 24f;

            // World_To_Camera_Matrix vs World_To_Camera_Projection_Matrix 差异
            Matrix4x4 vm = _cam2d.World_To_Camera_Matrix;
            float vcx = p.X * vm.M11 + p.Y * vm.M12 + vm.M14;
            float vcy = p.X * vm.M21 + p.Y * vm.M22 + vm.M24;
            ui.DrawString(Font, $"同一点(300,200,0)：World_To_Camera_Matrix→相机空间({vcx:0},{vcy:0})（单位=世界）；World_To_Camera_Projection_Matrix→屏幕像素({s.X:0},{s.Y:0})",
                          new Vector2(x, y), new Color(255, 206, 110));
            y += 24f;

            // 3D 透视断言
            int pass = 0;
            foreach (var c in _cases)
            {
                bool got = _cam3d.IsInFrustum(c.pos, 0f);
                bool ok = got == c.expected;
                if (ok) pass++;
                ui.DrawString(Font, $"3D 透视 [{c.name}] 期望{(c.expected ? "内" : "外")} 实际{(got ? "内" : "外")}: {(ok ? "PASS" : "FAIL")}",
                              new Vector2(x, y), ok ? new Color(80, 230, 120) : new Color(230, 80, 80));
                y += 22f;
            }
            ui.DrawString(Font, $"3D 透视断言 {pass}/{_cases.Count} 通过   |   绿点=视锥内 / 红点=视锥外（3D 模式可见）",
                          new Vector2(x, y), new Color(150, 200, 255));
            y += 30f;

            // 已知坑
            ui.DrawString(Font, "已知坑：WorldToScreen/ScreenToWorld、ProjectionMatrix、IsInFrustum 都依赖 KSceneMgr.Game.GraphicsDevice.Viewport（取当前渲染目标尺寸）；",
                          new Vector2(x, y), new Color(255, 170, 120));
            y += 22f;
            ui.DrawString(Font, "若在离屏渲染期间或首帧视口未就绪时调用，会按 RT 尺寸算而偏差——生产代码应在调用处显式传入视口。",
                          new Vector2(x, y), new Color(255, 170, 120));
        }

        private void ResetAll()
        {
            _perspective = false; _autoRotate = false; _autoMove = false; _showFrustum = true;
            _cam3d.FieldOfView = 60f; _cam3d.NearClipPlane = 0.1f; _cam3d.FarClipPlane = 1000f;
            _cam3d.LocalRotation = 0f; _cam3d.LocalPosition = Vector3.Zero;
            _cam2d.OrthographicSize = 540f; _cam2d.LocalRotation = 0f; _cam2d.LocalPosition = new Vector3(960, 540, 0);
        }

        private static Color Hsv(float h, float s, float v)
        {
            h = (h % 360f + 360f) % 360f;
            float c = v * s, x = c * (1f - MathF.Abs((h / 60f % 2f) - 1f)), m = v - c;
            float r = 0, g = 0, b = 0;
            if (h < 60) (r, g, b) = (c, x, 0);
            else if (h < 120) (r, g, b) = (x, c, 0);
            else if (h < 180) (r, g, b) = (0, c, x);
            else if (h < 240) (r, g, b) = (0, x, c);
            else if (h < 300) (r, g, b) = (x, 0, c);
            else (r, g, b) = (c, 0, x);
            return new Color((byte)((r + m) * 255f), (byte)((g + m) * 255f), (byte)((b + m) * 255f), (byte)255);
        }
    }
}
