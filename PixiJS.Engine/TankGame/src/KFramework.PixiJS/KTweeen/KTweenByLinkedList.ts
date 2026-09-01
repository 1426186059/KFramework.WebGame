import type { Container } from "pixi.js";

import { KTime } from "../GameEngine/KTime";
import { LinkedList } from "../Tool/LinkedList";
import type { LinkedListNode } from "../Tool/LinkedList";
import { KTweenMgr, ObjectPool } from "./KTween";
import type { FinishFunc, TweenItem, UpdateFunc } from "./KTween";
import { KTweenFunc } from "./KTweenFunc";

/**
 * 补间的实际调度器：用一个双向链表保存所有在跑的 TweenItem，
 * 每帧顺序推进，删除是 O(1)（节点存在 TweenItem.mEntry 里）。
 */
export class KTweenByLinkedList 
{
  private readonly mItemPool: ObjectPool = new ObjectPool();
  private readonly mTweenT: LinkedList<TweenItem> = new LinkedList<TweenItem>();

  /** 用 {@link KTime.deltaTime}（秒）推进一帧 */
  public Update(): void {
    this.UpdateWith(KTime.deltaTime);
  }

  /**
   * 用指定的 deltaTime（秒）推进一帧。
   *
   * 与原版唯一的差异：sumTime <= 0 的补间会直接判完成并回收。
   * C# 版这里 0/0 会得到 NaN，而 NaN >= 1 恒为 false，补间会永远留在链表里。
   */
  public UpdateWith(deltaTime: number): void {
    let mNode: LinkedListNode<TweenItem> | null = this.mTweenT.First;

    while (mNode !== null) {
      const mItem: TweenItem = mNode.Value;

      if (mItem.toggle === false || mItem.bindObj === null) {
        mNode = this.DoRemove(mNode);
        continue;
      }

      if (mItem.delay > 0) {
        mItem.delay -= deltaTime;
        mNode = this.DoNext(mNode);
        continue;
      }

      if (mItem.sumTime <= 0) {
        mItem.updateFunc?.(1);
        mItem.finishFunc?.();
        mNode = this.DoRemove(mNode);
        continue;
      }

      if (mItem.nLoopPingTong > 0) {
        // 乒乓：1 正向累加，2 反向递减
        if (mItem.nLoopPingTong === 2) {
          mItem.time -= deltaTime;
        } else {
          mItem.time += deltaTime;
        }

        mItem.time = Math.min(Math.max(mItem.time, 0), mItem.sumTime);
        const fTimePercent: number = mItem.time / mItem.sumTime;
        mItem.updateFunc?.(KTweenFunc.ApplyEase(mItem.nType, fTimePercent));

        if (mItem.nLoopPingTong === 2) {
          if (fTimePercent <= 0) {
            mItem.finishFunc?.();
            mItem.nLoopPingTong = 1;

            if (mItem.nLoopCount > 0) {
              mItem.nLoopCount--;

              if (mItem.nLoopCount <= 0) {
                mNode = this.DoRemove(mNode);
                continue;
              }
            }
          }
        } else if (fTimePercent >= 1) {
          mItem.nLoopPingTong = 2;
        }
      } else {
        mItem.time += deltaTime;
        mItem.time = Math.min(Math.max(mItem.time, 0), mItem.sumTime);
        const fTimePercent: number = mItem.time / mItem.sumTime;
        mItem.updateFunc?.(KTweenFunc.ApplyEase(mItem.nType, fTimePercent));

        if (fTimePercent >= 1) {
          mItem.finishFunc?.();

          if (mItem.nLoopCount === -1) {
            mItem.time = 0;
          } else {
            mItem.nLoopCount--;
            mItem.time = 0;

            if (mItem.nLoopCount <= 0) {
              mNode = this.DoRemove(mNode);
              continue;
            }
          }
        }
      }

      mNode = this.DoNext(mNode);
    }
  }

  private DoNext(mNode: LinkedListNode<TweenItem>): LinkedListNode<TweenItem> | null {
    return mNode.Next;
  }

  private DoRemove(
    mNode: LinkedListNode<TweenItem>,
  ): LinkedListNode<TweenItem> | null {
    const mNextNode: LinkedListNode<TweenItem> | null = this.DoNext(mNode);
    this.mTweenT.Remove(mNode);
    this.mItemPool.Recycle(mNode.Value);
    return mNextNode;
  }

  public AddTween(
    time: number,
    updateFunc?: UpdateFunc,
    finishFunc?: FinishFunc,
  ): TweenItem;
  public AddTween(
    obj: Container,
    time: number,
    updateFunc?: UpdateFunc,
    finishFunc?: FinishFunc,
  ): TweenItem;
  public AddTween(
    a: number | Container,
    b?: number | UpdateFunc,
    c?: UpdateFunc | FinishFunc,
    d?: FinishFunc,
  ): TweenItem {
    const mItem: TweenItem = this.mItemPool.Pop();

    if (typeof a === "number") {
      // 没指定对象时挂到管理器的全局绑定对象上，避免第一帧就被回收
      mItem.bindObj = KTweenMgr.GetInstance().bindObj;
      mItem.toggle = true;
      mItem.time = 0;
      mItem.sumTime = a;
      mItem.updateFunc = (b as UpdateFunc) ?? null;
      mItem.finishFunc = (c as FinishFunc) ?? null;
    } else {
      mItem.bindObj = a;
      mItem.toggle = true;
      mItem.time = 0;
      mItem.sumTime = b as number;
      mItem.updateFunc = (c as UpdateFunc) ?? null;
      mItem.finishFunc = d ?? null;
    }

    this.mTweenT.AddLast(mItem.mEntry);
    return mItem;
  }

  public SetMaxTweenCount(nCount: number): void {
    this.mItemPool.SetMaxCapacity(nCount);
  }

  public CancelAll(): void {
    let mNode: LinkedListNode<TweenItem> | null = this.mTweenT.First;
    while (mNode !== null) {
      mNode.Value.toggle = false;
      mNode = mNode.Next;
    }
  }

  public Cancel(obj: Container): void {
    let mNode: LinkedListNode<TweenItem> | null = this.mTweenT.First;
    while (mNode !== null) {
      if (mNode.Value.bindObj === obj) {
        mNode.Value.toggle = false;
      }
      mNode = mNode.Next;
    }
  }
}
