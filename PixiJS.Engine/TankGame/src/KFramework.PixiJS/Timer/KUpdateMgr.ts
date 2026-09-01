import { Ticker } from "pixi.js";
import { engine } from "../../app/getEngine";

export class KUpdateMgr
{
    public static AddListener(func: ()=>void):void
    {
        engine().ticker.add(func);
    }

    public static RemoveListener(func: ()=>void):void
    {
        engine().ticker.remove(func);
    }
}

