using NUnit.Framework;

namespace Zizo.Reversi.Tests
{
    public class ReversiBoardGeometryTests
    {
        private static ReversiBoardGeometry CreateGeometry(
            ReversiBoard board,
            ReversiCpuSettings settings = null
        ) => new(board, settings ?? new ReversiCpuSettings());

        // 通常の8x8では、4つの角が隅になる
        [Test]
        public void IsCorner_DetectsFourCorners_OnStandardBoard()
        {
            ReversiBoardGeometry geometry = CreateGeometry(ReversiTestHelper.CreateStandardBoard());

            int cornerCount = 0;
            for (int cell = 0; cell < 64; cell++)
            {
                if (geometry.IsCorner(cell))
                    cornerCount++;
            }

            Assert.AreEqual(4, cornerCount);
            Assert.IsTrue(geometry.IsCorner(0));
            Assert.IsTrue(geometry.IsCorner(63));
        }

        // 辺と内側のマスを判定できる
        [Test]
        public void IsEdge_DetectsEdgeAndInner_OnStandardBoard()
        {
            ReversiBoardGeometry geometry = CreateGeometry(ReversiTestHelper.CreateStandardBoard());

            Assert.IsTrue(geometry.IsEdge(3));
            Assert.IsFalse(geometry.IsEdge(3 * 8 + 3));
            Assert.IsFalse(geometry.IsEdge(1 * 8 + 1));
        }

        // 角を壁で削った盤では、削った角の隣が隅になる
        [Test]
        public void IsCorner_DetectsCorners_OnShapedBoard()
        {
            ReversiBoardGeometry geometry = CreateGeometry(
                ReversiTestHelper.CreateBoard("#...#", ".....", ".....", ".....", "#...#")
            );

            Assert.IsTrue(geometry.IsCorner(1));
            Assert.IsTrue(geometry.IsCorner(1 * 5 + 0));
            Assert.IsFalse(geometry.IsCorner(2));
            Assert.IsTrue(geometry.IsEdge(2));
        }

        // 隅・辺・内側に、設定の重みが入る
        [Test]
        public void PositionWeights_UseSettings()
        {
            var settings = new ReversiCpuSettings
            {
                CornerWeight = 50,
                EdgeWeight = 5,
                InnerWeight = 1,
            };
            ReversiBoardGeometry geometry = CreateGeometry(
                ReversiTestHelper.CreateStandardBoard(),
                settings
            );

            Assert.AreEqual(50, geometry.PositionWeights[0]);
            Assert.AreEqual(5, geometry.PositionWeights[3]);
            Assert.AreEqual(1, geometry.PositionWeights[3 * 8 + 3]);
            Assert.AreEqual(50, geometry.PositionScale);
        }

        // 重みを直接指定した場合は、その値を使う
        [Test]
        public void PositionWeights_UseCustomWeights()
        {
            var settings = new ReversiCpuSettings
            {
                CustomPositionWeights = new int[,]
                {
                    { 1, 2 },
                    { 3, -4 },
                },
            };
            ReversiBoardGeometry geometry = CreateGeometry(
                ReversiTestHelper.CreateBoard("..", ".."),
                settings
            );

            CollectionAssert.AreEqual(new[] { 1, 2, 3, -4 }, geometry.PositionWeights);
            Assert.IsEmpty(geometry.CornerNeighborCells);
            Assert.AreEqual(4, geometry.PositionScale);
        }

        // 手を調べる順番は、隅が先頭になる
        [Test]
        public void PlayableCells_StartWithCorner()
        {
            ReversiBoardGeometry geometry = CreateGeometry(ReversiTestHelper.CreateStandardBoard());

            Assert.IsTrue(geometry.IsCorner(geometry.PlayableCells[0]));
            Assert.AreEqual(64, geometry.PlayableCells.Length);
        }
    }
}
