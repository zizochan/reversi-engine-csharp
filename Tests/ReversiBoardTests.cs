using NUnit.Framework;

namespace Zizo.Reversi.Tests
{
    public class ReversiBoardTests
    {
        // コピーした盤面は、元の盤面を変えても影響を受けない
        [Test]
        public void Clone_ReturnsIndependentBoard()
        {
            ReversiBoard board = ReversiTestHelper.CreateBoard("....", ".01.", ".10.", "....");
            ReversiBoard clonedBoard = board.Clone();
            ReversiRules.PlaceStone(board, x: 2, y: 0, player: 0);

            Assert.AreEqual(ReversiBoard.Empty, clonedBoard.GetCell(2, 0));
            Assert.AreEqual(1, clonedBoard.GetCell(2, 1));
            Assert.AreEqual(2, clonedBoard.CountCells(0));
        }

        // 壁を置いたり取り除いたりすると、マスのつながりが作り直され、ルールの判定に反映される
        [Test]
        public void SetCell_UpdatesRules_WhenWallChanges()
        {
            ReversiBoard board = ReversiTestHelper.CreateBoard(".10");
            Assert.IsTrue(ReversiRules.CanPlace(board, x: 0, y: 0, player: 0));

            board.SetCell(1, 0, ReversiBoard.Wall);
            Assert.IsFalse(ReversiRules.CanPlace(board, x: 0, y: 0, player: 0));

            board.SetCell(1, 0, 1);
            Assert.IsTrue(ReversiRules.CanPlace(board, x: 0, y: 0, player: 0));
        }

        // コピーした盤面に壁を置いても、元の盤面のルールの判定は変わらない
        [Test]
        public void Clone_KeepsOriginalRules_WhenWallIsAddedToClone()
        {
            ReversiBoard board = ReversiTestHelper.CreateBoard(".10");
            Assert.IsTrue(ReversiRules.CanPlace(board, x: 0, y: 0, player: 0));

            ReversiBoard clonedBoard = board.Clone();
            clonedBoard.SetCell(1, 0, ReversiBoard.Wall);

            Assert.IsFalse(ReversiRules.CanPlace(clonedBoard, x: 0, y: 0, player: 0));
            Assert.IsTrue(ReversiRules.CanPlace(board, x: 0, y: 0, player: 0));
        }

        // 石数・空きマス・壁の数を数えられる
        [Test]
        public void CountCells_CountsMatchingCells()
        {
            ReversiBoard board = ReversiTestHelper.CreateBoard("001", "#..");

            Assert.AreEqual(2, board.CountCells(0));
            Assert.AreEqual(1, board.CountCells(1));
            Assert.AreEqual(2, board.CountCells(ReversiBoard.Empty));
            Assert.AreEqual(1, board.CountCells(ReversiBoard.Wall));
        }

        // FromRows() と同じ rows[y, x] の形式で書き出し、読み戻すと同じ盤面になる
        [Test]
        public void ToRows_ReturnsRowsInFromRowsFormat()
        {
            ReversiBoard board = ReversiTestHelper.CreateBoard("0.#", "1..");
            int[,] rows = board.ToRows();

            Assert.AreEqual(2, rows.GetLength(0));
            Assert.AreEqual(3, rows.GetLength(1));
            Assert.AreEqual(ReversiBoard.Wall, rows[0, 2]);
            Assert.AreEqual(1, rows[1, 0]);
            Assert.IsTrue(ReversiBoard.FromRows(rows).ContentEquals(board));
        }

        // 書き出した配列を変更しても、元の盤面は変わらない
        [Test]
        public void ToRows_ReturnsIndependentArray()
        {
            ReversiBoard board = ReversiTestHelper.CreateBoard("0.");
            int[,] rows = board.ToRows();
            rows[0, 1] = 1;

            Assert.AreEqual(ReversiBoard.Empty, board.GetCell(1, 0));
        }

        // 大きさと全てのマスが同じ場合だけ、同じ盤面とみなす
        [Test]
        public void ContentEquals_ComparesSizeAndCells()
        {
            ReversiBoard board = ReversiTestHelper.CreateBoard("01", "..");

            Assert.IsTrue(board.ContentEquals(board.Clone()));
            Assert.IsFalse(board.ContentEquals(ReversiTestHelper.CreateBoard("01", ".#")));
            Assert.IsFalse(board.ContentEquals(ReversiTestHelper.CreateBoard("01..")));
            Assert.IsFalse(board.ContentEquals(null));
        }

        // 空き・壁・プレイヤー番号を、空白と改行で区切った文字で表す
        [Test]
        public void ToString_ReturnsCellText()
        {
            ReversiBoard board = ReversiTestHelper.CreateBoard("0.#", "1..");
            board.SetCell(2, 1, 10);

            Assert.AreEqual("0 . #\n1 . 10", board.ToString());
        }

        // ToString() の文字表現を読み戻すと、同じ盤面になる
        [Test]
        public void Parse_ReturnsSameBoard_FromToStringText()
        {
            ReversiBoard board = ReversiTestHelper.CreateBoard("0.#", "1..");
            board.SetCell(2, 1, 10);

            Assert.IsTrue(ReversiBoard.Parse(board.ToString()).ContentEquals(board));
        }

        // 改行が "\r\n" でも、空行や余分な空白があっても読める
        [Test]
        public void Parse_IgnoresCarriageReturnAndBlankLines()
        {
            ReversiBoard board = ReversiBoard.Parse("\r\n0  . #\r\n\r\n1 . .\r\n");

            Assert.IsTrue(board.ContentEquals(ReversiTestHelper.CreateBoard("0.#", "1..")));
        }

        // 読めない文字や、行ごとにマスの数が違う場合は FormatException を投げる
        [TestCase("0 . x")]
        [TestCase("0 . -1")]
        [TestCase("0 . #\n1 .")]
        [TestCase("\n \n")]
        public void Parse_Throws_WhenTextIsInvalid(string text)
        {
            Assert.Throws<System.FormatException>(() => ReversiBoard.Parse(text));
        }
    }
}
