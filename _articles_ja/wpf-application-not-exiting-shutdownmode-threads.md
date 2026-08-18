---
layout: article-ja
title: "WPF でウィンドウを閉じてもプロセスが終了しない原因の切り分けと ShutdownMode・フォアグラウンドスレッドの扱い"
date: 2026-08-18
category: WPF
excerpt: "ウィンドウを閉じたのにタスクマネージャーにプロセスが残る。WPF の終了は「アプリケーションの終了」と「プロセスの終了」の 2 段階であり、どちらで止まったかによって原因も対処も変わる。実測は .NET 10 で取得した。"
---

## 概要

デバッグ実行を止め忘れたまま再ビルドしようとして、出力ファイルがロックされていることに気付く。
タスクマネージャーを開くと、閉じたはずのアプリケーションのプロセスがウィンドウを持たないまま残っている。
WPF アプリケーションの開発で頻繁に起きる状況である。

この症状は 1 つの原因から起きるわけではない。
WPF の終了処理には**「アプリケーションが終了する」段階**と**「プロセスが終了する」段階**という性質の異なる 2 つの関門があり、どちらで止まったかによって原因も対処もまったく異なる。
`ShutdownMode` を変えても直らない、`Application.Current.Shutdown()` を呼んでも残る、といった状況の多くは、止まっている関門を取り違えたことによる。

本記事では、この 2 段階を切り分けるための判定基準を示し、代表的なパターンごとにプロセスの寿命を計測した結果を掲載する。
掲載した値は、すべて後述の環境で実際に動かして得たものである。

---

## 前提・対象環境

- フレームワーク: .NET 6 以降 / WPF（実測はすべて .NET 10 / Windows 11 で取得）
- 言語: C#（掲載するコードは C# 7.0 以降で動作する構文のみを使う）
- 対象機能: `Application.ShutdownMode`、`Application.Windows`、`System.Threading.Thread`、`Dispatcher`
- アーキテクチャ: 単体で動作するデスクトップアプリケーション（MVVM・コードビハインドのいずれでも同じ）
- 診断ツール: `dotnet-stack`（`dotnet tool install --global dotnet-stack` で別途インストールする）
- 計測方法: 明示的な `Main` を持つ検証用アプリケーションで、ウィンドウを一定時間後に自動で閉じ、プロセスの生存時間と各イベントの発生時刻をファイルへ記録した

掲載するコードは記事の主題に関わる部分だけを抜き出している。
`Trace` は `System.Diagnostics`、`Thread` と `ManualResetEventSlim` と `ApartmentState` は `System.Threading`、`Application` と `Window` と `ExitEventArgs` は `System.Windows`、`Dispatcher` は `System.Windows.Threading` にある。

---

## 問題

ウィンドウを閉じる操作は成功し、画面上からウィンドウは消える。
しかしプロセスは残り続け、次のいずれかの形で表面化する。

- Visual Studio のデバッグセッションが終わらず、再ビルドが出力ファイルのロックで失敗する。
- タスクマネージャーの「プロセス」タブに、ウィンドウを持たないエントリが残る。
- アプリケーションを起動し直すと、多重起動チェックが前回のプロセスを検出して起動できない。

紛らわしいのは、この症状が**2 種類のまったく異なる状態を同じ見た目で見せる**点である。
一方は WPF のアプリケーション自体がまだ生きている状態、もう一方は WPF は正常に終了したのにプロセスだけが残っている状態である。
前者では `Application.Exit` イベントが発生しないが、後者では発生する。
この違いを見ずに `ShutdownMode` だけを変更しても、後者は直らない。

---

## 原因・背景

プロセスが消えるまでには、直列につながった 2 つの関門がある。

<figure class="article-figure article-figure--wide">
  <img src="/images/articles/wpf-application-not-exiting-shutdownmode-threads/shutdown-two-gates.svg" alt="WPF アプリケーションの終了が 2 段階であることを示す図。第 1 関門では、最後のウィンドウが閉じられて ShutdownMode の条件を満たすと Application.Shutdown が呼ばれ、Application.Exit が発生して Run が戻る。呼ばれない場合はウィンドウが無いままアプリケーションが動き続け、その原因としてウィンドウを 1 つも生成していないことと、閉じていないウィンドウのインスタンスが残っていることが挙げられている。第 2 関門では、Run から戻ったあと、メインスレッドを含むフォアグラウンドスレッドがすべて終了するとプロセスが終了する。残っている場合はプロセスがタスクマネージャーに残り、その原因として new Thread と 2 つ目の UI スレッド上の Dispatcher.Run が挙げられている。" width="880" height="394" loading="lazy">
  <figcaption>WPF アプリケーションの終了を構成する 2 つの関門と、それぞれを通過しなかったときの症状。各関門で止まる条件は .NET 10 / Windows 11 上の検証アプリで確認した。</figcaption>
</figure>

### 第 1 関門: WPF アプリケーションの終了

WPF は `Application.Shutdown` が呼ばれたときにアプリケーションを終了する。
`ShutdownMode` は、この `Shutdown` が暗黙に呼ばれる条件を決める設定である。
既定値の `OnLastWindowClose` では、**最後のウィンドウが閉じられたとき**に `Shutdown` が暗黙に呼ばれる（[Application.ShutdownMode プロパティ](https://learn.microsoft.com/dotnet/api/system.windows.application.shutdownmode)）。

ここで働く条件は 2 つある。
ウィンドウが閉じられたことが**きっかけ**であり、その時点で残るウィンドウが無いことが**成立条件**である。
きっかけの側が欠けても成立しない点は見落としやすく、ウィンドウを一度も生成しないまま `Application.Run()` を呼ぶと、`Application.Windows` は空でありながら閉じるイベントも起きないため、`OnLastWindowClose` のままではアプリケーションは終了しない（実測で確認）。

成立条件の側で問題になるのは、「残るウィンドウ」が画面に見えているウィンドウとは限らないことである。
`Window` のインスタンスは、UI スレッド上で**生成された時点で** `Application.Windows` へ追加され、可視かどうかは問われない。
削除されるのは `Closing` イベントの処理が終わってから `Closed` イベントが発生するまでの間である（[Application.Windows プロパティ](https://learn.microsoft.com/dotnet/api/system.windows.application.windows)）。

このため、生成しただけで一度も閉じていない `Window` が 1 つでも残っていると、可視ウィンドウを閉じても `Application.Windows` は空にならず、暗黙の `Shutdown` は起こらない。
画面には何も出ていないのにアプリケーションだけが動き続ける、という状態がここで生まれる。

なお、`ShutdownMode` の値に関わらず、ユーザーがログオフ・シャットダウン・再起動などで Windows セッションを終了し、`SessionEnding` が未処理またはキャンセルされずに処理された場合にも `Shutdown` は呼ばれる（[Application.Shutdown メソッド](https://learn.microsoft.com/dotnet/api/system.windows.application.shutdown)）。

### 第 2 関門: プロセスの終了

第 1 関門を通過すると `Application.Exit` が発生し、`Application.Run` が戻る。
`Run` が戻ることと `Main` が終わることは同じではない。
`Run` の後に後片付けなどを書いていれば、その処理はメインのフォアグラウンドスレッド上で続き、その間プロセスは終了しない。
`Main` が終わったとしても、なおプロセスが終わるとは限らない。

強制終了や未処理例外による終了を別にすれば、マネージドプロセスが終了するのは**フォアグラウンドスレッドがすべて停止したとき**である。
バックグラウンドスレッドはマネージド実行環境を生かし続けず、フォアグラウンドスレッドが尽きた時点でランタイムに停止させられる（[フォアグラウンド スレッドとバックグラウンド スレッド](https://learn.microsoft.com/dotnet/standard/threading/foreground-and-background-threads)）。

区別の要点は生成方法にある。

- `new Thread(...)` で作ったスレッドは、既定で**フォアグラウンド**である。
- スレッドプールのスレッドは**バックグラウンド**である。
  `Task.Run` の処理、`System.Threading.Timer` のコールバック、`SynchronizingObject` を設定していない `System.Timers.Timer` のコールバックがこれに当たる。

このため「非同期処理を止め忘れた」ことが原因に見えても、`Task.Run` に渡した処理はプロセスの終了を妨げない。
プロセスを残すのは `new Thread(...)` で作って `IsBackground` を設定していないスレッドの方である。

2 つ目の UI スレッドを立てる構成は、この 2 つが重なる典型例である。
`new Thread(...)` はフォアグラウンドであり、そのスレッドで呼ぶ `Dispatcher.Run()` は、そのディスパッチャーがシャットダウンされるまで戻らない。
しかも、ワーカースレッド上で生成した `Window` は `Application.Windows` に追加されないため、第 1 関門は素通りする。
結果として、`Application.Exit` は正常に発生するのにプロセスは残り続ける。

### 実測: 条件別の到達段階

同一の検証用アプリケーションで条件だけを変え、`Application.Exit` の発生・`Application.Run` の戻り・プロセスの終了を計測した。
ウィンドウを生成する条件では、起動から約 2 秒後に可視ウィンドウを 1 つ自動で閉じている（生成だけして閉じないウィンドウは、そのまま残す）。
バックグラウンド処理を伴う条件では、起動直後に開始して 6 秒間スリープさせた。

| 条件 | `Application.Exit` | `Run()` の戻り | プロセスの終了 |
|---|---|---|---|
| 可視ウィンドウのみ（対照） | 発生する | 戻る | ウィンドウを閉じた直後 |
| ウィンドウを 1 つも生成せずに `Run()` を呼ぶ | **発生しない** | 戻らない | 終了しない |
| 生成だけして閉じない `Window` が 1 つある | **発生しない** | 戻らない | 終了しない |
| `new Thread(...)`（既定のまま） | 発生する | 戻る | **残りのスリープ時間が尽きた時点（約 4 秒後）** |
| `Task.Run(...)` | 発生する | 戻る | 直後（処理は打ち切られる） |
| 2 つ目の UI スレッドで `Dispatcher.Run()` | 発生する | 戻る | **終了しない** |
| 同上 ＋ `Exit` で `InvokeShutdown()` | 発生する | 戻る | 直後 |

計測に使った 6 秒のスリープは、実際の障害で問題になる「止め忘れて終わらない処理」を有限時間に置き換えたものである。
そのため `new Thread(...)` の行はプロセスが約 4 秒後に終了しているが、実際に終わらないループを回していれば、そのままプロセスは残り続ける。

この表から、`Application.Exit` の有無が関門の判別にそのまま使えることが分かる。
「発生しない」は、ウィンドウを 1 つも生成しない条件と、生成だけして閉じないウィンドウがある条件の 2 つだけであり、これが第 1 関門で止まっているパターンである。
プロセスの終了を妨げる残り 2 パターン、すなわち `new Thread(...)` と 2 つ目の UI スレッドでは `Application.Exit` が正常に発生しており、`ShutdownMode` をどう設定しても解決しない。

`Task.Run(...)` の行は、原因の候補から外してよいことを示している。
6 秒のスリープを抱えたままでもプロセスは直後に終了しており、処理は完了を待たずに打ち切られている。

---

## 解決方法

最初に行うのは修正ではなく切り分けである。
前掲の実測が示すとおり、UI スレッドが応答している限り、**`Application.Exit` が発生するかどうか**だけで止まっている関門が確定する。
UI スレッド自体がブロックされている場合の扱いは注意点で述べる。

- 発生しない → 第 1 関門。
  `Application.Windows` に残っているウィンドウ、または `ShutdownMode` の設定が原因である。
- 発生するのにプロセスが残る → 第 2 関門。
  終了していないフォアグラウンドスレッドが原因である。

第 1 関門であれば、残っているウィンドウを `Application.Windows` から特定して閉じるか、そのウィンドウを生成しない構成に変える。
タスクトレイ常駐アプリケーションのように意図的にウィンドウを持たない構成では、`ShutdownMode` を `OnExplicitShutdown` にしたうえで、終了メニューから `Shutdown()` を明示的に呼ぶ。

第 2 関門であれば、残っているスレッドを特定して、打ち切ってよい監視・ポーリング用途なら `IsBackground` を `true` にする。
完了させる必要がある処理なら、フォアグラウンドスレッドのまま残し、キャンセルを通知して `Join` で待ち合わせる。
この形が完了を保証できるのは、スレッドがキャンセルに確実に応答することが前提であり、応答しなければプロセスは終了しない。
`IsBackground = true` はその前提を諦める選択であり、そのスレッドがプロセスの終了を妨げなくなる代わりに、完了は保証されない（詳細は代替案・比較で述べる）。
2 つ目の UI スレッドを立てている場合は、そのスレッドの `Dispatcher` を `InvokeShutdown()` で止める。

---

## 実装例

切り分け用のログは、`App` クラスの `OnExit` をオーバーライドして仕込む。
第 1 関門で止まっている場合、このメソッドは呼ばれない。
呼ばれた場合は第 1 関門を通過しており、残っているのはスレッドである。

```csharp
public partial class App : Application
{
    protected override void OnExit(ExitEventArgs e)
    {
        // ここへ到達すれば第 1 関門は通過している。
        // それでもプロセスが残るなら、原因はフォアグラウンドスレッドである。
        Trace.WriteLine($"OnExit: code={e.ApplicationExitCode}");
        base.OnExit(e);
    }
}
```

`Debug.WriteLine` ではなく `Trace.WriteLine` を使っているのは、Release 構成でも記録を残すためである。
`Debug` クラスの呼び出しは `DEBUG` 定数が定義されていない構成ではコンパイル時に除去される。
`Trace` クラスの呼び出しも `TRACE` 定数に依存するが、既定のままであれば、SDK スタイルの C# プロジェクトは構成に関わらず `DefineConstants` へ `TRACE` を追加するため、Release 構成でも定義される（[Trace クラス](https://learn.microsoft.com/dotnet/api/system.diagnostics.trace)）。
出力先は、デバッガーをアタッチしていれば Visual Studio の出力ウィンドウである。
アタッチせずに動かす配布版でファイルへ残したい場合は、`TextWriterTraceListener` などを `Trace.Listeners` に追加する。
出力ウィンドウを診断に使う手順は[WPF バインディングエラーの読み方と出力ウィンドウを使った原因特定](/ja/articles/wpf-binding-error-debugging-output-window/)で扱っている。

`OnExit` の到達時点では WPF が残存ウィンドウを閉じ終えているため、ここで `Windows.Count` を出力しても常に 0 になる。
残っているウィンドウを調べるのは、`OnExit` ではなく終了操作の直後である。
表示されていないウィンドウも列挙に含まれるため、`IsVisible` を併せて出力すると原因のインスタンスを特定できる。

```csharp
private static void DumpOpenWindows()
{
    foreach (Window window in Application.Current.Windows)
    {
        Trace.WriteLine($"{window.GetType().Name} visible={window.IsVisible} title={window.Title}");
    }
}
```

この列挙にはいくつか制約がある（詳細は注意点で述べる）。

第 2 関門で止まっている場合、残っているスレッドの特定には `dotnet-stack` が使える。
プロセスの全スレッドのマネージドスタックを出力するため、どのメソッドで止まっているかがそのまま分かる。

```text
dotnet-stack ps
dotnet-stack report --process-id 12345
```

Visual Studio でデバッグ実行中であれば、「デバッグ」→「ウィンドウ」→「スレッド」でも同じ情報を確認できる。
ただし、どちらの手段もスレッドがフォアグラウンドかバックグラウンドかは示さない。
止まっている位置を特定したうえで、そのスレッドの生成箇所を確認して `IsBackground` の設定を判断する。

スレッドを特定できたら、用途に応じて次のいずれかへ書き換える。
監視のように途中で打ち切ってよい処理は、`IsBackground` を `true` にするだけでよい。
`MonitorLoop` は引数を取らないため、`Thread` のコンストラクターは `ThreadStart` に解決される。

```csharp
private void StartMonitor()
{
    var monitor = new Thread(MonitorLoop) { IsBackground = true };
    monitor.Start();
}

private void MonitorLoop()
{
    // 監視ループの本体は省略する。
}
```

`IsBackground` を `true` にしたスレッドは、プロセス終了時に例外を発生させずに停止させられる。
書き込み途中のファイルをフラッシュするような処理をここに置く場合、完了は保証されない。

2 つ目の UI スレッドを立てている場合は、`Dispatcher.Run()` を明示的に終わらせる必要がある。
次は `App` クラスに置くことを想定したメソッドで、アプリケーションの終了に合わせて `InvokeShutdown()` を呼ぶ。

```csharp
private void StartSubUiThread()
{
    Dispatcher subDispatcher = null;

    var ready = new ManualResetEventSlim();

    var thread = new Thread(() =>
    {
        subDispatcher = Dispatcher.CurrentDispatcher;
        ready.Set();
        try
        {
            // 実際にはこのスレッド上でウィンドウを生成・表示する。
            Dispatcher.Run();
        }
        finally
        {
            // Dispatcher.Run() が戻る時点では待ち側の Wait() は完了しており、
            // Set() も抜けている。Dispose はスレッドセーフではないため、
            // 競合しないこの位置で破棄する。
            ready.Dispose();
        }
    });
    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();
    ready.Wait();

    Exit += (sender, e) => subDispatcher.InvokeShutdown();
}
```

`ready.Wait()` は、`subDispatcher` への代入が完了してから読み出すことを保証するために置いている。
`InvokeShutdown()` はそのディスパッチャーのメッセージループを終了させ、`Dispatcher.Run()` が戻る。
スレッドがフォアグラウンドのままでも、`Run()` から抜けてスレッドが終了すれば第 2 関門は通過する。
nullable 参照型を有効にしているプロジェクトでは、`Dispatcher? subDispatcher = null;` と宣言したうえで、`Exit` のハンドラー側でも `subDispatcher?.InvokeShutdown();` と書く。
宣言だけを変えると、ハンドラー側で null 参照の可能性が警告として残る。

---

## 注意点

- **`Environment.Exit` での強制終了は最後の手段とする。**
  プロセスは確実に終了するが、`Application.Exit` イベントは発生せず、そこへ置いた保存処理は実行されない。
  原因を特定するまでの暫定処置として使う場合は、その旨をコードに残す。
- **`OnExit` をオーバーライドするなら `base.OnExit(e)` を呼ぶ。**
  基底の実装が `Exit` イベントを発生させるため、これを省くとイベントハンドラー側の処理が動かなくなる。
- **`Application.Exit` の有無で原因まで絞り込めるのは、UI スレッドが応答している場合に限る。**
  UI スレッドがデッドロックや長時間の同期待ちでブロックされている場合も `OnExit` は呼ばれない。
  止まっているのは第 1 関門だが、原因は残存ウィンドウでも `ShutdownMode` でもないため、`dotnet-stack report` で UI スレッドの停止位置を先に確認する。
- **モーダル表示は UI スレッドのブロックではない。**
  `Window.ShowDialog()` や `MessageBox.Show` の表示中は入れ子の `Dispatcher` フレームが回っており、UI スレッドはメッセージを処理し続けている。
  `Window.ShowDialog()` で開いたものは `Application.Windows` に残っているだけであり、通常の残存ウィンドウとして扱える。
  一方、`MessageBox` と、`OpenFileDialog` などの `CommonDialog` 派生は `Window` を継承しないため、この列挙には現れない。
- **`OnMainWindowClose` は最初に生成されたウィンドウを基準にする。**
  `MainWindow` は最初にインスタンス化された `Window` が自動的に設定される（[メイン アプリケーション ウィンドウの取得と設定](https://learn.microsoft.com/dotnet/desktop/wpf/windows/how-to-get-set-main-application-window)）。
  スプラッシュウィンドウを先に生成する構成では、そのスプラッシュが `MainWindow` になる。
  この設定を使うなら `MainWindow` を明示的に代入する。
- **`OnExplicitShutdown` は終了漏れをそのままプロセス残留に変える。**
  トレイ常駐アプリケーションでは必要な設定だが、終了経路が 1 つでも `Shutdown()` を呼び忘れると、ウィンドウが無いまま動き続ける。
  トレイアイコンの実装で発生する別の落とし穴は[WPF タスクトレイの ContextMenu がフォーカス移動で閉じない問題の解消方法](/ja/articles/wpf-tray-contextmenu-close-on-focus-loss/)で扱っている。
- **`IsBackground = true` は打ち切ってよいという宣言である。**
  ランタイムはプロセス終了時にこのスレッドを例外なしで停止させる。
  ログのフラッシュや一時ファイルの後始末をこのスレッドに任せている場合、その処理は実行されないことがある。
- **ワーカースレッド上で生成したウィンドウは `Application.Windows` に現れない。**
  第 1 関門の調査でこの列挙だけを見ていると、2 つ目の UI スレッドが持つウィンドウを見落とす。
- **`Application.Windows` の参照は `Application` を生成したスレッドからのみ可能である。**
  他スレッドから診断用に読み出す場合は `Dispatcher.Invoke` を経由する。
  スレッド親和性が関わる別の症状は[WPF で ObservableCollection をバックグラウンドスレッドから更新するとクロススレッド例外が発生する問題の解決方法](/ja/articles/wpf-observablecollection-cross-thread-update/)で扱っている。

---

## 代替案・比較

### ShutdownMode の選択

| 設定値 | `Shutdown()` が暗黙に呼ばれる条件 | 適するケース | 注意点 |
|---|---|---|---|
| `OnLastWindowClose`（既定） | 最後のウィンドウが閉じられたとき | 通常のウィンドウアプリケーション | 非表示のウィンドウインスタンスが 1 つでも残ると終了しない |
| `OnMainWindowClose` | `MainWindow` が閉じられたとき | 主ウィンドウが明確で、補助ウィンドウを開いたまま終了させたい構成 | `MainWindow` は最初に生成されたウィンドウになる。スプラッシュ構成では明示的な代入が必要 |
| `OnExplicitShutdown` | ウィンドウの開閉では呼ばれない | トレイ常駐・多重起動制御など、ウィンドウの有無と寿命が一致しない構成 | 終了経路すべてで `Shutdown()` を呼ぶ責任が生じる |

いずれの設定でも、Windows セッションの終了時には `SessionEnding` を経て `Shutdown` が呼ばれる。
`OnExplicitShutdown` であっても、この経路だけは自動的に働く。

### 残っているスレッドへの対処

| 方法 | 効果 | 適するケース | 制約 |
|---|---|---|---|
| `IsBackground = true` | そのスレッドがプロセスの終了を妨げなくなり、終了時にランタイムが停止させる | 監視・ポーリングなど打ち切ってよい処理 | 完了保証が無く、後始末は実行されない可能性がある。効果は設定したスレッドに限られる |
| キャンセル通知 ＋ `Join`（フォアグラウンドのまま） | スレッドの終了を待ってから進む | 保存・フラッシュなど完了が必要な処理 | キャンセルに応答しないスレッドがあるとプロセスは終了しない。タイムアウト付きの `Join` は待ち側が諦めるだけで、スレッドは動き続ける |
| `Dispatcher.InvokeShutdown()` | メッセージループを終了させ `Dispatcher.Run()` を戻す | 2 つ目以降の UI スレッド | そのディスパッチャーにキュー済みの処理は破棄される |
| `Environment.Exit` | 即座にプロセスを終了する | 原因特定までの暫定処置 | `Application.Exit` が発生せず、そこへ置いた保存処理も走らない |

上 2 つは、完了保証を取るか、そのスレッドが終了を妨げないことを取るか、というトレードオフの関係にある。
キャンセルと `Join` は、スレッドがキャンセルに応答することを前提に完了を保証する代わりに、応答しなければプロセスが残る。
`IsBackground = true` はそのスレッドが終了を妨げなくなる代わりに、完了を保証しない。
効果が及ぶのは設定したスレッドだけであり、他にフォアグラウンドスレッドが残っていればプロセスはやはり終了しない。
`Join` にタイムアウトを付けても、この関係は変わらない。
時間切れで戻るのは待ち側だけで、対象スレッドは停止せずに動き続けるため、フォアグラウンドのままであればプロセスも残る。
完了が必要な処理では、まずキャンセルに確実に応答する実装にすることが本筋であり、`IsBackground = true` はその代用にならない。

---

## まとめ

ウィンドウを閉じてもプロセスが残る症状は、まず `Application.Exit`（`OnExit`）が発生するかを確認して、2 つの関門のどちらで止まっているかを確定させる。
これを先に決めない限り、`ShutdownMode` の変更もスレッドの見直しも当てずっぽうになる。

`OnExit` が呼ばれないなら第 1 関門である。
`Application.Windows` を列挙して、生成しただけで閉じていないウィンドウを探す。
ウィンドウの有無と寿命が一致しない構成であれば、`ShutdownMode` を `OnExplicitShutdown` にして終了経路から `Shutdown()` を呼ぶ形へ寄せる。

`OnExit` が呼ばれるのにプロセスが残るなら第 2 関門である。
`dotnet-stack report` かデバッガーのスレッドウィンドウで残存スレッドを特定し、打ち切ってよい処理なら `IsBackground` を `true` に、完了が必要ならキャンセルと `Join` に、2 つ目の UI スレッドなら `Dispatcher.InvokeShutdown()` に置き換える。
`Task.Run` に渡した処理はこの関門を妨げないため、調査対象から外してよい。
