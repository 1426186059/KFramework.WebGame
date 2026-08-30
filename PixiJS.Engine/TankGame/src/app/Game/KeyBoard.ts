import { Assets, Spritesheet } from "pixi.js";

export class KeyBoard
{
    private constructor() {}
    private static readonly m_Instance:KeyBoard = new KeyBoard();
    public static GetSingleton():KeyBoard
    {
        return KeyBoard.m_Instance;
    }
    
    public Update():void
    {
        
    }
        
}