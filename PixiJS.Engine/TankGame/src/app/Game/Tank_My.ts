import { AnimatedSprite, Bounds, Container, Point, Sprite, Texture, Ticker } from "pixi.js";
import { TankLevel } from "./TankLevel";
import { TileBase } from "./TileBase";
import { engine } from "../getEngine";
import { IDisposable } from "../../KFramework.PixiJS/Tool/IDisposable";
import { E_TANK_CAMP_TYPE, TankDirection, TankLevelConfig } from "./TankLevelConfig";
import { Tile_Block } from "./Tile_Block";
import { RectangleExtensions } from "./RectangleExtensions";
import { Shell } from "./Shell";
import { PixiTool } from "../../KFramework.PixiJS/Tool/PixiTool";
import { KTime } from "../../KFramework.PixiJS/GameEngine/KTime";
import { BornPoint_Player } from "./BornPoint_Player";
import { Tile_Home } from "./Tile_Home ";
import { KTween } from "../../KFramework.PixiJS/KTweeen/KTween";
import { FailScreen } from "../screens/main/FailScreen";
import { Tank_Base } from "./Tank_Base";
import { KeyBoard, KeyCode } from "../../KFramework.PixiJS/Input/KeyBoard";

export class Tank_My extends Tank_Base  implements IDisposable
{
    public mBornPoint:BornPoint_Player | null = null;
    private readonly mKeyBoard:KeyBoard = new KeyBoard();
    
    constructor(mTankLevel: TankLevel, x:number = 0, y:number = 0)
    {
        super(mTankLevel, x, y);
        this.mTankLevel.TankRoot.addChild(this);
        this.nCampType =  E_TANK_CAMP_TYPE.Player;

        this.mTankLevel.TankList.push(this);

        this.nTankType = 0;
        this.mDirection = TankDirection.UP;
        this.SwitchTankType(this.nTankType);
    }

    public Dispose():void
    {
        let nIndex = this.mTankLevel.TankList.indexOf(this);
        this.mTankLevel.TankList.splice(nIndex, 1);
        this.mKeyBoard.Dispose();
        this.destroy();

         KTween.delayedCall(2.0, async ()=>{
            await engine().navigation.showScreen(FailScreen);
        });
    }
    
    public override OnHitAttack():void
    {
        super.OnHitAttack();
        engine().audio.sfx.play("main/MyRes/Audio/Hit.wav");
    }

    protected override DoThinkOp():void
    {
        this.bMoveing = false;
        this.bFire = false;
        
        if (this.mKeyBoard.GetKey(KeyCode.KeyW) || this.mKeyBoard.GetKey(KeyCode.ArrowUp))
        {
            this.bMoveing = true;
            this.mDirection = TankDirection.UP;
        }
        else if (this.mKeyBoard.GetKey(KeyCode.KeyS) || this.mKeyBoard.GetKey(KeyCode.ArrowDown)) 
        {
            this.bMoveing = true;
            this.mDirection = TankDirection.DOWN;
        }
        else if (this.mKeyBoard.GetKey(KeyCode.KeyA) || this.mKeyBoard.GetKey(KeyCode.ArrowLeft)) 
        {
            this.bMoveing = true;
            this.mDirection = TankDirection.LEFT;
        }
        else if (this.mKeyBoard.GetKey(KeyCode.KeyD) || this.mKeyBoard.GetKey(KeyCode.ArrowRight)) 
        {
            this.bMoveing = true;
            this.mDirection = TankDirection.RIGHT;
        }
        
        if (this.mKeyBoard.GetKeyDown(KeyCode.Space))
        {
            this.bFire = true;
        }
    }
    
    public override update() 
    {
        super.update();
    }

    public override SwitchTankType(nType:number):void
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

}