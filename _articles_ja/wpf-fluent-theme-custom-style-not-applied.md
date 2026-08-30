---
layout: article-ja
title: "WPF Fluent テーマでカスタム Style を持つコントロールだけ旧外観に戻る問題"
date: 2026-08-27
category: WPF
excerpt: "Fluent テーマを適用しても、独自の Style を当てたコントロールだけが旧外観に戻る原因を解説する。BasedOn による解決と、それでも効かない配置パターンを実測の対応表で整理する。"
image: /images/articles/wpf-fluent-theme-custom-style-not-applied/implicit-style-shadows-fluent.png
---

## 概要

既存の WPF アプリへ Fluent テーマを導入すると、一部のコントロールだけが Fluent の外観にならず、従来どおりの角ばった外観のまま残ることがある。
この現象は、アプリ側が `Style` を当てていて、その `Style` が Fluent のスタイルへ `BasedOn` で連なっていないコントロールで発生する。
`<Style TargetType="Button">` のような**暗黙スタイル**は、書いた本人が意識しないままこの状態に陥る代表例である。
`Style="{x:Null}"` でスタイルの適用そのものを外した場合も、同じく旧外観のままである。
例外も警告も出ず、出力ウィンドウにも何も現れないため、テーマ適用の書き方が誤っていると誤解されやすい。

本記事では、この現象が起きる理由を Fluent テーマの供給方式から説明し、`BasedOn` による解決策を示す。
あわせて、`BasedOn` を書いたにもかかわらず解決されず旧外観のままになる配置パターンがあることを、実測した対応表とともに整理する。

---

## 前提・対象環境

- フレームワーク: .NET 9 / .NET 10 の WPF（`net9.0-windows` / `net10.0-windows`）
- OS: Windows 11（通常配色。ハイコントラストは対象外）
- テーマ適用方法: `ThemeMode` プロパティ、または `Fluent.xaml` リソースディクショナリの直接マージ
- 対象: `Application.Resources` / `Window.Resources` / 別ファイルのリソースディクショナリに定義したスタイル
- アーキテクチャ: MVVM・コードビハインドのいずれでも挙動は同じ

`ThemeMode` は `Application` と `Window` の双方に用意されており、アプリ全体にもウィンドウ単位にも設定できる。
本記事の対応表では、どちらに設定したかで結果が変わる組み合わせも扱う。

`Fluent.xaml` を直接マージする場合の pack URI は次のとおりである。

```text
pack://application:,,,/PresentationFramework.Fluent;component/Themes/Fluent.xaml
```

この URI を `ResourceDictionary` の `Source` に指定する。
一方 `ThemeMode` を使う場合、この記述は不要である。
実測では、`ThemeMode` を設定すると、明暗を確定した `Fluent.Light.xaml` または `Fluent.Dark.xaml` が自動でマージされた。

`ThemeMode` は実験的 API として公開されている。
本記事の実装例はすべて XAML の属性で設定するため抑制は不要だが、[.NET 9 の WPF における変更点](https://learn.microsoft.com/dotnet/desktop/wpf/whats-new/net90#thememode)が述べるとおり、コードから参照するとエラー `WPF0001` が発生する。
その場合はプロジェクトファイルで `<NoWarn>$(NoWarn);WPF0001</NoWarn>` を指定するか、`#pragma warning disable WPF0001` で抑制する。

---

## 問題

Fluent テーマの導入自体は属性 1 つで済む。
`App.xaml` の `Application` に `ThemeMode` を設定すれば、アプリ全体が Fluent の外観になる。
問題は、既存アプリの `App.xaml` に以前から暗黙スタイルが置かれている場合である。
次の `App.xaml` は、Fluent テーマを適用しつつ、`Button` の余白だけを従来どおり広げようとしたものである。

```xml
<Application x:Class="MyApp.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             StartupUri="MainWindow.xaml"
             ThemeMode="Light">
  <Application.Resources>
    <Style TargetType="Button">
      <Setter Property="Padding" Value="16,6" />
    </Style>
  </Application.Resources>
</Application>
```

`Padding` を足しただけであり、テンプレートも配色も触っていない。
それにもかかわらず、実行すると `Button` だけが Fluent の外観にならない。
同じウィンドウに置いた `CheckBox` は Fluent のまま表示されるため、テーマ自体は適用されていることが分かる。

<figure class="article-figure">
  <img src="/images/articles/wpf-fluent-theme-custom-style-not-applied/implicit-style-shadows-fluent.png" alt="Fluent テーマを適用した WPF ウィンドウ。Save ボタンは角が四角く背景が灰色の旧外観で、その下の Overwrite チェックボックスは角丸の Fluent 外観になっている。" width="286" height="183" loading="lazy">
  <figcaption>上の <code>App.xaml</code> と同じく、<code>{x:Type Button}</code> の暗黙スタイルを <code>Application.Resources</code> 直下へ置いた状態。Windows 11 / .NET 10 / <code>ThemeMode=Light</code> で撮影。<code>Button</code> は角が四角く背景が灰色であり、手を加えていない <code>CheckBox</code> は Fluent のままである。</figcaption>
</figure>

.NET 10 / `ThemeMode=Light` の環境でこの `Button` のテンプレートを実測すると、内部の `Border` は `CornerRadius` が `0`、背景が `#FFDDDDDD` である。
[.NET 9 の WPF における変更点](https://learn.microsoft.com/dotnet/desktop/wpf/whats-new/net90#thememode)は、`ThemeMode` の既定値 `None` では [Aero2](https://learn.microsoft.com/dotnet/desktop/wpf/controls/styles-templates-overview#available-built-in-themes) が使われると記述している。
上の実測値は通常配色の Windows 11 における Aero2 の `Button` と同じ値であり、Fluent のスタイルがまったく効いていないことを示す。

---

## 原因・背景

原因は、Fluent テーマがコントロールへ届く経路にある。

### Fluent はテーマスタイルではなく暗黙スタイルとして配られる

WPF の従来のテーマ（Aero2 など）は、コントロールの**テーマスタイル**として適用される。
[依存関係プロパティ値の優先順位](https://learn.microsoft.com/dotnet/desktop/wpf/properties/dependency-property-value-precedence)では、テーマスタイルはスタイル由来の値の中で最も弱く、アプリ側のスタイルが設定した値がその上に載る。
実測でも、`Padding` だけを設定した暗黙スタイルを当てたコントロールに、Aero2 のテンプレートが供給され続けることを確認した。
テーマスタイルは、[`OverridesDefaultStyle`](https://learn.microsoft.com/dotnet/api/system.windows.frameworkelement.overridesdefaultstyle) を `true` にしない限り、アプリ側が `Style` を設定してもテンプレートの供給をやめない。

Fluent テーマはこの経路を使わない。
[`Application.ThemeMode` プロパティ](https://learn.microsoft.com/dotnet/api/system.windows.application.thememode)の公式リファレンスは、このプロパティを設定すると Fluent のテーマディクショナリがアプリケーションリソースへ読み込まれる、と記述している。
スタイルの供給経路について言えば、Fluent はテーマスタイルではなく、**`ThemeMode` を設定した要素のリソースへマージされるリソースディクショナリ**として届く。
[`Window.ThemeMode` プロパティ](https://learn.microsoft.com/dotnet/api/system.windows.window.thememode)のリファレンスも、`Window` に設定した場合は Fluent のテーマディクショナリがそのウィンドウのリソースへ読み込まれる、と記述している。
このディクショナリにはブラシや数値などのリソースも多数含まれ、その一部として `{x:Type Button}` などをキーに持つ暗黙スタイルが入っている。

この違いは実測で確認できる。
`Style="{x:Null}"` を指定してスタイルの適用を明示的に外したコントロールは、Fluent テーマを適用していても Fluent の外観にならず、Aero2 の外観で描画される。
Fluent がテーマスタイルとして供給されているのであれば、`Style` を外しても Fluent の外観が残るはずである。

### 同じキーのスタイルが Fluent のスタイルを隠す

Fluent の暗黙スタイルのキーは、アプリ側が書く `<Style TargetType="Button">` のキーとまったく同じ `{x:Type Button}` である。
同じキーが 2 か所にあるとき、どちらが採用されるかはリソース探索の規則で決まる。
以下に挙げる 2 つの配置では、いずれもアプリ側のスタイルが採用される。

`Application.Resources` 直下に書いた場合は、ディクショナリと、そこへマージしたディクショナリ（以下「マージ辞書」）の優先順位で決まる。
[マージされたリソースディクショナリ](https://learn.microsoft.com/dotnet/desktop/wpf/systems/xaml-resources-merged-dictionaries)は、あるキーがマージ先のディクショナリとマージ辞書の両方にある場合、返されるのはマージ先のディクショナリのリソースである、と説明している。
`ThemeMode` は Fluent をマージ辞書として追加するため、`Application.Resources` 直下に置いたスタイルが優先される。

`Window.Resources` など、より内側のスコープに書いた場合はスコープの遠近で決まる。
[スタイルとテンプレート](https://learn.microsoft.com/dotnet/desktop/wpf/controls/styles-templates-overview#shared-resources-and-themes)は、要素のスタイルを探す際に要素ツリーをさかのぼり、次にアプリケーションのリソースを調べ、テーマは最後に参照されると説明している。
`Window.Resources` は `Application.Resources` より先に調べられるため、アプリ側のスタイルが採用される。

いずれの場合も、アプリ側のスタイルは Fluent のスタイルを**上書き**するのではなく、**丸ごと置き換えて隠す**。
隠された結果、Fluent のテンプレートは供給されなくなり、コントロールは WPF 本来のテーマスタイルである Aero2 のテンプレートへフォールバックする。
`Padding` を 1 つ足しただけのスタイルでも、Fluent のスタイル全体が失われるのはこのためである。

---

テンプレートがどちらから供給されているかは、テンプレート内の名前付きパーツで判別できる。
Fluent の `TextBox` テンプレートは `DeleteButton` を持ち、従来のテーマは持たない。

<figure class="article-figure">
  <img src="/images/articles/wpf-fluent-textbox-hide-clear-button/fluent-textbox-parts.svg" alt="テーマの届き方ごとに TextBox テンプレートの名前付きパーツを調べた表。ThemeMode を設定した行と Fluent.xaml を直接マージした行に DeleteButton が存在する。BasedOn を書かない暗黙スタイルを置くとどちらの経路でも DeleteButton が消え PART_ContentHost だけになるが、BasedOn で元のスタイルを引き継いだ行では DeleteButton が残る。" width="913" height="290" loading="lazy">
  <figcaption>.NET 10 / Windows 11 での実測結果。<code>Style applied</code> の列は、<code>Style</code> プロパティが埋まっているか（暗黙スタイル）、<code>null</code> のままか（従来のテーマスタイル）を示す。</figcaption>
</figure>

**2 行目の `Style applied` が `implicit style` になっている点が要点である。** `ThemeMode` を設定しただけで `Style` プロパティが埋まっており、Fluent がテーマスタイルではなく暗黙スタイルとして届いていることが分かる。
1 行目（`ThemeMode` なし）では `Style` が `null` のままで、従来のテーマスタイルからテンプレートが供給されている。

3 行目で、`BasedOn` を書かない暗黙スタイルをアプリ側が同じキーに置くと `DeleteButton` が消える。
`Padding` は 8 になっており、アプリ側のスタイルは効いている。**効いているからこそ Fluent のスタイルが置き換わり、テンプレートごと失われている。**

最終行が `BasedOn` で元のスタイルを引き継いだ場合である。`Padding` は同じく 8 に変わりながら、`DeleteButton` は残っている。
本記事の解決策が効くことが、この 1 行に出ている。

---

## 解決方法

スタイルに `BasedOn` を付け、Fluent の暗黙スタイルを継承させる。
`BasedOn="{StaticResource {x:Type Button}}"` と書けば、Fluent のスタイルを土台にしたうえで自前の `Setter` を追加できる。

ただし、この記述には注意すべき制約がある。
**`BasedOn` に指定したキーがそのスタイル自身のキーと同一で、かつ同名のキーがそのスタイルを宣言しているディクショナリのマージ辞書（入れ子のマージ辞書を含む）にあるとき、`BasedOn` は解決されず `null` のままである。**
このとき例外は発生せず、見た目は旧外観に戻ったままとなる。
これは .NET 9 と .NET 10 での実測から導いた条件であり、`StaticResource` の内部的な解決手順を説明するものではない。

`App.xaml` の `Application.Resources` 直下に暗黙スタイルを書いた場合がこの条件に当てはまる。
`ThemeMode` は Fluent のディクショナリを、そのスタイルを宣言している `Application.Resources` のマージ辞書として追加するためである。

これは**解決されない条件**であり、裏返して「この条件を外せば必ず Fluent へ解決される」とは言えない。
後述の対応表のとおり、条件を満たさないのに Fluent へ到達しない配置も存在する。

次の図は、スタイルを `Application.Resources` 直下へ置いた場合と、専用のリソースディクショナリファイル（以下 `Styles.xaml`）へ切り出した場合の違いを示す。

<figure class="article-figure article-figure--wide">
  <img src="/images/articles/wpf-fluent-theme-custom-style-not-applied/basedon-lookup-scope.svg" alt="左側は Application.Resources 直下にスタイルを置いた構成で、MergedDictionaries 内の Fluent.Light.xaml へ向かう経路が断たれ BasedOn が null になることを示す。右側は Styles.xaml を MergedDictionaries へ加えた構成で、その中のスタイルが同じ MergedDictionaries 内の Fluent.Light.xaml へ到達することを示す。" width="780" height="360" loading="lazy">
  <figcaption><code>BasedOn</code> の参照先と、スタイルを宣言しているディクショナリの関係。破線の枠が <code>MergedDictionaries</code>、白い枠がリソースディクショナリ、内側の薄い枠がスタイルを表す。左は自前のスタイルが <code>Application.Resources</code> 直下にあり、<code>BasedOn</code> に指定したキーがそのスタイル自身のキーと同一で、かつ同名のキーがそのスタイルを宣言しているディクショナリのマージ辞書にあるため解決されない。右は自前のスタイルを <code>MergedDictionaries</code> 内の別ファイルへ置いたため、同じ <code>MergedDictionaries</code> 内の Fluent へ到達する。いずれも <code>Application</code> に <code>ThemeMode</code> を設定した構成であり、.NET 9 / .NET 10 / Windows 11 で確認した。</figcaption>
</figure>

解決の失敗は、実行時にスタイルをたどれば確認できる。
`Application.Resources` にスタイルを置いた場合は、次の `basedOn` が `null` であれば解決されていない。

```csharp
Style? style = Application.Current.Resources[typeof(Button)] as Style;
Style? basedOn = style?.BasedOn;
```

`Window.Resources` に置いた場合は、`Application.Current.Resources` の代わりに対象ウィンドウの `Resources` を同じように参照する。

ただしこの確認は取りこぼしがある。
`basedOn` が `null` でなくても、参照先が Fluent ではなく既定テーマのスタイルである場合があるためである。
最終的な判断は、角の丸みなど Fluent 固有の要素を実際に描画して行う。

したがって、確実に Fluent を継承させる構成は次のとおりである。

1. `ThemeMode` は `Window` ではなく `Application` に設定する。
2. スタイルを `Application.Resources` 直下に置かない（専用のリソースディクショナリファイルへ切り出してマージするのが扱いやすい）。
3. そのファイルの中では Fluent のディクショナリをマージしない。
4. スタイルに `BasedOn="{StaticResource {x:Type Button}}"` を付ける。

---

## 実装例

### スタイルを別のリソースディクショナリへ切り出す

まず、暗黙スタイルを `Styles.xaml` として独立させる。
`BasedOn` に `{StaticResource {x:Type Button}}` を指定し、Fluent のスタイルを土台にする。

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Style TargetType="Button" BasedOn="{StaticResource {x:Type Button}}">
    <Setter Property="Padding" Value="16,6" />
  </Style>
</ResourceDictionary>
```

このファイルには Fluent のディクショナリを書かない。
`Styles.xaml` の中で `Fluent.xaml` をマージすると、参照先が `Styles.xaml` のマージ辞書になり、解決されなくなる。

### App.xaml でマージする

`App.xaml` では `ThemeMode` を設定し、`Styles.xaml` をマージするだけにする。
スタイルの実体を `Application.Resources` 直下に置かないことが重要である。

```xml
<Application x:Class="MyApp.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             StartupUri="MainWindow.xaml"
             ThemeMode="Light">
  <Application.Resources>
    <ResourceDictionary>
      <ResourceDictionary.MergedDictionaries>
        <ResourceDictionary Source="pack://application:,,,/MyApp;component/Styles.xaml" />
      </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
  </Application.Resources>
</Application>
```

`ThemeMode` がマージする Fluent のディクショナリと `Styles.xaml` は、`Application.Resources` の中で並列のマージ辞書となる。
この配置であれば `BasedOn` は Fluent の暗黙スタイルへ解決され、`Padding` の指定だけが上乗せされる。
実測では、`ThemeMode` が追加する Fluent のディクショナリは `Styles.xaml` より前に並ぶため、後述する順序の問題は起きない。
同じプロジェクト内でビルドアクションが `Resource` のファイルであれば、`Source="Styles.xaml"` のような相対パスでも指定できる。

<figure class="article-figure">
  <img src="/images/articles/wpf-fluent-theme-custom-style-not-applied/implicit-style-basedon-fluent.png" alt="Fluent テーマを適用した WPF ウィンドウ。Save ボタンが角丸で背景の明るい Fluent 外観になり、下の Overwrite チェックボックスと外観が揃っている。" width="286" height="183" loading="lazy">
  <figcaption>上の <code>Styles.xaml</code> と <code>App.xaml</code> をそのまま適用した状態。Windows 11 / .NET 10 / <code>ThemeMode=Light</code> で撮影。前の図と比べると、<code>Button</code> の角が丸くなり背景が明るくなっている。実測では、この状態でも <code>Padding</code> の指定は上書きされずに残る。</figcaption>
</figure>

### 配置と解決結果の対応

`BasedOn` が解決されるかどうかは、スタイルの置き場所と Fluent の供給元の組み合わせで決まる。
.NET 9 と .NET 10 の両方で `Button` について実測した結果は次のとおりである。
表の `BasedOn` 列は、`BasedOn="{StaticResource ...}"` に渡すキーを示す。
`x:Key` を付けた行は、同じディクショナリに暗黙の `{x:Type Button}` スタイルを併置せず、そのスタイルを `Style="{StaticResource ...}"` で明示適用したときの結果である。
この表は、自前のスタイルがコントロールへ実際に適用される配置だけを扱う。自前のスタイル自体が適用されない配置については、後述の注意点を参照する。

| スタイルの置き場所 | Fluent の供給元 | `BasedOn` | 結果 |
| --- | --- | --- | --- |
| `Application.Resources` 直下（暗黙） | `Application` の `ThemeMode` | なし | 旧外観 |
| `Application.Resources` 直下（暗黙） | `Application` の `ThemeMode` | `{x:Type Button}` | 旧外観（`BasedOn` が `null`） |
| `Application.Resources` 直下（暗黙） | `Application` の `ThemeMode` | `DefaultButtonStyle` | Fluent |
| `Application.Resources` 直下（`x:Key` 付き） | `Application` の `ThemeMode` | `{x:Type Button}` | Fluent |
| `Application.Resources` にマージした別ファイル（暗黙） | `Application` の `ThemeMode` | `{x:Type Button}` | Fluent |
| `Window.Resources`（暗黙） | `Application` の `ThemeMode` | `{x:Type Button}` | Fluent |
| `Window.Resources` にマージした別ファイル（暗黙） | `Application` の `ThemeMode` | `{x:Type Button}` | Fluent |
| `Window.Resources`（暗黙） | 同じ `Window` の `ThemeMode` | `{x:Type Button}` | 旧外観（`BasedOn` が `null`） |
| `Window.Resources` にマージした別ファイル（暗黙） | 同じ `Window` の `ThemeMode` | `{x:Type Button}` | 旧外観（`BasedOn` は `null` にならない） |
| `Fluent.xaml` を自身でマージする別ファイル（暗黙） | 同じファイル内の `Fluent.xaml` | `{x:Type Button}` | 旧外観（`BasedOn` が `null`） |

`BasedOn` が `null` になる 3 行は、いずれも「`BasedOn` に指定したキーがそのスタイル自身のキーと同一で、かつ同名のキーがそのスタイルを宣言しているディクショナリのマージ辞書にある」という条件を満たす。
キーが異なる `DefaultButtonStyle` を参照した行と、自身に `x:Key` を付けた行は、この条件を満たさないため、同じ `Application.Resources` 直下でも解決する。

一方「`Window.Resources` にマージした別ファイル」を同じ `Window` の `ThemeMode` と組み合わせた行は、この条件を満たさないにもかかわらず旧外観になる。
この行でスタイルを宣言しているディクショナリは別ファイルであり、Fluent はその外側の `Window.Resources` のマージ辞書にあるため、条件には当てはまらない。
それでも実測では `BasedOn` は `null` にならず、Fluent ではなく既定テーマのスタイルへ解決された。
`ThemeMode` を `Window` へ設定している場合は、スタイルを別ファイルへ出すだけでは解決しない。
`ThemeMode` を `Application` 側へ移せば解決する。
実測では、スタイルを `Window.Resources` 直下に置いた場合も、`Window.Resources` へマージした別ファイルに置いた場合も Fluent へ解決された。
ただし `Application.Resources` 直下へ移すのは別問題であり、表の 2 行目のとおり解決しない。

### 代替の書き方：App.xaml の構成を変えられない場合

ファイル構成を変更できない事情がある場合は、Fluent のテーマディクショナリが持つ `DefaultButtonStyle` を参照する。
自身のキー `{x:Type Button}` とは別のキーであるため、`Application.Resources` 直下でも解決される。

```xml
<Application.Resources>
  <Style TargetType="Button" BasedOn="{StaticResource DefaultButtonStyle}">
    <Setter Property="Padding" Value="16,6" />
  </Style>
</Application.Resources>
```

実測では、Fluent の `{x:Type Button}` スタイルは `Setter` を 1 つも持たず、その `BasedOn` が `DefaultButtonStyle` そのものであった。
`Button` に関しては両者が実質的に等価であり、この書き方で失われる設定はない。
ただし `DefaultButtonStyle` は公式ドキュメントに記載のないキーであり、`Fluent.xaml` の内部構造に依存するため、暫定策として扱う。
他のコントロールで同じ方法を使う場合、キー名と、暗黙スタイルがそのキーのスタイルと等価かどうかは個別に確認する必要がある。
実測では `DefaultButtonStyle` の `TargetType` は `Button` ではなく `ButtonBase` であった。参照先の `TargetType` が基底型になっている場合がある点にも注意する。

### 代替の書き方：適用範囲を限定する場合

キー付きスタイルにすると、同じ `Application.Resources` 直下でも `{x:Type Button}` への `BasedOn` が解決される。

```xml
<Application.Resources>
  <Style x:Key="WideButton" TargetType="Button"
         BasedOn="{StaticResource {x:Type Button}}">
    <Setter Property="Padding" Value="16,6" />
  </Style>
</Application.Resources>
```

このスタイルは自身が `{x:Type Button}` というキーを占有しないため、参照先は Fluent の暗黙スタイルである。
ただしキー付きスタイルは自動適用されないため、各コントロールで `Style="{StaticResource WideButton}"` を明示する必要がある。

この書き方が Fluent へ到達するのは、同じ `Application.Resources` 直下に暗黙の `{x:Type Button}` スタイルが残っていない場合に限る。
実測では、暗黙スタイルを併置すると `BasedOn` はそちらへ解決され、旧外観のままになった。
また `BasedOn` を付け忘れたキー付きスタイルを明示適用した場合も、暗黙スタイルのときと同じく旧外観に戻る。

---

## 注意点

- **エラーが出ない。** `BasedOn` が解決されなくても例外は発生せず、出力ウィンドウにも警告は出ない。見た目の違いだけが手がかりである。移行時は、角の丸みなど Fluent 固有の要素で目視確認する。
- **`ThemeMode` は `Application` に設定する。** `Window` 単位で設定すると、スタイルを別ファイルへ分けても `BasedOn` が Fluent ではなく既定テーマのスタイルへ解決される配置が生じる。さらに、`Window` 側へ `ThemeMode` を設定したまま `Application.Resources` へ自前のスタイルをマージすると、外観は Fluent になるものの自前の `Setter` が効かない。`ThemeMode` が `Window.Resources` へ入れた Fluent の暗黙スタイルが、より外側の `Application.Resources` にある自前のスタイルより先に見つかり、自前のスタイル自体が適用されないためである。ウィンドウごとに明暗を変える必要がない限り、`Application` 側へ統一する。なお `Application` 側に `None` 以外を設定すると、`Window` 側で `None` へ戻すことはできなくなる。
- **コントロールごとに個別の対応が必要である。** 暗黙スタイルのキーは型ごとに異なるため、`Button` を直しても `TextBox` や `CheckBox` のスタイルは別途 `BasedOn` を付ける必要がある。既存アプリの移行では、定義済みのスタイルを型単位で洗い出す。
- **Fluent が用意しているコントロールはバージョンで異なる。** [WPF の .NET 10 における変更点](https://learn.microsoft.com/dotnet/desktop/wpf/whats-new/net100#fluent-style-changes)では `GroupBox` などのスタイルが追加された。実測でも `GroupBox` の暗黙スタイルは .NET 9 の Fluent には存在せず、.NET 10 で追加されている。Fluent 側に暗黙スタイルがないコントロールでは、`BasedOn` は `null` にならず既定テーマのスタイルへ解決されるため、`null` かどうかの確認では気付けない。外観が変わらないことで判断する。
- **`Style="{x:Null}"` は Fluent を外す。** スタイルの適用を明示的に無効化すると、そのコントロールだけ Aero2 の外観になる。既定の見た目に戻す目的で `{x:Null}` を使っている箇所は、Fluent 導入時に見直す。
- **`ThemeMode` を使うなら Fluent のテーマディクショナリを手動でマージしない。** [`Application.ThemeMode` プロパティ](https://learn.microsoft.com/dotnet/api/system.windows.application.thememode)のリファレンスは、このプロパティを設定する場合に Fluent のテーマディクショナリを手動で追加しないよう推奨している。手動で追加したものが優先されるためである。同リファレンスによれば `ThemeMode` はウィンドウの背景素材とダークモードの適用も制御するため、`Fluent.xaml` の手動マージと等価ではない。
- **マージ辞書の順序で結果が変わる。** [マージされたリソースディクショナリ](https://learn.microsoft.com/dotnet/desktop/wpf/systems/xaml-resources-merged-dictionaries)が述べるとおり、同じ `MergedDictionaries` に同じキーがある場合は後に並べた側が採用される。実測では、`Fluent.xaml` を自前のスタイルより後にマージすると、自前の `Setter` が失われた。`Fluent.xaml` を手動でマージするときは、自前のスタイルより前に置く。
- **`Fluent.xaml` を直接マージすると Windows のテーマ設定に従う。** 実測では、ダークモードの環境で `Fluent.xaml` をマージすると、起動時にダーク側のディクショナリが読み込まれた。実行中に OS のテーマを切り替えたときの追従は、本記事では検証していない。明暗を固定するには、手動マージを取りやめたうえで `ThemeMode` に `Light` または `Dark` を指定する。手動マージを残したまま `ThemeMode` を足しても、手動マージ側が優先されるため、コントロールの配色は固定できない。
- **`ThemeMode` と Fluent は変更が続いている。** `ThemeMode` は .NET 10 時点でも実験的 API のままであり、リファレンスには将来のバージョンで削除される可能性があると記されている。Fluent のスタイル実装も継続中である。本記事の対応表は .NET 9 / .NET 10 で確認した結果であり、将来のバージョンでは再確認が必要である。

---

## 代替案・比較

| 方法 | メリット | デメリット | 適するケース |
| --- | --- | --- | --- |
| スタイルを別ディクショナリへ分離し `BasedOn="{StaticResource {x:Type Button}}"` | 公開された記述だけで完結し、暗黙スタイルのまま自動適用が続く | ファイル構成の変更が必要 | 既存アプリを Fluent へ移行する標準的なケース |
| `BasedOn="{StaticResource DefaultButtonStyle}"` | `App.xaml` の構成を変えずに済む | 公式ドキュメントに記載のないキーに依存する | ファイル構成を変えられない暫定対応 |
| `BasedOn` 付きのキー付きスタイルを明示適用 | 適用範囲を限定でき、影響を局所化できる | すべての適用箇所に `Style` の指定が必要 | 一部の画面・コントロールだけ調整したい場合 |
| Fluent を導入せず既存スタイルを維持 | 移行作業が不要 | Windows 11 の外観や明暗テーマの追従が得られない | 独自デザインを全面的に作り込んでいる場合 |

---

## まとめ

Fluent テーマは、スタイルの供給経路について言えば、テーマスタイルではなくリソースディクショナリ内の暗黙スタイルとして届く。
そのため、アプリ側が同じ `{x:Type Button}` キーのスタイルを定義すると Fluent のスタイルが隠れ、コントロールは Aero2 の外観へフォールバックする。

解決は `BasedOn="{StaticResource {x:Type Button}}"` による継承だが、`BasedOn` に指定したキーがそのスタイル自身のキーと同一で、かつ同名のキーがそのスタイルを宣言しているディクショナリのマージ辞書にあると、`BasedOn` は解決されず、エラーも出ないまま旧外観が残る。
既存アプリを移行する場合は、`ThemeMode` を `Application` に設定したうえで、スタイルを専用のリソースディクショナリファイルへ切り出し、`App.xaml` からマージする構成を既定とするのが妥当である。
`ThemeMode` を `Window` 単位で設定すると、スタイルを別ファイルへ分けても Fluent へ継承されない配置が生じる。
`App.xaml` の構成を変更できない事情がある場合に限り `DefaultButtonStyle` を参照し、影響範囲を一部に限定したい場合はキー付きスタイルと明示適用を選ぶ。

---

## 関連記事

- [WPF で Fluent デザインを追加ライブラリなしで適用する方法](/ja/articles/wpf-fluent-design-with-systemcolors/)
- [WPF Fluent テーマの TextBox でクリアボタンを非表示にする方法](/ja/articles/wpf-fluent-textbox-hide-clear-button/)
- [WPF で StaticResource を変更しても画面が更新されない原因と解決方法](/ja/articles/wpf-staticresource-vs-dynamicresource/)
