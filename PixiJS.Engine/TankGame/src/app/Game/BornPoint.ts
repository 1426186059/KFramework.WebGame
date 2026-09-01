import { AnimatedSprite, Assets, Bounds, Container, Point, Sprite, Texture, Ticker } from "pixi.js";
import { TankLevel } from "./TankLevel";
import { TileBase } from "./TileBase";
import { E_BORN_TYPE, TankDirection, TankLevelConfig } from "./TankLevelConfig";
import { RectangleExtensions } from "./RectangleExtensions";
import { PoolItemContainer } from "../../KFramework.PixiJS/Tool/PoolItemContainer";

//出生特效
export class BornPoint extends Container
{
    private mTankLevel: TankLevel;
    private mPos:Point;
    private nBornType:E_BORN_TYPE;
    
    constructor(mTankLevel: TankLevel,  mPos:Point, nBornType:E_BORN_TYPE)
    {
        super();
        this.mTankLevel = mTankLevel;
        this.mPos = mPos;
        this.nBornType = nBornType;
    }
    
    public DoTimerFunc():void
    {
        let m_BornEffect = this.mTankLevel.ExplodeEffectPool.pop();
        if(m_BornEffect != null)
        {
            m_BornEffect.PlayAni(this.position);
        }

        switch(this.nBornType)
        {
            case E_BORN_TYPE.Enemy:
                break;
            case E_BORN_TYPE.Player1:
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