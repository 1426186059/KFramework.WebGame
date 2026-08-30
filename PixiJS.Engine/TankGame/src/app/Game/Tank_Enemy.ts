import { Container, Point, Sprite } from "pixi.js";
import { TankLevel } from "./TankLevel";
import { TileBase } from "./Tile";

export class Tank_Enemy extends TileBase
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
        this.scale = new Point(this.mTankLevel.fTileScaleCoef, this.mTankLevel.fTileScaleCoef);
        this.position = this.mTankLevel.GetTilePos(this.TileX, this.TileY);
    }

    public Move(x:number, y:number):void
    {
        
    }
}