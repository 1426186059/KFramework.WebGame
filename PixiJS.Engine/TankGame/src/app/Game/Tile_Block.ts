import { Bounds, Sprite, Ticker } from "pixi.js";
import { TankLevel } from "./TankLevel";
import { TileBase } from "./TileBase";
import { engine } from "../getEngine";

export class Tile_Block extends TileBase
{
    public readonly mSprite:Sprite = new Sprite();

    constructor(mTankLevel: TankLevel, x:number, y:number)
    {
        super(mTankLevel, x, y);
        this.addChild(this.mSprite);
    }

    public override Collider2DZone():Bounds
    {
        let bounds:Bounds = this.mSprite.getBounds();
        //console.log("Collider2DZone 111 bounds: " + bounds.toString());
        return bounds;
    }
    
    public update(_time: Ticker) 
    {
        this.showBounds();
    }

}