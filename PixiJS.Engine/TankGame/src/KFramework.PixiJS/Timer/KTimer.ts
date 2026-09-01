import { Container, Ticker } from "pixi.js";
import { KUpdateMgr } from "./KUpdateMgr";
import { IDisposable } from "../Tool/IDisposable";

export class KTimer implements IDisposable
{
    private unscaled:boolean = false;
    private loop:number = 0;
    private duration:number = 0;
    private time:number = 0;
    private func: (()=>void) | null = null;
    private go:Container | null = null;
    
    public static New(go:Container, func:()=>void, duration:number, loop:number = 1, unscaled:boolean = false):KTimer
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

    public Start():void
    {
        KUpdateMgr.AddListener(this.Update);
    }

    public Stop():void
    {
        KUpdateMgr.RemoveListener(this.Update);
    }

    public Dispose(): void 
    {
        this.func = null;
        this.go = null;
    }

    private Update(_time: Ticker):void
    {
        if (this.go == null)
        {
            this.Stop();
            return;
        }

        let delta:number = this.unscaled ? _time.elapsedMS / 1000 : _time.deltaTime;
        this.time = this.time - delta;

        if (this.time <= 0)
        {
            if(this.func)
            {
                this.func();
            }

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
