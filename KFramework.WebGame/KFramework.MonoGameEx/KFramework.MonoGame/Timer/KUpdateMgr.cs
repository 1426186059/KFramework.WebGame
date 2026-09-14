using System;
using System.Collections.Generic;

namespace KFramework.MonoGame
{
    public class KUpdateMgr : SingleTonMonoBehaviour<KUpdateMgr>
    {
        readonly List<Action> mapUpdateFunc = new List<Action>();

        public override void Update()
        {
            int nUpdateCount = mapUpdateFunc.Count;
            for (int i = 0; i < nUpdateCount; i++)
            {
                if (i < mapUpdateFunc.Count)
                {
                    mapUpdateFunc[i]();
                }
                else
                {
                    break;
                }
            }
        }

        public void AddListener(Action func)
        {
            if (mapUpdateFunc.IndexOf(func) == -1)
            {
                mapUpdateFunc.Add(func);
            }
        }

        public void RemoveListener(Action func)
        {
            this.mapUpdateFunc.Remove(func);
        }
    }
}
