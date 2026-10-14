---
layout: article-ja
title: "WPF の DispatcherUnhandledException で捕まらない例外 — 別スレッド・Task・async void の届き先"
seo_title: "WPF の DispatcherUnhandledException で捕まらない例外の届き先"
date: 2026-10-14
category: WPF
excerpt: "DispatcherUnhandledException で Handled を立てても、new Thread の例外でアプリが落ち、待たれない Task の例外はすぐにはどこにも届かない。届き先は例外を最初に受け止める層で決まる。16 の条件を .NET 10 で実測した。"
image: /images/articles/wpf-unhandled-exception-dispatcher-task-thread/unhandled-exception-routes.svg
---

## 概要

WPF アプリケーションの未処理例外対策として、`App.xaml.cs` で `DispatcherUnhandledException` を登録し、ログを書いて `e.Handled = true` にする実装が広く使われている。
この実装を入れていても、例外の起き方によっては、アプリケーションが黙って終了する場合や、ログに何も残らない場合がある。

原因はハンドラの書き方ではなく、**例外の届き先が、その例外を最初に受け止める層で決まる**ことにある。
`DispatcherUnhandledException` が受け取るのは、メインの UI スレッドの `Dispatcher` が受け止めた例外だけである。
どの層にも受け止められずにスレッドの最上位へ達した例外（`new Thread` やスレッドプールで開始した `async void` の例外）は、`AppDomain.CurrentDomain.UnhandledException` に届いてプロセスを終わらせる。
`Task` や `System.Timers.Timer` が受け止めた例外はその内部で止まり、GC の後に `UnobservedTaskException` が呼ばれる場合を除いて、どのハンドラにも届かない。

本記事では、例外の経路と条件を 16 通り用意して、例外を投げたスレッドと、3 つのハンドラのどれが呼ばれ、プロセスが動き続けるかを実測した結果を示す。
そのうえで、経路ごとに例外を確実に拾うための登録先と書き方を整理する。

---

## 前提・対象環境

- フレームワーク: .NET 6 以降 / WPF（実測は .NET 10 のみ）
- 言語: C#（`async` / `await` を使う）
- 対象機能: `Application.DispatcherUnhandledException`、`Dispatcher.UnhandledException`、`AppDomain.CurrentDomain.UnhandledException`、`TaskScheduler.UnobservedTaskException`
- アーキテクチャ: MVVM・コードビハインドのいずれでも同じ（例外の届き先は例外を受け止める層で決まり、UI の組み立て方には依存しない）
- 検証環境: .NET 10 / Windows 11
- 計測方法: `Startup` で 3 つのハンドラを登録した検証用アプリを、例外の経路ごとに別プロセスとして起動し、各経路を 1 回ずつ実行した。例外を投げたスレッドの種類と呼ばれたハンドラをファイルに記録し、経路を始めてから 2 秒後に UI スレッドがまだ動いていれば「動き続ける」とした。Click は `ButtonAutomationPeer` の `Invoke` で非同期に発生させ、Click ハンドラが UI スレッドの `Dispatcher` の処理の中で実行されるようにしている。この計測は `tools/screenshot-capture` のシーンとして実装しており、表は実行のたびに測り直し、想定と異なる結果が出た場合は失敗するようにしている

.NET Framework では検証していない。
とくに待たれない `Task` の例外の扱いは、.NET Framework では構成ファイルの [`ThrowUnobservedTaskExceptions`](https://learn.microsoft.com/ja-jp/dotnet/framework/configure-apps/file-schema/runtime/throwunobservedtaskexceptions-element) 要素で変わるため、本記事の結果をそのまま当てはめない。

---

## 現象

次のハンドラを登録しておけば、アプリケーションのどこで例外が起きてもログが残り、アプリケーションは落ちない、と考えられがちである。

```csharp
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (_, args) =>
        {
            Log("UI", args.Exception);
            args.Handled = true;
        };
    }
}
```

`Log` は後述の実装例で定義する、ログを書き出すメソッドである。
このハンドラが働くのは、ボタンの `Click` ハンドラのように UI スレッドの `Dispatcher` が実行する処理から例外が漏れた場合だけである。
同じ例外を `new Thread` の中で投げると、ハンドラは呼ばれないままプロセスが終了する。
`Task.Run` に渡した処理の中で投げて、その `Task` を待たないと、ハンドラは呼ばれず、アプリケーションは何事も無かったように動き続ける。
どちらの場合も、このハンドラによるログは残らない。

---

## 内部で何が起きているか

3 つのハンドラを登録した状態で、例外を投げる経路だけを変えて実測した結果を次に示す。

{% include tables/articles/wpf-unhandled-exception-dispatcher-task-thread/unhandled-exception-routes.ja.md %}

.NET 10 / Windows 11 で、`ShutdownMode` を `OnExplicitShutdown` にした検証用アプリを経路ごとに別プロセスとして起動し、各 1 回記録した結果。`DispatcherUnhandledException` のハンドラは、「Handled を立てない」行を除いて `e.Handled = true` にしている。`UnobservedTaskException` のハンドラは `e.SetObserved()` を呼んでいる。「+」で始まる行は、直前の行に条件を加えたものである。「GC なし」の行と「+ GC.Collect()」の行は、経路を始めてから 2 秒間 `GC.TryStartNoGCRegion` で GC を抑止して測った。「+ GC.Collect()」の行では、2 秒後に抑止を解いてから `GC.Collect()`・`GC.WaitForPendingFinalizers()`・`GC.Collect()` の順に呼んでいる。「動き続ける」は、経路を始めてから 2 秒後にも UI スレッドが動いており、検証用アプリが自ら終了したことを示す。動き続けた経路では、ハンドラが呼ばれたかどうかを 2 秒後の確認までの記録で判定している。`DispatcherUnhandledException` の列の括弧内には、代わりに例外を受けた先を示した。
{: .table-caption}

表の行は、例外を最初に受け止める仕組みによって 4 つに分かれる。

### メインの Dispatcher が受け止める例外

`Click` ハンドラと `DispatcherTimer.Tick` の例外は、`DispatcherUnhandledException` に届く。
どちらも、メインの UI スレッドの `Dispatcher` が実行した処理である。
`Dispatcher` は実行した処理から漏れた例外を受け止め、`Application` はそれを `DispatcherUnhandledException` として通知する。
`e.Handled = true` にするとプロセスは動き続ける。

`e.Handled` を立てないと、例外は `Dispatcher` の外へ出る。
この場合は `DispatcherUnhandledException` の後に `AppDomain.UnhandledException` が `IsTerminating = true` で呼ばれ、プロセスは終了した。
`AppDomain.UnhandledException` は終了を止められない。
ログを書く最後の機会であって、回復の手段ではない。

### メインの Dispatcher に受け止められない例外

`new Thread` で作ったスレッドの例外は、`DispatcherUnhandledException` には届かない。
そのスレッドには `Dispatcher` が無く、例外を受け止める層がスレッドの最上位まで存在しないためである。
例外はそのままランタイムの未処理例外となり、`AppDomain.UnhandledException` が `IsTerminating = true` で呼ばれてプロセスが終了する。

2 つ目の UI スレッドも、何も登録しなければ同じ結果になる。
このスレッドは自前の `Dispatcher` を回しているが、`Application.DispatcherUnhandledException` が通知するのは `Application` を作ったスレッドの `Dispatcher` の例外だけである。
そのスレッドの `Dispatcher.UnhandledException` に登録して `Handled` を立てると、プロセスは動き続けた。

### Task と Timer の内部で止まる例外

`Task.Run` に渡した処理の例外は、`Task` が受け止めて、自分の状態を `Faulted` にする。
その `Task` を誰も待たなければ、例外はそれ以上どこにも伝わらない。
表の「GC なし」の行のとおり、3 つのハンドラのどれも呼ばれず、プロセスは動き続ける。

この点は、例外を投げたスレッドが UI スレッドでも変わらない。
`Click` ハンドラから `async Task` のメソッドを呼んで戻り値を捨てた行では、例外は `await` の後に UI スレッドで投げられたが、`DispatcherUnhandledException` には届かなかった。
例外を受け止めたのが `Dispatcher` ではなく、メソッドの戻り値の `Task` だからである。

`TaskScheduler.UnobservedTaskException` は、失敗したまま観測されなかった `Task` がガベージコレクションで回収されるときに呼ばれる。
表の「+ GC.Collect()」の行では、GC を明示的に走らせた後にハンドラが呼ばれた。
GC がいつ走るかはメモリの状況で決まるため、このハンドラが例外の発生直後に呼ばれる保証は無い。

`System.Timers.Timer` の同期の `Elapsed` で投げた例外も、どのハンドラにも届かなかった。
`System.Timers.Timer` は `Elapsed` のハンドラから漏れた例外を内部で抑制すると、公式ドキュメントに記載されている（[Timer クラス](https://learn.microsoft.com/ja-jp/dotnet/api/system.timers.timer)）。
同じページには、`await` を含む非同期のハンドラはこの限りでないことも書かれている。
表のとおり、`async` の `Elapsed` では例外がプロセスを終了させた。
理由は次の `async void` の節で説明する。

### async void の例外

`async void` のメソッドから漏れた例外は、メソッドを**開始した時点**の `SynchronizationContext` へ投げ直される。
`async void` には例外を格納する `Task` が無いためである（[Async/Await - 非同期プログラミングのベスト プラクティス](https://learn.microsoft.com/ja-jp/archive/msdn-magazine/2013/march/async-await-best-practices-in-asynchronous-programming)）。

UI スレッドで開始した `async void` の `Click` ハンドラでは、`SynchronizationContext` が `Dispatcher` を指している。
表のとおり、`await` した `Task.Run` の例外も、`ConfigureAwait(false)` の後に投げた例外も、`DispatcherUnhandledException` に届いた。
「投げたスレッド」の列が示すとおり、どちらの例外もスレッドプールのスレッドで投げられている。
届き先を決めたのは投げたスレッドではなく、`async void` を開始した時点の `SynchronizationContext` である。

一方、スレッドプールのスレッドで開始した `async void` には `SynchronizationContext` が無い。
例外はスレッドプールのスレッドで投げ直され、`new Thread` と同じくプロセスを終了させた。
`SynchronizingObject` を設定しない `System.Timers.Timer` の `Elapsed` はスレッドプールで呼ばれるため、そこに `async` のラムダを登録すると、この経路になる。

---

## 経路から導ける帰結

この仕組みから、未処理例外の対策について次のことが導ける。

- **`DispatcherUnhandledException` だけでは、`Dispatcher` を経由しない例外を拾えない。** `new Thread` やスレッドプールで開始した `async void` の例外を受けられるのは `AppDomain.UnhandledException` だけであり、そのハンドラはプロセスの終了を止められない。これらのスレッドの処理は、処理の内側で `try` / `catch` する。
- **`await` しなかった `Task` の例外は、すぐにはログに残らないものとして扱う。** 例外を投げたスレッドが UI スレッドでも、`Task` が受け止めた時点で `DispatcherUnhandledException` には届かない。`UnobservedTaskException` は GC が走るまで呼ばれない。
- **UI のイベントハンドラ（`async void`）から `await` の連鎖をつなぐと、例外は `DispatcherUnhandledException` に集まる。** イベントハンドラが `Task.Run` や `async Task` のメソッドを `await` し、そのメソッドも内側の `Task` をすべて `await` していれば、例外は連鎖をさかのぼってイベントハンドラから漏れる。そこから先は、イベントハンドラを開始した時点の `SynchronizationContext`、つまりメインの `Dispatcher` へ届く。表の「async void の Click で async Task を await」の行がこれにあたる。途中で 1 か所でも戻り値を捨てると、その `Task` で止まる。
- **同じ `async` のハンドラでも、イベントを発生させたスレッドの `SynchronizationContext` で届き先が変わる。** `Click` に登録した `async` のハンドラは `DispatcherUnhandledException` に届くが、`System.Timers.Timer.Elapsed` に登録した `async` のラムダはプロセスを終了させる。同期のハンドラでは例外が抑制されるのに、`async` にすると逆に終了するため、書き換えたときに挙動が反転する。

---

## 実装例

3 つのハンドラを、受け取れる例外と役割に合わせて登録する。
`DispatcherUnhandledException` は UI スレッドの例外を記録して処理を続け、`AppDomain.UnhandledException` は終了前の最後の記録、`UnobservedTaskException` は待ち忘れた `Task` の検出に使う。

```csharp
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // メインの Dispatcher が受け止めた例外。Handled を立てればアプリケーションは続行する。
        DispatcherUnhandledException += (_, args) =>
        {
            Log("UI", args.Exception);
            args.Handled = true;
        };

        // Dispatcher を経由しない例外と、Handled を立てなかった UI スレッドの例外。
        // ここに来た時点でプロセスの終了は止められない。
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            Log(args.IsTerminating ? "Fatal" : "NonFatal", args.ExceptionObject as Exception);

        // 待たれずに失敗した Task の例外。GC の後で呼ばれるため、発生からは遅れる。
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Log("Unobserved", args.Exception);
            args.SetObserved();
        };
    }

    private static void Log(string kind, Exception? exception)
    {
        // 実際のアプリケーションではファイルやログ基盤へ書き出す。
        System.Diagnostics.Trace.WriteLine($"[{kind}] {exception}");
    }
}
```

`AppDomain.UnhandledException` の `ExceptionObject` は `object` 型であり、`Exception` 以外が入る場合に備えて `as` で受けている。
このため `Log` の引数は null 許容にしている。
このハンドラはプロセスが終了する直前に呼ばれるため、時間のかかる処理や UI の表示を置かない。

バックグラウンドの処理は、UI のイベントハンドラから `await` する。
表の「async void の Click で await を try / catch」の行のとおり、例外は `await` の位置で投げ直され、`catch` で受けられる。
受けなかった例外は `DispatcherUnhandledException` に届く。

```csharp
private async void SaveButton_Click(object sender, RoutedEventArgs e)
{
    try
    {
        await Task.Run(() => SaveToFile(_document));
        StatusText.Text = "保存した";
    }
    catch (IOException ex)
    {
        StatusText.Text = $"保存に失敗した: {ex.Message}";
    }
}
```

`ImplicitUsings` を有効にしても、WPF のプロジェクトでは `System.IO` が暗黙の `using` に含まれない（[暗黙的な using ディレクティブ](https://learn.microsoft.com/ja-jp/dotnet/core/project-sdk/overview#implicit-using-directives)）。
このため、`IOException` を使うファイルの先頭に `using System.IO;` を書く。
`catch` で想定した例外だけを受け、それ以外は `DispatcherUnhandledException` へ流す構成である。
`Task.Run(...)` を `await` しないと、例外は `DispatcherUnhandledException` にも `AppDomain.UnhandledException` にも届かない。
届くとしても、GC の後の `UnobservedTaskException` だけである。

`new Thread` で作ったスレッドや、`System.Timers.Timer` の `Elapsed` では、処理の本体を `try` / `catch` で囲む。
`Elapsed` を `async` にする場合も同じである。
周期処理を UI スレッドで実行してよい場合は `DispatcherTimer` を使う。
`Tick` の例外は `DispatcherUnhandledException` に届く。
ただし `Tick` は UI スレッドで実行されるため、重い処理は `Tick` の中から `await Task.Run(...)` で逃がす。

---

## 注意点

- **`e.Handled = true` は、例外の後もアプリケーションの状態が正しいことを保証しない。** 処理の途中で例外が起きると、ViewModel やファイルが中途半端な状態で残る。どの例外でも無条件に続行させると、壊れた状態のまま操作を受け付けることになる。続行させる例外を限定するか、利用者へ通知して保存や再起動を促す。
- **`UnobservedTaskException` を「待ち忘れの保険」として当てにしない。** ファイナライザーが実行される時期は決まっておらず（[Object.Finalize](https://learn.microsoft.com/ja-jp/dotnet/api/system.object.finalize)）、.NET 5 以降はプロセスの終了時にファイナライザーを実行しない（[ファイナライザー](https://learn.microsoft.com/ja-jp/dotnet/csharp/programming-guide/classes-and-structs/finalizers)）。このため、一度も呼ばれずに終わる場合もある。
- **ViewModel のメソッドを `async void` にしない。** UI スレッドから呼べば `DispatcherUnhandledException` に届くが、スレッドプールから呼ぶとプロセスを終了させる。戻り値は `Task` にして、呼び出し側で `await` する。
- **2 つ目の UI スレッドを作る場合は、そのスレッドの `Dispatcher.UnhandledException` に登録する。** `Application.DispatcherUnhandledException` はメインの `Dispatcher` だけを対象にする。
- **.NET 10 には `System.Runtime.ExceptionServices.ExceptionHandling.SetUnhandledExceptionHandler` もある（[API リファレンス](https://learn.microsoft.com/ja-jp/dotnet/api/system.runtime.exceptionservices.exceptionhandling.setunhandledexceptionhandler)）。** 本記事では扱っておらず、WPF の別スレッドの例外に対する効果は検証していない。

---

## まとめ

未処理例外の届き先は、例外を最初に受け止める層で決まる。
`DispatcherUnhandledException` が受け取るのはメインの `Dispatcher` が受け止めた例外だけであり、どの層にも受け止められずにスレッドの最上位へ達した例外はプロセスを終了させ、待たれない `Task` の例外は GC が走るまでどのハンドラにも届かない。

アプリケーションを落とさず、例外を確実にログへ残すには、例外を `DispatcherUnhandledException` へ集める構成を基本にする。
バックグラウンドの処理は UI のイベントハンドラから `await` の連鎖を切らさずに呼び、UI スレッドで実行してよい周期処理は `DispatcherTimer` で書く。
この形に乗らない `new Thread` や `System.Timers.Timer` は、処理の内側で `try` / `catch` する。
`AppDomain.UnhandledException` は終了前の記録、`UnobservedTaskException` は待ち忘れの検出として登録し、どちらも例外から回復する手段としては使わない。

---

## 関連記事

- [WPF でウィンドウを閉じてもプロセスが終了しない原因の切り分けと ShutdownMode・フォアグラウンドスレッドの扱い](/ja/articles/wpf-application-not-exiting-shutdownmode-threads/)
- [WPF で ObservableCollection をバックグラウンドスレッドから更新するとクロススレッド例外が発生する問題の解決方法](/ja/articles/wpf-observablecollection-cross-thread-update/)
