using System.Collections.Generic;

namespace Zizo.Reversi
{
    // CPUの探索結果
    public class ReversiSearchResult
    {
        // スコアの高い順に並んだ候補手。空の場合は打てる手がない（パス）
        public IReadOnlyList<ReversiMove> Moves { get; }

        // 最後まで読み切れた深さ
        public int CompletedDepth { get; }

        // 探索した局面数
        public long NodeCount { get; }

        // 探索にかかった時間（ミリ秒）
        public long ElapsedMilliseconds { get; }

        public ReversiSearchResult(
            IReadOnlyList<ReversiMove> moves,
            int completedDepth,
            long nodeCount,
            long elapsedMilliseconds
        )
        {
            Moves = moves;
            CompletedDepth = completedDepth;
            NodeCount = nodeCount;
            ElapsedMilliseconds = elapsedMilliseconds;
        }
    }
}
