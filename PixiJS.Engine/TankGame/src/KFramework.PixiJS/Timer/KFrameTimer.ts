import { KUpdateMgr } from "./KUpdateMgr";

export class KFrameTimer
{
	private func:(()=>void) | null = null;
	private loop:number = 0;
	private nNowFrame:number = 0;
	private nSumFrame:number = 0;
	private running:boolean = false;
	
	public static New(func:()=>void, nFrameCount:number, loop:number = 1):KFrameTimer
	{
		var o = new KFrameTimer();
		o.func = func;
		o.nSumFrame = nFrameCount;
		o.loop = loop;
		o.nNowFrame = nFrameCount;
		o.running = false;
		return o;
	}

	public Start():void
	{
		KUpdateMgr.AddListener(this.Update);
		this.running = true;
	}

	public Stop():void
	{
		this.running = false;
		KUpdateMgr.RemoveListener(this.Update);
	}

	public Update():void
	{
		if (!this.running)
		{
			return;
		}

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