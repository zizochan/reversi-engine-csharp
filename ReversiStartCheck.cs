using System;
using System.Collections.Generic;

namespace Zizo.Reversi
{
    // 対局を始められない理由
    public enum ReversiStartError
    {
        // 始められる
        None,

        // 人数が2未満
        InvalidPlayerCount,

        // playerCount 以上の番号の石がある
        InvalidStone,

        // 最初から打てないプレイヤーがいる（石が1つもない場合も含む）
        PlayerCannotMove,
    }

    // 対局を始められるかを調べた結果
    public class ReversiStartCheck
    {
        public bool CanStart => Error == ReversiStartError.None;
        public ReversiStartError Error { get; }

        // Error が PlayerCannotMove の時、打てないプレイヤー（番号の小さい順）。それ以外は空
        public IReadOnlyList<int> PlayersWithoutMove { get; }

        public ReversiStartCheck(
            ReversiStartError error,
            IReadOnlyList<int> playersWithoutMove = null
        )
        {
            Error = error;
            PlayersWithoutMove = playersWithoutMove ?? Array.Empty<int>();
        }
    }
}
