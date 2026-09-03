import { Bounds, DestroyOptions, Sprite, Ticker } from "pixi.js";
import { TankLevel } from "./TankLevel";
import { TileBase } from "./TileBase";
import { E_TILE_TYPE } from "./TankLevelConfig";
import { engine } from "../getEngine";
import { KTween } from "../../KFramework.PixiJS/KTweeen/KTween";
import { FailScreen } from "../screens/main/FailScreen";

export class Tile_Block extends TileBase
{
    public readonly mSprite:Sprite = new Sprite();
    public nType:E_TILE_TYPE = E_TILE_TYPE.Wall;
    
    constructor(mTankLevel: TankLevel, x:number, y:number)
    {
        super(mTankLevel, x, y);
        this.addChild(this.mSprite);
    }

    public override Collider2DZone():Bounds
    {
        let bounds:Bounds = this.mSprite.getBounds();
        //console.log("Collider2DZone 111 bounds: " + bounds.toString());
        return bounds;
    }
    
    public update() 
    {
        this.showBounds();
    }
    
    public override destroy(options?: DestroyOptions): void 
    {
        super.destroy(options);
    }

    public override Dispose():void
    {
        this.mTankLevel.SetTileNull(this.TileX, this.TileY);
        this.destroy();
    }
    
    public OnHitAttack():void
    {
        engine().audio.sfx.play("main/MyRes/Audio/Hit.wav");

        if(this.nType == E_TILE_TYPE.Wall)
        {
            this.Dispose();
        }
        else if(this.nType == E_TILE_TYPE.Heart)
        {
            this.Dispose();
            KTween.delayedCall(2.0, async ()=>{
                await engine().navigation.showScreen(FailScreen);
            });
        }
        else if(this.nType == E_TILE_TYPE.Barriar)
        {
            
        }
        else if(this.nType == E_TILE_TYPE.Grass)
        {
            
        }
        else if(this.nType == E_TILE_TYPE.Water)
        {
            
        }
    }
}