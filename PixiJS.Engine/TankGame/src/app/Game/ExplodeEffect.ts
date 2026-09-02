import { AnimatedSprite, Assets, Bounds, Container, Point, Sprite, Texture, Ticker } from "pixi.js";
import { TankLevel } from "./TankLevel";
import { PoolItemContainer } from "../../KFramework.PixiJS/Tool/PoolItemContainer";

//爆炸特效
export class ExplodeEffect extends PoolItemContainer
{
    private mAnimationPlayer:AnimatedSprite | null = null;
    private mTankLevel: TankLevel;
    
    constructor(mTankLevel: TankLevel)
    {
        super();
        this.mTankLevel = mTankLevel;
        this.mTankLevel.EffectRoot.addChild(this);

        let Textures = 
        [
            Texture.from("main/MyRes/Textures/Explode1.png"),
        ];
        
        this.mAnimationPlayer = new AnimatedSprite(Textures);
        this.mAnimationPlayer.animationSpeed = 0.1;
        this.mAnimationPlayer.loop = false;
        this.mAnimationPlayer.pivot = new Point(Textures[0].width / 2, Textures[0].height / 2);
        
        this.mAnimationPlayer.onComplete = () => {
            this.mTankLevel.ExplodeEffectPool.push(this);
        }
        this.addChild(this.mAnimationPlayer);
    }

    public PlayAni(mPos:Point):void
    {
        this.position = mPos;
        this.mAnimationPlayer?.gotoAndPlay(0);
    }

    OnPoolPop(): void
    {
        this.visible = true;
    }

    OnPoolPush(): void
    {
        this.visible = false;
    }

    public Dispose(): void 
    {
        
    }

}