import { AnimatedSprite, Container, Point, Sprite, Texture, Ticker } from "pixi.js";
import { TankLevel } from "./TankLevel";
import { TileBase } from "./Tile";
import { engine } from "../getEngine";
import { IDisposable } from "../../KFramework.PixiJS/Tool/IDisposable";
import { TankDirection } from "./TankLevelConfig";

export class Tank_Enemy extends TileBase  implements IDisposable
{
    private readonly fMoveSpeed:number = 50;
    private readonly fAniSpeed:number = 0.12;
    private mDirection:TankDirection = TankDirection.DOWN;
    
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
        this.nTankType = 0;
        this.SwitchTankType(this.nTankType);
    }

    public Dispose():void
    {
       
    }

    public override resize():void
    {
        super.resize();
    }
    
    public update(_time: Ticker) 
    {
        
    }

    public SwitchTankType(nType:number):void
    {
        this.nTankType = nType;

        let mSprite1:Texture = Texture.from(`Player1_${nType * 2 + 0}`);
        let mSprite2:Texture = Texture.from(`Player1_${nType * 2 + 1}`);
        let mSprite3:Texture = Texture.from(`Player1_${nType * 2 + 8}`);
        let mSprite4:Texture = Texture.from(`Player1_${nType * 2 + 9}`);
        let mSprite5:Texture = Texture.from(`Player1_${nType * 2 + 16}`);
        let mSprite6:Texture = Texture.from(`Player1_${nType * 2 + 17}`);
        let mSprite7:Texture = Texture.from(`Player1_${nType * 2 + 24}`);
        let mSprite8:Texture = Texture.from(`Player1_${nType * 2 + 25}`);

        console.assert(mSprite1 != null, "mSprite1 is null");
        console.assert(mSprite2 != null, "mSprite2 is null");
        console.assert(mSprite3 != null, "mSprite3 is null");
        console.assert(mSprite4 != null, "mSprite4 is null");
        console.assert(mSprite5 != null, "mSprite5 is null");
        console.assert(mSprite6 != null, "mSprite6 is null");
        console.assert(mSprite7 != null, "mSprite7 is null");
        console.assert(mSprite8 != null, "mSprite8 is null");

        this.Animation_Up = [mSprite1, mSprite2];
        this.Animation_Down = [mSprite5, mSprite6];
        this.Animation_Left = [mSprite7, mSprite8];
        this.Animation_Right = [mSprite3, mSprite4];

        if(this.mDirection == TankDirection.UP)
        {
            this.PlayAnimation(this.Animation_Up);
        }
        else if(this.mDirection == TankDirection.DOWN)
        {
            this.PlayAnimation(this.Animation_Down);
        }
        else if(this.mDirection == TankDirection.LEFT)
        {
            this.PlayAnimation(this.Animation_Left);
        }
        else if(this.mDirection == TankDirection.RIGHT)
        {
            this.PlayAnimation(this.Animation_Right);
        }
    }

    public PlayAnimation(mFrameArray:Texture[] ):void
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

        this.mAnimationPlayer.play();
    }

}