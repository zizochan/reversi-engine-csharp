using System.Runtime.CompilerServices;

// 内部クラス（探索の処理など）もテストするため、テストのアセンブリに internal を公開する
// テストを別の名前のアセンブリで実行する場合は、ここも合わせて変更する
[assembly: InternalsVisibleTo("Zizo.Reversi.Tests")]
