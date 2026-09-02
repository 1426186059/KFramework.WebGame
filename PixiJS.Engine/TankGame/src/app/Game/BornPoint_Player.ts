import { AnimatedSprite, Assets, Bounds, Container, Point, Sprite, Texture, Ticker } from "pixi.js";
import { TankLevel } from "./TankLevel";
import { Tank_My } from "./Tank_My";
import { KTween } from "../../KFramework.PixiJS/KTweeen/KTween";

//玩家 出生点
export class BornPoint_Player
{
    private mTankLevel: TankLevel;
    private mPos:Point;
    
    constructor(mTankLevel: TankLevel,  mPos:Point)
    {
        this.mTankLevel = mTankLevel;
        this.mPos = mPos;

        this.DoBorn();
    }

    public DoBorn():void
    {
        let m_BornEffect = this.mTankLevel.BornEffectPool.pop();
        if(m_BornEffect != null)
        {
            m_BornEffect.PlayAni(this.mPos);
        }
        
        KTween.delayedCall(0.5, ()=>{
            let mTile = new Tank_My(this.mTankLevel);
            mTile.position = this.mPos;
            mTile.mBornPoint = this;
        });
    }

}