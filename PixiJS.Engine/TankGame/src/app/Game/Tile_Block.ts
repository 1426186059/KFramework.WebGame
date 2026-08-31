import { Bounds, Sprite } from "pixi.js";
import { TankLevel } from "./TankLevel";
import { TileBase } from "./Tile";

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
        return bounds;
    }

    public override showBounds() 
    {
        let bounds:Bounds = this.Collider2DZone();
        console.log("showBounds 222 bounds: " + bounds.toString());
        this.boundsGraphics.tint = 0x00FF00;
        this.boundsGraphics.rect(bounds.x, bounds.y, bounds.width, bounds.height);
        this.boundsGraphics.x = bounds.x;
        this.boundsGraphics.y = bounds.y;
        // 5. 添加到舞台
        this.addChild(this.boundsGraphics)
    }
}