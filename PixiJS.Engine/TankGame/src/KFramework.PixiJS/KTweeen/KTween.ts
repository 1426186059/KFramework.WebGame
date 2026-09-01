import { Container, Ticker } from "pixi.js";

import { IDisposable } from "../Tool/IDisposable";
import { LinkedListNode } from "../Tool/LinkedList";
import { KTweenMgr } from "./KTweenMgr";
import { KTweenType } from "./KTweenFunc";

export type UpdateFunc = (fPercent: number) => void;
export type FinishFunc = () => void;

export class KTween 
{
  public static SetMaxTweenCount(nCount: number): void 
  {
      KTweenMgr.GetInstance().SetMaxTweenCount(nCount);
  }

  public static GetHandle(mTSharePtr: TweenItem): KTweenHandle 
  {
      return new KTweenHandle(mTSharePtr);
  }

  /** 不绑定对象（跟随全局生命周期）的补间 */
  public static AddTween(time: number, updateFunc?: UpdateFunc, finishFunc?: FinishFunc): TweenItem;
  public static AddTween(obj: Container,time: number,updateFunc?: UpdateFunc,finishFunc?: FinishFunc): TweenItem;
  public static AddTween(a: number | Container, b?: number | UpdateFunc, c?: UpdateFunc | FinishFunc, d?: FinishFunc): TweenItem 
  {
    if (typeof a === "number") 
    {
      return KTweenMgr.GetInstance().AddTween(null,
        a,
        b as UpdateFunc | undefined,
        c as FinishFunc | undefined,
      );
    }

    return KTweenMgr.GetInstance().AddTween(
      a,
      b as number,
      c as UpdateFunc | undefined,
      d,
    );
  }

  public static delayedCall(time: number, finishFunc?: FinishFunc): TweenItem;
  public static delayedCall(obj: Container, time: number, finishFunc?: FinishFunc): TweenItem;
  public static delayedCall(a: number | Container, b?: number | FinishFunc, c?: FinishFunc): TweenItem 
  {
    if (typeof a === "number") 
    {
        return KTween.AddTween(a, undefined, b as FinishFunc | undefined);
    }
    return KTween.AddTween(a, b as number, undefined, c);
  }

  public static CancelAll(): void 
  {
    KTweenMgr.GetInstance().CancelAll();
  }

  public static Cancel(target: Container | KTweenHandle): void 
  {
    if (target instanceof KTweenHandle) 
    {
        target.Cancel();
        return;
    }
    KTweenMgr.GetInstance().Cancel(target);
  }
}

/**
 * 补间句柄 —— 对应 C# 里的 KTween.Handle（struct 改成了 class）。
 * 内部记录 TweenItem 的版本号，回收复用后旧句柄会自动失效。
 */
export class KTweenHandle implements IDisposable 
{
  private nVersion: number;
  private mInnerPtr: TweenItem | null;

  public constructor(mItem: TweenItem) {
    this.mInnerPtr = mItem;
    this.nVersion = mItem.nVersion;
  }

  public IsValid(): boolean {
    return this.mInnerPtr !== null && this.mInnerPtr.nVersion === this.nVersion;
  }

  public AppendTween(mOtherTween: KTweenHandle | TweenItem): void {
    const other: TweenItem | null =
      mOtherTween instanceof KTweenHandle ? mOtherTween.mInnerPtr : mOtherTween;

    if (other === null) {
      return;
    }

    if (this.IsValid()) {
      this.mInnerPtr!.AppendTween(other);
    } else {
      this.mInnerPtr = other;
      this.nVersion = other.nVersion;
    }
  }

  public Cancel(): void {
    if (this.IsValid()) {
      this.mInnerPtr!.cancel();
    }

    this.mInnerPtr = null;
    this.nVersion = 0;
  }

  public Dispose(): void {
    this.Cancel();
  }
}

/** 单个补间的数据（由 {@link ObjectPool} 复用，靠 nVersion 做句柄校验） */
export class TweenItem 
{
  public readonly mEntry: LinkedListNode<TweenItem>;
  /** 串行播放的下一个补间（C# 里就拼成 SqeNext，这里保留原拼写） */
  public SqeNext: TweenItem | null = null;

  public nVersion: number = 0;
  public bindObj: Container | null = null;
  public toggle: boolean = false;
  public delay: number = 0;
  public time: number = 0;
  public sumTime: number = 0;
  public nLoopCount: number = 0;
  /** 0 = 非乒乓；1 = 正向；2 = 反向 */
  public nLoopPingTong: number = 0;
  public nType: KTweenType = KTweenType.linear;
  public updateFunc: UpdateFunc | null = null;
  public finishFunc: FinishFunc | null = null;

  public constructor() {
    this.mEntry = new LinkedListNode<TweenItem>(this);
    this.Reset();
  }

  public Reset(): void {
    this.SqeNext = null;
    this.nVersion++;
    this.bindObj = null;
    this.toggle = false;

    this.delay = 0;
    this.time = 0;
    this.sumTime = 0;
    this.updateFunc = null;
    this.finishFunc = null;

    this.nLoopCount = 0;
    this.nLoopPingTong = 0;
    this.nType = KTweenType.linear;
  }

  public GetHandle(): KTweenHandle {
    return new KTweenHandle(this);
  }
  
  public cancel(): TweenItem 
  {
    if (this.toggle) {
      this.toggle = false;

      let mSqeNext: TweenItem | null = this.SqeNext;
      while (mSqeNext !== null) {
        mSqeNext.toggle = false;
        mSqeNext = mSqeNext.SqeNext;
      }
    }
    return this;
  }

  public SetDelay(fTime: number): TweenItem {
    this.delay = fTime;
    return this;
  }

  /** -1 = 无限循环 */
  public SetLoop(nLoopCount: number = -1): TweenItem {
    this.nLoopCount = nLoopCount;
    return this;
  }

  public SetLoopPingPong(nLoopCount: number = -1): TweenItem {
    this.nLoopCount = nLoopCount;
    this.nLoopPingTong = 1;
    return this;
  }

  /** 把另一个补间接到自己后面（累加延迟实现串行） */
  public AppendTween(mItem: TweenItem): TweenItem {
    const mTweenSumTime: number = this.delay + this.sumTime;
    mItem.delay += mTweenSumTime;
    this.SqeNext = mItem;
    return this;
  }

  public SetOnCompleteFunc(mFunc: FinishFunc): TweenItem {
    this.finishFunc = mFunc;
    return this;
  }

  public SetOnUpdateFunc(mFunc: UpdateFunc): TweenItem {
    this.updateFunc = mFunc;
    return this;
  }

  public SetEase(easeType: KTweenType): TweenItem {
    this.nType = easeType;
    return this;
  }
}
