import { AnimatedSprite, Container, Point, Sprite, Texture, Ticker } from "pixi.js";
import { TankLevel } from "./TankLevel";
import { TileBase } from "./Tile";
import { engine } from "../getEngine";
import { IDisposable } from "../../KFramework.PixiJS/Tool/IDisposable";

export class Tank_My extends TileBase  implements IDisposable
{
    public readonly mSprite:Sprite = new Sprite();

    private readonly fMoveSpeed:number = 50;
    private nTankType:number = 0;
    private readonly Animation_Up:AnimatedSprite = new AnimatedSprite([]);
    private readonly Animation_Down:AnimatedSprite= new AnimatedSprite([]);
    private readonly Animation_Left:AnimatedSprite= new AnimatedSprite([]);
    private readonly Animation_Right:AnimatedSprite= new AnimatedSprite([]);
    
    private readonly Keys = 
    {
        w: false,
        a: false,
        s: false,
        d: false
    };

    constructor(mTankLevel: TankLevel, x:number, y:number)
    {
        super(mTankLevel, x, y);
        this.addChild(this.mSprite);
        this.resize();

        this.nTankType = 0;
        this.SwitchTankType(this.nTankType);

        this.Animation_Up.animationSpeed = 0.12;      // 帧速率（12 FPS）
        this.Animation_Up.loop = true;
        this.addChild(this.Animation_Up);

        this.Animation_Down.animationSpeed = 0.12;      // 帧速率（12 FPS）
        this.Animation_Down.loop = true;
        this.addChild(this.Animation_Down);

        this.Animation_Left.animationSpeed = 0.12;      // 帧速率（12 FPS）
        this.Animation_Left.loop = true;
        this.addChild(this.Animation_Left);

        this.Animation_Right.animationSpeed = 0.12;      // 帧速率（12 FPS）
        this.Animation_Right.loop = true;
        this.addChild(this.Animation_Right);

        this.AddKeyboard();
    }

    public Dispose():void
    {
        this.RemoveKeyboard();
    }

    public override resize():void
    {
        super.resize();
    }
    
    public update(_time: Ticker) 
    {
        if (this.Keys.w) 
        {
            this.position.y -= this.fMoveSpeed * _time.deltaTime;
        }
        if (this.Keys.s) 
        {
            this.position.y += this.fMoveSpeed * _time.deltaTime;
        }
        if (this.Keys.a) 
        {
            this.position.x -= this.fMoveSpeed * _time.deltaTime;
        }
        if (this.Keys.d) 
        {
            this.position.x += this.fMoveSpeed * _time.deltaTime;
        }
    }

    public SwitchTankType(nType:number):void
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
        
        this.Animation_Up.textures = [mSprite1, mSprite2];
        this.Animation_Right.textures = [mSprite3, mSprite4];
        this.Animation_Down.textures = [mSprite5, mSprite6];
        this.Animation_Left.textures = [mSprite7, mSprite8];
    }

    private AddKeyboard():void
    {
        window.addEventListener('keydown', this.OnKeyDown);
        window.addEventListener('keyup', this.OnKeyUp);
    }

    private RemoveKeyboard():void
    {
        window.removeEventListener('keydown', this.OnKeyDown);
        window.removeEventListener('keyup', this.OnKeyUp);
    }

    private OnKeyDown(e:KeyboardEvent):void
    {
        if (e.key in this.Keys)
        {
            this.Keys[e.key as keyof typeof this.Keys] = true;
        }
    }

    private OnKeyUp(e:KeyboardEvent):void
    {
        if (e.key in this.Keys)
        {
            this.Keys[e.key as keyof typeof this.Keys] = false;
        }
    }
    
}