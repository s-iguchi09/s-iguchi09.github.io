---
layout: control-demo
permalink: /ja/apps/wpf-standard-control-demo/textblock.html
title: "TextBlock"
badge: "Display"
lead: "TextBlock は、1 つの文字列か書式付きの文字の並びを、読み取り専用で表示します。画面に文字を置く最も軽い方法です。"
description: "WPF の TextBlock を .NET 10 で実測して解説。Text と Inlines の関係、長い語に対する TextWrapping の違い、TextTrimming が効く条件、デモアプリの LineHeight 5 で行が重なる理由を確かめます。"
---

## Text と Inlines は思われているようには混ざらない

**TextBlock** は `Control` ではなく `FrameworkElement` を継承しているので、テンプレートを持ちません。フォーカスは受け取らず（`Focusable` は `False`）、文字を選択することもできません。選択できる読み取り専用の文字の表示方法は、下にリンクした記事で扱っています。

文字の与え方は 2 通りあり、1 つの文字列の `Text` か、書式付きの文にする `Run`・`Bold`・`Hyperlink` などの `Inlines` です。最初のレイアウトの前に `Inlines`（`Run` と `Bold`）で内容を作った TextBlock の `Text` は、レイアウトの前も後も空文字でした。このように作った TextBlock の `Text` には頼れません。そのあと `Text` を設定しても例外にはならず、書式付きの並びが 1 つの並びに置き換わりました。

バインドと書式を組み合わせるなら、`Run` の `Text` をバインドします。太字の「Name: 」の後に置いてバインドした `Run` は、バインドした文字を表示し、ソースの変更にも追従しました。

## 長い語、限られた幅、余白

`TextWrapping` の既定値は `NoWrap` です。1 語だけで幅 100 を超える語を含む「Say Supercalifragilisticexpialidocious now」を幅 100 に置くと、`NoWrap` は 1 行で 100 の位置で切れました。`Wrap` は長い語そのものを折って 4 行になりました。`WrapWithOverflow` は語を分けずに 3 行になり、その語が 100 を超えてそこで切れました。端からはみ出させたくないなら `Wrap` を使います。

既定値が `None` の `TextTrimming` は、幅の決まった列の名前のように、収まらない文字の末尾を省略記号にします。幅が限られているときだけ働きます。`CharacterEllipsis` の長い文字は、幅 100 の Grid では幅 100 になりました。幅に制限のない横の StackPanel では本来の幅 220.43 になり、省略されませんでした。

`Padding` の既定値は 0 です。`Padding="10"` では、TextBlock が縦横それぞれ 20 大きくなり、61.57 × 15.96 から 81.57 × 35.96 になりました。

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/textblock/textblock-behavior.svg" alt="TextBlock の計測結果の表。FrameworkElement を継承しフォーカスを受けず既定の余白はなく、最初のレイアウトの前に Inlines で作った内容では Text は空で、設定すると例外なく置き換わり、長い語は NoWrap で 1 行で切れ、Wrap で 4 行、WrapWithOverflow で 3 行ではみ出して切れ、省略は幅 100 の Grid では効くが StackPanel では効かず、Padding 10 で縦横 20 大きくなる" width="1022" height="290" loading="lazy">
  <figcaption>文字と Inlines、折り返し、省略、余白。.NET 10 / Windows 11 で計測。</figcaption>
</figure>

## デモアプリで行が重なる理由：LineHeight と LineStackingStrategy

`LineHeight` は各行の高さ、`LineStackingStrategy` はその当てはめ方です。デモアプリは `LineHeight` 5 と、コンボボックスの最初の値の `BlockLineHeight` で始まります。4 行の文字でこの設定だと、高さは 63.84 ではなく 20 になり、行が重なります。`MaxHeight` では行が文字より低くならず、5 でも高さは 63.84 のままでした。30 ではどちらも 120 でした。行が重ならないよう、`LineStackingStrategy="MaxHeight"` か文字より大きい `LineHeight` を使います。

<figure class="article-figure">
  <img src="/images/wpf-standard-control-demo/verification/textblock/textblock-lineheight.svg" alt="デモアプリの 4 行の文字の高さの表。LineHeight なしで 63.84、LineHeight 5 では BlockLineHeight で 20、MaxHeight で 63.84、LineHeight 30 ではどちらも 120" width="406" height="170" loading="lazy">
  <figcaption><code>LineHeight</code> と <code>LineStackingStrategy</code> ごとの、デモアプリの 4 行の高さ。.NET 10 / Windows 11 で計測。</figcaption>
</figure>

## デモアプリで試す

![デモアプリの TextBlock のページ。左にコントロールの一覧、右に最初の節の Text](/images/wpf-standard-control-demo/textblock.png){: .screenshot-img}

デモアプリの TextBlock のページには、`Text`、`TextWrapping`、`TextTrimming`、`TextAlignment`、`Padding`、フォントの各プロパティ、`Background`、`Foreground`、`LineHeight` と `LineStackingStrategy` の欄があります。`Text` の欄はテキストボックスにバインドし、`TextAlignment` の欄は折り返しを有効にして `Left`・`Right`・`Center`・`Justify` の 4 つの値を並べています。各欄の下の「Show Code」リンクで、その欄の XAML を表示できます。次の XAML は、`LineHeight` の欄（`TextBlockUsageControl.xaml`）から、スタイルと周囲の GroupBox を省き、名前空間の宣言を加えたものです。`markupextensions` はデモアプリ独自のマークアップ拡張の接頭辞です。

```xml
<StackPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
            xmlns:markupextensions="clr-namespace:WPFStandardControlDemoApp.Common.MarkupExtensions">
  <TextBox x:Name="LineHeightTextTextBox" AcceptsReturn="True"
           Text="TEXTBLOCK&#10;TEXTBLOCK&#10;TEXTBLOCK&#10;TEXTBLOCK" />
  <TextBox x:Name="LineHeightTextBox" Text="5" />
  <ComboBox x:Name="LineStackingStrategyComboBox"
            DisplayMemberPath="Name"
            ItemsSource="{markupextensions:EnumBindingSource EnumType=LineStackingStrategy}"
            SelectedValuePath="Value" SelectedIndex="0" />

  <TextBlock x:Name="LineHeightResultTextBlock"
             LineHeight="{Binding Text, ElementName=LineHeightTextBox}"
             LineStackingStrategy="{Binding SelectedValue, ElementName=LineStackingStrategyComboBox}"
             Text="{Binding Text, ElementName=LineHeightTextTextBox}" />
</StackPanel>
```

## 関連するコントロールと記事

- [Label](/ja/apps/wpf-standard-control-demo/label.html) — フォーカスを移すアクセスキーを持つ見出しです。
- [TextBox](/ja/apps/wpf-standard-control-demo/textbox.html) — 利用者が編集できる、または読み取り専用で選択できる文字です。
- [WPFで編集不可のままテキストを選択・コピー可能に表示する方法](/ja/articles/wpf-selectable-readonly-text-display/) — 文字を選択できる必要があるときに使うものを扱います。
- [WPF で Label を大量配置すると遅い原因と TextBlock への置き換え指針](/ja/articles/wpf-label-vs-textblock-performance/) — Label と TextBlock の負担の違いを扱います。

## ソースコードと計測の方法

このページの挙動の記述は、すべて .NET 10 / Windows 11 で実際に動かして確かめたものです。計測には、このサイトのスクリーンショット生成ツールの [`TextBlockDemoScene`](https://github.com/s-iguchi09/s-iguchi09.github.io/blob/main/tools/screenshot-capture/Scenes/TextBlockDemoScene.cs){: target="_blank" rel="noopener noreferrer"} を使いました。TextBlock は `Measure` と `Arrange` でレイアウトしました。行数は高さを 1 行の高さで割った値で、「clipped」は与えた幅で WPF のレイアウトによる切り抜きが働いたことを表します。大きさはフォントと表示スケールで変わり、計測したマシンでの値です。

[GitHub で TextBlock のソースコードを見る →](https://github.com/s-iguchi09/WPFStandardControlDemoApp/tree/main/src/WPFStandardControlDemoApp/Features/TextBlockUsage){: target="_blank" rel="noopener noreferrer"}
