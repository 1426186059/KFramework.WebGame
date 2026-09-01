import { Ticker, Container } from "pixi.js";

import { KTime } from "../GameEngine/KTime";
import { LinkedList, LinkedListNode } from "../Tool/LinkedList";
import { FinishFunc, TweenItem, UpdateFunc } from "./KTween";
import { KTweenFunc } from "./KTweenFunc";
import { PixiTool } from "../Tool/PixiTool";

export class KTweenMgr 
{
    private static m_Instance: KTweenMgr | null = null;
    private readonly UpdateFunc;
    public readonly bindObj: Container = new Container();

    private constructor() 
    {
      this.UpdateFunc = this.Update.bind(this);
      Ticker.shared.add(this.UpdateFunc);
    }
  
    public static GetInstance(): KTweenMgr 
    {
      if (KTweenMgr.m_Instance === null) 
      {
          KTweenMgr.m_Instance = new KTweenMgr();
      }
      return KTweenMgr.m_Instance;
    }


  private readonly mItemPool: ObjectPool = new ObjectPool();
  private readonly mTweenT: LinkedList<TweenItem> = new LinkedList<TweenItem>();

  /** 用 {@link KTime.deltaTime}（秒）推进一帧 */
  public Update(): void 
  {
    let deltaTime = KTime.deltaTime;

    let mNode: LinkedListNode<TweenItem> | null = this.mTweenT.First;
    while (mNode !== null) 
    {
      const mItem: TweenItem = mNode.Value;

      if (mItem.toggle === false || !PixiTool.isAlive(mItem.bindObj)) 
      {
          mNode = this.DoRemove(mNode);
          continue;
      }

      if (mItem.delay > 0) 
      {
          mItem.delay -= deltaTime;
          mNode = this.DoNext(mNode);
          continue;
      }

      if (mItem.sumTime <= 0) 
      {
          mItem.updateFunc?.(1);
          mItem.finishFunc?.();
          mNode = this.DoRemove(mNode);
          continue;
      }

      if (mItem.nLoopPingTong > 0)
      {
          // 乒乓：1 正向累加，2 反向递减
          if (mItem.nLoopPingTong === 2) 
          {
              mItem.time -= deltaTime;
          }
          else 
          {
              mItem.time += deltaTime;
          }

          mItem.time = Math.min(Math.max(mItem.time, 0), mItem.sumTime);
          const fTimePercent: number = mItem.time / mItem.sumTime;
          mItem.updateFunc?.(KTweenFunc.ApplyEase(mItem.nType, fTimePercent));

          if (mItem.nLoopPingTong === 2) 
          {
              if (fTimePercent <= 0) 
              {
                mItem.finishFunc?.();
                mItem.nLoopPingTong = 1;
                if (mItem.nLoopCount > 0) 
                {
                    mItem.nLoopCount--;
                    if (mItem.nLoopCount <= 0) {
                      mNode = this.DoRemove(mNode);
                      continue;
                    }
                }
              }
            }
            else if (fTimePercent >= 1) 
            {
              mItem.nLoopPingTong = 2;
            }
        } 
        else 
        {
            mItem.time += deltaTime;
            mItem.time = Math.min(Math.max(mItem.time, 0), mItem.sumTime);
            const fTimePercent: number = mItem.time / mItem.sumTime;
            mItem.updateFunc?.(KTweenFunc.ApplyEase(mItem.nType, fTimePercent));

            if (fTimePercent >= 1) 
            {
              mItem.finishFunc?.();
              if (mItem.nLoopCount === -1) 
              {
                  mItem.time = 0;
              } 
              else 
              {
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

  private DoNext(mNode: LinkedListNode<TweenItem>): LinkedListNode<TweenItem> | null 
  {
    return mNode.Next;
  }

  private DoRemove(mNode: LinkedListNode<TweenItem>): LinkedListNode<TweenItem> | null 
  {
      const mNextNode: LinkedListNode<TweenItem> | null = this.DoNext(mNode);
      this.mTweenT.Remove(mNode);
      this.mItemPool.Recycle(mNode.Value);
      return mNextNode;
  }

  public AddTween(obj: Container | null, time: number, updateFunc?: UpdateFunc, finishFunc?: FinishFunc): TweenItem
  {
      const mItem: TweenItem = this.mItemPool.Pop();
      mItem.bindObj = obj ?? KTweenMgr.GetInstance().bindObj;
      mItem.toggle = true;
      mItem.time = 0;
      mItem.sumTime = time;
      mItem.updateFunc = updateFunc ?? null;
      mItem.finishFunc = finishFunc ?? null;
      this.mTweenT.AddLast(mItem.mEntry);
      return mItem;
  }

  public SetMaxTweenCount(nCount: number): void 
  {
      this.mItemPool.SetMaxCapacity(nCount);
  }

  public CancelAll(): void 
  {
      let mNode: LinkedListNode<TweenItem> | null = this.mTweenT.First;
      while (mNode !== null) 
      {
          mNode.Value.toggle = false;
          mNode = mNode.Next;
      }
  }

  public Cancel(obj: Container): void 
  {
      let mNode: LinkedListNode<TweenItem> | null = this.mTweenT.First;
      while (mNode !== null) 
      {
        if (mNode.Value.bindObj === obj) 
        {
          mNode.Value.toggle = false;
        }
        mNode = mNode.Next;
      }
  }
}

/** TweenItem 的对象池 */
class ObjectPool 
{
  private readonly mObjectPool: TweenItem[] = [];
  private nMaxCapacity: number = 1024;

  public SetMaxCapacity(nCount: number): void {
    this.nMaxCapacity = nCount;
  }

  //! 是 TypeScript 的非空断言操作符（Non-null Assertion Operator）。
  //作用 告诉编译器："我保证这个值不是 null 也不是 undefined，你别报错了。"
  public Pop(): TweenItem 
  {
    if (this.mObjectPool.length > 0) {
      return this.mObjectPool.pop()!;
    }
    return new TweenItem();
  }

  public Recycle(t: TweenItem): void {
    t.Reset();
    if (this.mObjectPool.length < this.nMaxCapacity) {
      this.mObjectPool.push(t);
    }
  }
}
