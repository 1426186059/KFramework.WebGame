
export class TankLevelConfig
{
    public static readonly MapWidth:number = 21;
    public static readonly MapHeight:number = 20;
    public static readonly TileWidth:number = 32;
    public static readonly TileHeight:number = 32;
    
    public static GetLevelEnemyCount(nLevelIndex:number)
    {
        return nLevelIndex * 10
    }
}

export enum TankDirection {
    UP = 'UP',
    DOWN = 'DOWN',
    LEFT = 'LEFT',
    RIGHT = 'RIGHT'
}

export enum E_TILE_TYPE
{
    Wall = 1,
    Barriar = 2,
    Grass = 3,
    Water = 4,
    Heart = 5,
}

export enum E_TANK_CAMP_TYPE
{
    Player = 1,
    Enemy = 2,
}