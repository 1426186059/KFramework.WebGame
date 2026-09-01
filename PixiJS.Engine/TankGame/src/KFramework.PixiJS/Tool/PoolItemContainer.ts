import { Container } from "pixi.js";
import { IPoolItem } from "./IPoolItem";

export class PoolItemContainer extends Container implements IPoolItem
{
    public Reset()
    {
        throw "NotImplementedException: Reset";
    }
    
    public Dispose()
    {
        throw "NotImplementedException: Dispose";
    }
}