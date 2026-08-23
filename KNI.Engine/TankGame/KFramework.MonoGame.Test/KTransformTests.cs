using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using System;

namespace KFramework.MonoGame.Test
{
    [TestClass]
    public class KTransformTests
    {
        private const float Eps = 1e-4f;

        private static void AssertVec(Vector2 expected, Vector2 actual, string msg)
        {
            Assert.IsTrue(
                Math.Abs(expected.X - actual.X) < Eps &&
                Math.Abs(expected.Y - actual.Y) < Eps,
                $"{msg}: expected ({expected.X}, {expected.Y}) but got ({actual.X}, {actual.Y})");
        }

        // 1. 修复前的致命 bug：WorldPosition += Zero 会把自身位置清零
        [TestMethod]
        public void WorldPosition_PlusZero_DoesNotChange()
        {
            var t = new KTransform();
            t.LocalPosition = new Vector2(123, 456);
            var before = t.WorldPosition;

            t.WorldPosition += Vector2.Zero;

            AssertVec(before, t.WorldPosition, "WorldPosition += Zero should keep position");
            AssertVec(before, t.LocalPosition, "LocalPosition should be unchanged after += Zero");
        }

        // 2. WorldPosition setter 能正确设置世界位置（无父节点）
        [TestMethod]
        public void WorldPosition_Set_NoParent()
        {
            var t = new KTransform();
            t.WorldPosition = new Vector2(50, -30);
            AssertVec(new Vector2(50, -30), t.WorldPosition, "WorldPosition setter");
            AssertVec(new Vector2(50, -30), t.LocalPosition, "LocalPosition matches WorldPosition when no parent");
        }

        // 3. 无父节点时 LocalPosition == WorldPosition
        [TestMethod]
        public void LocalEqualsWorld_WhenNoParent()
        {
            var t = new KTransform();
            t.LocalPosition = new Vector2(10, 20);
            AssertVec(new Vector2(10, 20), t.WorldPosition, "no parent: world == local");
        }

        // 4. 父子层级：子世界位置 = 父世界位置 + 子本地位置
        [TestMethod]
        public void ChildWorldPosition_AddsParent()
        {
            var parent = new KTransform();
            parent.LocalPosition = new Vector2(100, 100);
            var child = new KTransform();
            child.LocalPosition = new Vector2(20, 30);
            child.Parent = parent;

            AssertVec(new Vector2(120, 130), child.WorldPosition, "child world = parent + local");
            AssertVec(new Vector2(100, 100), parent.WorldPosition, "parent world");
        }

        // 5. 父节点移动后，子节点世界位置随之更新（脏标记传播）
        [TestMethod]
        public void ParentMove_UpdatesChildWorld()
        {
            var parent = new KTransform();
            parent.LocalPosition = new Vector2(0, 0);
            var child = new KTransform();
            child.LocalPosition = new Vector2(10, 10);
            child.Parent = parent;

            AssertVec(new Vector2(10, 10), child.WorldPosition, "initial child world");

            parent.LocalPosition = new Vector2(200, 50);
            AssertVec(new Vector2(210, 60), child.WorldPosition, "child world after parent moved");
        }

        // 6. LocalToWorld：局部点经父节点旋转后正确变换
        [TestMethod]
        public void LocalToWorld_AppliesParentRotation()
        {
            var parent = new KTransform();
            parent.LocalRotation = MathHelper.PiOver2; // 旋转 90°
            var child = new KTransform();
            child.LocalPosition = new Vector2(0, 10);  // 局部 (0,10)
            child.Parent = parent;

            // 父旋转 90° 后，局部 (0,10) 旋转到世界 (-10, 0)
            AssertVec(new Vector2(-10, 0), child.WorldPosition, "child world after parent 90° rotation");

            // 子自身局部点 (1,0) 经父 90° 旋转后方向为 (0,1)，加到子原点 (-10,0) => (-10, 1)
            Vector2 p = child.LocalToWorld(new Vector2(1, 0));
            AssertVec(new Vector2(-10, 1), p, "child LocalToWorld(1,0) with parent rotation");
        }

        // 7. LocalToWorld 与 WorldToLocal 互逆
        [TestMethod]
        public void LocalToWorld_WorldToLocal_RoundTrip()
        {
            var parent = new KTransform();
            parent.LocalPosition = new Vector2(40, -20);
            parent.LocalRotation = 0.7f;
            parent.LocalScale = new Vector2(2f, 0.5f);
            var child = new KTransform();
            child.LocalPosition = new Vector2(15, 25);
            child.LocalRotation = -0.3f;
            child.LocalScale = new Vector2(1.5f, 1.5f);
            child.Parent = parent;

            Vector2 world = child.LocalToWorld(new Vector2(3, 4));
            Vector2 back = child.WorldToLocal(world);
            AssertVec(new Vector2(3, 4), back, "round-trip LocalToWorld->WorldToLocal");
        }

        // 8. WorldToLocal：世界点落入子节点应得正确的局部坐标
        [TestMethod]
        public void WorldToLocal_MapsWorldPointToLocal()
        {
            var parent = new KTransform();
            parent.LocalPosition = new Vector2(100, 100);
            var child = new KTransform();
            child.LocalPosition = new Vector2(0, 0); // 子局部原点在父 (100,100)
            child.Parent = parent;

            // 世界点 (100,100) 是子的局部原点 => 应得到 (0,0)
            AssertVec(new Vector2(0, 0), child.WorldToLocal(new Vector2(100, 100)), "world (100,100) is child local origin");

            // 世界点 (110,100) => 子局部 (10,0)
            AssertVec(new Vector2(10, 0), child.WorldToLocal(new Vector2(110, 100)), "world (110,100) is child local (10,0)");
        }

        // 9. WorldRotation 累加父旋转
        [TestMethod]
        public void WorldRotation_AddsParentRotation()
        {
            var parent = new KTransform();
            parent.LocalRotation = 0.5f;
            var child = new KTransform();
            child.LocalRotation = 0.3f;
            child.Parent = parent;

            Assert.AreEqual(0.8f, child.WorldRotation, Eps, "world rotation = parent + child");
        }

        // 10. WorldScale 累乘父缩放
        [TestMethod]
        public void WorldScale_MultipliesParentScale()
        {
            var parent = new KTransform();
            parent.LocalScale = new Vector2(2f, 3f);
            var child = new KTransform();
            child.LocalScale = new Vector2(1.5f, 0.5f);
            child.Parent = parent;

            AssertVec(new Vector2(3f, 1.5f), child.WorldScale, "world scale = parent * child");
        }

        // 11. WorldPosition setter 在有父节点时反算正确的 LocalPosition
        [TestMethod]
        public void WorldPosition_Set_WithParent()
        {
            var parent = new KTransform();
            parent.LocalPosition = new Vector2(100, 100);
            var child = new KTransform();
            child.Parent = parent;

            child.WorldPosition = new Vector2(150, 130); // 期望 child 局部 (50,30)
            AssertVec(new Vector2(150, 130), child.WorldPosition, "child world after set");
            AssertVec(new Vector2(50, 30), child.LocalPosition, "child local = world - parent");
        }

        // 12. 移除父节点（Unity 默认 worldPositionStays=false）：LocalPosition 不变，WorldPosition 回落为旧 LocalPosition
        [TestMethod]
        public void DetachParent_DefaultWorldPositionStaysFalse()
        {
            var parent = new KTransform();
            parent.LocalPosition = new Vector2(100, 100);
            var child = new KTransform();
            child.LocalPosition = new Vector2(20, 30);
            child.Parent = parent;
            AssertVec(new Vector2(120, 130), child.WorldPosition, "before detach");

            child.Parent = null;
            // Parent setter 不补偿 LocalPosition（同 Unity 默认），故 WorldPosition 变为旧 LocalPosition
            AssertVec(new Vector2(20, 30), child.WorldPosition, "world after detach = old local");
            AssertVec(new Vector2(20, 30), child.LocalPosition, "local unchanged after detach");
        }

        // 13. 脏标记：连续多次访问不崩溃且结果稳定
        [TestMethod]
        public void DirtyCache_StableAcrossAccess()
        {
            var parent = new KTransform();
            parent.LocalPosition = new Vector2(10, 10);
            var child = new KTransform();
            child.LocalPosition = new Vector2(5, 5);
            child.Parent = parent;

            var a = child.WorldPosition;
            var b = child.WorldPosition; // 第二次应命中缓存
            var c = child.WorldPosition;
            AssertVec(a, b, "cached access equal #1");
            AssertVec(a, c, "cached access equal #2");
        }

        // 14. 多次 SetDirty 递归（深层嵌套子节点）正确刷新
        [TestMethod]
        public void DeepNesting_ParentMove_UpdatesAll()
        {
            var root = new KTransform();
            var a = new KTransform(); a.LocalPosition = new Vector2(10, 0); a.Parent = root;
            var b = new KTransform(); b.LocalPosition = new Vector2(10, 0); b.Parent = a;
            var c = new KTransform(); c.LocalPosition = new Vector2(10, 0); c.Parent = b;

            AssertVec(new Vector2(30, 0), c.WorldPosition, "deep child initial");
            root.LocalPosition = new Vector2(100, 0);
            AssertVec(new Vector2(130, 0), c.WorldPosition, "deep child after root move");
        }

        [TestMethod]
        public void DeepNesting_Equal()
        {
            var A = new KTransform();
            object B = null;
            var C = new KTransform();

            Assert.IsTrue(A != B);
            Assert.IsTrue(B != A);
            Assert.IsTrue(A != C);
            Assert.IsTrue(C != A);
            Assert.IsTrue(B == null);
            Assert.IsTrue(null == B);
            Assert.IsTrue(A == A);

            A.Dispose();
            Assert.IsTrue(A == null);
            Assert.IsTrue(null == A);
            Assert.IsTrue(A != B);
            Assert.IsTrue(B != A);
            Assert.IsTrue(A != C);
            Assert.IsTrue(C != A);
            Assert.IsTrue(A == A);
        }
    }
}
