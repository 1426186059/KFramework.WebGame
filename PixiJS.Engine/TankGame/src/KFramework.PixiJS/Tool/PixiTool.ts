import { Container } from "pixi.js";

export class PixiTool
{
    public static clamp(value: number, min: number, max: number): number 
    {
        return Math.max(min, Math.min(value, max));
    }
    
    public static isAlive(obj: Container | null):boolean
    {
        return obj !== null && obj !== undefined && !obj.destroyed;
    }
}