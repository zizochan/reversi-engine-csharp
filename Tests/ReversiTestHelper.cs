namespace Zizo.Reversi.Tests
{
    // リバーシのテスト用に、文字列から盤面を作る
    // '.' = 空き、'#' = 壁、'0'〜'9' = プレイヤー番号
    public static class ReversiTestHelper
    {
        public static ReversiBoard CreateBoard(params string[] rows)
        {
            var board = new ReversiBoard(width: rows[0].Length, height: rows.Length);
            for (int y = 0; y < rows.Length; y++)
            {
                for (int x = 0; x < rows[y].Length; x++)
                {
                    board.SetCell(x, y, ToCellValue(rows[y][x]));
                }
            }
            return board;
        }

        // 通常の8x8の初期配置
        public static ReversiBoard CreateStandardBoard() =>
            CreateBoard(
                "........",
                "........",
                "........",
                "...01...",
                "...10...",
                "........",
                "........",
                "........"
            );

        private static int ToCellValue(char character) =>
            character switch
            {
                '.' => ReversiBoard.Empty,
                '#' => ReversiBoard.Wall,
                _ => character - '0',
            };
    }
}
