using System;

namespace KFramework.MonoGame
{
    public class KTimer
    {
        bool unscaled = false;
        int loop = 0;
        float duration = 0f;
        float time = 0f;
        Action func;
        public KTransform go;

        public static KTimer New(KTransform go, Action func, float duration, int loop = 1, bool unscaled = false)
        {
            var o = new KTimer();
            o.go = go;
            o.func = func;
            o.duration = duration;
            o.time = duration;
            o.loop = loop;
            o.unscaled = unscaled;
            return o;
        }

        public void Start()
        {
            KUpdateMgr.Instance.AddListener(this.Update);
        }

        public void Stop()
        {
            KUpdateMgr.Instance.RemoveListener(this.Update);
        }
        
        private void Update()
        {
            if (go == null)
            {
                this.Stop();
                return;
            }

            float delta = this.unscaled ? KTime.unscaledDeltaTime : KTime.deltaTime;
            this.time = this.time - delta;

            if (this.time <= 0)
            {
                this.func();

                if (this.loop > 0)
                {
                    this.loop = this.loop - 1;
                    this.time = this.time + this.duration;
                }

                if (this.loop == 0)
                {
                    this.Stop();
                }
                else if (this.loop < 0)
                {
                    this.time = this.time + this.duration;
                }
            }
        }
    }
}
