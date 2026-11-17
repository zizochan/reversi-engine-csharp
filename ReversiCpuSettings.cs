using System;

namespace Zizo.Reversi
{
    /*
    CPUの探索設定。
    MonoBehaviourの[SerializeField]に持たせれば、Inspectorから数値を調整できる。
    UnityEngineに依存しないよう、プロパティではなくpublicフィールドで定義している。

    // 計算時間の調整
    MaxSearchDepth、MaxNodeCount、TimeLimitMilliseconds のうち、最初に上限に達したところで探索を打ち切る。
    打ち切った場合は、最後まで読み切れた深さの結果を返す（1手先だけは必ず読む）。
    実際の計算量は、ReversiSearchResult の CompletedDepth / NodeCount / ElapsedMilliseconds で確認できる。
    */
    [Serializable]
    public class ReversiCpuSettings
    {
        // 返す候補手の数。0以下の場合は全ての合法手を返す
        public int CandidateCount = 5;

        // 何手先まで読むか（他のプレイヤーの手も1手として数える）
        public int MaxSearchDepth = 4;

        // 探索する局面数の上限。0以下の場合は無制限
        public int MaxNodeCount = 20000;

        // 探索時間の上限（ミリ秒）。0以下の場合は無制限
        public int TimeLimitMilliseconds = 0;

        // 残りの空きマスがこの数以下になったら、終局まで読み切る
        public int ExactSearchEmptyCount = 8;

        // 評価の比率（序盤 → 終盤へ、盤の埋まり具合に応じて変化する）
        // 位置：隅や辺などのマスの重み、着手可能数：打てる手の多さ、石数：石の数
        public float OpeningPositionWeight = 0.5f;
        public float OpeningMobilityWeight = 0.4f;
        public float OpeningStoneWeight = 0.1f;
        public float EndingPositionWeight = 0.2f;
        public float EndingMobilityWeight = 0.1f;
        public float EndingStoneWeight = 0.7f;

        // マスの重み（盤の形から、隅・辺・それ以外を自動で判定する）
        public int CornerWeight = 100;
        public int EdgeWeight = 10;
        public int InnerWeight = 0;

        // 隅が空いている時の、隅の隣のマスの重み（隅を取られやすくなるため）
        public int CornerNeighborWeight = -30;

        // マスの重みを直接指定する場合は rows[y, x] の配列を入れる（自動判定は使わない）
        [NonSerialized]
        public int[,] CustomPositionWeights;
    }
}
