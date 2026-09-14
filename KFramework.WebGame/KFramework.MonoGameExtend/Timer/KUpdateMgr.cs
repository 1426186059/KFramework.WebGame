using System;
using System.Collections.Generic;

namespace KFramework.MonoGameExtend
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

        // 兼容 PixiJS 的 KUpdateMgr.AddListener(func, context, priority) 写法：
        // C# 的 Action 委托本身已捕获 this，故 context 仅作占位，行为等同单参版本。
        public void AddListener(Action func, object context)
        {
            AddListener(func);
        }

        public void RemoveListener(Action func)
        {
            this.mapUpdateFunc.Remove(func);
        }

        public void RemoveListener(Action func, object context)
        {
            RemoveListener(func);
        }
    }
}
