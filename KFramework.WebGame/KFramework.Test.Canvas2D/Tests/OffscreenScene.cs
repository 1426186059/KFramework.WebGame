using KFramework.MonoGame;
using KFramework.MonoGameExtend;

namespace KFramework.Test.Canvas2D.Tests
{

    /// <summary>
    /// 离屏渲染（渲染目标）：Canvas2D 的"帧缓冲"就是一块离屏 canvas —— 既能被画进去，也能当纹理被
    /// <c>drawImage</c> 采样，所以"把控件 / 世界层 / UI 层烘焙进 RT，再合成上屏"这类架构在本后端能跑通。
    /// <para>
    /// 本页覆盖传奇依赖的四类用法：① 画进 RT 再上屏（控件烘焙）；② 透明 RT 叠加（UI 层）；
    /// ③ <see cref="RenderTargetUsage.PreserveContents"/> 下切走再切回内容不丢（分层烘焙）；
    /// ④ 把 RT 拉伸铺满（<c>PresentToScreen</c>）。顺带在同一绑定内读回 RT 的像素做自检。
    /// </para>
    /// <para>
    /// 已知差异：没有 RT 级 MSAA（<c>preferredMultiSampleCount</c> 被忽略）、没有深度 / 模板附件、
    /// 不支持多渲染目标（MRT）。<see cref="RenderTargetUsage.DiscardContents"/> 的"绑定时自动清屏"由引擎完成。
    /// </para>
    /// <para>
    /// 本页自己管 Begin / End：切渲染目标必须在批次之外（会改视口与投影），故离屏与上屏分成两个阶段。
    /// </para>
    /// </summary>
    public sealed class OffscreenScene : DemoScene
    {
        public override string Title => "5) 离屏渲染（渲染目标）";

        protected override string Description
            => "渲染目标 = 一块离屏 canvas：既能画进去（SetRenderTarget），也能当纹理采样（上屏 / 当中间图）";

        private const int RtW = 320;
        private const int RtH = 200;

        private Texture2D? _tex;
        private RenderTarget2D? _opaque;   // ① 不透明底：控件烘焙
        private RenderTarget2D? _alpha;    // ② 透明分层：UI 层叠加
        private RenderTarget2D? _keep;     // ③ 内容保留：切走再切回
        private RenderTarget2D? _fill;     // ④ 拉伸铺满

        private int _frame;
        private float _previewBottom = 300f;
        private float _notesY = 600f;
        private string _probe = "读回 RT 像素：—";

        public override void Update() => _frame++;

        public override void Draw()
        {
            EnsureResources();

            // 离屏阶段：必须在 Begin / End 之外
            BakeOpaque(_opaque!);
            BakeAlpha(_alpha!);
            BakeKeep(_keep!);
            BakeFill(_fill!);

            // 上屏阶段
            SpriteBatch batch = Batch;
            batch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.PointClamp);
            DrawHeader(batch);
            DrawPreviews(batch);
            DrawFillDemo(batch);
            DrawNotes(batch, _notesY);
            DrawFooter(batch);
            batch.End();
        }

        private void EnsureResources()
        {
            _tex ??= MakeChecker(32, new Color(236, 214, 150), new Color(60, 74, 108));
            _opaque ??= NewTarget(RenderTargetUsage.DiscardContents);
            _alpha ??= NewTarget(RenderTargetUsage.DiscardContents);
            _keep ??= NewTarget(RenderTargetUsage.PreserveContents);
            _fill ??= NewTarget(RenderTargetUsage.DiscardContents);
        }

        private RenderTarget2D NewTarget(RenderTargetUsage usage)
            => new(Device, RtW, RtH, false, SurfaceFormat.Color, DepthFormat.None, 0, usage);

        /// <summary>
        /// ① 控件烘焙：清成实色 → 画文字 / 棋盘格 / 半透明块 → 切回画布。
        /// 并在<b>同一次绑定内</b>读回一个像素自检（DiscardContents 的目标一旦切走再绑回来会被自动清屏）。
        /// </summary>
        private void BakeOpaque(RenderTarget2D target)
        {
            Device.SetRenderTarget(target);
            Device.Clear(new Color(28, 40, 66));

            SpriteBatch batch = Batch;
            batch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.PointClamp);
            batch.DrawString(Font, "画在 RT 里", new Vector2(14f, 14f), new Color(255, 220, 140));
            batch.Draw(_tex!, new Vector2(14f, 54f), scale: new Vector2(3f, 3f));
            batch.Draw(KDefaultRes.DefaultTexture2D, new Rectangle(126, 118, 178, 62), null, new Color(220, 90, 90, 170));
            batch.End();

            Color got = Device.ReadPixel(16, 56);
            Device.SetRenderTarget(null);

            Color expected = new(236, 214, 150);
            bool ok = Math.Abs(got.R - expected.R) <= 2 && Math.Abs(got.G - expected.G) <= 2 && Math.Abs(got.B - expected.B) <= 2;
            _probe = $"读回 RT 像素：{(ok ? "✓" : "✗")}  RT(16,56) 期望 {expected} 实读 {got}";
        }

        /// <summary>② 透明分层：清成全透明（clearRect），只画半透明内容 —— 上屏时底下的亮块要能透出来。</summary>
        private void BakeAlpha(RenderTarget2D target)
        {
            Device.SetRenderTarget(target);
            Device.Clear(Color.Transparent);

            SpriteBatch batch = Batch;
            batch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.PointClamp);
            batch.Draw(KDefaultRes.DefaultTexture2D, new Rectangle(0, 0, RtW, 64), null, new Color(90, 200, 255, 170));
            batch.Draw(_tex!, new Vector2(126f, 40f), scale: new Vector2(4f, 4f), color: new Color(255, 255, 255, 220));
            batch.End();

            Device.SetRenderTarget(null);
        }

        /// <summary>
        /// ③ 内容保留：<see cref="RenderTargetUsage.PreserveContents"/> 下绑定时<b>不会</b>自动清屏，
        /// 于是"画一笔 → 切回画布 → 再绑回来 → 再画一笔"两笔都在（分层烘焙依赖这个语义）。
        /// 这里每帧叠一小块让肉眼能看出内容在累积，并定期清一次避免糊成一片。
        /// </summary>
        private void BakeKeep(RenderTarget2D target)
        {
            Device.SetRenderTarget(target);
            if (_frame % 120 == 0) Device.Clear(Color.Transparent);

            SpriteBatch batch = Batch;
            batch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.PointClamp);
            batch.Draw(KDefaultRes.DefaultTexture2D,
                new Rectangle((_frame % 6) * 46, 24 + (_frame % 4) * 38, 56, 34), null,
                (_frame & 1) == 0 ? new Color(120, 220, 140, 70) : new Color(255, 180, 90, 70));
            batch.End();

            Device.SetRenderTarget(null);
        }

        /// <summary>④ 拉伸铺满：与传奇的 <c>PresentToScreen</c> 同一个动作 —— 把整块 RT 画到更大的矩形上。</summary>
        private void BakeFill(RenderTarget2D target)
        {
            Device.SetRenderTarget(target);
            Device.Clear(new Color(16, 22, 36));

            SpriteBatch batch = Batch;
            batch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.PointClamp);
            batch.DrawString(Font, "RT 320×200 → 拉伸上屏", new Vector2(14f, 14f), new Color(180, 220, 255));
            for (int i = 0; i < 6; i++)
            {
                batch.Draw(_tex!, new Vector2(16f + i * 48f, 56f + (i % 3) * 34f), scale: new Vector2(1.5f, 1.5f),
                    color: new Color(120 + i * 20, 200 - i * 20, 255, 235));
            }
            batch.End();

            Device.SetRenderTarget(null);
        }

        /// <summary>三块 RT 并排上屏；②是透明 RT，所以底下先铺一块亮底，透出来才算对。</summary>
        private void DrawPreviews(SpriteBatch batch)
        {
            float w = Math.Clamp((Device.Viewport.Width - 56f - 48f) / 3f, 180f, 260f);
            float h = w * RtH / RtW;
            const float gapX = 24f;
            const float y = 122f;

            (RenderTarget2D Rt, string Label)[] items =
            [
                (_opaque!, "① 画进 RT 再上屏（控件烘焙）"),
                (_alpha!, "② 透明 RT 叠加（UI 层）"),
                (_keep!, "③ PreserveContents：切走再切回内容还在"),
            ];

            for (int i = 0; i < items.Length; i++)
            {
                float x = 28f + i * (w + gapX);
                DrawLine(batch, items[i].Label, x, y - 32f, new Color(180, 220, 255));

                // 亮底：② 是透明 RT，透过它必须看得见这块底
                batch.Draw(KDefaultRes.DefaultTexture2D,
                    new Rectangle((int)x - 2, (int)y - 2, (int)w + 4, (int)h + 4), null, new Color(120, 150, 200));
                batch.Draw(items[i].Rt, new Rectangle((int)x, (int)y, (int)w, (int)h), null, Color.White);
            }

            _previewBottom = y + h;
        }

        private void DrawFillDemo(SpriteBatch batch)
        {
            float y = DrawLine(batch, "④ 拉伸铺满（PresentToScreen）：RT 320×200 → 480×220 上屏",
                28f, _previewBottom + 32f, new Color(180, 220, 255));

            batch.Draw(_fill!, new Rectangle(28, (int)y + 4, 480, 220), null, Color.White);
            _notesY = y + 236f;

            float ty = DrawLine(batch, _probe, 536f, y + 10f, new Color(255, 206, 110));
            ty = DrawLine(batch, "绑着 RT 时 ReadPixel → getImageData 读的就是那块离屏 canvas", 536f, ty, new Color(150, 165, 195));
            DrawLine(batch, "（读回要在同一次绑定内：DiscardContents 的目标切走再绑会被自动清屏）", 536f, ty, new Color(150, 165, 195));
        }

        private void DrawNotes(SpriteBatch batch, float y)
        {
            y = DrawLine(batch, "绑定 / 上屏：SetRenderTarget(rt) → 画 → SetRenderTarget(null) → 再把 rt 当纹理 Draw",
                28f, y, new Color(150, 165, 195));
            y = DrawLine(batch, "已知差异：无 RT 级 MSAA（忽略 preferredMultiSampleCount）、无深度 / 模板、无 MRT；投影随目标尺寸自动切换",
                28f, y, new Color(255, 206, 110));
            DrawLine(batch, "传奇依赖的三类用法都在本页：①控件烘焙上屏 ②UI 层透明叠加 ③分层烘焙时切走再切回内容保留",
                28f, y, new Color(150, 165, 195));
        }
    }

}
