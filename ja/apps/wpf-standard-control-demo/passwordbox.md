---
layout: control-demo
permalink: /ja/apps/wpf-standard-control-demo/passwordbox.html
title: "PasswordBox"
badge: "Inputs"
lead: "PasswordBox は、入力した文字ごとにマスク文字を表示し、文字列をコピーさせないテキスト入力です。"
description: "WPF の PasswordBox を .NET 10 で実測して解説します。Password はバインドできない CLR プロパティであること、MaxLength が入力にだけ効くこと、IsSelectionActive がフォーカスを表すこと、コピーできないことを確かめます。"
---

## Password がバインドできない理由と、値の取り出し方

**PasswordBox** は `TextBoxBase` ではなく `Control` を継承しています。`Password` プロパティは通常の CLR プロパティで、`PasswordProperty` が存在しないため、バインドの対象にはできません。コードや XAML から値を設定することはできます。デモアプリは属性として `Password="PASSWORD"` を指定しており、この値はそのまま読み出せました。

ViewModel へ値を渡すには、`PasswordChanged` か添付ビヘイビアを使います。`PasswordChanged` は、入力では 1 文字ごとに 1 回（「abc」で 3 回）、コードからの `Password` の設定と `Clear()` ではそれぞれ 1 回発生したので、ハンドラーはすべての変更を受け取れます。`Clear()` で `Password` は空になりました。値が不要になったら呼びます。

内部では、文字列は `PasswordTextContainer` の `SecureString` 型のフィールドに保持されていました。`Password` はそれを通常の `string` として返します。`SecurePassword` は、呼ぶたびに書き込み可能な新しい `SecureString` を返し、同じインスタンスではありませんでした。受け取ったコピーは呼び出した側のものなので、使い終わったら破棄します。

`PasswordChar` は `Password` と違って依存関係プロパティです。メタデータの既定値は `*` ですが、既定のスタイルが `●`（U+25CF）を設定するため、画面にはこちらが表示されます。

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/passwordbox/passwordbox-defaults.svg" alt="PasswordBox の型と既定値を示す表。基底クラスは Control、PasswordProperty はなく PasswordCharProperty はあり、PasswordChar のメタデータの既定値はアスタリスクで既定のスタイルは黒丸、内部は SecureString で保持され、SecurePassword は呼ぶたびに新しいインスタンスを返す" width="967" height="410" loading="lazy">
  <figcaption>型、プロパティ、既定値、パスワードの保持のしかた。.NET 10 / Windows 11 で計測。</figcaption>
</figure>

## MaxLength、コピー、IsSelectionActive が実際にすること

`MaxLength` が制限するのは入力だけで、既定値は 0（無制限）です。`MaxLength="8"` で "1234567890" と入力すると "12345678" になりました。コードから設定した `Password` は切り詰められず、10 文字すべてが残りました。`MaxLength` は入力の補助と考え、値を使う側で長さを確かめます。

すべての文字を選択した状態でも、コピーと切り取りのコマンドは実行できず、貼り付けは実行できました。

`IsSelectionActive` は読み取り専用で、名前に反して、文字が選択されているかどうかは表しません。フォーカスを得た時点で、何も選択していなくても `True` になり、すべてを選択しても `True` のままでした。フォーカスが別のコントロールへ移ると、選択は残っていても `False` になりました。キーボードフォーカスに従うので、選択の有無の判定には使いません。

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/passwordbox/passwordbox-behavior.svg" alt="PasswordBox の挙動を示す表。MaxLength 8 は入力を切り詰めるがコードからの Password は切らず、PasswordChanged は入力した 1 文字ごとに 1 回、Password の設定と Clear でそれぞれ 1 回発生し、コピーと切り取りは実行できず貼り付けはでき、IsSelectionActive は選択ではなくキーボードフォーカスに従う" width="936" height="350" loading="lazy">
  <figcaption>入力、イベント、クリップボードのコマンド、<code>IsSelectionActive</code>。.NET 10 / Windows 11 で計測。</figcaption>
</figure>

## キャレットと選択範囲の強調表示

`CaretBrush` の既定値は `null` です。`SelectionBrush` の既定値はスタイルではなくプロパティの既定値から来ており、計測した環境では `#FF0078D7` でした。`SelectionOpacity` の既定値は 0.4 です。`IsInactiveSelectionHighlightEnabled` は、フォーカスが外れた後も選択範囲の強調表示を残すかどうかを決め、既定値は `False` です。強調表示そのものは画面の描画なので、このページでは測っていません。

## デモアプリで試す

![デモアプリの PasswordBox のページ。左にコントロールの一覧、右に最初の節の CaretBrush](/images/wpf-standard-control-demo/passwordbox.png){: .screenshot-img}

デモアプリの PasswordBox のページには、`CaretBrush`、`IsInactiveSelectionHighlightEnabled`、`IsSelectionActive`、`MaxLength`、`PasswordChar`、`SelectionBrush`、`SelectionOpacity` の欄があります。ブラシは、"PASSWORD" が入った PasswordBox に対して名前付きのブラシの一覧から選びます。`MaxLength` は 8、`SelectionOpacity` は 0.5 をテキストボックスで指定しています。`IsInactiveSelectionHighlightEnabled` の欄は `True` から始まり、フォーカスを行き来できるように PasswordBox を 2 つ並べています。各欄の下の「Show Code」リンクで、その欄の XAML を表示できます。次の XAML は、`PasswordChar` の欄（`PasswordBoxUsageControl.xaml`）から、スタイルと周囲の GroupBox を省き、名前空間の宣言を加えたものです。テキストボックスの 1 文字目をバインドし、空のときは `●` を使います。

```xml
<StackPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <TextBox x:Name="PasswordCharTextBox" MaxLength="1" Text="*" />

  <PasswordBox x:Name="PasswordCharPasswordBox"
               PasswordChar="{Binding Text[0], ElementName=PasswordCharTextBox, FallbackValue=●}" />
</StackPanel>
```

## 関連するコントロールと記事

- [TextBox](/ja/apps/wpf-standard-control-demo/textbox.html) — 通常のテキスト入力です。`MaxLength` は同じように振る舞います。
- [Label](/ja/apps/wpf-standard-control-demo/label.html) — 隣の入力欄の名前を示す文字列です。

## ソースコードと計測の方法

このページの挙動の記述は、すべて .NET 10 / Windows 11 で実際に動かして確かめたものです。計測には、このサイトのスクリーンショット生成ツールの [`PasswordBoxDemoScene`](https://github.com/s-iguchi09/s-iguchi09.github.io/blob/main/tools/screenshot-capture/Scenes/PasswordBoxDemoScene.cs){: target="_blank" rel="noopener noreferrer"} を使いました。文字の入力は、キーボードと同じ `TextInput` の経路で文字を届ける `TextCompositionManager` で、1 文字ずつ再現しました。クリップボードは操作せず、コピー・切り取り・貼り付けのコマンドが実行できるかどうかだけを読みました。

[GitHub で PasswordBox のソースコードを見る →](https://github.com/s-iguchi09/WPFStandardControlDemoApp/tree/main/src/WPFStandardControlDemoApp/Features/PasswordBoxUsage){: target="_blank" rel="noopener noreferrer"}
