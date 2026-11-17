using System;
using NUnit.Framework;

namespace Zizo.Reversi.Tests
{
    public class ReversiTurnOrderTests
    {
        // 通常の順番では、通し番号 n の手番はプレイヤー n % playerCount
        [TestCase(0, 0)]
        [TestCase(2, 2)]
        [TestCase(3, 0)]
        [TestCase(7, 1)]
        public void Rotation_ReturnsTurnNumberModuloPlayerCount(int turnNumber, int expected)
        {
            ReversiTurnOrder turnOrder = ReversiTurnOrder.Rotation(playerCount: 3);

            Assert.AreEqual(expected, turnOrder.GetPlayer(turnNumber));
        }

        // 渡した関数で順番が決まる
        [Test]
        public void GetPlayer_UsesGivenFunction()
        {
            var turnOrder = new ReversiTurnOrder(playerCount: 2, turnNumber => turnNumber / 2 % 2);

            int[] players = new int[6];
            for (int turnNumber = 0; turnNumber < players.Length; turnNumber++)
            {
                players[turnNumber] = turnOrder.GetPlayer(turnNumber);
            }

            CollectionAssert.AreEqual(new[] { 0, 0, 1, 1, 0, 0 }, players);
        }

        // 不正な引数の場合は例外を投げる
        [Test]
        public void Constructor_Throws_WhenArgumentsAreInvalid()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new ReversiTurnOrder(playerCount: 1, turnNumber => 0)
            );
            Assert.Throws<ArgumentNullException>(() => new ReversiTurnOrder(playerCount: 2, null));
        }

        // 負の通し番号を渡した場合は例外を投げる
        [Test]
        public void GetPlayer_Throws_WhenTurnNumberIsNegative()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                ReversiTurnOrder.Rotation(playerCount: 2).GetPlayer(-1)
            );
        }

        // 関数が範囲外のプレイヤー番号を返した場合は例外を投げる
        [TestCase(-1)]
        [TestCase(2)]
        public void GetPlayer_Throws_WhenFunctionReturnsInvalidPlayer(int invalidPlayer)
        {
            var turnOrder = new ReversiTurnOrder(playerCount: 2, turnNumber => invalidPlayer);

            Assert.Throws<InvalidOperationException>(() => turnOrder.GetPlayer(0));
        }
    }
}
