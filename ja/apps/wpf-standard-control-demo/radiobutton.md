---
layout: control-demo
permalink: /ja/apps/wpf-standard-control-demo/radiobutton.html
title: "RadioButton"
badge: "Inputs"
lead: "RadioButton は、グループの中から 1 つを選ばせるボタンです。1 つをチェックすると、同じグループのほかのボタンのチェックが外れます。どのボタンが同じグループになるかは GroupName で決まります。"
description: "WPF の RadioButton を .NET 10 で実測して解説。GroupName の有無でどのボタンが同じグループになるか、bool へのバインドがどう連動するか、矢印キーと Tab キーで何が起きるかを確かめます。Popup をまたぐ場合も示します。"
---

## 概要

**RadioButton** は `ToggleButton` を継承しています。ToggleButton と違い、チェック済みの RadioButton をもう一度クリックしてもチェックは外れず、`IsChecked` は `True` のままでした。チェックが外れるのは、同じグループの別のボタンをチェックしたときだけです。

`GroupName` を設定しないと、同じ親要素を持つボタンが 1 つのグループになります。1 つの StackPanel に置いた 3 つのボタンは 1 つのグループになり、別の StackPanel のボタンは別のグループになりました。ボタンごとに親が違うと、この仕組みは働きません。1 つずつ `Border` で包んだボタンは、すべて同時にチェックできました。`ItemsControl` の `ItemTemplate` で作ったボタンも同じで、その `Parent` は `null` でした。こうしたボタンには `GroupName` を設定します。

キーボードで移るのはフォーカスで、選択は移りません。1 つ目のボタンをチェックしてフォーカスを置き、下矢印キーを押すと、フォーカスは 2 つ目へ移りましたが、チェックは 1 つ目に残りました。<kbd>Tab</kbd> キーも、次のグループではなく同じグループの次のボタンへ移りました。

デモアプリには `GroupName` と `VerticalContentAlignment` の 2 つの欄があります。各欄の下の「Show Code」リンクで、その欄の XAML を表示できます。

## 画面キャプチャ

![デモアプリの RadioButton のページ。左にコントロールの一覧、右に最初の節の GroupName](/images/wpf-standard-control-demo/radiobutton.png){: .screenshot-img}

## デモしているプロパティ

以下のプロパティが WPF 標準コントロールデモアプリでインタラクティブにデモされています。各プロパティの挙動は .NET 10 で実測したものです（[実測した挙動](#measured-behavior)を参照）。

| プロパティ | 設定値 | 説明 |
| --- | --- | --- |
| `GroupName` | `string` | グループの名前です。デモアプリには、これを設定しない 6 つのボタンと、`1` と `2` を設定した 6 つのボタンがあります。同じ名前のボタンは、親が違っても 1 つのグループになります。別々の GroupBox に置いた 2 つのボタンも、互いにチェックを外しました。ただし、グループはウィンドウの外へは広がりません。別のウィンドウと `Popup` の中の同じ名前のボタンは別のグループになり、3 つともチェックされたままでした。名前のあるボタンとないボタンは互いに影響しません。同じ親の中で名前のないボタンをチェックしても、`GroupName="1"` のボタンのチェックは外れませんでした。 |
| `VerticalContentAlignment (Control)` | `Top / Center / Bottom / Stretch` | 丸い記号と内容を、RadioButton の中で縦方向のどこに置くかです。既定値は `Top` で、プロパティの既定値から来ており、既定のスタイルが適用された後も `Top` でした。3 行に折り返すラベルでは、`Top` で記号がラベルの上端に来ました。`Center` では両方が RadioButton の中央に置かれるので、RadioButton の高さによらず、記号はラベルの中央に揃いました。高さ 200 の RadioButton では中心がどちらも約 100、ラベルと同じ高さ 46.88 では 23.44 と 22.94 でした。デモアプリはコンボボックスで値を選びます。 |

## IsChecked のバインド

`IsChecked` は既定で TwoWay にバインドされます。3 つのボタンを 3 つの `bool` プロパティにバインドして 2 つ目をクリックすると、プロパティは `A=False, B=True, C=False` になりました。1 つ目のボタンのバインドも残っていたので、ほかのプロパティを自分で false にする必要はありません。ソース側で `C` を `true` にすると、3 つ目のボタンがチェックされ、`B` は `false` になりました。

コンバーターを通して 1 つの列挙型のプロパティにバインドする方法もあります。下にリンクした記事では、コンバーターの `ConvertBack` が何を返すべきかと、`GroupName` が初期選択にどう影響するかを実測しています。

## XAML 使用例

デモアプリ（`RadioButtonUsageControl.xaml`）の `GroupName` の欄から、名前付きのグループの部分を、スタイル、周囲の GroupBox、配置の属性を省いて示します。名前空間の宣言を加えています。

```xml
<StackPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <RadioButton x:Name="GroupName1ARadioButton" Content="A(Group:1)" GroupName="1" IsChecked="True" />
  <RadioButton x:Name="GroupName1BRadioButton" Content="B(Group:1)" GroupName="1" />
  <RadioButton x:Name="GroupName1CRadioButton" Content="C(Group:1)" GroupName="1" />
  <RadioButton x:Name="GroupName2DRadioButton" Content="D(Group:2)" GroupName="2" IsChecked="True" />
  <RadioButton x:Name="GroupName2ERadioButton" Content="E(Group:2)" GroupName="2" />
  <RadioButton x:Name="GroupName2FRadioButton" Content="F(Group:2)" GroupName="2" />
</StackPanel>
```

## 主な使用例

- **設定** — ライト・ダーク・システムのように、少数の中から 1 つを選ぶ項目です。
- **フォーム** — 答えが 1 つだけの質問です。
- **絞り込み** — すべて・未完了・完了のように、1 つを選ぶとほかが外れる条件です。

## ヒントとベストプラクティス

- **親が共通でないボタンには `GroupName` を設定する** — 項目テンプレートの中のボタンや、1 つずつ別の要素で包んだボタンが当てはまります。
- **名前はウィンドウの中で重ならないようにする** — 1 つのウィンドウの 2 か所で同じ名前を使うと、1 つのグループになります。
- **同じ名前でウィンドウやポップアップをまたいでつなげようとしない** — それぞれが別のグループを持ちます。
- **最初に 1 つをチェックしておく** — `IsChecked` の既定値は `False` なので、`IsChecked="True"` を設定しないと何も選ばれていない状態で始まります。
- **複数行のラベルには `VerticalContentAlignment="Center"` を設定する** — 既定値の `Top` では記号が 1 行目の位置に残ります。

## 実測した挙動 {#measured-behavior}

このページの挙動の記述は、すべて .NET 10 / Windows 11 で実際に動かして確かめたものです。計測には、このサイトのスクリーンショット生成ツールの [`RadioButtonDemoScene`](https://github.com/s-iguchi09/s-iguchi09.github.io/blob/main/tools/screenshot-capture/Scenes/RadioButtonDemoScene.cs){: target="_blank" rel="noopener noreferrer"} を使いました。クリックはボタンのクリック処理（`OnClick`）を呼んで行い、矢印キーと <kbd>Tab</kbd> キーは、キーボードから打ったときと同じく WPF の入力管理（InputManager）を通して送りました。

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/radiobutton/radiobutton-groups.svg" alt="どの RadioButton が同じグループになるかの計測結果の表。チェック済みのボタンはクリックしても外れず、GroupName なしのボタンは親ごとにまとまり、Border で包んだボタンや ItemTemplate で作ったボタンはすべてチェックでき、名前のあるボタンとないボタンは影響し合わず、同じ GroupName は別々の GroupBox をつなぎ、別のウィンドウと Popup はつながない" width="959" height="350" loading="lazy">
  <figcaption>どのボタンが互いのチェックを外すか。.NET 10 / Windows 11 で計測。</figcaption>
</figure>

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/radiobutton/radiobutton-binding-keys.svg" alt="RadioButton のバインドとキーの計測結果の表。IsChecked を 3 つの bool にバインドして B をクリックすると A と C は false になりバインドは残り、ソースで C を true にすると B が外れ、下矢印キーはフォーカスだけを移してチェックは移さず、Tab は次のボタンへ移り、VerticalContentAlignment の既定値は Top で、Center では記号とラベルが RadioButton の中央に置かれ、記号が 3 行のラベルの中央に揃う" width="1061" height="320" loading="lazy">
  <figcaption>バインドした <code>IsChecked</code>、キーボード、<code>VerticalContentAlignment</code>。.NET 10 / Windows 11 で計測。</figcaption>
</figure>

## 関連コントロール・記事

- [CheckBox](/ja/apps/wpf-standard-control-demo/checkbox.html) — 項目ごとに独立してオン・オフする選択肢に使います。
- [ToggleButton](/ja/apps/wpf-standard-control-demo/togglebutton.html) — 基底クラスで、`GroupName` はありません。
- [ComboBox](/ja/apps/wpf-standard-control-demo/combobox.html) — 多くの中から 1 つを、ドロップダウンの一覧で選びます。
- [WPF で RadioButton を enum にバインドすると初期選択が表示されない問題と GroupName の役割](/ja/articles/wpf-radiobutton-enum-binding/) — グループを 1 つの列挙型のプロパティにバインドする方法を扱います。

## ソースコード

このデモ画面のソースコードは GitHub で公開しています。アプリでは、各欄の下にある「Show Code」リンクでその欄の XAML を表示できます。

[GitHub で RadioButton のソースコードを見る →](https://github.com/s-iguchi09/WPFStandardControlDemoApp/tree/main/src/WPFStandardControlDemoApp/Features/RadioButtonUsage){: target="_blank" rel="noopener noreferrer"}
