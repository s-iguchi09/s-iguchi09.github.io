---
layout: article-ja
title: "WPF で CommunityToolkit.Mvvm の AsyncRelayCommand が投げた例外の届き先と FlowExceptionsToTaskScheduler"
seo_title: "WPF の AsyncRelayCommand の例外の届き先と FlowExceptionsToTaskScheduler"
date: 2026-10-27
category: WPF
excerpt: "AsyncRelayCommand の例外は既定では DispatcherUnhandledException に届くが、FlowExceptionsToTaskScheduler を付けると、ビューモデルを残した計測では次の実行の後の GC までどこにも届かなかった。"
image: /images/articles/wpf-asyncrelaycommand-exception-handling/asyncrelaycommand-after-failure.png
---

## 概要

取り込みボタンの処理の例外でアプリが終了するのを避けようとして、コマンドに `FlowExceptionsToTaskScheduler` を付けると、今度は失敗しても何も言わなくなる。
CommunityToolkit.Mvvm（以下 Toolkit）の `AsyncRelayCommand` では、この設定 1 つで例外の届き先が入れ替わる。
既定では、`[RelayCommand]` を付けた `async Task` のメソッドの例外は `Application.DispatcherUnhandledException` に届く。
`FlowExceptionsToTaskScheduler` を付けると、1 回目の実行の後は、公式の解説が届き先として挙げる `TaskScheduler.UnobservedTaskException` にも、GC を呼んだ後でさえ届かなかった。
画面には何も出ず、失敗の後のボタンは押せる状態である。

例外の行き先を決めているのは、`AsyncRelayCommand.Execute` が呼ぶ 1 つの `async void` のメソッドである。
その経路を Toolkit のソースで追い、書き方ごとの届き先を実測で比べる。
比較には、Prism 8 の `DelegateCommand` に `async` のラムダを渡す書き方も加え、最後に例外をコマンドの中で受ける実装を示す。

---

## 前提・対象環境

- フレームワーク: .NET 6 以降 / WPF
- ライブラリ: CommunityToolkit.Mvvm 8.4.0、Prism.Core 8.1.97（Prism は MIT ライセンスで提供された最後の版を使った）
- 検証環境: .NET 10（10.0.13）/ Windows 11
- アーキテクチャ: MVVM（ビューはバインドだけで動かし、コードビハインドを持たない）

計測では、ツールのプロセスを落とさないために `DispatcherUnhandledException` のハンドラーで `e.Handled = true` にしている。
`Handled` にしなかったときにアプリが終了することは、本記事では測っていない。
.NET Framework と、Prism 9 以降にある `AsyncDelegateCommand` も測っていない。

---

## 現象

ファイルを取り込むコマンドを、Toolkit のソース生成で書く。
取り込みの失敗を再現するため、`ReadImportFile` は常に例外を投げる。

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

public sealed partial class ImportViewModel : ObservableObject
{
    [ObservableProperty]
    private string _errorMessage = "";

    [RelayCommand]
    private async Task ImportAsync()
    {
        await Task.Run(ReadImportFile);
    }

    private static void ReadImportFile() =>
        throw new InvalidOperationException("The import file was not found.");
}
```

`[RelayCommand]` は、`Task` を返すメソッドから `AsyncRelayCommand` を生成する。
コマンドの名前は、メソッド名から `Async` を除いて `Command` を付けた `ImportCommand` になる（[RelayCommand 属性の解説](https://learn.microsoft.com/ja-jp/dotnet/communitytoolkit/mvvm/generators/relaycommand)）。

ビューは、ボタンとエラーのメッセージを出す `TextBlock` だけである。

```xml
<StackPanel Margin="8" Width="360">
  <Button Content="Import" Command="{Binding ImportCommand}"
          HorizontalAlignment="Left" Padding="14,2" />
  <TextBlock Margin="0,6,0,0" Foreground="#D13438" Text="{Binding ErrorMessage}" />
</StackPanel>
```

ボタンを押すと、`ReadImportFile` の例外が `DispatcherUnhandledException` に届く。
[`DispatcherUnhandledException` のリファレンス](https://learn.microsoft.com/ja-jp/dotnet/api/system.windows.application.dispatcherunhandledexception)によれば、WPF は既定で未処理の例外をダイアログで知らせてアプリを終了し、ハンドラーで `Handled` を `true` にすると処理を続ける。
`Handled` にしないとアプリが終了する例は、[WPF の DispatcherUnhandledException で捕まらない例外 — 別スレッド・Task・async void の届き先](/ja/articles/wpf-unhandled-exception-dispatcher-task-thread/)の表の「Click ハンドラ（Handled を立てない）」の行で実測している。

[RelayCommand 属性の解説](https://learn.microsoft.com/ja-jp/dotnet/communitytoolkit/mvvm/generators/relaycommand#handling-asynchronous-exceptions)は、この既定の動作を変える `FlowExceptionsToTaskScheduler` を紹介している。
付けると例外でアプリは落ちなくなり、例外は `ExecutionTask` から読めるうえ `TaskScheduler.UnobservedTaskException` にも流れる、という説明である。

```csharp
[RelayCommand(FlowExceptionsToTaskScheduler = true)]
private async Task ImportAsync()
{
    await Task.Run(ReadImportFile);
}
```

この書き方では、ボタンを押しても `DispatcherUnhandledException` は呼ばれない。
ところが `UnobservedTaskException` も呼ばれず、画面にも何も出ず、失敗の後のボタンは押せる状態である。

---

## 内部で何が起きているか

### Execute は結果の Task を async void で待つ

ボタンはコマンドの `ICommand.Execute` を呼ぶ。
[Toolkit 8.4.0 の AsyncRelayCommand のソース](https://github.com/CommunityToolkit/dotnet/blob/v8.4.0/src/CommunityToolkit.Mvvm/Input/AsyncRelayCommand.cs)では、`Execute` は `ExecuteAsync` を呼んで `Task` を受け取り、`FlowExceptionsToTaskScheduler` が無ければ、それを `AwaitAndThrowIfFailed` に渡す。

```csharp
public void Execute(object? parameter)
{
    Task executionTask = ExecuteAsync(parameter);

    if ((this.options & AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler) == 0)
    {
        AwaitAndThrowIfFailed(executionTask);
    }
}

internal static async void AwaitAndThrowIfFailed(Task executionTask)
{
    await executionTask;
}
```

上はソースからコメントを除いて抜き出したものである。
`AwaitAndThrowIfFailed` は `async void` で、ソースのコメントには、同期のコマンドと同じく例外を元の同期コンテキストで投げ直すために、あえて `async void` にしていると書かれている。
[`async void` のメソッドから漏れた例外は、メソッドを開始した時点の `SynchronizationContext` に投げ直される](https://learn.microsoft.com/ja-jp/dotnet/standard/asynchronous-programming-patterns/common-async-bugs)。
ボタンから呼ばれた `Execute` は UI スレッドで動くので、例外は `Dispatcher` に投げ直され、`DispatcherUnhandledException` に届く（[前述の記事](/ja/articles/wpf-unhandled-exception-dispatcher-task-thread/)の「async void の例外」の節）。

`FlowExceptionsToTaskScheduler` を付けると、この `AwaitAndThrowIfFailed` が呼ばれない。
`ExecutionTask` の完了を通知するために `Task` を待つ内部の処理（`MonitorTask`）は残るが、結果を確かめない待ち方（`GetAwaitableWithoutEndValidation`）なので、例外は受け取られない。
例外は失敗した `Task` の中に残り、その `Task` は `ExecutionTask` プロパティに入れられたままである。

Prism 8 には `AsyncDelegateCommand` のような非同期専用のコマンドの型が無く、`DelegateCommand` は `Action` を受け取る。
ここに `async` のラムダを渡すと、ラムダは `async void` になり、Toolkit の既定と同じ経路で例外が `Dispatcher` に投げ直される。

```csharp
ImportCommand = new DelegateCommand(async () => await Task.Run(ReadImportFile));
```

`async void` のラムダは `Task` を返さないので、`DelegateCommand` の側に失敗が残ることも無い。

### 書き方ごとの届き先

書き方ごとに、ボタンを 1 回押した後の届き先と、2 回目を押して GC を呼んだ後の `UnobservedTaskException` を測った。

{% include tables/articles/wpf-asyncrelaycommand-exception-handling/asyncrelaycommand-exception-routes.ja.md %}

.NET 10.0.13 / Windows 11、CommunityToolkit.Mvvm 8.4.0 / Prism.Core 8.1.97 で、「現象」の節のビュー（5 行目だけはボタンを呼び出し側のコマンドに結び付ける）を表示し、ボタンを UI オートメーションの `Invoke` で押した（`Invoke` は `ButtonBase.OnClick` を呼び、`OnClick` が `Click` イベントの後にコマンドを実行する）。取り込みはどの書き方でも `Task.Run` の中で `InvalidOperationException` を投げる。押してから GC を呼ばずに 1 秒待ち、「GC の後」と「2 回目を押して GC の後」を除く列を読んだ。続けて `GC.Collect()`・`GC.WaitForPendingFinalizers()`・`GC.Collect()` を呼んで「GC の後」を読み、最後にもう一度押して 1 秒待ち、同じく GC を呼んで「2 回目を押して GC の後」を読んだ。`UnobservedTaskException` の回数は 1 回目からの累計で、それ以外の列は 1 回目を押した後の値である。ビューモデルはウィンドウに表示したまま GC を呼んでいる。`DispatcherUnhandledException` のハンドラーは `e.Handled = true` に、`UnobservedTaskException` のハンドラーは `e.SetObserved()` にしている。計測の前には、前の書き方の `Task` が数に入らないよう、`UnobservedTaskException` が出なくなるまで GC を繰り返した。各書き方 1 回の記録である。
{: .table-caption}

**既定の 1 行目と Prism の 3 行目では、`DispatcherUnhandledException` が 1 回呼ばれた。**
`AwaitAndThrowIfFailed` やラムダの `await` が例外を受け取っているので、`UnobservedTaskException` は GC の後も呼ばれない。

**`FlowExceptionsToTaskScheduler` の 2 行目では、1 回目を押した後に GC を呼んでも、`UnobservedTaskException` が呼ばれなかった。**
[`UnobservedTaskException` は、例外が観測されないまま失敗した `Task` がファイナライズされるときに呼ばれる](https://learn.microsoft.com/ja-jp/dotnet/standard/asynchronous-programming-patterns/task-exception-handling)。
1 回目の `Task` はコマンドの `ExecutionTask` が参照しているので、回収されない。
2 回目を押すと `ExecutionTask` が 2 回目の `Task` に差し替わり、その後の GC で初めて 1 回呼ばれた。
計測では 1 回目の `ExecutionTask` を弱参照で持ち、Toolkit の 4 つの行のどれでも、1 回目の GC の後には残っていて、2 回目を押した後の GC で回収されたことを確かめている。
2 回目の `Task` は `ExecutionTask` に入ったままなので、ソースの参照関係のとおり、2 回目の後の GC でも回収されない（こちらは弱参照で確かめていない）。

---

## 経路から導ける帰結

### 失敗は ExecutionTask にだけ残る

`FlowExceptionsToTaskScheduler` を付けた書き方では、最後の失敗を知る手がかりは `ExecutionTask` だけである。
`ExecutionTask` は、実行が終わると変更の通知を出し直す（[AsyncRelayCommand の解説](https://learn.microsoft.com/ja-jp/dotnet/communitytoolkit/mvvm/asyncrelaycommand)）。
そのため、ビューから `ExecutionTask.Status` にバインドすれば、失敗したことを画面に出せる。
「現象」の節のビューのボタンの下に、次の 1 行を足す。

```xml
<TextBlock Margin="0,6,0,0"
           Text="{Binding ImportCommand.ExecutionTask.Status,
                  StringFormat='ExecutionTask: {0}',
                  FallbackValue='ExecutionTask: (not run)'}" />
```

まだ実行していない間は `ExecutionTask` が `null` でパスを解決できないので、[`FallbackValue`](https://learn.microsoft.com/ja-jp/dotnet/api/system.windows.data.bindingbase.fallbackvalue) の文字列がそのまま出る。
計測では、押す前の行が `ExecutionTask: (not run)` で、押した後に `Faulted` や `RanToCompletion` に変わることを確かめた。
この表示は、後の「実装例」の節の図で使う。

### ExecuteAsync を await すれば、例外は呼び出し側に届く

例外を `Dispatcher` に投げ直すのは、`ICommand.Execute` の中の `AwaitAndThrowIfFailed` である。
`IAsyncRelayCommand.ExecuteAsync` は、実行の `Task` をそのまま返す。
別のコマンドから `ExecuteAsync` を `await` すれば、例外は呼び出し側の `try` / `catch` で受けられる。

```csharp
[RelayCommand]
private async Task ImportAndReportAsync()
{
    ErrorMessage = "";
    try
    {
        await ImportCommand.ExecuteAsync(null);
    }
    catch (InvalidOperationException ex)
    {
        ErrorMessage = ex.Message;
    }
}
```

表の 5 行目がこの書き方で、`DispatcherUnhandledException` は呼ばれず、メッセージが画面に出た。
ただし `ImportCommand.ExecutionTask.Status` は `Faulted` のままである。
呼び出し側で受けたかどうかは `ExecutionTask` には反映されないので、`ExecutionTask.Status` を画面に出している場合は、失敗の表示が残る。

### 失敗の後のボタンは押せる

表の最後の列のとおり、どの書き方でも、例外の後のボタンは押せる状態だった。
`FlowExceptionsToTaskScheduler` の書き方で画面に何も出さなければ、利用者には押しても何も起きなかったように見える。

---

## 実装例

取り込みの失敗のように、起きることが分かっている例外は、コマンドのメソッドの中で受けてビューモデルの状態にする。
`FlowExceptionsToTaskScheduler` は付けず、既定のまま使う。

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

public sealed partial class ImportViewModel : ObservableObject
{
    [ObservableProperty]
    private string _errorMessage = "";

    [RelayCommand]
    private async Task ImportAsync()
    {
        ErrorMessage = "";
        try
        {
            await Task.Run(ReadImportFile);
        }
        catch (InvalidOperationException ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    private static void ReadImportFile() =>
        throw new InvalidOperationException("The import file was not found.");
}
```

ビューは「現象」の節の XAML をそのまま使う。
表の 4 行目のとおり、`DispatcherUnhandledException` も `UnobservedTaskException` も呼ばれず、メッセージが画面に出た。
メソッドは正常に終わるので、`ExecutionTask.Status` は `RanToCompletion` になる。

3 つの書き方を、「経路から導ける帰結」の節の `ExecutionTask.Status` の行を足したビューで並べると、次のとおりである。

<figure class="article-figure">
  <img src="/images/articles/wpf-asyncrelaycommand-exception-handling/asyncrelaycommand-after-failure.png" alt="Import ボタンと ExecutionTask の状態の行を 1 組とした 3 組の画面。既定の上の組と FlowExceptionsToTaskScheduler の中の組は、どちらも ExecutionTask: Faulted と出ているだけで、エラーのメッセージは無い。ImportAsync の中で try / catch する下の組は、ExecutionTask: RanToCompletion の下に赤い文字で The import file was not found. と出ている。3 つの Import ボタンはどれも押せる状態である。" width="414" height="410" loading="lazy">
  <figcaption>.NET 10.0.13 / Windows 11、CommunityToolkit.Mvvm 8.4.0 で、「現象」の節のビューに <code>ExecutionTask.Status</code> の行を足したビュー 3 つに、それぞれ別のビューモデルを渡し、Import ボタンを押して 1 秒待った後に撮った。上は「現象」の節の既定の書き方（<code>DispatcherUnhandledException</code> は計測のハンドラーで <code>Handled</code> にした）、中は <code>FlowExceptionsToTaskScheduler</code> を付けた書き方、下は本節のコードである。各組の見出しは撮影のために付けたもので、ビューの XAML には含まれない。</figcaption>
</figure>

上の 2 組の画面は同じに見える。
既定の書き方では例外が `DispatcherUnhandledException` に届いているが、そのハンドラーが記録や表示をしなければ、画面からは区別できない。

`catch` で受けるのは、取り込みの失敗として想定した例外の型だけにする。
`catch` しなかった例外はメソッドから漏れ、ソースの経路のとおり、表の 1 行目と同じく `AwaitAndThrowIfFailed` から `DispatcherUnhandledException` に届くので、アプリ全体のハンドラーで記録できる（別の型の例外を投げる場合は測っていない）。
`catch (Exception)` ですべて受けると、取り込みと関係のない不具合まで画面のメッセージに紛れる。

---

## 注意点

- **`FlowExceptionsToTaskScheduler` を付けたら、`ExecutionTask` を誰かが見る。**
  [RelayCommand 属性の解説](https://learn.microsoft.com/ja-jp/dotnet/communitytoolkit/mvvm/generators/relaycommand#handling-asynchronous-exceptions)は、この設定では想定していない例外も自動では投げ直されなくなるので、場面ごとに扱いを決めるよう注意している。
  実測では、`UnobservedTaskException` は次に実行して `ExecutionTask` が差し替わり、GC が走るまで呼ばれなかった。
  一度しか実行しないコマンドでは、コマンドを持つビューモデルごと回収されるまで `Task` は回収されないと考えられるが、本記事では測っていない。
  コマンドを持つビューモデルが生きている間は、最後の失敗が次の実行と GC まで `UnobservedTaskException` に届かないので、この設定の例外を記録する手段として当てにできない。
- **`UnobservedTaskException` が呼ばれる時期は GC 次第である。**
  計測では GC を明示的に呼んだ。
  実際のアプリで GC がいつ走るかはメモリの状況で決まる（[前述の記事](/ja/articles/wpf-unhandled-exception-dispatcher-task-thread/)の「Task と Timer の内部で止まる例外」の節）。
- **Prism 8 の `DelegateCommand` に `async` のラムダを渡すと、`async void` になる。**
  例外は Toolkit の既定と同じく `DispatcherUnhandledException` に届く（表の 3 行目）。
  `DelegateCommand` には `ExecutionTask` に当たるものが無いので、失敗を後から読む手段も無い。
  例外を受けるには、Toolkit の実装例と同じく、ラムダの中で `await` を `try` / `catch` で囲むことになる。
  Prism でのこの書き方と、Prism 9 以降の `AsyncDelegateCommand` は測っていない。
- **計測は、取り込みの例外が `Task.Run` の中で投げられる場合だけである。**
  `await` より前に同期的に投げる場合や、`CancellationToken` を受け取るメソッドで取り消した場合は測っていない。

---

## まとめ

`AsyncRelayCommand` の `Execute` は、実行の `Task` を `async void` のメソッドで待ち、失敗すれば例外を `Execute` を呼んだ時点の同期コンテキストに投げ直す。
ボタンから実行した場合は UI スレッドの `Dispatcher` に投げ直されるので、既定では、コマンドの例外は `DispatcherUnhandledException` に届く。
Prism 8 の `DelegateCommand` に `async` のラムダを渡した場合も、ラムダが `async void` になるので同じ経路をたどった。

`FlowExceptionsToTaskScheduler` を付けると投げ直しが無くなり、失敗した `Task` は `ExecutionTask` に残る。
`ExecutionTask` が参照している間は回収されないので、ビューモデルを表示したまま測った計測では、`UnobservedTaskException` は次に実行した後の GC まで呼ばれなかった。
コマンドを持つビューモデルごと回収できる状態になれば、次の実行が無くても GC の後に呼ばれうるが、本記事では測っていない。
`ExecutionTask` を画面やコードで見ないのであれば、コマンドが使われ続けている間、例外は次に実行して GC が走るまでどこにも届かない。

起きることが分かっている例外は、既定のままコマンドのメソッドの中で `try` / `catch` し、ビューモデルの状態にして画面に出す。
`FlowExceptionsToTaskScheduler` は、`ExecutionTask` にバインドして結果を表示するなど、失敗を `ExecutionTask` から読み取る仕組みを用意した場合に限って使う。

---

## 関連記事

- [WPF の DispatcherUnhandledException で捕まらない例外 — 別スレッド・Task・async void の届き先](/ja/articles/wpf-unhandled-exception-dispatcher-task-thread/)
- [WPF で CommunityToolkit.Mvvm と Prism のコマンドに InvalidateRequerySuggested が効かない理由](/ja/articles/wpf-mvvm-library-command-requery/)
- [WPF で CommunityToolkit.Mvvm の ObservableValidator が起動直後に検証エラーを出さない理由](/ja/articles/wpf-observablevalidator-no-error-on-startup/)
