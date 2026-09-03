import { Ticker } from "pixi.js";
import { engine } from "../../app/getEngine";

export class KUpdateMgr
{
    //游戏统一 都用同一个 Ticker，否则设置 Update 优先级的时候，永远设置不正确。
    //Ticker.shared != engine().ticker 不一定相等。
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

