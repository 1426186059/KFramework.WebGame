import { Container, Sprite } from "pixi.js";

export class TileBase extends Container
{
    
}

export class Tile extends TileBase
{
    public readonly mSprite:Sprite = new Sprite();
    constructor()
    {
        super();
        this.addChild(this.mSprite);
        this.mSprite.position.set(0, 0);
        this.mSprite.setSize(100, 100);
    }
}