import { Bounds, Container, Graphics, Point, Sprite, Ticker } from "pixi.js";
import { TankLevel } from "./TankLevel";
import { engine } from "../getEngine";

export class TileBase extends Container
{
    public TileX:number;
    public TileY:number;
    public mTankLevel:TankLevel;
    protected readonly boundsGraphics = new Graphics();
    constructor(mTankLevel: TankLevel, x:number = 0, y:number = 0)
    {
        super();
        this.mTankLevel = mTankLevel;
        this.TileX = x;
        this.TileY = y;
    }

    public resize():void
    {
       
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
        let bounds:Bounds = this.Collider2DZone();
        //console.log("showBounds 222 bounds: " + bounds.toString());
        this.boundsGraphics.clear();
        this.boundsGraphics.rect(bounds.x, bounds.y, bounds.width, bounds.height);
        this.boundsGraphics.fill(0x00FF00, 0.5);
        // 5. 添加到舞台
        engine().stage.addChild(this.boundsGraphics)
    }

}