---
layout: control-demo
permalink: /ja/apps/wpf-standard-control-demo/combobox.html
title: "ComboBox"
badge: "Inputs"
lead: "ComboBox は、選択中の項目を表示し、ドロップダウンの一覧を開いて別の項目を選ばせるコントロールです。編集可能にして、文字を入力させることもできます。"
description: "WPF の ComboBox を .NET 10 で実測して解説。編集可能なときの自動補完と入力した大文字小文字の保持、IsReadOnly、ドロップダウンの高さ、実際のクリックでの StaysOpenOnEdit、リストの同期を確かめます。"
---

## 概要

デモアプリの **ComboBox** はどれも、`Name` と `Value` を持つ曜日の項目を `DisplayMemberPath="Name"`・`SelectedValuePath="Value"` で表示しています。このような項目では、2 つ目の曜日を選ぶと `Text` も `SelectedValue` も `Monday` になりますが、`SelectedItem` は項目のオブジェクトそのものです。項目のクラスが `ToString` を上書きしていないので、その文字は名前空間を含む完全な型名になります。計測した項目では `ScreenshotCapture.Scenes.ComboBoxDemoScene+EnumItem`、デモアプリの項目では `WPFStandardControlDemoApp.Common.MarkupExtensions.EnumBindingSourceExtension+EnumItem` で、デモアプリの `SelectedItem` の表示もこれになります。

`IsDropDownOpen` は既定で TwoWay にバインドされます。デモアプリは `Mode` を指定せずにチェックボックスにバインドしています。チェックボックスで一覧を開いたあと、<kbd>Down</kbd>・<kbd>Down</kbd>・<kbd>Enter</kbd> を押すと、Monday が選ばれて一覧が閉じ、チェックボックスのチェックも外れました。

デモアプリには、`IsDropDownOpen`、`IsEditable`、`IsReadOnly`、`MaxDropDownHeight`、`Text`、`StaysOpenOnEdit`、`ShouldPreserveUserEnteredPrefix`、選択のプロパティ、`IsSynchronizedWithCurrentItem` の欄があります。各欄の下の「Show Code」リンクで、その欄の XAML を表示できます。`ItemsSource` のバインドと選択の読み取り方は、下にリンクした記事で扱っています。

## 画面キャプチャ

![デモアプリの ComboBox のページ。左にコントロールの一覧、右に最初の節の IsDropDownOpen](/images/wpf-standard-control-demo/combobox.png){: .screenshot-img}

## デモしているプロパティ

以下のプロパティが WPF 標準コントロールデモアプリでインタラクティブにデモされています。各プロパティの挙動は .NET 10 で実測したものです（[実測した挙動](#measured-behavior)を参照）。

| プロパティ | 設定値 | 説明 |
| --- | --- | --- |
| `IsDropDownOpen` | `bool` | 一覧が開いているかどうかです。既定で TwoWay にバインドされるので、バインドしたチェックボックスは一覧が閉じたことに追従します（上を参照）。 |
| `IsEditable` | `bool` | 利用者が文字を入力できるかどうかで、既定値は `False` です。入力すると合う項目で補完され、「tue」は `Tuesday` になり、`SelectedValue` も Tuesday になりました。どの項目にも合わない文字は入力のまま残り、「xyz」では `SelectedIndex` が -1、`SelectedValue` が `null` でした。 |
| `IsReadOnly` | `bool` | `IsEditable` のときに入力を止めるかどうかです。両方が `True` のとき、「tue」を入力しても文字は空のままでしたが、<kbd>Down</kbd> キーでは項目（Sunday）が選ばれました。 |
| `MaxDropDownHeight` | `double` | 一覧の高さの上限です。既定値は、計測したマシンの画面の高さの 3 分の 1 の 480 で、7 つの曜日は 141.72 でした。デモアプリの 100 では、一覧は 7 つの曜日に必要な高さより低い 100 になりました。 |
| `Text` | `string` | 欄の文字で、選択した項目なら `DisplayMemberPath` から、編集可能な ComboBox なら入力のままになります。デモアプリは `SelectedValue` と並べて表示しています。 |
| `StaysOpenOnEdit` | `bool` | 利用者が編集欄をクリックしたときに一覧を開いたままにするかどうかで、既定値は `False` です。開いた編集可能な ComboBox の編集欄を実際のマウスでクリックすると、`False` では一覧が閉じ、`True` では開いたままでした。 |
| `ShouldPreserveUserEnteredPrefix` | `bool` | 自動補完で、入力した文字をそのまま残すかどうかで、既定値は `False` です。「tue」を入力すると、`False` では `Tuesday`、`True` では `tuesday` になりました。`SelectedValue` はどちらも Tuesday でした。 |
| `SelectedIndex / SelectedItem / SelectedValue / SelectedValuePath / DisplayMemberPath (Selector, ItemsControl)` | `int / object / object / string / string` | 選択と、項目のどのプロパティを値と表示の文字にするかです（上を参照）。 |
| `IsSynchronizedWithCurrentItem (Selector)` | `bool?` | 選択を一覧のビューの現在の項目に合わせるかどうかです。同じリストの 2 つの ComboBox を `True` にすると、どちらもインデックス 0 で始まり、一緒に動きました。1 つ目を 3 にすると 2 つ目も 3 になりました。未設定では、どちらも -1 で始まり、互いに独立でした。デモアプリは 3 つの ComboBox を `True` で始めるので、すべて Sunday で始まります。 |

## XAML 使用例

デモアプリ（`ComboBoxUsageControl.xaml`）の `ShouldPreserveUserEnteredPrefix` の欄から、スタイルと周囲の GroupBox を省き、名前空間の宣言を加えたものです。`markupextensions` はデモアプリ独自のマークアップ拡張の接頭辞です。

```xml
<StackPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
            xmlns:sys="clr-namespace:System;assembly=mscorlib"
            xmlns:markupextensions="clr-namespace:WPFStandardControlDemoApp.Common.MarkupExtensions">
  <CheckBox x:Name="ShouldPreserveUserEnteredPrefix" Content="ShouldPreserveUserEnteredPrefix" />

  <ComboBox x:Name="ShouldPreserveUserEnteredPrefixComboBox"
            DisplayMemberPath="Name"
            IsEditable="True"
            ItemsSource="{markupextensions:EnumBindingSource EnumType=sys:DayOfWeek}"
            SelectedValuePath="Value"
            ShouldPreserveUserEnteredPrefix="{Binding IsChecked, ElementName=ShouldPreserveUserEnteredPrefix}" />

  <TextBlock Text="{Binding Text, ElementName=ShouldPreserveUserEnteredPrefixComboBox}" />
  <TextBlock Text="{Binding SelectedValue, ElementName=ShouldPreserveUserEnteredPrefixComboBox}" />
</StackPanel>
```

## 主な使用例

- **多くの中から 1 つを選ぶ** — ラジオボタンでは長くなりすぎる、国や分類の選択です。
- **列挙型** — 列挙型の値を名前で表示し、値でバインドします。
- **候補付きの入力** — 既知の値を補完しつつ、それ以外も受け付ける編集可能な ComboBox です。

## ヒントとベストプラクティス

- **`SelectedItem` を表示するより、`SelectedValuePath` と `SelectedValue` をバインドする** — 項目のクラスが `ToString` を上書きしていなければ、完全な型名が出ます。
- **編集可能な ComboBox では `Text` だけでなく `SelectedIndex` も確かめる** — 何にも合わない文字では選択がありません。
- **`IsSynchronizedWithCurrentItem` は一覧を連動させたいときだけ設定する** — 最初の項目を選んだ状態でも始まります。
- **長い一覧には `MaxDropDownHeight` を設定する** — 既定値は画面の高さの 3 分の 1 です。

## 実測した挙動 {#measured-behavior}

このページの挙動の記述は、すべて .NET 10 / Windows 11 で実際に動かして確かめたものです。計測には、このサイトのスクリーンショット生成ツールの [`ComboBoxDemoScene`](https://github.com/s-iguchi09/s-iguchi09.github.io/blob/main/tools/screenshot-capture/Scenes/ComboBoxDemoScene.cs){: target="_blank" rel="noopener noreferrer"} を使い、デモアプリと同じ形の項目（各曜日の `Name` と `Value`、`ToString` の上書きなし）で試しました。文字は編集欄へ 1 文字ずつ送り、キーは WPF の入力管理（InputManager）を通して送り、編集欄のクリックは実際のマウスで行いました。高さは、計測したマシンでの値です。

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/combobox/combobox-behavior.svg" alt="ComboBox の計測結果の表。既定では編集不可・読み取り専用でなく、StaysOpenOnEdit と ShouldPreserveUserEnteredPrefix は False、文字検索は有効、MaxDropDownHeight 480 は画面の 3 分の 1、IsDropDownOpen は TwoWay なので Enter で閉じるとバインドしたチェックボックスも外れ、SelectedItem は項目のクラスの完全な型名を出し、tue の入力は Tuesday、入力を保つ設定では tuesday に補完され、xyz では選択がなく、IsReadOnly は入力を止めるが Down キーは止めず、一覧の高さは 141.72、デモアプリの値では 100、編集欄の実際のクリックは StaysOpenOnEdit でなければ一覧を閉じ、同期した ComboBox は 0 で始まって一緒に動く" width="1171" height="530" loading="lazy">
  <figcaption>選択、編集、ドロップダウン、同期。.NET 10 / Windows 11 で計測。</figcaption>
</figure>

## 関連コントロール・記事

- [ListBox](/ja/apps/wpf-standard-control-demo/listbox.html) — ドロップダウンではなく、すべての項目を同時に表示します。
- [RadioButton](/ja/apps/wpf-standard-control-demo/radiobutton.html) — すべて見えている、少数の選択肢です。
- [Popup](/ja/apps/wpf-standard-control-demo/popup.html) — ドロップダウンの一覧を入れている要素です。
- [WPF ComboBox の ItemsSource バインドパターンと選択値の取得方法](/ja/articles/wpf-combobox-itemssource-patterns/) — ComboBox への項目の与え方と、選択の読み取り方を扱います。

## ソースコード

このデモ画面のソースコードは GitHub で公開しています。アプリでは、各欄の下にある「Show Code」リンクでその欄の XAML を表示できます。

[GitHub で ComboBox のソースコードを見る →](https://github.com/s-iguchi09/WPFStandardControlDemoApp/tree/main/src/WPFStandardControlDemoApp/Features/ComboBoxUsage){: target="_blank" rel="noopener noreferrer"}
