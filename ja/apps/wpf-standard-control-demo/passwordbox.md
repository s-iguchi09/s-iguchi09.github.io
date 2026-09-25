---
layout: control-demo
permalink: /ja/apps/wpf-standard-control-demo/passwordbox.html
title: "PasswordBox"
badge: "Inputs"
lead: "PasswordBox は、入力した文字ごとにマスク文字を表示し、文字列をコピーさせないテキスト入力です。"
description: "WPF の PasswordBox を .NET 10 で実測して解説します。Password はバインドできない CLR プロパティであること、MaxLength が入力にだけ効くこと、IsSelectionActive がフォーカスを表すこと、コピーできないことを確かめます。"
---

## 概要

**PasswordBox** は `TextBoxBase` ではなく `Control` を継承しています。`Password` プロパティは通常の CLR プロパティで、`PasswordProperty` が存在しないため、バインドの対象にはできません。コードや XAML から値を設定することはできます。デモアプリは属性として `Password="PASSWORD"` を指定しており、この値はそのまま読み出せました。`PasswordChanged` は、入力では 1 文字ごとに 1 回（「abc」で 3 回）、コードからの `Password` の設定と `Clear()` ではそれぞれ 1 回発生したので、ハンドラーはすべての変更を受け取れます。

内部では、文字列は `PasswordTextContainer` の `SecureString` 型のフィールドに保持されていました。`Password` はそれを通常の `string` として返します。`SecurePassword` は、呼ぶたびに書き込み可能な新しい `SecureString` を返し、同じインスタンスではありませんでした。受け取ったコピーは呼び出した側のものなので、使い終わったら破棄します。

すべての文字を選択した状態でも、コピーと切り取りのコマンドは実行できず、貼り付けは実行できました。デモアプリには以下の各プロパティの欄があり、各欄の下の「Show Code」リンクでその欄の XAML を表示できます。

## 画面キャプチャ

![passwordbox demo screen](/images/wpf-standard-control-demo/passwordbox.png){: .screenshot-img}

## デモしているプロパティ

以下のプロパティが WPF 標準コントロールデモアプリでインタラクティブにデモされています。各プロパティの挙動は .NET 10 で実測したものです（[実測した挙動](#measured-behavior)を参照）。

| プロパティ | 設定値 | 説明 |
| --- | --- | --- |
| `CaretBrush` | `Brush` | 文字カーソル（キャレット）のブラシで、既定値は `null` です。デモアプリでは、"PASSWORD" が入った PasswordBox に対し、名前付きのブラシの一覧から選びます。 |
| `IsInactiveSelectionHighlightEnabled` | `bool` | フォーカスが外れた後も選択範囲の強調表示を残すかどうかを決めます。強調表示そのものは画面の描画なので、このページでは測っていません。既定値は `False` です。デモアプリは `True` から始まり、フォーカスを行き来できるように PasswordBox を 2 つ並べています。 |
| `IsSelectionActive` | `bool（読み取り専用）` | PasswordBox がキーボードフォーカスを持っているかどうかです。名前に反して、文字が選択されているかどうかは表しません。フォーカスを得た時点で、何も選択していなくても `True` になり、すべてを選択しても `True` のままでした。フォーカスが別のコントロールへ移ると、選択は残っていても `False` になりました。 |
| `MaxLength` | `int`（0 = 無制限） | ユーザーが入力できる最大の文字数で、既定値は 0 です。`MaxLength="8"` で "1234567890" と入力すると "12345678" になりました。コードから設定した `Password` は切り詰められず、10 文字すべてが残りました。デモアプリでは、テキストボックスで 8 を指定しています。 |
| `PasswordChar` | `char` | 入力した文字の代わりに表示する文字です。プロパティのメタデータの既定値は `*` ですが、既定のスタイルが `●`（U+25CF）を設定するため、画面にはこちらが表示されます。`Password` と違って依存関係プロパティなので、デモアプリでは "\*" が入ったテキストボックスの 1 文字目にバインドしています。 |
| `SelectionBrush` | `Brush` | 選択範囲の強調表示のブラシです。既定値はスタイルではなくプロパティの既定値から来ており、計測した環境では `#FF0078D7` でした。デモアプリでは、名前付きのブラシの一覧から選びます。 |
| `SelectionOpacity` | `double` | 選択範囲の強調表示の不透明度で、既定値は 0.4 です。デモアプリでは、テキストボックスで 0.5 を指定しています。 |

## XAML 使用例

デモアプリ（`PasswordBoxUsageControl.xaml`）の `PasswordChar` の欄から、スタイルと周囲の GroupBox を省き、名前空間の宣言を加えたものです。テキストボックスの 1 文字目をバインドし、空のときは `●` を使います。

```xml
<StackPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <TextBox x:Name="PasswordCharTextBox" MaxLength="1" Text="*" />

  <PasswordBox x:Name="PasswordCharPasswordBox"
               PasswordChar="{Binding Text[0], ElementName=PasswordCharTextBox, FallbackValue=●}" />
</StackPanel>
```

## 主な使用例

- **サインイン画面** — ユーザー名を `TextBox`、パスワードを PasswordBox で入力させます。
- **パスワードの変更** — 2 つの PasswordBox を置き、それぞれの `PasswordChanged` で比べます。
- **設定の中の秘密情報** — 表示もコピーもさせたくない API キーや接続用のパスワードに使います。

## ヒントとベストプラクティス

- **ViewModel へは `PasswordChanged` か添付ビヘイビアで渡す** — `Password` は依存関係プロパティではないため、バインドできません。
- **`MaxLength` は入力の補助と考える** — コードから設定した `Password` には効かないので、値を使う側で長さを確かめます。
- **`SecurePassword` の戻り値は破棄する** — 呼ぶたびに新しい `SecureString` が作られます。
- **選択の有無を `IsSelectionActive` で判定しない** — このプロパティはキーボードフォーカスに従います。
- **不要になったら `Clear()` を呼ぶ** — `Password` が空になり、`PasswordChanged` も発生しました。

## 実測した挙動 {#measured-behavior}

このページの挙動の記述は、すべて .NET 10 / Windows 11 で実際に動かして確かめたものです。計測には、このサイトのスクリーンショット生成ツールの [`PasswordBoxDemoScene`](https://github.com/s-iguchi09/s-iguchi09.github.io/blob/main/tools/screenshot-capture/Scenes/PasswordBoxDemoScene.cs){: target="_blank" rel="noopener noreferrer"} を使いました。文字の入力は、キーボードと同じ `TextInput` の経路で文字を届ける `TextCompositionManager` で、1 文字ずつ再現しました。クリップボードは操作せず、コピー・切り取り・貼り付けのコマンドが実行できるかどうかだけを読みました。

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/passwordbox/passwordbox-behavior.svg" alt="PasswordBox の挙動を示す表。MaxLength 8 は入力を切り詰めるがコードからの Password は切らず、PasswordChanged は入力した 1 文字ごとに 1 回、Password の設定と Clear でそれぞれ 1 回発生し、コピーと切り取りは実行できず貼り付けはでき、IsSelectionActive は選択ではなくキーボードフォーカスに従う" width="936" height="350" loading="lazy">
  <figcaption>入力、イベント、クリップボードのコマンド、<code>IsSelectionActive</code>。.NET 10 / Windows 11 で計測。</figcaption>
</figure>

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/passwordbox/passwordbox-defaults.svg" alt="PasswordBox の型と既定値を示す表。基底クラスは Control、PasswordProperty はなく PasswordCharProperty はあり、PasswordChar のメタデータの既定値はアスタリスクで既定のスタイルは黒丸、内部は SecureString で保持され、SecurePassword は呼ぶたびに新しいインスタンスを返す" width="967" height="410" loading="lazy">
  <figcaption>型、プロパティ、既定値、パスワードの保持のしかた。.NET 10 / Windows 11 で計測。</figcaption>
</figure>

## 関連コントロール

- [TextBox](/ja/apps/wpf-standard-control-demo/textbox.html) — 通常のテキスト入力です。`MaxLength` は同じように振る舞います。
- [Label](/ja/apps/wpf-standard-control-demo/label.html) — 隣の入力欄の名前を示す文字列です。

## ソースコード

このデモ画面のソースコードは GitHub で公開しています。アプリでは、各欄の下にある「Show Code」リンクでその欄の XAML を表示できます。

[GitHub で PasswordBox のソースコードを見る →](https://github.com/s-iguchi09/WPFStandardControlDemoApp/tree/main/src/WPFStandardControlDemoApp/Features/PasswordBoxUsage){: target="_blank" rel="noopener noreferrer"}
