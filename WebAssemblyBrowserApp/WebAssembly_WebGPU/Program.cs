using System;
using System.Runtime.InteropServices.JavaScript;
using WebAssemblyBrowserApp.Engine;
using WebAssemblyBrowserApp.Games;

static void Step(string label) => JsDiag.Step(label);

Step("01: top-level start");
var ge = GameEngine.Instance;
Step("02: RegisterScene");
ge.RegisterScene(new BreakoutScene());
Step("03: ge.Initialize(#game)");
ge.Initialize("#game");
Step("04: ge.Start(breakout)");
ge.Start("breakout");
Step("05: EngineLoop.Start → requestAnimationFrame frame loop");
EngineLoop.Start();
Step("06: Main RETURN (resolve runMain Promise → rAF will fire!)");
return;

// --- JSInterop ---
public static partial class JsDiag
{
    [JSImport("diag.step", "main.js")] internal static partial void Step(string label);
}

public static partial class Diagnostics
{
    [JSExport] public static string Ping(string input) => "pong:" + input;
}

public static partial class GameBridge
{
    [JSExport] public static void Tick(double dt) => GameEngine.Instance.Tick(dt);
}

public static partial class EngineLoop
{
    [JSImport("engine.startLoop", "main.js")]
    internal static partial void Start();
}
