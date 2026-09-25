---
layout: control-demo
permalink: /ja/apps/wpf-standard-control-demo/label.html
title: "Label"
badge: "Display"
lead: "Label は、主に入力欄の見出しを表示し、アクセスキーでその入力欄へキーボードフォーカスを移せるコントロールです。"
description: "WPF の Label を .NET 10 で実測して解説。アクセスキーでフォーカスがどこへ移るかを Target の有無や ToolBar の中で実際に確かめ、既定値と、Target がスクリーンリーダー向けの名前にはならないことも示します。"
---

## 概要

**Label** は `ContentControl` を継承しています。Label 自身はフォーカスを受け取りません。`Focusable` も `IsTabStop` も `False` で、Label の前の TextBox から <kbd>Tab</kbd> を押すと、Label の後の TextBox へ直接移りました。`Padding` の既定値は 4 辺とも 5 で、内容は `Left`・`Top` に配置されます。

文字列の内容はアクセスキーを認識して表示されます。アンダースコアの次の文字がアクセスキーになり、アンダースコア自体は表示されません。この点とアンダースコアの表示のしかたは、下にリンクした記事で扱っています。改行を含む文字列は 2 行で表示され、Label の高さは 1 行で 25.96、2 行で 41.92 でした。

デモアプリには、`Target`、内容と、`Control` のプロパティ（`Padding`、フォントの各プロパティ、`Background`、`Foreground`、枠、内容の配置）の欄があります。各欄の下の「Show Code」リンクで、その欄の XAML を表示できます。

## 画面キャプチャ

![label demo screen](/images/wpf-standard-control-demo/label.png){: .screenshot-img}

## デモしているプロパティ

以下のプロパティが WPF 標準コントロールデモアプリでインタラクティブにデモされています。各プロパティの挙動は .NET 10 で実測したものです（[実測した挙動](#measured-behavior)を参照）。

| プロパティ | 設定値 | 説明 |
| --- | --- | --- |
| `Target` | `UIElement` | Label のアクセスキーを押したときにフォーカスを受け取る要素です。デモアプリの 2 組、`_Name(Press Alt+N)` と `_Age(Press Alt+A)` では、アクセスキー `N` と `A` でフォーカスが Name と Age のテキストボックスへ移りました。`Target` のない `_Plain` のアクセスキーでは、フォーカスは動きませんでした。フォーカススコープが違っても問題はなく、ToolBar の中の TextBox を `Target` にした Label でも、フォーカスがその中へ移りました。`Label` クラスのドキュメントには、Target を設定すると UI オートメーションがラベルの文字を対象の名前に使うと書かれていますが、`Target` はスクリーンリーダー向けの名前になりませんでした。スクリーンリーダーも使う UI オートメーションのクライアント API で読むと、Name のテキストボックスの名前は空で、`LabeledBy` もありませんでした。これは未解決の報告 [dotnet/wpf#9185](https://github.com/dotnet/wpf/issues/9185){: target="_blank" rel="noopener noreferrer"} と一致します。`AutomationProperties.LabeledBy` を手動で設定すると効き、Age のテキストボックスの名前は「Age(Press Alt+A)」になりました。 |
| `Content (ContentControl)` | `object` | 見出しです。デモアプリは複数行のテキストボックスにバインドしているので、文字に改行を入れると 2 行の Label になります。 |
| `Padding (Control)` | `Thickness` | 内容の周りの余白で、既定値は 4 辺とも 5 です。 |
| `FontWeight / FontStyle / FontStretch / FontSize / FontFamily (Control)` | フォントの値 | 見出しのフォントです。デモアプリにはそれぞれの欄があります。 |
| `Background / Foreground / BorderBrush / BorderThickness (Control)` | `Brush / Thickness` | Label の色と枠です。デモアプリにはそれぞれの欄があります。 |
| `HorizontalContentAlignment / VerticalContentAlignment (Control)` | `HorizontalAlignment / VerticalAlignment` | Label の中での内容の位置で、既定値は `Left` と `Top` です。 |

## XAML 使用例

デモアプリ（`LabelUsageControl.xaml`）の `Target` の欄から、スタイルと周囲の GroupBox を省き、名前空間の宣言を加えたものです。

```xml
<Grid xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
      xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Grid.ColumnDefinitions>
    <ColumnDefinition Width="auto" />
    <ColumnDefinition Width="*" />
  </Grid.ColumnDefinitions>
  <Grid.RowDefinitions>
    <RowDefinition />
    <RowDefinition />
  </Grid.RowDefinitions>
  <Label x:Name="TargetNameLabel" Grid.Row="0" Grid.Column="0"
         Content="_Name(Press Alt+N)"
         Target="{Binding ElementName=TargetNameTextBox}" />
  <TextBox x:Name="TargetNameTextBox" Grid.Row="0" Grid.Column="1" />
  <Label x:Name="TargetAgeLabel" Grid.Row="1" Grid.Column="0"
         Content="_Age(Press Alt+A)"
         Target="{Binding ElementName=TargetAgeTextBox}" />
  <TextBox x:Name="TargetAgeTextBox" Grid.Row="1" Grid.Column="1" />
</Grid>
```

## 主な使用例

- **フォームの見出し** — 各入力欄の横に見出しを置き、アクセスキーでその欄へ移れるようにします。
- **ほかのコントロールの見出し** — ComboBox や DatePicker を `Target` にします。アクセスキーで、ComboBox ではそれ自体に、編集可能な ComboBox と DatePicker では中のテキストボックスにフォーカスが移りました。
- **内容のある見出し** — 内容には任意の要素を置けるので、アイコンと文字を並べられます。

## ヒントとベストプラクティス

- **アクセスキーを付けた見出しには必ず `Target` を設定する** — ないとキーを押しても何も起きません。
- **スクリーンリーダー向けの名前は別に付ける** — ドキュメントの説明に反して、`Target` では TextBox に名前が付きませんでした。入力欄の `AutomationProperties.LabeledBy` に Label を設定すると、ラベルの文字が名前になりました。
- **アクセスキーの要らない文字には TextBlock を使う** — 下にリンクした記事で、Label を大量に置いたときの負担を実測しています。

## 実測した挙動 {#measured-behavior}

このページの挙動の記述は、すべて .NET 10 / Windows 11 で実際に動かして確かめたものです。計測には、このサイトのスクリーンショット生成ツールの [`LabelDemoScene`](https://github.com/s-iguchi09/s-iguchi09.github.io/blob/main/tools/screenshot-capture/Scenes/LabelDemoScene.cs){: target="_blank" rel="noopener noreferrer"} を使いました。アクセスキーは <kbd>Alt</kbd> と一緒に押したキーを処理する `AccessKeyManager.ProcessKey` で、<kbd>Tab</kbd> はキーボードから打ったときと同じく WPF の入力管理（InputManager）を通して送りました。UI オートメーションの名前は、スクリーンリーダーも使う UI オートメーションのクライアント API で、同じプロセスの中の、ウィンドウの UI スレッドとは別のスレッドから読みました。大きさは、計測したマシンでの値です。

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/label/label-behavior.svg" alt="Label の計測結果の表。ContentControl を継承しフォーカスもタブ移動も受けず、余白は 5 で内容は Left と Top、Tab は Label を飛ばし、デモアプリのアクセスキー N と A で Name と Age のテキストボックスへフォーカスが移り、Target のない Label では動かず、ToolBar の中の Target にも移り、対象の TextBox には UI オートメーションの名前も LabeledBy も付かず、手動で設定した AutomationProperties.LabeledBy では名前が付き、改行で 2 行の高さになる。Target が ComboBox ならそれ自体に、編集可能な ComboBox と DatePicker なら中のテキストボックスにフォーカスが移る" width="1108" height="380" loading="lazy">
  <figcaption>既定値、アクセスキー、<code>Target</code>、UI オートメーション。.NET 10 / Windows 11 で計測。</figcaption>
</figure>

## 関連コントロール・記事

- [TextBlock](/ja/apps/wpf-standard-control-demo/textblock.html) — アクセスキーもテンプレートもない、軽い文字表示です。
- [TextBox](/ja/apps/wpf-standard-control-demo/textbox.html) — Label の `Target` として最もよく使われます。
- [WPF の Label でアンダーバーが消える理由と回避方法](/ja/articles/wpf-label-underscore-issue/) — Label の文字のアクセスキーと、アンダースコアの表示を扱います。
- [WPF で Label を大量配置すると遅い原因と TextBlock への置き換え指針](/ja/articles/wpf-label-vs-textblock-performance/) — Label を大量に置いたときの負担を TextBlock と比べます。

## ソースコード

このデモ画面のソースコードは GitHub で公開しています。アプリでは、各欄の下にある「Show Code」リンクでその欄の XAML を表示できます。

[GitHub で Label のソースコードを見る →](https://github.com/s-iguchi09/WPFStandardControlDemoApp/tree/main/src/WPFStandardControlDemoApp/Features/LabelUsage){: target="_blank" rel="noopener noreferrer"}
