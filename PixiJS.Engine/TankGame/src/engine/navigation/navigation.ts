import type { Ticker } from "pixi.js";
import { Assets, BigPool, Container } from "pixi.js";

import type { CreationEngine } from "../engine";
import { GameScene } from "../../app/Game/GameScene";

/** Interface for app screens */
interface AppScreen extends Container {
  /** Show the screen */
  show?(): Promise<void>;
  /** Hide the screen */
  hide?(): Promise<void>;
  /** Pause the screen */
  pause?(): Promise<void>;
  /** Resume the screen */
  resume?(): Promise<void>;
  /** Prepare screen, before showing */
  prepare?(): void;
  /** Reset screen, after hidden */
  reset?(): void;
  /** Update the screen, passing delta time/step */
  update?(time: Ticker): void;
  /** Resize the screen */
  resize?(width: number, height: number): void;
  /** Blur the screen */
  blur?(): void;
  /** Focus the screen */
  focus?(): void;
  /** Method to react on assets loading progress */
  onLoad?: (progress: number) => void;
}

/** Interface for app screens constructors */
interface AppScreenConstructor {
  new (): AppScreen;
  /** List of assets bundles required by the screen */
  assetBundles?: string[];
}

export class Navigation 
{
  public app!: CreationEngine;
  public width = 0;
  public height = 0;
  public background?: AppScreen;
  public currentScreen?: AppScreen;
  public currentPopup?: AppScreen;

  public init(app: CreationEngine) 
  {
    this.app = app;
  }

  public setBackground(ctor: AppScreenConstructor) 
  {
    this.background = new ctor();
    this.addAndShowScreen(this.background);
  }

  private async addAndShowScreen(screen: AppScreen) 
  {
    GameScene.GetInstance().UIRoot_Layer.addChild(screen);
    if (screen.prepare) 
    {
      screen.prepare();
    }

    if (screen.resize)
    {
      screen.resize(this.width, this.height);
    }

    if (screen.show) 
    {
      screen.interactiveChildren = false;
      await screen.show();
      screen.interactiveChildren = true;
    }
  }

  private async hideAndRemoveScreen(screen: AppScreen) 
  {
    screen.interactiveChildren = false;
    if (screen.hide) 
    {
        await screen.hide();
    }

    if (screen.parent) 
    {
        screen.parent.removeChild(screen);
    }

    if (screen.reset) 
    {
        screen.reset();
    }
  }

  public async showScreen(ctor: AppScreenConstructor) 
  {
    if (this.currentScreen) 
    {
      this.currentScreen.interactiveChildren = false;
    }
    
    if (ctor.assetBundles) 
    {
      await Assets.loadBundle(ctor.assetBundles, (progress) => 
        {
        if (this.currentScreen?.onLoad) {
          this.currentScreen.onLoad(progress * 100);
        }
      });
    }

    if (this.currentScreen?.onLoad)
    {
      this.currentScreen.onLoad(100);
    }

    if (this.currentScreen) 
    {
      await this.hideAndRemoveScreen(this.currentScreen);
    }

    this.currentScreen = BigPool.get(ctor);
    await this.addAndShowScreen(this.currentScreen);
  }

  /**
   * Resize screens
   * @param width Viewport width
   * @param height Viewport height
   */
  public resize(width: number, height: number) {
    this.width = width;
    this.height = height;
    this.currentScreen?.resize?.(width, height);
    this.currentPopup?.resize?.(width, height);
    this.background?.resize?.(width, height);
  }

  /**
   * Show up a popup over current screen
   */
  public async presentPopup(ctor: AppScreenConstructor) 
  {
      if (this.currentScreen) 
      {
        this.currentScreen.interactiveChildren = false;
        await this.currentScreen.pause?.();
      }

      if (this.currentPopup) 
      {
        await this.hideAndRemoveScreen(this.currentPopup);
      }

      this.currentPopup = new ctor();
      await this.addAndShowScreen(this.currentPopup);
  }

  /**
   * Dismiss current popup, if there is one
   */
  public async dismissPopup() 
  {
    if (!this.currentPopup) return;
    const popup = this.currentPopup;
    this.currentPopup = undefined;
    await this.hideAndRemoveScreen(popup);
    if (this.currentScreen) 
    {
      this.currentScreen.interactiveChildren = true;
      this.currentScreen.resume?.();
    }
  }


  public blur() 
  {
    this.currentScreen?.blur?.();
    this.currentPopup?.blur?.();
    this.background?.blur?.();
  }

  public focus()
  {
    this.currentScreen?.focus?.();
    this.currentPopup?.focus?.();
    this.background?.focus?.();
  }
}
