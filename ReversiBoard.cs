using System;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Zizo.Reversi
{
    /*
    リバーシの盤面。ルール（ReversiRules）とCPU（ReversiCpuSearcher）の両方で使う。
    各マスの値は、空き(Empty)、壁(Wall)、プレイヤー番号(0〜プレイヤー数-1)のいずれか。
    盤外と壁は同じ扱いになるため、壁を置くことで自由な形の盤を表せる。

    // 使い方
    var board = new ReversiBoard(width: 8, height: 8);
    board.SetCell(3, 3, 0);
    board.SetCell(4, 4, 0);
    board.SetCell(3, 4, 1);
    board.SetCell(4, 3, 1);

    // 2次元配列から作る場合は、rows[y, x] の順で指定する
    var board = ReversiBoard.FromRows(rows);

    // 同じ形式で書き出す（セーブデータや通信などに使う）
    int[,] rows = board.ToRows();

    // ToString() の文字表現から作る（デバッグ出力の読み戻しやテストなどに使う）
    var board = ReversiBoard.Parse("# . . #\n. 0 1 .\n. 1 0 .\n# . . #");
    */
    public class ReversiBoard
    {
        public const int Empty = -1;
        public const int Wall = -2;

        private const string EmptyText = ".";
        private const string WallText = "#";

        // Parse() でマスの区切りとして扱う文字（改行が "\r\n" の場合の '\r' も含める）
        private static readonly char[] CellSeparators = { ' ', '\t', '\r' };

        private readonly int[] _cells;

        // マスのつながり。壁の配置が変わるまで使い回す（壁の配置が変わったら null にして作り直す）
        private ReversiBoardTopology _topology;

        public int Width { get; }
        public int Height { get; }

        // 全マスが空きの盤面を作る
        public ReversiBoard(int width, int height)
        {
            if (width <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(width),
                    "width must be greater than 0"
                );
            if (height <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(height),
                    "height must be greater than 0"
                );

            Width = width;
            Height = height;
            _cells = new int[width * height];
            Array.Fill(_cells, Empty);
        }

        // rows[y, x] の2次元配列から盤面を作る
        public static ReversiBoard FromRows(int[,] rows)
        {
            if (rows == null)
                throw new ArgumentNullException(nameof(rows));

            var board = new ReversiBoard(width: rows.GetLength(1), height: rows.GetLength(0));
            for (int y = 0; y < board.Height; y++)
            {
                for (int x = 0; x < board.Width; x++)
                {
                    board.SetCell(x, y, rows[y, x]);
                }
            }
            return board;
        }

        // FromRows() と同じ rows[y, x] の2次元配列に書き出す
        public int[,] ToRows()
        {
            var rows = new int[Height, Width];
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    rows[y, x] = GetCell(x, y);
                }
            }
            return rows;
        }

        // ToString() の文字表現から盤面を作る。'.' = 空き、'#' = 壁、数字 = プレイヤー番号
        // マスは空白、行は改行で区切る（空行は無視する）。読めない場合は FormatException を投げる
        public static ReversiBoard Parse(string text)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));

            string[][] rows = text.Split('\n')
                .Select(line => line.Split(CellSeparators, StringSplitOptions.RemoveEmptyEntries))
                .Where(cellTexts => cellTexts.Length > 0)
                .ToArray();
            if (rows.Length == 0)
                throw new FormatException("text has no cells");

            int width = rows[0].Length;
            if (rows.Any(cellTexts => cellTexts.Length != width))
                throw new FormatException("every row must have the same number of cells");

            var board = new ReversiBoard(width, height: rows.Length);
            for (int y = 0; y < board.Height; y++)
            {
                for (int x = 0; x < board.Width; x++)
                {
                    board.SetCell(x, y, ToCellValue(rows[y][x]));
                }
            }
            return board;
        }

        public int GetCell(int x, int y) => _cells[ToCellIndex(x, y)];

        public void SetCell(int x, int y, int value)
        {
            if (value < Wall)
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    "value must be Empty, Wall or player index"
                );

            int cellIndex = ToCellIndex(x, y);
            if ((_cells[cellIndex] == Wall) != (value == Wall))
                _topology = null;
            _cells[cellIndex] = value;
        }

        // 独立したコピーを返す。Undo用の履歴などに使う
        public ReversiBoard Clone()
        {
            var clonedBoard = new ReversiBoard(Width, Height);
            Array.Copy(_cells, clonedBoard._cells, _cells.Length);
            // マスのつながりは変更されないため、コピーせずに共有する
            clonedBoard._topology = _topology;
            return clonedBoard;
        }

        // 指定した値のマスの数。プレイヤー番号を渡すと石数、Empty を渡すと空きマスの数になる
        public int CountCells(int value)
        {
            int count = 0;
            foreach (int cell in _cells)
            {
                if (cell == value)
                    count++;
            }
            return count;
        }

        // 大きさと全てのマスの値が同じか。盤面の比較やテストなどに使う
        // 盤面は変更できるため、Equals() ではなくこのメソッドで比較する
        public bool ContentEquals(ReversiBoard other) =>
            other != null
            && Width == other.Width
            && Height == other.Height
            && _cells.SequenceEqual(other._cells);

        // デバッグ用に、盤面を文字で表す。'.' = 空き、'#' = 壁、数字 = プレイヤー番号
        // 1マスずつ空白で区切り、1行ごとに改行する
        public override string ToString()
        {
            var builder = new StringBuilder();
            for (int y = 0; y < Height; y++)
            {
                if (y > 0)
                    builder.Append('\n');

                for (int x = 0; x < Width; x++)
                {
                    if (x > 0)
                        builder.Append(' ');
                    builder.Append(ToCellText(GetCell(x, y)));
                }
            }
            return builder.ToString();
        }

        // コピーせずに内部の配列を返す
        internal int[] RawCells => _cells;

        internal int[] CopyCells() => (int[])_cells.Clone();

        internal ReversiBoardTopology Topology =>
            _topology ??= new ReversiBoardTopology(Width, Height, _cells);

        internal int ToCellIndex(int x, int y)
        {
            if (x < 0 || x >= Width)
                throw new ArgumentOutOfRangeException(nameof(x), $"({x}, {y}) is out of the board");
            if (y < 0 || y >= Height)
                throw new ArgumentOutOfRangeException(nameof(y), $"({x}, {y}) is out of the board");

            return y * Width + x;
        }

        private static string ToCellText(int cell) =>
            cell switch
            {
                Empty => EmptyText,
                Wall => WallText,
                _ => cell.ToString(CultureInfo.InvariantCulture),
            };

        // ToCellText() の逆。プレイヤー番号は符号のない数字だけを受け付ける
        private static int ToCellValue(string cellText) =>
            cellText switch
            {
                EmptyText => Empty,
                WallText => Wall,
                _ => int.TryParse(
                    cellText,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out int player
                )
                    ? player
                    : throw new FormatException($"'{cellText}' is not a cell value"),
            };
    }
}
