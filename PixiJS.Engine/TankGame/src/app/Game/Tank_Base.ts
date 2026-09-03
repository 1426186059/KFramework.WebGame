import { AnimatedSprite, Bounds, Container, Point, Sprite, Texture, Ticker } from "pixi.js";
import { TankLevel } from "./TankLevel";
import { TileBase } from "./TileBase";
import { IDisposable } from "../../KFramework.PixiJS/Tool/IDisposable";
import { E_TANK_CAMP_TYPE, E_TILE_TYPE, TankDirection, TankLevelConfig } from "./TankLevelConfig";
import { RectangleExtensions } from "./RectangleExtensions";
import { Tile_Block } from "./Tile_Block";
import { PixiTool } from "../../KFramework.PixiJS/Tool/PixiTool";
import { KTime } from "../../KFramework.PixiJS/GameEngine/KTime";
import { Tile_Home } from "./Tile_Home ";

export class Tank_Base extends TileBase  implements IDisposable
{
    protected readonly fMoveSpeed:number = 200;
    protected readonly fAniSpeed:number = 0.12;
    protected mDirection:TankDirection = TankDirection.DOWN;
    protected bMoveing:boolean = false;
    protected bFire:boolean = false;

    protected HP:number = 0;

    protected nTankType:number = 0;
    protected Animation_Up:Texture[] | null = null;
    protected Animation_Down:Texture[] | null = null;
    protected Animation_Left:Texture[] | null = null;
    protected Animation_Right:Texture[] | null = null;
    protected mAnimationPlayer:AnimatedSprite | null = null;

    public nCampType:E_TANK_CAMP_TYPE = E_TANK_CAMP_TYPE.Enemy;

    constructor(mTankLevel: TankLevel, x:number = 0, y:number = 0)
    {
        super(mTankLevel, x, y);
        this.mTankLevel.TankRoot.addChild(this);
    }

    public override OnPoolPop():void
    {
        
    }

    public override OnPoolPush():void
    {
        
    }
    
    public override Dispose():void
    {
        this.destroy();
    }

    public OnHitAttack():void
    {
        this.HP--;
        if(this.HP <= 0)
        {
            this.Dispose();
        }
    }
    
    public isAlive():boolean
    {
        return PixiTool.isAlive(this) && this.visible;
    }

    protected DoThinkOp():void
    {

    }
    
    public override update() 
    {
        let dir:TankDirection = this.mDirection;
        let bMove = this.bMoveing;

        this.DoThinkOp();

        if (this.bFire) 
        {
            this.bFire = false;
            let mShell = this.mTankLevel.ShellPool.pop();
            if(mShell)
            {
                mShell.UpdateSprite(this, dir);
            }
        }
        
        if(this.mDirection != dir)
        {
            this.SwitchTankType (this.nTankType);
            this.mAnimationPlayer?.gotoAndPlay(0);
        }
        else
        {
            if(bMove != this.bMoveing)
            {
                if(this.bMoveing)
                {
                    this.mAnimationPlayer?.play();
                }
                else
                {
                    this.mAnimationPlayer?.stop();
                }
            }

            if(this.bMoveing)
            {
                let fMoveDistance = this.fMoveSpeed * KTime.deltaTime;
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
                    
                    this.position.set(
                        PixiTool.clamp(this.position.x, this.mTankLevel.MinPosX, this.mTankLevel.MaxPosX - 30),
                        PixiTool.clamp(this.position.y, this.mTankLevel.MinPosY, this.mTankLevel.MaxPosY - 30),
                    );

                    //在这里进行 物理碰撞检测
                    this.HandleCollisions();
                }
            }
        }

        this.showBounds();
    }

    protected SwitchTankType(nType:number):void
    {
        throw new Error("not implement");
    }

    protected SetAnimation(mFrameArray:Texture[]):void
    {
        if(this.mAnimationPlayer != null)
        {
            this.mAnimationPlayer.textures = mFrameArray;
        }
        else
        {
            this.mAnimationPlayer = new AnimatedSprite(mFrameArray);
            this.mAnimationPlayer.animationSpeed = this.fAniSpeed;
            this.mAnimationPlayer.loop = true;
            this.addChild(this.mAnimationPlayer);
        }
    }
    
    public override Collider2DZone():Bounds
    {
        if(this.mAnimationPlayer != null)
        {
            return this.mAnimationPlayer?.getBounds();
        }
        else
        {
            return new Bounds(0, 0, 0, 0);
        }
    }

    private HandleCollisions():void
    {
        let bounds:Bounds = this.Collider2DZone();
        let leftTile = Math.floor((this.position.x + TankLevelConfig.MapWidth * TankLevelConfig.TileWidth / 2) / TankLevelConfig.TileWidth) - 2;
        let rightTile = Math.ceil((this.position.x + TankLevelConfig.MapWidth * TankLevelConfig.TileWidth / 2) / TankLevelConfig.TileWidth) + 2;
        let topTile = Math.floor((this.position.y + TankLevelConfig.MapHeight * TankLevelConfig.TileHeight / 2) / TankLevelConfig.TileHeight) - 2;
        let bottomTile = Math.ceil((this.position.y + TankLevelConfig.MapHeight * TankLevelConfig.TileHeight / 2) / TankLevelConfig.TileHeight) + 2;
        
        //坦克 与 砖瓦 碰撞
        for (let y = topTile; y <= bottomTile; ++y)
        {
            for (let x = leftTile; x <= rightTile; ++x)
            {
                let mTile = this.mTankLevel.GetTile(x, y);
                if (mTile != null)
                {
                    let bCollision:boolean = false;
                    if(mTile instanceof Tile_Block)
                    {
                        if(mTile.nType == E_TILE_TYPE.Wall || 
                            mTile.nType == E_TILE_TYPE.Barriar ||
                            mTile.nType == E_TILE_TYPE.Water)
                        {
                            bCollision = true;
                        }
                    }
                    else if(mTile instanceof Tile_Home)
                    {
                        bCollision = true;
                    }
                    
                    if(bCollision)
                    {
                        let mTarget:TileBase = mTile;
                        let tileBounds:Bounds = mTarget.Collider2DZone();
                        let depth:Point = RectangleExtensions.getIntersectionDepth(bounds, tileBounds);
                        //如果重叠区域 大于0
                        if (depth.x != 0 || depth.y != 0)
                        {
                            let absDepthX = Math.abs(depth.x);
                            let absDepthY = Math.abs(depth.y);
                            if(absDepthX < absDepthY)
                            {
                                let worldPos = this.getGlobalPosition();
                                worldPos.x += depth.x;
                                let localPos = this.mTankLevel.SceneRoot.toLocal(worldPos);
                                this.position.set(localPos.x, localPos.y);
                            }
                            else
                            {
                                let worldPos = this.getGlobalPosition();
                                worldPos.y += depth.y;
                                let localPos = this.mTankLevel.SceneRoot.toLocal(worldPos);
                                this.position.set(localPos.x, localPos.y);
                            }
                        }
                    }

                }
            }
        }

        //坦克与坦克之间的碰撞
        for(let i = 0; i < this.mTankLevel.TankList.length; i++)
        {
            let mTile = this.mTankLevel.TankList[i];
            if (mTile != this)
            {
                let tileBounds:Bounds = mTile.Collider2DZone();
                let depth:Point = RectangleExtensions.getIntersectionDepth(bounds, tileBounds);
                //如果重叠区域 大于0
                if (depth.x != 0 || depth.y != 0)
                {
                    let absDepthX = Math.abs(depth.x);
                    let absDepthY = Math.abs(depth.y);
                    if(absDepthX < absDepthY)
                    {
                        let worldPos = this.getGlobalPosition();
                        worldPos.x += depth.x;
                        let localPos = this.mTankLevel.SceneRoot.toLocal(worldPos);
                        this.position.set(localPos.x, localPos.y);
                    }
                    else
                    {
                        let worldPos = this.getGlobalPosition();
                        worldPos.y += depth.y;
                        let localPos = this.mTankLevel.SceneRoot.toLocal(worldPos);
                        this.position.set(localPos.x, localPos.y);
                    }
                }
            }
        }
        
    }
}