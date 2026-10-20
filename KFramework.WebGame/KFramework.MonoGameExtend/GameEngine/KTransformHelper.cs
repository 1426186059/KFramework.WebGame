using KFramework.MonoGame;

namespace KFramework.MonoGameExtend
{
    public static class KTransformHelper
    {
        public static void Do_Update_AllChildList(KTransform t)
        {
            var mEntry = t.ChildList.First;
            while(mEntry != null)
            {
                var next = mEntry.Next;
                if (mEntry.Value.IsDispose)
                {
                    if (mEntry.List != null)
                    {
                        t.ChildList.Remove(mEntry);
                    }
                }
                else
                {
                    if (mEntry.Value.activeInHierarchy)
                    {
                        mEntry.Value.Update();
                        if (mEntry.Value.IsDispose)
                        {
                            if (mEntry.List != null)
                            {
                                t.ChildList.Remove(mEntry);
                            }
                        }
                        else
                        {
                            Do_Update_AllChildList(mEntry.Value);
                        }
                    }
                }
                mEntry = next;
            }
        }
        
        public static void Do_Draw_AllChildList(KTransform t)
        {
            Do_Draw_AllChildList(t, ~0u);
        }

        /// <summary>
        /// 带层剔除的绘制遍历（v2）：只绘制 <paramref name="layerMask"/> 命中的 <see cref="KDrawable"/> 节点。
        /// 命中规则：<c>(1u &lt;&lt; node.Layer) &amp; layerMask != 0</c>。
        /// 子节点始终递归（每个节点按自身 Layer 独立判定，父节点被剔除不会连带隐藏子节点，与 Unity 一致）。
        /// 供 <see cref="KCamera.RenderRoot"/> 调用：每个相机只渲染自己 CullingMask 内的层。
        /// </summary>
        public static void Do_Draw_AllChildList(KTransform t, uint layerMask)
        {
            var mEntry = t.ChildList.First;
            while (mEntry != null)
            {
                var next = mEntry.Next;
                if (mEntry.Value.IsDispose)
                {
                    if (mEntry.List != null)
                    {
                        t.ChildList.Remove(mEntry);
                    }
                }
                else
                {
                    if (mEntry.Value.activeInHierarchy)
                    {
                        KTransform node = mEntry.Value;
                        // 层剔除：本节点是否在相机可见层内（只影响是否绘制本节点，不影响递归）。
                        if ((layerMask & (1u << (int)node.Layer)) != 0 && node is KDrawable)
                        {
                            node.Draw();
                        }

                        if (node.IsDispose)
                        {
                            if (mEntry.List != null)
                            {
                                t.ChildList.Remove(mEntry);
                            }
                        }
                        else
                        {
                            Do_Draw_AllChildList(node, layerMask);
                        }
                    }
                }
                mEntry = next;
            }
        }

    }
}
