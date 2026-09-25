---
layout: control-demo
permalink: /ja/apps/wpf-standard-control-demo/togglebutton.html
title: "ToggleButton"
badge: "Inputs"
lead: "ToggleButton は、クリックすると押された状態のまま残り、次のクリックで戻るボタンです。CheckBox と RadioButton はこれを継承しています。"
description: "WPF の ToggleButton の IsThreeState、テンプレート、ClickMode、Command、Popup の連動を .NET 10 で実測して解説。null が未チェックと同じ見た目になることや、UI オートメーションではコマンドが動かないことも示します。"
---

## 概要

**ToggleButton** は `ButtonBase` を継承しており、`CheckBox` と `RadioButton` は ToggleButton を継承しています。状態は `bool?` 型の `IsChecked` で表し、既定で TwoWay にバインドされます。ToggleButton には `GroupName` がないので、複数の中から 1 つを選ばせる場合は RadioButton を使います。

既定のテンプレートには表示状態のグループ（VisualStateGroup）がなく、見た目を変えるトリガーは `IsChecked` = `true` に対するものだけでした。そのため、`IsChecked` が `null` のボタンは、チェックされていないボタンと同じ見た目になります。未確定の状態を見せたい場合は、テンプレートで表現する必要があります。一方、UI オートメーションは 3 つの状態を区別して報告し、`ToggleState` は `false`・`true`・`null` に対して `Off`・`On`・`Indeterminate` でした。

デモアプリには `IsChecked` と `IsThreeState` の 2 つの欄があり、どちらもボタンの横に値を表示しています。各欄の下の「Show Code」リンクで、その欄の XAML を表示できます。

## 画面キャプチャ

![togglebutton demo screen](/images/wpf-standard-control-demo/togglebutton.png){: .screenshot-img}

## デモしているプロパティ

以下のプロパティが WPF 標準コントロールデモアプリでインタラクティブにデモされています。各プロパティの挙動は .NET 10 で実測したものです（[実測した挙動](#measured-behavior)を参照）。

| プロパティ | 設定値 | 説明 |
| --- | --- | --- |
| `IsChecked` | `bool? (Null / False / True)` | ボタンが押された状態かどうかです。デモアプリには、`null`・`false`・`true` で始まる 3 つのボタンがあります。どれも 3 状態ではないため、クリックごとに `false` と `true` が入れ替わります。`null` で始まるボタンは、最初のクリックで `false` になり、`null` には戻れません。 |
| `IsThreeState` | `bool` | クリックで未確定の状態を通るかどうかです。`True` ではクリックごとに `false` → `true` → `null` → `false` と変わり、既定値の `False` では `false` → `true` → `false` と変わりました。既定のテンプレートは `null` を `false` と同じように描くため、デモアプリの 3 状態のボタンは、どちらの状態でも押されていない見た目になります。違いは横の文字で分かります。 |

## ClickMode・Command・Popup

以下はデモアプリの ToggleButton の画面にはありませんが、トグルボタンでよく使うものです。同じ方法で挙動を測りました。

- **`ClickMode="Press"`** — マウスボタンを押した時点で切り替わり、離してもその状態のままでした。`IsChecked` はボタンを押した時点で `true` になり、離した後も `true` でした。押している間だけ有効なボタンにはなりません。
- **`Command`** — 状態が変わった後に実行されます。`CommandParameter` をボタン自身の `IsChecked` にバインドすると、`false` からのクリックで `Execute` に `True` が渡り、その時点で `IsChecked` はすでに `true` でした。支援技術と同じく UI オートメーションの `Toggle` で切り替えると、`IsChecked` は変わりましたが、コマンドは実行されませんでした。どの方法で切り替えられても処理したい場合は、状態をバインドしたプロパティで受け取ります。
- **`IsOpen` を `IsChecked` にバインドした `Popup`** — ボタンをクリックすると開きました。ポップアップが閉じると `IsChecked` も `false` に戻りました。`Popup.IsOpen` が既定で TwoWay にバインドされるためです。`StaysOpen="False"` で実際にクリックすると、ウィンドウの空いた領域のクリックではポップアップが閉じてボタンのチェックも外れました。ポップアップが開いている間にボタン自体をクリックしても閉じず、その後も `IsOpen` と `IsChecked` はどちらも `true` のままでした。

## XAML 使用例

デモアプリ（`ToggleButtonUsageControl.xaml`）の `IsThreeState` の欄から、スタイルと周囲のレイアウトを省き、名前空間の宣言を加えたものです。

```xml
<StackPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <ToggleButton x:Name="IsThreeStateTrueButton" HorizontalAlignment="Stretch"
                Content="ToggleButton" IsThreeState="True" />
  <TextBlock HorizontalAlignment="Center"
             Text="{Binding IsChecked, ElementName=IsThreeStateTrueButton, TargetNullValue=(Null)}" />

  <ToggleButton x:Name="IsThreeStateFalseButton" HorizontalAlignment="Stretch"
                Content="ToggleButton" IsThreeState="False" />
  <TextBlock HorizontalAlignment="Center"
             Text="{Binding IsChecked, ElementName=IsThreeStateFalseButton}" />
</StackPanel>
```

## 主な使用例

- **ツールバーのトグル** — 太字・斜体や描画モードなど、オフにするまで有効なままの機能に使います。
- **パネルやポップアップの表示** — `IsChecked` でパネルの表示やポップアップの `IsOpen` を切り替えます。
- **独自のスイッチ** — テンプレートを差し替えて、オン・オフのスイッチの形に描きます。

## ヒントとベストプラクティス

- **`IsThreeState` を使うなら `null` の見た目をテンプレートに用意する** — 既定のテンプレートにはありません。
- **新しい状態はバインドで渡す** — `CommandParameter="{Binding IsChecked, RelativeSource={RelativeSource Self}}"` で、クリック後の状態が渡ります。
- **`Command` だけに頼らない** — UI オートメーションで切り替えると、コマンドは実行されずに `IsChecked` だけが変わります。
- **排他的なトグルには RadioButton を使う** — ToggleButton には `GroupName` がありません。

## 実測した挙動 {#measured-behavior}

このページの挙動の記述は、すべて .NET 10 / Windows 11 で実際に動かして確かめたものです。計測には、このサイトのスクリーンショット生成ツールの [`ToggleButtonDemoScene`](https://github.com/s-iguchi09/s-iguchi09.github.io/blob/main/tools/screenshot-capture/Scenes/ToggleButtonDemoScene.cs){: target="_blank" rel="noopener noreferrer"} を使いました。状態の切り替えは UI オートメーションの `Toggle` で行い、コマンドはボタンのクリック処理（`OnClick`）を呼んで実行し、`ClickMode` はマウスの左ボタンのイベントを発生させて確かめました。

<figure class="article-figure article-figure--wide">
  <img src="/images/wpf-standard-control-demo/verification/togglebutton/togglebutton-behavior.svg" alt="ToggleButton の計測結果の表。ButtonBase を継承し CheckBox と RadioButton の基底であり、IsThreeState のときだけ false・true・null と巡回し、既定のテンプレートには表示状態がなく IsChecked true のトリガーだけがあり、UI オートメーションは Off・On・Indeterminate を報告し、ClickMode Press は押した時点で切り替わってそのまま残り、IsChecked にバインドした CommandParameter には新しい値が渡り、UI オートメーションの Toggle ではコマンドが実行されず、バインドした Popup が閉じると IsChecked も戻り、空いた領域の実際のクリックでは閉じるが、開いている間のボタンのクリックでは開いたまま" width="1187" height="530" loading="lazy">
  <figcaption>状態、テンプレート、<code>ClickMode</code>、<code>Command</code>、バインドした <code>Popup</code>。.NET 10 / Windows 11 で計測。</figcaption>
</figure>

## 関連コントロール

- [CheckBox](/ja/apps/wpf-standard-control-demo/checkbox.html) — チェックボックスの形に描かれた ToggleButton です。
- [RadioButton](/ja/apps/wpf-standard-control-demo/radiobutton.html) — 複数の中から 1 つを選ぶ ToggleButton です。
- [Popup](/ja/apps/wpf-standard-control-demo/popup.html) — ToggleButton で開閉することの多いポップアップです。

## ソースコード

このデモ画面のソースコードは GitHub で公開しています。アプリでは、各欄の下にある「Show Code」リンクでその欄の XAML を表示できます。

[GitHub で ToggleButton のソースコードを見る →](https://github.com/s-iguchi09/WPFStandardControlDemoApp/tree/main/src/WPFStandardControlDemoApp/Features/ToggleButtonUsage){: target="_blank" rel="noopener noreferrer"}
