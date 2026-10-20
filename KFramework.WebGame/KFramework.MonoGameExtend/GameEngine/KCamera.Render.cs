using KFramework.MonoGameExtend;
using KFramework.MonoGame;
using System;
using System.Collections.Generic;

// KCamera 的渲染分部：自动合批 + 层剔除 + 材质动态合批。主体（字段/屏幕适配/矩阵）见 KCamera.cs。
public partial class KCamera : KTransform
{
    /// <summary>
    /// 自动合批渲染：切到 <see cref="Target"/> →（可选）清屏 → Begin（相机矩阵）→ PreDraw → draw 回调 → End。
    /// 一次调用即一个相机的一整段批，天然解决「切 Target 自动 Begin/End」。
    /// </summary>
    public void Render(Action<SpriteBatch> draw)
    {
        var device = KSceneMgr.Game.GraphicsDevice;
        device.SetRenderTarget(Target);
        if (ClearOnRender) device.Clear(BackgroundColor);

        var batch = KSceneMgr.SpriteBatch;
        Material mat = Material ?? (_defaultMaterial ??= new Material());
        batch.Begin(mat, SortMode, ViewMatrix);

        PreDraw?.Invoke(batch);
        draw(batch);

        batch.End();
    }

    /// <summary>渲染一个根节点（带层剔除 + 自动合批）：相机只画 <see cref="CullingMask"/> 命中的 <see cref="MeshRenderer"/> 节点。</summary>
    public void RenderRoot(KTransform root) => RenderAuto(root);

    /// <summary>渲染一个场景（带层剔除 + 自动合批）：等价于 <see cref="RenderRoot"/>(scene.SceneNodeRoot)。</summary>
    public void RenderScene(KSceneBase scene) => RenderRoot(scene.SceneNodeRoot);

    /// <summary>
    /// 相机自动合批渲染（核心发动机）：顺序扫描整棵子树里的所有 <see cref="MeshRenderer"/> 批贡献节点
    /// （非 KDrawable、active、命中 CullingMask），先按 <see cref="RenderQueue"/> 分桶，桶内再按队列类型分类排序，
    /// 然后顺序扫描——相邻且 Material 相同的节点合进同一个 Begin/End 批；Material 变化则自动补 End + Begin。
    /// 即「每个物体自带材质（对标 Unity），相机据此分类、动态合批」。KDrawable（KCanvas 这类自己开批的容器）被跳过。
    /// </summary>
    public void RenderAuto(KTransform root)
    {
        var device = KSceneMgr.Game.GraphicsDevice;
        device.SetRenderTarget(Target);
        if (ClearOnRender) device.Clear(BackgroundColor);

        var batch = KSceneMgr.SpriteBatch;
        Matrix4x4 view = ViewMatrix;

        // 1) 收集批贡献节点（MeshRenderer，按层剔除）
        var nodes = new List<MeshRenderer>();
        CollectBatchContributors(root, CullingMask, nodes);

        // 2) 分类排序：先按 RenderQueue 分桶（不同队列强制断批）；同一队列内再按队列类型走不同优先级
        //    —— 对标 Unity：不透明队列(Geometry/AlphaTest/Background) 按「世界 Z 深度 优先、SortingOrder 兜底」，
        //       透明队列(Transparent/Overlay) 按「SortingOrder 优先、世界 Z 深度 兜底」。
        nodes.Sort((a, b) =>
        {
            int c = a.RenderQueue.CompareTo(b.RenderQueue);
            if (c != 0) return c;

            if (a.RenderQueue >= RenderQueue.Transparent)   // 透明队列：SortingOrder 在先，Z 在后
            {
                c = a.SortingOrder.CompareTo(b.SortingOrder);
                if (c != 0) return c;
                return a.WorldZ.CompareTo(b.WorldZ);
            }

            // 不透明队列：Z 在先，SortingOrder 在后
            c = a.WorldZ.CompareTo(b.WorldZ);
            if (c != 0) return c;
            return a.SortingOrder.CompareTo(b.SortingOrder);
        });

        // 前置绘制（如把离屏 RT 合成回屏幕）：在独立的前置批里完成
        if (PreDraw != null)
        {
            batch.Begin(Material ?? (_defaultMaterial ??= new Material()), SortMode, view);
            PreDraw(batch);
            batch.End();
        }

        // 3) 顺序扫描：相邻同材质 → 一个批；材质变 → 自动断批重开
        Material? currentMat = null;
        bool opened = false;
        for (int i = 0; i < nodes.Count; i++)
        {
            MeshRenderer node = nodes[i];
            Material mat = node.Material ?? Material ?? (_defaultMaterial ??= new Material());
            if (!opened || !ReferenceEquals(mat, currentMat))
            {
                if (opened) batch.End();
                batch.Begin(mat, SortMode, view);
                currentMat = mat;
                opened = true;
            }
            node.Draw();
        }
        if (opened) batch.End();
    }

    private static void CollectBatchContributors(KTransform root, uint layerMask, List<MeshRenderer> outList)
    {
        var mEntry = root.ChildList.First;
        while (mEntry != null)
        {
            var next = mEntry.Next;
            if (!mEntry.Value.IsDispose && mEntry.Value.activeInHierarchy)
            {
                KTransform node = mEntry.Value;
                // KDrawable 自己开批（不能嵌在相机已开的批里），跳过其自身 Draw，但递归进去收集它的 MeshRenderer 子节点（嵌套分类）。
                if ((layerMask & (1u << (int)node.Layer)) != 0 && node is MeshRenderer mr)
                {
                    outList.Add(mr);
                }
                CollectBatchContributors(node, layerMask, outList);
            }
            mEntry = next;
        }
    }
}
