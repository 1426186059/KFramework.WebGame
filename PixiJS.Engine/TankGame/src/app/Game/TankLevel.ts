import { Assets } from 'pixi.js';
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
        const path = "MyRes/Levels/${this.nLevelIndex.toString().padStart(2, '0')}";
        const text = await Assets.load<string>(path);
        console.log(text);

        
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

    //   tiles = new TileBase[nMaxWidth, lines.Count];
    //    Tile.TileHeight = KSceneMgr.Game.GraphicsDevice.Viewport.Height / (float)Height;
    //    Tile.TileWidth = Tile.TileHeight;
    //    Tile.TileFloorY = KSceneMgr.Game.GraphicsDevice.Viewport.Height;
    //    Tile.TileMinPosX = 0;
    //    Tile.TileMaxPosX = nMaxWidth * Tile.TileWidth - KSceneMgr.Game.GraphicsDevice.Viewport.Width;

        for (let y = 0; y < TankLevelConfig.Height; y++)
        {
            for (let x = 0; x < TankLevelConfig.Width; ++x)
            {
                let tileType:string = ' ';
                if (x < lines[y].length)
                {
                    tileType = lines[y][x];
                }
                this.tiles[y][x] = LoadTile(tileType, x, y);
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
                return LoadExitTile(x, y, TileCollision.Exit);
            case 'P': //玩家出生点
                return LoadStartTile(x, y);
            
            case "#": //砖块
                return LoadTile(x, y, mSpriteSheet_misc3Atlas, "misc-3_68", TileCollision.Impassable); 
            case "*":  //铁板
                return LoadTile(x, y, mSpriteSheet_misc3Atlas, "misc-3_68", TileCollision.Impassable);    
            case "*":  //水
                return LoadTile(x, y, mSpriteSheet_misc3Atlas, "misc-3_68", TileCollision.Impassable);    
            default:
                break;
        }

        throw "LoadTile error";
    }

    private LoadTile2():TileBase
    {

    }

}