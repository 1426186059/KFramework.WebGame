import { Point } from "pixi.js";
import type { PointData } from "pixi.js";

/**
 * 缓动类型 —— 与 MonoGame 版 KTween 的 KTweenType 一一对应（连顺序都保持一致）。
 * 其中 once / clamp / pingPong / animationCurve 在 C# 版里也没有实现，落到 default 分支。
 */
export enum KTweenType {
  linear,
  easeOutQuad,
  easeInQuad,
  easeInOutQuad,
  easeInCubic,
  easeOutCubic,
  easeInOutCubic,
  easeInQuart,
  easeOutQuart,
  easeInOutQuart,
  easeInQuint,
  easeOutQuint,
  easeInOutQuint,
  easeInSine,
  easeOutSine,
  easeInOutSine,
  easeInExpo,
  easeOutExpo,
  easeInOutExpo,
  easeInCirc,
  easeOutCirc,
  easeInOutCirc,
  easeInBounce,
  easeOutBounce,
  easeInOutBounce,
  easeInBack,
  easeOutBack,
  easeInOutBack,
  easeInElastic,
  easeOutElastic,
  easeInOutElastic,
  easeSpring,
  easeShake,
  punch,
  once,
  clamp,
  pingPong,
  animationCurve,
}

/** 缓动取值函数：number 版返回 number，Point 版返回 Point */
export interface EaseFunc {
  (from: number, to: number, fPercent: number): number;
  (from: PointData, to: PointData, fPercent: number): Point;
}

function lerpNumber(from: number, to: number, fPercent: number): number {
  return from + (to - from) * fPercent;
}

/**
 * 用一个曲线函数生成 C# 里那套 from/to 重载：
 * 传 number 得到 number，传 PointData 得到 Point。
 */
function makeEase(curve: (f: number) => number): EaseFunc {
  const impl = (
    from: number | PointData,
    to: number | PointData,
    fPercent: number,
  ): number | Point => {
    const eased: number = curve(fPercent);

    if (typeof from === "number") {
      return lerpNumber(from, to as number, eased);
    }

    const fromPoint: PointData = from as PointData;
    const toPoint: PointData = to as PointData;
    return new Point(
      lerpNumber(fromPoint.x, toPoint.x, eased),
      lerpNumber(fromPoint.y, toPoint.y, eased),
    );
  };

  return impl as EaseFunc;
}

/**
 * 缓动函数集合。
 * ApplyEase 按枚举取曲线；其余静态方法是各个曲线本体（C# 版为 private，这里放开方便复用）。
 */
export class KTweenFunc {
  /** 通过枚举选取缓动曲线，输入/输出都是 [0,1] 的进度 */
  public static ApplyEase(type: KTweenType, fPercent: number): number {
    const t: number = Math.min(Math.max(fPercent, 0), 1);

    switch (type) {
      case KTweenType.linear:
        return t;
      case KTweenType.easeInQuad:
        return KTweenFunc.QuadIn(t);
      case KTweenType.easeOutQuad:
        return KTweenFunc.QuadOut(t);
      case KTweenType.easeInOutQuad:
        return KTweenFunc.QuadInOut(t);
      case KTweenType.easeInCubic:
        return KTweenFunc.CubicIn(t);
      case KTweenType.easeOutCubic:
        return KTweenFunc.CubicOut(t);
      case KTweenType.easeInOutCubic:
        return KTweenFunc.CubicInOut(t);
      case KTweenType.easeInQuart:
        return KTweenFunc.QuartIn(t);
      case KTweenType.easeOutQuart:
        return KTweenFunc.QuartOut(t);
      case KTweenType.easeInOutQuart:
        return KTweenFunc.QuartInOut(t);
      case KTweenType.easeInQuint:
        return KTweenFunc.QuintIn(t);
      case KTweenType.easeOutQuint:
        return KTweenFunc.QuintOut(t);
      case KTweenType.easeInOutQuint:
        return KTweenFunc.QuintInOut(t);
      case KTweenType.easeInSine:
        return KTweenFunc.SineIn(t);
      case KTweenType.easeOutSine:
        return KTweenFunc.SineOut(t);
      case KTweenType.easeInOutSine:
        return KTweenFunc.SineInOut(t);
      case KTweenType.easeInExpo:
        return KTweenFunc.ExpoIn(t);
      case KTweenType.easeOutExpo:
        return KTweenFunc.ExpoOut(t);
      case KTweenType.easeInOutExpo:
        return KTweenFunc.ExpoInOut(t);
      case KTweenType.easeInCirc:
        return KTweenFunc.CircIn(t);
      case KTweenType.easeOutCirc:
        return KTweenFunc.CircOut(t);
      case KTweenType.easeInOutCirc:
        return KTweenFunc.CircInOut(t);
      case KTweenType.easeInBounce:
        return 1 - KTweenFunc.BounceOut(1 - t);
      case KTweenType.easeOutBounce:
        return KTweenFunc.BounceOut(t);
      case KTweenType.easeInOutBounce:
        return t < 0.5
          ? (1 - KTweenFunc.BounceOut(1 - 2 * t)) / 2
          : (1 + KTweenFunc.BounceOut(2 * t - 1)) / 2;
      case KTweenType.easeInBack:
        return KTweenFunc.BackIn(t);
      case KTweenType.easeOutBack:
        return KTweenFunc.BackOut(t);
      case KTweenType.easeInOutBack:
        return KTweenFunc.BackInOut(t);
      case KTweenType.easeInElastic:
        return KTweenFunc.ElasticIn(t);
      case KTweenType.easeOutElastic:
        return KTweenFunc.ElasticOut(t);
      case KTweenType.easeInOutElastic:
        return KTweenFunc.ElasticInOut(t);
      case KTweenType.easeSpring:
        return KTweenFunc.Spring(t);
      case KTweenType.easeShake:
        return KTweenFunc.Shake(t);
      case KTweenType.punch:
        return KTweenFunc.Punch(t);
      default:
        return t;
    }
  }

  /** 在两个值之间按进度插值，number 与 Point 都支持 */
  public static Lerp(from: number, to: number, fPercent: number): number;
  public static Lerp(from: PointData, to: PointData, fPercent: number): Point;
  public static Lerp(
    from: number | PointData,
    to: number | PointData,
    fPercent: number,
  ): number | Point {
    if (typeof from === "number") {
      return lerpNumber(from, to as number, fPercent);
    }
    const a: PointData = from as PointData;
    const b: PointData = to as PointData;
    return new Point(
      lerpNumber(a.x, b.x, fPercent),
      lerpNumber(a.y, b.y, fPercent),
    );
  }

  // ==================== 核心缓动曲线 ====================
  // C# 版用 MathF，这里直接换成 Math

  public static Linear(f: number): number {
    return f;
  }

  public static QuadIn(f: number): number {
    return f * f;
  }

  public static QuadOut(f: number): number {
    return f * (2 - f);
  }

  public static QuadInOut(f: number): number {
    return f < 0.5 ? 2 * f * f : -1 + (4 - 2 * f) * f;
  }

  public static CubicIn(f: number): number {
    return f * f * f;
  }

  public static CubicOut(f: number): number {
    const t: number = f - 1;
    return t * t * t + 1;
  }

  public static CubicInOut(f: number): number {
    return f < 0.5 ? 4 * f * f * f : (f - 1) * (2 * f - 2) * (2 * f - 2) + 1;
  }

  public static QuartIn(f: number): number {
    return f * f * f * f;
  }

  public static QuartOut(f: number): number {
    const t: number = f - 1;
    return 1 - t * t * t * t;
  }

  public static QuartInOut(f: number): number {
    const t: number = f - 1;
    return f < 0.5 ? 8 * f * f * f * f : 1 - 8 * t * t * t * t;
  }

  public static QuintIn(f: number): number {
    return f * f * f * f * f;
  }

  public static QuintOut(f: number): number {
    const t: number = f - 1;
    return 1 + t * t * t * t * t;
  }

  public static QuintInOut(f: number): number {
    const t: number = f - 1;
    return f < 0.5 ? 16 * f * f * f * f * f : 1 + 16 * t * t * t * t * t;
  }

  public static SineIn(f: number): number {
    return 1 - Math.cos((f * Math.PI) / 2);
  }

  public static SineOut(f: number): number {
    return Math.sin((f * Math.PI) / 2);
  }

  public static SineInOut(f: number): number {
    return 0.5 * (1 - Math.cos(f * Math.PI));
  }

  public static ExpoIn(f: number): number {
    return f <= 0 ? 0 : Math.pow(2, 10 * f - 10);
  }

  public static ExpoOut(f: number): number {
    return f >= 1 ? 1 : 1 - Math.pow(2, -10 * f);
  }

  public static ExpoInOut(f: number): number {
    if (f === 0 || f === 1) return f;
    return f < 0.5
      ? Math.pow(2, 20 * f - 10) / 2
      : (2 - Math.pow(2, -20 * f + 10)) / 2;
  }

  public static CircIn(f: number): number {
    return 1 - Math.sqrt(1 - f * f);
  }

  public static CircOut(f: number): number {
    const t: number = f - 1;
    return Math.sqrt(1 - t * t);
  }

  public static CircInOut(f: number): number {
    return f < 0.5
      ? (1 - Math.sqrt(1 - 4 * f * f)) / 2
      : (Math.sqrt(1 - (-2 * f + 2) * (-2 * f + 2)) + 1) / 2;
  }

  public static BounceOut(f: number): number {
    let t: number = Math.min(Math.max(f, 0), 1);
    const n1 = 7.5625;
    const d1 = 2.75;

    if (t < 1 / d1) return n1 * t * t;

    if (t < 2 / d1) {
      t -= 1.5 / d1;
      return n1 * t * t + 0.75;
    }

    if (t < 2.5 / d1) {
      t -= 2.25 / d1;
      return n1 * t * t + 0.9375;
    }

    t -= 2.625 / d1;
    return n1 * t * t + 0.984375;
  }

  public static BackIn(f: number): number {
    const c1 = 1.70158;
    const c3 = c1 + 1;
    return c3 * f * f * f - c1 * f * f;
  }

  public static BackOut(f: number): number {
    const c1 = 1.70158;
    const c3 = c1 + 1;
    const t: number = f - 1;
    return 1 + c3 * t * t * t + c1 * t * t;
  }

  public static BackInOut(f: number): number {
    const c1 = 1.70158;
    const c2 = c1 * 1.525;
    return f < 0.5
      ? (2 * f * (2 * f) * ((c2 + 1) * 2 * f - c2)) / 2
      : ((2 * f - 2) * (2 * f - 2) * ((c2 + 1) * (f * 2 - 2) + c2) + 2) / 2;
  }

  public static ElasticIn(f: number): number {
    if (f <= 0 || f >= 1) return f;
    const c4 = 2.0943951;
    return -Math.pow(2, 10 * f - 10) * Math.sin((f * 10 - 10.75) * c4);
  }

  public static ElasticOut(f: number): number {
    if (f <= 0 || f >= 1) return f;
    const c4 = 2.0943951;
    return Math.pow(2, -10 * f) * Math.sin((f * 10 - 0.75) * c4) + 1;
  }

  public static ElasticInOut(f: number): number {
    if (f <= 0 || f >= 1) return f;
    const c5 = 1.3962634;
    return f < 0.5
      ? -(Math.pow(2, 20 * f - 10) * Math.sin((20 * f - 11.125) * c5)) / 2
      : (Math.pow(2, -20 * f + 10) * Math.sin((20 * f - 11.125) * c5)) / 2 + 1;
  }

  public static Spring(f: number): number {
    const t: number = Math.min(Math.max(f, 0), 1);
    return (
      Math.pow(2, -10 * t) * Math.sin(((t - 0.075) * Math.PI * 2) / 0.3) + 1
    );
  }

  public static Shake(f: number): number {
    return Math.pow(2, -10 * f) * Math.sin(f * 7 * Math.PI);
  }

  public static Punch(f: number): number {
    if (f === 0 || f === 1) return 0;
    return Math.pow(2, -10 * f) * Math.sin(f * 9 * Math.PI);
  }
}

// ==================== from/to 重载版（对应 C# 的 Vector2/float 两套重载） ====================
// Pixi 是 2D 引擎，所以去掉了 C# 的 Vector3 重载，只保留 number 与 PointData。

export const easeLinear: EaseFunc = makeEase(KTweenFunc.Linear);
export const easeInQuad: EaseFunc = makeEase(KTweenFunc.QuadIn);
export const easeOutQuad: EaseFunc = makeEase(KTweenFunc.QuadOut);
export const easeInOutQuad: EaseFunc = makeEase(KTweenFunc.QuadInOut);
export const easeInCubic: EaseFunc = makeEase(KTweenFunc.CubicIn);
export const easeOutCubic: EaseFunc = makeEase(KTweenFunc.CubicOut);
export const easeInOutCubic: EaseFunc = makeEase(KTweenFunc.CubicInOut);
export const easeInQuart: EaseFunc = makeEase(KTweenFunc.QuartIn);
export const easeOutQuart: EaseFunc = makeEase(KTweenFunc.QuartOut);
export const easeInOutQuart: EaseFunc = makeEase(KTweenFunc.QuartInOut);
export const easeInQuint: EaseFunc = makeEase(KTweenFunc.QuintIn);
export const easeOutQuint: EaseFunc = makeEase(KTweenFunc.QuintOut);
export const easeInOutQuint: EaseFunc = makeEase(KTweenFunc.QuintInOut);
export const easeInSine: EaseFunc = makeEase(KTweenFunc.SineIn);
export const easeOutSine: EaseFunc = makeEase(KTweenFunc.SineOut);
export const easeInOutSine: EaseFunc = makeEase(KTweenFunc.SineInOut);
export const easeInExpo: EaseFunc = makeEase(KTweenFunc.ExpoIn);
export const easeOutExpo: EaseFunc = makeEase(KTweenFunc.ExpoOut);
export const easeInOutExpo: EaseFunc = makeEase(KTweenFunc.ExpoInOut);
export const easeInCirc: EaseFunc = makeEase(KTweenFunc.CircIn);
export const easeOutCirc: EaseFunc = makeEase(KTweenFunc.CircOut);
export const easeInOutCirc: EaseFunc = makeEase(KTweenFunc.CircInOut);
export const easeInBounce: EaseFunc = makeEase(
  (f: number) => 1 - KTweenFunc.BounceOut(1 - f),
);
export const easeOutBounce: EaseFunc = makeEase(KTweenFunc.BounceOut);
export const easeInOutBounce: EaseFunc = makeEase((f: number) =>
  f < 0.5
    ? (1 - KTweenFunc.BounceOut(1 - 2 * f)) / 2
    : (1 + KTweenFunc.BounceOut(2 * f - 1)) / 2,
);
export const easeInBack: EaseFunc = makeEase(KTweenFunc.BackIn);
export const easeOutBack: EaseFunc = makeEase(KTweenFunc.BackOut);
export const easeInOutBack: EaseFunc = makeEase(KTweenFunc.BackInOut);
export const easeInElastic: EaseFunc = makeEase(KTweenFunc.ElasticIn);
export const easeOutElastic: EaseFunc = makeEase(KTweenFunc.ElasticOut);
export const easeInOutElastic: EaseFunc = makeEase(KTweenFunc.ElasticInOut);
export const easeSpring: EaseFunc = makeEase(KTweenFunc.Spring);
export const easeShake: EaseFunc = makeEase(KTweenFunc.Shake);
export const punch: EaseFunc = makeEase(KTweenFunc.Punch);
