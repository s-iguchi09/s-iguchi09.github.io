---
layout: control-demo
permalink: /ja/apps/wpf-standard-control-demo/button.html
title: "Button"
badge: "Inputs"
lead: "Button は、クリックされると Click イベントか Command にバインドしたコマンドで処理を実行するボタンです。ClickMode、IsPressed、コマンド関連のプロパティは基底クラスの ButtonBase が持っています。"
description: "WPF の Button を .NET 10 で実測して解説。UserControl やダイアログでの IsCancel と IsDefault、実際のマウスとキーでの ClickMode、CanExecute が呼び直される時点と回数を確かめます。"
---

## 概要

**Button** は `ButtonBase` を継承し、`ButtonBase` は `ContentControl` を継承しています。そのため `Content` には文字列、画像、パネルを置けます。既定のテンプレートは内容を `ContentPresenter` で表示し、その `RecognizesAccessKey` は `True` です。`Content="_Save"` では `S` がアクセスキーになり、UI オートメーションの名前は `Save` でした。

既定のテンプレートには表示状態のグループ（VisualStateGroup）がありません。見た目は、`IsDefaulted`・`IsMouseOver`・`IsPressed`・`IsChecked`（いずれも `true`）と `IsEnabled` = `false` のトリガーで切り替えています。

デモアプリには `IsCancel` と `IsDefault`、`ClickMode`、`IsPressed`、`Command`、`CommandParameter` の欄があります。各欄の下の「Show Code」リンクで、その欄の XAML を表示できます。

## 画面キャプチャ

![button demo screen](/images/wpf-standard-control-demo/button.png){: .screenshot-img}

## デモしているプロパティ

以下のプロパティが WPF 標準コントロールデモアプリでインタラクティブにデモされています。各プロパティの挙動は .NET 10 で実測したものです（[実測した挙動](#measured-behavior)を参照）。

| プロパティ | 設定値 | 説明 |
| --- | --- | --- |
| `IsCancel` | `bool` | <kbd>Esc</kbd> でボタンを押せるようにします。デモアプリはボタンを `UserControl` の中に置いており、そこでも動きます。UserControl の中の TextBox にフォーカスがある状態で、<kbd>Esc</kbd> でボタンが押されました。ウィンドウが閉じるかどうかは、ウィンドウの開き方で変わります。`ShowDialog` で開いたウィンドウは <kbd>Esc</kbd> で閉じ、`ShowDialog` は `false` を返しました。`Show` で開いたウィンドウでは、ボタンは押されましたがウィンドウは開いたままでした。1 つのウィンドウに `IsCancel` のボタンが 2 つあると、<kbd>Esc</kbd> ではどちらも押されませんでした。フォーカスが 1 つ目のボタンへ移り、もう一度押すと 2 つ目へ移りました。 |
| `IsDefault` | `bool` | <kbd>Enter</kbd> でボタンを押せるようにします。TextBox にフォーカスがあると <kbd>Enter</kbd> で押され、そのときの `IsDefaulted` は `True` でした。`AcceptsReturn="True"` の TextBox にフォーカスがあると、<kbd>Enter</kbd> は改行になり、ボタンは押されませんでした。ほかのボタンにフォーカスがあると、<kbd>Enter</kbd> ではフォーカスのあるボタンが押され、`IsDefaulted` は `False` でした。 |
| `ClickMode (ButtonBase)` | `Release / Press / Hover` | どの時点でクリックとするかです。実際のマウスで試すと、既定値の `Release` はマウスボタンを離した時点でクリックになりました。離す前にポインターをボタンの外へ出すと、`IsPressed` が `False` になり、クリックになりませんでした。`Press` はマウスボタンを押した時点でクリックになりました。`Hover` は、マウスボタンを押さなくても、ポインターがボタンに入った時点でクリックになりました。`Hover` のボタンはキーボードでは押せず、<kbd>Space</kbd> でも <kbd>Enter</kbd> でも何も起きませんでした。ほかの 2 つでは、<kbd>Space</kbd> は `Release` ならキーを離した時点、`Press` ならキーを押した時点でクリックになり、<kbd>Enter</kbd> はどちらもキーを押した時点でクリックになりました。 |
| `IsPressed (ButtonBase)` | `bool (ReadOnly)` | ボタンが押されている最中かどうかで、デモアプリはボタンの横に表示しています。ボタンの上でマウスボタンを押している間と、<kbd>Space</kbd> を押している間は `True` で、離すと `False` でした。`Hover` のボタンは、ポインターが上にある間押された状態でした。キーボードでは `Hover` のボタンは押された状態にならず、<kbd>Space</kbd> を押している間も `IsPressed` は `False` のままでした。 |
| `Command (ButtonBase)` | `ICommand` | クリックで実行するコマンドです。`CanExecute` が `false` を返す間、ボタンの `IsEnabled` は `False` でした。ボタンに `IsEnabled="True"` を設定しても同じです。デモアプリの `RelayCommand` は、`CanExecuteChanged` を `CommandManager.RequerySuggested` に委ねています。この場合、`CanExecute` の結果が変わっても、`CommandManager.InvalidateRequerySuggested` を呼ぶか、フォーカスの移動などで WPF が問い合わせ直すまで、ボタンは変わりませんでした。この問い合わせはボタンごとに行われます。同じコマンドをバインドしたボタンが 20 個あると、フォーカスの移動 1 回で `CanExecute` が 20 回呼ばれました。 |
| `CommandParameter (ButtonBase)` | `object` | `CanExecute` と `Execute` に渡す値です。デモアプリは、`Command` の後に TextBox の `Text` へバインドしています。この XAML では、読み込み時の 2 回の `CanExecute` にはどちらも `ShowMessageText` が渡り、`null` は渡りませんでした。パラメーターが変わると、すぐに問い合わせ直されます。コードからテキストを空にすると、`InvalidateRequerySuggested` を呼ばなくても `CanExecute` が 1 回呼ばれ、ボタンは無効になりました。`Execute` には、クリックした時点のテキストが渡りました。 |

## XAML 使用例

デモアプリ（`ButtonUsageControl.xaml`）の `CommandParameter` の欄から、スタイルと周囲の GroupBox を省き、名前空間の宣言を加えたものです。

```xml
<StackPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <TextBox x:Name="CommandParameterText" Text="ShowMessageText" />
  <Button x:Name="ClickWithParameterCommandButton"
          Command="{Binding ClickWithParameterCommand}"
          CommandParameter="{Binding Text, ElementName=CommandParameterText}"
          Content="ShowMessageBox" />
</StackPanel>
```

コマンドはビューモデル（`ButtonUsageViewModel.cs`）で定義しています。

```csharp
public ICommand ClickWithParameterCommand { get; } =
    new RelayCommand(param => MessageBox.Show(param?.ToString()), null);
```

## 主な使用例

- **ダイアログのボタン** — `ShowDialog` で開くウィンドウで、OK に `IsDefault`、キャンセルに `IsCancel` を設定します。
- **MVVM のコマンド** — ビューモデルの `ICommand` にバインドし、`CanExecute` で有効・無効を切り替えます。
- **一覧の行のボタン** — 項目のテンプレートにボタンを置き、その項目を `CommandParameter` で渡します。`CommandParameter="{Binding}"` にすると、2 行目のボタンを実際にクリックしたとき、`Execute` は `Row 2` を受け取りました。

## ヒントとベストプラクティス

- **`IsCancel` のボタンは 1 つのウィンドウに 1 つにする** — 2 つあると、<kbd>Esc</kbd> はその間でフォーカスを移すだけになります。
- **`Show` で開いたウィンドウは自分で閉じる** — `IsCancel` で閉じるのは、`ShowDialog` で開いたウィンドウだけです。
- **`CanExecute` は軽くする** — フォーカスが移るたびに、コマンドをバインドしたボタンの数だけ呼ばれます。
- **有効・無効は `CanExecute` で決める** — `IsEnabled` を設定しても `CanExecute` の結果は覆りません。
- **画像だけのボタンには `AutomationProperties.Name` を付ける** — 付けないと、UI オートメーションの名前は空でした。
- **キーボードで操作するボタンに `ClickMode="Hover"` を使わない** — <kbd>Space</kbd> でも <kbd>Enter</kbd> でも押せません。

## 実測した挙動 {#measured-behavior}

このページの挙動の記述は、すべて .NET 10 / Windows 11 で実際に動かして確かめたものです。計測には、このサイトのスクリーンショット生成ツールの [`ButtonDemoScene`](https://github.com/s-iguchi09/s-iguchi09.github.io/blob/main/tools/screenshot-capture/Scenes/ButtonDemoScene.cs){: target="_blank" rel="noopener noreferrer"} を使いました。`IsCancel` と `IsDefault` は WPF の入力管理（InputManager）の中で処理されるため、キーはキーボードから打ったときと同じく InputManager を通して送りました。`ClickMode` は、計測用のウィンドウの上で実際のマウスを動かし、クリックして確かめました。TextBox の文字を `CommandParameter` で渡すクリックは UI オートメーションの `Invoke` で、項目のテンプレートの行のボタンのクリックは実際のマウスで行いました。

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/button/button-keys.svg" alt="IsCancel と IsDefault の計測結果の表。UserControl の中でも Esc で IsCancel のボタンが押されてウィンドウは開いたまま、Enter で IsDefault のボタンが押され、AcceptsReturn の TextBox では改行になって押されず、ほかのボタンにフォーカスがあるとそのボタンが押され、IsCancel のボタンが 2 つあるとフォーカスが移るだけで、ShowDialog で開いたウィンドウは閉じて false を返す" width="889" height="290" loading="lazy">
  <figcaption>フォーカスの位置とウィンドウの開き方ごとの <code>IsCancel</code> と <code>IsDefault</code>。.NET 10 / Windows 11 で計測。</figcaption>
</figure>

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/button/button-clickmode.svg" alt="ClickMode の計測結果の表。既定値は Release で IsPressed は読み取り専用、Hover はポインターが入った時点、Press はマウスボタンを押した時点、Release は離した時点でクリックになり、外で離すとクリックにならず、キーボードでは Release は Space を離した時点、Press は押した時点、Enter はどちらも押した時点でクリックになり、Hover は何も起きない" width="1046" height="320" loading="lazy">
  <figcaption>実際のマウスとキーボードでの <code>ClickMode</code> と <code>IsPressed</code>。.NET 10 / Windows 11 で計測。</figcaption>
</figure>

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/button/button-command.svg" alt="Command の計測結果の表。CanExecute が false なら IsEnabled を True にしても無効、RequerySuggested のコマンドは InvalidateRequerySuggested の後に更新され、ボタン 20 個ではフォーカスの移動 1 回で CanExecute が 20 回呼ばれ、デモアプリの XAML では読み込み時の CanExecute にテキストが渡り、テキストを空にすると CanExecute が 1 回呼ばれ、Execute には現在のテキストが渡る。項目のテンプレートでは行のボタンの実際のクリックで CommandParameter=&quot;{Binding}&quot; によりその行の項目が渡る" width="1046" height="290" loading="lazy">
  <figcaption><code>Command</code> と <code>CommandParameter</code>。.NET 10 / Windows 11 で計測。</figcaption>
</figure>

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/button/button-template.svg" alt="Button の型とテンプレートの計測結果の表。ButtonBase と ContentControl を継承し、既定のテンプレートには表示状態がなく IsDefaulted・IsMouseOver・IsPressed・IsChecked・IsEnabled のトリガーがあり、ContentPresenter はアクセスキーを認識し、UI オートメーションの名前は文字列ならその文字列、_Save なら Save、画像だけなら空、AutomationProperties.Name を付ければその値になる" width="1140" height="290" loading="lazy">
  <figcaption>基底クラス、既定のテンプレート、UI オートメーションの名前。.NET 10 / Windows 11 で計測。</figcaption>
</figure>

## 関連コントロール・記事

- [RepeatButton](/ja/apps/wpf-standard-control-demo/repeatbutton.html) — 押している間クリックを繰り返す ButtonBase です。
- [ToggleButton](/ja/apps/wpf-standard-control-demo/togglebutton.html) — クリックすると押された状態のまま残る ButtonBase です。
- [WPF で RelayCommand の CanExecute がボタンの有効・無効に反映されない問題の解決方法](/ja/articles/wpf-relaycommand-canexecute-not-updating/) — ボタンが `CanExecute` に追従しない理由と、問い合わせ直す方法を扱います。
- [WPF の Label でアンダーバーが消える理由と回避方法](/ja/articles/wpf-label-underscore-issue/) — ボタンの内容にも当てはまるアクセスキーの挙動を扱います。

## ソースコード

このデモ画面のソースコードは GitHub で公開しています。アプリでは、各欄の下にある「Show Code」リンクでその欄の XAML を表示できます。

[GitHub で Button のソースコードを見る →](https://github.com/s-iguchi09/WPFStandardControlDemoApp/tree/main/src/WPFStandardControlDemoApp/Features/ButtonUsage){: target="_blank" rel="noopener noreferrer"}
