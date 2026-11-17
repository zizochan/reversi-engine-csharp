namespace Zizo.Reversi
{
    // 盤の形から計算する、マスのつながり（各マスの8方向の隣のマス）
    // ルールの判定に必要な情報だけを持ち、CPUの設定には依存しない
    // 作るのに盤の大きさ分の計算がかかるため、ReversiBoard.Topology で壁の配置が変わるまで使い回す
    internal class ReversiBoardTopology
    {
        public const int DirectionCount = 8;
        public const int NoNeighbor = -1;

        // 方向nと方向n+4は逆向き
        private static readonly int[] DirectionX = { 1, 1, 0, -1, -1, -1, 0, 1 };
        private static readonly int[] DirectionY = { 0, 1, 1, 1, 0, -1, -1, -1 };

        // [マス * 8 + 方向] → 隣のマス。盤外・壁の場合は NoNeighbor
        private readonly int[] _neighbors;

        public ReversiBoardTopology(int width, int height, int[] cells)
        {
            _neighbors = BuildNeighbors(width, height, cells);
        }

        public int GetNeighbor(int cell, int direction) =>
            _neighbors[cell * DirectionCount + direction];

        private static int[] BuildNeighbors(int width, int height, int[] cells)
        {
            var neighbors = new int[cells.Length * DirectionCount];
            for (int cell = 0; cell < cells.Length; cell++)
            {
                int x = cell % width;
                int y = cell / width;
                for (int direction = 0; direction < DirectionCount; direction++)
                {
                    int neighborX = x + DirectionX[direction];
                    int neighborY = y + DirectionY[direction];
                    bool isInside =
                        neighborX >= 0 && neighborX < width && neighborY >= 0 && neighborY < height;
                    int neighbor = neighborY * width + neighborX;
                    neighbors[cell * DirectionCount + direction] =
                        isInside && cells[neighbor] != ReversiBoard.Wall ? neighbor : NoNeighbor;
                }
            }
            return neighbors;
        }
    }
}
