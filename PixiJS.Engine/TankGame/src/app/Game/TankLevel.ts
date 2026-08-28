import { Assets, Sprite, Texture } from 'pixi.js';
import { TankLevelConfig } from './TankLevelConfig';
import { Tile, TileBase } from './Tile';

export class TankLevel
{
    private nLevelIndex:number = 0;
    private tiles:TileBase[][] = [];

    public Init(nLevelIndex:number):void
    {
        //初始化 坦克关卡
        this.nLevelIndex = nLevelIndex;
        this.AysncInit();
    }
    
    private async AysncInit():Promise<void>
    {
        //先加载图集:
        const path = "MyRes/Levels/${this.nLevelIndex.toString().padStart(2, '0')}";
        const text = await Assets.load<string>(path);
        console.log(text);
        
        this.LoadTiles(text);
    }

    private LoadTiles(content:string):void
    {
        let orilines: string[] = content.trim().split(/\r?\n/);
        let width:number = 0;
        let ignoreLineCount:number = 3;
        let nMaxWidth:number = 0;
        let lines:Array<string> = [];

        for (let i = 0; i < orilines.length; i++) 
        {
            while (ignoreLineCount-- > 0)
            {
                
            }

            let line:string = orilines[i];
            console.log(line);
            if(line.length > nMaxWidth)
            {
                nMaxWidth = line.length;
            }

            lines.push(line);
        }

        for (let y = 0; y < TankLevelConfig.Height; y++)
        {
            for (let x = 0; x < TankLevelConfig.Width; ++x)
            {
                let tileType:string = ' ';
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
            case " ":
            case ".":
                return new Tile();
            case 'E': //敌人出生点
                return this.LoadExitTile(x, y);
            case 'P': //玩家出生点
                return this.LoadStartTile(x, y);

            case "#": //砖块
                return this.LoadCommonTile(x, y, "misc-3_68"); 
            case "*":  //铁板
                return this.LoadCommonTile(x, y, "misc-3_68");    
            case "*":  //水
                return this.LoadCommonTile(x, y,  "misc-3_68");    
            default:
                break;
        }

        throw "LoadTile error";
    }
    
    private LoadCommonTile(x:number, y:number, strName:string):TileBase 
    {
        let mTile = new Tile();
        mTile.position.set(x, y);
        mTile.mSprite.texture = Texture.from(strName);
        return mTile;
    }

    private LoadStartTile(x:number, y:number):TileBase 
    {
        let mTile = new Tile();
        mTile.position.set(x, y);
        //mTile.mSprite.texture = Texture.from(strName);
        return mTile;
    }

    private LoadExitTile(x:number, y:number):TileBase 
    {
        let mTile = new Tile();
        mTile.position.set(x, y);
        //mTile.mSprite.texture = Texture.from(strName);
        return mTile;
    }
}