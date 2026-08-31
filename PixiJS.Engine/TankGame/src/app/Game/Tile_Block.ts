import { Bounds, Sprite, Ticker } from "pixi.js";
import { TankLevel } from "./TankLevel";
import { TileBase } from "./Tile";
import { engine } from "../getEngine";

export class Tile_Block extends TileBase
{
    public readonly mSprite:Sprite = new Sprite();

    constructor(mTankLevel: TankLevel, x:number, y:number)
    {
        super(mTankLevel, x, y);
        this.addChild(this.mSprite);
        this.resize();
    }

    public override resize():void
    {
        super.resize();
    }

    public override Collider2DZone():Bounds
    {
        let bounds:Bounds = this.mSprite.getBounds();
        console.log("Collider2DZone 111 bounds: " + bounds.toString());
        return bounds;
    }
    
    public override showBounds() 
    {
        let bounds:Bounds = this.Collider2DZone();
        console.log("showBounds 222 bounds: " + bounds.toString());
        this.boundsGraphics.clear();
        this.boundsGraphics.rect(bounds.x, bounds.y, bounds.width, bounds.height);
        this.boundsGraphics.fill(0x00FF00, 0.5);
        // 5. 添加到舞台
        engine().stage.addChild(this.boundsGraphics)
    }
    
    public update(_time: Ticker) 
    {
        this.showBounds();
    }

}