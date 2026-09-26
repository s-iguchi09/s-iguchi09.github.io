---
layout: control-demo
permalink: /ja/apps/wpf-standard-control-demo/textbox.html
title: "TextBox"
badge: "Inputs"
lead: "TextBox は WPF の編集可能なテキスト入力で、1 行の入力欄にも、<code>AcceptsReturn</code> を使った複数行のテキストにも使います。"
description: "WPF の TextBox を .NET 10 で実測して解説します。MaxLength と CharacterCasing は入力にだけ効くこと、Wrap と WrapWithOverflow の違い、XAML の MinLines が表示直後に効かないことを確かめます。"
---

## 入力がバインドしたソースへ届くタイミング

**TextBox** は `TextBoxBase` を、`TextBoxBase` は `Control` を継承しています。`Text` プロパティは既定で TwoWay にバインドされ、既定の `UpdateSourceTrigger` は `LostFocus` です。つまり、バインドしたソースに値が届くのは、キーを押すたびではなく、TextBox がフォーカスを失ったときです。「abc」を 1 文字ずつ入力すると、`UpdateSourceTrigger=PropertyChanged` ではソースへの書き込みが 3 回あり、既定ではフォーカスが移るまで 1 回もありませんでした。キーを押すたびに絞り込む検索ボックスには `PropertyChanged` を使います。トリガーごとの違いは[WPF TextBox の UpdateSourceTrigger で入力がソースへ反映されるタイミングを制御する](/ja/articles/wpf-textbox-updatesourcetrigger-binding-timing/)で実測しています。

## 入力にだけ働くもの

いくつかのプロパティは、ユーザーが入力した文字にだけ働きます。規則ではなく入力の補助と考え、コードや ViewModel から入る値は別に検証します。

- **`MaxLength`** — 既定値の 0 は無制限で、負の値を設定すると `ArgumentException` が発生します。`MaxLength="5"` で "ABCDEFGH" と入力すると "ABCDE" になりました。コードやバインドで設定した文字列は、8 文字すべてが残りました。
- **`CharacterCasing`** — 既定値は `Normal` です。`Upper` で "hello" と入力すると "HELLO" になりましたが、コードから `Text = "hello"` と設定すると "hello" のままでした。
- **`AcceptsReturn`** — 既定値の `False` では、Enter キーを押しても文字列は変わりませんでした。`True` では改行（`\r\n`）が入りました。
- **`IsReadOnly`** — 入力した文字は無視されましたが、`SelectAll()` で文字列を選択でき、選択した状態ではコピーは実行でき、切り取りと貼り付けは実行できませんでした。ユーザーがコピーする可能性のあるログやエラーの詳細の表示に向きます。既定のテンプレートには `IsReadOnly` のトリガーがなく、背景と枠線は編集可能な TextBox と同じ色のままでした。編集できないことを見た目で示したい場合は、自分でスタイルのトリガーを加えます。コピーできる文字列を表示する他の方法との比較は、[WPFで編集不可のままテキストを選択・コピー可能に表示する方法](/ja/articles/wpf-selectable-readonly-text-display/)で扱っています。

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/textbox/textbox-input.svg" alt="キー入力と、コードやバインドで設定した文字列を比べた表。MaxLength 5 は入力を ABCDE で止めるがコードやバインドの文字列は切らず、CharacterCasing Upper は入力を大文字にするがコードの文字列は変えず、IsReadOnly は入力を無視するが選択はでき、Enter キーは AcceptsReturn のときだけ改行を入れる。PropertyChanged では入力の 1 文字ごとにソースへ書き込み、既定ではフォーカスが移るまで書き込まず、読み取り専用の TextBox は入力を受け付けずコピーは実行できる" width="975" height="440" loading="lazy">
  <figcaption>キー入力と、コードやバインドで設定した文字列の比較。.NET 10 / Windows 11 で計測。</figcaption>
</figure>

## 折り返し・配置・装飾

`TextWrapping` の既定値は `NoWrap` です。幅 150 の TextBox に 26 文字の単語を 2 つ並べると、`NoWrap` は 1 行のまま幅 385 になりました。`Wrap` は単語の途中でも折り返して 4 行になり、1 行目は 20 文字でした。`WrapWithOverflow` は単語の区切りでだけ折り返して 2 行になり、単語が右端からはみ出しました（表示幅 144 に対して 190.86）。パスや URL など途中で折り返したくない文字列には `WrapWithOverflow` を、すべてを幅に収めたい場合は `Wrap` を選びます。TextBox には `TextTrimming` プロパティはありません。これは `TextBlock` の機能です。

`TextAlignment` の既定値は `Left` です。幅 200 の TextBox に "123" を入れると、1 文字目の位置は `Left` で x = 3、`Center` で 90.3、`Right` で 177.59 でした。

`TextDecorations` は複数の線を組み合わせられ、`TextDecorations="Underline, Strikethrough"` は 2 つの装飾のコレクションに変換されました。`SelectionOpacity` の既定値は 0.4 です。

## 高さ：MinLines・MaxLines・スクロールバー

複数行の TextBox にも、既定ではスクロールバーは出ません。`VerticalScrollBarVisibility` と `HorizontalScrollBarVisibility` の既定値が `Hidden` のためです。高さ 60 の TextBox に 10 行を入れると内容の高さは 159.6 になりましたが、`VerticalScrollBarVisibility="Auto"` にするまでスクロールバーは出ませんでした。高さを制限しない `StackPanel` の中では、同じ TextBox は高さ 161.6 まで伸びました。メモやコメントの欄には、`AcceptsReturn="True"`・`TextWrapping="Wrap"`・`VerticalScrollBarVisibility="Auto"` を組み合わせます。

`MaxLines` は TextBox が広がる最大の行数で、既定値は `Int32.MaxValue` です。`AcceptsReturn`、`VerticalScrollBarVisibility="Auto"` で 10 行の文字列を入れると、`MaxLines` が 2・4・6 のときの高さは 33.92・65.84・97.77 になり、いずれも縦のスクロールバーが表示されました。

`MinLines` は TextBox の最小の行数で、既定値は 1 です。.NET 10 では、XAML のように表示前に設定した値は効きませんでした。空の TextBox に `MinLines="4"` を指定しても、高さは 1 行分（17.96）で、1・2・6 を指定した場合と同じでした。表示後に文字列が変わると効き、高さは 4 行分の 65.84 になりました。表示後に `MinLines` を設定するとすぐに効き、XAML で指定していなければ `Loaded` のハンドラーで設定しても効きました。XAML にも `MinLines="4"` があると、`Loaded` で 4 を設定し直しても値は変わらず、高さは 1 行分のままでした。最初の表示で効かせたいなら、XAML では指定せず、読み込み後に設定します。

`ScrollToEnd()` は、UI スレッドから呼ぶと末尾までスクロールしました（`VerticalOffset` 101.6、スクロールできる高さと同じ）。ワーカースレッドから呼ぶと `InvalidOperationException` が発生しました。別のスレッドから文字列を追加する場合は、`Dispatcher` を通して呼びます。

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/textbox/textbox-layout.svg" alt="TextBox のレイアウトの計測結果の表。NoWrap・Wrap・WrapWithOverflow の行数とはみ出し、MinLines と MaxLines による高さ（MinLines は表示後に文字列が変わるまで効かない）、高さ 60 の複数行 TextBox のスクロールバー、TextAlignment の位置、TextDecorations の組み合わせ、ワーカースレッドからの ScrollToEnd で InvalidOperationException が発生すること" width="1116" height="920" loading="lazy">
  <figcaption>折り返し、行数の制限、スクロールバー、配置、<code>ScrollToEnd</code>。.NET 10 / Windows 11 で計測。</figcaption>
</figure>

## デモアプリで試す

![デモアプリの TextBox のページ。左にコントロールの一覧、右に最初の節の Text](/images/wpf-standard-control-demo/textbox.png){: .screenshot-img}

デモアプリの TextBox のページには、`Text`、`TextWrapping`、`TextDecorations`、`TextAlignment`、`MaxLength`、`MaxLines`、`MinLines`、`CharacterCasing` の欄があります。いくつかの欄では 1 つの入力欄の文字列をバインドで複数のテキストボックスに流しており、同じ文字列で設定を比べられます。そのため、`MaxLength="5"` と `MaxLength="10"` の欄には上限を超えた文字列もそのまま表示され、`MinLines` の欄は最初はどれも 1 行の高さで、入力欄に文字を入れると広がります。各欄の下の「Show Code」リンクで、その欄の XAML を表示できます。次の XAML は、`Text` の欄（`TextBoxUsageControl.xaml`）から、スタイルと周囲の GroupBox を省き、名前空間の宣言を加えたものです。3 つのテキストボックスが、同じ `TextBlock` へ異なるタイミングで書き込みます。前の 2 つはテキストボックスから離れたときに、3 つ目はキーを押すたびに書き込みます。

```xml
<StackPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <TextBox x:Name="UpdateSourceTriggerDefaultTextBox"
           Text="{Binding Text, ElementName=UpdateSourceTrigger, UpdateSourceTrigger=Default}" />
  <TextBox x:Name="UpdateSourceTriggerLostFocusTextBox"
           Text="{Binding Text, ElementName=UpdateSourceTrigger, UpdateSourceTrigger=LostFocus}" />
  <TextBox x:Name="UpdateSourceTriggerPropertyChangedTextBox"
           Text="{Binding Text, ElementName=UpdateSourceTrigger, UpdateSourceTrigger=PropertyChanged}" />

  <TextBlock x:Name="UpdateSourceTrigger" />
</StackPanel>
```

## 関連するコントロールと記事

- [WPF TextBox の UpdateSourceTrigger で入力がソースへ反映されるタイミングを制御する](/ja/articles/wpf-textbox-updatesourcetrigger-binding-timing/) — トリガーごとにソースが更新されるタイミングを扱います。
- [WPF で TextBox の UpdateSource を View から呼び出すときの落とし穴と実装](/ja/articles/wpf-textbox-updatesource-from-view-pitfalls/) — ソースを明示的に更新する方法を扱います。
- [WPFで編集不可のままテキストを選択・コピー可能に表示する方法](/ja/articles/wpf-selectable-readonly-text-display/) — 選択できる読み取り専用の文字列を扱います。
- [PasswordBox](/ja/apps/wpf-standard-control-demo/passwordbox.html) — パスワード用の、伏せ字で表示する入力欄です。
- [TextBlock](/ja/apps/wpf-standard-control-demo/textblock.html) — 編集せずに文字列を表示します。

## ソースコードと計測の方法

このページの挙動の記述は、すべて .NET 10 / Windows 11 で実際に動かして確かめたものです。計測には、このサイトのスクリーンショット生成ツールの [`TextBoxDemoScene`](https://github.com/s-iguchi09/s-iguchi09.github.io/blob/main/tools/screenshot-capture/Scenes/TextBoxDemoScene.cs){: target="_blank" rel="noopener noreferrer"} を使いました。文字の入力は、キーボードと同じ `TextInput` の経路で文字を届ける `TextCompositionManager` で 1 文字ずつ、Enter キーは表示したウィンドウへキーのイベントを送って再現しています。このページの高さ・幅・位置は計測した環境での値で、フォントや表示スケールによって変わります。

[GitHub で TextBox のソースコードを見る →](https://github.com/s-iguchi09/WPFStandardControlDemoApp/tree/main/src/WPFStandardControlDemoApp/Features/TextBoxUsage){: target="_blank" rel="noopener noreferrer"}
