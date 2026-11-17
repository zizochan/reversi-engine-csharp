using System.Linq;
using NUnit.Framework;

namespace Zizo.Reversi.Tests
{
    public class ReversiSearchBoardTests
    {
        private const int PlayerCount = 3;

        private static ReversiBoard CreateThreePlayerBoard() =>
            ReversiTestHelper.CreateBoard(
                "#......#",
                "........",
                "...012..",
                "..2.01..",
                "..120...",
                "........",
                "#......#"
            );

        private static (ReversiSearchBoard searchBoard, ReversiBoardGeometry geometry) Prepare(
            ReversiBoard board
        )
        {
            int[] cells = board.CopyCells();
            var geometry = new ReversiBoardGeometry(board, new ReversiCpuSettings());
            return (new ReversiSearchBoard(cells, geometry, PlayerCount), geometry);
        }

        // 差分で更新した値が、盤面全体を数え直した値と一致する
        private static void AssertMatchesRecount(
            ReversiSearchBoard searchBoard,
            ReversiBoardGeometry geometry
        )
        {
            int[] cells = searchBoard.Cells;
            Assert.AreEqual(
                cells.Count(cell => cell == ReversiBoard.Empty),
                searchBoard.EmptyCount
            );

            var moveBuffer = new int[cells.Length];
            var moveCounts = new int[PlayerCount];
            searchBoard.CountMovesOfAllPlayers(moveCounts);
            for (int player = 0; player < PlayerCount; player++)
            {
                int[] playerCells = geometry
                    .PlayableCells.Where(cell => cells[cell] == player)
                    .ToArray();
                Assert.AreEqual(playerCells.Length, searchBoard.StoneCounts[player]);
                Assert.AreEqual(
                    playerCells.Sum(cell => geometry.PositionWeights[cell]),
                    searchBoard.PositionScores[player]
                );

                // 合法手は、盤面全体を手を調べる順番に調べた場合と同じ順番で返す
                int[] expectedMoves = geometry
                    .PlayableCells.Where(cell =>
                        ReversiRules.CanPlace(cells, geometry.Topology, cell, player)
                    )
                    .ToArray();
                int moveCount = searchBoard.CollectMoves(player, moveBuffer);
                CollectionAssert.AreEqual(expectedMoves, moveBuffer.Take(moveCount));
                Assert.AreEqual(expectedMoves.Length, moveCounts[player]);
            }
        }

        // 作った直後の値が、盤面全体を数え直した値と一致する
        [Test]
        public void Constructor_MatchesRecount()
        {
            var (searchBoard, geometry) = Prepare(CreateThreePlayerBoard());

            AssertMatchesRecount(searchBoard, geometry);
        }

        // 石を何手か置いても、戻しても、盤面全体を数え直した値と一致する
        [Test]
        public void PlaceStoneAndUndo_MatchRecount()
        {
            ReversiBoard board = CreateThreePlayerBoard();
            var (searchBoard, geometry) = Prepare(board);
            int[] original = board.CopyCells();
            var moveBuffer = new int[original.Length];
            var checkpoints = new int[PlayerCount * 2];

            for (int turn = 0; turn < checkpoints.Length; turn++)
            {
                int player = turn % PlayerCount;
                checkpoints[turn] = searchBoard.Checkpoint;
                int moveCount = searchBoard.CollectMoves(player, moveBuffer);
                Assert.Greater(moveCount, 0);
                searchBoard.PlaceStone(moveBuffer[turn % moveCount], player);
                AssertMatchesRecount(searchBoard, geometry);
            }

            for (int turn = checkpoints.Length - 1; turn >= 0; turn--)
            {
                searchBoard.UndoTo(checkpoints[turn]);
                AssertMatchesRecount(searchBoard, geometry);
            }
            CollectionAssert.AreEqual(original, searchBoard.Cells);
        }
    }
}
