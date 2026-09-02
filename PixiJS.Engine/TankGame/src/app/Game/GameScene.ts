import { Container } from "pixi.js";
import { TankLevel } from "./TankLevel";
import { engine } from "../getEngine";

export class GameScene extends Container
{
    private static readonly m_Instance: GameScene = new GameScene();
    public static GetInstance(): GameScene 
    {
        return GameScene.m_Instance;
    }

    public mTankLevel:TankLevel | null = null;
    public nLevelIndex:number = 0;

    private constructor()
    {
        super();
        engine().stage.addChild(this);
    }

    public LoadLevel():void
    {

        if(this.mTankLevel != null)
        {
            this.mTankLevel.Dispose();
            this.mTankLevel = null;
        }

        this.mTankLevel = new TankLevel();
        this.mTankLevel.Init(this.nLevelIndex);
    }

    public LoadNextLevel():void
    {
        this.nLevelIndex++;
        this.LoadLevel();
    }

}