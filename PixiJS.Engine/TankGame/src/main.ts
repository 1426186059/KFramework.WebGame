import { setEngine } from "./app/getEngine";
import { LoadScreen } from "./app/screens/LoadScreen";
import { MainScreen } from "./app/screens/main/MainScreen";
import { userSettings } from "./app/utils/userSettings";
import { CreationEngine } from "./engine/engine";
import "@pixi/sound";
import { initDevtools } from '@pixi/devtools';
import { GameScene } from "./app/Game/GameScene";
// import "@esotericsoftware/spine-pixi-v8";

// Create a new creation engine instance
const engine = new CreationEngine();
setEngine(engine);

async function MainFunc()
{
  await engine.init({
    background: "#1E1E1E",
    resizeOptions: { minWidth: 768, minHeight: 1024, letterbox: false },
  });

  initDevtools({ app:engine});

  userSettings.init();

  await GameScene.GetInstance().Init();
}

MainFunc();