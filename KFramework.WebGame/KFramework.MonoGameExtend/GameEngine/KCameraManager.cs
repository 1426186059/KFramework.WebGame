using System;
using System.Collections.Generic;
using KFramework.MonoGame;

namespace KFramework.MonoGameExtend
{
    /// <summary>
    /// 相机管理器（对标 Unity 的 Camera 渲染队列）：维护一组 <see cref="KCamera"/>，
    /// 按 <see cref="KCamera.Depth"/> 排序后逐个渲染。每个相机自己合自己的批、画到自己的目标，
    /// 这就是 Unity「自动合批的发动机 = 相机」的本引擎实现。
    /// <para>
    /// 用法：把若干相机 Add 进来（离屏相机 Depth 给负值先画，屏幕相机 Depth=0 后画并合成），
    /// 每帧调一次 <see cref="RenderAllScene"/> / <see cref="RenderAll"/> 即可，无需手写 Begin / End / SetRenderTarget。
    /// </para>
    /// </summary>
    public sealed class KCameraManager
    {
        private readonly List<KCamera> _cameras = new List<KCamera>();

        /// <summary>已登记的相机（只读）。</summary>
        public IReadOnlyList<KCamera> Cameras => _cameras;

        public void Add(KCamera camera)
        {
            if (camera != null && !_cameras.Contains(camera)) _cameras.Add(camera);
        }

        public void Remove(KCamera camera) => _cameras.Remove(camera);

        public void Clear() => _cameras.Clear();

        /// <summary>按 Depth 升序渲染每个相机（同一个 draw 回调，各自合批到自己的目标）。</summary>
        public void RenderAll(Action<SpriteBatch> draw)
        {
            _cameras.Sort((a, b) => a.Depth.CompareTo(b.Depth));
            foreach (KCamera camera in _cameras) camera.Render(draw);
        }

        /// <summary>按 Depth 升序渲染：每个相机用自身 <see cref="KCamera.CullingMask"/> 层剔除后渲染整个场景。</summary>
        public void RenderAllScene(KSceneBase scene)
        {
            _cameras.Sort((a, b) => a.Depth.CompareTo(b.Depth));
            foreach (KCamera camera in _cameras) camera.RenderScene(scene);
        }
    }
}
