import type { Ticker } from "pixi.js";
import { Container, Graphics, Sprite, Texture } from "pixi.js";
import { engine } from "../../getEngine";
import { PausePopup } from "../../popups/PausePopup";

export class StartScreen extends Container 
{
  public static assetBundles = ["main"];
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

    let m_Tank = new Sprite(Texture.from("Player1_8"));
    m_Tank.pivot.set(m_Tank.texture.width * 0.5, m_Tank.texture.height * 0.5);
    m_Tank.position.x = engine().renderer.width * 0.5 - 100;
    m_Tank.position.y = engine().renderer.height * 0.5;
    this.addChild(m_Tank);
  }

  public prepare() 
  {
    
  }

  public update(_time: Ticker) 
  {

  }

  public reset() 
  {
    
  }

  public async show(): Promise<void> 
  {
      engine().audio.bgm.play("main/sounds/bgm-main.mp3", { volume: 0.5 });  
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
