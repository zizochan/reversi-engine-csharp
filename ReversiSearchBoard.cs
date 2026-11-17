using System;

namespace Zizo.Reversi
{
    // 探索中の盤面
    // 評価や合法手の判定のたびに盤面全体を調べ直さないよう、次の情報を石を置く・戻すたびに差分で更新する
    // ・プレイヤーごとの石数と、マスの重みの合計
    // ・石に隣接した空きマス（打てるマスは必ずこの中にあるため、合法手はここだけ調べればよい）
    internal class ReversiSearchBoard
    {
        private const int NotInFrontier = -1;

        // 全員の打てる手をまとめて数えられる人数の上限（プレイヤーを long のビットで表すため）
        private const int MaxBitPlayerCount = 64;

        // 石に隣接した空きマスが、空きマスのこの割合（％）以上を占める場合は、絞り込む効果が薄いため、全てのマスを順番に調べる
        private const int ScanAllCellsFrontierPercent = 50;

        private readonly ReversiBoardGeometry _geometry;
        private readonly ReversiBoardTopology _topology;
        private readonly ReversiUndoStack _undoStack = new();

        // [マス] → 隣（8方向）にある石の数
        private readonly int[] _adjacentStoneCounts;

        // 石に隣接した空きマスの集合。追加・削除をO(1)で行うため、順番は保たない
        private readonly int[] _frontierCells;
        private readonly int[] _frontierIndices;
        private int _frontierCount;

        public int[] Cells { get; }
        public int EmptyCount { get; private set; }

        // [プレイヤー] → 石数、マスの重みの合計（隅の隣のマスの補正は含まない）
        public int[] StoneCounts { get; }
        public int[] PositionScores { get; }

        // 今の盤面に戻すための位置。UndoTo() に渡す
        public int Checkpoint => _undoStack.Count;

        public ReversiSearchBoard(int[] cells, ReversiBoardGeometry geometry, int playerCount)
        {
            Cells = cells;
            _geometry = geometry;
            _topology = geometry.Topology;
            StoneCounts = new int[playerCount];
            PositionScores = new int[playerCount];
            _adjacentStoneCounts = new int[cells.Length];
            _frontierCells = new int[cells.Length];
            _frontierIndices = new int[cells.Length];
            Array.Fill(_frontierIndices, NotInFrontier);

            foreach (int cell in geometry.PlayableCells)
            {
                int owner = cells[cell];
                if (owner == ReversiBoard.Empty)
                {
                    EmptyCount++;
                    continue;
                }

                AddStone(cell, owner);
                for (
                    int direction = 0;
                    direction < ReversiBoardTopology.DirectionCount;
                    direction++
                )
                {
                    int neighbor = _topology.GetNeighbor(cell, direction);
                    if (neighbor != ReversiBoardTopology.NoNeighbor)
                        _adjacentStoneCounts[neighbor]++;
                }
            }

            foreach (int cell in geometry.PlayableCells)
            {
                if (cells[cell] == ReversiBoard.Empty && _adjacentStoneCounts[cell] > 0)
                    AddToFrontier(cell);
            }
        }

        // 石を置いて裏返す。置けるマスかどうかは確認しない
        public void PlaceStone(int cell, int player)
        {
            int checkpoint = _undoStack.Count;
            ReversiRules.PlaceStone(Cells, _topology, cell, player, _undoStack);

            // 裏返した石の持ち主を付け替える（最後に積まれているのは、置いたマス自身）
            for (int index = checkpoint; index < _undoStack.Count - 1; index++)
            {
                int flippedCell = _undoStack.GetChangedCell(index);
                RemoveStone(flippedCell, _undoStack.GetPreviousValue(index));
                AddStone(flippedCell, player);
            }

            AddStone(cell, player);
            EmptyCount--;
            RemoveFromFrontier(cell);
            for (int direction = 0; direction < ReversiBoardTopology.DirectionCount; direction++)
            {
                int neighbor = _topology.GetNeighbor(cell, direction);
                if (neighbor == ReversiBoardTopology.NoNeighbor)
                    continue;

                _adjacentStoneCounts[neighbor]++;
                if (Cells[neighbor] == ReversiBoard.Empty && _adjacentStoneCounts[neighbor] == 1)
                    AddToFrontier(neighbor);
            }
        }

        // checkpoint の時点まで盤面を戻す
        public void UndoTo(int checkpoint)
        {
            while (_undoStack.Count > checkpoint)
            {
                int index = _undoStack.Count - 1;
                int cell = _undoStack.GetChangedCell(index);
                int previousValue = _undoStack.GetPreviousValue(index);
                _undoStack.Pop();

                RemoveStone(cell, Cells[cell]);
                Cells[cell] = previousValue;
                if (previousValue != ReversiBoard.Empty)
                {
                    AddStone(cell, previousValue);
                    continue;
                }

                // 置いた石を取り除いたので、空きマスに戻す
                EmptyCount++;
                for (
                    int direction = 0;
                    direction < ReversiBoardTopology.DirectionCount;
                    direction++
                )
                {
                    int neighbor = _topology.GetNeighbor(cell, direction);
                    if (neighbor == ReversiBoardTopology.NoNeighbor)
                        continue;

                    _adjacentStoneCounts[neighbor]--;
                    if (
                        Cells[neighbor] == ReversiBoard.Empty
                        && _adjacentStoneCounts[neighbor] == 0
                    )
                        RemoveFromFrontier(neighbor);
                }
                if (_adjacentStoneCounts[cell] > 0)
                    AddToFrontier(cell);
            }
        }

        // 打てるマスを、手を調べる順番（ReversiBoardGeometry.PlayableCells の順）で moveBuffer に入れ、その数を返す
        public int CollectMoves(int player, int[] moveBuffer)
        {
            if (ShouldScanAllCells())
                return CollectMovesFromAllCells(player, moveBuffer);

            // 順番を揃えるため、マスの代わりに PlayableCells での順番を集めて並べ替える
            int moveCount = 0;
            for (int frontierIndex = 0; frontierIndex < _frontierCount; frontierIndex++)
            {
                int cell = _frontierCells[frontierIndex];
                if (ReversiRules.CanPlace(Cells, _topology, cell, player))
                    moveBuffer[moveCount++] = _geometry.MoveOrderRanks[cell];
            }

            Array.Sort(moveBuffer, 0, moveCount);
            for (int moveIndex = 0; moveIndex < moveCount; moveIndex++)
            {
                moveBuffer[moveIndex] = _geometry.PlayableCells[moveBuffer[moveIndex]];
            }
            return moveCount;
        }

        // [プレイヤー] → 打てるマスの数を moveCounts に入れる
        public void CountMovesOfAllPlayers(int[] moveCounts)
        {
            int playerCount = StoneCounts.Length;
            if (playerCount > MaxBitPlayerCount)
            {
                for (int player = 0; player < playerCount; player++)
                {
                    moveCounts[player] = CountMoves(player);
                }
                return;
            }

            Array.Clear(moveCounts, 0, playerCount);
            long allPlayers = playerCount == MaxBitPlayerCount ? -1L : (1L << playerCount) - 1;
            foreach (int cell in GetCellsToScan())
            {
                long placeablePlayers = ReversiRules.GetPlaceablePlayers(
                    Cells,
                    _topology,
                    cell,
                    allPlayers
                );
                for (int player = 0; placeablePlayers != 0; player++, placeablePlayers >>= 1)
                {
                    if ((placeablePlayers & 1) != 0)
                        moveCounts[player]++;
                }
            }
        }

        // 全てのマスを、手を調べる順番（PlayableCells の順）に調べる
        private int CollectMovesFromAllCells(int player, int[] moveBuffer)
        {
            int moveCount = 0;
            foreach (int cell in _geometry.PlayableCells)
            {
                if (ReversiRules.CanPlace(Cells, _topology, cell, player))
                    moveBuffer[moveCount++] = cell;
            }
            return moveCount;
        }

        private int CountMoves(int player)
        {
            int moveCount = 0;
            foreach (int cell in GetCellsToScan())
            {
                if (ReversiRules.CanPlace(Cells, _topology, cell, player))
                    moveCount++;
            }
            return moveCount;
        }

        // 打てるかを調べるマス。打てるマスは必ず石に隣接した空きマスの中にあるが、それが空きマスの多くを占める場合は、
        // 集合をたどるより全てのマスを順番に調べる方が速い
        private ReadOnlySpan<int> GetCellsToScan() =>
            ShouldScanAllCells()
                ? _geometry.PlayableCells
                : new ReadOnlySpan<int>(_frontierCells, 0, _frontierCount);

        private bool ShouldScanAllCells() =>
            _frontierCount * 100 >= EmptyCount * ScanAllCellsFrontierPercent;

        private void AddStone(int cell, int owner)
        {
            StoneCounts[owner]++;
            PositionScores[owner] += _geometry.PositionWeights[cell];
        }

        private void RemoveStone(int cell, int owner)
        {
            StoneCounts[owner]--;
            PositionScores[owner] -= _geometry.PositionWeights[cell];
        }

        private void AddToFrontier(int cell)
        {
            _frontierIndices[cell] = _frontierCount;
            _frontierCells[_frontierCount] = cell;
            _frontierCount++;
        }

        // 最後の要素を空いた場所に移して詰める
        private void RemoveFromFrontier(int cell)
        {
            int index = _frontierIndices[cell];
            if (index == NotInFrontier)
                return;

            _frontierCount--;
            int lastCell = _frontierCells[_frontierCount];
            _frontierCells[index] = lastCell;
            _frontierIndices[lastCell] = index;
            _frontierIndices[cell] = NotInFrontier;
        }
    }
}
