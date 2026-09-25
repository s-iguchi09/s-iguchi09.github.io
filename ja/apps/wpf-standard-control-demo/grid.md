---
layout: control-demo
permalink: /ja/apps/wpf-standard-control-demo/grid.html
title: "Grid"
badge: "Layout"
lead: "Grid は子要素を行と列に並べるパネルです。行の高さと列の幅は、固定値、内容に合わせる <code>Auto</code>、残りのスペースを比率で分ける <code>*</code> のいずれかで決めます。"
description: "WPF の Grid を、行と列の定義、Auto・固定値・* の列幅、Span、SharedSizeGroup、ZIndex の順に .NET 10 で実測した結果とデモアプリの画面で解説します。範囲外の番号を指定したときの配置や、* 列が比率を失う条件も扱います。"
---

## 概要

Grid は行を `RowDefinition`、列を `ColumnDefinition` で定義し、子要素は添付プロパティ `Grid.Row` と `Grid.Column`（0 始まり）でセルを選びます。3 種類のサイズ指定は組み合わせて使えます。幅 440 の Grid に `Auto` | `80` | `1*` | `2*` の 4 列を定義し、各列に幅 60 の内容を置くと、列幅は 60 / 80 / 100 / 200 になりました。`*` の 2 列は、残りの 300 を 1:2 で分けています。同じ Grid を幅 740 に広げると、変わったのは `*` の 2 列だけで、200 / 400 になりました。

`Grid.RowSpan` と `Grid.ColumnSpan` を使うと、1 つの子要素が複数のセルにまたがります。1 つのセルに複数の子要素を置くこともでき、その場合は重なって表示され、`Panel.ZIndex` で前後関係を決めます。`SharedSizeGroup` を使うと、別々の Grid の列（または行）の幅をそろえられます。フォームの各行を個別の Grid で組むときに、ラベル列の幅をそろえる方法です。

デモアプリでは、以下の各プロパティに専用の欄があり、入力欄で値を変えると結果がその場で変わります。欄ごとの「Show Code」リンクで、その欄の XAML も表示できます。

## 画面キャプチャ

![grid demo screen](/images/wpf-standard-control-demo/grid.png){: .screenshot-img}

## デモしているプロパティ

以下のプロパティが WPF 標準コントロールデモアプリでインタラクティブにデモされています。各プロパティの挙動は .NET 10 で実測したものです（[実測した挙動](#measured-behavior)を参照）。

| プロパティ | 設定値 | 説明 |
| --- | --- | --- |
| `Column / Row (Grid attached)` | `int` | 子要素を置く列・行の番号（0 始まり）です。既定値はどちらも 0 なので、指定しなかった子要素はすべて 0 行 0 列に置かれて重なります。定義した数を超える番号を指定してもエラーにはならず、最後の列・行に置かれます。2 × 2 の Grid で `Grid.Column="5" Grid.Row="5"` とすると、右下のセルに置かれました。デモアプリでも、2 × 2 の Grid で列番号に 2 を選べますが、文字は右の列にとどまります。負の値を設定すると `ArgumentException` が発生します。 |
| `ColumnSpan / RowSpan (Grid attached)` | `int` | `Grid.Column`・`Grid.Row` の位置から、指定した数の列・行にまたがって子要素を置きます。残りの列・行より大きい値はエラーにならず、定義された範囲で打ち切られます。3 列の Grid で `Grid.Column="1" Grid.ColumnSpan="5"` とすると 1 列目と 2 列目にまたがり、2 行の Grid で `RowSpan="5"` とすると 2 行にまたがりました。0 を設定すると `ArgumentException` が発生します。 |
| `ShowGridLines` | `bool` | 行と列の境界に線を引き、セルの大きさを確かめやすくします。線は内部の専用のビジュアル（`GridLinesRenderer`）が描き、Grid の公開プロパティのうち線に関係するのは `ShowGridLines` だけです。線の色・太さ・種類は変えられないので、デザインとして見せる罫線には `Border` を使います。 |
| `IsSharedSizeScope (Grid attached) / SharedSizeGroup (ColumnDefinition)` | `bool / string` | 同じグループ名を付けた列の幅を、別々の Grid の間でそろえます。共通の親要素に `Grid.IsSharedSizeScope="True"` を設定します。内容の幅が 50 と 120 の場合、2 つの `Auto` 列はスコープなしで 50 / 120、スコープありで 120 / 120 になりました。共有グループに入れた `*` 列は `Auto` と同じ扱いになり、残りのスペースを埋めずに 120 / 120 になりました。デモアプリでは、チェックボックスでスコープを切り替え、テキストボックスで各 Grid の内容を変えられます。 |
| `Width / MinWidth / MaxWidth (ColumnDefinition)` | `GridLength / double` | `Auto` は内容のうち最も広いものに合わせ、数値は固定幅、`*` は残りの幅を比率で分けます。`MinWidth` と `MaxWidth` は `Auto` 列にも効きます。内容の幅が 60 の `Auto` 列に `MaxWidth="30"` を設定すると、列幅は 30 になりました。値が矛盾するときは `MinWidth` が優先されます。デモアプリの初期値 `Width="25" MinWidth="30" MaxWidth="100"` では列幅は 30 になり、`MinWidth="100" MaxWidth="50"` では 100 になりました。`*` 列が比率を保つのは、Grid に有限の幅が与えられたときだけです（下の「ヒントとベストプラクティス」を参照）。 |
| `Height / MinHeight / MaxHeight (RowDefinition)` | `GridLength / double` | 行の高さも列と同じ規則で決まります。デモアプリの初期値（`Height="20" MinHeight="10" MaxHeight="100"`）では高さ 20 になりました。`Height="5" MinHeight="10"` では 10、内容の高さが 40 の `Auto` 行に `MaxHeight="15"` を設定すると 15 になりました。`ScrollViewer` の中で `Auto` 行を使っても問題はありません。高さ 120 の ScrollViewer に、高さ 100 の `Auto` 行を 3 つ持つ Grid を入れると、Grid の高さは 300 になり、縦のスクロールバーが表示されました。 |
| `Background (Panel)` | `Brush` | Grid の空いている部分がマウスに反応するかどうかを決めます。既定値の `null` では、Grid の空いている部分をヒットテストすると、背後の要素に当たりました。`Transparent` を設定すると、同じ位置で Grid に当たりました。色を塗らずに領域全体でクリックや `MouseEnter` を受けたい場合は `Transparent` を設定します。 |
| `ZIndex (Panel attached)` | `int` | 重なった子要素の前後関係を決めます。指定しない場合は後から追加した子要素が手前になり、先の要素に `ZIndex="1"` を設定すると手前に来ました。比べられるのは同じパネルの子要素どうしだけです。入れ子の Grid の中の要素に `ZIndex="100"` を設定しても、入れ子の Grid の後にある兄弟要素より手前には出ませんでした。入れ子の Grid 自体の `ZIndex` を上げると手前に出ました。 |

## XAML 使用例

デモアプリ（`GridUsageControl.xaml`）の `ColumnSpan` / `RowSpan` の欄から、スタイルと周囲の GroupBox を省き、名前空間の宣言を加えたものです。コンボボックスで選んだ値が、1 つ目と 2 つ目の TextBlock の Span にバインドされます。

```xml
<StackPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <ComboBox x:Name="ColumnSpanComboBox" SelectedIndex="0" SelectedValuePath="Content">
    <ComboBoxItem Content="1" />
    <ComboBoxItem Content="2" />
  </ComboBox>
  <ComboBox x:Name="RowSpanComboBox" SelectedIndex="0" SelectedValuePath="Content">
    <ComboBoxItem Content="1" />
    <ComboBoxItem Content="2" />
  </ComboBox>

  <Grid x:Name="ColumnSpanRowSpanResultGrid">
    <Grid.ColumnDefinitions>
      <ColumnDefinition />
      <ColumnDefinition />
      <ColumnDefinition />
    </Grid.ColumnDefinitions>
    <Grid.RowDefinitions>
      <RowDefinition />
      <RowDefinition />
    </Grid.RowDefinitions>
    <TextBlock Grid.Row="0" Grid.Column="0"
               Grid.ColumnSpan="{Binding SelectedValue, ElementName=ColumnSpanComboBox}"
               Background="CadetBlue" Text="1x1,ColumnSpanSetting" />
    <TextBlock Grid.Row="0" Grid.Column="2"
               Grid.RowSpan="{Binding SelectedValue, ElementName=RowSpanComboBox}"
               Background="SteelBlue" Text="1x3,RowSpanSetting" />
    <TextBlock Grid.Row="1" Grid.Column="0" Background="CornflowerBlue" Text="2x1" />
    <TextBlock Grid.Row="1" Grid.Column="1" Background="LightBlue" Text="2x2" />
  </Grid>
</StackPanel>
```

## 主な使用例

- **入力フォーム** — ラベル列を `Auto`、入力欄の列を `*` にすると、ラベルは内容の幅に収まり、入力欄が残りの幅を使います。
- **アプリの外枠** — ヘッダー行、サイドバーと本体を並べた行、フッター行を 1 つの Grid で組みます。
- **オーバーレイ** — 読み込み中の表示やメッセージを内容と同じセルに置き、`Panel.ZIndex` で手前に出して、`Visibility` のバインドで表示を切り替えます。
- **テンプレートで作る行の整列** — `ItemsControl` の各項目を個別の Grid にし、`SharedSizeGroup` で列をそろえます。

## ヒントとベストプラクティス

- **`*` 列には有限の幅が必要** — `StackPanel` や `ScrollViewer` の中でも `*` 列が 0 に潰れることはありませんが、比率が崩れることがあります。幅 60 と 30 の内容を持つ `1*` | `2*` の列は、横向きの `StackPanel` の中では内容の幅どおりの 60 / 30 になりました。縦向きの `StackPanel` は幅を子に渡すので、比率は保たれました（133.33 / 266.67）。内容より狭い `ScrollViewer` の中では、横スクロールの設定で結果が分かれました。横スクロールを許すと列は内容の幅になり、スクロールバーが出ました。横スクロールを無効にすると 1:2 の比率（20 / 40）が保たれ、幅 60 の内容は切れました。
- **範囲外の番号はエラーにならない** — `Grid.Column`・`Grid.Row`・Span は、定義した行と列の範囲に丸められ、例外にはなりません。`ColumnDefinition` を削除したときは、その列を指定していた子要素を確認します。警告なしに最後の列へ移ります。
- **定義のない Grid は 1 つのセル** — すべての子要素がセル全体に置かれて重なります。`Grid.Column="1"` を指定した子要素も、その 1 つのセルに置かれました。
- **Grid の入れ子自体のコストは小さい** — 500 個の TextBlock を並べる場合、行ごとに Grid を入れ子にしても、初回レイアウトの時間は平坦な Grid と比べて、繰り返し測って 1 割程度の差に収まりました。すべての TextBlock を 3 重の Grid で包み、Grid を 1,500 個増やすと、1〜2.5 割ほど長くなりました。列を `*` から `Auto` に変えても差は出ませんでした。構造は読みやすさで選んで構いません。
- **`Auto` と `*` が交差すると測り直しが起きる** — 列が `Auto`, `*`、行が `*`, `Auto` の 2 × 2 の Grid では、`Auto` 列と `*` 行が交わるセルの子要素が初回レイアウトで 2 回測られました。固定値だけ、`Auto` だけ、`*` だけの場合は、どの子要素も 1 回でした。測るのに時間のかかる子要素を置くときに影響します。
- **ヒットテストには `Background="Transparent"`** — 空いている部分でもマウスに反応させたい Grid に設定します。

## 実測した挙動 {#measured-behavior}

このページの挙動の記述は、すべて .NET 10 / Windows 11 で実際に動かして確かめたものです。計測には、このサイトのスクリーンショット生成ツールの [`GridDemoScene`](https://github.com/s-iguchi09/s-iguchi09.github.io/blob/main/tools/screenshot-capture/Scenes/GridDemoScene.cs){: target="_blank" rel="noopener noreferrer"} を使いました。配置と `*` 列の結果を以下に示します。

<figure class="article-figure">
  <img src="/images/wpf-standard-control-demo/verification/grid/grid-placement.svg" alt="300×200 の Grid での子要素の配置を示す表。行と列を指定しない子要素は最初のセルで重なり、範囲外の番号や Span は最後の列と行に丸められ、負の値と Span 0 は ArgumentException になり、定義のない Grid ではすべての子要素が 1 つのセルに置かれる" width="810" height="380" loading="lazy">
  <figcaption>300 &times; 200 の Grid で子要素が置かれた位置（x, y, 幅, 高さ）。.NET 10 / Windows 11 で計測。</figcaption>
</figure>

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/grid/grid-unbounded.svg" alt="幅 60 と 30 の内容を持つ 1* と 2* の列の幅を、親要素ごとに示す表。横向きの StackPanel では内容の幅 60 と 30、縦向きの StackPanel と広い ScrollViewer では 1:2、幅 60 の ScrollViewer では横スクロールの設定により内容の幅とスクロールバー、または 20 と 40 の比率になる" width="964" height="320" loading="lazy">
  <figcaption>親要素ごとの <code>*</code> 列・<code>*</code> 行の大きさ。どの場合も 0 には潰れない。.NET 10 / Windows 11 で計測。</figcaption>
</figure>

## 関連コントロール・記事

- [GridSplitter](/ja/apps/wpf-standard-control-demo/gridsplitter.html) — Grid の行・列の境界をドラッグして大きさを変えられるようにします。
- [DockPanel](/ja/apps/wpf-standard-control-demo/dockpanel.html) — 子要素を上下左右の端に寄せます。領域の少ない外枠ならこちらで足ります。
- [StackPanel](/ja/apps/wpf-standard-control-demo/stackpanel.html) — 子要素を一方向に積み重ねます。
- [UniformGrid](/ja/apps/wpf-standard-control-demo/uniformgrid.html) — 行と列を定義せずに、すべてのセルを同じ大きさにします。
- [WPF で ScrollViewer がスクロールしない原因と解決方法](/ja/articles/wpf-scrollviewer-not-scrolling/) — 縦向きの StackPanel の中の ScrollViewer は有限の高さを受け取れないためスクロールしない、という原因と、Grid に置き換える解決方法を扱います。

## ソースコード

このデモ画面のソースコードは GitHub で公開しています。アプリでは、各欄の下にある「Show Code」リンクでその欄の XAML を表示できます。

[GitHub で Grid のソースコードを見る →](https://github.com/s-iguchi09/WPFStandardControlDemoApp/tree/main/src/WPFStandardControlDemoApp/Features/GridUsage){: target="_blank" rel="noopener noreferrer"}
