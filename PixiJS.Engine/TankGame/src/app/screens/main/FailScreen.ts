import { Point, Ticker } from "pixi.js";
import { Container, Graphics, Sprite, Texture } from "pixi.js";
import { engine } from "../../getEngine";
import { PausePopup } from "../../popups/PausePopup";
import { KeyBoard, KeyCode } from "../../../KFramework.PixiJS/Input/KeyBoard";
import { IDisposable } from "../../../KFramework.PixiJS/Tool/IDisposable";
import { KUpdateMgr } from "../../../KFramework.PixiJS/Timer/KUpdateMgr";
import { GameScene } from "../../Game/GameScene";
import { KTween } from "../../../KFramework.PixiJS/KTweeen/KTween";
import { StartScreen } from "./StartScreen";

export class FailScreen extends Container implements IDisposable
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

    let m_Background = new Sprite(Texture.from("main/MyRes/Textures/UIGameOver.png"));
    m_Background.pivot.set(m_Background.texture.width * 0.5, m_Background.texture.height * 0.5);
    m_Background.position.x = engine().renderer.width * 0.5;
    m_Background.position.y = engine().renderer.height * 0.5;
    this.addChild(m_Background);

    KTween.delayedCall(3, async ()=> {
       this.destroy();
       await engine().navigation.showScreen(StartScreen);
    });
    
  }

  public Dispose(): void 
  {
    this.mKeyBoard.Dispose(); 
    this.destroy();
  }
}
