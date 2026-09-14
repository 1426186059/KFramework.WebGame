using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace KFramework.MonoGame
{
    public static partial class KTween
    {
        public static void SetMaxTweenCount(int nCount)
        {
            KTweenMgr.Instance.SetMaxTweenCount(nCount);
        }

        public static Handle GetHandle(TweenItem mTSharePtr)
        {
            return new Handle(mTSharePtr);
        }

        public static TweenItem AddTween(float time, Action<float> updateFunc = null, Action finishFunc = null)
        {
            return KTweenMgr.Instance.AddTween(time, updateFunc, finishFunc);
        }

        public static TweenItem AddTween(KTransform obj, float time, Action<float> updateFunc = null, Action finishFunc = null)
        {
            return KTweenMgr.Instance.AddTween(obj, time, updateFunc, finishFunc);
        }

        public static TweenItem delayedCall(float time, Action finishFunc = null)
        {
            return AddTween(time, null, finishFunc);
        }

        public static TweenItem delayedCall(KTransform obj, float time, Action finishFunc = null)
        {
            return AddTween(obj, time, null, finishFunc);
        }

        public static void CancelAll()
        {
            KTweenMgr.Instance.CancelAll();
        }

        public static void Cancel(KTransform obj)
        {
            KTweenMgr.Instance.Cancel(obj);
        }

        public static void Cancel(Handle handle)
        {
            handle.Cancel();
        }

        public struct Handle : IDisposable
        {
            private uint nVersion;
            private TweenItem mInnerPtr;

            public Handle(TweenItem mItem)
            {
                this.mInnerPtr = mItem;
                this.nVersion = mItem.nVersion;
            }

            public bool IsValid()
            {
                return mInnerPtr != null && mInnerPtr.nVersion == nVersion;
            }

            public void AppendTween(Handle mOtherTween)
            {
                if (IsValid() && mOtherTween.IsValid())
                {
                    mInnerPtr.AppendTween(mOtherTween.mInnerPtr);
                }
                else
                {
                    this.mInnerPtr = mOtherTween.mInnerPtr;
                    this.nVersion = mOtherTween.mInnerPtr.nVersion;
                }
            }

            public void AppendTween(TweenItem mOtherTween)
            {
                if (IsValid() && mOtherTween != null)
                {
                    mInnerPtr.AppendTween(mOtherTween);
                }
                else
                {
                    this.mInnerPtr = mOtherTween;
                    this.nVersion = mOtherTween.nVersion;
                }
            }

            public void Cancel()
            {
                if (IsValid())
                {
                    mInnerPtr.cancel();
                }

                mInnerPtr = null;
                nVersion = 0;
            }

            public void Dispose()
            {
                Cancel();
            }
        };

        public class KTweenMgr:SingleTonMonoBehaviour<KTweenMgr>
        {
            private readonly KTweenByLinkedList mManager = new KTweenByLinkedList();
            public readonly KTransform bindObj = new KTransform();

            public override void Update()
            {
                mManager.Update();
            }

            public void SetMaxTweenCount(int nCount)
            {
                mManager.SetMaxTweenCount(nCount);
            }

            public void CancelAll()
            {
                mManager.CancelAll();
            }

            public void Cancel(KTransform obj)
            {
                mManager.Cancel(obj);
            }

            public TweenItem AddTween(float time, Action<float> updateFunc = null, Action finishFunc = null)
            {
                return mManager.AddTween(time, updateFunc, finishFunc);
            }

            public TweenItem AddTween(KTransform obj, float time, Action<float> updateFunc = null, Action finishFunc = null)
            {
                return mManager.AddTween(obj, time, updateFunc, finishFunc);
            }

            public TweenItem delayedCall(float time, Action finishFunc = null)
            {
                return AddTween(time, null, finishFunc);
            }

            public TweenItem delayedCall(KTransform obj, float time, Action finishFunc = null)
            {
                return AddTween(obj, time, null, finishFunc);
            }
        }

        public class TweenItem
        {
            public readonly LinkedListNode<TweenItem> mEntry;
            public TweenItem SqeNext;

            public uint nVersion;
            public KTransform bindObj;
            public bool toggle;
            public float delay;
            public float time = 0f;
            public float sumTime = 0f;
            public int nLoopCount = 0;
            public int nLoopPingTong = 0;
            public KTweenType nType = KTweenType.linear;
            public Action<float> updateFunc = null;
            public Action finishFunc = null;

            public TweenItem()
            {
                mEntry = new LinkedListNode<TweenItem>(this);
                Reset();
            }

            public void Reset()
            {
                SqeNext = null;
                nVersion++;
                bindObj = null;
                toggle = false;

                delay = 0f;
                time = 0f;
                sumTime = 0f;
                updateFunc = null;
                finishFunc = null;

                nLoopCount = 0;
                nLoopPingTong = 0;
                nType = KTweenType.linear;
            }

            public Handle GetHandle()
            {
                return new Handle(this);
            }

            public TweenItem cancel()
            {
                if (toggle)
                {
                    toggle = false;

                    var mSqeNext = SqeNext;
                    while (mSqeNext != null)
                    {
                        mSqeNext.toggle = false;
                        mSqeNext = mSqeNext.SqeNext;
                    }
                }
                return this;
            }

            public TweenItem SetDelay(float fTime)
            {
                this.delay = fTime;
                return this;
            }

            public TweenItem SetLoop(int nLoopCount = -1)
            {
                this.nLoopCount = nLoopCount;
                return this;
            }

            public TweenItem SetLoopPingPong(int nLoopCount = -1)
            {
                this.nLoopCount = nLoopCount;
                nLoopPingTong = 1;
                return this;
            }

            public TweenItem AppendTween(TweenItem mItem)
            {
                float mTweenSumTime = this.delay + this.sumTime;
                mItem.delay += mTweenSumTime;
                SqeNext = mItem;
                return this;
            }

            public TweenItem SetOnCompleteFunc(Action mFunc)
            {
                this.finishFunc = mFunc;
                return this;
            }

            public TweenItem SetOnUpdateFunc(Action<float> mFunc)
            {
                this.updateFunc = mFunc;
                return this;
            }

            public TweenItem SetEase(KTweenType easeType)
            {
                this.nType = easeType;
                return this;
            }
        }

        private class ObjectPool
        {
            readonly Stack<TweenItem> mObjectPool = new Stack<TweenItem>();
            int nMaxCapacity = 1024;

            public void SetMaxCapacity(int nCount)
            {
                this.nMaxCapacity = nCount;
            }

            public TweenItem Pop()
            {
                if (mObjectPool.Count > 0)
                {
                    return mObjectPool.Pop();
                }
                else
                {
                    return new TweenItem();
                }
            }

            public void recycle(TweenItem t)
            {
                t.Reset();
                if (mObjectPool.Count < nMaxCapacity)
                {
                    mObjectPool.Push(t);
                }
            }
        }
    }
}
