---
layout: control-demo
permalink: /ja/apps/wpf-standard-control-demo/expander.html
title: "Expander"
badge: "Display"
lead: "Expander は、見出しを常に表示し、見出しのクリックで内容を表示したり隠したりするコントロールです。"
description: "WPF の Expander を .NET 10 で実測して解説。折りたたんだ内容にかかる負担、展開にアニメーションがあるか、ExpandDirection ごとの配置、見出しと内容にどのプロパティが届くかを確かめます。見出しのクリックは実際のマウスで試しました。"
---

## 概要

**Expander** は `HeaderedContentControl` を継承しています。既定のテンプレートの見出しは、`HeaderSite` という名前の `ToggleButton` です。テンプレートには表示状態のグループ（VisualStateGroup）もアニメーションもなく、`IsExpanded`・`ExpandDirection`・`IsEnabled` のトリガーで切り替えています。`IsExpanded` を `False` にすると、内容はその場で `Collapsed` になりました。

折りたたんだ内容の負担は、思われているより小さいものです。最初から折りたたんだ Expander では、内容の要素は作られて `Loaded` も発生しましたが、測られませんでした。中に置いた 1000 項目の ListBox は、`ListBoxItem` を 1 つも作りませんでした。展開すると内容が測られ、ListBox は 11 個の項目を作り、内容にはもう一度 `Loaded` が発生しました。

デモアプリには、`ExpandDirection`、`IsExpanded`、見出し、内容と、`Control` のプロパティ（`Padding`、フォントの各プロパティ、`Background`、`Foreground`、枠）の欄があります。各欄の下の「Show Code」リンクで、その欄の XAML を表示できます。

## 画面キャプチャ

![デモアプリの Expander のページ。左にコントロールの一覧、右に最初の節の ExpandDirection](/images/wpf-standard-control-demo/expander.png){: .screenshot-img}

## デモしているプロパティ

以下のプロパティが WPF 標準コントロールデモアプリでインタラクティブにデモされています。各プロパティの挙動は .NET 10 で実測したものです（[実測した挙動](#measured-behavior)を参照）。

| プロパティ | 設定値 | 説明 |
| --- | --- | --- |
| `ExpandDirection` | `Down / Up / Left / Right` | 見出しに対して内容をどちらに出すかで、既定値は `Down` です。80 × 40 の内容で試すと、`Down` では見出しが上、内容がその下に、`Up` では内容が見出しの上に来ました。`Left` と `Right` では、見出しは内容の横の幅 49、高さ 39 の列になり、文字は横書きのまま列の下の方に置かれました。内容は `Left` で左、`Right` で右でした。 |
| `IsExpanded` | `bool` | 内容を表示するかどうかで、既定値は `False`、既定で TwoWay にバインドされます。デモアプリは `Mode` を指定せずにチェックボックスにバインドしているので、両者は連動します。実際のマウスで見出しをクリックすると、Expander が開き、チェックボックスにチェックが付き、`Expanded` が発生しました。チェックを外すと閉じて、`Collapsed` が発生しました。 |
| `Header / HasHeader (HeaderedContentControl)` | `object / bool (ReadOnly)` | 見出しの内容と、それがあるかどうかです。デモアプリは空のテキストボックスを `null` に変換します。`Header` が `null` のとき `HasHeader` は `False` でしたが、見出しの ToggleButton は高さ 19 で表示されたままでした。消すには独自のテンプレートが必要です。 |
| `Content (ContentControl)` | `object` | 表示したり隠したりする内容です。折りたたんでいる間も作られて読み込まれますが、レイアウトはされません（上を参照）。 |
| `Padding (Control)` | `Thickness` | Expander の内側の余白で、内容だけでなく見出しにも効きます。`Padding="20"` では、見出しの高さが 19 から 59 になり、内容もその分下へ移りました。 |
| `FontWeight / FontStyle / FontStretch / FontSize / FontFamily (Control)` | フォントの値 | 見出しと内容のフォントです。Expander に `FontWeight="Bold"` を設定すると、見出しの文字も内容の TextBlock も太字になりました。 |
| `Background / Foreground (Control)` | `Brush` | `Foreground` は見出しと内容の両方の文字に届き、`Red` では両方が赤になりました。`Background` は見出しと内容の両方の後ろに塗られ、幅 200 の Expander で、塗られた範囲は 200 × 38.96 で見出しと内容を含んでいました。`Background` 自体は継承されるプロパティではないので、色が付くのは内容の後ろの領域です。 |
| `BorderBrush / BorderThickness (Control)` | `Brush / Thickness` | 見出しを含めた Expander 全体を囲む枠です。`Padding` を付けずに単独で測ると、`BorderThickness="5"` では見出しが各辺から 5 内側へ移って (1, 1) から (6, 6) になり、内容も 5 下がりました。 |

## XAML 使用例

デモアプリ（`ExpanderUsageControl.xaml`）の `IsExpanded` の欄から、スタイルと周囲の GroupBox を省き、名前空間の宣言を加えたものです。

```xml
<StackPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <CheckBox x:Name="IsExpandedCheckBox" VerticalContentAlignment="Center" Content="IsExpanded" />

  <Expander x:Name="IsExpandedExpander"
            Content="CONTENT"
            Header="Expander"
            IsExpanded="{Binding IsChecked, ElementName=IsExpandedCheckBox}" />
</StackPanel>
```

## 主な使用例

- **詳細な設定** — 必要になるまで畳んでおく上級者向けの設定です。
- **長いフォーム** — 入力を終えた部分を畳めるフォームの区切りです。
- **詳細の表示** — エラーメッセージの下にスタックトレースを畳んで置くような、要約と詳細の組み合わせです。

## ヒントとベストプラクティス

- **内容の `Loaded` で重い処理を始めない** — 折りたたんでいる間にも動き、展開したときにもう一度動きます。
- **折りたたんだ Expander の中の一覧は軽い** — 展開するまで項目は作られません。
- **状態を保つなら `IsExpanded` をバインドする** — 既定で TwoWay なので、ビューモデルのプロパティが利用者のクリックに追従します。
- **見出しを消したり、アニメーションを付けたりするなら独自のテンプレートにする** — 既定のテンプレートは、見出しがなくても矢印を表示し、切り替えは一瞬です。
- **見出しと内容をまとめて色付けするなら Expander に `Background` を設定する** — 両方の後ろに塗られます。

## 実測した挙動 {#measured-behavior}

このページの挙動の記述は、すべて .NET 10 / Windows 11 で実際に動かして確かめたものです。計測には、このサイトのスクリーンショット生成ツールの [`ExpanderDemoScene`](https://github.com/s-iguchi09/s-iguchi09.github.io/blob/main/tools/screenshot-capture/Scenes/ExpanderDemoScene.cs){: target="_blank" rel="noopener noreferrer"} を使いました。見出しのクリックは、計測用のウィンドウの上で実際のマウスを使って行いました。位置は Expander の左上からのもので、大きさは計測したマシンでの値です。

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/expander/expander-behavior.svg" alt="Expander の動作の計測結果の表。HeaderedContentControl を継承し IsExpanded の既定値は False で既定で TwoWay、見出しは HeaderSite という ToggleButton、テンプレートには表示状態がなくトリガーだけで、折りたたみは即座に行われ、見出しを実際にクリックすると開いてバインドしたチェックボックスにチェックが付き、折りたたんだ内容は読み込まれるが測られず、展開するまで ListBox の項目は作られない" width="1132" height="320" loading="lazy">
  <figcaption>テンプレート、クリック、折りたたんだ内容。.NET 10 / Windows 11 で計測。</figcaption>
</figure>

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/expander/expander-layout.svg" alt="Expander の配置の計測結果の表。Down・Up・Left・Right での見出しと内容の位置と、見出しの文字が横書きのままであること、見出しが null でも ToggleButton が残ること、FontWeight と Foreground が見出しと内容に届き、Background は見出しと内容の両方の後ろに塗られ、別々に測った Padding と BorderThickness は見出しの周りにも効くこと" width="1163" height="380" loading="lazy">
  <figcaption><code>ExpandDirection</code>、見出し、<code>Control</code> のプロパティ。.NET 10 / Windows 11 で計測。</figcaption>
</figure>

## 関連コントロール

- [GroupBox](/ja/apps/wpf-standard-control-demo/groupbox.html) — 常に表示する内容を、見出しと枠で囲みます。
- [ToggleButton](/ja/apps/wpf-standard-control-demo/togglebutton.html) — 見出しを構成している要素です。
- [TreeView](/ja/apps/wpf-standard-control-demo/treeview.html) — 少数の区切りではなく、階層になった項目を畳みます。

## ソースコード

このデモ画面のソースコードは GitHub で公開しています。アプリでは、各欄の下にある「Show Code」リンクでその欄の XAML を表示できます。

[GitHub で Expander のソースコードを見る →](https://github.com/s-iguchi09/WPFStandardControlDemoApp/tree/main/src/WPFStandardControlDemoApp/Features/ExpanderUsage){: target="_blank" rel="noopener noreferrer"}
