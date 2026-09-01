import { Bounds, Point, Sprite, Texture, Ticker } from "pixi.js";
import { TankLevel } from "./TankLevel";
import { TileBase } from "./TileBase";
import { engine } from "../getEngine";
import { TankDirection, TankLevelConfig } from "./TankLevelConfig";
import { Tile_Block } from "./Tile_Block";
import { RectangleExtensions } from "./RectangleExtensions";
import { ObjPool } from "../../KFramework.PixiJS/Tool/PoolContainer";
import { IPoolItem } from "../../KFramework.PixiJS/Tool/IPoolItem";
import { Tank_Enemy } from "./Tank_Enemy";

export class Shell extends TileBase
{
    public readonly mSprite:Sprite = new Sprite();
    public mDirection:TankDirection = TankDirection.UP;
    private readonly fMoveSpeed:number = 5;
    private bMoveing:boolean = false;
    private WhoSendShell:TileBase | null = null;

    constructor(mTankLevel: TankLevel)
    {
        super(mTankLevel);
        this.mTankLevel.SceneRoot.addChild(this);
        this.addChild(this.mSprite);
        this.resize();
        this.bMoveing = true;
        this.visible = true;
        this.WhoSendShell = null;
    }

    public Reset():void
    {
        this.bMoveing = true;
        this.visible = true;
        this.WhoSendShell =null;
    }

    public Dispose(): void 
    {
        
    }

    public UpdateSprite(WhoSendShell:TileBase, dir:TankDirection):void
    {
        this.mDirection = dir;
        this.WhoSendShell = WhoSendShell;
        let mPos = WhoSendShell.position;
        if (this.mDirection == TankDirection.UP) 
        {
            this.mSprite.texture = Texture.from(`bullet_0`);
            this.position.set(mPos.x + 14, mPos.y);
        }
        else if (this.mDirection == TankDirection.DOWN) 
        {
            this.mSprite.texture = Texture.from(`bullet_2`);
            this.position.set(mPos.x + 14, mPos.y + 32);
        }
        else if (this.mDirection == TankDirection.LEFT) 
        {
            this.mSprite.texture = Texture.from(`bullet_3`);
            this.position.set(mPos.x, mPos.y + 13);
        }
        else if (this.mDirection == TankDirection.RIGHT) 
        {
            this.mSprite.texture = Texture.from(`bullet_1`);
            this.position.set(mPos.x + 32, mPos.y + 13);
        }

        this.mSprite.pivot = new Point(this.mSprite.texture.width / 2, this.mSprite.texture.height / 2);
    }

    public override Collider2DZone():Bounds
    {
        let bounds:Bounds = this.mSprite.getBounds();
        //console.log("Collider2DZone 111 bounds: " + bounds.toString());
        return bounds;
    }
    
    public update(_time: Ticker) 
    {
        if(!this.visible) return;
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
        if(!this.visible) return;

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
                if (mTile != null && this.WhoSendShell != mTile)
                {
                    let mTarget = mTile;
                    let tileBounds:Bounds = mTarget.Collider2DZone();
                    let depth:Point = RectangleExtensions.getIntersectionDepth(bounds, tileBounds);
                    //console.log("AABB depth 000: " + depth.toString());
                    if (depth.x != 0 || depth.y != 0)
                    {
                        this.mTankLevel.ShellPool.push(this);

                        let m_BornEffect = this.mTankLevel.BornEffectPool.pop();
                        if(m_BornEffect != null)
                        {
                            m_BornEffect.PlayAni(this.position);
                        }

                        if(mTile instanceof Tank_Enemy)
                        {
                            
                        }
                        else if(mTile instanceof Tile_Block)
                        {
                            this.mTankLevel.SetTileNull(x, y);
                        }

                        return;
                    }
                }
            }
        }
    }

}