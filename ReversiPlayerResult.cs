namespace Zizo.Reversi
{
    // プレイヤーごとの最終結果
    public readonly struct ReversiPlayerResult
    {
        public int Player { get; }
        public int StoneCount { get; }

        // 自分より石の多いプレイヤーの数 + 1（同数の場合は同じ順位になる）
        public int Rank { get; }

        public ReversiPlayerResult(int player, int stoneCount, int rank)
        {
            Player = player;
            StoneCount = stoneCount;
            Rank = rank;
        }
    }
}
