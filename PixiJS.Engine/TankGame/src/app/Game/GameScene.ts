import { Container, Ticker } from "pixi.js";
import { TankLevel } from "./TankLevel";
import { engine } from "../getEngine";
import { LoadScreen } from "../screens/LoadScreen";
import { MainScreen } from "../screens/main/MainScreen";
import { StartScreen } from "../screens/main/StartScreen";
import { KUpdateMgr } from "../../KFramework.PixiJS/Timer/KUpdateMgr";

export class GameScene extends Container
{
    private static m_Instance: GameScene;
    public static GetInstance(): GameScene 
    {
        if(GameScene.m_Instance == null)
        {
            GameScene.m_Instance = new GameScene();
        }
        return GameScene.m_Instance;
    }

    public mTankLevel:TankLevel | null = null;
    public nLevelIndex:number = 0;

    private constructor()
    {
        super();
        engine().stage.addChild(this);
        KUpdateMgr.AddListener(this.update, this);
    }
    
    public async Init()
    {
        await engine().navigation.showScreen(LoadScreen);
        await engine().navigation.showScreen(MainScreen);
        await engine().navigation.showScreen(StartScreen);
    }

    public update() 
    {
        if(this.mTankLevel != null)
        {
            this.mTankLevel.update();
        }
    }
    
    public resize(width: number, height: number) 
    {
        if(this.mTankLevel != null)
        {
            this.mTankLevel.resize();
        }
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