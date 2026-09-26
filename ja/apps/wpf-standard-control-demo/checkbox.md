---
layout: control-demo
permalink: /ja/apps/wpf-standard-control-demo/checkbox.html
title: "CheckBox"
badge: "Inputs"
lead: "CheckBox は、ユーザーに選択肢のオン・オフを切り替えさせるコントロールです。<code>IsThreeState</code> を使うと、3 つ目の未確定の状態も持てます。"
description: "WPF の CheckBox の 3 状態の切り替え、IsChecked のバインド、キー操作、ラベルの配置を .NET 10 で実測して解説します。bool 型にバインドすると null が変換されずにバインドエラーになることも、デモアプリとあわせて確かめます。"
---

## 3 つの状態と、クリックで巡る順番

**CheckBox** は `ToggleButton` を継承しています。状態は `bool?` 型の `IsChecked` で表し、`true` がオン、`false` がオフ、`null` が未確定です。`IsThreeState` は、クリックで未確定の状態を通るかどうかを決めます。`True` ではクリックごとに `false` → `true` → `null` → `false` と変わり、既定値の `False` では `false` → `true` → `false` と変わりました。

この設定が影響するのはクリックだけで、コードや XAML からは、どちらの場合でも `IsChecked` に `null` を設定できます。デモアプリには、`null`・`False`・`True` で始まる 3 つの CheckBox があります。最初の 1 つは 3 状態ではないため、一度クリックすると `null` には戻れず、クリックごとに `null` から `false`、`true`、`false` と変わりました。

3 状態の「すべて選択」の CheckBox も同じ順番で巡ります。`null` の次のクリックで `false` になるので、ViewModel で別の扱いにしない限り、「一部選択」の状態でクリックするとすべてが外れます。そのクリックの意味を決めておきます。

## IsChecked のバインド：null には bool? が要る

`IsChecked` は既定で TwoWay にバインドされます。3 つの状態をすべて保つには `bool?` 型のプロパティにバインドします。`bool` 型のプロパティでは `null` は変換されません。`IsThreeState` の CheckBox を `bool` 型のソースにバインドし、`true` から `null` へクリックすると、バインドエラーになり、ソースは `true` のままでした。`null` になりうるなら `bool?` を使います。

ほかのコントロールから `IsChecked` を直接参照することもできます。ボタンの `IsEnabled` を CheckBox の `IsChecked` にバインドすると、CheckBox を実際にクリックしたときにボタンが無効から有効に変わりました。同意の CheckBox でボタンを有効にするしくみです。

## クリックとイベントの届き方

クリックできるのは四角い部分だけではなく、コントロール全体です。ラベルの中央をヒットテストすると、CheckBox の中にあるラベルの `TextBlock` に当たりました。フォーカスのある CheckBox は、Space キーを押して離したときにも切り替わりました。

`Checked`・`Unchecked`・`Indeterminate` の各イベントはバブルします。親の `StackPanel` に置いた 1 つの `Checked` ハンドラーが、子の CheckBox の変化を受け取りました。一覧などで実行時に CheckBox を作る場合に便利です。

## 折り返すラベルと四角い部分をそろえる

`VerticalContentAlignment` は、四角い部分とラベルを、CheckBox の中で縦方向のどこに置くかを決めます。既定値はプロパティの既定値から来る `Top` です。3 行に折り返すラベルでは、`Top` のとき四角い部分は 1 行目の横に並びました（四角い部分は y = 1、ラベルは y = -1）。`Center` のときは両方が CheckBox の中央に置かれるので、CheckBox の高さによらず、四角い部分はラベルの中央に揃いました。高さ 200 の CheckBox では中心がどちらも約 100、ラベルと同じ高さ 46.88 では 23.44 と 22.94 でした。

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/checkbox/checkbox-behavior.svg" alt="CheckBox の計測結果の表。ToggleButton を継承し、IsChecked は既定で TwoWay、イベントはバブルし、VerticalContentAlignment の既定値は Top、3 状態のクリックは false・true・null と巡回し、bool 型のソースは null をバインドエラーにし、Space キーで切り替わり、ラベルもクリックできる範囲に含まれ、Center では四角い部分とラベルが CheckBox の中央に置かれ、四角い部分が折り返したラベルの中央に揃う。IsEnabled を IsChecked にバインドしたボタンは CheckBox の実際のクリックで有効になる" width="1140" height="590" loading="lazy">
  <figcaption>状態、バインド、キー操作、ヒットテスト、配置。.NET 10 / Windows 11 で計測。</figcaption>
</figure>

## デモアプリで試す

![デモアプリの CheckBox のページ。左にコントロールの一覧、右に最初の節の IsChecked(ToggleButton)](/images/wpf-standard-control-demo/checkbox.png){: .screenshot-img}

デモアプリの CheckBox のページには、`IsChecked`、`IsThreeState`、一覧から値を選べる `VerticalContentAlignment` の欄があります。各欄の下の「Show Code」リンクで、その欄の XAML を表示できます。次の XAML は、`IsThreeState` の欄（`CheckBoxUsageControl.xaml`）から、スタイルと周囲の GroupBox を省き、名前空間の宣言を加えたものです。`TargetNullValue` で、未確定の状態を "(Null)" と表示しています。

```xml
<StackPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <CheckBox x:Name="IsThreeStateTrueCheckBox" HorizontalAlignment="Center"
            VerticalContentAlignment="Center" Content="CheckBox" IsThreeState="True" />
  <TextBlock HorizontalAlignment="Center"
             Text="{Binding IsChecked, ElementName=IsThreeStateTrueCheckBox, TargetNullValue=(Null)}" />

  <CheckBox x:Name="IsThreeStateFalseCheckBox" HorizontalAlignment="Center"
            VerticalContentAlignment="Center" Content="CheckBox" IsThreeState="False" />
  <TextBlock HorizontalAlignment="Center"
             Text="{Binding IsChecked, ElementName=IsThreeStateFalseCheckBox}" />
</StackPanel>
```

## 関連するコントロールと記事

- [RadioButton](/ja/apps/wpf-standard-control-demo/radiobutton.html) — 同じく `ToggleButton` の派生で、複数の中から 1 つを選ばせます。
- [ToggleButton](/ja/apps/wpf-standard-control-demo/togglebutton.html) — 基底クラスで、押した状態が保たれるボタンです。

## ソースコードと計測の方法

このページの挙動の記述は、すべて .NET 10 / Windows 11 で実際に動かして確かめたものです。計測には、このサイトのスクリーンショット生成ツールの [`CheckBoxDemoScene`](https://github.com/s-iguchi09/s-iguchi09.github.io/blob/main/tools/screenshot-capture/Scenes/CheckBoxDemoScene.cs){: target="_blank" rel="noopener noreferrer"} を使いました。クリックは、同意の CheckBox だけ実際のマウスで、ほかはクリックと同じ切り替え処理を実行する UI オートメーションの `Toggle` で、Space キーは表示したウィンドウへキーを押す・離すイベントを送って再現しました。位置は計測した環境での値です。

[GitHub で CheckBox のソースコードを見る →](https://github.com/s-iguchi09/WPFStandardControlDemoApp/tree/main/src/WPFStandardControlDemoApp/Features/CheckBoxUsage){: target="_blank" rel="noopener noreferrer"}
