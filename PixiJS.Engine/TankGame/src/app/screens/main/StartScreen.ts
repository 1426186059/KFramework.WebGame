import { FancyButton } from "@pixi/ui";
import { animate } from "motion";
import type { AnimationPlaybackControls } from "motion/react";
import type { Ticker } from "pixi.js";
import { Container } from "pixi.js";

import { engine } from "../../getEngine";
import { PausePopup } from "../../popups/PausePopup";
import { SettingsPopup } from "../../popups/SettingsPopup";
import { Button } from "../../ui/Button";

import { Bouncer } from "./Bouncer";
import { ResCenter } from "../../Game/ResCenter";
import { TankLevel } from "../../Game/TankLevel";

/** The screen that holds the app */
export class StartScreen extends Container {
  /** Assets bundles required by this screen */
  public static assetBundles = ["main"];

  public mainContainer: Container;
  private pauseButton: FancyButton;
  private settingsButton: FancyButton;
  private addButton: FancyButton;
  private removeButton: FancyButton;
  private bouncer: Bouncer;
  private paused = false;

  private mTankLevel:TankLevel;
  constructor() 
  {
    super();

  }

  /** Prepare the screen just before showing */
  public prepare() 
  {
    
  }

  public update(_time: Ticker) 
  {

  }

  /** Fully reset */
  public reset() 
  {
    
  }

  /** Show screen with animations */
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
