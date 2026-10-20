using KFramework.MonoGame;
using KFramework.MonoGameExtend;
using System;
using System.Collections.Generic;

namespace KFramework.Test.Common.Tests
{
    /// <summary>
    /// KCamera 自动合批演示（v1 自动 Begin/End/SetRenderTarget + v2 层剔除 + 材质自动合批）。
    /// <list type="bullet">
    ///   <item><description>所有渲染组件继承 <b>MeshRenderer</b>（对标 Unity Renderer），每个物体自带 <see cref="MeshRenderer.Material"/>；</description></item>
    ///   <item><description><b>世界相机</b>（Depth=-10，Target=离屏 RT，CullingMask=层1）扫描所有 MeshRenderer，按世界 Z 排序后<b>相邻同材质自动合批</b>——80 个同材质精灵 → 1 个批；</description></item>
    ///   <item><description><b>屏幕相机</b>（Depth=0，Target=画布，CullingMask=层0）把离屏 RT 合成面板（单独材质，Z=10 最后画）与 UI 精灵分开，材质变化自动断批重开；</description></item>
    ///   <item><description>场景的 Draw 里<b>没有任何</b> Begin/End/SetRenderTarget：全部由 KCameraManager 按 Depth 驱动，证明「切 Target 自动 Begin/End + 按材质动态合批」。</description></item>
    ///   <item><description>把某个精灵的 Layer 改掉，它就会在对应相机里消失——直观看到层剔除。</description></item>
    /// </list>
    /// </summary>
    public sealed class CameraBatchScene : KSceneBase
    {
        private RenderTarget2D? _rt;
        private readonly KCameraManager _manager = new KCameraManager();

        private readonly List<SpriteRenderer> _worldSprites = new();
        private readonly List<SpriteRenderer> _uiSprites = new();
        private int _frame;

        public override void LoadContent()
        {
            var device = KSceneMgr.Game.GraphicsDevice;
            _rt = new RenderTarget2D(device, 512, 512, false,
                SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.DiscardContents);

            // 三种材质：80 个世界精灵共享 matWorld（相邻同材质 → 自动合批成 1 个批）；
            // UI 精灵共享 matUI；合成面板用 matComposite（与 UI 不同材质 → 自动断批重开）。
            var matWorld = new Material();
            var matUI = new Material();
            var matComposite = new Material();

            // —— 世界层（Layer=1）：画进离屏 RT ——
            for (int i = 0; i < 80; i++)
            {
                var sr = new SpriteRenderer(KDefaultRes.DefaultTexture2D)
                {
                    Size = new Vector2(22, 22),
                    Color = Hsv((i * 360f / 80f), 0.6f, 1f),
                    Layer = 1,
                    Material = matWorld,
                };
                float a = i / 80f * MathF.PI * 2f;
                float r = 60f + (i % 10) * 18f;
                sr.LocalPosition = new Vector2(256 + MathF.Cos(a) * r, 256 + MathF.Sin(a) * r);
                sr.Parent = SceneNodeRoot;
                _worldSprites.Add(sr);
            }

            // —— UI 层（Layer=0）：直接画到屏幕 ——
            for (int i = 0; i < 10; i++)
            {
                var sr = new SpriteRenderer(KDefaultRes.DefaultTexture2D)
                {
                    Size = new Vector2(36, 36),
                    Color = new Color(150, 200, 255),
                    Layer = 0,
                    Material = matUI,
                };
                sr.LocalPosition = new Vector2(40 + i * 52, 690f);
                sr.Parent = SceneNodeRoot;
                _uiSprites.Add(sr);
            }

            // 把离屏 RT 当面板合成回屏幕：用一个 SpriteRenderer 节点（Layer=0，单独材质 matComposite，
            // Z=10 保证最后画）。相机自动合批时它和 UI 不同材质 → 分成两段批，直观看到「材质变自动断批」。
            var composite = new SpriteRenderer(_rt)
            {
                Size = new Vector2(512, 512),
                Color = Color.White,
                Layer = 0,
                Material = matComposite,
            };
            composite.LocalPosition = new Vector3(40, 120, 10f);
            composite.Parent = SceneNodeRoot;

            // 离屏相机：目标=RT，只渲染层1，清屏成透明
            var camWorld = new KCamera
            {
                Target = _rt,
                Depth = -10,
                CullingMask = 1u << 1,
                BackgroundColor = new Color(0, 0, 0, 0),
                ClearOnRender = true,
                ViewMatrixOverride = Matrix4x4.Identity,   // 离屏：世界坐标=像素
            };

            // 屏幕相机：目标=画布，只渲染层0，清屏成深色；PreDraw 只画说明文字（前置批）
            var camUI = new KCamera
            {
                Target = null,
                Depth = 0,
                CullingMask = 1u << 0,
                BackgroundColor = new Color(18, 22, 34),
                ClearOnRender = true,
                ViewMatrixOverride = Matrix4x4.Identity,   // 屏幕：世界坐标=像素
                PreDraw = batch =>
                {
                    batch.DrawString(KDefaultRes.DefaultSpriteFont,
                        "KCamera 自动合批：离屏相机(Depth=-10,层1)→RT，屏幕相机(Depth=0,层0)→画布",
                        new Vector2(40, 40), new Color(126, 200, 255));
                    batch.DrawString(KDefaultRes.DefaultSpriteFont,
                        "左侧面板=离屏 RT 内容(同材质80精灵→自动合批成1批)；下方蓝块=UI 层(层0)。改精灵 Layer 即被对应相机剔除。",
                        new Vector2(40, 72), new Color(180, 195, 220));
                },
            };

            _manager.Add(camWorld);
            _manager.Add(camUI);
        }

        public override void Update()
        {
            base.Update();
            _frame++;
            // 世界层精灵旋转 + 轻微呼吸，证明离屏内容是动态的
            for (int i = 0; i < _worldSprites.Count; i++)
            {
                _worldSprites[i].LocalRotation = _frame * 0.01f + i;
            }
            // UI 层精灵做简单位移
            for (int i = 0; i < _uiSprites.Count; i++)
            {
                _uiSprites[i].LocalPosition = new Vector2(
                    40 + i * 52,
                    690f + MathF.Sin(_frame * 0.05f + i) * 10f);
            }
        }

        public override void Draw()
        {
            // 没有任何 Begin / End / SetRenderTarget：全部由相机驱动
            _manager.RenderAllScene(this);
        }

        public override void Dispose()
        {
            (_rt as IDisposable)?.Dispose();
            _rt = null;
            base.Dispose();
        }

        private static Color Hsv(float h, float s, float v)
        {
            h = (h % 360f + 360f) % 360f;
            float c = v * s;
            float x = c * (1f - MathF.Abs((h / 60f % 2f) - 1f));
            float m = v - c;
            float r = 0, g = 0, b = 0;
            if (h < 60) (r, g, b) = (c, x, 0);
            else if (h < 120) (r, g, b) = (x, c, 0);
            else if (h < 180) (r, g, b) = (0, c, x);
            else if (h < 240) (r, g, b) = (0, x, c);
            else if (h < 300) (r, g, b) = (x, 0, c);
            else (r, g, b) = (c, 0, x);
            return new Color(
                (byte)((r + m) * 255f), (byte)((g + m) * 255f), (byte)((b + m) * 255f), (byte)255);
        }
    }
}
