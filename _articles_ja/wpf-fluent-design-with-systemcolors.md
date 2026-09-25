---
layout: article-ja
title: "WPF で Fluent デザインを追加ライブラリなしで適用する方法"
date: 2026-05-30
category: WPF
excerpt: "追加ライブラリなしで WPF に Fluent の外観を適用する方法。App.xaml でのテーマの設定、ライトとダークに追従する Fluent テーマのブラシ、SystemColors がダークモードに追従しないことを、実測した図と表で示す。"
image: /images/articles/wpf-fluent-design-with-systemcolors/fluent-systemcolors-card.png
---

## 概要

本記事では、WPF アプリに Fluent デザインの要素を取り入れる方法を扱う。  
対象は「追加ライブラリを導入しない」構成であり、WPF 標準のテンプレート、余白設計、角丸、階層表現と、Fluent テーマ自身のブラシで、ライトとダークに追従する一貫した外観を構築する。`SystemColors` が何に追従し、何に追従しないかも示す。  

---

## 前提・対象環境

- フレームワーク／言語: .NET 9 / C# 13
- 対象 UI: WPF Window / UserControl / Button / TextBlock
- アーキテクチャ: MVVM またはコードビハインド（本稿の XAML はどちらでも適用可能）
- 方針: 外部 UI ライブラリ（MahApps.Metro、ModernWpf など）を追加しない
- 検証環境: .NET 10 / Windows 11

本記事の実測は、上記の環境で、Windows をダークモードにした PC で行った。`systemcolors-values.svg` は各 `SystemColors` キーが返す色とその相対輝度を、`systemcolors-tracking.svg` はアプリケーションリソースを差し替えた前後の参照値を、`theme-brush-values.svg` は `ThemeMode` の `Light` と `Dark` でのブラシのキーの値を読み出している。画面の図は、記事の XAML を表示したものである。
この環境で確認しているのは次の点である。

- 選択項目の `HighlightColor` と、個人用設定のアクセント色 `AccentColor` は別の値である。
- 色を直接読んで焼き込んだ場合、後からの差し替えには追随しない。
- リソースキーを動的に参照した場合（記事の XAML では `DynamicResource`）は、差し替えに追随する。
- `SystemColors` は、Windows のダークモードでも、`ThemeMode` の `Light` と `Dark` の切り替えでも変わらなかった。Fluent テーマのブラシのキーは変わった。

---

## 問題

既定の WPF テーマは長期運用で安定している一方、余白、配色、角丸、情報階層の表現が現行の Windows UI と乖離しやすい。  
特に複数画面を持つ業務アプリでは、コントロールを既定スタイルのまま配置すると視覚的な密度が高くなり、操作対象の優先度が判別しにくくなる。  

<figure class="article-figure">
  <img src="/images/articles/wpf-fluent-design-with-systemcolors/fluent-default-theme.png" alt="既定テーマの WPF 画面。見出し、説明文、四角い枠のボタンが背景と同じ面の上に並んでいる。" width="426" height="293" loading="lazy">
  <figcaption>既定テーマ（Aero2）のまま配置した画面。カード面と背景が分離しておらず、ボタンの角も直角で、どこが主要な操作かが読み取りにくい。</figcaption>
</figure>

---

## 原因・背景

WPF は柔軟な描画基盤を持つが、Fluent 固有の外観は標準で自動適用されない。  
したがって、Fluent 的な印象は次の要素を明示的に定義して初めて成立する。  

- 角丸と余白によるレイアウトの緩和
- 背景と枠線のコントラストによる階層分離
- アクセント色の限定利用
- ライトとダークのテーマに追従する色の参照

標準で使える色は 2 系統ある。Fluent テーマ自身のブラシのキー（`ApplicationBackgroundBrush` など）は、テーマに合わせて切り替わる。`SystemColors` は固定色の代わりに Windows のシステムカラーを返すが、後述の実測のとおり、ダークモードには追従しない。

---

`SystemColors` が実際に返す色は、読み出して確かめられる。

<figure class="article-figure">
  <img src="/images/articles/wpf-fluent-design-with-systemcolors/systemcolors-values.svg" alt="SystemColors の各キーが返す色と相対輝度の表。WindowColor は白、WindowTextColor は黒、HighlightColor は青系、AccentColor は赤系で、いずれも OS の設定に対応した値になっている。1 行目は、計測した PC が Windows のダークモードだったことを示す。" width="603" height="320" loading="lazy">
  <figcaption>.NET 10 / Windows 11で、Windows をダークモードにした PC（表の 1 行目）で <code>SystemColors</code> の各キーを読み出した結果。<code>relative luminance</code> は WCAG の相対輝度で、前景と背景のコントラストを見積もるために併記している。</figcaption>
</figure>

この PC はダークモードだが、`WindowColor` と `WindowTextColor` の輝度は `1.00` と `0.00` で、ライトの値のままである。`SystemColors` は Windows のダークモードに追従しなかった。

`HighlightColor` は選択項目のハイライト色である。ユーザーが個人用設定で選ぶアクセント色は別のキーの `AccentColor` で、両者は混同されやすい。
撮影した環境ではアクセント色を赤にしてあるため、表でも `HighlightColor` が `#FF0078D7`、`AccentColor` が `#FFE2241A` と別の値になっている。アクセントを固定色で書くと、この設定と食い違う。

**ただし、これらのキーを読み取るだけでは、後からの差し替えに追随しない。**
`SystemColors.WindowColor` のように色を直接読み取ると、読み取った時点の値がそのまま焼き込まれる。

<figure class="article-figure">
  <img src="/images/articles/wpf-fluent-design-with-systemcolors/systemcolors-tracking.svg" alt="色の参照方法ごとに、システムのブラシを差し替える前後の値を測った表。SystemColors.WindowColor から作ったブラシは差し替え後も白のまま。DynamicResource で SystemColors.WindowBrushKey を参照した側だけが新しい色に変わる。" width="697" height="140" loading="lazy">
  <figcaption>アプリケーションのリソースにある <code>SystemColors.WindowBrushKey</code> を差し替え、その前後で両者の値を読み取った結果。測っているのはこの差し替えへの追随であり、OS のテーマ切り替えそのものは測っていない。</figcaption>
</figure>

直接読み取った側は差し替え後も値が変わらず、リソースキーを `DynamicResource` で参照した側だけが新しい色になっている。
**差し替えに追随させるには、色ではなく `SystemColors.WindowBrushKey` のようなリソースキーを `DynamicResource` で参照する必要がある。**

ただし、`DynamicResource` でも、値そのものが変わらなければ追従のしようがない。`ThemeMode` を `Light` と `Dark` で切り替えると、測った Fluent のブラシのキー 5 つはすべて変わったが、測った `SystemColors` のキー 3 つ（`WindowBrushKey`・`ControlTextBrushKey`・`AccentColorBrushKey`）はどれも変わらなかった。

<figure class="article-figure">
  <img src="/images/articles/wpf-fluent-design-with-systemcolors/theme-brush-values.svg" alt="ThemeMode の Light と Dark でのブラシのキーの値の表。Fluent の ApplicationBackgroundBrush・CardBackgroundFillColorDefaultBrush・TextFillColorPrimaryBrush・TextFillColorSecondaryBrush・AccentFillColorDefaultBrush はすべて変わり、たとえば背景は FAFAFA から 202020 になる。SystemColors.WindowBrushKey は白、ControlTextBrushKey は黒、AccentColorBrushKey は同じ赤のままだった。" width="610" height="320" loading="lazy">
  <figcaption>.NET 10 / Windows 11 で、それぞれの <code>ThemeMode</code> のウィンドウを表示し、そのウィンドウから各キーを引いた結果。</figcaption>
</figure>

そのため、テーマに追従させたい背景・面・文字の色は、Fluent のブラシのキーから取る必要がある。`SystemColors` は Windows 自身から来る色に向く。たとえば `SystemColors.AccentColorBrushKey` は、どちらのテーマでも個人用設定のアクセント色を返した。

---

## 解決方法

外部ライブラリを使わず、以下の 4 点を組み合わせる。  

- `App.xaml` に Fluent テーマのリソースディクショナリ、または `ThemeMode` を設定し、アプリ全体へテーマを適用する。
- `DynamicResource` で Fluent テーマのブラシのキー（`ApplicationBackgroundBrush`・`TextFillColorPrimaryBrush` など）を参照し、ライトとダークのテーマに追従させる。
- コントロールテンプレートで角丸・余白・ホバー時の視覚フィードバックを定義する。
- 画面全体の背景、カード面、アクセントの役割を分離し、情報の階層を明確化する。

特に .NET 9 の Fluent テーマをアプリ全体に反映する場合、`App.xaml` の設定が実質的な必須手順となる。  
Window 単位の設定だけでは、画面ごとにテーマ適用が分散し、運用時の整合性が崩れやすい。  
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

ただし、2 つの方法は同等ではない。[`Application.ThemeMode` プロパティのリファレンス](https://learn.microsoft.com/dotnet/api/system.windows.application.thememode)によれば、`ThemeMode` はウィンドウの背景とダークモードも制御する。また、`ThemeMode` を設定したうえで Fluent のディクショナリを手動でもマージすると、手動のほうが優先されるため、併用は勧められていない。
なお、同じリファレンスによれば `ThemeMode` は .NET 10 でも実験的な API（`Experimental("WPF0001")`）であり、将来のバージョンで削除される可能性がある。コードから使うには診断 WPF0001 を抑える必要がある。長く保守するアプリケーションでは、この点を踏まえて選ぶ。次の手順の例は `ThemeMode` を使う。  
どちらか一方を先に入れることで、各 Window 側ではコントロールのローカル調整に集中できる。  
`App.xaml` の設定がない場合、Fluent テーマの適用範囲が局所化し、画面間で見た目が揃わない。  

### 2. Window 側で Fluent テーマのブラシを使って配色と操作感を整える

次に、画面全体とカード領域、ボタンのスタイルを定義する。
次の例は、背景・カード・文字の色を Fluent テーマ自身のブラシのキーから、ボタンの色をアクセントのキーから、いずれも `DynamicResource` で参照する。そのため、ライトとダークのテーマに合わせて切り替わる。

```xml
<Window x:Class="Sample.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Fluent Without External Libraries"
        Width="440" Height="300"
        Background="{DynamicResource ApplicationBackgroundBrush}">

  <Window.Resources>
    <Style x:Key="CardBorderStyle" TargetType="Border">
      <Setter Property="Padding" Value="24" />
      <Setter Property="CornerRadius" Value="12" />
      <Setter Property="BorderThickness" Value="1" />
      <Setter Property="Background" Value="{DynamicResource CardBackgroundFillColorDefaultBrush}" />
      <Setter Property="BorderBrush" Value="{DynamicResource CardStrokeColorDefaultBrush}" />
    </Style>

    <Style x:Key="AccentButtonStyle" TargetType="Button">
      <Setter Property="Padding" Value="14,8" />
      <Setter Property="Margin" Value="0,12,0,0" />
      <Setter Property="HorizontalAlignment" Value="Left" />
      <Setter Property="Foreground" Value="{DynamicResource TextOnAccentFillColorPrimaryBrush}" />
      <Setter Property="Background" Value="{DynamicResource AccentFillColorDefaultBrush}" />
      <Setter Property="Template">
        <Setter.Value>
          <ControlTemplate TargetType="Button">
            <Border x:Name="Root"
                    Background="{TemplateBinding Background}"
                    CornerRadius="8"
                    Padding="{TemplateBinding Padding}">
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
                   Foreground="{DynamicResource TextFillColorPrimaryBrush}"
                   Text="WPF Fluent Style" />

        <TextBlock Margin="0,10,0,0"
                   TextWrapping="Wrap"
                   Foreground="{DynamicResource TextFillColorSecondaryBrush}"
                   Text="Fluent theme brushes follow the light and dark theme." />

        <Button Style="{StaticResource AccentButtonStyle}"
                Content="Run Action" />
      </StackPanel>
    </Border>
  </Grid>
</Window>
```

上記の実装は、WPF 標準機能のみで視覚階層と操作フィードバックを整える構成である。
同じ XAML を `ThemeMode` の `Light` と `Dark` で表示すると、次のようになる。

<figure class="article-figure">
  <img src="/images/articles/wpf-fluent-design-with-systemcolors/fluent-systemcolors-card.png" alt="上の XAML を ThemeMode の Light で表示した画面。明るい灰色の背景に白い角丸のカードがあり、濃い色の見出し、灰色の説明文、赤い角丸のアクセントボタンが並ぶ。" width="426" height="293" loading="lazy">
  <figcaption>上の XAML を <code>ThemeMode="Light"</code> で表示した結果。追加ライブラリは使っていない。「問題」の図と比べると、カード面が背景から分離し、角丸・余白・アクセントのボタンで階層が作られている。</figcaption>
</figure>

<figure class="article-figure">
  <img src="/images/articles/wpf-fluent-design-with-systemcolors/fluent-card-dark.png" alt="同じ画面を ThemeMode の Dark で表示した結果。暗い背景に少し明るいカードがあり、白い見出し、明るい灰色の説明文、暗い文字のサーモン色のアクセントボタンが並ぶ。" width="426" height="293" loading="lazy">
  <figcaption>同じ XAML を <code>ThemeMode="Dark"</code> で表示した結果。すべての色がテーマのブラシのキーから来るため、画面全体が切り替わる。</figcaption>
</figure>

同じ画面を `SystemColors` で組むと、ダークのテーマでは成り立たない。背景・カード・文字を `SystemColors` のキーから取ると、Fluent のボタンのスタイルはダークに切り替わる一方、画面は白いまま残った。

<figure class="article-figure">
  <img src="/images/articles/wpf-fluent-design-with-systemcolors/systemcolors-under-dark.png" alt="背景・カード・文字に SystemColors のキーを使った画面を ThemeMode の Dark で表示した結果。画面とカードは白と明るい灰色、文字は黒のままで、ボタンだけが暗い灰色になり文字がほとんど読めない。" width="426" height="293" loading="lazy">
  <figcaption>背景・カード・文字を <code>SystemColors</code> のキーから <code>DynamicResource</code> で参照し、<code>ThemeMode="Dark"</code> で表示した結果。<code>SystemColors</code> の値は変わらないため、Fluent がスタイルを当てるコントロールだけが暗くなる。</figcaption>
</figure>

---

## 注意点

- この方法は Fluent の「設計思想」を実装するものであり、WinUI のマテリアル表現を完全に再現するものではない。リファレンスによれば `ThemeMode` はウィンドウの背景（Mica）も適用するが、Acrylic などはこの構成の対象外である。
- .NET 9 の Fluent テーマをアプリ全体へ適用する場合は、`App.xaml` で `ThemeMode` または Fluent リソースディクショナリのどちらかを設定する。Window ごとの個別設定のみで運用すると、画面追加時にテーマ漏れが発生しやすい。
- テーマのブラシは `DynamicResource` で参照する。`StaticResource` は一度だけ解決され、テーマの切り替えに追従しない。`SystemColors` はダークモードにも `ThemeMode` にも追従しないため、テーマに追従させたい面には使わない。
- 複数画面で統一する場合は、スタイルを `App.xaml` または共通 `ResourceDictionary` に集約して重複定義を避ける。

---

## 代替案・比較

| 方法                            | メリット                                                    | デメリット                                 | 適するケース                              |
| ------------------------------- | ----------------------------------------------------------- | ------------------------------------------ | ----------------------------------------- |
| WPF 標準スタイル + テーマのブラシ | 追加ライブラリ不要、依存が増えない、ライトとダークに追従する | Fluent の高度な素材表現は限定的            | 長期保守重視、既存 WPF 資産を維持する場合 |
| 外部 Fluent 系ライブラリ導入    | 既製テーマで見た目を短時間に統一しやすい                    | 依存関係の更新コスト、テーマ差分検証が必要 | 新規開発で UI 優先度が高い場合            |
| 完全カスタム描画                | 表現自由度が最も高い                                        | 実装・検証コストが高い                     | ブランド要件が強く、専用 UI が必要な場合  |

---

## まとめ

WPF で Fluent デザインを適用する実装は、追加ライブラリなしでも成立する。  
要点は、`App.xaml` で Fluent テーマをアプリ全体に適用したうえで、角丸と階層表現を定義し、Fluent テーマのブラシを `DynamicResource` で参照してライトとダークに追従させることである。`SystemColors` はダークモードに追従しない。  
保守性を重視する場合は WPF 標準スタイル + テーマのブラシが適し、視覚効果の優先度が高い場合のみ外部ライブラリ導入を検討するのが妥当である。  

---

## 関連記事

- [WPF Fluent テーマの TextBox でクリアボタンを非表示にする方法](/ja/articles/wpf-fluent-textbox-hide-clear-button/)
- [WPF Fluent テーマでカスタム Style を持つコントロールだけ旧外観に戻る問題](/ja/articles/wpf-fluent-theme-custom-style-not-applied/)
- [WPF の ThemeMode を実行時に切り替えても一部だけ Light のまま残る原因の切り分け](/ja/articles/wpf-fluent-thememode-runtime-switch/)
