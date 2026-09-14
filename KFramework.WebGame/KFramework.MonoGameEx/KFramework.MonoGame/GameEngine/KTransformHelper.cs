using Microsoft.Xna.Framework;

namespace KFramework.MonoGame
{
    public static class KTransformHelper
    {
        public static void Do_Update_AllChildList(KTransform t)
        {
            foreach (var v in t.ChildList)
            {
                v.Update();
                Do_Update_AllChildList(v);
            }
        }

        public static void Do_Draw_AllChildList(KTransform t)
        {
            foreach (var v in t.ChildList)
            {
                if (v is KDrawable)
                {
                    v.Draw();
                }
                else
                {
                    Do_Draw_AllChildList(v);
                }
            }
        }

    }
}
