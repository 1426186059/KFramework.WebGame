using System.Collections.Generic;

namespace KFramework.MonoGame
{
    public class KUIRoot : Singleton<KUIRoot>
    {
        private readonly Dictionary<int, KCanvas> mLayerCanvasDic = new Dictionary<int, KCanvas>();

        public KCanvas GetCanvas(int nLayer)
        {
            KCanvas mCanvas = null;
            if (!mLayerCanvasDic.TryGetValue(nLayer, out mCanvas))
            {
                mCanvas = new KCanvas();
                mCanvas.Parent = KSceneMgr.DontDestroyOnLoadRoot;
                mLayerCanvasDic.Add(nLayer, mCanvas);
            }

            return mCanvas;
        }
    }
}
