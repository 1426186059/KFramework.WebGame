import { AnimatedSprite, Container, Point, Sprite, Texture, Ticker } from "pixi.js";
import { TankLevel } from "./TankLevel";
import { TileBase } from "./Tile";
import { engine } from "../getEngine";
import { IDisposable } from "../../KFramework.PixiJS/Tool/IDisposable";
import { TankDirection } from "./TankLevelConfig";
import { randomInt } from "../../engine/utils/random";

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
            if (this.mDirection == TankDirection.UP) 
            {
                this.position.y -= this.fMoveSpeed * _time.deltaTime;
            }
            else if (this.mDirection == TankDirection.DOWN) 
            {
                this.position.y += this.fMoveSpeed * _time.deltaTime;
            }
            else if (this.mDirection == TankDirection.LEFT) 
            {
                this.position.x -= this.fMoveSpeed * _time.deltaTime;
            }
            else if (this.mDirection == TankDirection.RIGHT) 
            {
                this.position.x += this.fMoveSpeed * _time.deltaTime;
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

        let Offset:number = Math.trunc(nType / 4) * 32;
        let mSprite1:Texture = Texture.from(`Enemys_${nType * 2 + Offset + 0}`);
        let mSprite2:Texture = Texture.from(`Enemys_${nType * 2 + Offset + 1}`);
        let mSprite3:Texture = Texture.from(`Enemys_${nType * 2 + Offset + 8}`);
        let mSprite4:Texture = Texture.from(`Enemys_${nType * 2 + Offset + 9}`);
        let mSprite5:Texture = Texture.from(`Enemys_${nType * 2 + Offset + 16}`);
        let mSprite6:Texture = Texture.from(`Enemys_${nType * 2 + Offset + 17}`);
        let mSprite7:Texture = Texture.from(`Enemys_${nType * 2 + Offset + 24}`);
        let mSprite8:Texture = Texture.from(`Enemys_${nType * 2 + Offset + 25}`);

        console.assert(mSprite1 != null, "mSprite1 is null");
        console.assert(mSprite3 != null, "mSprite3 is null");
        console.assert(mSprite4 != null, "mSprite4 is null");
        console.assert(mSprite5 != null, "mSprite5 is null");
        console.assert(mSprite6 != null, "mSprite6 is null");
        console.assert(mSprite7 != null, "mSprite7 is null");
        console.assert(mSprite8 != null, "mSprite8 is null");
        
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
}