import { Container, Sprite } from "pixi.js";

export class TileBase extends Container
{
    
}

export class Tile extends TileBase
{
    public mSprite:Sprite | null = null;
}