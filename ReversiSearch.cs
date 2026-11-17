using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Zizo.Reversi
{
    /*
    フレームをまたいで少しずつ進める、1回分の探索。ReversiCpuSearcher.StartSearch() で作り、Step() で進める。
    使い方は ReversiCpuSearcher の先頭のコメントを参照。

    // 実装について
    途中で止めて続きから再開できるように、再帰を使わず、読んでいる途中の局面をスタック（_frames）に積んで探索する
    */
    public sealed class ReversiSearch
    {
        // 時間の確認は重いため、何局面かごとに行う
        // 盤が大きいほど1局面の計算量が増えるため、マスの数が多いほど頻繁に確認する（8x8で256局面ごと）
        private const int TimeCheckCellBudget = 16384;
        private const double MaxScore = 100;

        private readonly ReversiSearchBoard _board;
        private readonly ReversiBoardGeometry _geometry;
        private readonly ReversiEvaluator _evaluator;
        private readonly ReversiCpuSettings _settings;
        private readonly List<int[]> _moveBuffers = new();
        private readonly List<SearchFrame> _frames = new();
        private readonly Stopwatch _stopwatch = new();
        private readonly ReversiTurnOrder _turnOrder;
        private readonly ReversiPassTracker _passTracker;
        private readonly int _rootTurnNumber;
        private readonly int _rootPlayer;
        private readonly int _timeCheckInterval;
        private readonly int[] _rootMoves;
        private readonly int _rootMoveCount;
        private readonly int _maxDepth;
        private readonly double[] _values;

        private long _nodeCount;
        private bool _canAbort;
        private bool _isAborted;

        // 今読んでいる深さと、ルートの何番目の手を読んでいるか
        private int _depth = 1;
        private int _rootMoveIndex;
        private int _rootCheckpoint;
        private double[] _completedValues;
        private int _completedDepth;

        // 使っている_framesの数（_framesの要素は使い回す）
        private int _frameCount;

        // 読み終えた局面の評価値。積まれている一番上の局面（なければルート）が受け取る
        private bool _hasChildValue;
        private double _childValue;

        // Step() の時間を使い切ったら、探索を一時停止する
        private long _stepDeadlineMilliseconds;
        private bool _isPauseRequested;

        // RequestStop() が呼ばれたら、上限に達した時と同じように探索を打ち切る
        private bool _isStopRequested;

        public bool IsCompleted { get; private set; }

        // 探索結果。探索が終わるまでは null
        public ReversiSearchResult Result { get; private set; }

        // 盤面はここでコピーするため、探索中に board を変更しても結果には影響しない
        internal ReversiSearch(
            ReversiBoard board,
            int rootTurnNumber,
            ReversiTurnOrder turnOrder,
            ReversiCpuSettings settings
        )
        {
            int[] cells = board.CopyCells();
            int playerCount = turnOrder.PlayerCount;
            _turnOrder = turnOrder;
            _passTracker = new ReversiPassTracker(playerCount);
            _rootTurnNumber = rootTurnNumber;
            _rootPlayer = turnOrder.GetPlayer(rootTurnNumber);
            _settings = settings;
            _geometry = new ReversiBoardGeometry(board, settings);
            _evaluator = new ReversiEvaluator(_geometry, settings, playerCount);
            _board = new ReversiSearchBoard(cells, _geometry, playerCount);
            _timeCheckInterval = Math.Max(1, TimeCheckCellBudget / cells.Length);

            _rootMoves = GetMoveBuffer(ply: 0);
            _rootMoveCount = _board.CollectMoves(_rootPlayer, _rootMoves);
            if (_rootMoveCount == 0)
            {
                IsCompleted = true;
                Result = new ReversiSearchResult(
                    Array.Empty<ReversiMove>(),
                    completedDepth: 0,
                    nodeCount: 0,
                    elapsedMilliseconds: 0
                );
                return;
            }

            _maxDepth = Math.Max(1, _settings.MaxSearchDepth);
            if (_board.EmptyCount <= _settings.ExactSearchEmptyCount)
                _maxDepth = Math.Max(_maxDepth, _board.EmptyCount);

            _values = new double[_rootMoveCount];
            // 1手先だけは、上限を超えても必ず読み切る
            _canAbort = false;
        }

        // 探索を budgetMilliseconds ほど進める。探索が終わったら true を返す
        // 時間の確認は何局面かごとに行うため、少し超えることがある
        // 設定の TimeLimitMilliseconds は、Step() を呼んでいる間の時間の合計で判定する
        public bool Step(int budgetMilliseconds)
        {
            Run(_stopwatch.ElapsedMilliseconds + budgetMilliseconds);
            return IsCompleted;
        }

        // 探索を打ち切るように指示する。次の Step() で、最後まで読み切れた深さの結果を返して終わる
        // ただし1手先だけは必ず読み切るため、1手先を読んでいる途中なら、読み終わるまで Step() が続く
        // フレームの待ち時間を含めた、実際の経過時間で思考時間を制限したい場合などに使う
        public void RequestStop()
        {
            _isStopRequested = true;
        }

        internal void RunToCompletion() => Run(long.MaxValue);

        // 探索の時間（_stopwatch）が deadlineMilliseconds に達するまで探索を進める
        private void Run(long deadlineMilliseconds)
        {
            if (IsCompleted)
                return;

            _stopwatch.Start();
            _stepDeadlineMilliseconds = deadlineMilliseconds;
            Advance();
            _stopwatch.Stop();
        }

        // 探索が終わるか、Step() の時間を使い切るまで探索を進める
        private void Advance()
        {
            _isPauseRequested = false;
            while (!IsCompleted && !_isPauseRequested)
            {
                if (_frameCount == 0)
                    AdvanceRoot();
                else
                    AdvanceFrame(_frames[_frameCount - 1]);
            }
        }

        // 候補手を全て正確に比べるため、ルートでは手ごとに全範囲で読む
        private void AdvanceRoot()
        {
            if (_hasChildValue)
            {
                _hasChildValue = false;
                _board.UndoTo(_rootCheckpoint);
                if (_isAborted)
                {
                    Complete();
                    return;
                }

                _values[_rootMoveIndex] = _childValue;
                _rootMoveIndex++;
                return;
            }

            if (_rootMoveIndex >= _rootMoveCount)
            {
                CompleteDepth();
                return;
            }

            _rootCheckpoint = _board.Checkpoint;
            _board.PlaceStone(_rootMoves[_rootMoveIndex], _rootPlayer);
            EnterNode(
                _rootTurnNumber + 1,
                _depth - 1,
                double.NegativeInfinity,
                double.PositiveInfinity,
                ply: 1
            );
        }

        // 積まれている一番上の局面で、読み終えた手の評価値を受け取るか、次の手を読み始める
        private void AdvanceFrame(SearchFrame frame)
        {
            if (!_hasChildValue)
            {
                frame.Checkpoint = _board.Checkpoint;
                _board.PlaceStone(_moveBuffers[frame.Ply][frame.MoveIndex], frame.Player);
                EnterNode(
                    frame.TurnNumber + 1,
                    frame.Depth - 1,
                    frame.Alpha,
                    frame.Beta,
                    frame.Ply + 1
                );
                return;
            }

            _hasChildValue = false;
            _board.UndoTo(frame.Checkpoint);
            if (_isAborted)
            {
                ReturnFromFrame(0);
                return;
            }

            if (frame.IsMaximizing)
            {
                frame.BestValue = Math.Max(frame.BestValue, _childValue);
                frame.Alpha = Math.Max(frame.Alpha, frame.BestValue);
            }
            else
            {
                frame.BestValue = Math.Min(frame.BestValue, _childValue);
                frame.Beta = Math.Min(frame.Beta, frame.BestValue);
            }

            frame.MoveIndex++;
            if (frame.Alpha >= frame.Beta || frame.MoveIndex >= frame.MoveCount)
                ReturnFromFrame(frame.BestValue);
        }

        // 通し番号 turnNumber の手番の局面を読み始める。すぐに評価できる局面は評価値を返し、そうでなければ_framesに積む
        private void EnterNode(int turnNumber, int depth, double alpha, double beta, int ply)
        {
            int player = _turnOrder.GetPlayer(turnNumber);
            _passTracker.Reset();
            while (true)
            {
                _nodeCount++;
                bool shouldCheckTime = _nodeCount % _timeCheckInterval == 0;
                if (shouldCheckTime && _stopwatch.ElapsedMilliseconds >= _stepDeadlineMilliseconds)
                    _isPauseRequested = true;
                if (_canAbort && IsLimitExceeded(shouldCheckTime))
                {
                    _isAborted = true;
                    ReturnValue(0);
                    return;
                }

                if (_board.EmptyCount == 0)
                {
                    ReturnValue(_evaluator.EvaluateTerminal(_board, _rootPlayer));
                    return;
                }
                if (depth == 0)
                {
                    ReturnValue(_evaluator.Evaluate(_board, _rootPlayer));
                    return;
                }

                int[] moves = GetMoveBuffer(ply);
                int moveCount = _board.CollectMoves(player, moves);
                if (moveCount > 0)
                {
                    PushFrame(turnNumber, player, depth, alpha, beta, ply, moveCount);
                    return;
                }

                // 全員が続けてパスしたら終局。パスは深さを消費しない
                if (_passTracker.AddPass(player))
                {
                    ReturnValue(_evaluator.EvaluateTerminal(_board, _rootPlayer));
                    return;
                }
                turnNumber++;
                player = _turnOrder.GetPlayer(turnNumber);
                ply++;
            }
        }

        // 深さ1つ分を読み終えた。上限に達していなければ、1手深く読み直す
        private void CompleteDepth()
        {
            _completedValues = (double[])_values.Clone();
            _completedDepth = _depth;
            if (_depth >= _maxDepth || IsLimitExceeded(shouldCheckTime: true))
            {
                Complete();
                return;
            }

            _depth++;
            _rootMoveIndex = 0;
            _canAbort = true;
        }

        private void Complete()
        {
            IsCompleted = true;
            Result = new ReversiSearchResult(
                BuildCandidateMoves(_rootMoves, _rootMoveCount, _completedValues),
                _completedDepth,
                _nodeCount,
                _stopwatch.ElapsedMilliseconds
            );
        }

        private void PushFrame(
            int turnNumber,
            int player,
            int depth,
            double alpha,
            double beta,
            int ply,
            int moveCount
        )
        {
            if (_frameCount == _frames.Count)
                _frames.Add(new SearchFrame());

            bool isMaximizing = player == _rootPlayer;
            SearchFrame frame = _frames[_frameCount];
            frame.TurnNumber = turnNumber;
            frame.Player = player;
            frame.Depth = depth;
            frame.Alpha = alpha;
            frame.Beta = beta;
            frame.Ply = ply;
            frame.MoveCount = moveCount;
            frame.MoveIndex = 0;
            frame.IsMaximizing = isMaximizing;
            frame.BestValue = isMaximizing ? double.NegativeInfinity : double.PositiveInfinity;
            _frameCount++;
        }

        private void ReturnFromFrame(double value)
        {
            _frameCount--;
            ReturnValue(value);
        }

        private void ReturnValue(double value)
        {
            _childValue = value;
            _hasChildValue = true;
        }

        private bool IsLimitExceeded(bool shouldCheckTime)
        {
            if (_isStopRequested)
                return true;
            if (_settings.MaxNodeCount > 0 && _nodeCount >= _settings.MaxNodeCount)
                return true;

            return shouldCheckTime
                && _settings.TimeLimitMilliseconds > 0
                && _stopwatch.ElapsedMilliseconds >= _settings.TimeLimitMilliseconds;
        }

        // 深さ（ply）ごとに手のバッファを使い回し、GCを抑える
        private int[] GetMoveBuffer(int ply)
        {
            while (_moveBuffers.Count <= ply)
            {
                _moveBuffers.Add(new int[_board.Cells.Length]);
            }
            return _moveBuffers[ply];
        }

        private IReadOnlyList<ReversiMove> BuildCandidateMoves(
            int[] rootMoves,
            int rootMoveCount,
            double[] values
        )
        {
            int candidateCount =
                _settings.CandidateCount > 0 ? _settings.CandidateCount : rootMoveCount;
            return Enumerable
                .Range(0, rootMoveCount)
                .OrderByDescending(moveIndex => values[moveIndex])
                .Take(candidateCount)
                .Select(moveIndex => ToMove(rootMoves[moveIndex], values[moveIndex]))
                .ToList();
        }

        // 評価値（-1〜1）を、スコア（0〜100）に変換する
        private ReversiMove ToMove(int cell, double value)
        {
            double score = Math.Clamp((value + 1) / 2 * MaxScore, 0, MaxScore);
            return new ReversiMove(cell % _geometry.Width, cell / _geometry.Width, (float)score);
        }

        // 読んでいる途中の局面。再帰で書いた場合の、1回の呼び出しのローカル変数にあたる
        private sealed class SearchFrame
        {
            public int TurnNumber;
            public int Player;
            public int Depth;
            public double Alpha;
            public double Beta;
            public int Ply;
            public int MoveCount;
            public int MoveIndex;
            public bool IsMaximizing;
            public double BestValue;

            // 今読んでいる手を打つ直前の、_boardの位置（ReversiSearchBoard.Checkpoint）
            public int Checkpoint;
        }
    }
}
