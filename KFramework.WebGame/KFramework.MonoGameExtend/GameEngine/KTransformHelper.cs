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
                        if (mEntry.Value is KDrawable)
                        {
                            mEntry.Value.Draw();
                        }

                        if (mEntry.Value.IsDispose)
                        {
                            if (mEntry.List != null)
                            {
                                t.ChildList.Remove(mEntry);
                            }
                        }
                        else
                        {
                            Do_Draw_AllChildList(mEntry.Value);
                        }
                    }
                }
                mEntry = next;
            }
        }

    }
}
