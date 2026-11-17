namespace Zizo.Reversi
{
    // CPUが返す候補手
    public readonly struct ReversiMove
    {
        public int X { get; }
        public int Y { get; }

        // 打つマスの座標。ReversiRules.GetLegalMoves() などの結果と比べる時に使う
        public ReversiPosition Position => new(X, Y);

        // 0〜100。50が互角、100に近いほど有利（勝ちが確定した手は95以上、負けが確定した手は5以下）
        public float Score { get; }

        public ReversiMove(int x, int y, float score)
        {
            X = x;
            Y = y;
            Score = score;
        }
    }
}
