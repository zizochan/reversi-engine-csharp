using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Zizo.Reversi.Tests
{
    public class ReversiRulesTests
    {
        // ルールの内部処理（盤面の配列とマスのつながりを直接受け取るもの）を試すための準備
        private static (int[] cells, ReversiBoardTopology topology) Prepare(ReversiBoard board) =>
            (board.CopyCells(), board.Topology);

        private static ReversiBoard CreateSmallBoard() =>
            ReversiTestHelper.CreateBoard("....", ".01.", ".10.", "....");

        private static List<(int x, int y)> ToTuples(IEnumerable<ReversiPosition> positions) =>
            positions.Select(position => (position.X, position.Y)).ToList();

        // 挟んだ石を裏返し、変更したマスと変更前の値を、裏返した石 → 置いたマスの順に履歴へ積む
        [Test]
        public void PlaceStone_FlipsStones_AndRecordsPreviousValues()
        {
            var (cells, topology) = Prepare(ReversiTestHelper.CreateBoard(".110"));
            var undoStack = new ReversiUndoStack();

            ReversiRules.PlaceStone(cells, topology, cell: 0, player: 0, undoStack);

            CollectionAssert.AreEqual(new[] { 0, 0, 0, 0 }, cells);
            Assert.AreEqual(3, undoStack.Count);
            CollectionAssert.AreEqual(
                new[] { (1, 1), (2, 1), (0, ReversiBoard.Empty) },
                Enumerable
                    .Range(0, undoStack.Count)
                    .Select(index =>
                        (undoStack.GetChangedCell(index), undoStack.GetPreviousValue(index))
                    )
            );
        }

        // 3人以上の場合、挟んだ間の石は誰の石でも裏返す
        [Test]
        public void PlaceStone_FlipsAllOpponents_WithThreePlayers()
        {
            var (cells, topology) = Prepare(ReversiTestHelper.CreateBoard(".120"));

            ReversiRules.PlaceStone(cells, topology, cell: 0, player: 0, new ReversiUndoStack());

            CollectionAssert.AreEqual(new[] { 0, 0, 0, 0 }, cells);
        }

        // 盤面に null を渡すと、どのメソッドも ArgumentNullException を投げる
        [Test]
        public void PublicMethods_Throw_WhenBoardIsNull()
        {
            Assert.Throws<ArgumentNullException>(() => ReversiRules.CanPlace(null, 0, 0, 0));
            Assert.Throws<ArgumentNullException>(() => ReversiRules.GetLegalMoves(null, 0));
            Assert.Throws<ArgumentNullException>(() => ReversiRules.HasAnyMove(null, 0));
            Assert.Throws<ArgumentNullException>(() =>
                ReversiRules.GetFlipPositions(null, 0, 0, 0)
            );
            Assert.Throws<ArgumentNullException>(() => ReversiRules.PlaceStone(null, 0, 0, 0));
            Assert.Throws<ArgumentNullException>(() =>
                ReversiRules.PlaceStone(null, 0, 0, 0, out _)
            );
            Assert.Throws<ArgumentNullException>(() => ReversiRules.CheckStart(null, 2));
            Assert.Throws<ArgumentNullException>(() => ReversiRules.FindNextTurn(null, 0, 2));
            Assert.Throws<ArgumentNullException>(() => ReversiRules.GetPlayerResults(null, 2));
        }

        // 打てるのは、隣の石の持ち主以外で、その先に自分の石があるプレイヤー
        [Test]
        public void GetPlaceablePlayers_ReturnsPlayersWhoCanPlace()
        {
            var (cells, topology) = Prepare(ReversiTestHelper.CreateBoard(".120"));
            const long AllPlayers = 0b111;

            long placeablePlayers = ReversiRules.GetPlaceablePlayers(
                cells,
                topology,
                cell: 0,
                AllPlayers
            );

            Assert.AreEqual(0b101, placeablePlayers);
            for (int player = 0; player < 3; player++)
            {
                Assert.AreEqual(
                    ReversiRules.CanPlace(cells, topology, cell: 0, player),
                    (placeablePlayers & (1L << player)) != 0
                );
            }
        }

        // 間に壁や空きマスがある場合は挟めない
        [TestCase(".1#0")]
        [TestCase(".1.0")]
        [TestCase(".11.")]
        public void CanPlace_ReturnsFalse_WhenNotSandwiched(string row)
        {
            var (cells, topology) = Prepare(ReversiTestHelper.CreateBoard(row));

            Assert.IsFalse(ReversiRules.CanPlace(cells, topology, cell: 0, player: 0));
        }

        // 石が置かれているマスには打てない
        [Test]
        public void CanPlace_ReturnsFalse_WhenCellIsOccupied()
        {
            var (cells, topology) = Prepare(ReversiTestHelper.CreateBoard("0110"));

            Assert.IsFalse(ReversiRules.CanPlace(cells, topology, cell: 0, player: 0));
        }

        // 斜め方向にも裏返せる
        [Test]
        public void PlaceStone_FlipsDiagonal()
        {
            var (cells, topology) = Prepare(ReversiTestHelper.CreateBoard("...", ".1.", "..0"));

            ReversiRules.PlaceStone(cells, topology, cell: 0, player: 0, new ReversiUndoStack());

            Assert.AreEqual(0, cells[1 * 3 + 1]);
        }

        // 相手の石を挟めるマスには置ける
        [Test]
        public void CanPlace_ReturnsTrue_WhenStoneCanBeFlipped()
        {
            Assert.IsTrue(ReversiRules.CanPlace(CreateSmallBoard(), x: 2, y: 0, player: 0));
        }

        // 挟めないマス・埋まっているマスには置けない
        [Test]
        public void CanPlace_ReturnsFalse_WhenNothingCanBeFlipped()
        {
            ReversiBoard board = CreateSmallBoard();
            Assert.IsFalse(ReversiRules.CanPlace(board, x: 0, y: 0, player: 0));
            Assert.IsFalse(ReversiRules.CanPlace(board, x: 1, y: 1, player: 0));
        }

        // 間に壁がある場合は挟めない
        [Test]
        public void CanPlace_ReturnsFalse_WhenWallIsBetween()
        {
            Assert.IsFalse(
                ReversiRules.CanPlace(ReversiTestHelper.CreateBoard(".1#0"), x: 0, y: 0, player: 0)
            );
        }

        // 盤外の座標は呼び出し側の誤りとして例外にする
        [Test]
        public void CanPlace_Throws_WhenOutOfBoard()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                ReversiRules.CanPlace(CreateSmallBoard(), x: 4, y: 0, player: 0)
            );
        }

        // 打てるマスを、左上から行ごとの順で返す
        [Test]
        public void GetLegalMoves_ReturnsMovesInRowOrder()
        {
            CollectionAssert.AreEqual(
                new List<(int x, int y)> { (4, 2), (5, 3), (2, 4), (3, 5) },
                ToTuples(
                    ReversiRules.GetLegalMoves(ReversiTestHelper.CreateStandardBoard(), player: 0)
                )
            );
        }

        // 裏返るマスの一覧を返す。盤面は変えない
        [Test]
        public void GetFlipPositions_ReturnsFlippedCells_AndKeepsBoard()
        {
            ReversiBoard board = ReversiTestHelper.CreateBoard(".110");

            CollectionAssert.AreEqual(
                new List<(int x, int y)> { (1, 0), (2, 0) },
                ToTuples(ReversiRules.GetFlipPositions(board, x: 0, y: 0, player: 0))
            );
            Assert.AreEqual(ReversiBoard.Empty, board.GetCell(0, 0));
            Assert.AreEqual(2, board.CountCells(1));
        }

        // 置けないマスでは、裏返るマスはない
        [Test]
        public void GetFlipPositions_ReturnsEmpty_WhenCannotPlace()
        {
            Assert.IsEmpty(
                ReversiRules.GetFlipPositions(CreateSmallBoard(), x: 0, y: 0, player: 0)
            );
        }

        // 置いたマスと挟んだ石が自分の色になる
        [Test]
        public void PlaceStone_FlipsSandwichedStones()
        {
            ReversiBoard board = CreateSmallBoard();
            Assert.IsTrue(ReversiRules.PlaceStone(board, x: 2, y: 0, player: 0));
            Assert.AreEqual(0, board.GetCell(2, 0));
            Assert.AreEqual(0, board.GetCell(2, 1));
            Assert.AreEqual(4, board.CountCells(0));
            Assert.AreEqual(1, board.CountCells(1));
        }

        // 複数方向を同時に裏返す
        [Test]
        public void PlaceStone_FlipsMultipleDirections()
        {
            ReversiBoard board = ReversiTestHelper.CreateBoard(".10.", "11..", "0...", "....");
            Assert.IsTrue(ReversiRules.PlaceStone(board, x: 0, y: 0, player: 0));
            Assert.AreEqual(0, board.GetCell(1, 0));
            Assert.AreEqual(0, board.GetCell(0, 1));
            // 斜めは挟めていないので裏返らない
            Assert.AreEqual(1, board.GetCell(1, 1));
        }

        // 置けないマスでは盤面が変わらない
        [Test]
        public void PlaceStone_ReturnsFalse_AndKeepsBoard_WhenCannotPlace()
        {
            ReversiBoard board = CreateSmallBoard();
            Assert.IsFalse(ReversiRules.PlaceStone(board, x: 0, y: 0, player: 0));
            Assert.AreEqual(ReversiBoard.Empty, board.GetCell(0, 0));
        }

        // 置くと同時に、裏返したマスを GetFlipPositions() と同じ順で返す
        [Test]
        public void PlaceStone_ReturnsFlipPositions_SameAsGetFlipPositions()
        {
            ReversiBoard board = ReversiTestHelper.CreateBoard(".10.", "11..", "0...", "....");
            List<(int x, int y)> expected = ToTuples(
                ReversiRules.GetFlipPositions(board, x: 0, y: 0, player: 0)
            );

            Assert.IsTrue(
                ReversiRules.PlaceStone(
                    board,
                    x: 0,
                    y: 0,
                    player: 0,
                    out IReadOnlyList<ReversiPosition> flipPositions
                )
            );
            CollectionAssert.AreEqual(expected, ToTuples(flipPositions));
            Assert.AreEqual(2, flipPositions.Count);
        }

        // 置けない場合は、裏返したマスは空
        [Test]
        public void PlaceStone_ReturnsEmptyFlipPositions_WhenCannotPlace()
        {
            ReversiBoard board = CreateSmallBoard();

            Assert.IsFalse(
                ReversiRules.PlaceStone(
                    board,
                    x: 0,
                    y: 0,
                    player: 0,
                    out IReadOnlyList<ReversiPosition> flipPositions
                )
            );
            Assert.IsEmpty(flipPositions);
        }

        [Test]
        public void HasAnyMove_ReturnsFalse_WhenNoMoveExists()
        {
            ReversiBoard board = ReversiTestHelper.CreateBoard("00..", "....", "....", "....");
            Assert.IsFalse(ReversiRules.HasAnyMove(board, player: 0));
            Assert.IsFalse(ReversiRules.HasAnyMove(board, player: 1));
            Assert.IsTrue(ReversiRules.HasAnyMove(CreateSmallBoard(), player: 1));
        }

        // 最後のプレイヤーの次は、最初のプレイヤーに戻る
        [TestCase(0, 1)]
        [TestCase(1, 2)]
        [TestCase(2, 0)]
        public void GetNextPlayer_WrapsAround(int player, int expected)
        {
            Assert.AreEqual(expected, ReversiRules.GetNextPlayer(player, playerCount: 3));
        }

        // 打てるプレイヤーがいれば、そのプレイヤーの手番になる
        [Test]
        public void FindNextTurn_ReturnsStartPlayer_WhenStartPlayerCanMove()
        {
            ReversiTurn turn = ReversiRules.FindNextTurn(
                ReversiTestHelper.CreateBoard("01."),
                startPlayer: 0,
                playerCount: 2
            );

            Assert.AreEqual(0, turn.Player);
            Assert.IsFalse(turn.IsGameOver);
            Assert.IsEmpty(turn.PassedPlayers);
        }

        // 打てないプレイヤーを飛ばし、最後のプレイヤーの次は最初のプレイヤーに戻る
        [Test]
        public void FindNextTurn_SkipsPassedPlayers_AndWrapsAround()
        {
            ReversiTurn turn = ReversiRules.FindNextTurn(
                ReversiTestHelper.CreateBoard("01."),
                startPlayer: 2,
                playerCount: 3
            );

            Assert.AreEqual(0, turn.Player);
            CollectionAssert.AreEqual(new[] { 2 }, turn.PassedPlayers);
        }

        // 全員打てなければ終局
        [Test]
        public void FindNextTurn_IsGameOver_WhenNobodyCanMove()
        {
            ReversiTurn turn = ReversiRules.FindNextTurn(
                ReversiTestHelper.CreateBoard("00."),
                startPlayer: 0,
                playerCount: 2
            );

            Assert.IsTrue(turn.IsGameOver);
            Assert.AreEqual(ReversiTurn.NoPlayer, turn.Player);
            CollectionAssert.AreEqual(new[] { 0, 1 }, turn.PassedPlayers);
        }

        [TestCase(2, 2)]
        [TestCase(-1, 2)]
        [TestCase(0, 1)]
        public void FindNextTurn_Throws_WhenPlayerIsInvalid(int startPlayer, int playerCount)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                ReversiRules.FindNextTurn(CreateSmallBoard(), startPlayer, playerCount)
            );
        }

        // 二手連続（0, 0, 1, 1, …）の順番
        private static ReversiTurnOrder CreateDoubleMoveOrder() =>
            new(playerCount: 2, turnNumber => turnNumber / 2 % 2);

        // turnOrder を渡すと、通し番号からその手番のプレイヤーを決める
        [Test]
        public void FindNextTurn_UsesTurnOrder()
        {
            ReversiTurn turn = ReversiRules.FindNextTurn(
                ReversiTestHelper.CreateBoard("01."),
                startTurnNumber: 1,
                CreateDoubleMoveOrder()
            );

            Assert.AreEqual(0, turn.Player);
            Assert.AreEqual(1, turn.TurnNumber);
            Assert.IsEmpty(turn.PassedPlayers);
        }

        // パスも1手番と数え、通し番号の順に次の手番を調べる
        [Test]
        public void FindNextTurn_SkipsPassedTurns_WithTurnOrder()
        {
            ReversiTurn turn = ReversiRules.FindNextTurn(
                ReversiTestHelper.CreateBoard("10."),
                startTurnNumber: 0,
                CreateDoubleMoveOrder()
            );

            Assert.AreEqual(1, turn.Player);
            Assert.AreEqual(2, turn.TurnNumber);
            CollectionAssert.AreEqual(new[] { 0, 0 }, turn.PassedPlayers);
        }

        // 順番に関わらず、全員が続けてパスしたら終局
        [Test]
        public void FindNextTurn_IsGameOver_WhenEveryPlayerPassed_WithTurnOrder()
        {
            ReversiTurn turn = ReversiRules.FindNextTurn(
                ReversiTestHelper.CreateBoard("00."),
                startTurnNumber: 0,
                CreateDoubleMoveOrder()
            );

            Assert.IsTrue(turn.IsGameOver);
            Assert.AreEqual(3, turn.TurnNumber);
            CollectionAssert.AreEqual(new[] { 0, 0, 1 }, turn.PassedPlayers);
        }

        // 打てるプレイヤーに手番が回ってこない順番の場合は例外を投げる
        [Test]
        public void FindNextTurn_Throws_WhenTurnOrderSkipsMovablePlayer()
        {
            // プレイヤー2だけが打てるが、手番は0と1にしか回ってこない
            var turnOrder = new ReversiTurnOrder(playerCount: 3, turnNumber => turnNumber % 2);

            Assert.Throws<InvalidOperationException>(() =>
                ReversiRules.FindNextTurn(ReversiTestHelper.CreateBoard("21."), 0, turnOrder)
            );
        }

        [Test]
        public void FindNextTurn_Throws_WhenTurnOrderArgumentsAreInvalid()
        {
            Assert.Throws<ArgumentNullException>(() =>
                ReversiRules.FindNextTurn(CreateSmallBoard(), 0, turnOrder: null)
            );
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                ReversiRules.FindNextTurn(CreateSmallBoard(), -1, CreateDoubleMoveOrder())
            );
        }

        // 石数が同じプレイヤーは同じ順位になり、次の順位は人数分飛ぶ
        [Test]
        public void GetPlayerResults_ReturnsStoneCountsAndRanks()
        {
            IReadOnlyList<ReversiPlayerResult> results = ReversiRules.GetPlayerResults(
                ReversiTestHelper.CreateBoard("0011.2"),
                playerCount: 3
            );

            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, results.Select(result => result.Player));
            CollectionAssert.AreEqual(
                new[] { 2, 2, 1 },
                results.Select(result => result.StoneCount)
            );
            CollectionAssert.AreEqual(new[] { 1, 1, 3 }, results.Select(result => result.Rank));
        }

        // 全員が打てる盤面なら始められる
        [Test]
        public void CheckStart_CanStart_WhenEveryPlayerCanMove()
        {
            ReversiStartCheck startCheck = ReversiRules.CheckStart(
                ReversiTestHelper.CreateStandardBoard(),
                playerCount: 2
            );
            Assert.IsTrue(startCheck.CanStart);
            Assert.AreEqual(ReversiStartError.None, startCheck.Error);
            CollectionAssert.IsEmpty(startCheck.PlayersWithoutMove);
        }

        [TestCase(1)]
        [TestCase(0)]
        public void CheckStart_ReturnsInvalidPlayerCount_WhenPlayerCountIsLessThanTwo(
            int playerCount
        )
        {
            ReversiStartCheck startCheck = ReversiRules.CheckStart(
                ReversiTestHelper.CreateStandardBoard(),
                playerCount
            );
            Assert.IsFalse(startCheck.CanStart);
            Assert.AreEqual(ReversiStartError.InvalidPlayerCount, startCheck.Error);
        }

        // 人数を超える番号の石がある場合は、打てるかより先に弾く
        [Test]
        public void CheckStart_ReturnsInvalidStone_WhenStoneIsOutOfPlayerCount()
        {
            ReversiStartCheck startCheck = ReversiRules.CheckStart(
                ReversiTestHelper.CreateBoard("....", ".012", ".10.", "...."),
                playerCount: 2
            );
            Assert.AreEqual(ReversiStartError.InvalidStone, startCheck.Error);
        }

        // 石が1つもないプレイヤーも、打てないプレイヤーとして返す
        [Test]
        public void CheckStart_ReturnsPlayersWithoutMove_WhenSomePlayersCannotMove()
        {
            ReversiStartCheck startCheck = ReversiRules.CheckStart(
                CreateSmallBoard(),
                playerCount: 4
            );
            Assert.AreEqual(ReversiStartError.PlayerCannotMove, startCheck.Error);
            CollectionAssert.AreEqual(new[] { 2, 3 }, startCheck.PlayersWithoutMove);
        }

        // 石はあっても、挟める位置がなければ始められない
        [Test]
        public void CheckStart_ReturnsPlayerCannotMove_WhenStonesCannotBeSandwiched()
        {
            ReversiStartCheck startCheck = ReversiRules.CheckStart(
                ReversiTestHelper.CreateBoard("01.."),
                playerCount: 2
            );
            Assert.AreEqual(ReversiStartError.PlayerCannotMove, startCheck.Error);
            CollectionAssert.AreEqual(new[] { 1 }, startCheck.PlayersWithoutMove);
        }
    }
}
