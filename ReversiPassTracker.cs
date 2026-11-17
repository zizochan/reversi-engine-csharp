using System;

namespace Zizo.Reversi
{
    // 続けてパスしたプレイヤーを数え、終局（全員が続けてパスした）かを判定する
    // 手番の順番は自由に決められるため、パスの回数ではなく、パスしたプレイヤーの種類で判定する
    internal sealed class ReversiPassTracker
    {
        // 打てるプレイヤーに手番が回ってこない順番だと、パスが終わらなくなるため、この回数で打ち切る
        private const int MaxConsecutivePassCount = 1024;

        private readonly bool[] _hasPassed;
        private int _passedPlayerCount;
        private int _passCount;

        public ReversiPassTracker(int playerCount)
        {
            _hasPassed = new bool[playerCount];
        }

        // 誰かが石を置いたら、続けてパスした記録を消す
        public void Reset()
        {
            if (_passCount == 0)
                return;

            Array.Clear(_hasPassed, 0, _hasPassed.Length);
            _passedPlayerCount = 0;
            _passCount = 0;
        }

        // player のパスを記録する。全員が続けてパスしたら true（終局）
        public bool AddPass(int player)
        {
            _passCount++;
            if (_passCount > MaxConsecutivePassCount)
                throw new InvalidOperationException(
                    "turnOrder does not give a turn to a player who can move"
                );

            if (!_hasPassed[player])
            {
                _hasPassed[player] = true;
                _passedPlayerCount++;
            }
            return _passedPlayerCount == _hasPassed.Length;
        }
    }
}
