import { beforeEach, describe, expect, it } from "vitest";
import { Container, Sprite } from "pixi.js";
import type { Ticker } from "pixi.js";

import { KTween, KTweenMgr } from "../src/KFramework.PixiJS/KTweeen/KTween";
import { KTweenEx } from "../src/KFramework.PixiJS/KTweeen/KTweenEx";
import {
  KTweenFunc,
  KTweenType,
  easeInQuad,
} from "../src/KFramework.PixiJS/KTweeen/KTweenFunc";

/** 手动推进一帧（秒） */
function step(deltaTime: number): void {
  KTweenMgr.GetInstance().UpdateWith(deltaTime);
}

/** 推进若干秒，默认按 60FPS 切片 */
function stepFor(seconds: number, deltaTime = 1 / 60): void {
  let acc = 0;
  while (acc < seconds - 1e-9) {
    step(deltaTime);
    acc += deltaTime;
  }
}

/** 只提供 deltaMS 字段的 Ticker 替身，用于驱动 KTween.update */
function makeTicker(deltaMS: number): Ticker {
  return { deltaMS } as Ticker;
}

beforeEach(() => {
  // 清空上一轮残留的补间（CancelAll 只是置 toggle=false，再推进一帧才会真正回收）
  KTween.CancelAll();
  step(0);
});

describe("KTween - 基础补间", () => {
  it("[Test1] 单段 moveX：1 秒后到达目标 x", () => {
    const obj = new Container();
    KTweenEx.moveX(obj, 5, 1.0);

    expect(obj.position.x).toBe(0); // 还没推进时间

    stepFor(1.0);

    expect(obj.position.x).toBeCloseTo(5, 5);
  });

  it("[Test2] 链式两段 moveX：2 秒后到达第二个目标", () => {
    const obj = new Container();
    KTweenEx.moveX(obj, 5, 1.0).AppendTween(KTweenEx.moveX(obj, 10, 1.0));

    stepFor(1.0);
    expect(obj.position.x).toBeCloseTo(5, 5);

    stepFor(1.0);
    expect(obj.position.x).toBeCloseTo(10, 5);
  });

  it("[Test3] delayedCall 到点触发回调", () => {
    let called = false;
    KTween.delayedCall(0.5, () => (called = true));

    stepFor(0.4);
    expect(called).toBe(false);

    stepFor(0.2);
    expect(called).toBe(true);
  });

  it("补间跑完后会被回收，不再占用链表", () => {
    const obj = new Container();
    KTweenEx.moveLocalX(obj, 100, 0.5);

    stepFor(0.5);
    step(1 / 60); // 浮点累加：30×(1/60)=0.49999999999999994，补一帧才真正走完

    // 再推进，位置不应该继续变化
    const settled = obj.position.x;
    stepFor(1.0);
    expect(obj.position.x).toBe(settled);
  });
});

describe("KTween - 缓动 / 延迟 / 循环", () => {
  it("SetEase：easeInQuad 在半程时进度为 0.25", () => {
    const obj = new Container();
    KTweenEx.moveLocalX(obj, 100, 1.0).SetEase(KTweenType.easeInQuad);

    stepFor(0.5);
    expect(obj.position.x).toBeCloseTo(25, 1);
  });

  it("SetDelay：延迟期间不推进", () => {
    const obj = new Container();
    KTweenEx.moveLocalX(obj, 100, 1.0).SetDelay(0.5);

    stepFor(0.5);
    expect(obj.position.x).toBe(0);

    // 延迟是靠「每帧减 delay」实现的，到点的那一整帧不会推进进度，故多给一帧
    stepFor(1.0);
    step(1 / 60);
    expect(obj.position.x).toBeCloseTo(100, 5);
  });

  it("SetLoop(2)：跑满两轮后自动结束", () => {
    const obj = new Container();
    let finished = 0;
    KTweenEx.moveLocalX(obj, 100, 1.0)
      .SetLoop(2)
      .SetOnCompleteFunc(() => finished++);

    stepFor(1.0);
    expect(finished).toBe(1);
    expect(obj.position.x).toBeCloseTo(100, 5);

    stepFor(1.0);
    expect(finished).toBe(2);

    step(0); // 回收
    const settled = obj.position.x;
    stepFor(1.0);
    expect(obj.position.x).toBe(settled);
  });

  it("SetLoopPingPong：正向 1 秒再反向 1 秒回到起点", () => {
    const obj = new Container();
    let finished = 0;
    KTweenEx.moveLocalX(obj, 100, 1.0)
      .SetLoopPingPong(1)
      .SetOnCompleteFunc(() => finished++);

    stepFor(1.0);
    expect(obj.position.x).toBeCloseTo(100, 5);
    expect(finished).toBe(0); // 正向到顶还不算一轮

    stepFor(1.0);
    expect(obj.position.x).toBeCloseTo(0, 5);
    expect(finished).toBe(1);
  });

  it("SetOnUpdateFunc 收到的是【已缓动】的进度", () => {
    const percents: number[] = [];
    KTween.AddTween(1.0, (t) => percents.push(t)).SetEase(
      KTweenType.easeInQuad,
    );

    stepFor(1.0);

    expect(percents[percents.length - 1]).toBeCloseTo(1, 5);
    // easeInQuad 半程应该是 0.25 附近
    const half = percents[Math.floor(percents.length / 2)];
    expect(half).toBeCloseTo(0.25, 1);
  });

  it("sumTime <= 0 的补间会立刻判定完成（C# 版这里会卡成 NaN）", () => {
    let finished = false;
    KTween.AddTween(0, undefined, () => (finished = true));

    step(0);
    expect(finished).toBe(true);
  });
});

describe("KTween - 取消与句柄", () => {
  it("Cancel(对象)：该对象上的补间全部停掉", () => {
    const obj = new Container();
    KTweenEx.moveLocalX(obj, 100, 1.0);
    KTweenEx.moveLocalY(obj, 100, 1.0);

    stepFor(0.5);
    const x = obj.position.x;
    const y = obj.position.y;
    expect(x).toBeGreaterThan(0);

    KTween.Cancel(obj);
    stepFor(1.0);

    expect(obj.position.x).toBe(x);
    expect(obj.position.y).toBe(y);
  });

  it("CancelAll：清空所有补间", () => {
    const obj = new Container();
    KTweenEx.moveLocalX(obj, 100, 1.0);

    stepFor(0.5);
    const x = obj.position.x;

    KTween.CancelAll();
    stepFor(1.0);

    expect(obj.position.x).toBe(x);
  });

  it("句柄：Cancel 之后 IsValid 变 false，且补间不再推进", () => {
    const obj = new Container();
    const handle = KTweenEx.moveLocalX(obj, 100, 1.0).GetHandle();

    expect(handle.IsValid()).toBe(true);

    stepFor(0.5);
    const x = obj.position.x;

    handle.Cancel();
    expect(handle.IsValid()).toBe(false);

    stepFor(1.0);
    expect(obj.position.x).toBe(x);
  });

  it("句柄：Dispose 等价于 Cancel", () => {
    const obj = new Container();
    const handle = KTweenEx.moveLocalX(obj, 100, 1.0).GetHandle();

    handle.Dispose();
    expect(handle.IsValid()).toBe(false);
  });

  it("cancel 会连带取消串在后面的补间", () => {
    const obj = new Container();
    const first = KTweenEx.moveLocalX(obj, 50, 1.0);
    const second = KTweenEx.moveLocalX(obj, 100, 1.0);
    first.AppendTween(second);

    first.cancel();
    step(0);
    stepFor(2.0);

    expect(obj.position.x).toBe(0);
  });
});

describe("KTweenEx - Pixi 属性补间", () => {
  it("moveLocal 走本地坐标", () => {
    const parent = new Container();
    parent.position.set(100, 200);
    const obj = new Container();
    parent.addChild(obj);

    KTweenEx.moveLocal(obj, { x: 30, y: 40 }, 1.0);
    stepFor(1.0);

    expect(obj.position.x).toBeCloseTo(30, 5);
    expect(obj.position.y).toBeCloseTo(40, 5);
  });

  it("move 走世界坐标（内部换算回本地）", () => {
    const parent = new Container();
    parent.position.set(100, 200);
    const obj = new Container();
    parent.addChild(obj);

    KTweenEx.move(obj, { x: 150, y: 250 }, 1.0);
    stepFor(1.0);

    expect(obj.position.x).toBeCloseTo(50, 5); // 150 - 100
    expect(obj.position.y).toBeCloseTo(50, 5); // 250 - 200
  });

  it("move 沿路径：两段各自占一半时间", () => {
    const obj = new Container();
    KTweenEx.moveLocal(
      obj,
      [
        { x: 0, y: 0 },
        { x: 100, y: 0 },
        { x: 100, y: 100 },
      ],
      2.0,
    );

    stepFor(1.0);
    expect(obj.position.x).toBeCloseTo(100, 5);
    expect(obj.position.y).toBeCloseTo(0, 5);

    stepFor(1.0);
    expect(obj.position.y).toBeCloseTo(100, 5);
  });

  it("moveBezier：路径点数不是 3n+1 时返回 null", () => {
    const obj = new Container();

    expect(
      KTweenEx.moveBezier(
        obj,
        [
          { x: 0, y: 0 },
          { x: 1, y: 1 },
        ],
        1.0,
      ),
    ).toBeNull();
    expect(
      KTweenEx.moveBezier(
        obj,
        [
          { x: 0, y: 0 },
          { x: 0, y: 100 },
          { x: 100, y: 100 },
          { x: 100, y: 0 },
        ],
        1.0,
      ),
    ).not.toBeNull();
  });

  it("scale 与 rotateAround", () => {
    const obj = new Container();
    obj.scale.set(1, 1);

    KTweenEx.scale(obj, { x: 2, y: 3 }, 1.0);
    KTweenEx.rotateAround(obj, Math.PI, 1.0);

    stepFor(1.0);

    expect(obj.scale.x).toBeCloseTo(2, 5);
    expect(obj.scale.y).toBeCloseTo(3, 5);
    expect(obj.rotation).toBeCloseTo(Math.PI, 5);
  });

  it("color：tint 按 RGB 三通道插值", () => {
    const sprite = new Sprite();
    sprite.tint = 0x000000;

    KTweenEx.color(sprite, 0xff0000, 1.0);

    stepFor(0.5);
    expect(sprite.tint).toBeCloseTo(0x7f0000, -2); // 允许取整误差

    stepFor(0.5);
    expect(sprite.tint).toBe(0xff0000);
  });
});

describe("KTweenFunc - 缓动函数", () => {
  it("ApplyEase 会把进度夹到 [0,1]", () => {
    expect(KTweenFunc.ApplyEase(KTweenType.linear, -1)).toBe(0);
    expect(KTweenFunc.ApplyEase(KTweenType.linear, 2)).toBe(1);
  });

  it("数值版与 Point 版的 ease 函数结果一致", () => {
    const n: number = easeInQuad(0, 10, 0.5);
    const p = easeInQuad({ x: 0, y: 0 }, { x: 10, y: 20 }, 0.5);

    expect(n).toBeCloseTo(2.5, 5);
    expect(p.x).toBeCloseTo(2.5, 5);
    expect(p.y).toBeCloseTo(5, 5);
  });

  it("未实现的枚举（once / clamp / pingPong / animationCurve）落回线性", () => {
    expect(KTweenFunc.ApplyEase(KTweenType.once, 0.3)).toBeCloseTo(0.3, 5);
    expect(KTweenFunc.ApplyEase(KTweenType.animationCurve, 0.7)).toBeCloseTo(
      0.7,
      5,
    );
  });
});

describe("KTween - 用 Ticker 驱动", () => {
  it("KTween.update(ticker) 按 deltaMS（毫秒）推进", () => {
    const obj = new Container();
    KTweenEx.moveLocalX(obj, 120, 1.0);

    // 60 帧 × 16.6667ms ≈ 1 秒
    for (let i = 0; i < 60; i++) {
      KTween.update(makeTicker(1000 / 60));
    }

    expect(obj.position.x).toBeCloseTo(120, 5);
  });
});
