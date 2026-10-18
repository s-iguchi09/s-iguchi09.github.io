---
layout: article-ja
title: "WPF で Task.Result を呼ぶと画面が固まる原因と、ConfigureAwait(false) で直らない条件"
date: 2026-10-19
category: WPF
excerpt: "await の後で UI スレッドへ戻る async メソッドを UI スレッドで .Result で待つと、後続が UI スレッドの空きを待ち続けて画面が固まる。回避策の ConfigureAwait(false) と Task.Run も、書き方しだいで同じように固まる。"
image: /images/articles/wpf-task-result-deadlock-synchronizationcontext/task-result-deadlock-matrix.svg
---

## 概要

ボタンの Click ハンドラのような同期メソッドの中で、`LoadAsync().Result` と書いて非同期メソッドの結果を受け取ると、その非同期メソッドが `await` の後で UI スレッドへ戻る作りであれば、WPF ではクリックした瞬間に画面が応答しなくなる。
例外は発生せず、`.Result` の待機自体にもタイムアウトが無いため、ウィンドウは二度と戻らない。
本記事の計測では、この状態を監視用のスレッドが 2.5 秒で判定し、プロセスを外から終了させている（前提・対象環境を参照）。

`await` は始まったときの UI スレッドへ後続の処理を戻そうとし、その UI スレッドは `.Result` の中で後続を待っている。
互いに相手を待つため、どちらも進まない。

Microsoft Learn は、同期的に待つことが避けられない場合の緩和策として、非同期メソッドのすべての `await` に `ConfigureAwait(false)` を付けることと、`Task.Run` でスレッドプールへ逃がすことを挙げている。
どちらも条件が合えば効くが、`ConfigureAwait(false)` を完了済みの `Task` の `await` や外側の `await` にだけ付けた書き方や、`Task.Run` の中から UI スレッドを同期的に呼ぶ書き方では、同じように固まった。
効いた場合でも、待っている間は UI スレッドが止まる。

以下では、Click ハンドラから同じ処理を 12 通りの方法で待ち、どこで止まり、どこで抜けるかを表にした。
`ConfigureAwait(false)` がどの `await` に付いていれば足りるのかを、この表から読み取る。

---

## 前提・対象環境

- フレームワーク: .NET 6 以降 / WPF（実測は .NET 10 のみ）
- 言語: C#（`async` / `await` を使う）
- 対象機能: `Task.Result`、`Task.Wait`、`Task.GetAwaiter().GetResult()`、`Task.ConfigureAwait`、`Task.Run`、`Dispatcher.Invoke`、`DispatcherSynchronizationContext`
- アーキテクチャ: コードビハインドの Click ハンドラで測った。MVVM の `ICommand` から呼ぶ場合も UI スレッドで待つ点は同じだが、計測はしていない
- 検証環境: .NET 10 / Windows 11
- 計測方法: 待ち方ごとに検証用の WPF アプリケーションを別プロセスで起動し、ウィンドウを表示した後に `ButtonAutomationPeer` の `Invoke` で Click を発生させた。Click ハンドラは最初に UI スレッドへ処理を 2 つ積み（`Dispatcher.BeginInvoke`。優先度は `Normal` と、描画と同じ優先度の `Render`）、そのうえで非同期メソッドを待つ。監視用のスレッドが 2.5 秒後に Click ハンドラが戻っていなければ「止まったまま」とし、その時点の `Task.Delay` と積んだ処理の状態を記録した。固まったプロセスは外から終了させている。この計測は `tools/screenshot-capture` のシーンとして実装しており、表は実行のたびに測り直し、想定と異なる結果が出た場合は失敗するようにしている

.NET Framework と、WPF 以外のアプリケーションモデルでは検証していない。

---

## 問題

次の Click ハンドラは、ボタンを押すと戻らない。
`LoadAsync` は、検証では I/O の代わりに `Task.Delay(300)` を待つ。
実際のアプリケーションでは `HttpClient` やデータベースの呼び出しがこの位置に来る。

```csharp
private static async Task<int> LoadAsync()
{
    await Task.Delay(300);
    return 42;
}

private void OnLoadClick(object sender, RoutedEventArgs e)
{
    int value = LoadAsync().Result; // ここから戻らない
    StatusText.Text = value.ToString();
}
```

300 ミリ秒で終わるはずの処理が、2.5 秒経っても終わらない。
固まった時点で調べると、`Task.Delay` はすでに完了している。
止まっているのは待っていた処理ではなく、その後の `return 42;` である。

`.Result` を `.GetAwaiter().GetResult()` や `.Wait()` に替えても結果は変わらない。
3 つとも、Click ハンドラの中で `Task` の完了を待って UI スレッドを止める点は同じである。

---

## よく紹介される対処

Microsoft Learn の [非同期メソッドの同期ラッパー](https://learn.microsoft.com/ja-jp/dotnet/standard/asynchronous-programming-patterns/synchronous-wrappers-for-asynchronous-methods)（英語版の題名は Synchronous wrappers for asynchronous methods）は、まず呼び出し元まで `async` にする書き換えを勧め、それができない場合の緩和策として次の 2 つを挙げている。

- 非同期メソッドを自分で書き換えられる場合は、その中のすべての `await` に `ConfigureAwait(false)` を付ける
- 書き換えられない場合は、呼び出しを `Task.Run` でスレッドプールへ逃がす

`ConfigureAwait(false)` は、`await` の後の処理を元のコンテキストへ戻さないよう指定するメソッドである（[Task.ConfigureAwait(Boolean)](https://learn.microsoft.com/ja-jp/dotnet/api/system.threading.tasks.task.configureawait)）。
`LoadAsync` の `await` に付けたものを `LoadAllConfigureAwaitAsync` とする。

```csharp
private static async Task<int> LoadAllConfigureAwaitAsync()
{
    await Task.Delay(300).ConfigureAwait(false);
    return 42;
}
```

`Task.Run` で逃がす方法は、`LoadAsync` を書き換えずに次のように呼ぶ。

```csharp
int value = Task.Run(() => LoadAsync()).Result;
```

どちらの形でも `.Result` は戻った。
問題は、実際のコードがこの例ほど単純ではないことにある。
`ConfigureAwait(false)` を付けたつもりでも固まる書き方が 2 つあり、`Task.Run` で逃がしても固まる書き方が 1 つあった。

---

## なぜ効かないのか

Click ハンドラから、待ち方だけを変えて非同期メソッドを待った結果を次に示す。
各メソッドの中身は、本文のコード例のとおりである。

{% include tables/articles/wpf-task-result-deadlock-synchronizationcontext/task-result-deadlock-matrix.ja.md %}

待ち方ごとに別プロセスで 1 回ずつ実行した結果（.NET 10 / Windows 11）。「UI スレッド」の列の時間はこの環境での値である。戻った行の時間は、内側で待っている `Task.Delay(300)` の合計（`LoadFirstSuspendConfigureAwaitAsync` は 2 回で 600 ミリ秒）にほぼ一致し、`Wait(TimeSpan.FromSeconds(1))` の行はタイムアウトの 1 秒に一致する。`async void` の行だけは、冒頭で積んだ処理が走るまでの時間である。「待っている間に UI スレッドへ積んだ処理」は、Click ハンドラの冒頭で積んだ `Normal` 優先度の処理が実行された時点を示す。止まったままの行では、`Render` 優先度で積んだ処理も Pending のままだった。
{: .table-caption}

Click ハンドラの中では、`SynchronizationContext.Current` が `DispatcherSynchronizationContext` になっていた。
`await` はこのコンテキストを取り込み、待っていた `Task` が完了すると、後続の処理をこのコンテキスト経由で UI スレッドへ送る。
UI スレッドは `.Result` の中で止まっているので、送られた処理は実行されない。
冒頭で積んだ処理が `Normal` も `Render` も Pending のままであることも、UI スレッドが積まれた処理を実行できていないことを示している。

後続が UI スレッドの順番待ちになっていることは、`LoadAsync().Wait(TimeSpan.FromSeconds(1))` の行から分かる。
1 秒のタイムアウトで `Wait` が `false` を返し、Click ハンドラが UI スレッドを放すと、`await` の後の処理はその後に UI スレッドで実行された。
タイムアウトは固まった状態を失敗に変えるだけで、待っている間に UI スレッドが止まることは変わらない。

### 完了済みの Task に付けた ConfigureAwait(false)

キャッシュに値があれば `Task.FromResult` を返す `GetCachedAsync` を先に待ち、その後で I/O を待つ形にしたのが `LoadCachedFirstAsync` である。
`ConfigureAwait(false)` は 1 つ目の `await` にだけ付けている。

```csharp
private static Task<int> GetCachedAsync() => Task.FromResult(42);

private static async Task<int> LoadCachedFirstAsync()
{
    int cached = await GetCachedAsync().ConfigureAwait(false); // 完了済みの Task
    await Task.Delay(300);
    return cached;
}
```

`GetCachedAsync` が返す `Task` は、`await` の時点で完了している。
完了済みの `Task` に対する `await` は中断しないので、`ConfigureAwait(false)` は何もしない。
実測でも、1 つ目の `await` の直後はまだ UI スレッドにいた。
そのため、付いていない 2 つ目の `await` が UI スレッドのコンテキストを取り込み、`LoadCachedFirstAsync().Result` の行は止まったままになった。
2 つ目の `await` にも付けた `LoadCachedFirstAllConfigureAwaitAsync` は戻っている。

```csharp
private static async Task<int> LoadCachedFirstAllConfigureAwaitAsync()
{
    int cached = await GetCachedAsync().ConfigureAwait(false); // 完了済みの Task
    await Task.Delay(300).ConfigureAwait(false);                // ここで中断し、後続はスレッドプールで動く
    return cached;
}
```

1 つ目の `await` が何もしなくても、実際に中断する 2 つ目の `await` に付いていれば、後続は元のコンテキストへ戻されない（この例ではスレッドプールで続いた）。

### 外側の await にだけ付けた ConfigureAwait(false)

自分のメソッドの `await` にだけ付け、呼び出し先の `LoadAsync` はそのままにしたのが `LoadOuterAsync` である。

```csharp
private static async Task<int> LoadOuterAsync()
{
    return await LoadAsync().ConfigureAwait(false); // LoadAsync の中の await には付いていない
}
```

`ConfigureAwait(false)` が影響するのは、それを付けた `await` の後続だけである。
`LoadAsync` は UI スレッドで呼ばれ、その中の `await` が UI スレッドのコンテキストを取り込む。
内側の後続が UI スレッドで待たされるため、外側の `await` の後には一度も到達しなかった。
自分のコードに付けても、呼び出し先の `await` が付いていないまま UI スレッドの上で中断すれば効かない。

### Task.Run の中の Dispatcher.Invoke

`Task.Run(() => LoadAsync()).Result` は戻った。
`Task.Run` に渡した処理の中では `SynchronizationContext.Current` が `null` だったため、`LoadAsync` の後続もスレッドプールで実行される。
ただし、待った後に画面を更新しようとして `Dispatcher.Invoke` を呼ぶ `LoadWithInvokeAsync` を包んで `Task.Run(() => LoadWithInvokeAsync()).Result` とすると、止まったままになった。

```csharp
private async Task<int> LoadWithInvokeAsync()
{
    await Task.Delay(300);
    Dispatcher.Invoke(() => StatusText.Text = "loaded"); // ここから戻らない
    return 42;
}
```

`await` の後はスレッドプールで実行されたが、`Dispatcher.Invoke` から戻らなかった。
`Dispatcher.Invoke` は UI スレッドでの実行を同期的に待つので（[Dispatcher.Invoke](https://learn.microsoft.com/ja-jp/dotnet/api/system.windows.threading.dispatcher.invoke)）、`.Result` で止まっている UI スレッドを待つことになる。
`Task.Run` で逃がす方法は、包んだ処理が UI スレッドを同期的に呼ばない場合に限って成り立つ。

---

## 効く条件

`ConfigureAwait(false)` を付けた行のうち、戻ったのは `LoadAllConfigureAwaitAsync`・`LoadFirstSuspendConfigureAwaitAsync`・`LoadCachedFirstAllConfigureAwaitAsync` の 3 つである。
`LoadFirstSuspendConfigureAwaitAsync` は、実際に中断する 1 つ目の `await` にだけ付け、2 つ目には付けていない。

```csharp
private static async Task<int> LoadFirstSuspendConfigureAwaitAsync()
{
    await Task.Delay(300).ConfigureAwait(false); // ここで中断し、後続はスレッドプールで動く
    await Task.Delay(300);                        // UI スレッドのコンテキストはもう無い
    return 42;
}
```

1 つ目の `await` の後では `SynchronizationContext.Current` が `null` になっており、2 つ目の `await` が取り込む UI スレッドのコンテキストは無かった。
戻った 3 つと止まった 2 つを分けたのは、**UI スレッドで実行されている間に、`ConfigureAwait(false)` の付いていない `await` が中断するかどうか**である。
止まった `LoadCachedFirstAsync` と `LoadOuterAsync` では、どちらもそのような `await` が UI スレッドの上で中断していた。

ただし、この条件を満たしているかは、呼び出し先のどの `await` が実際に中断するかで決まる。
キャッシュの当たり外れのように実行時の状態で変わるため、コードを読んで保証するのは難しい。
公式の説明は、すべての `await` に付けることを、パフォーマンスのうえでのベストプラクティスであり、呼び出し元がブロックしたときのデッドロックを防ぐのに役立つものとして勧めている。
すべてに付けておけば、どの `await` が中断するかを見極める必要もなくなる。

`Task.Run` で逃がす方法は、包んだ処理が `Dispatcher.Invoke` のように UI スレッドを同期的に待つ呼び出しを含まない場合に戻った。

どの条件で戻った場合も、待っている間は UI スレッドが止まる。
冒頭で積んだ処理は、Click ハンドラが戻った後まで実行されなかった。
`.Result` で待つかぎり、UI スレッドは待ち時間のあいだ、積まれた処理を実行しない。

待っている間も UI スレッドが止まらなかったのは、`async void` の Click ハンドラで `await` した場合だけだった。
`await` は UI スレッドを止めずに Click ハンドラから抜けるため、冒頭で積んだ処理はすぐに実行された。
`await` の後の処理は UI スレッドに戻って実行されるので、そのまま画面を更新できる。

---

## 実装例

`.Result` を使わず、イベントハンドラを `async void` にして `await` する。
イベントハンドラは戻り値を返せないため、`async void` を使ってよい数少ない場所である（[非同期の戻り値の型](https://learn.microsoft.com/ja-jp/dotnet/csharp/asynchronous-programming/async-return-types)）。

```csharp
private async void OnLoadClick(object sender, RoutedEventArgs e)
{
    LoadButton.IsEnabled = false;
    try
    {
        int value = await LoadAsync();
        StatusText.Text = value.ToString(); // await の後は UI スレッドなので、そのまま更新できる
    }
    finally
    {
        LoadButton.IsEnabled = true;
    }
}
```

`await` している間も UI スレッドは処理を進めるため、ボタンが再び押される可能性がある。
処理中はボタンを無効にして、二重に実行されないようにしている。
`async void` のハンドラから漏れた例外は `Application.DispatcherUnhandledException` に届く（[WPF の DispatcherUnhandledException で捕まらない例外 — 別スレッド・Task・async void の届き先](/ja/articles/wpf-unhandled-exception-dispatcher-task-thread/)）。
処理ごとにエラーを表示したい場合は、`await` を `try` / `catch` で囲む。

`.Result` が出てくるのは、多くの場合、呼び出しの途中に同期メソッドが挟まっているときである。
その場合は同期メソッドを `async Task` に変え、呼び出し元を順に `await` へ書き換えて、イベントハンドラまで `async` を通す。

```csharp
// 変更前: 同期メソッドの中で待っている
public void Refresh()
{
    Items = _repository.LoadAsync().Result;
}

// 変更後: async を呼び出し元まで通す
public async Task RefreshAsync()
{
    Items = await _repository.LoadAsync();
}
```

`RefreshAsync` を呼ぶ側も `await` で待つ。
途中の 1 か所でも `.Result` に戻すと、その場所で同じように固まる。

ライブラリを書く側では、`await` の後の処理が元のコンテキストを必要としないメソッドのすべての `await` に `ConfigureAwait(false)` を付ける。
画面に触れないメソッドでも、`await` の後で呼び出し元から渡されたコールバックやイベントを実行するなら、それが UI スレッドを前提にしていないかを先に確かめる。
公式の説明も、元のコンテキストで再開する必要がある場合を除外している。
`ConfigureAwait(false)` を付けることは、呼び出し元が `.Result` で待ったときに固まる経路を減らすが、待っている間に呼び出し元の UI スレッドが止まることは防げない。
アプリケーション側の対策は、あくまで `await` で待つことである。

---

## 注意点

- `ConfigureAwait(false)` を付けた `await` が中断した場合、後続は元のコンテキストへ戻されない。どのスレッドで続くかは `Task` の完了のしかたによる（実測の `Task.Delay` ではスレッドプールだった）。完了済みの `Task` に付けた場合は中断しないので、UI スレッドのまま続く。画面を更新するコードの前の `await` には付けない。
- 呼び出し先のライブラリが `ConfigureAwait(false)` を付けているかは、ソースを読まなければ分からない。一部の `await` にだけ付いている場合、完了済みの `Task` を返す分岐があると、付いていない後続の `await` が UI スレッドのコンテキストを取り込む。`.Result` が戻ることを、呼び出し先の実装に頼らない。
- `Task.Run(...).Result` は戻るが、待っている間は UI スレッドが止まる。包んだ処理に `Dispatcher.Invoke` で画面を同期的に更新するコードを足した時点で固まる。
- `Wait(TimeSpan)` でタイムアウトを付けても、待っている間は UI スレッドが止まる。タイムアウトの後も非同期の処理は止まらず、後続は UI スレッドが空いた後に実行される。
- `.GetAwaiter().GetResult()` も、固まるかどうかについては `.Result` と同じ結果だった。
- バックグラウンドスレッドから `Dispatcher.Invoke` で UI を更新するコードは、コレクションの更新であっても、UI スレッドが `.Result` で待っていれば同じ理由で戻らなくなる。バックグラウンドスレッドからコレクションを更新する方法は [WPF で ObservableCollection をバックグラウンドスレッドから更新するとクロススレッド例外が発生する問題の解決方法](/ja/articles/wpf-observablecollection-cross-thread-update/) で扱っている。

---

## まとめ

UI スレッドで `.Result`・`.Wait()`・`.GetAwaiter().GetResult()` を呼ぶと、待っている間は必ず UI スレッドが止まる。
そのうえで、未完了の `await` が UI スレッドのコンテキストを取り込んでいれば、後続が UI スレッドの空きを待ち続けて、戻らなくなる。
`LoadAllConfigureAwaitAsync` のように戻る場合でも、待ち時間のあいだ UI スレッドが止まることは変わらない。
測った範囲では、`ConfigureAwait(false)` を付けて戻ったのは、付いていない `await` が UI スレッドの上で中断しない場合だった。
完了済みの `Task` の `await` や外側の `await` にだけ付けたものは効かなかった。
`Task.Run` で逃がす方法も、中で `Dispatcher.Invoke` を呼んだ時点で固まる。

WPF のアプリケーションでは、イベントハンドラを `async void` にして `await` し、`async` を呼び出し元まで通す方法を選ぶ。
`ConfigureAwait(false)` は、`await` の後の処理が元のコンテキストを必要としないライブラリのコードで、すべての `await` に付けるものとして扱う。
`Task.Run(...).Result` は、同期メソッドを要求するインターフェイスを実装するなど書き換えができない場合に限り、包む処理が UI スレッドを同期的に呼ばないことと、待っている間に画面が止まってよいことを確かめたうえで使う。

---

## 関連記事

- [WPF の DispatcherUnhandledException で捕まらない例外 — 別スレッド・Task・async void の届き先](/ja/articles/wpf-unhandled-exception-dispatcher-task-thread/)
- [WPF で ObservableCollection をバックグラウンドスレッドから更新するとクロススレッド例外が発生する問題の解決方法](/ja/articles/wpf-observablecollection-cross-thread-update/)
