import { Point, Ticker } from "pixi.js";
import { Container, Graphics, Sprite, Texture } from "pixi.js";
import { engine } from "../../getEngine";
import { PausePopup } from "../../popups/PausePopup";
import { KeyBoard, KeyCode } from "../../../KFramework.PixiJS/Input/KeyBoard";
import { IDisposable } from "../../../KFramework.PixiJS/Tool/IDisposable";
import { KUpdateMgr } from "../../../KFramework.PixiJS/Timer/KUpdateMgr";
import { GameScene } from "../../Game/GameScene";

export class StartScreen extends Container implements IDisposable
{
  public static assetBundles = ["main"];

  private opt1Pos:Point;
  private opt2Pos:Point;
  private m_Tank:Sprite;

  private readonly mKeyBoard:KeyBoard = new KeyBoard();
  constructor() 
  {
    super();

    const g = new Graphics();
    g.rect(0, 0, engine().renderer.width, engine().renderer.height);
    g.fill(0x000000);
    g.alpha = 1;
    this.addChild(g)

    let m_Background = new Sprite(Texture.from("main/MyRes/Textures/Title.png"));
    m_Background.pivot.set(m_Background.texture.width * 0.5, m_Background.texture.height * 0.5);
    m_Background.position.x = engine().renderer.width * 0.5;
    m_Background.position.y = engine().renderer.height * 0.5;
    this.addChild(m_Background);

    this.opt1Pos = new Point(engine().renderer.width * 0.5 - 100, engine().renderer.height * 0.5 + 70);
    this.opt2Pos = new Point(engine().renderer.width * 0.5 - 100, engine().renderer.height * 0.5 + 100);
    
    this.m_Tank = new Sprite(Texture.from("Player1_8"));
    this.m_Tank.pivot.set(this.m_Tank.texture.width * 0.5, this.m_Tank.texture.height * 0.5);
    this.m_Tank.position = this.opt1Pos;
    this.addChild(this.m_Tank);

    KUpdateMgr.AddListener(this.update, this);
  }

  public Dispose(): void 
  {
    KUpdateMgr.RemoveListener(this.update, this);
    this.mKeyBoard.Dispose(); 
    this.destroy();
  }

  public prepare() 
  {
    
  }

  public update() 
  {
     if(this.mKeyBoard.GetKeyDown(KeyCode.KeyW) || this.mKeyBoard.GetKeyDown(KeyCode.ArrowUp))
     {
        this.m_Tank.position = this.opt1Pos;
     }
     else if(this.mKeyBoard.GetKeyDown(KeyCode.KeyS) || this.mKeyBoard.GetKeyDown(KeyCode.ArrowDown))
     {
        this.m_Tank.position = this.opt2Pos;
     }

     if(this.mKeyBoard.GetKeyDown(KeyCode.Enter))
     {
        this.Dispose()
        GameScene.GetInstance().LoadLevel();
     }
  }

  public reset() 
  {
    
  }

  public async show(): Promise<void> 
  {
      //engine().audio.bgm.play("main/sounds/bgm-main.mp3", { volume: 0.5 });  
  }

  public async hide() 
  {

  }

  public blur() 
  {
      if (!engine().navigation.currentPopup) 
      {
          engine().navigation.presentPopup(PausePopup);
      }
  }
}
