---
layout: article-ja
title: "WPF の Label でアンダーバーが消える理由と回避方法"
date: 2026-06-09
category: WPF
excerpt: "WPF の Label にアンダーバー（_）を含む文字列を表示すると画面上で消える。原因である ContentPresenter.RecognizesAccessKey の働きと、影響を受けるコントロールの範囲、4 つの回避方法を実測付きで解説する。"
image: /images/articles/wpf-label-underscore-issue/label-underscore-rendering.png
---

## 概要

WPF の `Label` コントロールに `_` （アンダーバー）を含む文字列を設定すると、その文字が画面に表示されず消えてしまうことがある。
これは WPF の仕様によるものであり、原因と代表的な回避方法を整理する。

この事象は `Label` 固有の不具合として扱われることが多いが、実際には `Button` や `CheckBox` など複数のコントロールで同じ結果になる。
本記事では、影響を受けるコントロールの範囲を実測で確定させたうえで、4 つの回避方法とその選択基準を示す。

---

## 前提・対象環境

- フレームワーク／言語: .NET 10 / C# / WPF / XAML
- 対象コントロール: 既定テンプレートの `ContentPresenter` が `RecognizesAccessKey="True"` である標準コントロール（`Label`・`Button`・`CheckBox`・`RadioButton`・`ToggleButton` と、`GroupBox`・`Expander`・`TabItem`・`MenuItem` の `Header`）
- アーキテクチャ: MVVM / コードビハインドのいずれでも発生する
- 検証環境: Windows 11、既定テーマ（Aero2）、表示スケール 100%
- 前提知識: WPF 基本操作、XAML の基礎

本記事に載せた図と実測値は、上記の環境で実際にアプリケーションを起動して取得したものである。
**他のバージョンやテーマでは確認していない。** 既定テンプレートの `RecognizesAccessKey` に依存する挙動であるため、テーマや `ControlTemplate` を差し替えた環境では結果が変わりうる。

---

## 問題

`Label` の `Content` にアンダーバーを含む文字列（例: `my_variable`）を設定したとき、画面には `myvariable` のようにアンダーバーが欠落した状態で表示される。
また、`_F` のように書くと `F` に下線が付いた状態で表示されることがある。
バインドしている文字列データにアンダーバーが含まれている場合でも同様に消えてしまうため、動的なデータ表示でも問題が発生する。

ファイルパス、識別子、データベースの列名、スネークケースのキーなど、アンダーバーを含む文字列を画面に出す場面は多い。
それらをそのまま `Label` に流し込むと、開発中は気付かず、実データを表示した段階で初めて表示崩れが露見する。

---

## 原因・背景

`Label` は内部で `AccessText` というコントロールを使ってテキストを描画している。
`AccessText` はアンダーバーをアクセスキー（Alt キーと組み合わせてフォーカス移動を行うショートカット機能）の目印として解釈する。

具体的な挙動は以下のとおりである。

| 入力文字列 | 画面上の表示   | 解釈                                             |
| ---------- | -------------- | ------------------------------------------------ |
| `_File`    | **F**ile       | `F` がアクセスキーとして登録される               |
| `my_var`   | my**v**ar      | `v` がアクセスキーとして登録される               |
| `name_`    | name_          | 直後に文字が無いため、アンダーバーがそのまま残る |

上表の 3 例を実際に描画した結果が次の画像である。

<figure class="article-figure">
  <img src="/images/articles/wpf-label-underscore-issue/label-underscore-rendering.png" alt="WPF の Label に _File、my_var、name_ を設定して実行した画面。_File は File、my_var は myvar と表示され、アンダーバーが消えている。name_ だけはアンダーバーが残っている。" width="461" height="166" loading="lazy">
  <figcaption>.NET 10 / Windows 11 で <code>Label</code> に各文字列を設定した実行結果。左が XAML の記述、右が実際の描画である。<code>_File</code> と <code>my_var</code> ではアンダーバーが失われる一方、<code>name_</code> は直後に対象の文字が無いためアクセスキーとして解釈されず、そのまま表示される。</figcaption>
</figure>

このため、データにアンダーバーが含まれているだけで、意図しない表示崩れが起きる。
なお、アンダーバーが消えるのは直後に文字が続く場合に限られるため、文字列中のどの位置にアンダーバーがあるかで結果が変わる。

アンダーバーが複数ある場合、アクセスキーとして登録されるのは最初の 1 つだけである。
`a_b_c` を与えると `b` がアクセスキーになり、2 つ目のアンダーバーはそのまま表示される。

### AccessText を挟むかどうかを決めているもの

`AccessText` が使われるかどうかは、`Label` という型そのものではなく、**既定の `ControlTemplate` に置かれた `ContentPresenter` の `RecognizesAccessKey` プロパティ**で決まる。
`ContentPresenter` は、`RecognizesAccessKey` が `True` で、**かつ文字列にアンダーバーが含まれるときにだけ** `AccessText` を生成する。
アンダーバーを含まない文字列では、`RecognizesAccessKey` が `True` であっても通常の `TextBlock` が使われる。

visual ツリーを実際にたどると、この差がそのまま現れる。

| `Content` | 構築される visual ツリー |
| --- | --- |
| `Status Running` | `Label` → `Border` → `ContentPresenter` → `TextBlock` |
| `Status _Running` | `Label` → `Border` → `ContentPresenter` → `AccessText` → `TextBlock` |

アンダーバーを含む場合だけ `AccessText` が 1 段挟まる。
つまり「`Label` がアンダーバーを食べる」のではなく、「`RecognizesAccessKey="True"` の `ContentPresenter` が食べる」というのが正確な理解である。

### 影響を受けるコントロールの範囲

`RecognizesAccessKey` を `True` にしている既定テンプレートは `Label` だけではない。
各コントロールに同じ文字列 `my_var` を与え、visual ツリー上に `AccessText` が生成されるかを調べた結果が次の表である。

| コントロール | アンダーバーの扱い | 対象プロパティ |
| --- | --- | --- |
| `Label` | 消える | `Content` |
| `Button` | 消える | `Content` |
| `CheckBox` | 消える | `Content` |
| `RadioButton` | 消える | `Content` |
| `ToggleButton` | 消える | `Content` |
| `GroupBox` | 消える | `Header` のみ |
| `Expander` | 消える | `Header` のみ |
| `TabItem` | 消える | `Header` のみ |
| `MenuItem` | 消える | `Header` のみ |
| `TreeViewItem` | 消えない | — |
| `ListBoxItem` | 消えない | — |
| `ComboBoxItem` | 消えない | — |
| `StatusBarItem` | 消えない | — |
| `TextBlock` | 消えない | — |

`Header` を持つこれらのコントロールでは、`RecognizesAccessKey="True"` なのは `Header` を描画する `ContentPresenter` だけである。
本体の `Content` を描画する側の構成はコントロールによって異なる。

| コントロール | 自身のテンプレート内の `ContentPresenter` |
| --- | --- |
| `GroupBox`・`Expander` | 2 つ。`Header` 用（`True`）と `Content` 用（`False`） |
| `MenuItem` | 2 つ。`Header` 用（`True`）と `Icon` 用（`False`）。`MenuItem` は `Content` プロパティを持たない |
| `TabItem` | 1 つだけ。`Header` 用（`True`）。選択中の `Content` は親の `TabControl` にある `PART_SelectedContentHost` が描画する |

このため、同じコントロールでもヘッダーに置いた文字列だけアンダーバーが消える。

主要なコントロールについて、同じ文字列を与えて描画した結果を次に示す。

<figure class="article-figure">
  <img src="/images/articles/wpf-label-underscore-issue/label-underscore-affected-controls.png" alt="my_var という同じ文字列を Label、Button、CheckBox、GroupBox のヘッダー、ListBoxItem、TextBlock に設定した実行画面。前の 4 つは myvar と表示され、ListBoxItem と TextBlock だけが my_var と表示されている。" width="504" height="309" loading="lazy">
  <figcaption>.NET 10 / Windows 11 で、同じ文字列 <code>my_var</code> を各コントロールに与えた実行結果。<code>Label</code>・<code>Button</code>・<code>CheckBox</code>・<code>GroupBox</code> のヘッダーではアンダーバーが失われ、<code>ListBoxItem</code> と <code>TextBlock</code> ではそのまま表示される。差は既定テンプレートの <code>ContentPresenter.RecognizesAccessKey</code> によって生じている。</figcaption>
</figure>

`ListBoxItem` や `ComboBoxItem` で問題が起きないのは、一覧に並ぶ項目にアクセスキーを割り当てる意味がないためである。
逆に言えば、`ItemTemplate` の中に `Label` や `Button` を置いた場合は、そこでアンダーバーが消える。

---

## 解決方法

回避方法は 4 つある。用途に合わせて使い分ける。

- アンダーバーをエスケープしたいだけなら、`__` と 2 つ重ねて書く（方法 1）。
- アクセスキー機能が不要で単純にテキストを表示したい場合は、`TextBlock` に変更する（方法 2）。
- `Label` のまま動的なバインドデータを正しく表示したい場合は、`ContentTemplate` で `TextBlock` を使う（方法 3）。
- 既定の文字列表示でのアクセスキー解釈そのものを止めたい場合は、`ControlTemplate` で `RecognizesAccessKey="False"` を指定する（方法 4）。ただし既定テンプレートを置き換えるため、`Border` や余白は自前で作り直すことになる。

方法 1 だけが「アクセスキー機能を残したまま表示を直す」手段であり、方法 2〜4 はいずれもアクセスキー機能を捨てる代わりに文字列をそのまま扱えるようにする手段である。

---

## 実装例

### 方法 1：アンダーバーを 2 つ重ねてエスケープする

`__` と 2 つ続けて書くことで、画面に 1 つのアンダーバーが表示される。
XAML 側で静的に文字列を設定しているケースに向いている。

```xml
<Label Content="my__variable" />
```

- **メリット:** XAML を 1 箇所修正するだけで対応できる。アクセスキー機能もそのまま使える。
- **デメリット:** 動的バインドのデータにアンダーバーが含まれている場合、ViewModel 側で置換処理が必要になる。

動的データに対して置換で対応する場合は、次のように書く。

```csharp
// 表示用にアンダーバーをエスケープする。元の値は書き換えない。
public string DisplayName => Name.Replace("_", "__");
```

この置換は**必ず 1 回だけ適用する**。
すでにエスケープ済みの文字列へ再度適用すると `a_b` が `a____b` となり、画面には `a__b` と表示されて誤りになる。
プロパティのゲッターで都度算出する形にしておくと、二重適用を避けやすい。

---

### 方法 2：TextBlock コントロールに変更する

アクセスキー機能や `Target` プロパティによるフォーカス制御が不要であれば、`Label` を `TextBlock` に変更するのが最も単純な対応である。
`TextBlock` は `AccessText` を使わないため、アンダーバーをそのまま表示できる。

```xml
<TextBlock Text="my_variable" />
```

バインドの場合も同様にそのまま動作する。

```xml
<TextBlock Text="{Binding VariableName}" />
```

- **メリット:** アンダーバーを気にする必要がなくなる。`Label` より軽量で、表示専用であれば `TextBlock` が適切である。
- **デメリット:** `Label` の `Target` プロパティによるフォーカス制御は使えなくなる。既定の `Padding` も無くなるため、行間と整列の再確認が必要になる。

---

### 方法 3：ContentTemplate で TextBlock を使う

`Label` を維持しつつ、動的バインドのデータにアンダーバーが含まれる場合でも正しく表示するには、`ContentTemplate` に `TextBlock` を指定する。
これにより、`Label` の `Content` を `AccessText` ではなく `TextBlock` で描画させることができる。

```xml
<Label Content="{Binding VariableName}">
    <Label.ContentTemplate>
        <DataTemplate>
            <TextBlock Text="{Binding}" />
        </DataTemplate>
    </Label.ContentTemplate>
</Label>
```

アプリ全体で統一したい場合は、このテンプレートをスタイルとして定義する方法もある。

```xml
<Style x:Key="PlainLabel" TargetType="Label">
    <Setter Property="ContentTemplate">
        <Setter.Value>
            <DataTemplate>
                <TextBlock Text="{Binding}" />
            </DataTemplate>
        </Setter.Value>
    </Setter>
</Style>
```

- **メリット:** 動的バインドのデータにアンダーバーが含まれても、そのまま表示できる。スタイルとして共通化すれば複数箇所への適用も容易である。
- **デメリット:** XAML のコード量が増える。`ContentTemplate` は `Content` が文字列以外のオブジェクトである場合にも適用されるため、対象を絞る必要がある。

---

### 方法 4：ControlTemplate で RecognizesAccessKey を False にする

原因そのものを断つ方法である。
`ControlTemplate` を差し替え、`ContentPresenter` の `RecognizesAccessKey` を `False` に指定する。

```xml
<Label Content="{Binding VariableName}">
    <Label.Template>
        <ControlTemplate TargetType="Label">
            <ContentPresenter RecognizesAccessKey="False" />
        </ControlTemplate>
    </Label.Template>
</Label>
```

`ContentPresenter` が既定の文字列表示で `AccessText` を選ばなくなるため、アンダーバーはそのまま表示される。

この設定が効くのは、`ContentPresenter` が文字列から表示要素を選ぶ既定の経路だけである。
`ContentTemplate` を明示した場合はそちらのテンプレートが優先され、`Content` に `AccessText` を直接置いた場合はその要素がそのまま描画される。
いずれの場合も `RecognizesAccessKey` の値は結果に影響しない。

- **メリット:** `ContentTemplate` を用意せずに、既定の文字列表示からアクセスキー解釈だけを外せる。バインドしたデータが文字列である限り、値の内容によらず一律に効く。
- **デメリット:** `ControlTemplate` を差し替えるため、既定テンプレートが持つ `Border` や無効時の表示（`IsEnabled="False"` のときの前景色）を自前で書き直す必要がある。上の例のように `ContentPresenter` だけを置くと、それらは失われる。

既定の外観を保ちたい場合は、`ControlTemplate` を全面的に差し替えるのではなく、方法 3 を選ぶほうが影響範囲が小さい。

4 つの方法をそれぞれ実行すると、いずれも同じ表示結果になる。

<figure class="article-figure">
  <img src="/images/articles/wpf-label-underscore-issue/label-underscore-workarounds.png" alt="4 つの回避方法を適用した WPF アプリの画面。エスケープした Label、TextBlock、ContentTemplate を差し替えた Label、RecognizesAccessKey を False にした Label のいずれもが my_variable と表示している。" width="619" height="202" loading="lazy">
  <figcaption>4 つの回避方法を同一アプリ上で実行した結果。<code>__</code> でのエスケープ、<code>TextBlock</code> への変更、<code>ContentTemplate</code> の差し替え、<code>RecognizesAccessKey="False"</code> のいずれでも <code>my_variable</code> が欠落せずに表示される。</figcaption>
</figure>

---

## 注意点

- `Target` プロパティを使ってフォーカス移動を行いたい場合、アクセスキーの仕組みは有効に保つ必要があるため、方法 2・3・4 は適していない。その場合は方法 1（エスケープ）を使う。
- 方法 3 で `ContentTemplate` を使うと、`Content` が文字列以外のオブジェクトであるケースにも適用されるため、バインドしているデータ型が `string` であることを確認してから適用する。
- ViewModel 側で `_` を `__` に置換する方法は、View 側の表示ルールを ViewModel に持ち込む構成になる。値オブジェクトや業務ロジックが参照する元のプロパティは変更せず、表示専用のプロパティを別に用意する。
- 同じ問題は `Button` や `MenuItem` でも起きる。`Label` だけを直しても、同じ文字列をボタンのキャプションに使っていれば同様に欠落する。データ由来の文字列を表示する箇所は横断的に確認する。
- アクセスキーが意図せず登録されると、表示崩れだけでなく**キーボード操作にも影響する**。同じアクセスキーを持つコントロールが同一スコープに複数あると、Alt キーによるフォーカス移動先が切り替わる形になり、操作が不安定になる。
- 一覧内でアンダーバーを含む文字列を扱う場合、`ListBox` 自体は影響を受けないが、`ItemTemplate` の中に `Label` を置いていればそこで消える。テンプレートの中身まで確認する。

### 描画コストへの副作用

`AccessText` が挟まると visual が 1 段増えるため、描画コストにも影響する。
非仮想化の `StackPanel` に `Label` を 1,000 個並べて比較すると、アンダーバーを含む文字列を与えた場合のレイアウト時間は、含まない場合の約 3 倍になる。
一方、方法 3 の `ContentTemplate` を適用した `Label` は、アンダーバーを含まない `Label` とほぼ同じコストに収まる。

大量のテキストを表示する画面でアンダーバー入りのデータを扱う場合、方法 3 は表示の正しさと描画コストの両方に効く。
コントロール選択と描画コストの関係は「[WPF で Label を大量配置すると遅い原因と TextBlock への置き換え指針](/ja/articles/wpf-label-vs-textblock-performance/)」で詳しく扱う。

---

## 代替案・比較

| 方法                                | メリット                                     | デメリット                               | 適するケース                       |
| ----------------------------------- | -------------------------------------------- | ---------------------------------------- | ---------------------------------- |
| 方法 1: `__` でエスケープ           | XAML 1 箇所の修正で済む。アクセスキーを維持できる | 動的データには ViewModel 側の処理が必要。二重適用の危険がある | 静的な文字列で、アクセスキーも使いたい場合 |
| 方法 2: `TextBlock` に変更          | 最もシンプルで軽量                           | `Label` の `Target` 機能と既定余白を失う       | 表示専用でアクセスキーが不要な場合 |
| 方法 3: `ContentTemplate` を変更    | 動的バインドに対応でき、既定の外観を保てる   | XAML 量が増える。`Content` の型を選ばない       | `Label` を維持しつつ動的表示が必要 |
| 方法 4: `RecognizesAccessKey="False"` | 原因を直接無効化できる。`ContentTemplate` が不要 | `ControlTemplate` の再実装が必要。既定の文字列表示にしか効かない | 独自テンプレートを既に持っている場合 |

---

## まとめ

WPF の `Label` でアンダーバーが消えるのは、既定テンプレートの `ContentPresenter` が `RecognizesAccessKey="True"` であり、文字列を `AccessText` として描画するためである。
`Label` 固有の問題ではなく、`Button`・`CheckBox`・`RadioButton` や `GroupBox`・`Expander`・`TabItem`・`MenuItem` の `Header` でも同じ結果になる。

- 表示専用のテキストであれば、`TextBlock` への変更（方法 2）が最もシンプルで適切な対応である。
- `Label` を使う必要があり、静的な文字列でアクセスキーも残したい場合は `__` でエスケープ（方法 1）する。
- 動的バインドのデータにアンダーバーが含まれる場合は、`ContentTemplate` で `TextBlock` を使う（方法 3）。既定の外観を保ったまま対応でき、`AccessText` による描画コストの増加も避けられる。
- すでに独自の `ControlTemplate` を持っている場合は、その `ContentPresenter` に `RecognizesAccessKey="False"` を指定する（方法 4）。

アクセスキーを使うかどうかが選択の分岐点になる。
使うなら方法 1、使わないなら方法 2〜4 から、`Label` の外観をどこまで保ちたいかで選ぶ。

---

<!-- 関連記事 -->
- [WPF で Label を大量配置すると遅い原因と TextBlock への置き換え指針](/ja/articles/wpf-label-vs-textblock-performance/)
