import { Container } from "pixi.js";
import type { Ticker } from "pixi.js";

import { KTime } from "../GameEngine/KTime";
import { IDisposable } from "../Tool/IDisposable";
import { LinkedListNode } from "../Tool/LinkedList";
import { KTweenByLinkedList } from "./KTweenByLinkedList";
import { KTweenType } from "./KTweenFunc";

/** 每帧回调，参数是【已经过缓动处理】的进度 [0,1] */
export type UpdateFunc = (fPercent: number) => void;
/** 播放完成回调 */
export type FinishFunc = () => void;

/**
 * KTween —— 从 MonoGame 版移植过来的轻量补间库（PixiJS 版）。
 *
 * 对应关系：
 * - C# 的 KTransform 绑定对象 -> Pixi 的 {@link Container}
 * - C# 的 Vector2            -> Pixi 的 Point / PointData
 * - C# 的 KTime.deltaTime    -> {@link KTime}.deltaTime（秒）
 *
 * 每帧需要驱动一次，推荐在主循环里调用 {@link KTween.update}：
 * ```ts
 * KUpdateMgr.AddListener(() => KTween.update(engine().ticker));
 * ```
 */
export class KTween {
  public static SetMaxTweenCount(nCount: number): void {
    KTweenMgr.Instance.SetMaxTweenCount(nCount);
  }

  public static GetHandle(mTSharePtr: TweenItem): KTweenHandle {
    return new KTweenHandle(mTSharePtr);
  }

  /** 不绑定对象（跟随全局生命周期）的补间 */
  public static AddTween(
    time: number,
    updateFunc?: UpdateFunc,
    finishFunc?: FinishFunc,
  ): TweenItem;
  /** 绑定到某个显示对象的补间，对象被 {@link KTween.Cancel} 时可以整组取消 */
  public static AddTween(
    obj: Container,
    time: number,
    updateFunc?: UpdateFunc,
    finishFunc?: FinishFunc,
  ): TweenItem;
  public static AddTween(
    a: number | Container,
    b?: number | UpdateFunc,
    c?: UpdateFunc | FinishFunc,
    d?: FinishFunc,
  ): TweenItem {
    if (typeof a === "number") {
      return KTweenMgr.Instance.AddTween(
        a,
        b as UpdateFunc | undefined,
        c as FinishFunc | undefined,
      );
    }

    return KTweenMgr.Instance.AddTween(
      a,
      b as number,
      c as UpdateFunc | undefined,
      d,
    );
  }

  public static delayedCall(time: number, finishFunc?: FinishFunc): TweenItem;
  public static delayedCall(
    obj: Container,
    time: number,
    finishFunc?: FinishFunc,
  ): TweenItem;
  public static delayedCall(
    a: number | Container,
    b?: number | FinishFunc,
    c?: FinishFunc,
  ): TweenItem {
    if (typeof a === "number") {
      return KTween.AddTween(a, undefined, b as FinishFunc | undefined);
    }
    return KTween.AddTween(a, b as number, undefined, c);
  }

  public static CancelAll(): void {
    KTweenMgr.Instance.CancelAll();
  }

  /** 取消某个对象上的全部补间，或者取消指定句柄 */
  public static Cancel(target: Container | KTweenHandle): void {
    if (target instanceof KTweenHandle) {
      target.Cancel();
      return;
    }
    KTweenMgr.Instance.Cancel(target);
  }

  /** 每帧驱动一次：推进 KTime，再推进所有补间 */
  public static update(ticker: Ticker): void {
    KTime.From(ticker);
    KTweenMgr.Instance.Update();
  }
}

/**
 * 补间句柄 —— 对应 C# 里的 KTween.Handle（struct 改成了 class）。
 * 内部记录 TweenItem 的版本号，回收复用后旧句柄会自动失效。
 */
export class KTweenHandle implements IDisposable {
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
export class TweenItem {
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

  /** 取消自己以及串在自己后面的整条链 */
  public cancel(): TweenItem {
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

/** TweenItem 的对象池 */
export class ObjectPool {
  private readonly mObjectPool: TweenItem[] = [];
  private nMaxCapacity: number = 1024;

  public SetMaxCapacity(nCount: number): void {
    this.nMaxCapacity = nCount;
  }

  public Pop(): TweenItem {
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

/**
 * 补间管理器（单例）。
 * bindObj 是"全局绑定对象"：不指定对象创建的补间会挂到它上面，
 * 这样就不会因为 bindObj == null 而在第一帧被回收。
 */
export class KTweenMgr 
{
  private static m_Instance: KTweenMgr | null = null;

  private constructor() {}

  public static get GetInstance(): KTweenMgr 
  {
      if (KTweenMgr.m_Instance === null) {
        KTweenMgr.m_Instance = new KTweenMgr();
      }
      return KTweenMgr.m_Instance;
  }

  private readonly mManager: KTweenByLinkedList = new KTweenByLinkedList();
  public readonly bindObj: Container = new Container();
  
  public Update(): void {
    this.mManager.Update();
  }

  /** 手动指定 deltaTime（秒）推进一帧，方便脱离 Ticker 做单元测试 */
  public UpdateWith(deltaTime: number): void {
    this.mManager.UpdateWith(deltaTime);
  }

  public SetMaxTweenCount(nCount: number): void {
    this.mManager.SetMaxTweenCount(nCount);
  }

  public CancelAll(): void {
    this.mManager.CancelAll();
  }

  public Cancel(obj: Container): void {
    this.mManager.Cancel(obj);
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
    if (typeof a === "number") {
      return this.mManager.AddTween(
        a,
        b as UpdateFunc | undefined,
        c as FinishFunc | undefined,
      );
    }

    return this.mManager.AddTween(
      a,
      b as number,
      c as UpdateFunc | undefined,
      d,
    );
  }

  public delayedCall(time: number, finishFunc?: FinishFunc): TweenItem;
  public delayedCall(
    obj: Container,
    time: number,
    finishFunc?: FinishFunc,
  ): TweenItem;
  public delayedCall(
    a: number | Container,
    b?: number | FinishFunc,
    c?: FinishFunc,
  ): TweenItem {
    if (typeof a === "number") {
      return this.AddTween(a, undefined, b as FinishFunc | undefined);
    }
    return this.AddTween(a, b as number, undefined, c);
  }
}
