import { AnimatedSprite, Bounds, Container, Point, Sprite, Texture, Ticker } from "pixi.js";
import { TankLevel } from "./TankLevel";
import { Tile, TileBase } from "./Tile";
import { engine } from "../getEngine";
import { IDisposable } from "../../KFramework.PixiJS/Tool/IDisposable";
import { TankDirection, TankLevelConfig } from "./TankLevelConfig";
import { randomInt } from "../../engine/utils/random";
import { RectangleExtensions } from "./RectangleExtensions";
import { Tile_Block } from "./Tile_Block";

export class Tank_Enemy extends TileBase  implements IDisposable
{
    private readonly fMoveSpeed:number = 5;
    private readonly fAniSpeed:number = 0.12;
    private mDirection:TankDirection = TankDirection.DOWN;
    private bMoveing:boolean = false;

    private nTankType:number = 0;
    private Animation_Up:Texture[] | null = null;
    private Animation_Down:Texture[] | null = null;
    private Animation_Left:Texture[] | null = null;
    private Animation_Right:Texture[] | null = null;
    private mAnimationPlayer:AnimatedSprite | null = null;

    constructor(mTankLevel: TankLevel, x:number, y:number)
    {
        super(mTankLevel, x, y);
        this.resize();
        this.nTankType = randomInt(0, 7);
        this.SwitchTankType(this.nTankType);
    }

    public Dispose():void
    {
        
    }

    public override resize():void
    {
        super.resize();
    }
    
    public override update(_time: Ticker) 
    {
        let dir:TankDirection = this.mDirection;
        let bMove = this.bMoveing;

        this.DoAIThink(_time);
        if(this.bMoveing)
        {
            let fMoveDistance = this.fMoveSpeed * _time.deltaTime;
            //这里有可能移动距离过大，所以这里得分为很多帧执行
            let nStepCount = Math.ceil(fMoveDistance / 32);
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

        if (this.bFire) 
        {
            //发射子弹
        }
        
        if(this.mDirection != dir)
        {
            this.SwitchTankType (this.nTankType);
            this.mAnimationPlayer?.gotoAndPlay(0);
        }

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
    }

    public SwitchTankType(nType:number):void
    {
        this.nTankType = nType;

        let nIndex:number = nType % 4;
        let Offset:number = Math.trunc(nType / 4) * 32;
        let mSprite1:Texture = Texture.from(`Enemys_${nIndex * 2 + Offset + 0}`);
        let mSprite2:Texture = Texture.from(`Enemys_${nIndex * 2 + Offset + 1}`);
        let mSprite3:Texture = Texture.from(`Enemys_${nIndex * 2 + Offset + 8}`);
        let mSprite4:Texture = Texture.from(`Enemys_${nIndex * 2 + Offset + 9}`);
        let mSprite5:Texture = Texture.from(`Enemys_${nIndex * 2 + Offset + 16}`);
        let mSprite6:Texture = Texture.from(`Enemys_${nIndex * 2 + Offset + 17}`);
        let mSprite7:Texture = Texture.from(`Enemys_${nIndex * 2 + Offset + 24}`);
        let mSprite8:Texture = Texture.from(`Enemys_${nIndex * 2 + Offset + 25}`);

        console.assert(mSprite1 != null, "mSprite1 is null: " + nType);
        console.assert(mSprite3 != null, "mSprite3 is null: " + nType);
        console.assert(mSprite4 != null, "mSprite4 is null: " + nType);
        console.assert(mSprite5 != null, "mSprite5 is null: " + nType);
        console.assert(mSprite6 != null, "mSprite6 is null: " + nType);
        console.assert(mSprite7 != null, "mSprite7 is null: " + nType);
        console.assert(mSprite8 != null, "mSprite8 is null: " + nType);
        
        this.Animation_Up = [mSprite1, mSprite2];
        this.Animation_Right = [mSprite3, mSprite4];
        this.Animation_Down = [mSprite5, mSprite6];
        this.Animation_Left = [mSprite7, mSprite8];

        if(this.mDirection == TankDirection.UP)
        {
            this.SetAnimation(this.Animation_Up);
        }
        else if(this.mDirection == TankDirection.DOWN)
        {
            this.SetAnimation(this.Animation_Down);
        }
        else if(this.mDirection == TankDirection.LEFT)
        {
            this.SetAnimation(this.Animation_Left);
        }
        else if(this.mDirection == TankDirection.RIGHT)
        {
            this.SetAnimation(this.Animation_Right);
        }

    }

    public SetAnimation(mFrameArray:Texture[]):void
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

    private fLastThinkTime:number = 0;
    private bFire:boolean = false;
    private DoAIThink(_time: Ticker):void
    {
        const currentTime = performance.now() / 1000.0;
        if(currentTime - this.fLastThinkTime > 0.5)
        {
            this.fLastThinkTime = currentTime;
            let nResult = randomInt(0, 10);
            if(nResult == 0)
            {
                this.mDirection = TankDirection.UP;
            }
            else if(nResult == 1)
            {
                this.mDirection = TankDirection.DOWN;
            }
            else if(nResult == 2)
            {
                this.mDirection = TankDirection.LEFT;
            }
            else if(nResult == 3)
            {
                this.mDirection = TankDirection.RIGHT;
            }

            nResult = randomInt(0, 10);
            this.bMoveing = false;
            this.bFire = false;
            if(nResult < 5)
            {
                this.bMoveing = true;
            }
            else
            {
                this.bFire = true;
            }
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
        let leftTile = Math.floor(bounds.left / TankLevelConfig.TileWidth) - 2;
        let rightTile = Math.ceil(bounds.right / TankLevelConfig.TileWidth) + 2;
        let topTile = Math.floor(bounds.top / TankLevelConfig.TileHeight) - 2;
        let bottomTile = Math.ceil((bounds.bottom / TankLevelConfig.TileHeight)) + 2;
        
        for (let y = topTile; y <= bottomTile; ++y)
        {
            for (let x = leftTile; x <= rightTile; ++x)
            {
                let mTile = this.mTankLevel.GetTile(x, y);
                if (mTile != null && mTile instanceof Tile_Block)
                {
                    let mTarget:Tile_Block = mTile;
                    let tileBounds:Bounds = mTarget.Collider2DZone();
                    let depth = RectangleExtensions.getIntersectionDepth(bounds, tileBounds);
                    //如果重叠区域 大于0
                    if (depth.x != 0 || depth.y != 0)
                    {
                        let absDepthX = Math.abs(depth.x);
                        let absDepthY = Math.abs(depth.y);
                        if(absDepthX < absDepthY)
                        {
                            this.position.x += depth.x;
                        }
                        else
                        {
                             this.position.y += depth.y;
                        }
                    }
                }
            }
        }
        
    }
}