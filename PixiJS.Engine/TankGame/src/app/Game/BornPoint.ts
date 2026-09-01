import { AnimatedSprite, Assets, Bounds, Container, Point, Sprite, Texture, Ticker } from "pixi.js";
import { TankLevel } from "./TankLevel";
import { TileBase } from "./TileBase";
import { E_BORN_TYPE } from "./TankLevelConfig";
import { Tank_My } from "./Tank_My";
import { Tank_Enemy } from "./Tank_Enemy";
import { KTimer } from "../../KFramework.PixiJS/Timer/KTimer";

//出生特效
export class BornPoint
{
    private mTankLevel: TankLevel;
    private mPos:Point;
    private nBornType:E_BORN_TYPE;
    
    constructor(mTankLevel: TankLevel,  mPos:Point, nBornType:E_BORN_TYPE)
    {
        this.mTankLevel = mTankLevel;
        this.mPos = mPos;
        this.nBornType = nBornType;

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

        switch(this.nBornType)
        {
            case E_BORN_TYPE.Enemy:
                {
                    let mTile = new Tank_Enemy(this.mTankLevel);
                    this.mTankLevel.SceneRoot.addChild(mTile);
                    mTile.position = this.mPos;
                }
                break;
            case E_BORN_TYPE.Player1:
                {
                    let mTile = new Tank_My(this.mTankLevel);
                    this.mTankLevel.SceneRoot.addChild(mTile);
                    mTile.position = this.mPos;
                }
                break;
            case E_BORN_TYPE.Player2:
                break;
        }
    }

    public Reset():void
    {
        
    }

    public Dispose(): void 
    {
        
    }

}