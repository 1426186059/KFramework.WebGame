import { AnimatedSprite, Assets, Bounds, Container, Point, Sprite, Texture, Ticker } from "pixi.js";
import { TankLevel } from "./TankLevel";
import { KTimer } from "../../KFramework.PixiJS/Timer/KTimer";
import { KTween } from "../../KFramework.PixiJS/KTweeen/KTween";

//敌人 出生点
export class BornPoint_Enemy
{
    private mTankLevel: TankLevel;
    private mPos:Point;
    
    constructor(mTankLevel: TankLevel,  mPos:Point)
    {
        this.mTankLevel = mTankLevel;
        this.mPos = mPos;

        let mTimer = KTimer.New(this.mTankLevel.SceneRoot, this.DoTimerFunc.bind(this), 5.0, -1);
        mTimer.Start();
    }

    private DoTimerFunc():void
    {
        let m_BornEffect = this.mTankLevel.BornEffectPool.pop();
        if(m_BornEffect != null)
        {
            m_BornEffect.PlayAni(this.mPos);
        }
        
        KTween.delayedCall(0.5, ()=>{
            if(this.mTankLevel.Tank_EnemyPool.SumCount() < 30)
            {
                let mTile = this.mTankLevel.Tank_EnemyPool.pop();
                mTile.position = this.mPos;
            }
        });
    }

}