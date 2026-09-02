import { Container } from "pixi.js";
import { IPoolItem } from "./IPoolItem";

export class PoolItemContainer extends Container implements IPoolItem
{
    OnPoolPop(): void 
    {
        throw new Error("Method not implemented.");
    }

    OnPoolPush(): void 
    {
        throw new Error("Method not implemented.");
    }
    
    public Dispose()
    {
        throw "NotImplementedException: Dispose";
    }
}