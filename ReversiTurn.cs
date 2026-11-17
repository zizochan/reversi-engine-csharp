using System.Collections.Generic;

namespace Zizo.Reversi
{
    // 次の手番を調べた結果
    public class ReversiTurn
    {
        public const int NoPlayer = -1;

        // 次に打つプレイヤー。全員打てない場合は NoPlayer
        public int Player { get; }

        // 次に打つ手番の通し番号（ReversiTurnOrder を参照）。打ったら、TurnNumber + 1 から次の手番を調べる
        // 終局の場合は、最後にパスした手番の次の番号
        // playerCount を渡す FindNextTurn() では、startPlayer を通し番号とみなして数える
        public int TurnNumber { get; }

        // 全員打てない場合は終局
        public bool IsGameOver => Player == NoPlayer;

        // 打てる手がなく、パスになったプレイヤー（手番の順。順番によっては同じプレイヤーが何度も入る）
        public IReadOnlyList<int> PassedPlayers { get; }

        public ReversiTurn(int player, int turnNumber, IReadOnlyList<int> passedPlayers)
        {
            Player = player;
            TurnNumber = turnNumber;
            PassedPlayers = passedPlayers;
        }
    }
}
