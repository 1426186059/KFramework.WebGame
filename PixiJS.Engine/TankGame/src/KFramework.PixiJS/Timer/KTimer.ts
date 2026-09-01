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

    private UpdateFunc:(()=>void) | null = null; 
    
    public static New(go:Container, func:()=>void, duration:number, loop:number = 1, unscaled:boolean = false):KTimer
    {
        var o = new KTimer();
        o.go = go;
        o.func = func;
        o.duration = duration;
        o.time = duration;
        o.loop = loop;
        o.unscaled = unscaled;
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

    public Dispose(): void 
    {
        this.func = null;
        this.go = null;
    }

    private Update():void
    {
        if (this.go == null)
        {
            this.Stop();
            return;
        }

        console.log("KTimer Update: " + Ticker.shared.deltaTime);
        let delta:number = this.unscaled ? Ticker.shared.elapsedMS / 1000 : Ticker.shared.elapsedMS * Ticker.shared.speed / 1000;
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
