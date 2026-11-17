using System;
using System.Collections.Generic;
using System.Linq;

namespace Zizo.Reversi
{
    /*
    リバーシの基本ルール。ReversiBoard だけで動くため、CPUを使わないゲームでも単体で使える。
    自分の石で挟んだ間の石は、誰の石でも全て裏返す（3人以上でも同じ）。
    CPUの探索も同じルール（このクラス）で先読みする。

    盤面の形や初期配置はゲーム側で決める。このクラスは、渡された盤面と人数で始められるかを判定する。

    // 使い方
    ReversiBoard board = ...; // ゲーム側で、壁と初期配置の石を置いた盤面を作る
    ReversiStartCheck startCheck = ReversiRules.CheckStart(board, playerCount: 2);
    if (!startCheck.CanStart)
        return; // startCheck.Error で理由がわかる

    // 手番を進める。startPlayer から手番の順に調べ、最初に打てるプレイヤーを返す
    ReversiTurn turn = ReversiRules.FindNextTurn(board, startPlayer: 0, playerCount: 2);
    while (!turn.IsGameOver)
    {
        // ReversiPosition move = ...; // turn.Player の手を決める
        ReversiRules.PlaceStone(board, move.X, move.Y, turn.Player);

        int nextPlayer = ReversiRules.GetNextPlayer(turn.Player, playerCount: 2);
        turn = ReversiRules.FindNextTurn(board, nextPlayer, playerCount: 2);
    }

    // 手番の順番を変える場合（二手連続など）は、ReversiTurnOrder を渡し、手番の通し番号で進める
    var turnOrder = new ReversiTurnOrder(playerCount: 2, turnNumber => turnNumber / 2 % 2);
    ReversiTurn turn = ReversiRules.FindNextTurn(board, startTurnNumber: 0, turnOrder);
    while (!turn.IsGameOver)
    {
        // ReversiPosition move = ...; // turn.Player の手を決める
        ReversiRules.PlaceStone(board, move.X, move.Y, turn.Player);
        turn = ReversiRules.FindNextTurn(board, turn.TurnNumber + 1, turnOrder);
    }

    // 結果を集計する（[プレイヤー番号] → 石数と順位）
    IReadOnlyList<ReversiPlayerResult> results = ReversiRules.GetPlayerResults(board, playerCount: 2);
    */
    public static class ReversiRules
    {
        private const int MinPlayerCount = 2;

        public static bool CanPlace(ReversiBoard board, int x, int y, int player)
        {
            ValidateBoard(board);
            int[] cells = board.RawCells;
            ReversiBoardTopology topology = board.Topology;
            return CanPlace(cells, topology, board.ToCellIndex(x, y), player);
        }

        // 打てるマスの一覧（左上から行ごとの順）。打てない場合は空
        public static IReadOnlyList<ReversiPosition> GetLegalMoves(ReversiBoard board, int player)
        {
            ValidateBoard(board);
            int[] cells = board.RawCells;
            ReversiBoardTopology topology = board.Topology;
            var moves = new List<ReversiPosition>();
            for (int cell = 0; cell < cells.Length; cell++)
            {
                if (CanPlace(cells, topology, cell, player))
                    moves.Add(ToPosition(board, cell));
            }
            return moves;
        }

        public static bool HasAnyMove(ReversiBoard board, int player)
        {
            ValidateBoard(board);
            return HasAnyMove(board.RawCells, board.Topology, player);
        }

        // 置いた時に裏返るマスの一覧（置く前に調べる）。置けない場合は空
        // 置くのと同時に裏返したマスを知りたい場合は、PlaceStone() の flipPositions を使う
        public static IReadOnlyList<ReversiPosition> GetFlipPositions(
            ReversiBoard board,
            int x,
            int y,
            int player
        )
        {
            ValidateBoard(board);
            int[] cells = board.RawCells;
            ReversiBoardTopology topology = board.Topology;
            int cell = board.ToCellIndex(x, y);
            var flipPositions = new List<ReversiPosition>();
            if (cells[cell] != ReversiBoard.Empty)
                return flipPositions;

            for (int direction = 0; direction < ReversiBoardTopology.DirectionCount; direction++)
            {
                int flipCount = CountFlipsInDirection(cells, topology, cell, direction, player);
                int current = cell;
                for (int flipped = 0; flipped < flipCount; flipped++)
                {
                    current = topology.GetNeighbor(current, direction);
                    flipPositions.Add(ToPosition(board, current));
                }
            }
            return flipPositions;
        }

        // 石を置いて裏返す。置けない場合は何もせずfalseを返す
        public static bool PlaceStone(ReversiBoard board, int x, int y, int player) =>
            PlaceStone(board, x, y, player, out _);

        // 石を置いて裏返し、裏返したマスの一覧（GetFlipPositions() と同じ順）を flipPositions に返す
        // 置けない場合は何もせずfalseを返し、flipPositions は空になる
        public static bool PlaceStone(
            ReversiBoard board,
            int x,
            int y,
            int player,
            out IReadOnlyList<ReversiPosition> flipPositions
        )
        {
            ValidateBoard(board);
            int[] cells = board.RawCells;
            ReversiBoardTopology topology = board.Topology;
            int cell = board.ToCellIndex(x, y);
            if (!CanPlace(cells, topology, cell, player))
            {
                flipPositions = Array.Empty<ReversiPosition>();
                return false;
            }

            var undoStack = new ReversiUndoStack();
            PlaceStone(cells, topology, cell, player, undoStack);
            flipPositions = ToFlipPositions(board, undoStack);
            return true;
        }

        // 対局を始められるか。始められない場合は理由を返す
        // 人数 → 石の番号 → 全員が最初から打てるか、の順に調べ、最初に見つかった理由を返す
        // 対局を始める前にこれで確認しておけば、FindNextTurn() や CPU の探索が例外を投げることはない
        public static ReversiStartCheck CheckStart(ReversiBoard board, int playerCount)
        {
            ValidateBoard(board);
            if (playerCount < MinPlayerCount)
                return new ReversiStartCheck(ReversiStartError.InvalidPlayerCount);

            int[] cells = board.RawCells;
            if (HasInvalidStone(cells, playerCount))
                return new ReversiStartCheck(ReversiStartError.InvalidStone);

            ReversiBoardTopology topology = board.Topology;
            int[] playersWithoutMove = Enumerable
                .Range(0, playerCount)
                .Where(player => !HasAnyMove(cells, topology, player))
                .ToArray();
            return playersWithoutMove.Length > 0
                ? new ReversiStartCheck(ReversiStartError.PlayerCannotMove, playersWithoutMove)
                : new ReversiStartCheck(ReversiStartError.None);
        }

        // 手番は player → player + 1 → … → playerCount - 1 → 0 の順に回る
        public static int GetNextPlayer(int player, int playerCount) => (player + 1) % playerCount;

        // startPlayer から手番の順に調べ、最初に打てるプレイヤーを返す（startPlayer 自身も調べる）
        // 打てる手がないプレイヤーはパスとし、全員が続けてパスしたら終局とする
        public static ReversiTurn FindNextTurn(ReversiBoard board, int startPlayer, int playerCount)
        {
            ValidateBoard(board);
            ValidatePlayer(startPlayer, playerCount);
            return FindNextTurn(board, startPlayer, ReversiTurnOrder.Rotation(playerCount));
        }

        // 通し番号 startTurnNumber の手番から turnOrder の順に調べ、最初に打てる手番を返す（startTurnNumber 自身も調べる）
        // 打てる手がないプレイヤーはパスとし、全員が続けてパスしたら終局とする
        public static ReversiTurn FindNextTurn(
            ReversiBoard board,
            int startTurnNumber,
            ReversiTurnOrder turnOrder
        )
        {
            ValidateBoard(board);
            ValidateTurnOrder(turnOrder);
            ReversiTurnOrder.ValidateTurnNumber(startTurnNumber);

            int[] cells = board.RawCells;
            ReversiBoardTopology topology = board.Topology;
            var passedPlayers = new List<int>();
            var passTracker = new ReversiPassTracker(turnOrder.PlayerCount);
            for (int turnNumber = startTurnNumber; ; turnNumber++)
            {
                int player = turnOrder.GetPlayer(turnNumber);
                if (HasAnyMove(cells, topology, player))
                    return new ReversiTurn(player, turnNumber, passedPlayers);

                passedPlayers.Add(player);
                if (passTracker.AddPass(player))
                    return new ReversiTurn(ReversiTurn.NoPlayer, turnNumber + 1, passedPlayers);
            }
        }

        // [プレイヤー番号] → 石数と順位。勝敗の扱いはゲームごとに決めること
        public static IReadOnlyList<ReversiPlayerResult> GetPlayerResults(
            ReversiBoard board,
            int playerCount
        )
        {
            ValidateBoard(board);
            int[] stoneCounts = Enumerable
                .Range(0, playerCount)
                .Select(player => board.CountCells(player))
                .ToArray();

            return Enumerable
                .Range(0, playerCount)
                .Select(player => new ReversiPlayerResult(
                    player,
                    stoneCounts[player],
                    stoneCounts.Count(stoneCount => stoneCount > stoneCounts[player]) + 1
                ))
                .ToArray();
        }

        // 盤面が null の場合は例外を投げる（呼び出し方の誤り）
        internal static void ValidateBoard(ReversiBoard board)
        {
            if (board == null)
                throw new ArgumentNullException(nameof(board));
        }

        // playerCount 以上の番号の石があるか
        internal static bool HasInvalidStone(int[] cells, int playerCount) =>
            cells.Any(cell => cell >= playerCount);

        internal static void ValidateTurnOrder(ReversiTurnOrder turnOrder)
        {
            if (turnOrder == null)
                throw new ArgumentNullException(nameof(turnOrder));
        }

        internal static void ValidatePlayer(int player, int playerCount)
        {
            if (playerCount < MinPlayerCount)
                throw new ArgumentOutOfRangeException(
                    nameof(playerCount),
                    "playerCount must be 2 or more"
                );
            if (player < 0 || player >= playerCount)
                throw new ArgumentOutOfRangeException(
                    nameof(player),
                    "player must be 0 to playerCount - 1"
                );
        }

        internal static bool CanPlace(
            int[] cells,
            ReversiBoardTopology topology,
            int cell,
            int player
        )
        {
            if (cells[cell] != ReversiBoard.Empty)
                return false;

            for (int direction = 0; direction < ReversiBoardTopology.DirectionCount; direction++)
            {
                if (CountFlipsInDirection(cells, topology, cell, direction, player) > 0)
                    return true;
            }
            return false;
        }

        // そのマスに打てるプレイヤーを、ビットの集合で返す（ビット p が立っていれば、プレイヤー p が打てる）
        // 各方向を1回たどるだけで全員分を調べられるため、プレイヤーごとに CanPlace() を呼ぶより速い
        // ある方向で打てるのは、隣の石の持ち主以外で、その先の続いた石の中に自分の石があるプレイヤー
        internal static long GetPlaceablePlayers(
            int[] cells,
            ReversiBoardTopology topology,
            int cell,
            long allPlayers
        )
        {
            if (cells[cell] != ReversiBoard.Empty)
                return 0;

            long placeablePlayers = 0;
            for (int direction = 0; direction < ReversiBoardTopology.DirectionCount; direction++)
            {
                int current = topology.GetNeighbor(cell, direction);
                if (current == ReversiBoardTopology.NoNeighbor || cells[current] < 0)
                    continue;

                int adjacentOwner = cells[current];
                long ownersBeyond = 0;
                current = topology.GetNeighbor(current, direction);
                while (current != ReversiBoardTopology.NoNeighbor && cells[current] >= 0)
                {
                    ownersBeyond |= 1L << cells[current];
                    current = topology.GetNeighbor(current, direction);
                }

                placeablePlayers |= ownersBeyond & ~(1L << adjacentOwner);
                if (placeablePlayers == allPlayers)
                    break;
            }
            return placeablePlayers;
        }

        // 石を置いて裏返す。変更前の値は undoStack に積む
        internal static void PlaceStone(
            int[] cells,
            ReversiBoardTopology topology,
            int cell,
            int player,
            ReversiUndoStack undoStack
        )
        {
            for (int direction = 0; direction < ReversiBoardTopology.DirectionCount; direction++)
            {
                int flipCount = CountFlipsInDirection(cells, topology, cell, direction, player);
                int current = cell;
                for (int flipped = 0; flipped < flipCount; flipped++)
                {
                    current = topology.GetNeighbor(current, direction);
                    undoStack.Push(current, cells[current]);
                    cells[current] = player;
                }
            }

            undoStack.Push(cell, cells[cell]);
            cells[cell] = player;
        }

        private static bool HasAnyMove(int[] cells, ReversiBoardTopology topology, int player)
        {
            for (int cell = 0; cell < cells.Length; cell++)
            {
                if (CanPlace(cells, topology, cell, player))
                    return true;
            }
            return false;
        }

        // 指定方向に裏返せる石の数。自分の石で挟めない場合は0
        private static int CountFlipsInDirection(
            int[] cells,
            ReversiBoardTopology topology,
            int cell,
            int direction,
            int player
        )
        {
            int count = 0;
            int current = topology.GetNeighbor(cell, direction);
            while (current != ReversiBoardTopology.NoNeighbor && cells[current] >= 0)
            {
                if (cells[current] == player)
                    return count;

                count++;
                current = topology.GetNeighbor(current, direction);
            }
            return 0;
        }

        // PlaceStone() で積んだ変更から、裏返したマスを取り出す（最後に積んだのは置いたマスなので除く）
        private static IReadOnlyList<ReversiPosition> ToFlipPositions(
            ReversiBoard board,
            ReversiUndoStack undoStack
        )
        {
            int flipCount = undoStack.Count - 1;
            var flipPositions = new ReversiPosition[flipCount];
            for (int index = 0; index < flipCount; index++)
            {
                flipPositions[index] = ToPosition(board, undoStack.GetChangedCell(index));
            }
            return flipPositions;
        }

        private static ReversiPosition ToPosition(ReversiBoard board, int cell) =>
            new(cell % board.Width, cell / board.Width);
    }
}
