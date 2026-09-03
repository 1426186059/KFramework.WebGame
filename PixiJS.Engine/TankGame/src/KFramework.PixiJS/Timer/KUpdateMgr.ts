import { Ticker } from "pixi.js";
import { engine } from "../../app/getEngine";

export class KUpdateMgr
{
    public static GetTicker():Ticker
    {
        return engine().ticker;
    }

    public static AddListener(func: ()=>void, context?: any, priority?: number):void
    {
        this.GetTicker().add(func, context, priority);
    }

    public static RemoveListener(func: ()=>void, context?: any):void
    {
        this.GetTicker().remove(func, context);
    }
}

