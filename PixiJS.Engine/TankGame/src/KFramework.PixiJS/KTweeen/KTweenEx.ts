import { Point, type Container } from "pixi.js";
import type { PointData } from "pixi.js";

import { KTween } from "./KTween";
import type { TweenItem } from "./KTween";

/** 支持 tint 的显示对象（Sprite / Graphics 等都满足） */
export type Tintable = Container & { tint: number };

/**
 * LeanTween 兼容 API 层 —— 内部调用 KTween 实现，所有方法返回 TweenItem 支持链式调用。
 *
 * 与 MonoGame 版的差异：
 * - KTransform -> Pixi 的 {@link Container}；Vector2 -> PointData
 * - moveXxx 走世界坐标（内部用 toGlobal / parent.toLocal 换算），moveLocalXxx 走本地坐标
 * - 旋转单位是【弧度】（与 C# 版一致，MonoGame 的 CreateRotationZ 也是弧度）
 * - Color -> Pixi 的 tint（0xRRGGBB 数字），颜色按 RGB 三个通道分别插值
 * - C# 的两个 color 重载（SpriteRenderer / KImage）在这里合并成一个 {@link Tintable} 版本
 */
export class KTweenEx {
  // ==================== 世界坐标移动 ====================

  public static move(obj: Container, to: PointData, time: number): TweenItem;
  public static move(
    obj: Container,
    path: PointData[],
    time: number,
  ): TweenItem | null;
  public static move(
    obj: Container,
    target: PointData | PointData[],
    time: number,
  ): TweenItem | null {
    if (Array.isArray(target)) {
      const segCount: number = target.length - 1;
      if (segCount <= 0) return null;

      const p: PointData[] = target;
      return KTween.AddTween(obj, time, (fPercent: number) => {
        if (fPercent >= 1) {
          KTweenEx.setWorldPosition(obj, p[p.length - 1]);
          return;
        }
        const t: number = fPercent * segCount;
        let idx: number = Math.floor(t);
        if (idx >= segCount) idx = segCount - 1;
        const segT: number = t - idx;
        KTweenEx.setWorldPosition(obj, lerpPoint(p[idx], p[idx + 1], segT));
      });
    }

    const from: Point = KTweenEx.getWorldPosition(obj);
    const toPoint: PointData = target;
    return KTween.AddTween(obj, time, (fPercent: number) => {
      KTweenEx.setWorldPosition(obj, lerpPoint(from, toPoint, fPercent));
    });
  }

  /**
   * 贝塞尔曲线路径移动 —— 三次贝塞尔串联。
   * 路径长度必须为 3n+1（4, 7, 10, 13...），每 4 个点 = 一段贝塞尔。
   */
  public static moveBezier(
    obj: Container,
    path: PointData[],
    time: number,
  ): TweenItem | null {
    const segCount: number = path.length <= 0 ? 0 : (path.length - 1) / 3;
    if (segCount <= 0 || (path.length - 1) % 3 !== 0) return null;

    const p: PointData[] = path;
    return KTween.AddTween(obj, time, (fPercent: number) => {
      if (fPercent >= 1) {
        KTweenEx.setWorldPosition(obj, p[p.length - 1]);
        return;
      }
      const t: number = fPercent * segCount;
      let segIdx: number = Math.floor(t);
      if (segIdx >= segCount) segIdx = segCount - 1;
      KTweenEx.setWorldPosition(obj, bezierAt(p, segIdx * 3, t - segIdx));
    });
  }

  public static moveX(obj: Container, x: number, time: number): TweenItem {
    const from: Point = KTweenEx.getWorldPosition(obj);
    return KTween.AddTween(obj, time, (fPercent: number) => {
      KTweenEx.setWorldPosition(
        obj,
        new Point(from.x + (x - from.x) * fPercent, from.y),
      );
    });
  }

  public static moveY(obj: Container, y: number, time: number): TweenItem {
    const from: Point = KTweenEx.getWorldPosition(obj);
    return KTween.AddTween(obj, time, (fPercent: number) => {
      KTweenEx.setWorldPosition(
        obj,
        new Point(from.x, from.y + (y - from.y) * fPercent),
      );
    });
  }

  // ==================== 本地坐标移动 ====================

  public static moveLocal(
    obj: Container,
    to: PointData,
    time: number,
  ): TweenItem;
  public static moveLocal(
    obj: Container,
    path: PointData[],
    time: number,
  ): TweenItem | null;
  public static moveLocal(
    obj: Container,
    target: PointData | PointData[],
    time: number,
  ): TweenItem | null {
    if (Array.isArray(target)) {
      const segCount: number = target.length - 1;
      if (segCount <= 0) return null;

      const p: PointData[] = target;
      return KTween.AddTween(obj, time, (fPercent: number) => {
        if (fPercent >= 1) {
          obj.position.set(p[p.length - 1].x, p[p.length - 1].y);
          return;
        }
        const t: number = fPercent * segCount;
        let idx: number = Math.floor(t);
        if (idx >= segCount) idx = segCount - 1;
        const at: Point = lerpPoint(p[idx], p[idx + 1], t - idx);
        obj.position.set(at.x, at.y);
      });
    }

    const from: Point = obj.position.clone();
    const toPoint: PointData = target;
    return KTween.AddTween(obj, time, (fPercent: number) => {
      const at: Point = lerpPoint(from, toPoint, fPercent);
      obj.position.set(at.x, at.y);
    });
  }

  public static moveLocalBezier(
    obj: Container,
    path: PointData[],
    time: number,
  ): TweenItem | null {
    const segCount: number = (path.length - 1) / 3;
    if (segCount <= 0 || (path.length - 1) % 3 !== 0) return null;

    const p: PointData[] = path;
    return KTween.AddTween(obj, time, (fPercent: number) => {
      if (fPercent >= 1) {
        obj.position.set(p[p.length - 1].x, p[p.length - 1].y);
        return;
      }
      const t: number = fPercent * segCount;
      let segIdx: number = Math.floor(t);
      if (segIdx >= segCount) segIdx = segCount - 1;
      const at: Point = bezierAt(p, segIdx * 3, t - segIdx);
      obj.position.set(at.x, at.y);
    });
  }

  public static moveLocalX(obj: Container, x: number, time: number): TweenItem {
    const from: Point = obj.position.clone();
    return KTween.AddTween(obj, time, (fPercent: number) => {
      obj.position.set(from.x + (x - from.x) * fPercent, from.y);
    });
  }

  public static moveLocalY(obj: Container, y: number, time: number): TweenItem {
    const from: Point = obj.position.clone();
    return KTween.AddTween(obj, time, (fPercent: number) => {
      obj.position.set(from.x, from.y + (y - from.y) * fPercent);
    });
  }

  // ==================== 缩放 / 旋转 / 颜色 ====================

  public static scale(obj: Container, to: PointData, time: number): TweenItem {
    const from: Point = new Point(obj.scale.x, obj.scale.y);
    return KTween.AddTween(obj, time, (fPercent: number) => {
      const at: Point = lerpPoint(from, to, fPercent);
      obj.scale.set(at.x, at.y);
    });
  }

  public static rotateAround(
    obj: Container,
    angle: number,
    time: number,
  ): TweenItem {
    const startRot: number = obj.rotation;
    return KTween.AddTween(obj, time, (fPercent: number) => {
      obj.rotation = startRot + angle * fPercent;
    });
  }

  /** Pixi 的 Container.rotation 本身就是本地旋转，所以与 rotateAround 行为一致 */
  public static rotateAroundLocal(
    obj: Container,
    angle: number,
    time: number,
  ): TweenItem {
    return KTweenEx.rotateAround(obj, angle, time);
  }

  /**  tint 过渡，to 传 0xRRGGBB */
  public static color(obj: Tintable, to: number, time: number): TweenItem {
    const from: number = obj.tint;
    return KTween.AddTween(obj, time, (fPercent: number) => {
      obj.tint = lerpColor(from, to, fPercent);
    });
  }

  // ==================== 世界坐标读写 ====================

  /** 取对象原点在世界坐标系里的位置 */
  public static getWorldPosition(obj: Container): Point {
    return obj.toGlobal(new Point(0, 0));
  }

  /**
   * 设置世界坐标（内部换算回本地坐标再赋值）。
   * 与 C# 版一致：用【父节点】的世界矩阵求逆，而不是自身矩阵。
   */
  public static setWorldPosition(obj: Container, worldPos: PointData): void {
    const local: Point = obj.parent
      ? obj.parent.toLocal(worldPos)
      : new Point(worldPos.x, worldPos.y);
    obj.position.set(local.x, local.y);
  }
}

function lerpPoint(from: PointData, to: PointData, fPercent: number): Point {
  return new Point(
    from.x + (to.x - from.x) * fPercent,
    from.y + (to.y - from.y) * fPercent,
  );
}

/** 三次贝塞尔：p[i] 为 P0，依次 P1/P2/P3 */
function bezierAt(p: PointData[], i: number, t: number): Point {
  const u: number = 1 - t;
  const uuu: number = u * u * u;
  const uut: number = 3 * u * u * t;
  const utt: number = 3 * u * t * t;
  const ttt: number = t * t * t;

  return new Point(
    uuu * p[i].x + uut * p[i + 1].x + utt * p[i + 2].x + ttt * p[i + 3].x,
    uuu * p[i].y + uut * p[i + 1].y + utt * p[i + 2].y + ttt * p[i + 3].y,
  );
}

/** 0xRRGGBB 三通道分别插值 */
function lerpColor(from: number, to: number, fPercent: number): number {
  const r: number =
    ((from >> 16) & 0xff) +
    (((to >> 16) & 0xff) - ((from >> 16) & 0xff)) * fPercent;
  const g: number =
    ((from >> 8) & 0xff) +
    (((to >> 8) & 0xff) - ((from >> 8) & 0xff)) * fPercent;
  const b: number = (from & 0xff) + ((to & 0xff) - (from & 0xff)) * fPercent;

  return (Math.round(r) << 16) | (Math.round(g) << 8) | Math.round(b);
}
