import { AnimatedSprite, Bounds, Container, Point, Sprite, Texture, Ticker } from "pixi.js";
import { TankLevel } from "./TankLevel";
import { TileBase } from "./TileBase";
import { IDisposable } from "../../KFramework.PixiJS/Tool/IDisposable";
import { E_TANK_CAMP_TYPE, E_TILE_TYPE, TankDirection, TankLevelConfig } from "./TankLevelConfig";
import { randomInt } from "../../engine/utils/random";
import { RectangleExtensions } from "./RectangleExtensions";
import { Tile_Block } from "./Tile_Block";
import { PixiTool } from "../../KFramework.PixiJS/Tool/PixiTool";
import { KTime } from "../../KFramework.PixiJS/GameEngine/KTime";
import { Tile_Home } from "./Tile_Home ";
import { Tank_Base } from "./Tank_Base";
import { GameScene } from "./GameScene";

export class Tank_Enemy extends Tank_Base  implements IDisposable
{
    private fLastThinkTime:number = 0;

    constructor(mTankLevel: TankLevel, x:number = 0, y:number = 0)
    {
        super(mTankLevel, x, y);
        this.mTankLevel.TankRoot.addChild(this);
        this.nCampType =  E_TANK_CAMP_TYPE.Enemy;
    }

    public override OnPoolPop():void
    {
        this.nTankType = randomInt(0, 7);
        this.mDirection = TankDirection.DOWN;
        this.SwitchTankType(this.nTankType);
        this.HP = PixiTool.clamp(this.nTankType, 1, 5);
        this.mTankLevel.TankList.push(this);
        this.visible = true;
    }

    public override OnPoolPush():void
    {
        let nIndex = this.mTankLevel.TankList.indexOf(this);
        this.mTankLevel.TankList.splice(nIndex,1);
        this.visible = false;
    }
        
    public override Dispose():void
    {
        super.Dispose();
        this.mTankLevel.nKillEnemyCount++;
        let nSumNeedKillEnemyCount = TankLevelConfig.GetLevelEnemyCount(this.mTankLevel.nLevelIndex);
        if(this.mTankLevel.nKillEnemyCount >= nSumNeedKillEnemyCount)
        {
            GameScene.GetInstance().LoadNextLevel();
        }
    }

    public override OnHitAttack():void
    {
        super.OnHitAttack();
    }
    
    public override update() 
    {
        super.update();
    }

    public override SwitchTankType(nType:number):void
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

    protected override DoThinkOp():void
    {
        const currentTime = performance.now() / 1000.0;
        if(currentTime - this.fLastThinkTime > 1.0)
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