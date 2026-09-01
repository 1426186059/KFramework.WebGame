import type { Ticker } from "pixi.js";

/**
 * 全局时间工具 —— 对齐 Unity 的 UnityEngine.Time，单位为【秒】。
 *
 * 注意 Pixi 的 {@link Ticker.deltaTime} 是无量纲的帧倍数（60FPS 时约等于 1），
 * 而 KTween 用的时间是秒，所以这里统一用 Ticker.deltaMS / 1000 换算。
 * 用法：在主循环里调用 {@link KTime.From}（KTween.update 内已自动调用）。
 */
export class KTime {
  /** 游戏开始以来经过的时间（受 timeScale 影响，秒） */
  public static time: number = 0;
  /** 完成上一帧所用的时间（受 timeScale 影响，且被 maximumDeltaTime 截断） */
  public static deltaTime: number = 0;
  /** 不受 timeScale 影响的上一帧时间（仍被 maximumDeltaTime 截断） */
  public static unscaledDeltaTime: number = 0;
  /** 不受 timeScale 影响的时间（秒） */
  public static unscaledTime: number = 0;
  /** 平滑后的 deltaTime（对最近若干帧做加权平均，避免单帧抖动） */
  public static smoothDeltaTime: number = 0;
  /** 已渲染的帧数 */
  public static frameCount: number = 0;

  /** 时间缩放（1 = 正常，0 = 暂停，2 = 两倍速） */
  public static timeScale: number = 1;
  /** 一帧允许的最大 deltaTime（秒），超过会被截断 */
  public static maximumDeltaTime: number = 0.333;

  private static mSmoothFrameCount: number = 0;

  /** 从 Pixi 的 Ticker 推进一帧 */
  public static From(ticker: Ticker): void {
    KTime.Update(ticker.deltaMS / 1000);
  }

  /**
   * 推进一帧
   * @param rawDeltaSeconds 这一帧的真实耗时（秒），尚未截断、未乘 timeScale
   */
  public static Update(rawDeltaSeconds: number): void {
    const raw: number = Math.max(rawDeltaSeconds, 0);

    // 与 Unity 一致：先截断到 maximumDeltaTime，再乘 timeScale
    KTime.unscaledDeltaTime = Math.min(raw, KTime.maximumDeltaTime);
    KTime.deltaTime = KTime.unscaledDeltaTime * KTime.timeScale;

    KTime.time += KTime.deltaTime;
    KTime.unscaledTime += KTime.unscaledDeltaTime;
    KTime.frameCount++;

    KTime.UpdateSmoothDeltaTime();
  }

  private static UpdateSmoothDeltaTime(): void {
    // 累积平均：前 20 帧逐渐收敛，之后等价于约 20 帧的移动平均
    KTime.mSmoothFrameCount = Math.min(KTime.mSmoothFrameCount + 1, 20);
    KTime.smoothDeltaTime +=
      (KTime.deltaTime - KTime.smoothDeltaTime) / KTime.mSmoothFrameCount;
  }
}
