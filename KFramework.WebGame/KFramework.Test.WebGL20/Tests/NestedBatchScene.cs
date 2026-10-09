using System;
using KFramework.MonoGame;
using KFramework.MonoGameExtend;

namespace KFramework.Test.WebGL20.Tests
{
    /// <summary>
    /// 批处理嵌套（<see cref="SpriteNestedBatch"/>）：在一个批里用<b>切换材质</b>的方式分段绘制，
    /// 段与段之间按调用顺序、各自一次提交，退一层（<c>End</c>）后自动切回外层材质。
    /// <para>
    /// 本页画 4 簇互相叠压的光斑，依次用：外层材质（NonPremultiplied）→ 发光材质（Additive）→
    /// 再嵌一层（换效果的灰度段）→ <b>退层后回到外层材质</b>（最后一簇应当与第一簇同款）。
    /// 空格切换「嵌套 / 不嵌套」：嵌套 = 4 段 = 4 次 DrawCall（画面能看到叠加发光与灰度）；
    /// 不嵌套 = 全部用外层材质 = 1 段 = 1 次 DrawCall（画面变平，36 个精灵合并成一批）。
    /// </para>
    /// <para>交互：<b>空格</b>切换嵌套 / 不嵌套。</para>
    /// </summary>
    public sealed class NestedBatchScene : DemoScene
    {
        public override string Title => "11) 批处理嵌套（SpriteNestedBatch：用材质切段）";

        protected override string Description
            => "同一个批内嵌套 Begin 换材质：外层 / 发光段 / 再嵌一层灰度段 / 退层回到外层 —— 段间按调用顺序，各自一次提交";

        /// <summary>每簇的光斑数（3×3，彼此叠压）。</summary>
        private const int BlobsPerCluster = 9;

        private const float ClusterTop = 150f;
        private const float ClusterSize = 260f;

        private SpriteNestedBatch? _nested;
        private Texture2D? _blob;

        private Material? _outer;
        private Material? _glow;
        private Material? _gray;

        private bool _nestedOn = true;
        private int _lastDrawCalls;
        private int _lastSegments;

        public override void Update()
        {
            if (Input_KeyBoard.GetKeyDown(Keys.Space)) _nestedOn = !_nestedOn;
        }

        public override void LoadContent()
        {
            _blob = MakeBlob(96);
            _nested = new SpriteNestedBatch(Device);

            _outer = new Material();                                                        // 默认（NonPremultiplied）
            _glow = new Material { Blend = BlendState.Additive };                           // 换成叠加混合
            _gray = new Material { Effect = Device.CreateShaderEffect(GrayscaleFragment) }; // 换成不同效果
        }

        public override void Draw()
        {
            SpriteBatch batch = Batch;

            DrawClusters();

            batch.Begin();
            DrawHeader(batch);
            DrawFooter(batch);
            DrawReadouts(batch);
            batch.End();
        }

        /// <summary>用一个批画出 4 簇：中间两簇用嵌套的 Begin / End 换材质，最后一簇验证"退层后已切回外层材质"。</summary>
        private void DrawClusters()
        {
            SpriteNestedBatch nested = _nested!;

            long before = Device.Metrics.DrawCount;

            nested.Begin(_outer);
            DrawCluster(nested, 0, new Color(90, 170, 250));            // ① 外层材质

            if (_nestedOn)
            {
                nested.Begin(_glow);                                     // ② 叠一层：发光（Additive）
                DrawCluster(nested, 1, new Color(255, 180, 90));

                nested.Begin(_gray);                                     // ③ 再叠一层：灰度效果
                DrawCluster(nested, 2, new Color(255, 120, 200));
                nested.End();                                            // 退回发光层

                nested.End();                                            // 退回外层
                DrawCluster(nested, 3, new Color(90, 170, 250));         // ④ 又回到外层材质
            }

            nested.End();                                                // 最外层 End：整批结束
            _lastDrawCalls = (int)(Device.Metrics.DrawCount - before);
            _lastSegments = _nestedOn ? 4 : 1;
        }

        /// <summary>画一簇互相叠压的光斑（3×3、彼此重叠约 40%），让"不同混合 / 不同效果"看得见。</summary>
        private void DrawCluster(SpriteNestedBatch nested, int index, Color color)
        {
            Texture2D blob = _blob!;

            float columnWidth = (Device.Viewport.Width - 56f) / 4f;
            float cx = 28f + columnWidth * (index + 0.5f);
            float cy = ClusterTop + ClusterSize * 0.5f;

            const int cols = 3;
            float step = ClusterSize / (cols + 1f) * 0.9f;

            for (int i = 0; i < BlobsPerCluster; i++)
            {
                int c = i % cols;
                int r = i / cols;
                var center = new Vector2(cx + (c - 1) * step, cy + (r - 1) * step);
                float scale = 1f + 0.12f * MathF.Sin(i * 1.7f);

                nested.DrawCentered(blob, center, color, 0f, scale);
            }
        }

        private void DrawReadouts(SpriteBatch batch)
        {
            string[] labels = _nestedOn
                ? ["① 外层材质", "② 嵌套 Begin(Additive)", "③ 再嵌一层（灰度效果）", "④ End 退层之后：外层材质"]
                : ["① 外层材质", "② 同一个材质", "③ 同一个材质", "④ 同一个材质"];

            float columnWidth = (Device.Viewport.Width - 56f) / 4f;
            for (int i = 0; i < labels.Length; i++)
            {
                DrawLine(batch, labels[i], 28f + columnWidth * i + 6f, ClusterTop + ClusterSize + 12f,
                         i == 3 ? new Color(150, 220, 180) : new Color(200, 210, 230));
            }

            float readoutY = Device.Viewport.Height - 108f;
            DrawLine(batch, $"嵌套 = {(_nestedOn ? "开（4 段）" : "关（1 段）")}　→　本批 DrawCall = {_lastDrawCalls}　段数 = {_lastSegments}　精灵数 = {BlobsPerCluster * 4}",
                28f, readoutY, new Color(120, 200, 160));
            DrawLine(batch, "嵌套 = 嵌套 Begin 把一批切成若干段：Begin 时冲刷当前段（用当前材质），End 时冲刷内层段并重新下发外层材质",
                28f, readoutY + 20f, new Color(255, 206, 110));
            DrawLine(batch, $"FPS: {KTime.realFps:0.0}　空格切换嵌套 / 不嵌套：同样 36 个精灵，不嵌套 1 次 DC、嵌套 4 次（每段各自提交）",
                28f, readoutY + 40f, new Color(150, 165, 195));
        }

        /// <summary>程序化生成一张径向渐变的白色光斑（中心不透明 → 边缘透明），叠加混合时才有"发光"感。</summary>
        private Texture2D MakeBlob(int size)
        {
            var texture = Device.CreateTexture(size, size);
            var data = new byte[size * size * 4];
            float half = size / 2f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - half) / half;
                    float dy = (y + 0.5f - half) / half;
                    float a = Math.Clamp(1f - MathF.Sqrt(dx * dx + dy * dy), 0f, 1f);
                    a *= a;   // 平方一下，边缘更柔

                    int i = (y * size + x) * 4;
                    data[i] = 255;
                    data[i + 1] = 255;
                    data[i + 2] = 255;
                    data[i + 3] = (byte)(a * 255f);
                }
            }

            texture.SetData(data);
            return texture;
        }

        /// <summary>
        /// 灰度片元着色器：只演示"物料不同"——同样一张光斑，这一层被转成灰度。
        /// 自定义片元的可用 varying 就是统一接口那三个（vTexCoord / vColor / vInstanceID）+ uTexture。
        /// </summary>
        private const string GrayscaleFragment = @"#version 300 es
precision highp float;
in vec2 vTexCoord;
in vec4 vColor;
uniform sampler2D uTexture;
out vec4 fragColor;
void main()
{
    vec4 c = texture(uTexture, vTexCoord) * vColor;
    float g = dot(c.rgb, vec3(0.299, 0.587, 0.114));
    fragColor = vec4(g, g, g, c.a);
}";
    }
}
