import { Ticker } from "pixi.js";
import { engine } from "../../app/getEngine";

export class KUpdateMgr
{
    public static AddListener(func: ()=>void, context?: any):void
    {
        engine().ticker.add(func, context);
    }

    public static RemoveListener(func: ()=>void, context?: any):void
    {
        engine().ticker.remove(func, context);
    }
}

