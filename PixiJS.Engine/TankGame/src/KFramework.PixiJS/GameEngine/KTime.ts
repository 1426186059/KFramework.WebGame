import { Ticker } from "pixi.js";

export class KTime 
{
    public static get deltaTime(): number
    {
        return Ticker.shared.deltaMS / 1000;
    };
    
    public static get unscaledDeltaTime(): number
    {
        return Ticker.shared.elapsedMS / 1000;
    };

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
