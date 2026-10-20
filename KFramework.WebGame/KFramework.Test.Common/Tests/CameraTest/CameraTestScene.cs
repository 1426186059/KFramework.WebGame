using KFramework.MonoGame;
using KFramework.MonoGameExtend;
using System;
using System.Collections.Generic;

namespace KFramework.Test.Common.Tests.CameraTest
{
    /// <summary>
    /// 相机专项测试：2D 正交相机 + 3D 透视相机 + 视锥裁剪，对标 Unity。
    /// <list type="bullet">
    ///   <item><description>2D 正交：世界坐标 ↔ 屏幕坐标往返；正交视锥剔除（环上越界的精灵被 activeSelf=false 真正剔除，不提交）。</description></item>
    ///   <item><description>3D 透视：构造一台看向 -Z 的透视相机，用 <see cref="KCamera.IsInFrustum(KFramework.MonoGame.Vector3, float)"/> 断言「前方 / 背后 / 超远 / 过近 / FOV 外 / FOV 内」各用例。</description></item>
    ///   <item><description>坐标系：相机是中心原点、屏幕是左上角（见 <see cref="KCamera.World_To_Camera_Matrix"/>）。</description></item>
    /// </list>
    /// </summary>
    public sealed class CameraTestScene : KSceneBase
    {
        private readonly KCameraManager _manager = new();
        private KCamera _cam2d = null!;

        // 仅用于测试透视矩阵 / 视锥裁剪，不参与渲染
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

        public override void LoadContent()
        {
            _cam2d = new KCamera
            {
                Target = null,
                Depth = 0,
                BackgroundColor = new Color(20, 24, 36),
                LocalPosition = new Vector3(960, 540, 0),   // 相机是中心原点：放到设计分辨率中心，世界坐标=屏幕像素
                Orthographic = true,
                OrthographicSize = 540f,
                PreDraw = DrawReport,
            };

            // 一圈精灵：半径递增，越界者会被正交视锥剔除
            for (int i = 0; i < 48; i++)
            {
                var sr = new SpriteRenderer(KDefaultRes.DefaultTexture2D)
                {
                    Size = new Vector2(30, 30),
                    Color = Hsv(i * 360f / 48f, 0.6f, 1f),
                    Layer = 0,
                };
                float a = i / 48f * MathF.PI * 2f;
                float r = 150f + (i % 6) * 130f;
                sr.LocalPosition = new Vector3(960f + MathF.Cos(a) * r, 540f + MathF.Sin(a) * r, 0f);
                sr.Parent = SceneNodeRoot;
                _sprites.Add(sr);
            }

            // 3D 透视相机视锥裁剪用例（相机在原点看向 -Z）
            _cases.Add((new Vector3(0, 0, -5f), true, "前方 (0,0,-5)"));
            _cases.Add((new Vector3(0, 0, 5f), false, "背后 (0,0,5)"));
            _cases.Add((new Vector3(0, 0, -2000f), false, "超远 (0,0,-2000)"));
            _cases.Add((new Vector3(0, 0, -0.05f), false, "过近 (0,0,-0.05)"));
            _cases.Add((new Vector3(1000f, 0, -5f), false, "FOV 外 (1000,0,-5)"));
            _cases.Add((new Vector3(2.5f, 0, -5f), true, "FOV 内 (2.5,0,-5)"));

            _manager.Add(_cam2d);
        }

        public override void Update()
        {
            base.Update();
            for (int i = 0; i < _sprites.Count; i++)
            {
                _sprites[i].LocalRotation = i * 0.02f;
                // 正交视锥裁剪：越界精灵直接置为非激活（真正不提交渲染）
                _sprites[i].activeSelf = _cam2d.IsInFrustum(_sprites[i].LocalPosition, 15f);
            }
        }

        public override void Draw() => _manager.RenderAllScene(this);

        private void DrawReport(SpriteBatch batch)
        {
            var font = KDefaultRes.DefaultSpriteFont;
            float y = 36f;
            batch.DrawString(font, "相机测试：2D 正交 + 3D 透视 + 视锥裁剪（对标 Unity）", new Vector2(40, y), new Color(120, 220, 255));
            y += 30f;

            // 2D 坐标往返
            Vector2 p = new Vector2(123, 456);
            Vector2 s = _cam2d.WorldToScreen(p);
            Vector2 p2 = _cam2d.ScreenToWorld(s);
            bool rt = MathF.Abs(p2.X - p.X) < 0.01f && MathF.Abs(p2.Y - p.Y) < 0.01f;
            batch.DrawString(font, $"2D 坐标往返 WorldToScreen→ScreenToWorld: {(rt ? "PASS" : "FAIL")}  结果=({p2.X:0},{p2.Y:0})", new Vector2(40, y), rt ? new Color(0, 255, 0) : new Color(255, 0, 0));
            y += 26f;

            // 2D 正交剔除
            bool cull2d = !_cam2d.IsInFrustum(new Vector3(10000, 0, 0), 1f);
            batch.DrawString(font, $"2D 正交剔除 远处(10000,0) 应被剔除: {(cull2d ? "PASS" : "FAIL")}", new Vector2(40, y), cull2d ? new Color(0, 255, 0) : new Color(255, 0, 0));
            y += 26f;

            // 3D 透视用例
            int pass = 0;
            foreach (var c in _cases)
            {
                bool got = _cam3d.IsInFrustum(c.pos, 0f);
                bool ok = got == c.expected;
                if (ok) pass++;
                batch.DrawString(font, $"3D 透视 [{c.name}] 期望{(c.expected ? "内" : "外")} 实际{(got ? "内" : "外")} : {(ok ? "PASS" : "FAIL")}", new Vector2(40, y), ok ? new Color(0, 255, 0) : new Color(255, 0, 0));
                y += 22f;
            }
            batch.DrawString(font, $"3D 透视用例通过 {pass}/{_cases.Count}　|　环上越界精灵已被视锥剔除（不提交）", new Vector2(40, y), new Color(150, 200, 255));
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
