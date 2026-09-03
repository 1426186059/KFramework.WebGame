import { Bounds, Sprite, Ticker } from "pixi.js";
import { TankLevel } from "./TankLevel";
import { TileBase } from "./TileBase";
import { KTween } from "../../KFramework.PixiJS/KTweeen/KTween";
import { engine } from "../getEngine";
import { FailScreen } from "../screens/main/FailScreen";

//坦克老巢
export class Tile_Home extends TileBase
{
    public readonly mSprite:Sprite = new Sprite();

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

    public override Dispose(): void 
    {
        this.boundsGraphics.destroy();
        this.destroy();
        
        KTween.delayedCall(2.0, async ()=>{
            await engine().navigation.showScreen(FailScreen);
        });
    }

}