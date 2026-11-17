using System;

namespace Zizo.Reversi
{
    /*
    リバーシのCPU。盤面を渡すと、候補手をスコア付きで返す。
    実際に石を置く処理や手番の判定は ReversiRules を使う。

    // 使い方（一度に最後まで探索する）
    var settings = new ReversiCpuSettings { CandidateCount = 5, MaxSearchDepth = 4 };
    ReversiSearchResult result = ReversiCpuSearcher.Search(board, player: 0, playerCount: 2, settings);
    if (result.Moves.Count == 0)
    {
        // パス
    }
    ReversiMove bestMove = result.Moves[0]; // スコアの高い順に並んでいる

    // 使い方（フレームをまたいで少しずつ探索する）
    // 探索中も画面を止めないためのもの。スレッドを使わないため、WebGLでも動く
    ReversiSearch search = ReversiCpuSearcher.StartSearch(board, player: 0, playerCount: 2, settings);
    while (!search.Step(budgetMilliseconds: 5)) // 1回の呼び出しで、およそ5ミリ秒だけ探索を進める
    {
        await UniTask.Yield(); // 次のフレームへ
    }
    ReversiSearchResult result = search.Result; // Search() と同じ結果になる

    // 使い方（手番の順番を変える。二手連続など）
    // ReversiTurnOrder を渡すと、その順番で先読みする。turnNumber は今の手番の通し番号（ReversiTurn.TurnNumber）
    var turnOrder = new ReversiTurnOrder(playerCount: 2, turnNumber => turnNumber / 2 % 2);
    ReversiSearchResult result = ReversiCpuSearcher.Search(board, turn.TurnNumber, turnOrder, settings);

    // 手番について
    ・playerCount を渡した場合、手番は player → player + 1 → … → playerCount - 1 → 0 の順に回る前提で読む
    ・ReversiTurnOrder を渡した場合、その順番で回る前提で読む
    ・打てる手がないプレイヤーはパスとし、全員が続けてパスしたら終局とする

    // 探索について
    ・自分以外の全員が、自分にとって最も不利な手を打つと仮定して読む（2人の場合は通常のαβ探索と同じ）
    ・1手先から順に深く読み、上限に達したら、最後まで読み切れた深さの結果を返す
    */
    public static class ReversiCpuSearcher
    {
        public static ReversiSearchResult Search(
            ReversiBoard board,
            int player,
            int playerCount,
            ReversiCpuSettings settings = null
        )
        {
            ReversiSearch search = StartSearch(board, player, playerCount, settings);
            search.RunToCompletion();
            return search.Result;
        }

        // turnOrder の通し番号 turnNumber の手番のプレイヤーとして探索する
        public static ReversiSearchResult Search(
            ReversiBoard board,
            int turnNumber,
            ReversiTurnOrder turnOrder,
            ReversiCpuSettings settings = null
        )
        {
            ReversiSearch search = StartSearch(board, turnNumber, turnOrder, settings);
            search.RunToCompletion();
            return search.Result;
        }

        // 探索を始める。Step() を呼ぶたびに少しずつ進む
        // 盤面はこの時点でコピーするため、探索中に board を変更しても結果には影響しない
        public static ReversiSearch StartSearch(
            ReversiBoard board,
            int player,
            int playerCount,
            ReversiCpuSettings settings = null
        )
        {
            ReversiRules.ValidateBoard(board);
            ReversiRules.ValidatePlayer(player, playerCount);
            return StartSearch(board, player, ReversiTurnOrder.Rotation(playerCount), settings);
        }

        // turnOrder の通し番号 turnNumber の手番のプレイヤーとして、探索を始める
        public static ReversiSearch StartSearch(
            ReversiBoard board,
            int turnNumber,
            ReversiTurnOrder turnOrder,
            ReversiCpuSettings settings = null
        )
        {
            ReversiRules.ValidateBoard(board);
            ReversiRules.ValidateTurnOrder(turnOrder);
            ReversiTurnOrder.ValidateTurnNumber(turnNumber);

            if (ReversiRules.HasInvalidStone(board.RawCells, turnOrder.PlayerCount))
                throw new ArgumentException(
                    "board has a player index greater than or equal to playerCount",
                    nameof(board)
                );

            return new ReversiSearch(board, turnNumber, turnOrder, settings ?? new());
        }
    }
}
