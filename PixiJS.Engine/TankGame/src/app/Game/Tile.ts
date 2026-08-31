import { Bounds, Container, Graphics, Point, Sprite, Ticker } from "pixi.js";
import { TankLevel } from "./TankLevel";

export class TileBase extends Container
{
    public TileX:number;
    public TileY:number;
    public mTankLevel:TankLevel;
    protected readonly boundsGraphics = new Graphics();
    constructor(mTankLevel: TankLevel, x:number, y:number)
    {
        super();
        this.mTankLevel = mTankLevel;
        this.TileX = x;
        this.TileY = y;
        this.addChild(this.boundsGraphics)
    }

    public resize():void
    {
        this.scale = new Point(this.mTankLevel.fTileScaleCoef, this.mTankLevel.fTileScaleCoef);
        this.position = this.mTankLevel.GetTilePos(this.TileX, this.TileY);
    }
    
    public update(_time: Ticker) 
    {
        
    }
    
    public Collider2DZone():Bounds
    {
        throw "Collider2DZone not implemented";
    }

    public showBounds() 
    {
        let bounds = this.Collider2DZone();
        console.log("showBounds bounds: " + bounds.toString());
        this.boundsGraphics.lineStyle(2, 0xff0000, 1.0);
        this.boundsGraphics.rect(bounds.x, bounds.y, bounds.width, bounds.height);
        this.boundsGraphics.x = bounds.x;
        this.boundsGraphics.y = bounds.y;
        // 5. 添加到舞台
        this.addChild(this.boundsGraphics);
    }

}