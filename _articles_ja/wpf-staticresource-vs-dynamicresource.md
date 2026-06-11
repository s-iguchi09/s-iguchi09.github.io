---
layout: article-ja
title: "WPF で StaticResource を変更しても画面が更新されない原因と解決方法"
date: 2026-06-11
category: WPF
excerpt: "StaticResource はXAMLロード時に値を確定するため、実行時の変更は反映されない。動的な変更が必要な場合は DynamicResource を使用する。両者の仕組みと使い分けの判断基準を解説する。"
---

## 概要

WPF のリソース参照には `StaticResource` と `DynamicResource` の2種類がある。
`StaticResource` で定義したリソースをコードから変更しても画面が更新されない場合、その原因はリソースを評価するタイミングの違いにある。
本記事では、両者の内部動作の違いを解説し、用途に応じた選択基準を整理する。

---

## 前提・対象環境

- フレームワーク: .NET 6 以降 / WPF
- 言語: C#
- アーキテクチャ: MVVM・コードビハインドのいずれにも適用可能

---

## 問題

アプリ実行中に `ResourceDictionary` のエントリを変更したにもかかわらず、コントロールの外観が変わらないケースがある。

たとえば、以下のように `Window.Resources` に定義した `SolidColorBrush` をコードから差し替えても、ボタンの背景色は変化しない。

```xml
<Window.Resources>
    <SolidColorBrush x:Key="ThemeColor" Color="SkyBlue" />
</Window.Resources>

<Button Background="{StaticResource ThemeColor}" Content="ボタン" />
```

```csharp
// 実行中に色を変更しようとする
Resources["ThemeColor"] = new SolidColorBrush(Colors.OrangeRed);
```

上記のコードを実行しても、ボタンの背景色は `SkyBlue` のまま変わらない。

---

## 原因・背景

`StaticResource` と `DynamicResource` の決定的な違いは、**リソースを検索・確定するタイミング**にある。

### StaticResource の動作

`StaticResource` は XAML が解析（ロード）される瞬間に一度だけリソースを検索し、見つかった値をコントロールのプロパティに直接セットする。
値のセット後は参照関係が存在しないため、リソースの内容を変更しても WPF はそれを検知せず、プロパティは変化しない。

また、XAML は上から順番に解析されるため、`StaticResource` で参照するリソースは参照元より**前**の行で定義されている必要がある。
定義順が守られていない場合、`XamlParseException` が発生する。

### DynamicResource の動作

`DynamicResource` は、画面ロード時にプロパティとリソースキーの紐付け（ディクショナリキーの保持）だけを行う。
アプリ実行中にそのキーに対応するリソースが変更されると、WPF はそれを検知し、対象プロパティを自動的に再評価して画面を更新する。

| 比較項目 | StaticResource | DynamicResource |
| --- | --- | --- |
| 評価タイミング | XAML ロード時（一度のみ） | ロード時 ＋ 変更検知のたびに再評価 |
| 実行時の変更 | 反映されない | 即座に反映される |
| リソースの定義順 | 参照元より前に定義が必要 | 前後どちらでも可 |
| パフォーマンス | 高速 | 監視オーバーヘッドあり |

---

## 解決方法

実行時にリソースの変更を画面に反映させるには、`StaticResource` を `DynamicResource` に置き換える。

---

## 実装例

### DynamicResource による動的テーマ切り替え

以下の例では、ボタンのクリック時にリソースディクショナリのブラシを差し替え、画面全体の配色を切り替える。

```xml
<Window.Resources>
    <SolidColorBrush x:Key="ThemeColor" Color="SkyBlue" />
</Window.Resources>

<StackPanel>
    <Button Background="{DynamicResource ThemeColor}" Content="対象ボタン" />
    <Button Content="テーマ切り替え" Click="OnThemeToggleClick" />
</StackPanel>
```

```csharp
private bool _isDark = false;

private void OnThemeToggleClick(object sender, RoutedEventArgs e)
{
    _isDark = !_isDark;
    Resources["ThemeColor"] = _isDark
        ? new SolidColorBrush(Colors.DarkSlateGray)
        : new SolidColorBrush(Colors.SkyBlue);
}
```

`DynamicResource` を使用することで、`Resources["ThemeColor"]` への代入が即座に `Background` プロパティへ反映される。

### ResourceDictionary の丸ごと差し替え

アプリ全体のテーマをライト／ダーク間で切り替える場合は、テーマ定義ファイルを分けておき、`MergedDictionaries` を差し替える方法が一般的である。

テーマファイルの例（`Themes/Light.xaml`）：

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
    <SolidColorBrush x:Key="Background" Color="White" />
    <SolidColorBrush x:Key="Foreground" Color="Black" />
</ResourceDictionary>
```

テーマファイルの例（`Themes/Dark.xaml`）：

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
    <SolidColorBrush x:Key="Background" Color="#1E1E1E" />
    <SolidColorBrush x:Key="Foreground" Color="White" />
</ResourceDictionary>
```

差し替えコード：

```csharp
private void SwitchTheme(string themeName)
{
    var uri = new Uri($"Themes/{themeName}.xaml", UriKind.Relative);
    var dict = new ResourceDictionary { Source = uri };

    Application.Current.Resources.MergedDictionaries.Clear();
    Application.Current.Resources.MergedDictionaries.Add(dict);
}
```

テーマ対象のすべてのリソースを `DynamicResource` で参照しておくことで、上記の差し替え操作だけで画面全体の外観が切り替わる。

---

## 注意点

- **`Freeze` の影響は既存 `Freezable` インスタンスを変更するときに限られる:** たとえば `((SolidColorBrush)Resources["ThemeColor"]).Color = ...` のように既存の `SolidColorBrush` を直接変更する場合、`Freeze()` 済みなら変更できない。一方、今回のように `Resources["ThemeColor"]` に新しい `SolidColorBrush` を代入して差し替えるだけなら、通常は `IsFrozen` の確認は不要である。
- **`DynamicResource` の乱用はパフォーマンスに影響する:** WPF は変更を監視するための内部リスナーを保持するため、すべてのリソース参照を `DynamicResource` にすると描画速度やメモリ使用量に影響が出る場合がある。変更が不要なリソースには `StaticResource` を維持する。
- **`DynamicResource` は一部のプロパティでは使用できない:** `Setter.Value` 以外の一部の構文（`ControlTemplate` 内のトリガーなど）では `DynamicResource` が制限される場合がある。コンパイル時ではなく実行時にエラーが発生することがあるため、事前に動作確認が必要である。

---

## 代替案・比較

| 方法 | メリット | デメリット | 適するケース |
| --- | --- | --- | --- |
| `DynamicResource` への切り替え | 実装がシンプル・変更が即反映 | 監視コストがある | テーマ切り替えやシステムカラー連動 |
| `MergedDictionaries` の差し替え | テーマを一括管理できる | ファイル分割の設計が必要 | ライト／ダークモードなどの全体テーマ切り替え |
| `INotifyPropertyChanged` ＋ バインディング | ViewModel の値変更で UI が更新される | リソースディクショナリを使わない設計になる | 単一コントロールの動的スタイルではなく、データ駆動の表示切り替え |

---

## まとめ

`StaticResource` は XAML ロード時に値を確定するため、実行後のリソース変更は画面に反映されない。
実行中に変更を反映させる必要がある場合は `DynamicResource` を使用する。

選択の基準は次のとおりである。

- **変更が不要なリソース（固定カラー、固定フォントサイズなど）:** `StaticResource` を使用する。パフォーマンスへの影響が少なく、定義順のルールさえ守れば問題は発生しない。
- **実行中に変更が必要なリソース（テーマカラー、OS 設定連動など）:** `DynamicResource` を使用する。`SystemColors` や `SystemParameters` などの OS 設定と連動させる場合も `DynamicResource` が必要となる。

原則として `StaticResource` を基本とし、動的な変更が求められる箇所にのみ `DynamicResource` を適用する設計が、保守性とパフォーマンスのバランスとして適切である。
