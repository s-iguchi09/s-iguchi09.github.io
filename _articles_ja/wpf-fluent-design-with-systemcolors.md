---
layout: article-ja
title: "WPF で Fluent デザインを追加ライブラリなしで適用する方法"
date: 2026-05-30
category: WPF
excerpt: "WPF 標準機能だけで Fluent デザインの見た目を整え、SystemColors を使って Windows の色設定に追従する実装方法を整理します。"
---

## 概要

本記事では、WPF アプリに Fluent デザインの要素を取り入れる方法を扱う。  
対象は「追加ライブラリを導入しない」構成であり、WPF 標準のテンプレート、余白設計、角丸、階層表現、`SystemColors` の活用で一貫した外観を構築する。

---

## 前提・対象環境

- フレームワーク／言語: .NET 9 / C# 13
- 対象 UI: WPF Window / UserControl / Button / TextBlock
- アーキテクチャ: MVVM またはコードビハインド（本稿の XAML はどちらでも適用可能）
- 方針: 外部 UI ライブラリ（MahApps.Metro、ModernWpf など）を追加しない

---

## 問題

既定の WPF テーマは長期運用で安定している一方、余白、配色、角丸、情報階層の表現が現行の Windows UI と乖離しやすい。  
特に複数画面を持つ業務アプリでは、コントロールを既定スタイルのまま配置すると視覚的な密度が高くなり、操作対象の優先度が判別しにくくなる。

---

## 原因・背景

WPF は柔軟な描画基盤を持つが、Fluent 固有の外観は標準で自動適用されない。したがって、Fluent 的な印象は次の要素を明示的に定義して初めて成立する。

- 角丸と余白によるレイアウトの緩和
- 背景と枠線のコントラストによる階層分離
- アクセント色の限定利用
- OS 設定と整合する色の参照

このとき `SystemColors` を使うと、Windows 側の色設定に依存した色を参照できるため、テーマ変更時の不整合を減らせる。

---

## 解決方法

外部ライブラリを使わず、以下の 3 点を組み合わせる。

- `App.xaml` に Fluent テーマのリソースディクショナリ、または `ThemeMode` を設定し、アプリ全体へテーマを適用する。
- `DynamicResource` と `SystemColors.*BrushKey` を使い、OS 依存の色を参照する。
- コントロールテンプレートで角丸・余白・ホバー時の視覚フィードバックを定義する。
- 画面全体の背景、カード面、アクセントの役割を分離し、情報の階層を明確化する。

特に .NET 9 の Fluent テーマをアプリ全体に反映する場合、`App.xaml` の設定が実質的な必須手順となる。Window 単位の設定だけでは、画面ごとにテーマ適用が分散し、運用時の整合性が崩れやすい。

この構成により、WPF でも手軽に Fluent の設計思想に近い UI を実現できる。

---

## 実装例

### 1. App.xaml で Fluent テーマを適用する

アプリ全体で Fluent テーマを有効化するには、`App.xaml` にテーマ設定を記述する。  
`.NET 9` では `ThemeMode` を使う方法と、Fluent リソースディクショナリをマージする方法のどちらかで適用できる。

`ThemeMode` を使う場合は次のように記述する。

```xml
<Application x:Class="Sample.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             StartupUri="MainWindow.xaml"
             ThemeMode="System">
  <Application.Resources>
    <ResourceDictionary />
  </Application.Resources>
</Application>
```

Fluent リソースディクショナリを使う場合は次のように記述する。

```xml
<Application x:Class="Sample.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             StartupUri="MainWindow.xaml">
  <Application.Resources>
    <ResourceDictionary>
      <ResourceDictionary.MergedDictionaries>
        <ResourceDictionary Source="pack://application:,,,/PresentationFramework.Fluent;component/Themes/Fluent.xaml" />
      </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
  </Application.Resources>
</Application>
```

どちらか一方を先に入れることで、各 Window 側ではコントロールのローカル調整に集中できる。`App.xaml` の設定がない場合、Fluent テーマの適用範囲が局所化し、画面間で見た目が揃わない。

### 2. Window 側で SystemColors を使って配色と操作感を整える

次に、画面全体とカード領域、ボタンのスタイルを定義する。`SystemColors` を `DynamicResource` で参照すると、Windows の色設定変更に追従可能となる。

```xml
<Window x:Class="Sample.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Fluent Without External Libraries"
        Width="800" Height="480"
        Background="{DynamicResource {x:Static SystemColors.WindowBrushKey}}">

  <Window.Resources>
    <SolidColorBrush x:Key="CardBackgroundBrush"
                     Color="{Binding Source={x:Static SystemColors.ControlLightColor}}" />

    <Style x:Key="CardBorderStyle" TargetType="Border">
      <Setter Property="Padding" Value="24" />
      <Setter Property="CornerRadius" Value="12" />
      <Setter Property="BorderThickness" Value="1" />
      <Setter Property="Background" Value="{StaticResource CardBackgroundBrush}" />
      <Setter Property="BorderBrush"
              Value="{DynamicResource {x:Static SystemColors.ActiveBorderBrushKey}}" />
    </Style>

    <Style x:Key="FluentLikeButtonStyle" TargetType="Button">
      <Setter Property="Padding" Value="14,8" />
      <Setter Property="Margin" Value="0,12,0,0" />
      <Setter Property="Foreground"
              Value="{DynamicResource {x:Static SystemColors.ControlTextBrushKey}}" />
      <Setter Property="Background"
              Value="{DynamicResource {x:Static SystemColors.ControlBrushKey}}" />
      <Setter Property="BorderBrush"
              Value="{DynamicResource {x:Static SystemColors.ActiveBorderBrushKey}}" />
      <Setter Property="BorderThickness" Value="1" />
      <Setter Property="Template">
        <Setter.Value>
          <ControlTemplate TargetType="Button">
            <Border x:Name="Root"
                    Background="{TemplateBinding Background}"
                    BorderBrush="{TemplateBinding BorderBrush}"
                    BorderThickness="{TemplateBinding BorderThickness}"
                    CornerRadius="8">
              <ContentPresenter HorizontalAlignment="Center"
                                VerticalAlignment="Center" />
            </Border>
            <ControlTemplate.Triggers>
              <Trigger Property="IsMouseOver" Value="True">
                <Setter TargetName="Root" Property="Opacity" Value="0.92" />
              </Trigger>
              <Trigger Property="IsPressed" Value="True">
                <Setter TargetName="Root" Property="Opacity" Value="0.82" />
              </Trigger>
              <Trigger Property="IsEnabled" Value="False">
                <Setter TargetName="Root" Property="Opacity" Value="0.55" />
              </Trigger>
            </ControlTemplate.Triggers>
          </ControlTemplate>
        </Setter.Value>
      </Setter>
    </Style>
  </Window.Resources>

  <Grid Margin="32">
    <Border Style="{StaticResource CardBorderStyle}">
      <StackPanel>
        <TextBlock FontSize="24"
                   FontWeight="SemiBold"
                   Foreground="{DynamicResource {x:Static SystemColors.ControlTextBrushKey}}"
                   Text="WPF Fluent Style" />

        <TextBlock Margin="0,10,0,0"
                   TextWrapping="Wrap"
                   Foreground="{DynamicResource {x:Static SystemColors.GrayTextBrushKey}}"
                   Text="SystemColors を参照することで、Windows の色設定に依存した色を利用できる。" />

        <Button Style="{StaticResource FluentLikeButtonStyle}"
                Content="操作を実行" />
      </StackPanel>
    </Border>
  </Grid>
</Window>
```

上記の実装は、WPF 標準機能のみで視覚階層と操作フィードバックを整える構成である。`DynamicResource` を利用しているため、OS の色設定が変わった際にブラシ参照の再評価が行われ、固定色中心の実装より追従性が高くなる。`SystemColors.AccentColorBrushKey` 系を利用すれば、Windows のアクセントカラーにも追従できる。

---

## 注意点

- この方法は Fluent の「設計思想」を実装するものであり、WinUI の Mica や Acrylic と同等のマテリアル表現を完全再現するものではない。
- .NET 9 の Fluent テーマをアプリ全体へ適用する場合は、`App.xaml` で `ThemeMode` または Fluent リソースディクショナリのどちらかを設定する。Window ごとの個別設定のみで運用すると、画面追加時にテーマ漏れが発生しやすい。
- `StaticResource` で `SystemColors` を参照すると、実行中の色変更追従が限定されるため、テーマ追従が必要な箇所は `DynamicResource` を優先する。
- 複数画面で統一する場合は、スタイルを `App.xaml` または共通 `ResourceDictionary` に集約して重複定義を避ける。

---

## 代替案・比較

| 方法 | メリット | デメリット | 適するケース |
|---|---|---|---|
| WPF 標準スタイル + SystemColors | 追加ライブラリ不要、依存が増えない、OS 色設定に追従しやすい | Fluent の高度な素材表現は限定的 | 長期保守重視、既存 WPF 資産を維持する場合 |
| 外部 Fluent 系ライブラリ導入 | 既製テーマで見た目を短時間に統一しやすい | 依存関係の更新コスト、テーマ差分検証が必要 | 新規開発で UI 優先度が高い場合 |
| 完全カスタム描画 | 表現自由度が最も高い | 実装・検証コストが高い | ブランド要件が強く、専用 UI が必要な場合 |

---

## まとめ

WPF で Fluent デザインを適用する実装は、追加ライブラリなしでも成立する。要点は、`App.xaml` で Fluent テーマをアプリ全体に適用したうえで、角丸と階層表現を定義し、`SystemColors` の参照で OS 設定へ追従させることである。保守性を重視する場合は WPF 標準スタイル + `SystemColors` が適し、視覚効果の優先度が高い場合のみ外部ライブラリ導入を検討するのが妥当である。

---

<!-- 関連記事 -->
<!-- - [WPF DatePicker の表示形式をカスタマイズする方法](/articles/wpf-datepicker-custom-format) -->
<!-- - [英語版: WPF Fluent Design Without Extra Libraries](/en/articles/wpf-fluent-design-with-systemcolors) -->
