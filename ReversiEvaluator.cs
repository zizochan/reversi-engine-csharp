using System;

namespace Zizo.Reversi
{
    // 盤面を、CPUのプレイヤーから見た -1〜1 の値で評価する
    // 3人以上の場合は、相手全員の平均と比較する
    internal class ReversiEvaluator
    {
        // 終局していない盤面は -0.9〜0.9、終局した盤面は勝ちなら0.95以上・負けなら-0.95以下にする
        private const double NonTerminalScale = 0.9;
        private const double TerminalBaseValue = 0.95;
        private const double TerminalMarginScale = 0.05;

        private readonly ReversiBoardGeometry _geometry;
        private readonly ReversiCpuSettings _settings;
        private readonly int _playerCount;
        private readonly int[] _stoneCounts;
        private readonly int[] _positionScores;
        private readonly int[] _mobilityCounts;

        public ReversiEvaluator(
            ReversiBoardGeometry geometry,
            ReversiCpuSettings settings,
            int playerCount
        )
        {
            _geometry = geometry;
            _settings = settings;
            _playerCount = playerCount;
            _stoneCounts = new int[playerCount];
            _positionScores = new int[playerCount];
            _mobilityCounts = new int[playerCount];
        }

        public double Evaluate(ReversiSearchBoard board, int rootPlayer)
        {
            CountStonesAndPositions(board);
            board.CountMovesOfAllPlayers(_mobilityCounts);

            double progress = 1.0 - (double)board.EmptyCount / _geometry.PlayableCells.Length;
            double positionWeight = Lerp(
                _settings.OpeningPositionWeight,
                _settings.EndingPositionWeight,
                progress
            );
            double mobilityWeight = Lerp(
                _settings.OpeningMobilityWeight,
                _settings.EndingMobilityWeight,
                progress
            );
            double stoneWeight = Lerp(
                _settings.OpeningStoneWeight,
                _settings.EndingStoneWeight,
                progress
            );
            double totalWeight = positionWeight + mobilityWeight + stoneWeight;
            if (totalWeight <= 0)
                return 0;

            double positionTerm = Math.Tanh(
                DifferenceFromOpponentAverage(_positionScores, rootPlayer) / _geometry.PositionScale
            );
            double mobilityTerm = RatioFromOpponentAverage(_mobilityCounts, rootPlayer);
            double stoneTerm = RatioFromOpponentAverage(_stoneCounts, rootPlayer);

            double weightedSum =
                positionWeight * positionTerm
                + mobilityWeight * mobilityTerm
                + stoneWeight * stoneTerm;
            return NonTerminalScale * weightedSum / totalWeight;
        }

        // 終局した盤面の評価。一番石の多い相手より多ければ勝ち、同じなら引き分け(0)
        public double EvaluateTerminal(ReversiSearchBoard board, int rootPlayer)
        {
            CountStonesAndPositions(board);

            int ownStones = _stoneCounts[rootPlayer];
            int bestOpponentStones = 0;
            int totalStones = 0;
            for (int player = 0; player < _playerCount; player++)
            {
                totalStones += _stoneCounts[player];
                if (player != rootPlayer)
                    bestOpponentStones = Math.Max(bestOpponentStones, _stoneCounts[player]);
            }

            if (ownStones == bestOpponentStones)
                return 0;

            double margin =
                (double)Math.Abs(ownStones - bestOpponentStones) / Math.Max(1, totalStones);
            double value = TerminalBaseValue + TerminalMarginScale * margin;
            return ownStones > bestOpponentStones ? value : -value;
        }

        // 石数とマスの重みの合計は盤面側で差分更新しているため、隅の隣のマスの補正だけここで計算する
        private void CountStonesAndPositions(ReversiSearchBoard board)
        {
            Array.Copy(board.StoneCounts, _stoneCounts, _playerCount);
            Array.Copy(board.PositionScores, _positionScores, _playerCount);

            int[] cells = board.Cells;
            for (int pairIndex = 0; pairIndex < _geometry.CornerNeighborCells.Length; pairIndex++)
            {
                int owner = cells[_geometry.CornerNeighborCells[pairIndex]];
                if (owner >= 0 && cells[_geometry.AdjacentCorners[pairIndex]] == ReversiBoard.Empty)
                    _positionScores[owner] += _geometry.CornerNeighborWeight;
            }
        }

        private double OpponentAverage(int[] values, int rootPlayer)
        {
            double total = 0;
            for (int player = 0; player < _playerCount; player++)
            {
                if (player != rootPlayer)
                    total += values[player];
            }
            return total / (_playerCount - 1);
        }

        private double DifferenceFromOpponentAverage(int[] values, int rootPlayer) =>
            values[rootPlayer] - OpponentAverage(values, rootPlayer);

        // 自分と相手平均の差を、合計で割って -1〜1 にする
        private double RatioFromOpponentAverage(int[] values, int rootPlayer)
        {
            double own = values[rootPlayer];
            double opponentAverage = OpponentAverage(values, rootPlayer);
            double total = own + opponentAverage;
            return total > 0 ? (own - opponentAverage) / total : 0;
        }

        private static double Lerp(double from, double to, double progress) =>
            from + (to - from) * progress;
    }
}
