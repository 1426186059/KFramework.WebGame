import { AnimatedSprite, Assets, Bounds, Container, Point, Sprite, Texture, Ticker } from "pixi.js";
import { TankLevel } from "./TankLevel";
import { TileBase } from "./TileBase";
import { E_BORN_TYPE, TankDirection, TankLevelConfig } from "./TankLevelConfig";
import { RectangleExtensions } from "./RectangleExtensions";
import { PoolItemContainer } from "../../KFramework.PixiJS/Tool/PoolItemContainer";
import { Tank_My } from "./Tank_My";
import { Tank_Enemy } from "./Tank_Enemy";

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