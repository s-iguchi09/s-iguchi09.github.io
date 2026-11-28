---
layout: article-ja
title: "WPF の ShutdownMode と MainWindow: ログイン画面を閉じるとアプリが終了する原因"
date: 2026-11-28
category: WPF
image: /images/articles/wpf-login-window-startup-app-exits/login-startup-exit-matrix.svg
excerpt: "OnStartup でログイン画面を ShowDialog した後にメイン画面を Show すると、例外なしにアプリが終わる。OnMainWindowClose やメイン画面の先行生成が失敗する条件と、ログイン中は OnExplicitShutdown にする書き方を実測で示す。"
---

## 概要

WPF でログイン画面を先に出すには、`App.xaml` の `StartupUri` を外し、`OnStartup` でログイン画面を `ShowDialog` してからメイン画面を `Show` する形がよく使われる。
この形をそのまま書くと、ログイン画面で認証を通した直後にアプリが終了し、メイン画面は一度も表示されない。
例外は出ず、`Show()` も何事もなく戻るため、コードを読んでも原因が見えにくい。

原因は `ShutdownMode` ごとに分かれる。
既定の `OnLastWindowClose` では、ログインの間はログイン画面がアプリの最後のウィンドウになる。
`OnMainWindowClose` に変えても、最初に生成されたログイン画面が `MainWindow` になる。
どちらの場合も、ログイン画面を閉じて `ShowDialog` が戻った時点で、終了の処理はすでに始まっている。

本記事では、この 2 つの原因と、`ShutdownMode` の説明から導かれる対処がどの条件で失敗するかを実測で確かめる。
最後に、ログインの間だけ `ShutdownMode` を `OnExplicitShutdown` にする書き方を MVVM で実装する。

---

## 前提・対象環境

- フレームワーク: .NET 5 以降の WPF を想定している。挙動を測ったのは下記の検証環境だけで、.NET 5〜9 と .NET Framework 4.8 では測っていない
- 言語: コード例は null 許容参照型の注釈（`object?`）を使うため C# 8 以降が要る
- アーキテクチャ: MVVM。ビューモデルの基底クラス `ObservableObject`（値が変わったときに `true` を返す `SetProperty` を持つ）と `RelayCommand` は下記の自前の最小実装で、外部のライブラリは使わない。ビューのコードビハインドは `InitializeComponent` だけにし、ウィンドウを組み立てる処理は `App` の `OnStartup` に置く
- 検証環境: .NET 10 / Windows 11。構成ごとに別のプロセスとして検証用アプリを起動し、`Application.Exit` の発生とプロセスの終了を記録した
- 題材: ログイン画面でユーザー名を入れてログインし、メイン画面に「ログイン中のユーザー」を表示する

---

## 問題

ログイン画面のビューモデルは、ウィンドウを参照せずに「閉じたい」ことを結果付きで知らせる。
この形は [MVVM でダイアログを出す記事](/ja/articles/wpf-mvvm-dialog-service/)の子ウィンドウと同じである。

ビューモデルの基底クラス `ObservableObject` は、値が変わったときだけ `PropertyChanged` を発生させて `true` を返す `SetProperty` を持つ。
ボタンのコマンドには、実行する処理だけを受け取る最小の `RelayCommand` を使う。
本記事のボタンは常に押せるため、`CanExecute` は常に `true` を返す。

```csharp
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace LoginSample
{
    public abstract class ObservableObject : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
    }

    public sealed class RelayCommand : ICommand
    {
        private readonly Action _execute;

        public RelayCommand(Action execute) => _execute = execute;

        public event EventHandler? CanExecuteChanged { add { } remove { } }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => _execute();
    }
}
```

`CanExecuteChanged` は発生させないので、購読を受け付けるだけの空のアクセサーにした。
押せる条件を持つコマンドが要る場合は、[CanExecute の記事](/ja/articles/wpf-relaycommand-canexecute-not-updating/)の実装を使う。

ログイン画面とメイン画面のビューモデルは次のとおりである。
メイン画面のビューモデルは、ログインしたユーザー名を表示用の文字列にするだけである。

```csharp
using System;
using System.Windows.Input;

namespace LoginSample
{
    public interface ICloseRequest
    {
        event EventHandler<bool>? CloseRequested;
    }

    public sealed class LoginViewModel : ObservableObject, ICloseRequest
    {
        private string _userName = "";

        public LoginViewModel()
        {
            LoginCommand = new RelayCommand(() => CloseRequested?.Invoke(this, true));
            CancelCommand = new RelayCommand(() => CloseRequested?.Invoke(this, false));
        }

        public string UserName
        {
            get => _userName;
            set => SetProperty(ref _userName, value);
        }

        public ICommand LoginCommand { get; }

        public ICommand CancelCommand { get; }

        public event EventHandler<bool>? CloseRequested;
    }

    public sealed class MainViewModel
    {
        public MainViewModel(string userName) => Greeting = $"{userName} でログイン中";

        public string Greeting { get; }
    }
}
```

認証そのものは本記事の主題ではないため、`LoginCommand` はそのまま `true` を知らせている。
実際のアプリでは、認証に成功したときだけ `CloseRequested` を `true` で発生させる。

`App.xaml` からは `StartupUri` を外し、`OnStartup` でログイン画面を出す。
ログイン画面の `CloseRequested` を受けて `DialogResult` を設定するのは、ウィンドウを作った `App` の側である。

```csharp
protected override void OnStartup(StartupEventArgs e)
{
    base.OnStartup(e);

    var loginViewModel = new LoginViewModel();
    var login = new LoginWindow { DataContext = loginViewModel };

    void OnCloseRequested(object? sender, bool result) => login.DialogResult = result;
    loginViewModel.CloseRequested += OnCloseRequested;
    bool? result = login.ShowDialog();
    loginViewModel.CloseRequested -= OnCloseRequested;

    if (result != true)
    {
        Shutdown();
        return;
    }

    var main = new MainWindow { DataContext = new MainViewModel(loginViewModel.UserName) };
    main.Show();
}
```

このコードでログインすると、`ShowDialog` は `true` を返し、`main.Show()` まで進む。
それでもメイン画面は表示されず、`Application.Exit` が発生してプロセスが終わる。
`Show()` は例外を投げず、呼んだ直後の `main.IsVisible` は `false` だった（下の表の「OnLastWindowClose（既定）、ログイン後に Show」の行）。

---

## よく紹介される対処

`ShutdownMode` の既定値は `OnLastWindowClose` で、最後のウィンドウが閉じたときにアプリが終了する（[Application.ShutdownMode](https://learn.microsoft.com/dotnet/api/system.windows.application.shutdownmode)）。
この説明からは、次の 2 つの対処が導かれる。

- **`ShutdownMode` を `OnMainWindowClose` にする。** メイン画面が閉じたときだけ終了させれば、ログイン画面を閉じても終わらない、という考え方である
- **メイン画面を先に生成しておく。** ログイン画面が閉じた時点でメイン画面のインスタンスが残っていれば、「最後のウィンドウ」にはならない、という考え方である

もう 1 つ、`StartupUri` を残したまま `OnStartup` でログイン画面を出し、メイン画面の生成を `StartupUri` に任せる書き方もある。

これらの構成と、本記事で推奨する構成を、同じログイン画面とメイン画面で動かした結果が次の表である。

{% include tables/articles/wpf-login-window-startup-app-exits/login-startup-exit-matrix.ja.md %}

構成ごとに別のプロセスとして検証用アプリを起動し、`OnStartup` の中身だけを変えて記録した。.NET 10 / Windows 11 で実測。表を作るシーンを 2 回実行し（各構成を 1 回ずつ起動する）、2 回の表が一致することを確かめた。ログイン画面の操作は、表示から 0.8 秒後に、ビューモデルの `LoginCommand` を実行するか、取り消しの行では `CancelCommand` を実行するか、「× で閉じる」の行ではウィンドウへ `WM_CLOSE` を送って代えた。「ShowDialog() の戻り値」の列は `bool?` を文字列にした値（`True` / `False`）である。最初のメイン画面の表示から 1.5 秒後に、開いているメイン画面の数を記録してから、すべて閉じた。`MainWindow` の列は `Application.MainWindow` の型名で、「ログイン中」はログイン画面の `Loaded` で、「ログイン画面を閉じた後」は `ShowDialog` が戻った直後に、「Show() 後」は `Show()` の直後に読んだ。「ShowDialog() 直後の終了処理」は、`ShowDialog` が戻った直後に `Application` の内部の静的プロパティ `IsShuttingDown` をリフレクションで読んだ値である。「メイン画面」の列の生成の有無は、メイン画面のコンストラクタで記録した。「生成されない」はコンストラクタが呼ばれなかったこと、「表示されない」は生成されたが `Loaded` が発生しなかったことを示す。「代入を省き、…」「代入し、…」の 2 行は推奨形から変えたもので、「OnExplicitShutdown → OnLastWindowClose」の 2 行は `MainWindow` を代入している。「別の Window を生成」は、メイン画面より前に `Window` を 1 つ生成し、表示しないままにした。「Application.Exit のきっかけ」は、`ShowDialog` の直後に終了処理が始まっていた場合を、その後の `Shutdown()` の呼び出しの有無にかかわらず「ログイン画面を閉じたこと」とし、それ以外は `Exit` より前に記録された `Shutdown()` の呼び出しかメイン画面を閉じる操作とした。「—」は、その構成では `Show()` を呼ばないか、その前に処理が止まったこと（例外の列では、例外が起きなかったこと）を示す。例外は `DispatcherUnhandledException` で記録し、処理済みにした。10 秒たっても終わらないプロセスは「終わらない」とし、強制終了した。`StartupUri` を残す 2 行は、`StartupUri` をコードで `MainWindow.xaml` に設定した。
{: .table-caption}

---

## なぜ効かないのか

### OnLastWindowClose では、ログイン画面が最後のウィンドウになる

`OnStartup` の中でログイン画面を `ShowDialog` している間、アプリが持つウィンドウはログイン画面だけである。
ログイン画面が閉じた時点で残りのウィンドウは 0 になり、`OnLastWindowClose` の条件が成立する。
表の「OnLastWindowClose（既定）、ログイン後に Show」の行では、`ShowDialog` が戻った直後には終了処理がすでに始まっていた（「ShowDialog() 直後の終了処理」の列）。
その後の `Show()` は例外を投げずに戻り、直後の `IsVisible` は `false` だった。

### OnMainWindowClose では、ログイン画面が MainWindow になる

`Application.MainWindow` には、そのアプリで最初に生成された `Window` が自動的に入る（[メイン アプリケーション ウィンドウの取得と設定](https://learn.microsoft.com/dotnet/desktop/wpf/windows/how-to-get-set-main-application-window)）。
`OnStartup` で最初に生成するのはログイン画面なので、ログイン中の `MainWindow` はログイン画面である（表の「ログイン中の MainWindow」の列）。
`OnMainWindowClose` は `MainWindow` が閉じたときに終了するため、表の「起動時から OnMainWindowClose」の行でも、`ShowDialog` の直後には終了処理が始まっていた。
既定の構成と同じく、続く `Show()` の直後の `IsVisible` は `false` だった。

### StartupUri のメイン画面は、生成されても表示されない

表の「StartupUri を残し、OnStartup でログイン」の行でも、`ShowDialog` の直後には終了処理が始まっていた。
`StartupUri` のメイン画面は生成されたが（「メイン画面」の列が「生成されない」ではなく「表示されない」）、`Loaded` は発生せず、そのまま終了した。
既定の `ShutdownMode` のままなので、原因は既定の構成と同じである。

### メイン画面の先行生成は、取り消したときに Shutdown() を呼び忘れるとプロセスが残る

メイン画面を先に生成しておく構成は、ログインに成功したときは期待どおりに動いた。
生成済みのメイン画面が残っているため、ログイン画面を閉じても最後のウィンドウにはならず、`MainWindow` も最初に生成されたメイン画面になる。

注意が要るのはログインを取り消したときである。
表の「ログイン前にメイン画面を生成、取り消し、Shutdown() なし」の行では、`ShowDialog` が `false` を返したところで `OnStartup` を抜けると、表示していないメイン画面のインスタンスが残った。
`OnLastWindowClose` の「最後のウィンドウが閉じたとき」は、そのウィンドウを閉じるまで成立しない。
`Application.Exit` は発生せず、プロセスは 10 秒たっても終わらなかった。
非表示のウィンドウがプロセスを引き止める仕組みは、[ウィンドウを閉じてもプロセスが終了しない原因の記事](/ja/articles/wpf-application-not-exiting-shutdownmode-threads/)で切り分けている。

取り消したときに `Shutdown()` を呼べば、この構成でもプロセスは終わる（表の「ログイン前にメイン画面を生成、取り消し → Shutdown()」の行）。
ただしこの形では、ログインの結果を知る前にメイン画面を生成し、取り消しの経路では明示的な `Shutdown()` に頼ることになる。
このため、ログインの間は自動の終了そのものを止め、終了の経路をすべて `Shutdown()` に寄せるほうが、終わる条件をコードから読み取りやすい。

---

## 効く条件

ログインの間に自動の終了を起こさなければ、ログイン画面が最後のウィンドウや `MainWindow` であっても問題にならない。
`OnExplicitShutdown` は、`Shutdown()` を呼んだときにだけ終了するモードである。

- ログイン画面を出す前に `ShutdownMode` を `OnExplicitShutdown` にする
- ログインに成功したら、メイン画面を `MainWindow` に代入し、`ShutdownMode` を `OnMainWindowClose` に切り替えてから、メイン画面を `Show` する
- ログインを取り消したら、`Shutdown()` を呼んで `OnStartup` を抜ける

表の「OnExplicitShutdown → OnMainWindowClose（推奨）」の行では、メイン画面は表示から 1.5 秒後も開いており、メイン画面を閉じたことをきっかけに `Application.Exit` が発生した。
「推奨形、取り消し → Shutdown()」と「推奨形、× で閉じる（WM_CLOSE）→ Shutdown()」の行では、どちらも `ShowDialog` が `false` を返し、`Shutdown()` を呼んだ時点で `Application.Exit` が発生してプロセスが終わった。

### OnLastWindowClose ではなく OnMainWindowClose に切り替える理由

ログインの後に `OnLastWindowClose` に切り替えても、ウィンドウがメイン画面だけなら同じように終わる（表の「OnExplicitShutdown → OnLastWindowClose」の行）。
違いが出るのは、表示していない別のウィンドウが残っている場合である。
表の「OnExplicitShutdown → OnLastWindowClose、別の Window を生成」の行では、メイン画面の前に `Window` を 1 つ生成しただけ（表示はしない）で、メイン画面を閉じても `Application.Exit` が発生せず、プロセスが残った。
同じ条件でも、`OnMainWindowClose` に切り替えた「代入し、メイン画面の前に別の Window を生成」の行では、メイン画面を閉じたときに終了した。
メイン画面を閉じたらアプリを終える、という意図をそのまま表すのは `OnMainWindowClose` である。

### MainWindow を代入する理由

表の「推奨形から MainWindow の代入を省く」の行も、推奨形と同じ結果になった。
`MainWindow` だったログイン画面を閉じると `MainWindow` は `null` に戻り（表の「ログイン画面を閉じた後の MainWindow」の列）、次に生成したメイン画面が自動で `MainWindow` になったためである。

それでも代入は省かない。
自動の設定は生成の順序で決まるためである。
表の「代入を省き、メイン画面の前に別の Window を生成」の行では、`MainWindow` は先に生成した `Window` になり（「Show() 後の MainWindow」の列）、メイン画面を閉じても `Application.Exit` は発生せず、プロセスが残った。
代入した「代入し、メイン画面の前に別の Window を生成」の行では、メイン画面を閉じたときに終了した。

---

## 実装例

`App` の `OnStartup` は次のとおりである。
ログイン画面を開いて結果を受け取る部分は、メソッドに分けた。

```csharp
using System.Windows;

namespace LoginSample
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // ログインの間は、ウィンドウが閉じても終了させない。
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            if (!TryLogin(out string userName))
            {
                Shutdown();
                return;
            }

            var main = new MainWindow { DataContext = new MainViewModel(userName) };
            MainWindow = main;
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            main.Show();
        }

        private static bool TryLogin(out string userName)
        {
            var viewModel = new LoginViewModel();
            var login = new LoginWindow { DataContext = viewModel };

            void OnCloseRequested(object? sender, bool result) => login.DialogResult = result;
            viewModel.CloseRequested += OnCloseRequested;
            try
            {
                bool? result = login.ShowDialog();
                userName = viewModel.UserName;
                return result == true;
            }
            finally
            {
                viewModel.CloseRequested -= OnCloseRequested;
            }
        }
    }
}
```

`ShowDialog` の戻り値は `bool?` なので、`== true` で比べて `bool` にする。
ログイン画面を × で閉じた場合は誰も `DialogResult` を設定しないが、`ShowDialog` は `false` を返し（表の「推奨形、× で閉じる（WM_CLOSE）→ Shutdown()」の行）、取り消しと同じ経路を通る。
`CloseRequested` の購読は `finally` で外し、閉じた後のログイン画面とビューモデルのつながりを残さない。
`App.xaml` の `x:Class` には、C# 側の名前空間を含めた `LoginSample.App` を書き、同じ partial クラスにする。

`App.xaml` には `StartupUri` を書かない。

```xml
<Application x:Class="LoginSample.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
</Application>
```

`StartupUri` が無いので、起動時にどのウィンドウを出すかは `OnStartup` だけが決める。
`StartupUri` を残したときの結果は、`ShutdownMode` で変わる。
既定の `ShutdownMode` のままなら、表の「StartupUri を残し、OnStartup でログイン」の行のとおり、メイン画面は生成されても表示されないまま終了する。
この推奨形で残すと、終了はしないが、表の「推奨形で StartupUri を残す」の行のとおり、`StartupUri` のメイン画面と `OnStartup` で表示したメイン画面が 2 つとも開く。

ログイン画面の XAML は、入力欄と 2 つのボタンをビューモデルにバインドするだけである。

```xml
<Window x:Class="LoginSample.LoginWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="ログイン" Width="260" Height="150">
  <StackPanel Margin="12">
    <TextBox Text="{Binding UserName, UpdateSourceTrigger=PropertyChanged}" />
    <StackPanel Orientation="Horizontal" HorizontalAlignment="Right" Margin="0,8,0,0">
      <Button Content="ログイン" Command="{Binding LoginCommand}" Width="70" />
      <Button Content="キャンセル" Command="{Binding CancelCommand}" Width="70" Margin="8,0,0,0" />
    </StackPanel>
  </StackPanel>
</Window>
```

ログイン画面のコードビハインドは `InitializeComponent` だけである。
閉じる操作はビューモデルの `CloseRequested` を経由し、`App` が `DialogResult` に変える。

メイン画面の XAML は、ビューモデルの `Greeting` を表示するだけである。

```xml
<Window x:Class="LoginSample.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="メイン" Width="300" Height="160">
  <TextBlock Margin="12" Text="{Binding Greeting}" />
</Window>
```

メイン画面のコードビハインドも `InitializeComponent` だけである。
`DataContext` は `OnStartup` で `MainViewModel` を設定するため、XAML では設定しない。

---

## 注意点

- **`Show()` の戻りでは失敗を検出できない。** 終了処理が始まった後の `Show()` は例外を投げずに戻った。メイン画面が出ない症状を調べるときは、`Show()` の直後に `IsVisible` を読むか、`Application.Exit` イベントのハンドラー（または `OnExit` のオーバーライド）にブレークポイントを置く
- **`ShutdownMode` は `ShowDialog` より前に変える。** 表の「ShowDialog が戻った後に OnExplicitShutdown を設定」の行では、終了処理がすでに始まっていたため、`ShutdownMode` の設定そのものが `InvalidOperationException` になった。例外で `OnStartup` の残りが実行されず、メイン画面は生成されないまま終了した
- **`OnExplicitShutdown` のまま切り替え忘れると、メイン画面を閉じても終わらない。** 表の「ログイン後も OnExplicitShutdown のまま」の行では、メイン画面を閉じても `Application.Exit` が発生せず、プロセスが残った。`OnMainWindowClose` に切り替える行を、メイン画面の `Show` と組にして書く
- **ログイン画面を `Show`（モードレス）で出す構成は扱っていない。** `ShowDialog` と違って `OnStartup` の処理が止まらないため、ログイン画面の閉じるイベントからメイン画面を出す別の設計になる

---

## まとめ

ログイン画面を閉じた直後にアプリが終わるのは、`ShutdownMode` が既定の `OnLastWindowClose` ならログイン画面が最後のウィンドウになり、`OnMainWindowClose` に変えてもログイン画面が `MainWindow` になるためである。
どちらでも、ログイン画面を閉じて `ShowDialog` が戻った時点で終了処理が始まっており、続く `Show()` は表示しないまま戻る。
メイン画面を先に生成する方法は、取り消したときに `Shutdown()` を呼び忘れるとプロセスが残る。

ログイン画面を先に出すアプリでは、ログインの間だけ `ShutdownMode` を `OnExplicitShutdown` にする。
ログインに成功したらメイン画面を `MainWindow` に代入して `OnMainWindowClose` に切り替え、取り消したら `Shutdown()` を呼ぶ。
この構成なら、成功・取り消し・× のいずれでも、終了するタイミングがコードに書いたとおりになる。

---

## 関連記事

- [WPF でウィンドウを閉じてもプロセスが終了しない原因の切り分けと ShutdownMode・フォアグラウンドスレッドの扱い](/ja/articles/wpf-application-not-exiting-shutdownmode-threads/)
- [WPF MVVM でダイアログを出す: IDialogService の最小実装と Owner・戻り値の落とし穴](/ja/articles/wpf-mvvm-dialog-service/)
