import { Ticker } from "pixi.js";
import { KUpdateMgr } from "../Timer/KUpdateMgr";

export class KTime 
{
    public static get deltaTime(): number
    {
        return KUpdateMgr.GetTicker().deltaMS / 1000;
    }
    
    public static get unscaledDeltaTime(): number
    {
        return KUpdateMgr.GetTicker().elapsedMS / 1000;
    }

    // 最稳妥的做法是自己维护一个累加器，而不是直接去读 lastTime
    // public static get time(): number
    // {
    //     return Ticker.shared.lastTime / 1000.0;
    // }

    // /** 不受 timeScale 影响的时间（秒） */
    // public static get unscaledTime(): number
    // {
    //     return Ticker.shared.lastTime / 1000;
    // }
}
