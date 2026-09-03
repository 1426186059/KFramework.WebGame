import { Container } from "pixi.js";
import { KUpdateMgr } from "./KUpdateMgr";
import { IDisposable } from "../Tool/IDisposable";
import { KTime } from "../GameEngine/KTime";
import { PixiTool } from "../Tool/PixiTool";

export class KTimer implements IDisposable
{
    private unscaled:boolean = false;
    private loop:number = 0;
    private duration:number = 0;
    private time:number = 0;
    private func: (()=>void) | null = null;
    private go:Container | null = null;

    private readonly UpdateFunc =  this.Update.bind(this); 
    
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
        KUpdateMgr.AddListener(this.UpdateFunc);
    }

    public Stop():void
    {
        KUpdateMgr.RemoveListener(this.UpdateFunc);
    }

    public Dispose(): void 
    {
        this.func = null;
        this.go = null;
    }

    private Update():void
    {
        if (!PixiTool.isAlive(this.go))
        {
            this.Stop();
            return;
        }
        
        //console.log("KTimer Update: " + KTime.deltaTime);
        let delta:number = this.unscaled ? KTime.unscaledDeltaTime : KTime.deltaTime;
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
