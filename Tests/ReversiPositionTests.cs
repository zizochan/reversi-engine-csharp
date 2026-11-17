using System.Collections.Generic;
using NUnit.Framework;

namespace Zizo.Reversi.Tests
{
    public class ReversiPositionTests
    {
        // X と Y が同じなら同じ座標とみなす
        [Test]
        public void Equals_ComparesXAndY()
        {
            var position = new ReversiPosition(3, 4);

            Assert.IsTrue(position == new ReversiPosition(3, 4));
            Assert.IsTrue(position.Equals((object)new ReversiPosition(3, 4)));
            Assert.IsTrue(position != new ReversiPosition(4, 3));
            Assert.IsFalse(position.Equals(null));
        }

        // Contains() や Dictionary のキーとして使える
        [Test]
        public void GetHashCode_AllowsCollectionLookup()
        {
            var positions = new HashSet<ReversiPosition> { new(3, 4) };

            Assert.IsTrue(positions.Contains(new ReversiPosition(3, 4)));
            Assert.IsFalse(positions.Contains(new ReversiPosition(4, 3)));
        }

        // CPUの候補手の座標を、合法手の一覧と比べられる
        [Test]
        public void MovePosition_MatchesLegalMove()
        {
            ReversiBoard board = ReversiTestHelper.CreateStandardBoard();
            ReversiSearchResult result = ReversiCpuSearcher.Search(
                board,
                player: 0,
                playerCount: 2
            );

            IReadOnlyList<ReversiPosition> legalMoves = ReversiRules.GetLegalMoves(
                board,
                player: 0
            );
            foreach (ReversiMove move in result.Moves)
            {
                CollectionAssert.Contains(legalMoves, move.Position);
            }
        }

        // デバッグ用に (x, y) の形で表す
        [Test]
        public void ToString_ReturnsCoordinateText()
        {
            Assert.AreEqual("(3, 4)", new ReversiPosition(3, 4).ToString());
        }
    }
}
