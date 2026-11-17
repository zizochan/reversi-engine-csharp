# Reversi Engine

リバーシのルール処理と CPU 思考を行う C# ライブラリです。描画や UI には依存せず、
**盤面の管理、合法手の判定、石を置いて裏返す処理、手番・パス・終局の判定、結果の集計、CPU による候補手の探索**
をまとめて提供します。

- UnityEngine などのエンジンや外部ライブラリに依存しない純粋な C# コード（Unity でもそのまま動作）
- 盤の形は自由（四角いグリッドに壁を置いて、穴あき・十字型などの盤を表せます）
- 3 人以上の対戦にも対応
- ルール（`ReversiRules`）は CPU に依存しないため、CPU を使わないゲームでも単体で使えます
- CPU は、フレームをまたいで少しずつ探索できます（スレッドを使わないため、WebGL でも動作）
- NUnit によるテスト付き

様々なゲームで使い回せるよう、リバーシの基礎的なロジックだけを持ちます。
次のものはライブラリの外（ゲーム側）で行います。

- 盤面の作成（盤の形、壁、初期配置の石）
- 画面表示・入力・演出
- 対局の進行管理（履歴、Undo、セーブデータなど）
- CPU の強さの調整（候補手のうち、どれを選ぶか）

> English summary is available at the [bottom of this page](#english-summary).

## 目次

- [動作環境](#動作環境)
- [導入方法](#導入方法)
- [クイックスタート](#クイックスタート)
- [全体像](#全体像)
- [盤面](#盤面)
- [ルール](#ルール)
- [CPU](#cpu)
- [よくある使い方](#よくある使い方)
- [制限事項](#制限事項)
- [テスト](#テスト)
- [ライセンス](#ライセンス)

## 動作環境

- C# 9.0 以降
- .NET Standard 2.1 相当の API（`HashCode`、`ReadOnlySpan<T>` 等）
- Unity の場合は C# 9.0 に対応した 2021.2 以降が目安です（開発・テストは Unity 6000.6 で実施）
- 外部ライブラリへの依存なし（テストのみ NUnit を使用）

## 導入方法

`*.cs` ファイルをそのまま自分のプロジェクトにコピーして使います（パッケージとしての配布は行っていません）。

1. このリポジトリを clone するか、ZIP でダウンロードします。
2. `Tests/` 以外の `*.cs` ファイルを、プロジェクトの任意の場所にコピーします。
3. 名前空間は `Zizo.Reversi` です。`using Zizo.Reversi;` して使ってください。

`AssemblyInfo.cs` はテストから内部クラスを参照するための設定です。テストを使わない場合も、残したままで問題ありません。

### Unity の場合

`Assets/` 以下の任意のフォルダにコピーすれば動作します。他のコードとアセンブリを分けたい場合は、
コピーしたフォルダに Assembly Definition を作成してください（UnityEngine を参照しないため、
`No Engine References` を有効にできます）。

### Unity 以外の .NET の場合

`netstandard2.1` / `net6.0` 以降などのプロジェクトに含めてください。C# 9.0 以降が必要です。

## クイックスタート

CPU 同士で 1 局打ち、結果を表示する例です（.NET のコンソールアプリでそのまま動きます）。

```csharp
using System;
using Zizo.Reversi;

const int PlayerCount = 2;

// 1. 盤面を作る（'.' = 空き、'#' = 壁、数字 = プレイヤー番号）
ReversiBoard board = ReversiBoard.Parse(@"
. . . . . . . .
. . . . . . . .
. . . . . . . .
. . . 1 0 . . .
. . . 0 1 . . .
. . . . . . . .
. . . . . . . .
. . . . . . . .");

// 2. 対局を始められるか確認する
ReversiStartCheck startCheck = ReversiRules.CheckStart(board, PlayerCount);
if (!startCheck.CanStart)
    throw new InvalidOperationException($"始められない盤面です: {startCheck.Error}");

// 3. 打てるプレイヤーがいる間、手番を進める
var settings = new ReversiCpuSettings { MaxSearchDepth = 4 };
ReversiTurn turn = ReversiRules.FindNextTurn(board, startPlayer: 0, PlayerCount);
while (!turn.IsGameOver)
{
    // CPU に候補手を探させ、一番スコアの高い手を打つ
    ReversiSearchResult result = ReversiCpuSearcher.Search(board, turn.Player, PlayerCount, settings);
    ReversiMove bestMove = result.Moves[0];
    ReversiRules.PlaceStone(board, bestMove.X, bestMove.Y, turn.Player);

    // 次のプレイヤーから順に調べ、打てるプレイヤーの手番にする（打てないプレイヤーはパス）
    int nextPlayer = ReversiRules.GetNextPlayer(turn.Player, PlayerCount);
    turn = ReversiRules.FindNextTurn(board, nextPlayer, PlayerCount);
}

// 4. 結果を表示する
Console.WriteLine(board);
foreach (ReversiPlayerResult playerResult in ReversiRules.GetPlayerResults(board, PlayerCount))
{
    Console.WriteLine($"プレイヤー{playerResult.Player}: {playerResult.StoneCount}個 {playerResult.Rank}位");
}
```

`FindNextTurn()` が返すプレイヤーは必ず打てる手を持つため、ループ内で `Search()` の結果が空になることはありません。

## 全体像

| 型 | 役割 |
| --- | --- |
| `ReversiBoard` | 盤面。各マスの値（空き・壁・プレイヤー番号）を持つ |
| `ReversiPosition` | マスの座標 |
| `ReversiRules` | ルール。合法手、石を置いて裏返す処理、手番・パス・終局の判定、結果の集計 |
| `ReversiStartCheck` / `ReversiStartError` | 対局を始められるかの結果と、始められない理由 |
| `ReversiTurn` | 次の手番（打つプレイヤー、手番の通し番号、パスしたプレイヤー、終局か） |
| `ReversiTurnOrder` | 手番の順番（二手連続など、通常と違う順番にする時に使う） |
| `ReversiPlayerResult` | プレイヤーごとの最終結果（石数と順位） |
| `ReversiCpuSearcher` | CPU の入口。盤面を渡すと、候補手をスコア付きで返す |
| `ReversiSearch` | フレームをまたいで少しずつ進める探索 |
| `ReversiCpuSettings` | CPU の設定（探索の深さ・時間の上限、評価の重み） |
| `ReversiSearchResult` / `ReversiMove` | CPU の探索結果と候補手 |

それぞれ型名と同じ名前のファイルに入っています（`ReversiStartError` だけは `ReversiStartCheck.cs` に入っています）。
表にないファイルは、CPU の探索などで使う内部のクラスです。

ルールは `ReversiBoard` だけに依存し、CPU のことは知りません。CPU は同じルールの実装を使って先読みします。

## 盤面

各マスには次のいずれかの値を入れます。

| 値 | 意味 |
| --- | --- |
| `ReversiBoard.Empty` (-1) | 空き |
| `ReversiBoard.Wall` (-2) | 壁（盤外と同じ扱い） |
| `0` 〜 `playerCount - 1` | プレイヤー番号 |

壁を置くことで、穴あき・十字型など自由な形の盤を表せます。

`ReversiBoard`

| メンバー | 内容 |
| --- | --- |
| `new ReversiBoard(width, height)` | 全マスが空きの盤面を作る |
| `Width` / `Height` | 盤の大きさ |
| `GetCell(x, y)` / `SetCell(x, y, value)` | マスの値を読む・書く |
| `FromRows(rows)` / `ToRows()` | `rows[y, x]` の 2 次元配列から作る・書き出す（セーブデータや通信などに使う） |
| `Parse(text)` / `ToString()` | 文字から作る・文字で表す（下記） |
| `Clone()` | 独立したコピー（Undo 用の履歴などに使う） |
| `CountCells(value)` | 指定した値のマスの数（石数・空きマスの数など） |
| `ContentEquals(other)` | 大きさと全てのマスの値が同じか。盤面は変更できるため、`Equals()` ではなくこちらで比較する |

2 次元配列から作る場合は `rows[y, x]` の順で指定します。

```csharp
const int E = ReversiBoard.Empty;
const int W = ReversiBoard.Wall;

var board = ReversiBoard.FromRows(new int[,]
{
    { W, E, E, E, E, W },
    { E, E, E, E, E, E },
    { E, E, 0, 1, E, E },
    { E, E, 1, 0, E, E },
    { E, E, E, E, E, E },
    { W, E, E, E, E, W },
});
```

文字で表す場合は、`.` = 空き、`#` = 壁、数字 = プレイヤー番号とし、マスを空白、行を改行で区切ります。
`ToString()` で書き出した文字は、`Parse()` でそのまま読み戻せます（10 以上のプレイヤー番号も扱えます）。
`Parse()` は空行と `\r` を無視し、読めない場合は `FormatException` を投げます。

```csharp
var board = ReversiBoard.Parse(@"
# . . . . #
. . . . . .
. . 0 1 . .
. . 1 0 . .
. . . . . .
# . . . . #");
```

マスの座標は `ReversiPosition`（`X`, `Y`）で表します。`X` と `Y` が同じなら同じ座標とみなすため、
`==` での比較や、`Contains()`・`Dictionary` のキーにそのまま使えます。座標は左上が `(0, 0)` です。

## ルール

- 対局は、全員が最初から打てる盤面でのみ始められます
- 自分の石で挟んだ間の石は、誰の石でも全て裏返します（3 人以上でも同じ）
- 手番は `player → player + 1 → … → playerCount - 1 → 0` の順に回ります（`ReversiTurnOrder` で変えられます。下記）
- 打てる手がないプレイヤーはパスし、全員が続けてパスしたら終局です

`ReversiRules`

| メソッド | 内容 |
| --- | --- |
| `CheckStart(board, playerCount)` | 対局を始められるか（下記） |
| `CanPlace(board, x, y, player)` | そのマスに打てるか |
| `GetLegalMoves(board, player)` | 打てるマスの一覧（左上から行ごとの順）。打てない場合は空 |
| `HasAnyMove(board, player)` | 打てるマスがあるか |
| `GetFlipPositions(board, x, y, player)` | 置いた時に裏返るマスの一覧（置く前に調べる） |
| `PlaceStone(board, x, y, player)` | 石を置いて裏返す。置けない場合は何もせず `false` |
| `PlaceStone(board, x, y, player, out flipPositions)` | 上と同じ。裏返したマスの一覧（`GetFlipPositions()` と同じ順）も返す。裏返す演出などに使う |
| `GetNextPlayer(player, playerCount)` | 次の手番のプレイヤー（パスは考慮しない） |
| `FindNextTurn(board, startPlayer, playerCount)` | `startPlayer` から手番の順に調べ、最初に打てるプレイヤーを返す（`startPlayer` 自身も調べる） |
| `FindNextTurn(board, startTurnNumber, turnOrder)` | 上と同じ。通し番号 `startTurnNumber` の手番から、`turnOrder` の順に調べる |
| `GetPlayerResults(board, playerCount)` | `[プレイヤー番号]` → 石数と順位 |

`ReversiStartCheck`（`CheckStart()` の結果）

| プロパティ | 内容 |
| --- | --- |
| `CanStart` | 始められる |
| `Error` | 始められない理由（`ReversiStartError`）。始められる場合は `None` |
| `PlayersWithoutMove` | `Error` が `PlayerCannotMove` の時、打てないプレイヤー（番号の小さい順） |

`ReversiStartError` は、次の順に調べて最初に見つかったものになります。

| 値 | 内容 |
| --- | --- |
| `InvalidPlayerCount` | 人数が 2 未満 |
| `InvalidStone` | `playerCount` 以上の番号の石がある |
| `PlayerCannotMove` | 最初から打てないプレイヤーがいる（石が 1 つもない場合も含む） |

`ReversiTurn`（`FindNextTurn()` の結果）

| プロパティ | 内容 |
| --- | --- |
| `Player` | 次に打つプレイヤー。全員打てない場合は `ReversiTurn.NoPlayer` |
| `TurnNumber` | 次に打つ手番の通し番号。打ったら `TurnNumber + 1` から次の手番を調べる。終局の場合は、最後にパスした手番の次の番号 |
| `IsGameOver` | 全員打てない（終局） |
| `PassedPlayers` | 打てる手がなく、パスになったプレイヤー（手番の順。順番によっては同じプレイヤーが何度も入る） |

`playerCount` を渡す `FindNextTurn()` では、`startPlayer` を通し番号とみなして数えます。

### 手番の順番を変える（ReversiTurnOrder）

二手連続など、通常と違う順番で手番を回す場合は、`ReversiTurnOrder` を作り、`FindNextTurn()` と CPU の探索に渡します。
CPU もその順番で先読みします。

順番は、手番の通し番号（対局の最初の手番を 0 とし、パスも 1 手番と数える）から、その手番に打つプレイヤーを返す関数で決めます。

```csharp
// 二手連続（0, 0, 1, 1, 0, 0, …）
var turnOrder = new ReversiTurnOrder(playerCount: 2, turnNumber => turnNumber / 2 % 2);

ReversiTurn turn = ReversiRules.FindNextTurn(board, startTurnNumber: 0, turnOrder);
while (!turn.IsGameOver)
{
    ReversiSearchResult result = ReversiCpuSearcher.Search(board, turn.TurnNumber, turnOrder, settings);
    ReversiMove move = result.Moves[0];
    ReversiRules.PlaceStone(board, move.X, move.Y, turn.Player);

    turn = ReversiRules.FindNextTurn(board, turn.TurnNumber + 1, turnOrder);
}
```

| メンバー | 内容 |
| --- | --- |
| `new ReversiTurnOrder(playerCount, getPlayer)` | 通し番号 → プレイヤー番号の関数で順番を決める |
| `ReversiTurnOrder.Rotation(playerCount)` | 通常の順番（通し番号 `n` の手番は、プレイヤー `n % playerCount`） |
| `PlayerCount` | 人数 |
| `GetPlayer(turnNumber)` | 通し番号 `turnNumber` の手番に打つプレイヤー |

- `getPlayer` は探索中に何度も呼ばれます。同じ通し番号には常に同じプレイヤーを返す、軽い処理にしてください
- 打てる手があるプレイヤーには、いずれ必ず手番が回ってくる順番にしてください
- パスした時に手番を飛ばすかどうかも、通し番号で決まります（二手連続で 1 手目がパスなら、2 手目も同じプレイヤーの手番です）
- 決められるのは、通し番号で決まる順番だけです。盤面の状態で手番が変わるルール（角を取ったらもう一手、など）は表せません

`ReversiPlayerResult`（`GetPlayerResults()` の結果）

| プロパティ | 内容 |
| --- | --- |
| `Player` | プレイヤー番号 |
| `StoneCount` | 石数 |
| `Rank` | 自分より石の多いプレイヤーの数 + 1（同数の場合は同じ順位） |

勝敗の扱い（1 位が複数いる場合など）は、ゲームごとに決めてください。

### エラーの扱い

| 種類 | 例 | 扱い |
| --- | --- | --- |
| 盤面・ルールとして成り立たない | 人数が足りない、範囲外の番号の石がある、最初から打てない人がいる | `CheckStart()` が理由を返す |
| 呼び出し方の誤り | `null` を渡す | `ArgumentNullException` |
| | 盤外の座標、盤の大きさが 0 以下、`SetCell()` に範囲外の値を渡す | `ArgumentOutOfRangeException` |
| | `FindNextTurn()` / CPU の探索に、2 未満の人数や範囲外のプレイヤー番号、負の通し番号を渡す | `ArgumentOutOfRangeException` |
| | `ReversiTurnOrder` の関数が範囲外のプレイヤー番号を返す、打てるプレイヤーに手番が回ってこない | `InvalidOperationException` |
| | CPU に人数以上の番号の石がある盤面を渡す、`CustomPositionWeights` の大きさが盤と違う | `ArgumentException` |
| | `Parse()` に読めない文字を渡す | `FormatException` |

対局を始める前に `CheckStart()` で確認しておけば、`FindNextTurn()` や CPU の探索が例外を投げることはありません
（`ReversiTurnOrder` の関数が正しい場合）。

人数を受け取らないメソッド（`CanPlace()` / `GetLegalMoves()` / `HasAnyMove()` / `GetFlipPositions()` / `PlaceStone()`）は、
プレイヤー番号が範囲内かを確認しません。盤面にないプレイヤー番号を渡した場合は、打てる手がないものとして扱います
（`false` や空の一覧を返し、盤面は変わりません）。

## CPU

`ReversiCpuSearcher.Search()` に盤面と手番のプレイヤーを渡すと、候補手をスコア付きで返します。
実際に石を置く処理や手番の判定は `ReversiRules` で行います。

```csharp
ReversiSearchResult result = ReversiCpuSearcher.Search(board, player: 0, playerCount: 2, settings);
if (result.Moves.Count == 0)
{
    // 打てる手がない（パス）
}
ReversiMove bestMove = result.Moves[0]; // スコアの高い順に並んでいる
```

`settings` は省略できます（省略時は既定値を使います）。

`playerCount` の代わりに `ReversiTurnOrder` と手番の通し番号を渡すと、その順番で先読みします
（`Search(board, turnNumber, turnOrder, settings)`。`StartSearch()` も同じ）。

### 結果

`ReversiSearchResult`

| プロパティ | 内容 |
| --- | --- |
| `Moves` | 候補手（スコアの高い順）。空ならパス |
| `CompletedDepth` | 最後まで読み切れた深さ |
| `NodeCount` | 探索した局面数 |
| `ElapsedMilliseconds` | 探索にかかった時間（ミリ秒） |

`ReversiMove`

| プロパティ | 内容 |
| --- | --- |
| `X`, `Y` | 打つマスの座標 |
| `Position` | 打つマスの座標（`ReversiPosition`）。`GetLegalMoves()` などの結果と比べる時に使う |
| `Score` | 0〜100。50 が互角、高いほど有利。勝ち確定は 95 以上、負け確定は 5 以下 |

スコアは盤面の有利さを表す絶対的な値のため、候補手同士の差が小さいこともあります。

### フレームをまたいで少しずつ探索する

`Search()` は探索が終わるまで処理が止まります。探索中も画面を止めたくない場合は、`StartSearch()` で探索を始め、
毎フレーム `Step()` を呼んで少しずつ進めます。スレッドを使わないため、WebGL でも動作します。

```csharp
ReversiSearch search = ReversiCpuSearcher.StartSearch(board, player: 0, playerCount: 2, settings);
while (!search.Step(budgetMilliseconds: 5)) // 1 回の呼び出しで、およそ 5 ミリ秒だけ探索を進める
{
    await UniTask.Yield(); // 次のフレームへ（コルーチンなら yield return null）
}
ReversiSearchResult result = search.Result;
```

- 結果は `Search()` と同じになります
- 盤面は `StartSearch()` の時点でコピーするため、探索中に `board` を変更しても結果には影響しません
- 時間の確認は何局面かごとに行うため、`budgetMilliseconds` を少し超えることがあります
- `TimeLimitMilliseconds` と `ElapsedMilliseconds` は、`Step()` を呼んでいる間の時間の合計です（フレームの待ち時間は含みません）
- `RequestStop()` を呼ぶと、次の `Step()` で、最後まで読み切れた深さの結果を返して終わります（1 手先を読んでいる途中なら、読み終わるまで続きます）。
  フレームの待ち時間を含めた、実際の経過時間で思考時間を制限したい場合に使います

### 設定（ReversiCpuSettings）

`[Serializable]` なので、Unity では MonoBehaviour の `[SerializeField]` に持たせれば Inspector から調整できます。

```csharp
[SerializeField] private ReversiCpuSettings _cpuSettings = new();
```

#### 計算量

| 項目 | 既定値 | 内容 |
| --- | --- | --- |
| `CandidateCount` | 5 | 返す候補手の数。0 以下で全ての合法手 |
| `MaxSearchDepth` | 4 | 何手先まで読むか（他のプレイヤーの手も 1 手と数える） |
| `MaxNodeCount` | 20000 | 探索する局面数の上限。0 以下で無制限 |
| `TimeLimitMilliseconds` | 0 | 探索時間の上限（ミリ秒）。0 以下で無制限 |
| `ExactSearchEmptyCount` | 8 | 空きマスがこの数以下になったら、終局まで読み切る |

1 手先から順に深く読み、上限のどれかに達したら、最後まで読み切れた深さの結果を返します（1 手先は必ず読みます）。
実際の計算量は `CompletedDepth` / `NodeCount` / `ElapsedMilliseconds` で確認して調整してください。

WebGL はシングルスレッドのため、`Search()` では探索中に処理が止まります。`StartSearch()` / `Step()` で少しずつ探索し、
思考時間が長くなりすぎないよう `TimeLimitMilliseconds` を併用するのがおすすめです。

参考値（Mac / .NET 10 の Release ビルド、初期配置、`MaxNodeCount = 0`、5 回計測した中で最短）。
WebGL ではこの数倍遅くなる想定です。

| 盤 | 人数 | 深さ | 時間 | 局面数 |
| --- | --- | --- | --- | --- |
| 8x8 | 2 人 | 4 | 約 1ms | 約 300 |
| 8x8 | 2 人 | 6 | 約 10ms | 約 3,800 |
| 10x10 | 3 人 | 6 | 約 62ms | 約 88,000 |

10x10・3 人の初期配置は、中央の 3x3 マスに各プレイヤーの石を 3 つずつ、斜めに並べたものです。

#### 評価

盤の埋まり具合に応じて、序盤（Opening）の比率から終盤（Ending）の比率へ変化します。
比率は合計が 1 でなくても構いません（合計で割って使います）。

| 項目 | 序盤 | 終盤 | 内容 |
| --- | --- | --- | --- |
| `*PositionWeight` | 0.5 | 0.2 | 隅や辺などのマスの重み |
| `*MobilityWeight` | 0.4 | 0.1 | 打てる手の多さ |
| `*StoneWeight` | 0.1 | 0.7 | 石の数 |

#### マスの重み

盤の形から、隅・辺・それ以外を自動で判定します。

| 項目 | 既定値 | 内容 |
| --- | --- | --- |
| `CornerWeight` | 100 | 隅（縦・横・斜めの全方向で、片側が盤外・壁のマス。二度と裏返らない） |
| `EdgeWeight` | 10 | 辺（いずれかの方向で、片側が盤外・壁のマス） |
| `InnerWeight` | 0 | それ以外 |
| `CornerNeighborWeight` | -30 | 隅が空いている時の、隅の隣のマス |
| `CustomPositionWeights` | null | マスの重みを `rows[y, x]` で直接指定する（指定時は自動判定を使わない。Inspector 非対応） |

### 探索の仕組み

- 1 手先から順に深く読む反復深化で探索します
- 自分以外の全員が、自分にとって最も不利な手を打つと仮定して読みます（Paranoid 探索）
  - 2 人の場合は通常の αβ 探索と同じです
  - 3 人以上では、やや慎重（悲観的）な読みになります
- 3 人以上の評価は、相手全員の平均と比較します
- 終局時は、一番石の多い相手より多ければ勝ち、同じなら引き分けとして評価します

## よくある使い方

### 人が打つ・裏返す演出をする

```csharp
// 打てるマスを表示する
IReadOnlyList<ReversiPosition> legalMoves = ReversiRules.GetLegalMoves(board, player);

// クリックされたマスに打つ。裏返したマスの一覧を使って演出する
if (ReversiRules.PlaceStone(board, x, y, player, out IReadOnlyList<ReversiPosition> flipPositions))
{
    foreach (ReversiPosition position in flipPositions)
    {
        // position の石を裏返す演出
    }
}
```

### Undo する

ライブラリは履歴を持たないため、打つ前の盤面を `Clone()` して積んでおきます。
手番などの盤面以外の状態は、ゲーム側で一緒に保存してください。

```csharp
var history = new Stack<ReversiBoard>();

history.Push(board.Clone()); // 打つ前に積む
ReversiRules.PlaceStone(board, x, y, player);

board = history.Pop(); // 1 手戻す
```

### セーブ・ロードする

```csharp
int[,] rows = board.ToRows();                  // 保存する
ReversiBoard loaded = ReversiBoard.FromRows(rows); // 読み込む
```

### CPU の強さを調整する

候補手のうち、どれを選ぶかで強さを調整します。読む深さを浅くするのも有効です。

```csharp
var settings = new ReversiCpuSettings { CandidateCount = 3, MaxSearchDepth = 2 };
ReversiSearchResult result = ReversiCpuSearcher.Search(board, player, playerCount, settings);

// 上位 3 手からランダムに選ぶ（弱めの CPU）
ReversiMove move = result.Moves[random.Next(result.Moves.Count)];
```

### ヒント・形勢を表示する

CPU の手番以外でも、`Search()` に人間のプレイヤーを渡せば、そのプレイヤーにとっての候補手を調べられます。

```csharp
// ヒント：人間のプレイヤーにとって良い手（スコアの高い順）
ReversiSearchResult hint = ReversiCpuSearcher.Search(board, humanPlayer, playerCount, settings);

// 形勢：最善手のスコアを、そのプレイヤーから見た盤面の評価として使う（50 が互角）
// 打てる手がない（パスの）場合は、候補手がないため評価できない
float? evaluation = hint.Moves.Count > 0 ? hint.Moves[0].Score : null;
```

全ての合法手のスコアを知りたい場合は、`CandidateCount` を 0 以下にします。

### 別スレッドで探索する

スレッドを使える環境（WebGL 以外）では、`Task.Run()` などで別スレッドから `Search()` を呼べます。
ライブラリは共有の状態（変更される static フィールド）を持たないため、複数の探索を同時に動かすこともできます。

```csharp
ReversiBoard boardCopy = board.Clone();
ReversiSearchResult result = await Task.Run(
    () => ReversiCpuSearcher.Search(boardCopy, player, playerCount, settings)
);
```

- 盤面は探索の開始時にコピーされますが、別スレッドでは開始のタイミングがわからないため、`Clone()` したものを渡してください
- `settings` は探索中も参照するため、探索中に値を変更しないでください
- 1 つの `ReversiBoard` を、複数のスレッドから同時に変更しないでください

## 制限事項

- 対局の進行管理（履歴・Undo・棋譜）は持ちません。上記のように `Clone()` などを使ってゲーム側で管理してください
- ルールの変化形（打てる手があってもパスできる、特定のプレイヤーの石だけ裏返す、など）には対応していません
- CPU の評価は、マスの重み・打てる手の多さ・石数による簡易なものです。定石や学習済みの評価は持ちません
- 3 人以上の対戦では、CPU は相手全員が自分に不利な手を打つと仮定して読むため、最善の手になるとは限りません
- コード内のコメントは日本語です

## テスト

`Tests/` に NUnit のテストがあります。ライブラリ本体はテストに依存しないため、不要であればコピーしなくて構いません。

テストは内部クラスも参照するため、テストを含むアセンブリの名前を `Zizo.Reversi.Tests` にしてください
（`AssemblyInfo.cs` の `InternalsVisibleTo` で指定しています。別の名前にする場合は、こちらも合わせて変更してください）。

- **Unity**: `Tests/` もコピーし、ライブラリ本体のフォルダに Assembly Definition を作成します
  （テストのアセンブリから参照するために必要です）。次に `Tests/` に `Zizo.Reversi.Tests` という名前の EditMode 用
  Assembly Definition を作成し、ライブラリ本体の Assembly Definition と NUnit（Test Framework）を参照すると、
  Test Runner ウィンドウから実行できます。
- **dotnet**: リポジトリの直下に、次のようなテストプロジェクトを作成して `dotnet test` で実行できます
  （`TargetFramework` は環境に合わせてください）。テストは NUnit 3 の書き方のため、NUnit は 3.x を使ってください。

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <AssemblyName>Zizo.Reversi.Tests</AssemblyName>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
    <PackageReference Include="NUnit" Version="3.14.0" />
    <PackageReference Include="NUnit3TestAdapter" Version="4.5.0" />
  </ItemGroup>
</Project>
```

テストでは `Tests/ReversiTestHelper` で、文字列から盤面を作ります（`.` = 空き、`#` = 壁、数字 = プレイヤー番号）。

```csharp
ReversiBoard board = ReversiTestHelper.CreateBoard(
    "#..#",
    ".01.",
    ".10.",
    "#..#"
);
```

## ライセンス

[MIT License](LICENSE)

---

## English summary

A rule engine and CPU player for Reversi, written in plain C# with no dependency on UnityEngine.
It covers the board, legal moves, placing and flipping stones, turn/pass/game-over detection, final stone counts and
ranks, and a CPU player that returns scored candidate moves.

- Boards of any shape (a rectangular grid with walls) and games with 3 or more players are supported.
- Custom turn orders (e.g. two moves in a row) can be given with `ReversiTurnOrder`; the CPU searches with the same order.
- The rules (`ReversiRules`) do not depend on the CPU, so they can be used on their own.
- The CPU uses iterative deepening with Paranoid search (equivalent to alpha-beta for 2 players). It can also search
  incrementally across frames with `StartSearch()` / `Step()` (no threads, so it works on WebGL).
- Building the starting board, rendering/input, game history/undo, and choosing a move by difficulty are left to the game.
- Requires C# 9.0 and .NET Standard 2.1 APIs. Works in Unity 2021.2+ as well.
- Distributed as plain source files: copy the `*.cs` files except `Tests/` into your project. Namespace: `Zizo.Reversi`.
- See [Quick start](#クイックスタート) for a complete example. In-code comments and this README are written in Japanese.
- Licensed under the [MIT License](LICENSE).
