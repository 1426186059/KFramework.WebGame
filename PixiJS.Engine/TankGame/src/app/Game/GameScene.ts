import { Container } from "pixi.js";
import { TankLevel } from "./TankLevel";
import { engine } from "../getEngine";
import { LoadScreen } from "../screens/LoadScreen";
import { MainScreen } from "../screens/main/MainScreen";
import { StartScreen } from "../screens/main/StartScreen";
import { KUpdateMgr } from "../../KFramework.PixiJS/Timer/KUpdateMgr";
import { IDisposable } from "../../KFramework.PixiJS/Tool/IDisposable";

export class GameScene implements IDisposable
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

    public readonly World_Layer:Container = new Container({label:"World_Layer"});
    public readonly UIRoot_Layer:Container = new Container({label:"UIRoot_Layer"});
    
    public readonly OnResizeFunc = (width: number, height: number) => this.resize(width, height);
    private constructor()
    {
        engine().stage.addChild(this.World_Layer);
        engine().stage.addChild(this.UIRoot_Layer);
        KUpdateMgr.AddListener(this.update, this);
        engine().renderer.on("resize", this.OnResizeFunc);
    }

    public Dispose(): void 
    {
        KUpdateMgr.RemoveListener(this.update, this);
        engine().renderer.off("resize", this.OnResizeFunc);
        this.World_Layer.destroy();
        this.UIRoot_Layer.destroy();
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