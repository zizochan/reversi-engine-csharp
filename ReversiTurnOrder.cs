using System;

namespace Zizo.Reversi
{
    /*
    手番の順番。手番の通し番号（対局の最初の手番を 0 とし、パスも1手番と数える）から、その手番に打つプレイヤーを決める。
    ReversiRules.FindNextTurn() と CPU の探索に渡すと、その順番で手番を進める（CPUもその順番で先読みする）。

    // 使い方（通常の順番。0 → 1 → … → playerCount - 1 → 0）
    ReversiTurnOrder turnOrder = ReversiTurnOrder.Rotation(playerCount: 2);

    // 使い方（二手連続。0, 0, 1, 1, 0, 0, …）
    var turnOrder = new ReversiTurnOrder(playerCount: 2, turnNumber => turnNumber / 2 % 2);

    // 注意
    ・getPlayer は探索中に何度も呼ばれるため、同じ通し番号には常に同じプレイヤーを返す、軽い処理にすること
    ・打てる手があるプレイヤーには、いずれ必ず手番が回ってくる順番にすること
    */
    public sealed class ReversiTurnOrder
    {
        private const int MinPlayerCount = 2;

        private readonly Func<int, int> _getPlayer;

        public int PlayerCount { get; }

        public ReversiTurnOrder(int playerCount, Func<int, int> getPlayer)
        {
            if (playerCount < MinPlayerCount)
                throw new ArgumentOutOfRangeException(
                    nameof(playerCount),
                    "playerCount must be 2 or more"
                );

            PlayerCount = playerCount;
            _getPlayer = getPlayer ?? throw new ArgumentNullException(nameof(getPlayer));
        }

        // 通常の順番（0 → 1 → … → playerCount - 1 → 0）。通し番号 n の手番は、プレイヤー n % playerCount
        public static ReversiTurnOrder Rotation(int playerCount) =>
            new(playerCount, turnNumber => turnNumber % playerCount);

        // 通し番号 turnNumber の手番に打つプレイヤー
        public int GetPlayer(int turnNumber)
        {
            ValidateTurnNumber(turnNumber);
            int player = _getPlayer(turnNumber);
            if (player < 0 || player >= PlayerCount)
                throw new InvalidOperationException(
                    $"getPlayer returned {player} for turn {turnNumber}; it must be 0 to playerCount - 1"
                );
            return player;
        }

        internal static void ValidateTurnNumber(int turnNumber)
        {
            if (turnNumber < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(turnNumber),
                    "turnNumber must be 0 or more"
                );
        }
    }
}
