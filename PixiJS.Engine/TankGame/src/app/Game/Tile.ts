import { Container, Point, Sprite, Ticker } from "pixi.js";
import { TankLevel } from "./TankLevel";

export class TileBase extends Container
{
    public TileX:number;
    public TileY:number;
    public mTankLevel:TankLevel;
    constructor(mTankLevel: TankLevel, x:number, y:number)
    {
        super();
        this.mTankLevel = mTankLevel;
        this.TileX = x;
        this.TileY = y;
    }

    public resize():void
    {
        this.scale = new Point(this.mTankLevel.fTileScaleCoef, this.mTankLevel.fTileScaleCoef);
        this.position = this.mTankLevel.GetTilePos(this.TileX, this.TileY);
    }
    
    public update(_time: Ticker) 
    {
        
    }
}

export class Tile extends TileBase
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

}