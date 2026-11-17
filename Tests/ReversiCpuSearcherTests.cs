using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Zizo.Reversi.Tests
{
    public class ReversiCpuSearcherTests
    {
        // 候補手はスコアの高い順に並び、スコアは0〜100になる
        [Test]
        public void Search_ReturnsMovesSortedByScore()
        {
            ReversiSearchResult result = ReversiCpuSearcher.Search(
                ReversiTestHelper.CreateStandardBoard(),
                player: 0,
                playerCount: 2
            );

            Assert.AreEqual(4, result.Moves.Count);
            for (int moveIndex = 0; moveIndex < result.Moves.Count; moveIndex++)
            {
                Assert.That(result.Moves[moveIndex].Score, Is.InRange(0f, 100f));
                if (moveIndex > 0)
                    Assert.GreaterOrEqual(
                        result.Moves[moveIndex - 1].Score,
                        result.Moves[moveIndex].Score
                    );
            }
        }

        // 候補手の数を指定できる
        [Test]
        public void Search_LimitsCandidateCount()
        {
            var settings = new ReversiCpuSettings { CandidateCount = 2 };

            ReversiSearchResult result = ReversiCpuSearcher.Search(
                ReversiTestHelper.CreateStandardBoard(),
                player: 0,
                playerCount: 2,
                settings
            );

            Assert.AreEqual(2, result.Moves.Count);
        }

        // 打てる手がない場合は、空のリストを返す（パス）
        [Test]
        public void Search_ReturnsEmpty_WhenNoMoves()
        {
            ReversiSearchResult result = ReversiCpuSearcher.Search(
                ReversiTestHelper.CreateBoard("0000", "0110"),
                player: 1,
                playerCount: 2
            );

            Assert.IsEmpty(result.Moves);
        }

        // 隅が取れる場合は、隅が最も高いスコアになる
        [Test]
        public void Search_PrefersCorner()
        {
            ReversiSearchResult result = ReversiCpuSearcher.Search(
                ReversiTestHelper.CreateBoard(
                    ".10.....",
                    "........",
                    "........",
                    "...01...",
                    "...10...",
                    "........",
                    "........",
                    "........"
                ),
                player: 0,
                playerCount: 2
            );

            Assert.AreEqual((0, 0), (result.Moves[0].X, result.Moves[0].Y));
        }

        // 勝ちが確定する手は、スコアが95以上になる
        [Test]
        public void Search_ReturnsHighScore_WhenWinIsCertain()
        {
            ReversiSearchResult result = ReversiCpuSearcher.Search(
                ReversiTestHelper.CreateBoard("..10"),
                player: 0,
                playerCount: 2
            );

            Assert.AreEqual(1, result.Moves.Count);
            Assert.GreaterOrEqual(result.Moves[0].Score, 95f);
        }

        // 負けが確定する手は、スコアが5以下になる
        [Test]
        public void Search_ReturnsLowScore_WhenLossIsCertain()
        {
            // 0が(1,0)に打つと、1が(4,0)に打って全て1の石になる
            ReversiSearchResult result = ReversiCpuSearcher.Search(
                ReversiTestHelper.CreateBoard("1.10."),
                player: 0,
                playerCount: 2
            );

            Assert.AreEqual(1, result.Moves.Count);
            Assert.LessOrEqual(result.Moves[0].Score, 5f);
        }

        // 3人戦でも、全ての合法手を候補手として返す
        [Test]
        public void Search_ReturnsAllLegalMoves_WithThreePlayers()
        {
            ReversiBoard board = ReversiTestHelper.CreateBoard(
                "#......#",
                "........",
                "...012..",
                "..2.01..",
                "..120...",
                "........",
                "#......#"
            );
            var settings = new ReversiCpuSettings { CandidateCount = 0 };

            ReversiSearchResult result = ReversiCpuSearcher.Search(
                board,
                player: 1,
                playerCount: 3,
                settings
            );

            CollectionAssert.AreEquivalent(
                ReversiRules.GetLegalMoves(board, player: 1),
                result.Moves.Select(move => move.Position)
            );
        }

        // 局面数の上限に達したら、読み切れた深さの結果を返す（1手先は必ず読む）
        [Test]
        public void Search_StopsAtNodeLimit()
        {
            var settings = new ReversiCpuSettings { MaxSearchDepth = 6, MaxNodeCount = 1 };

            ReversiSearchResult result = ReversiCpuSearcher.Search(
                ReversiTestHelper.CreateStandardBoard(),
                player: 0,
                playerCount: 2,
                settings
            );

            Assert.AreEqual(1, result.CompletedDepth);
            Assert.AreEqual(4, result.Moves.Count);
        }

        // 上限に達しなければ、指定した深さまで読む
        [Test]
        public void Search_ReachesMaxDepth()
        {
            var settings = new ReversiCpuSettings { MaxSearchDepth = 3, MaxNodeCount = 0 };

            ReversiSearchResult result = ReversiCpuSearcher.Search(
                ReversiTestHelper.CreateStandardBoard(),
                player: 0,
                playerCount: 2,
                settings
            );

            Assert.AreEqual(3, result.CompletedDepth);
        }

        // 同じ盤面からは、毎回同じ結果を返す
        [Test]
        public void Search_IsDeterministic()
        {
            ReversiBoard board = ReversiTestHelper.CreateStandardBoard();

            List<ReversiMove> first = ReversiCpuSearcher
                .Search(board, player: 1, playerCount: 2)
                .Moves.ToList();
            List<ReversiMove> second = ReversiCpuSearcher
                .Search(board, player: 1, playerCount: 2)
                .Moves.ToList();

            CollectionAssert.AreEqual(first, second);
        }

        // Step() で少しずつ探索しても、Search() と同じ結果になる
        [Test]
        public void Step_ReturnsSameResultAsSearch()
        {
            ReversiBoard board = ReversiTestHelper.CreateStandardBoard();
            var settings = new ReversiCpuSettings
            {
                CandidateCount = 0,
                MaxSearchDepth = 6,
                MaxNodeCount = 0,
            };

            ReversiSearchResult expected = ReversiCpuSearcher.Search(
                board,
                player: 0,
                playerCount: 2,
                settings
            );
            ReversiSearch search = ReversiCpuSearcher.StartSearch(
                board,
                player: 0,
                playerCount: 2,
                settings
            );
            int stepCount = 1;
            while (!search.Step(budgetMilliseconds: 0))
            {
                Assert.IsNull(search.Result);
                stepCount++;
            }

            // 途中で止まらずに一度で終わると、少しずつ進められているかを確認できない
            Assert.Greater(stepCount, 1);
            Assert.AreEqual(expected.CompletedDepth, search.Result.CompletedDepth);
            Assert.AreEqual(expected.NodeCount, search.Result.NodeCount);
            CollectionAssert.AreEqual(expected.Moves, search.Result.Moves);
        }

        // 探索が終わった後に Step() を呼んでも、結果は変わらない
        [Test]
        public void Step_ReturnsTrue_AfterCompleted()
        {
            ReversiSearch search = ReversiCpuSearcher.StartSearch(
                ReversiTestHelper.CreateStandardBoard(),
                player: 0,
                playerCount: 2
            );
            while (!search.Step(budgetMilliseconds: 0)) { }
            ReversiSearchResult result = search.Result;

            Assert.IsTrue(search.Step(budgetMilliseconds: 0));
            Assert.IsTrue(search.IsCompleted);
            Assert.AreSame(result, search.Result);
        }

        // RequestStop() を呼ぶと、1手先だけ読んで終わる
        [Test]
        public void RequestStop_StopsAfterFirstDepth()
        {
            var settings = new ReversiCpuSettings { MaxSearchDepth = 6, MaxNodeCount = 0 };
            ReversiSearch search = ReversiCpuSearcher.StartSearch(
                ReversiTestHelper.CreateStandardBoard(),
                player: 0,
                playerCount: 2,
                settings
            );

            search.RequestStop();
            while (!search.Step(budgetMilliseconds: 0)) { }

            Assert.AreEqual(1, search.Result.CompletedDepth);
            Assert.AreEqual(4, search.Result.Moves.Count);
        }

        // 打てる手がない場合は、Step() を呼ぶ前から探索が終わっている
        [Test]
        public void StartSearch_IsCompleted_WhenNoMoves()
        {
            ReversiSearch search = ReversiCpuSearcher.StartSearch(
                ReversiTestHelper.CreateBoard("0000", "0110"),
                player: 1,
                playerCount: 2
            );

            Assert.IsTrue(search.IsCompleted);
            Assert.IsEmpty(search.Result.Moves);
        }

        // 二手連続（0, 0, 1, 1, …）の順番
        private static ReversiTurnOrder CreateDoubleMoveOrder() =>
            new(playerCount: 2, turnNumber => turnNumber / 2 % 2);

        // turnOrder を渡すと、その順番で先読みする
        // この盤面では、通常の順番なら(3,0)、二手連続なら(3,1)だけが勝てる手になる
        [Test]
        public void Search_ReadsAhead_WithTurnOrder()
        {
            ReversiBoard board = ReversiTestHelper.CreateBoard(".01.", ".01.", ".011");

            ReversiSearchResult rotationResult = ReversiCpuSearcher.Search(
                board,
                turnNumber: 0,
                ReversiTurnOrder.Rotation(playerCount: 2)
            );
            ReversiSearchResult doubleMoveResult = ReversiCpuSearcher.Search(
                board,
                turnNumber: 0,
                CreateDoubleMoveOrder()
            );

            Assert.AreEqual(new ReversiPosition(3, 0), rotationResult.Moves[0].Position);
            Assert.AreEqual(new ReversiPosition(3, 1), doubleMoveResult.Moves[0].Position);
        }

        // 通し番号の手番のプレイヤーとして探索する
        [Test]
        public void Search_SearchesForPlayerOfTurnNumber()
        {
            // プレイヤー1だけが打てる
            ReversiBoard board = ReversiTestHelper.CreateBoard("10.");

            Assert.IsEmpty(
                ReversiCpuSearcher.Search(board, turnNumber: 1, CreateDoubleMoveOrder()).Moves
            );
            Assert.AreEqual(
                1,
                ReversiCpuSearcher.Search(board, turnNumber: 2, CreateDoubleMoveOrder()).Moves.Count
            );
        }

        [Test]
        public void Search_ThrowsException_WhenTurnOrderArgumentsAreInvalid()
        {
            ReversiBoard board = ReversiTestHelper.CreateStandardBoard();

            Assert.Throws<ArgumentNullException>(() =>
                ReversiCpuSearcher.Search(board, 0, turnOrder: null)
            );
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                ReversiCpuSearcher.Search(board, -1, CreateDoubleMoveOrder())
            );
        }

        // 不正な引数の場合は例外を投げる
        [Test]
        public void Search_ThrowsException_WhenArgumentsAreInvalid()
        {
            ReversiBoard board = ReversiTestHelper.CreateStandardBoard();

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                ReversiCpuSearcher.Search(board, 0, playerCount: 1)
            );
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                ReversiCpuSearcher.Search(board, 2, playerCount: 2)
            );
            Assert.Throws<ArgumentException>(() =>
                ReversiCpuSearcher.Search(ReversiTestHelper.CreateBoard(".102"), 0, playerCount: 2)
            );
        }
    }
}
