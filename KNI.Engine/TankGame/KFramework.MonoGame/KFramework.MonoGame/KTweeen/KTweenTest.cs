using Microsoft.Xna.Framework;
using System;

namespace KFramework.MonoGame
{
    /// <summary>
    /// 纯 C# 测试类（无 Unity 依赖），用 KTransform 验证 KTween 行为。
    /// 在游戏循环中调用 RunAll() 即可手动验证，或通过 [TestClass] 单元测试调用。
    /// </summary>
    public static class KTweenTest
    {
        static void Test1()
        {
            Console.WriteLine("[Test1] 单段 moveX");
            KTransform obj = new KTransform();
            KTweenEx.moveX(obj, 5, 1.0f);

            if (Math.Abs(obj.LocalPosition.X - 5) < 0.0001f)
                Console.WriteLine("    PASS: x=" + obj.LocalPosition.X);
            else
                Console.WriteLine("    FAIL: x=" + obj.LocalPosition.X);
        }

        static void Test2()
        {
            Console.WriteLine("[Test2] 链式两段 moveX");
            KTransform obj = new KTransform();
            KTweenEx.moveX(obj, 5, 1.0f).AppendTween(KTweenEx.moveX(obj, 10, 1.0f));

            if (Math.Abs(obj.LocalPosition.X - 10) < 0.0001f)
                Console.WriteLine("    PASS: x=" + obj.LocalPosition.X);
            else
                Console.WriteLine("    FAIL: x=" + obj.LocalPosition.X);
        }

        static void Test3()
        {
            Console.WriteLine("[Test3] delayedCall 回调");
            bool called = false;
            KTween.delayedCall(0.5f, () => called = true);

            if (called)
                Console.WriteLine("    PASS: finishFunc 已触发");
            else
                Console.WriteLine("    FAIL: finishFunc 未触发");
        }

        public static void RunAll()
        {
            Test1();
            Test2();
            Test3();
        }
    }
}
