import { Bounds, Point, Sprite, Texture, Ticker } from "pixi.js";
import { TankLevel } from "./TankLevel";
import { TileBase } from "./TileBase";
import { TankDirection, TankLevelConfig } from "./TankLevelConfig";
import { Tile_Block } from "./Tile_Block";
import { RectangleExtensions } from "./RectangleExtensions";
import { Tank_Enemy } from "./Tank_Enemy";
import { Tank_My } from "./Tank_My";
import { PixiTool } from "../../KFramework.PixiJS/Tool/PixiTool";
import { KTime } from "../../KFramework.PixiJS/GameEngine/KTime";
import { Tile_Home } from "./Tile_Home ";
import { engine } from "../getEngine";

//坦克发射的 炮弹
export class Shell extends TileBase
{
    public readonly mSprite:Sprite = new Sprite();
    public mDirection:TankDirection = TankDirection.UP;
    private readonly fMoveSpeed:number = 200;
    private bMoveing:boolean = false;
    private WhoSendShell:TileBase | null = null;

    constructor(mTankLevel: TankLevel)
    {
        super(mTankLevel);
        this.mTankLevel.TankRoot.addChild(this);
        this.addChild(this.mSprite);
    }

    public override OnPoolPop():void
    {
        this.bMoveing = true;
        this.visible = true;
        this.WhoSendShell =null;
        this.mTankLevel.ShellList.push(this);
        this.visible = true;
    }

    public override OnPoolPush():void
    {
        this.bMoveing = false;
        this.visible = false;
        this.WhoSendShell =null;

        let nIndex = this.mTankLevel.ShellList.indexOf(this);
        this.mTankLevel.ShellList.splice(nIndex,1);
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
    
    public update() 
    {
        if(!this.visible) return;
        this.showBounds();

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

                //在这里进行 物理碰撞检测
                this.HandleCollisions();
            }

            if(this.visible)
            {
                if(this.position.x <= this.mTankLevel.MinPosX || 
                    this.position.x >= this.mTankLevel.MaxPosX)
                {
                    this.mTankLevel.ShellPool.push(this);
                }

                if(this.position.y <= this.mTankLevel.MinPosY || 
                    this.position.y >= this.mTankLevel.MaxPosY)
                {
                    this.mTankLevel.ShellPool.push(this);
                }
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
                        let m_BornEffect = this.mTankLevel.ExplodeEffectPool.pop();
                        if(m_BornEffect != null)
                        {
                            m_BornEffect.PlayAni(this.position);
                            if(this.WhoSendShell instanceof Tank_My)
                            {
                                engine().audio.sfx.play("main/MyRes/Audio/Explosion.wav");
                            }
                        }

                        if(mTile instanceof Tile_Block)
                        {
                            mTile.OnHitAttack();
                        }
                        else if(mTile instanceof Tile_Home)
                        {
                            mTile.OnHitAttack();
                        }

                        this.mTankLevel.ShellPool.push(this);
                        return;
                    }
                }
            }
        }

        for(let i = 0; i < this.mTankLevel.TankList.length; i++)
        {
            let mTile = this.mTankLevel.TankList[i];
            if(PixiTool.isAlive(mTile) && 
                this.WhoSendShell != null && 
                mTile.constructor !== this.WhoSendShell.constructor)
            {
                let tileBounds:Bounds = mTile.Collider2DZone();
                let depth:Point = RectangleExtensions.getIntersectionDepth(bounds, tileBounds);
                if (depth.x != 0 || depth.y != 0)
                {
                    let m_ExplodeEffect = this.mTankLevel.ExplodeEffectPool.pop();
                    if(m_ExplodeEffect != null)
                    {
                        m_ExplodeEffect.PlayAni(this.position);
                        if(this.WhoSendShell instanceof Tank_My)
                        {
                            engine().audio.sfx.play("main/MyRes/Audio/Explosion.wav");
                        }
                    }

                    if(mTile instanceof Tank_Enemy)
                    {
                        mTile.OnHitAttack();
                    }
                    else if(mTile instanceof Tank_My)
                    {
                        mTile.OnHitAttack();
                    }

                    this.mTankLevel.ShellPool.push(this);
                    return;
                }
            }
        }

        for(let i = 0; i < this.mTankLevel.ShellList.length; i++)
        {
            let otherShell = this.mTankLevel.ShellList[i];
            if(PixiTool.isAlive(otherShell) && this != otherShell)
            {
                let tileBounds:Bounds = otherShell.Collider2DZone();
                let depth:Point = RectangleExtensions.getIntersectionDepth(bounds, tileBounds);
                if (depth.x != 0 || depth.y != 0)
                {
                    let m_ExplodeEffect = this.mTankLevel.ExplodeEffectPool.pop();
                    if(m_ExplodeEffect != null)
                    {
                        m_ExplodeEffect.PlayAni(this.position);
                        if(this.WhoSendShell instanceof Tank_My)
                        {
                            engine().audio.sfx.play("main/MyRes/Audio/Explosion.wav");
                        }
                    }

                    this.mTankLevel.ShellPool.push(this);
                    this.mTankLevel.ShellPool.push(otherShell);
                    return;
                }
            }
        }
    }

}