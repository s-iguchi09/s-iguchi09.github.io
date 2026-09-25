---
layout: control-demo
permalink: /ja/apps/wpf-standard-control-demo/gridsplitter.html
title: "GridSplitter"
badge: "Resizer"
lead: "GridSplitter は Grid の中に置くコントロールで、ドラッグや矢印キーで両隣の行・列の大きさを変えられるようにします。"
description: "WPF の GridSplitter で変わる列の組を、ResizeBehavior と両隣の列の組み合わせ 36 通りで .NET 10 で実測して解説します。既定の配置が Right のため専用の列が広がる落とし穴や、プレビュー、キーボード操作、幅の保存も扱います。"
---

## 概要

**GridSplitter** は `Thumb` を継承しています（`Thumb` は `Control` を継承）。ドラッグのイベント `DragStarted`・`DragDelta`・`DragCompleted` と、読み取り専用の `IsDragging` プロパティは `Thumb` のものです。スプリッター自身が動くわけではなく、ドラッグ中に隣の `ColumnDefinition` の `Width`（行なら `RowDefinition` の `Height`）を書き換えます。ドラッグ後の値は列の種類によって変わりました。`*` 列どうしは `227.5*` と `167.5*` のように `*` の値のまま、固定幅の列は新しい固定値に、`Auto` 列は固定値に置き換わりました。

どの行・列が変わるかは `ResizeDirection` と `ResizeBehavior` で決まり、どちらも既定ではスプリッターの配置から自動で選ばれます。注意したいのは、GridSplitter の `HorizontalAlignment` の既定値が `Stretch` ではなく `Right` であることです。既定の `ResizeBehavior="BasedOnAlignment"` では、右寄せのスプリッターは自分の列と次の列を変えます。内容と同じ列の右端に置く場合はこれで意図どおりに動きます。しかしスプリッター専用の列に置いた場合は、右へ 30 ドラッグすると幅 5 の `Auto` 列そのものが 35 に広がり、スプリッターの横に隙間ができました。

デモアプリには、以下の各プロパティの欄があります。`ResizeBehavior` の欄では、4 種類の動作ごとに、両隣の列（`*`・`Auto`・固定幅）の組み合わせ 9 通りを並べており、それぞれをドラッグして比べられます。各欄の下の「Show Code」リンクで、その欄の XAML を表示できます。

## 画面キャプチャ

![gridsplitter demo screen](/images/wpf-standard-control-demo/gridsplitter.png){: .screenshot-img}

## デモしているプロパティ

以下のプロパティが WPF 標準コントロールデモアプリでインタラクティブにデモされています。各プロパティの挙動は .NET 10 で実測したものです（[実測した挙動](#measured-behavior)を参照）。

| プロパティ | 設定値 | 説明 |
| --- | --- | --- |
| `ResizeDirection` | `Auto / Columns / Rows` | 列と行のどちらを変えるかを決めます。既定値の `Auto` は配置から判断します。横方向に引き伸ばされていないスプリッターは列を変えます。縦横とも `Stretch` の場合は形で決まり、幅より高さが大きいスプリッターは列を、高さより幅が大きいスプリッターは行を変えました。デモアプリの欄には、全行にまたがる縦のスプリッターと、全列にまたがる横のスプリッターがあります。`Auto` ではそれぞれの向きに変わりました。`Columns` では横のスプリッターが、`Rows` では縦のスプリッターが何も変えませんでした。全列にまたがるスプリッターは 0 列目にあり、その前に変えられる列が無いためです。 |
| `ResizeBehavior` | `BasedOnAlignment / CurrentAndNext / PreviousAndCurrent / PreviousAndNext` | どの列（行）の組を変えるかを決めます。`PreviousAndNext` はスプリッターの列の両隣の境界を動かし、`CurrentAndNext` は自分の列と次の列を、`PreviousAndCurrent` は前の列と自分の列を変えます。既定値の `BasedOnAlignment` は `HorizontalAlignment` で選びます。実測では、`Left` は `PreviousAndCurrent`、`Right`（既定値）は `CurrentAndNext`、`Center` と `Stretch` は `PreviousAndNext` と同じ動きになりました。デモアプリではスプリッターが専用の `Auto` 列にあります。この配置で 9 通りすべてについて両側のペインを変えたのは `PreviousAndNext` だけでした。`CurrentAndNext` はスプリッターの列そのものを広げました。`PreviousAndCurrent` は前の列を広げましたが、前の列が `*` 列の場合は何も変えませんでした。 |
| `ShowsPreview` | `bool` | 既定値の `False` では、ドラッグの 1 ステップごとに列幅が変わります。10 ステップのドラッグで、左の列の内容が 10 回測り直されました。`True` にするとドラッグ中はプレビューだけが動きます。マウスボタンを離すまで列幅は変わらず、内容も一度も測り直されず、離した時点でまとめて反映されました。なお、デモアプリのこの欄のスプリッターは既定の配置のままなので、ドラッグするとスプリッターの列も広がります（`ResizeBehavior` を参照）。 |
| `DragIncrement` | `double` | ドラッグ量をこの値の倍数に丸めます。既定値は 1 です。`DragIncrement="20"` では、9・11・27・31 のドラッグで境界がそれぞれ 0・20・20・40 動きました。0 を設定すると `ArgumentException` が発生します。 |
| `KeyboardIncrement` | `double` | スプリッターにキーボードフォーカスがあるとき、矢印キー 1 回で境界が動く量です。既定値は 10 で、右矢印キー 1 回で 10、`KeyboardIncrement="25"` では 25 動きました。0 を設定すると `ArgumentException` が発生します。キーボードでのリサイズを止めたい場合は `Focusable="False"` を設定します。フォーカスを受け取らなくなり（`Focus()` が `False` を返しました）、矢印キーが届かなくなります。 |
| `PreviewStyle` | `Style` | `ShowsPreview="True"` のときに表示するプレビューのスタイルです。プレビューは装飾層（adorner layer）に置かれる `Control` で、このスタイルはそこに適用されるため、`TargetType` は `Control` にします。設定しない場合は GridSplitter の既定のスタイルから与えられ、プレビューは `#80000000`（不透明度 50% の黒）で塗った `Rectangle` になりました。デモアプリでは、テンプレートを `YellowGreen` の Border にしたスタイルと並べて比べられます。 |
| `IsDragging (Thumb)` | `bool（読み取り専用）` | `Thumb` から継承した読み取り専用のプロパティです。スプリッター上でマウスの左ボタンを押すと `True` になり、ドラッグを中止すると `False` に戻りました。ドラッグ中にスプリッターを強調表示するトリガーに使えます。デモアプリでは、スプリッターの横にこの値を表示しています。 |

## XAML 使用例

デモアプリ（`GridSplitterUsageControl.xaml`）の `ResizeDirection` の欄から、スタイル、周囲の GroupBox、4 つの Label を省き、名前空間の宣言を加えたものです。コンボボックスに `GridResizeDirection` の値を並べ、2 つのスプリッターにバインドしています。`markupextensions` はデモアプリ独自のマークアップ拡張の接頭辞です。

```xml
<StackPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
            xmlns:markupextensions="clr-namespace:WPFStandardControlDemoApp.Common.MarkupExtensions">
  <ComboBox x:Name="ResizeDirectionComboBox"
            DisplayMemberPath="Name"
            ItemsSource="{markupextensions:EnumBindingSource EnumType=GridResizeDirection}"
            SelectedValuePath="Value"
            SelectedIndex="0" />

  <Grid Height="100">
    <Grid.RowDefinitions>
      <RowDefinition Height="*" />
      <RowDefinition Height="5" />
      <RowDefinition Height="*" />
    </Grid.RowDefinitions>
    <Grid.ColumnDefinitions>
      <ColumnDefinition Width="*" />
      <ColumnDefinition Width="5" />
      <ColumnDefinition Width="*" />
    </Grid.ColumnDefinitions>
    <!-- 四隅のセルに TopLeft / TopRight / BottomLeft / BottomRight の Label -->
    <GridSplitter x:Name="VerticalSplitter"
                  Grid.RowSpan="3" Grid.Column="1"
                  Width="5" VerticalAlignment="Stretch" Background="Gray"
                  ResizeBehavior="PreviousAndNext"
                  ResizeDirection="{Binding SelectedValue, ElementName=ResizeDirectionComboBox}" />
    <GridSplitter x:Name="HorizontalSplitter"
                  Grid.Row="1" Grid.ColumnSpan="3"
                  Height="5" HorizontalAlignment="Stretch" Background="Gray"
                  ResizeBehavior="PreviousAndNext"
                  ResizeDirection="{Binding SelectedValue, ElementName=ResizeDirectionComboBox}" />
  </Grid>
</StackPanel>
```

## 主な使用例

- **ナビゲーションと本体** — 左にツリーや一覧、右に内容を置き、境界にスプリッターを置きます。
- **一覧と詳細** — 上にレコードの一覧、下に選んだレコードの詳細を置き、横のスプリッターで分けます。
- **出力ペイン** — 上にエディターや文書、下にログや出力のペインを置きます。
- **3 ペイン** — ナビゲーション・本体・プロパティの 3 つを並べ、境界ごとにスプリッターを置きます。

## ヒントとベストプラクティス

- **専用の列に置くときは配置を変える** — `HorizontalAlignment="Stretch"`（または `Center`）か、`ResizeBehavior="PreviousAndNext"` を設定します。既定の `Right` のままだと、概要のとおりスプリッター自身の列が広がります。内容と同じ列に置く場合は既定のままで動き、0 列目の右端に置いたスプリッターは 0 列目と 1 列目を変えました。
- **ペインに `MinWidth` / `MinHeight` を設定する** — 左へ 1,000 ドラッグすると左のペインは 0 まで縮みましたが、`MinWidth="50"` を設定すると 50 で止まりました。
- **幅を保存するなら `Mode=TwoWay` でバインドする** — `TwoWay` では、ドラッグ後の幅（`227.5*`）がソースのプロパティに反映され、バインドも残りました。Mode を指定しないと、最初のドラッグでバインドが外れ、ソースは元の値のままでした。
- **レイアウトが重いペインには `ShowsPreview="True"`** — 内容の測り直しが、ドラッグのステップごとではなく、ボタンを離したときの 1 回で済みます。
- **Esc キーでドラッグを取り消せる** — ボタンを離す前に Esc キーを押すと、列幅はドラッグ前の値に戻りました。
- **置き場所を誤っても何も起きない** — Grid の外に置いたスプリッターや、`PreviousAndNext` で前の列が無い位置のスプリッターは、例外も出さず何も変えませんでした。ドラッグしても変わらないときは、スプリッターの列と `ResizeBehavior` を確認します。

## 実測した挙動 {#measured-behavior}

このページの挙動の記述は、すべて .NET 10 / Windows 11 で実際に動かして確かめたものです。計測には、このサイトのスクリーンショット生成ツールの [`GridSplitterDemoScene`](https://github.com/s-iguchi09/s-iguchi09.github.io/blob/main/tools/screenshot-capture/Scenes/GridSplitterDemoScene.cs){: target="_blank" rel="noopener noreferrer"} を使いました。ドラッグは、マウスでドラッグしたときに `Thumb` が発生させるのと同じ `DragStarted`・`DragDelta`・`DragCompleted` イベントを発生させて再現しています。次の表は、デモアプリの `ResizeBehavior` の欄と同じ構成です。

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/gridsplitter/gridsplitter-resize-behavior.svg" alt="幅 400 の Grid で専用の Auto 列に置いた GridSplitter を右へ 30 ドラッグした後の列幅を、両隣の列が *・Auto・200 の 9 通りと ResizeBehavior の 4 値について示す表。PreviousAndNext は両隣の境界を動かし、BasedOnAlignment と CurrentAndNext はスプリッターの列を 35 に広げ、PreviousAndCurrent は前の列が * 列のとき何も変えない" width="977" height="350" loading="lazy">
  <figcaption><code>ResizeBehavior</code> ごとの、30 ドラッグした後の列幅（左 / スプリッター / 右）。内容の幅は 60。.NET 10 / Windows 11 で計測。</figcaption>
</figure>

## 関連コントロール

- [Grid](/ja/apps/wpf-standard-control-demo/grid.html) — GridSplitter は Grid の中でだけ動き、変えるのは Grid の行・列の定義です。
- [DockPanel](/ja/apps/wpf-standard-control-demo/dockpanel.html) — ペインを上下左右の端に寄せます。ユーザーが大きさを変えることはできません。

## ソースコード

このデモ画面のソースコードは GitHub で公開しています。アプリでは、各欄の下にある「Show Code」リンクでその欄の XAML を表示できます。

[GitHub で GridSplitter のソースコードを見る →](https://github.com/s-iguchi09/WPFStandardControlDemoApp/tree/main/src/WPFStandardControlDemoApp/Features/GridSplitterUsage){: target="_blank" rel="noopener noreferrer"}
