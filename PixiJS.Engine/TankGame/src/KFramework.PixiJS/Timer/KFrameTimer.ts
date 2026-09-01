import { KUpdateMgr } from "./KUpdateMgr";

export class KFrameTimer
{
	private func:(()=>void) | null = null;
	private loop:number = 0;
	private nNowFrame:number = 0;
	private nSumFrame:number = 0;
	private UpdateFunc:(()=>void) | null = null; 
	
	public static New(func:()=>void, nFrameCount:number, loop:number = 1):KFrameTimer
	{
		var o = new KFrameTimer();
		o.func = func;
		o.nSumFrame = nFrameCount;
		o.loop = loop;
		o.nNowFrame = nFrameCount;
		o.UpdateFunc = o.Update.bind(o);
		return o;
	}

    public Start():void
    {
        if(this.UpdateFunc)
        {
            KUpdateMgr.AddListener(this.UpdateFunc);
        }
    }

    public Stop():void
    {
        if(this.UpdateFunc)
        {
            KUpdateMgr.RemoveListener(this.UpdateFunc);
        }
    }

	public Update():void
	{
		this.nNowFrame = this.nNowFrame - 1;
		if (this.nNowFrame <= 0)
		{
			if(this.func)
			{
				this.func();
			}

			if (this.loop > 0)
			{
				this.loop = this.loop - 1;
			}

			if (this.loop == 0)
			{
				this.Stop();
			}
			else
			{
				this.nNowFrame = this.nSumFrame;
			}
		}
	}
}