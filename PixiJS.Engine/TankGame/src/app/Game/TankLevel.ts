import { Assets, Container, Point, Sprite, Texture } from 'pixi.js';
import { TankLevelConfig } from './TankLevelConfig';
import { Tile, TileBase } from './Tile';
import { engine } from '../getEngine';
import { IDisposable } from '../../KFramework.PixiJS/Tool/IDisposable';

export class TankLevel implements IDisposable
{
    private nLevelIndex:number = 0;
    private tiles:TileBase[][] = [];

    private readonly SceneRoot:Container = new Container();
    private _disoised:boolean = false;
    private  m_Background:Sprite | null = null;
    public fTileScaleCoef:number = 1.0;
    constructor()
    {
        this.m_Background = null;
    }

    public Dispose():void
    {
        if(!this._disoised)
        {
            this._disoised = true;
            engine().stage.removeChild(this.SceneRoot);
        }
    }

    public Init(nLevelIndex:number):void
    {
        this.Dispose();
        this._disoised = false;

        engine().stage.addChild(this.SceneRoot);
        this.SceneRoot.width = engine().renderer.width;
        this.SceneRoot.height = engine().renderer.height;
        this.fTileScaleCoef = (engine().renderer.height - 100) / TankLevelConfig.Height / 32.0;

        this.nLevelIndex = nLevelIndex;
        this.AysncInit();
    }

    private AysncInit():void
    {   
        this.m_Background = new Sprite(Texture.from("main/MyRes/Textures/BackGround.jpg"));
        this.SceneRoot.addChild(this.m_Background);
        this.m_Background.scale = new Point(5, 5);
        this.m_Background.position = this.GetBackGroundPos();
        
        console.log("当前关卡:" + this.nLevelIndex);
        const path = `main/MyRes/Levels/${this.nLevelIndex.toString().padStart(2, '0')}.txt`;
        const text = Assets.get<string>(path);
        console.log(text);
        this.LoadLevel(text);
    }

    private LoadLevel(content:string):void
    {
        let orilines: string[] = content.trim().split(/\r?\n/);
        let ignoreLineCount:number = 3;
        let nMaxWidth:number = 0;
        let lines:string[] = [];

        for (let i = 0; i < orilines.length; i++) 
        {
            if(ignoreLineCount-- > 0)
            {
                continue;
            }

            let line:string = orilines[i];
            if(line.length > nMaxWidth)
            {
                nMaxWidth = line.length;
            }

            lines.push(line);
        }

        for (let y = 0; y < TankLevelConfig.Height; y++)
        {
            this.tiles[y] = [];
            if(lines[y] == undefined)
            {
                lines[y] = "";
            }

            for (let x = 0; x < TankLevelConfig.Width; ++x)
            {
                let tileType:string = " ";
                if (x < lines[y].length)
                {
                    tileType = lines[y][x];
                }
                this.tiles[y][x] = this.LoadTile(tileType, x, y);
            }
        }

    }
    
    private LoadTile(tileType:string, x:number, y:number):TileBase
    {
        switch (tileType)
        {
            case "":
            case " ":
            case ".":
                return new Tile(this, x, y);
            case 'E': //敌人出生点
                return this.LoadExitTile(x, y);
            case 'P': //玩家出生点
                return this.LoadStartTile(x, y);
            
            case "#": //砖块
                return this.LoadCommonTile(x, y, "Map_0"); 
            case "*":  //铁板
                return this.LoadCommonTile(x, y, "Map_1");    
            case "~":  //水
                return this.LoadCommonTile(x, y,  "Map_2");
            case "@":  //老窝
                return this.LoadCommonTile(x, y,  "Map_5");     
            default:
                throw "LoadTile error: " + tileType;
        }
    }
    
    private LoadCommonTile(x:number, y:number, strName:string):TileBase 
    {
        let mTile = new Tile(this, x, y);
        this.SceneRoot.addChild(mTile);
        mTile.mSprite.texture = Texture.from(strName);
        return mTile;
    }

    private LoadStartTile(x:number, y:number):TileBase 
    {
        let mTile = new Tile(this, x, y);
        //mTile.mSprite.texture = Texture.from(strName);
        this.SceneRoot.addChild(mTile);
        return mTile;
    }

    private LoadExitTile(x:number, y:number):TileBase 
    {
        let mTile = new Tile(this, x, y);
        //mTile.mSprite.texture = Texture.from(strName);
        this.SceneRoot.addChild(mTile);
        return mTile;
    }

    public GetTilePos(x:number, y:number):Point 
    {
        let worldPos:Point = this.GameZoneLeftTop();
        worldPos.x += x * this.GetTileWidth();
        worldPos.y += y * this.GetTileWidth();
        return worldPos;
    }

    private ScreenCenter():Point
    {
        return new Point(engine().renderer.width / 2.0,  engine().renderer.height / 2.0);
    }
    
    private GameZoneLeftTop():Point
    {
        if(this.m_Background)
        {
            return new Point(
                (engine().renderer.width - this.GetTileWidth() * TankLevelConfig.Width) / 2.0, 
                50);
        }
        else
        {
            return new Point(0, 0);
        }
    }
    
    private GetBackGroundPos():Point
    {
        if(this.m_Background)
        {
            return new Point(
                (engine().renderer.width - this.m_Background.width) / 2.0, 
                (engine().renderer.height - this.m_Background.height) / 2.0);
        }
        else
        {
            return new Point(0, 0);
        }
    }

    public GetTileWidth():number
    {
        return 32 * this.fTileScaleCoef;
    }

    public resize()
    {
        this.fTileScaleCoef = (engine().renderer.height - 100) / TankLevelConfig.Height / 32.0;
        if(this.m_Background)
        {
            this.m_Background.position = this.GetBackGroundPos();
        }

        this.SceneRoot.children.forEach(element => 
        {
            if(element instanceof TileBase)
            {
                element.resize();
            }
        });
    }

}