---
layout: control-demo
permalink: /ja/apps/wpf-standard-control-demo/groupbox.html
title: "GroupBox"
badge: "Display"
lead: "GroupBox は、フォームの 1 つの区切りの入力欄など、関連するコントロールを見出し付きの枠で囲むコントロールです。"
description: "WPF の GroupBox を .NET 10 で実測して解説。HeaderStringFormat が効く条件、複数行の見出し、Padding、内容に引き継がれるプロパティ、見出しのアクセスキーでフォーカスがどこへ移るかを、実際に動かして確かめます。"
---

## 概要

**GroupBox** は `HeaderedContentControl` を継承しています。既定の枠は `#FFD5DFE5` の幅 1 で、`Padding` は 0 です。UI オートメーションからは、見出しを名前とする `Group` に見えます。「Personal information」という見出しの GroupBox は、その名前で報告されました。

見出しはキーボード操作にも関わります。文字列の見出しはアクセスキーを認識して表示されるので、`Header="_Name"` では `N` がアクセスキーになりました。これを押すと、フォーカスは見出しではなくグループの中の最初の TextBox へ移りました。

デモアプリには、見出し、`HeaderStringFormat`、内容と、`Control` のプロパティ（`Padding`、フォントの各プロパティ、`Background`、`Foreground`、枠）の欄があります。各欄の下の「Show Code」リンクで、その欄の XAML を表示できます。

## 画面キャプチャ

![デモアプリの GroupBox のページ。左にコントロールの一覧、右に最初の節の Header(ContentControl) / HasHeader(ContentControl)](/images/wpf-standard-control-demo/groupbox.png){: .screenshot-img}

## デモしているプロパティ

以下のプロパティが WPF 標準コントロールデモアプリでインタラクティブにデモされています。各プロパティの挙動は .NET 10 で実測したものです（[実測した挙動](#measured-behavior)を参照）。

| プロパティ | 設定値 | 説明 |
| --- | --- | --- |
| `Header / HasHeader (HeaderedContentControl)` | `object / bool (ReadOnly)` | 見出しと、それがあるかどうかです。デモアプリは空のテキストボックスを `null` に変換します。`null` では `HasHeader` が `False` になり、見出しの領域の高さは 0 でした。改行を含む文字列は 2 行で表示され、見出しの高さは 1 行で 15.96、2 行で 31.92 でした。そのために TextBlock を使う必要はありません。 |
| `HeaderStringFormat (HeaderedContentControl)` | `string` | 見出しの書式です。デモアプリと同じ `"Header={0}."` では、見出し `HEADER` は `Header=HEADER.` と表示されました。効くのは要素でない見出しだけで、TextBlock を見出しにすると `HEADER` のままでした。 |
| `Content (ContentControl)` | `object` | 枠の中に置く 1 つの子です。複数のコントロールをまとめるときは、パネルを内容にします。 |
| `Padding (Control)` | `Thickness` | 枠と内容の間の余白です。既定のテンプレートでも効き、`Padding="20"` では内容が 20 下へ移りました。 |
| `FontWeight / FontStyle / FontStretch / FontSize / FontFamily (Control)` | フォントの値 | 見出しと内容のフォントです。GroupBox に `FontSize="20"` を設定すると、見出しの文字も内容の TextBlock も 20 になりました。 |
| `Background / Foreground (Control)` | `Brush` | `Foreground` は見出しと内容の両方の文字に届きます。`Background` は、見出しの中ほどから下の、枠の内側を塗ります。見出しが上から 1〜27.6 にある GroupBox で、塗られた範囲は 13.8 から始まりました。内容はこの範囲の中にあるので、内容の後ろにも背景が見えます。 |
| `BorderBrush / BorderThickness (Control)` | `Brush / Thickness` | 枠の色と幅です。既定値は `#FFD5DFE5` と 1 です。 |

## XAML 使用例

デモアプリ（`GroupBoxUsageControl.xaml`）の `HeaderStringFormat` の欄から、スタイルと周囲の GroupBox を省き、名前空間の宣言を加えたものです。

```xml
<StackPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <TextBox x:Name="HeaderForHeaderStringFormatTextBox" Text="HEADER" />

  <GroupBox x:Name="HeaderStringFormatGroupBox"
            Header="{Binding Text, ElementName=HeaderForHeaderStringFormatTextBox}"
            HeaderStringFormat="Header={0}." />
</StackPanel>
```

## 主な使用例

- **フォームの区切り** — 個人情報・住所・支払いを別々のグループにします。
- **設定のダイアログ** — 関連する設定を 1 つの見出しの下にまとめます。
- **データを含む見出し** — 「Items (12)」のような見出しを `HeaderStringFormat` で作ります。

## ヒントとベストプラクティス

- **見出しにアクセスキーを付ける** — `Header="_Name"` で、キーボードからグループの中へ移れます。
- **意味の分かる見出しにする** — 見出しはスクリーンリーダーにとってのグループの名前になります。
- **`HeaderStringFormat` は要素でない見出しにだけ使う** — 見出しが要素のときは無視されます。
- **GroupBox の `Background` は見出しの中ほどから塗られると考える** — 見出しの上半分は塗られる範囲の外に出ます。

## 実測した挙動 {#measured-behavior}

このページの挙動の記述は、すべて .NET 10 / Windows 11 で実際に動かして確かめたものです。計測には、このサイトのスクリーンショット生成ツールの [`GroupBoxDemoScene`](https://github.com/s-iguchi09/s-iguchi09.github.io/blob/main/tools/screenshot-capture/Scenes/GroupBoxDemoScene.cs){: target="_blank" rel="noopener noreferrer"} を使いました。見出しは、テンプレートの見出しの ContentPresenter の中の文字の要素から読みました。アクセスキーは、<kbd>Alt</kbd> と一緒に押したキーを処理する `AccessKeyManager.ProcessKey` で送りました。位置は GroupBox の左上からのもので、大きさは計測したマシンでの値です。

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/groupbox/groupbox-behavior.svg" alt="GroupBox の計測結果の表。HeaderedContentControl を継承し枠は #FFD5DFE5 の幅 1 で余白はなく、null の見出しは高さを取らず、HeaderStringFormat は文字列の見出しには効くが TextBlock には効かず、改行を含む見出しは 2 行の高さになり、Padding 20 で内容が 20 下がり、FontSize と Foreground は見出しと内容に届き、Background は見出しの中ほどから塗られて内容の範囲も覆い、UI オートメーションは見出しを名前とする Group を報告し、見出しのアクセスキーで中の最初の TextBox にフォーカスが移る" width="1077" height="470" loading="lazy">
  <figcaption>見出し、余白、引き継がれるプロパティ、アクセスキー。.NET 10 / Windows 11 で計測。</figcaption>
</figure>

## 関連コントロール

- [Expander](/ja/apps/wpf-standard-control-demo/expander.html) — 利用者が畳める見出しと内容です。
- [Label](/ja/apps/wpf-standard-control-demo/label.html) — こちらも文字をアクセスキーを認識して表示します。
- [Grid](/ja/apps/wpf-standard-control-demo/grid.html) — ラベルと入力欄を並べるための、GroupBox のよくある内容です。

## ソースコード

このデモ画面のソースコードは GitHub で公開しています。アプリでは、各欄の下にある「Show Code」リンクでその欄の XAML を表示できます。

[GitHub で GroupBox のソースコードを見る →](https://github.com/s-iguchi09/WPFStandardControlDemoApp/tree/main/src/WPFStandardControlDemoApp/Features/GroupBoxUsage){: target="_blank" rel="noopener noreferrer"}
