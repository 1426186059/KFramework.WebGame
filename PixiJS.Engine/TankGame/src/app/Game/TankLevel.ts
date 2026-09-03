import { Assets, Container, Point, Sprite, Texture, Ticker } from 'pixi.js';
import { E_BORN_TYPE, E_TILE_TYPE, TankLevelConfig } from './TankLevelConfig';
import { TileBase } from './TileBase';
import { engine } from '../getEngine';
import { IDisposable } from '../../KFramework.PixiJS/Tool/IDisposable';
import { Tank_My } from './Tank_My';
import { Tank_Enemy } from './Tank_Enemy';
import { Tile_Block } from './Tile_Block';
import { Shell } from './Shell';
import { PoolContainer } from '../../KFramework.PixiJS/Tool/PoolContainer';
import { BornEffect } from './BornEffect';
import { ExplodeEffect } from './ExplodeEffect';
import { BornPoint_Enemy } from './BornPoint_Enemy';
import { BornPoint_Player } from './BornPoint_Player';
import { GameScene } from './GameScene';
import { Tile_Home } from './Tile_Home ';
import { Tank_Base } from './Tank_Base';
import { RandomTool } from '../../KFramework.PixiJS/Tool/RandomTool';
import { PixiTool } from '../../KFramework.PixiJS/Tool/PixiTool';

export class TankLevel implements IDisposable
{
    public nLevelIndex:number = 0;
    private tiles:(TileBase | null)[][] = [];

    public readonly SceneRoot:Container = new Container();
    public readonly BGRoot:Container = new Container();
    public readonly Map1Root:Container = new Container();
    public readonly TankRoot:Container = new Container();
    public readonly Map2Root:Container = new Container();
    public readonly EffectRoot:Container = new Container();

    private _disoised:boolean = false;
    private  m_Background:Sprite | null = null;
    public fTileScaleCoef:number = 1.0;

    public readonly ShellPool:PoolContainer<Shell> = new PoolContainer<Shell>(()=> new Shell(this));
    public readonly BornEffectPool:PoolContainer<BornEffect> = new PoolContainer<BornEffect>(()=> new BornEffect(this));
    public readonly ExplodeEffectPool:PoolContainer<ExplodeEffect> = new PoolContainer<ExplodeEffect>(()=> new ExplodeEffect(this));
    public readonly Tank_EnemyPool:PoolContainer<Tank_Enemy> = new PoolContainer<Tank_Enemy>(()=> new Tank_Enemy(this));

    public readonly TankList:Tank_Base[] = [];
    public readonly ShellList:Shell[] = [];
    
    public readonly MaxPosX:number = 0;
    public readonly MinPosX:number = 0;
    public readonly MaxPosY:number = 0;
    public readonly MinPosY:number = 0;

    public nKillEnemyCount:number = 0; 
    private nNeddAllocEnemyCount:number = 0; 
    private mBornPoint_EnemyList:BornPoint_Enemy[] = [];
    constructor()
    {
        this.m_Background = null;
        
        this.MaxPosX = TankLevelConfig.MapWidth * TankLevelConfig.TileWidth / 2;
        this.MinPosX = -TankLevelConfig.MapWidth * TankLevelConfig.TileWidth / 2;
        this.MaxPosY = TankLevelConfig.MapHeight * TankLevelConfig.TileHeight / 2;
        this.MinPosY = -TankLevelConfig.MapHeight * TankLevelConfig.TileHeight / 2;
    }

    public Dispose():void
    {
        if(!this._disoised)
        {
            this._disoised = true;
           this.SceneRoot.destroy();
        }
    }

    public Init(nLevelIndex:number):void
    { 
        this.nNeddAllocEnemyCount = TankLevelConfig.GetLevelEnemyCount(nLevelIndex);

        engine().audio.sfx.play("main/MyRes/Audio/Start.wav");
        GameScene.GetInstance().World_Layer.addChild(this.SceneRoot);
        this.SceneRoot.addChild(this.BGRoot);
        this.SceneRoot.addChild(this.Map1Root);
        this.SceneRoot.addChild(this.TankRoot);
        this.SceneRoot.addChild(this.Map2Root);
        this.SceneRoot.addChild(this.EffectRoot);
        
        this.fTileScaleCoef = (engine().renderer.height - 100) / TankLevelConfig.MapHeight / 32.0;
        this.SceneRoot.scale.set(this.fTileScaleCoef, this.fTileScaleCoef);
        this.SceneRoot.position = new Point(engine().renderer.width / 2.0, engine().renderer.height / 2.0);

        this.nLevelIndex = nLevelIndex;
        
        this.m_Background = new Sprite(Texture.from("main/MyRes/Textures/BackGround.jpg"));
        this.BGRoot.addChild(this.m_Background);
        this.m_Background.setSize(
            TankLevelConfig.MapWidth * TankLevelConfig.TileWidth, 
            TankLevelConfig.MapHeight * TankLevelConfig.TileHeight);
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

        let nSumEnemyCount = TankLevelConfig.GetLevelEnemyCount(this.nLevelIndex);
        let nAllocCount:number = Math.ceil(nSumEnemyCount / this.mBornPoint_EnemyList.length);
        for(let A of this.mBornPoint_EnemyList)
        {
            nAllocCount = Math.min(nAllocCount, nSumEnemyCount);
            A.SetEnemyCount(nAllocCount);
            nSumEnemyCount -= nAllocCount;
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
                return this.LoadCommonTile(x, y, "Map_0", E_TILE_TYPE.Wall); 
            case "*":  //铁板
                return this.LoadCommonTile(x, y, "Map_1", E_TILE_TYPE.Barriar);    
            case "~":  //水
                return this.LoadCommonTile(x, y, "Map_3", E_TILE_TYPE.Water);
            case "^":  //草
                return this.LoadCommonTile(x, y, "Map_2", E_TILE_TYPE.Grass);

            case "@":  //老窝
                return this.LoadTile_Home(x, y, "Map_5");     
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
    
    public SetTileNull(x:number, y:number):void
    {
        let mTile = this.GetTile(x, y);
        if(mTile != null)
        {
            this.tiles[y][x] = null;
        }
    }
    
    private LoadCommonTile(x:number, y:number, strName:string, nType:E_TILE_TYPE):TileBase 
    {
        let mTile = new Tile_Block(this, x, y);
        if(nType == E_TILE_TYPE.Grass)
        {
            this.Map2Root.addChild(mTile);
        }
        else
        {
            this.Map1Root.addChild(mTile);
        }

        mTile.position = this.GetTilePos(x, y);
        mTile.mSprite.texture = Texture.from(strName);
        mTile.nType = nType;
        return mTile;
    }

    private LoadTile_Home(x:number, y:number, strName:string):TileBase | null
    {
        let mTile = new Tile_Home(this, x, y);
        this.Map1Root.addChild(mTile);
        mTile.position = this.GetTilePos(x, y);
        mTile.mSprite.texture = Texture.from(strName);
        return mTile;
    }

    private LoadPlayerTile(x:number, y:number):TileBase | null
    {
        let mPos = this.GetTilePos(x, y);
        new BornPoint_Player(this, mPos); 
        return null;
    }

    private LoadEnemyTile(x:number, y:number):TileBase | null
    {
        let mPos = this.GetTilePos(x, y);
        let mPoint = new BornPoint_Enemy(this, mPos); 
        this.mBornPoint_EnemyList.push(mPoint);
        return null;
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
    
    public update() 
    {
        this.TankRoot.children.forEach(element => 
        {
            if(element instanceof TileBase)
            {
                element.update();
            }
        });
    }
}