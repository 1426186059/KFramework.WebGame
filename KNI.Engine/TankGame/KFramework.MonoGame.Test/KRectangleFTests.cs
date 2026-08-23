using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using System;

namespace KFramework.MonoGame.Test
{
    [TestClass]
    public class KRectangleFTests
    {
        private const float Eps = 1e-4f;

        private static void AssertRect(KRectangleF expected, KRectangleF actual, string msg)
        {
            Assert.IsTrue(
                Math.Abs(expected.X - actual.X) < Eps &&
                Math.Abs(expected.Y - actual.Y) < Eps &&
                Math.Abs(expected.Width - actual.Width) < Eps &&
                Math.Abs(expected.Height - actual.Height) < Eps,
                $"{msg}: expected ({expected.X},{expected.Y},{expected.Width},{expected.Height}) but got ({actual.X},{actual.Y},{actual.Width},{actual.Height})");
        }

        // 1. 基本构造 (x, y, w, h)
        [TestMethod]
        public void Construct_XYWH_SetsFields()
        {
            var r = new KRectangleF(10, 20, 30, 40);
            AssertRect(new KRectangleF(10, 20, 30, 40), r, "ctor xywh");
        }

        // 2. 构造 (location, size)
        [TestMethod]
        public void Construct_LocationSize()
        {
            var r = new KRectangleF(new Vector2(5, 6), new Vector2(7, 8));
            AssertRect(new KRectangleF(5, 6, 7, 8), r, "ctor location size");
        }

        // 3. MinMax(Vector2, Vector2)
        [TestMethod]
        public void MinMax_Vectors_ComputesWidthHeight()
        {
            var r = KRectangleF.MinMax(new Vector2(2, 3), new Vector2(12, 9));
            AssertRect(new KRectangleF(2, 3, 10, 6), r, "MinMax vec");
        }

        // 4. MinMax(float, float, float, float)
        [TestMethod]
        public void MinMax_Floats()
        {
            var r = KRectangleF.MinMax(0, 0, 100, 50);
            AssertRect(new KRectangleF(0, 0, 100, 50), r, "MinMax floats");
        }

        // 5. Left / Right / Top / Bottom
        [TestMethod]
        public void Edges_AreCorrect()
        {
            var r = new KRectangleF(10, 20, 30, 40);
            Assert.AreEqual(10f, r.Left, Eps, "Left");
            Assert.AreEqual(40f, r.Right, Eps, "Right = X+Width");
            Assert.AreEqual(20f, r.Top, Eps, "Top = Y");
            Assert.AreEqual(60f, r.Bottom, Eps, "Bottom = Y+Height");
        }

        // 6. Right/ Bottom setter 改的是 Width/Height
        [TestMethod]
        public void EdgeSetters_UpdateWidthHeight()
        {
            var r = new KRectangleF(10, 20, 30, 40);
            r.Right = 100;   // Width = 100 - 10 = 90
            r.Bottom = 200;  // Height = 200 - 20 = 180
            AssertRect(new KRectangleF(10, 20, 90, 180), r, "edge setters");
        }

        // 7. Center
        [TestMethod]
        public void Center_IsMiddle()
        {
            var r = new KRectangleF(0, 0, 100, 50);
            Assert.AreEqual(50f, r.Center.X, Eps, "center x");
            Assert.AreEqual(25f, r.Center.Y, Eps, "center y");
        }

        // 8. Location / Size 读写
        [TestMethod]
        public void LocationSize_RoundTrip()
        {
            var r = new KRectangleF(1, 2, 3, 4);
            Assert.AreEqual(1f, r.Location.X, Eps);
            Assert.AreEqual(2f, r.Location.Y, Eps);
            Assert.AreEqual(3f, r.Size.X, Eps);
            Assert.AreEqual(4f, r.Size.Y, Eps);

            r.Location = new Vector2(10, 20);
            r.Size = new Vector2(30, 40);
            AssertRect(new KRectangleF(10, 20, 30, 40), r, "location/size set");
        }

        // 9. Contains(float, float) 边界：左闭右开（< X+Width）
        [TestMethod]
        public void Contains_Point_BoundaryExclusive()
        {
            var r = new KRectangleF(0, 0, 10, 10);
            Assert.IsTrue(r.Contains(0f, 0f), "origin inside");
            Assert.IsTrue(r.Contains(9.9f, 9.9f), "near corner inside");
            Assert.IsFalse(r.Contains(10f, 5f), "right edge excluded (== Right)");
            Assert.IsFalse(r.Contains(5f, 10f), "bottom edge excluded (== Bottom)");
            Assert.IsFalse(r.Contains(-1f, 5f), "left out");
        }

        // 10. Contains(Vector2)
        [TestMethod]
        public void Contains_Vector2()
        {
            var r = new KRectangleF(0, 0, 10, 10);
            Assert.IsTrue(r.Contains(new Vector2(5, 5)));
            Assert.IsFalse(r.Contains(new Vector2(11, 5)));
        }

        // 11. Contains(KRectangleF) 完全包含
        [TestMethod]
        public void Contains_Rectangle_FullContainment()
        {
            var outer = new KRectangleF(0, 0, 100, 100);
            var inner = new KRectangleF(10, 10, 20, 20);
            var partial = new KRectangleF(90, 90, 20, 20);
            Assert.IsTrue(outer.Contains(inner), "inner fully contained");
            Assert.IsFalse(outer.Contains(partial), "partial overlap not contained");
        }

        // 13. Offset(int, int) 精确偏移
        [TestMethod]
        public void Offset_Int_MovesExactly()
        {
            var r = new KRectangleF(1, 2, 3, 4);
            r.Offset(10, -5);
            AssertRect(new KRectangleF(11, -3, 3, 4), r, "offset int");
        }

        // 14. Offset(float, float) 精确浮点相加（无截断）
        [TestMethod]
        public void Offset_Float_AddsPrecisely()
        {
            var r = new KRectangleF(1, 2, 3, 4);
            r.Offset(10.9f, -5.4f); // X += 10.9 → 11.9；Y += -5.4 → -3.4
            AssertRect(new KRectangleF(11.9f, -3.4f, 3, 4), r, "offset float precise");
        }

        // 15. Offset(Vector2) 同样精确浮点相加
        [TestMethod]
        public void Offset_Vector2_AddsPrecisely()
        {
            var r = new KRectangleF(0, 0, 3, 4);
            r.Offset(new Vector2(2.8f, 1.2f)); // X += 2.8 → 2.8；Y += 1.2 → 1.2
            AssertRect(new KRectangleF(2.8f, 1.2f, 3, 4), r, "offset vector precise");
        }

        // 16. == / != 运算符
        [TestMethod]
        public void EqualityOperators()
        {
            var a = new KRectangleF(1, 2, 3, 4);
            var b = new KRectangleF(1, 2, 3, 4);
            var c = new KRectangleF(1, 2, 3, 5);
            Assert.IsTrue(a == b, "equal");
            Assert.IsFalse(a != b, "not != when equal");
            Assert.IsTrue(a != c, "diff height not equal");
        }

        // 17. Equals(object) / Equals(KRectangleF)
        [TestMethod]
        public void Equals_Methods()
        {
            var a = new KRectangleF(1, 2, 3, 4);
            var b = new KRectangleF(1, 2, 3, 4);
            Assert.IsTrue(a.Equals((object)b), "Equals(object)");
            Assert.IsTrue(a.Equals(b), "Equals(KRectangleF)");
            Assert.IsFalse(a.Equals(new KRectangleF(0, 0, 0, 0)));
            Assert.IsFalse(a.Equals("not a rect"));
        }

        // 18. GetHashCode 一致
        [TestMethod]
        public void GetHashCode_ConsistentForEqual()
        {
            var a = new KRectangleF(1, 2, 3, 4);
            var b = new KRectangleF(1, 2, 3, 4);
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode(), "equal rects share hash");
        }

        // 19. Empty / IsEmpty
        [TestMethod]
        public void Empty_IsEmpty()
        {
            Assert.IsTrue(KRectangleF.Empty.IsEmpty, "Empty is empty");
            Assert.IsTrue(new KRectangleF(0, 0, 0, 0).IsEmpty, "all zero is empty");
            Assert.IsFalse(new KRectangleF(1, 2, 3, 4).IsEmpty, "normal rect not empty");
        }

        // 20. ToString 格式
        [TestMethod]
        public void ToString_Format()
        {
            var r = new KRectangleF(1, 2, 3, 4);
            StringAssert.Contains(r.ToString(), "X:1");
            StringAssert.Contains(r.ToString(), "Width:3");
        }
    }
}
