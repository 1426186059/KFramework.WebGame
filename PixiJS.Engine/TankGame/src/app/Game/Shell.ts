import { Bounds, Point, Sprite, Ticker } from "pixi.js";
import { TankLevel } from "./TankLevel";
import { TileBase } from "./TileBase";
import { engine } from "../getEngine";
import { TankDirection, TankLevelConfig } from "./TankLevelConfig";
import { Tile_Block } from "./Tile_Block";
import { RectangleExtensions } from "./RectangleExtensions";

export class Shell extends TileBase
{
    public static readonly Pool:Shell[] = [];
    
    public readonly mSprite:Sprite = new Sprite();
    public mDirection:TankDirection = TankDirection.UP;
    private readonly fMoveSpeed:number = 5;
    private bMoveing:boolean = false;

    constructor(mTankLevel: TankLevel)
    {
        super(mTankLevel);
        this.addChild(this.mSprite);
        this.resize();
        this.bMoveing = true;
        this.visible = true;
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

        if(this.bMoveing)
        {
            let fMoveDistance = this.fMoveSpeed * _time.deltaTime;
            //这里有可能移动距离过大，所以这里得分为很多帧执行
            let nStepCount = Math.ceil(fMoveDistance / (TankLevelConfig.TileWidth / 2));
            let fStepDistance = fMoveDistance / nStepCount;
            while(nStepCount-- > 0)
            {
                if (this.mDirection == TankDirection.UP) 
                {
                    this.position.y -= fStepDistance;
                }
                else if (this.mDirection == TankDirection.DOWN) 
                {
                    this.position.y += fStepDistance;
                }
                else if (this.mDirection == TankDirection.LEFT) 
                {
                    this.position.x -= fStepDistance;
                }
                else if (this.mDirection == TankDirection.RIGHT) 
                {
                    this.position.x += fStepDistance;
                }

                //在这里进行 物理碰撞检测
                this.HandleCollisions();
            }
        }

    }

    private HandleCollisions():void
    {
        let bounds:Bounds = this.Collider2DZone();
        let leftTile = Math.floor((this.position.x + TankLevelConfig.MapWidth * TankLevelConfig.TileWidth / 2) / TankLevelConfig.TileWidth) - 2;
        let rightTile = Math.ceil((this.position.x + TankLevelConfig.MapWidth * TankLevelConfig.TileWidth / 2) / TankLevelConfig.TileWidth) + 2;
        let topTile = Math.floor((this.position.y + TankLevelConfig.MapHeight * TankLevelConfig.TileHeight / 2) / TankLevelConfig.TileHeight) - 2;
        let bottomTile = Math.ceil((this.position.y + TankLevelConfig.MapHeight * TankLevelConfig.TileHeight / 2) / TankLevelConfig.TileHeight) + 2;
        
        // console.log("leftTile: " + leftTile);
        // console.log("rightTile: " + rightTile);
        // console.log("topTile: " + topTile);
        // console.log("bottomTile: " + bottomTile);

        for (let y = topTile; y <= bottomTile; ++y)
        {
            for (let x = leftTile; x <= rightTile; ++x)
            {
                let mTile = this.mTankLevel.GetTile(x, y);
                if (mTile != null)
                {
                    let mTarget = mTile;
                    let tileBounds:Bounds = mTarget.Collider2DZone();
                    let depth:Point = RectangleExtensions.getIntersectionDepth(bounds, tileBounds);
                    //console.log("AABB depth 000: " + depth.toString());
                    if (depth.x != 0 || depth.y != 0)
                    {
                        this.bMoveing = false;
                        this.visible = false;
                    }
                }
            }
        }
    }

}