# 「エラーも出ずに壊れる WPF」付録サンプル

本の各章の**再現版**（問題が起きるコード）と**修正版**を、MVVM の形で収めたソリューション。
各章の内容は、サイトの記事の実測にもとづく。

## 実行環境

- Windows 11
- .NET 10 SDK

## 実行方法

リポジトリのルートで次を実行する。

```bash
dotnet run --project samples/book-a/src/BookA.Samples
```

章ごとのタブに、再現版と修正版を左右に並べて表示する。
両者は別のビューモデルを持つので、片方の操作はもう片方に影響しない。

## 構成

| プロジェクト | 中身 | 再現版と修正版の違い |
|---|---|---|
| `src/BookA.Mvvm` | 全章が使う最小の MVVM の基盤（`ObservableObject`・`RelayCommand`・`RelayCommand<T>`・`AsyncRelayCommand`）。外部のライブラリは使わない | — |
| `src/Ch01.BindingModeOmitted` | 第 1 章: Mode を書かないバインドが、ユーザー操作で黙って外れる | ビューの XAML（`Mode=TwoWay` の有無） |
| `src/Ch02.InputLimits` | 第 2 章: MaxLength などの入力制限が、コードやバインドから入る値に効かない | ビューモデル（値の検証と丸めをビューモデルで行うか） |
| `src/Ch03.DataGridCommitOnSave` | 第 3 章: DataGrid で編集中の値が保存ボタンで ViewModel に届かない | ビューの XAML（DataGrid に `PendingEdits.Host` を付けるか） |
| `src/Ch04.CanExecuteNotUpdating` | 第 4 章: RelayCommand の CanExecute がボタンの有効・無効に反映されない | ビューモデル（`CanExecuteChanged` を発行するか） |
| `src/Ch05.ValidationNotDisplayed` | 第 5 章: 入力検証のエラーが表示されない | ビューモデルとビュー（`INotifyDataErrorInfo` と ErrorTemplate） |
| `src/Ch06.StyleTriggerLocalValue` | 第 6 章: Style の Trigger・DataTrigger が効かない | ビューの XAML（背景の既定値をローカル値と Setter のどちらに置くか） |
| `src/Ch07.DataTemplateParentBinding` | 第 7 章: DataTemplate の中から親の DataContext にバインドできない | ビューの XAML（テンプレートの中で RelativeSource を使うか） |
| `src/Ch08.UserControlDependencyProperty` | 第 8 章: UserControl に定義した依存関係プロパティへ、中からバインドできない | カード（UserControl）の XAML（中の Grid に DataContext を委譲するか） |
| `src/Ch09.StaticResourceNotUpdating` | 第 9 章: StaticResource を変更しても画面が更新されない | ビューの XAML（StaticResource と DynamicResource のどちらで参照するか） |
| `src/Ch10.UnhandledExceptionRoutes` | 第 10 章: DispatcherUnhandledException で捕まらない例外 | ビューモデル（Task を待って try / catch で受けるか） |
| `src/Ch11.TaskResultDeadlock` | 第 11 章: Task.Result を呼ぶと画面が固まる | ビューモデル（Result で待つか、await で待つか） |
| `src/Ch12.ProcessNotExiting` | 第 12 章: ウィンドウを閉じてもプロセスが終了しない | ビューモデル（監視のスレッドに IsBackground を立てるか） |
| `src/BookA.Samples` | 起動用のアプリ | — |

各章のプロジェクトは、ビューモデル（`*ViewModel.cs`）と、再現版・修正版のビュー（`ReproView.xaml`・`FixedView.xaml`）を持つ。
ビューのコードビハインドには `InitializeComponent` しか書かない。
修正がビューにある章では、再現版と修正版は同じビューモデルのクラスを使い（インスタンスは別）、違いはビューの XAML だけにしてある。
修正がビューモデルにある章では、再現版と修正版でビューモデルのクラスが分かれ、ビューはほぼ同じにしてある。
添付プロパティ（第 3 章の `PendingEdits`、第 9 章の `ThemeResources`）と、再利用するコントロールの依存関係プロパティ（第 8 章のカード）は、画面の処理ではなく部品のコードとして章のプロジェクトに置く。

## 記事の実測と同じ動作であることの検証

各章のサンプルを動かし、結果が記事の表と同じになることを確かめている。
入力は 2 種類に分けている。

- **利用者の操作:** 記事の計測と同じ経路（実際のマウスとキーボード、または InputManager）で送る。
- **プログラムから入る値:** 記事が「コードからの代入」や「バインドした値」として測った値は、サンプルではビューモデルのコマンドやプロパティから入れる（第 2 章の「上限を超えたデータを読み込む」など）。コントロールに直接代入する記事の経路とは違い、比べているのは結果である。

各章のレポートの「記事の該当箇所」に、どの行と比べたかを書いている。
検証は `tools/screenshot-capture/Scenes/BookA/` のシーンで、結果は `verification/` に章ごとに書き出す。
リポジトリのルートで次を実行する（章の slug を並べると、その章だけを検証する）。

```bash
dotnet run --project tools/screenshot-capture -c Release -- book-a-ch01-binding-mode-omitted book-a-ch02-input-limits book-a-ch03-datagrid-commit-on-save book-a-ch04-canexecute-not-updating book-a-ch05-validation-not-displayed book-a-ch06-style-trigger-local-value book-a-ch07-datatemplate-parent-binding book-a-ch08-usercontrol-dependency-property book-a-ch09-staticresource-not-updating book-a-ch10-unhandled-exception-routes book-a-ch11-task-result-deadlock book-a-ch12-process-not-exiting
```

実行中はウィンドウが前面に出て、マウスカーソルが動く。
第 11 章と第 12 章は、UI スレッドが固まる・プロセスが終わらない挙動を測るため、計測ツール自身を子プロセスとして起動して外から監視する。
結果が記事の表と 1 か所でも食い違うと、レポートを書いたうえで失敗（終了コード 1）になる。
