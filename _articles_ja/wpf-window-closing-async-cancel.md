---
layout: article-ja
title: "WPF の Window.Closing で await すると e.Cancel が効かない理由と、先に止めて閉じ直す実装"
date: 2026-11-11
category: WPF
image: /images/articles/wpf-window-closing-async-cancel/closing-cancel-after-await.svg
excerpt: "Window.Closing で保存を await してから e.Cancel を立てても、ハンドラーが完了していない await で戻った直後に WPF が Cancel を読むため閉じる。先に止めて後で閉じ直す実装と、再入・同期完了・Shutdown の落とし穴を実測で示す。"
---

## 概要

未保存の変更があるウィンドウを閉じるとき、`Closing` で保存を `await` し、失敗したら `e.Cancel = true` にして閉じるのを止める。
この書き方は、保存に時間がかかると効かない。
`Cancel` を立てる前にウィンドウは閉じ、最後のウィンドウであればプロセスも終わり、保存の続きは実行されないまま消える。
一方、判定が待たずに `false` を返す場合は同じコードで止まる（`await` より前に同期の確認ダイアログで決まる場合などがこれに当たる）ため、手元の確認だけでは気付きにくい。

本記事では、WPF のソースから `Closing` の後で `Cancel` が読まれる時点を確かめ、そこから「先に止めて、判定の後で閉じ直す」実装を導く。
閉じ直すときに踏む 4 つの落とし穴（`Closing` の繰り返し、`Closing` の中での `Close` の例外、判定中の × の再入、`ShowDialog` の戻り値が `false` になること）と、`Application.Shutdown` などでは判定そのものが効かないことも、検証用のアプリで測る。

---

## 前提・対象環境

- フレームワーク: .NET 6 以降 / WPF
- 検証環境: .NET 10（10.0.13）/ Windows 11
- アーキテクチャ: MVVM（判定はビューモデルに置き、`Closing` の処理は添付プロパティにまとめる）
- 比較に使うライブラリ: CommunityToolkit.Mvvm 8.4.0（「現象」の節の書き方だけで使う）

計測は、条件ごとに検証用のアプリを別のプロセスで起動して行った。
× ボタンを押したときと同じ `WM_CLOSE` をウィンドウに送り（条件によっては、代わりに `Application.Shutdown` を呼ぶ、別のウィンドウを閉じる、終了ボタンを UI オートメーションで押す、`DialogResult` を設定する）、`Closing`・`Closed`・判定の開始と完了・未処理の例外を、起きた順にログへ書かせて読んだ。
.NET Framework と、OS のサインアウトやシャットダウンで閉じる場合は測っていない。

---

## 現象

よく見る書き方では、`Closing` をビューモデルのコマンドへ渡し、判定を `await` した後で `Cancel` を立てる。
コマンドには CommunityToolkit.Mvvm の `AsyncRelayCommand<T>` を使う。

```csharp
using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;

public sealed class EditorViewModel
{
    public IAsyncRelayCommand<CancelEventArgs> ClosingCommand { get; }

    public EditorViewModel()
    {
        ClosingCommand = new AsyncRelayCommand<CancelEventArgs>(async e =>
        {
            if (!await CanCloseAsync())
            {
                e!.Cancel = true; // 閉じてほしくないときだけ止める
            }
        });
    }

    // 保存や確認ダイアログを行い、閉じてよければ true を返す。
    public Task<bool> CanCloseAsync() => /* 省略 */;
}
```

ビューでは、`Closing` を受け取ってコマンドの `Execute` に `CancelEventArgs` を渡すだけの添付プロパティ（`ClosingEvent.Command`）で結び付ける。

```csharp
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace Sample;

public static class ClosingEvent
{
    public static readonly DependencyProperty CommandProperty =
        DependencyProperty.RegisterAttached(
            "Command", typeof(ICommand), typeof(ClosingEvent),
            new PropertyMetadata(null, OnCommandChanged));

    public static ICommand? GetCommand(Window window) => (ICommand?)window.GetValue(CommandProperty);

    public static void SetCommand(Window window, ICommand? value) => window.SetValue(CommandProperty, value);

    private static void OnCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var window = (Window)d;
        window.Closing -= OnClosing;
        if (e.NewValue is not null)
        {
            window.Closing += OnClosing;
        }
    }

    private static void OnClosing(object? sender, CancelEventArgs e) => GetCommand((Window)sender!)?.Execute(e);
}
```

`OnClosing` は `Execute` を呼んで戻るだけで、コマンドの処理の完了は待たない。
XAML では、この添付プロパティにコマンドをバインドする。

```xml
<Window x:Class="Sample.EditorWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:local="clr-namespace:Sample"
        Title="Editor" Width="320" Height="200"
        local:ClosingEvent.Command="{Binding ClosingCommand}">
  <TextBox />
</Window>
```

判定が `false`（閉じない）を返すようにして × を押した結果が、次の表の 1・2 行目である。
3・4 行目は、後述の実装（`ClosingGuard`）に置き換えた結果である。

{% include tables/articles/wpf-window-closing-async-cancel/closing-cancel-after-await.ja.md %}

.NET 10.0.13 / Windows 11 で、表示から 200 ms 後に `WM_CLOSE` を送り、表示から 1.5 秒後のウィンドウの状態を読んだ。判定は保存や確認ダイアログの代わりに、`Task.Delay(300)` を待ってから結果を返すか、待たずに返す（同期で完了する）。「判定の開始 / 完了」は判定のメソッドの先頭と、結果を返すか例外を投げる直前に書いたログの回数である。各条件 1 回の記録。
{: .table-caption}

**判定に 300 ms かかる 1 行目では、判定が完了する前に `Closed` が発生し、ウィンドウは閉じた。**
最後のウィンドウだったのでプロセスも終わり、判定の完了は記録されなかった。
保存の処理であれば、その途中でアプリが終わることになる。
**判定がすぐに完了する 2 行目では、同じコードで閉じるのを止められた。**

---

## 内部で何が起きているか

### Closing のハンドラーが戻った直後に Cancel を読む

[WPF の Window.cs](https://github.com/dotnet/wpf/blob/release/10.0/src/Microsoft.DotNet.Wpf/src/PresentationFramework/System/Windows/Window.cs) の `WmClose` は、`WM_CLOSE` を受けると次の順に処理する（要点だけを抜き出した。コメント・先頭のガード節・例外処理は `// ...` で省き、`else` の分岐は早期 return の形に整えた）。

```csharp
private bool WmClose()
{
    // ...
    _isClosing = true;

    CancelEventArgs e = new CancelEventArgs(false);
    OnClosing(e);                      // Closing のハンドラーを呼ぶ（例外処理は省略）

    if (ShouldCloseWindow(e.Cancel))   // 戻った直後に Cancel を読む
    {
        CloseWindowFromWmClose();
        return false;
    }

    _isClosing = false;
    _dialogResult = null;              // 閉じるのを止めると DialogResult を null に戻す
    return true;
}
```

`Cancel` を読むのは、`OnClosing` が戻った直後の 1 回だけである。
その後で `Cancel` を書き換えても、読む側はもういない。

`async` のメソッドは、完了していない `Task` を初めて `await` した時点で呼び出し元に戻る。
`AsyncRelayCommand<T>.Execute` は、`ExecuteAsync` が返した `Task` を待たずに戻る（`ICommand.Execute` は `void` で、`Task` を呼び出し元に返せない）。
このため、判定を `await` したところで `Execute` が戻り、添付プロパティのハンドラーが戻り、`OnClosing` が戻る。
このとき `Cancel` はまだ `false` で、ウィンドウは閉じる。
`Cancel = true` の行が実行されるのは、判定が終わって続きが UI スレッドで再開されたときであり、それは `Cancel` が読まれた後である。

判定が同期で完了した 2 行目では、`await` で戻らずにそのまま続きが実行され、`Cancel = true` は `OnClosing` の中で書かれる。
**効くかどうかは「`Cancel` を書く前に、完了していない `await` があるか」で決まり、`async` を付けたかどうかでは決まらない。**

### Closing の処理中は Close を呼べない

`WmClose` は `OnClosing` を呼ぶ前に `_isClosing` を `true` にし、閉じるのを止めた場合は戻る前に `false` に戻す。
`Close` が呼ぶ `InternalClose` は最初に `VerifyNotClosing` を呼び、`_isClosing` が `true` なら `InvalidOperationException` を投げる。
[Closing のリファレンス](https://learn.microsoft.com/ja-jp/dotnet/api/system.windows.window.closing)にも、閉じている途中に `Close` を呼ぶと `InvalidOperationException` になると書かれている。

### Shutdown の最中は Cancel を見ない

`ShouldCloseWindow` は、`Cancel` だけでなく、アプリの終了処理の途中かどうかも見る。

```csharp
private bool ShouldCloseWindow(bool cancelled)
{
    return ((!cancelled) || (_appShuttingDown) || (_ignoreCancel));
}
```

`Application.Shutdown` は、各ウィンドウを `InternalClose(true, true)` で閉じる（[Application.cs](https://github.com/dotnet/wpf/blob/release/10.0/src/Microsoft.DotNet.Wpf/src/PresentationFramework/System/Windows/Application.cs) の `DoShutdown`）。
このとき `_appShuttingDown` と `_ignoreCancel` の両方が `true` になり、`Cancel` を立てても閉じる。
`ShutdownMode` が `OnMainWindowClose` のときは、メインウィンドウが閉じた後に、残ったウィンドウが同じ `Shutdown` の処理で閉じられる。
メインウィンドウ自身の `Closing` は通常の `WmClose` を通るので、そこで立てた `Cancel` は効く（ソースからの帰結で、測っていない）。
リファレンスにも、`Shutdown` を呼ぶと各ウィンドウの `Closing` は発生するが、キャンセルは無視されると書かれている。

---

## 経路から導ける帰結

### 先に Cancel を立て、判定の後で閉じ直す

`Cancel` が読まれる前に結果を決められない以上、最初の `await` より前に `Cancel = true` にして、いったん閉じるのを止めるしかない。
判定が「閉じてよい」を返したら、改めて `Close` を呼ぶ。
この閉じ直しには、次の表の 3 つの落とし穴がある（4 つ目は次の小見出しで扱う）。

{% include tables/articles/wpf-window-closing-async-cancel/closing-close-again.ja.md %}

測り方は前の表と同じである。判定はいずれも `true`（閉じてよい）を返す。「× を 2 回」の行は、1 回目の 100 ms 後（判定の途中）に 2 回目の `WM_CLOSE` を送り、2 回目の判定が完了するかを読むため、アプリの `ShutdownMode` を `OnExplicitShutdown` にして閉じた後もプロセスを残した。最後の行は、1 回目の判定が 300 ms 後に `IOException` を投げ、2 回目の判定は `true` を返す。1 回目の `WM_CLOSE` の 600 ms 後に 2 回目を送った。各条件 1 回の記録。
{: .table-caption}

**1 行目: 閉じ直した `Close` でも `Closing` が発生する。**
同じハンドラーがもう一度 `Cancel` を立てて判定を始めるので、判定が `true` でもウィンドウは閉じず、表示から 1.5 秒までの間、`Closing` と判定が繰り返された（回数は判定にかかる時間やタイマーの精度で変わる）。
判定を終えて閉じてよいと決まったことを覚えておき、2 回目の `Closing` では何もしないようにする必要がある。

**2・3 行目: 判定が同期で完了すると、`Close` が `Closing` の中で呼ばれる。**
判定に 300 ms かかる 2 行目では、`Close` は `Closing` が終わった後に呼ばれるので閉じる。
判定がすぐに完了する 3 行目では、`await` の続きが `OnClosing` の中で実行され、`_isClosing` が `true` のまま `Close` が呼ばれて `InvalidOperationException` になった。
`async void` のハンドラーから漏れた例外は `Dispatcher` に投げ直されるため、`DispatcherUnhandledException` に届き（計測では `Handled` にしている）、ウィンドウは開いたまま残った。
保存するものが無いときだけ例外になるので、これも手元の確認では見落としやすい。
4 行目のように `Dispatcher.InvokeAsync` で `Close` を後回しにすると、`Closing` が終わってから呼ばれるので、同期で完了しても閉じる。

**5 行目: 判定の途中で × がもう一度押されると、判定が 2 回始まる。**
2 回目の `Closing` でも `Cancel` は立てる必要がある（立てなければ、`OnClosing` が戻った時点で `Cancel` が `false` のままなので、「内部で何が起きているか」の経路のとおり閉じる）。
ただし判定をもう一度始めると、保存が 2 回走り、1 回目の判定で閉じた後も 2 回目の判定が動き続ける。
判定中であることを覚えておき、`Cancel` だけ立てて戻れば、6 行目のように判定は 1 回で済む。

7 行目は、後述の `ClosingGuard` で判定が例外を投げた場合である。
例外は `DispatcherUnhandledException` に届き（計測では `Handled` にしている）、ウィンドウは開いたまま残った。
もう一度 × を押すと判定をやり直し、2 回目の判定が `true` を返した後に閉じた（`Closed` は 2 回目の判定の完了後にだけ発生した）。

### ShowDialog では DialogResult が失われる

`WmClose` は、閉じるのを止めたときに `DialogResult` を `null` に戻す（「内部で何が起きているか」の抜粋）。
このため `ShowDialog` で開いたウィンドウでは、OK ボタンで `DialogResult = true` にしても、判定のためにいったん止めて `Close` で閉じ直すと、`ShowDialog` は `false` を返す。
`null` のまま閉じたダイアログの戻り値は `false` になる（`Window.cs` の `DoDialogHide` が `null` を `false` に置き換える）。

{% include tables/articles/wpf-window-closing-async-cancel/closing-dialog-result.ja.md %}

.NET 10.0.13 / Windows 11 で、`ShowDialog` で開いたウィンドウに、表示から 200 ms 後に `DialogResult = true` を設定した（`WM_CLOSE` は送らない）。判定は 300 ms 後に `true` を返す。`ShowDialog` の戻り値をログに書くため、アプリの `ShutdownMode` を `OnExplicitShutdown` にした。2 行目は「先に Cancel を立て、判定の後で閉じ直す」の表の 2 行目と同じ、`Close()` で閉じ直す実装である。各条件 1 回の記録。
{: .table-caption}

閉じ直すときに、止める前の `DialogResult` を設定し直せば、3 行目のように `true` が返る。
モーダルのウィンドウでは、`DialogResult` を設定するとウィンドウが閉じる。

### Shutdown や Owner の終了で閉じるウィンドウは止められない

`Application.Shutdown` を呼ぶと、後述の `ClosingGuard` を付けたウィンドウでも止められない。
`Shutdown` を経由するほかの経路と、`Shutdown` を経由しない `Owner` の終了も測った。
終了ボタンを置く場合は、`Shutdown` ではなく × と同じ `Close` の経路に通す。

{% include tables/articles/wpf-window-closing-async-cancel/closing-shutdown.ja.md %}

測り方は最初の表と同じで、`WM_CLOSE` は送らない。1 行目は `Application.Current.Shutdown()` を呼んだ。2 行目はアプリの `ShutdownMode` を `OnMainWindowClose` にし、`ClosingGuard` の無い別のウィンドウを `MainWindow` にして閉じた。3 行目は `ClosingGuard` の無い別のウィンドウを `Owner` にして閉じた。4・5 行目は `Command="Close"`（`ApplicationCommands.Close`）を割り当てた終了ボタンを UI オートメーションの `Invoke` で押した。2 行目以外の `ShutdownMode` は既定の `OnLastWindowClose` である。各条件 1 回の記録。
{: .table-caption}

**`Shutdown` の 1 行目では、`ClosingGuard` が `Cancel` を立てても、ウィンドウは閉じてプロセスが終わり、判定は完了しなかった。**
`OnMainWindowClose` でメインウィンドウを閉じた 2 行目も同じ結果だった。
**`Owner` を閉じた 3 行目では、`Closing` 自体が発生せず、判定は始まらないまま閉じた。**
[Closing のリファレンス](https://learn.microsoft.com/ja-jp/dotnet/api/system.windows.window.closing)にも、`Owner` が閉じると所有されたウィンドウの `Closing` は発生しないと書かれている。
Win32 の [DestroyWindow](https://learn.microsoft.com/ja-jp/windows/win32/api/winuser/nf-winuser-destroywindow) は、所有ウィンドウを `WM_CLOSE` ではなく `WM_DESTROY` を送って、オーナーより先に破棄する。
計測でも、所有されたウィンドウのウィンドウプロシージャに `WM_CLOSE` は届かなかった（`HwndSource.AddHook` で記録した）。
このため `WmClose` を通らず、`Closing` が発生しない。
終了ボタンから `ApplicationCommands.Close` を経てウィンドウの `Close` を呼ぶ 4・5 行目では、× と同じ判定を通り、`false` なら開いたまま残った。
`ShutdownMode` が `OnLastWindowClose` なので、最後のウィンドウが閉じればアプリも終わる。
`ShutdownMode` とプロセスの終了の関係は、[WPF でウィンドウを閉じてもプロセスが終了しない原因の切り分けと ShutdownMode・フォアグラウンドスレッドの扱い](/ja/articles/wpf-application-not-exiting-shutdownmode-threads/)で扱っている。

---

## 実装例

判定はビューモデルに、閉じ直しの手順は添付プロパティにまとめる。
ビューモデルが実装するインターフェイスは、閉じてよいかを返すメソッド 1 つである。

```csharp
namespace Sample;

public interface IClosingGuard
{
    // 閉じてよければ true を返す。保存や確認ダイアログはここで行う。
    Task<bool> CanCloseAsync();
}
```

添付プロパティ `ClosingGuard.IsEnabled` を `True` にしたウィンドウは、`Closing` のたびに `DataContext` の `IClosingGuard` に判定を求める。
ウィンドウごとの状態（判定中・判定済み）を非公開の添付プロパティに持たせ、前節の 4 つの落とし穴をそれぞれ避ける。

```csharp
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace Sample;

public static class ClosingGuard
{
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled", typeof(bool), typeof(ClosingGuard),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(Window window) => (bool)window.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(Window window, bool value) => window.SetValue(IsEnabledProperty, value);

    // ウィンドウごとの状態。判定中か、判定を終えて閉じてよいと決まったか。
    private enum State { Idle, Checking, Approved }

    private static readonly DependencyProperty StateProperty =
        DependencyProperty.RegisterAttached(
            "State", typeof(State), typeof(ClosingGuard),
            new PropertyMetadata(State.Idle));

    private static readonly CommandBinding CloseBinding =
        new(ApplicationCommands.Close, (sender, _) => ((Window)sender).Close());

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var window = (Window)d;
        window.Closing -= OnClosing;
        window.CommandBindings.Remove(CloseBinding);

        if ((bool)e.NewValue)
        {
            window.Closing += OnClosing;
            // 終了ボタンなどの Command="Close" も、× と同じ Close の経路に通す。
            window.CommandBindings.Add(CloseBinding);
        }
    }

    private static async void OnClosing(object? sender, CancelEventArgs e)
    {
        var window = (Window)sender!;
        var state = (State)window.GetValue(StateProperty);

        if (state == State.Approved || window.DataContext is not IClosingGuard guard)
        {
            return; // 判定を終えた 2 回目の Close は、そのまま閉じる
        }

        e.Cancel = true; // 最初の await より前に止める
        if (state == State.Checking)
        {
            return; // 判定中にもう一度押された × は無視する
        }

        // Cancel すると WPF が DialogResult を null に戻すので、閉じ直すときのために控えておく。
        bool? dialogResult = window.DialogResult;
        window.SetValue(StateProperty, State.Checking);
        bool canClose = false;
        try
        {
            canClose = await guard.CanCloseAsync();
        }
        finally
        {
            window.SetValue(StateProperty, canClose ? State.Approved : State.Idle);
        }

        if (canClose)
        {
            // Closing の処理中に Close を呼ぶと例外になるため、Closing が終わってから閉じ直す。
            _ = window.Dispatcher.InvokeAsync(() =>
            {
                if (dialogResult is bool result)
                {
                    window.DialogResult = result; // ShowDialog の戻り値を戻す（設定すると閉じる）
                }
                else
                {
                    window.Close();
                }
            });
        }
    }
}
```

`State.Approved` は「先に Cancel を立て、判定の後で閉じ直す」の表の 1 行目の繰り返しを、`Dispatcher.InvokeAsync` は 3 行目の例外を、`State.Checking` は 5 行目の二重の判定を防ぐ。
`dialogResult` を控えて設定し直すのは、「ShowDialog では DialogResult が失われる」の表の 2 行目を防ぐためである。
非モーダルのウィンドウでは `DialogResult` は `null` のままなので、`Close` で閉じ直す。

判定が例外を投げた場合は、`finally` で `Idle` に戻してから例外が `async void` の外へ漏れる。
`DispatcherUnhandledException` で `Handled` にしていれば、閉じるのは止めたままなので、「先に Cancel を立て、判定の後で閉じ直す」の表の 7 行目のとおり、もう一度 × を押せば判定をやり直せる。
`Handled` にしなければ、WPF の既定の動作で未処理の例外としてアプリが終了する（[DispatcherUnhandledException のリファレンス](https://learn.microsoft.com/ja-jp/dotnet/api/system.windows.application.dispatcherunhandledexception)の記述による。本記事では測っていない）。
保存の失敗が想定できる場合は、`CanCloseAsync` の中で例外を捕まえて `false` を返すほうがよい。
`async void` の例外の届き先は、[WPF の DispatcherUnhandledException で捕まらない例外 — 別スレッド・Task・async void の届き先](/ja/articles/wpf-unhandled-exception-dispatcher-task-thread/)で実測している。

ビューは添付プロパティを `True` にし、終了ボタンには `Close` コマンドを割り当てる。

```xml
<Window x:Class="Sample.EditorWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:local="clr-namespace:Sample"
        Title="Editor" Width="320" Height="200"
        local:ClosingGuard.IsEnabled="True">
  <DockPanel>
    <Button DockPanel.Dock="Bottom" Content="Exit" Command="Close" />
    <TextBox />
  </DockPanel>
</Window>
```

コードビハインドは `InitializeComponent` だけでよい。
ビューモデルは `IClosingGuard` を実装し、`CanCloseAsync` で保存や確認を行う。

```csharp
public sealed class EditorViewModel : IClosingGuard
{
    public bool IsDirty { get; private set; }

    public async Task<bool> CanCloseAsync()
    {
        if (!IsDirty)
        {
            return true; // 待たずに返る（同期で完了する）
        }

        return await SaveAsync(); // 保存できたときだけ閉じる
    }

    private Task<bool> SaveAsync() => /* 省略 */;
}
```

検証では、`ClosingGuard` と `IClosingGuard` を上のコードのまま使い、`CanCloseAsync` だけを、`Task.Delay(300)` を待つか待たずに返すもの、または 1 回目に `IOException` を投げるものに置き換えた。
各表の「ClosingGuard」の行と、`Shutdown` の表の全行がその結果である。

---

## 注意点

- **`ClosingGuard` のほかに `Closing` を止める処理を置かない前提である。** 閉じ直しの `Close` をほかのハンドラーが止めると、状態は `Approved` のまま残り、以後の × は判定を通らずに閉じる（コードからの帰結で、測っていない）。
- **アプリのコードから `Shutdown` を呼ぶと、判定の結果は効かない。** `ClosingGuard` は `Shutdown` による `Closing` でも判定を始めるが、`Shutdown` の表の 1 行目のとおり、判定は完了しないまま打ち切られる。終了の操作は `Shutdown` ではなくウィンドウの `Close` に通し、`ShutdownMode` を `OnLastWindowClose` にしてアプリを終わらせる。`OnMainWindowClose` では、メインウィンドウが閉じた時点で、残ったウィンドウの判定は効かない（同じ表の 2 行目）。
- **`Owner` を閉じると、所有されたウィンドウは判定を通らずに閉じる。** `Closing` が発生しないためである（`Shutdown` の表の 3 行目）。所有されたウィンドウに未保存の変更を持たせる場合は、`Owner` 側の判定でまとめて確かめる必要がある（この構成は測っていない）。
- **OS のサインアウトやシャットダウンは、本記事では測っていない。** [リファレンス](https://learn.microsoft.com/ja-jp/dotnet/api/system.windows.window.closing)は、この場合は `Closing` が発生しないとし、`Application.SessionEnding` を処理するよう案内している。

---

## まとめ

`Closing` のハンドラーで判定を `await` してから `Cancel` を立てる書き方は、判定が同期で完了するときしか効かない。
WPF は `Closing` のハンドラーが戻った直後に `Cancel` を 1 回だけ読み、`async` のハンドラーは最初の完了していない `await` で戻るためである。

非同期の判定で閉じるのを止めたい場合は、次の順にする。

1. 最初の `await` より前に `Cancel = true` で止める。
2. 判定の後、閉じてよければ `Dispatcher.InvokeAsync` で `Close` を呼び直す（`Closing` の中での例外を避ける）。
3. 判定済みと判定中をウィンドウごとに覚え、閉じ直しの `Closing` は通し、判定中の × は無視する。
4. `ShowDialog` で開いたウィンドウでは、止める前の `DialogResult` を控え、閉じ直すときに設定し直す。

この手順は添付プロパティにまとめておけば、ビューモデルは `CanCloseAsync` を実装するだけで済む。
ただし、`Application.Shutdown` ではすべてのウィンドウの、`OnMainWindowClose` ではメインウィンドウの後に残ったウィンドウの `Cancel` が無視され、`Owner` を閉じると所有されたウィンドウでは `Closing` 自体が発生しない。
終了の操作は `Shutdown` ではなくウィンドウの `Close` に通し、`OnLastWindowClose` でアプリを終える。

---

## 関連記事

- [WPF でウィンドウを閉じてもプロセスが終了しない原因の切り分けと ShutdownMode・フォアグラウンドスレッドの扱い](/ja/articles/wpf-application-not-exiting-shutdownmode-threads/)
- [WPF の DispatcherUnhandledException で捕まらない例外 — 別スレッド・Task・async void の届き先](/ja/articles/wpf-unhandled-exception-dispatcher-task-thread/)
- [WPF で CommunityToolkit.Mvvm の AsyncRelayCommand が投げた例外の届き先と FlowExceptionsToTaskScheduler](/ja/articles/wpf-asyncrelaycommand-exception-handling/)
