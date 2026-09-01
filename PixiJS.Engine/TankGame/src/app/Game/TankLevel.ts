import { Assets, Container, Graphics, Point, Sprite, Texture, Ticker } from 'pixi.js';
import { TankLevelConfig } from './TankLevelConfig';
import { Tile, TileBase } from './TileBase';
import { engine } from '../getEngine';
import { IDisposable } from '../../KFramework.PixiJS/Tool/IDisposable';
import { Tank_My } from './Tank_My';
import { Tank_Enemy } from './Tank_Enemy';
import { Tile_Block } from './Tile_Block';
import { Shell } from './Shell';
import { ObjPool } from '../../KFramework.PixiJS/Tool/ObjPool';

export class TankLevel implements IDisposable
{
    private nLevelIndex:number = 0;
    private tiles:(TileBase | null)[][] = [];

    public readonly SceneRoot:Container = new Container();
    private _disoised:boolean = false;
    private  m_Background:Sprite | null = null;
    public fTileScaleCoef:number = 1.0;

    public readonly ShellPool:ObjPool<Shell> = new ObjPool<Shell>(()=> new Shell(this));
    
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
        this.fTileScaleCoef = (engine().renderer.height - 100) / TankLevelConfig.MapHeight / 32.0;
        this.SceneRoot.scale.set(this.fTileScaleCoef, this.fTileScaleCoef);
        this.SceneRoot.position = new Point(engine().renderer.width / 2.0, engine().renderer.height / 2.0);

        this.nLevelIndex = nLevelIndex;
        this.AysncInit();
    }

    private AysncInit():void
    {   
        this.m_Background = new Sprite(Texture.from("main/MyRes/Textures/BackGround.jpg"));
        this.SceneRoot.addChild(this.m_Background);
        this.m_Background.scale = new Point(3, 3);
        this.m_Background.pivot = new Point(this.m_Background.texture.width / 2, this.m_Background.texture.height / 2);
        this.m_Background.position = new Point(0, 0);
        this.m_Background.tint = 0x00FF00;
        
        //坦克图背景是个黑图，不是透明的
        // const g = new Graphics();
        // g.rect(this.m_Background.position.x,  this.m_Background.position.y, this.m_Background.width, this.m_Background.height);
        // g.fill(0x00FF00, 0.5);
        // this.SceneRoot.addChild(g);
        
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

        for (let y = 0; y < TankLevelConfig.MapHeight; y++)
        {
            this.tiles[y] = [];
            if(lines[y] == undefined)
            {
                lines[y] = "";
            }

            for (let x = 0; x < TankLevelConfig.MapWidth; ++x)
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
    
    private LoadTile(tileType:string, x:number, y:number):TileBase | null
    {
        switch (tileType)
        {
            case "":
            case " ":
            case ".":
                return null;
            case 'E': //敌人出生点
                return this.LoadEnemyTile(x, y);
            case 'P': //玩家出生点
                return this.LoadPlayerTile(x, y);
            
            case "#": //砖块
                return this.LoadCommonTile(x, y, "Map_0"); 
            case "*":  //铁板
                return this.LoadCommonTile(x, y, "Map_1");    
            case "~":  //水
                return this.LoadCommonTile(x, y, "Map_2");
            case "@":  //老窝
                return this.LoadCommonTile(x, y, "Map_5");     
            default:
                throw "LoadTile error: " + tileType;
        }
    }

    public GetTile(x:number, y:number):TileBase | null 
    {
        if(x < 0 || x >= TankLevelConfig.MapWidth)
        {
            return null;
        }

        if(y < 0 || y >= TankLevelConfig.MapHeight)
        {
            return null;
        }

        return this.tiles[y][x];
    }
    
    private LoadCommonTile(x:number, y:number, strName:string):TileBase 
    {
        let mTile = new Tile_Block(this, x, y);
        this.SceneRoot.addChild(mTile);
        mTile.position = this.GetTilePos(x, y);
        mTile.mSprite.texture = Texture.from(strName);
        mTile.showBounds();
        return mTile;
    }

    private LoadPlayerTile(x:number, y:number):TileBase 
    {
        let mTile = new Tank_My(this, x, y);
        this.SceneRoot.addChild(mTile);
        mTile.position = this.GetTilePos(x, y);
       // mTile.showBounds();
        return mTile;
    }

    private LoadEnemyTile(x:number, y:number):TileBase 
    {
        let mTile = new Tank_Enemy(this, x, y);
        this.SceneRoot.addChild(mTile);
        mTile.position = this.GetTilePos(x, y);
        //mTile.showBounds();
        return mTile;
    }

    public GetTilePos(x:number, y:number):Point 
    {
        let localPos:Point = new Point(0, 0);
        localPos.x += (x - TankLevelConfig.MapWidth / 2) * TankLevelConfig.TileWidth;
        localPos.y += (y - TankLevelConfig.MapHeight / 2) * TankLevelConfig.TileHeight;
        return localPos;
    }
    
    public resize():void
    {
        console.log("resize: " + "width: " + engine().renderer.width + " height: " + engine().renderer.height);
        this.fTileScaleCoef = (engine().renderer.height - 100) / TankLevelConfig.MapHeight / TankLevelConfig.TileHeight;
        this.SceneRoot.scale.set(this.fTileScaleCoef, this.fTileScaleCoef);
        this.SceneRoot.position = new Point(engine().renderer.width / 2.0, engine().renderer.height / 2.0);
    }
    
    public update(_time: Ticker) 
    {
        this.SceneRoot.children.forEach(element => 
        {
            if(element instanceof TileBase)
            {
                element.update(_time);
            }
        });
    }
}