using System;
using System.Collections.Generic;

namespace KFramework.MonoGame
{
    public static class GameProfiler
    {
        private static readonly Stack<DateTime> TestStack = new Stack<DateTime>();

        public static void TestStart()
        {
            var StartDate = System.DateTime.Now;
            TestStack.Push(StartDate);
        }

        public static double GetTestFinishSpendTime()
        {
            var FinishDate = System.DateTime.Now;
            PrintTool.Assert(TestStack.Count > 0, "Test 方法 要成对出现  !!!");
            var StartDate = TestStack.Pop();
            return ((FinishDate - StartDate).TotalSeconds);
        }

        public static void ClearTestStack()
        {
            TestStack.Clear();
        }

        public static void TestFinishAndLog(string TAG)
        {
            PrintTool.Log($"GameProfiler [{TAG}]: " + GetTestFinishSpendTime());
        }
    }
}