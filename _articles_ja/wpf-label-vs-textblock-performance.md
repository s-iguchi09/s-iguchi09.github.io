---
layout: article-ja
title: "WPF で Label を大量配置すると遅い原因と TextBlock への置き換え指針"
date: 2026-06-10
category: WPF
excerpt: "WPF で Label を大量配置した際に描画が遅くなる原因を、visual ツリーの実測に基づいて整理する。TextBlock との差、アンダーバーを含む文字列が招く追加コスト、仮想化を有効にした場合に差が残るかまでを扱う。"
image: /images/articles/wpf-label-vs-textblock-performance/label-vs-textblock-measurement.png
---

## 概要

WPF で `Label` を大量に配置した画面において、初期描画とスクロール時の応答が低下する事象を確認した。
本記事では、`Label` と `TextBlock` の構造差に基づいて原因を整理し、性能を優先する画面での実装方針を示す。

結論を先に述べる。
`Label` と `TextBlock` の差は実測できるが、**それが問題になるのは UI 仮想化が効いていない画面に限られる**。
仮想化が有効な `ListBox` に 10,000 件を流した場合、両者のレイアウト時間に実用上の差は残らない。
コントロールの置き換えは、仮想化を確認したうえで検討する順序が適切である。

---

## 前提・対象環境

- フレームワーク／言語: .NET 10 / C# / WPF
- 対象コントロール: `Label`、`TextBlock`、`ContentPresenter`
- アーキテクチャ: MVVM（コードビハインドでも同様）
- 想定画面: 一覧・ダッシュボードなどテキスト要素を多数表示する画面
- 計測環境: Windows 11、既定テーマ（Aero2）、表示スケール 100%

本記事の数値は、上記環境で実際にアプリケーションを起動し、`Measure` から `UpdateLayout` までの所要時間を計測して得たものである。
各条件を交互に 15 回試行し、その最小値を採っている。
所要時間は実行環境に依存するため、**絶対値ではなく条件間の比率**として読む。

---

## 問題

`Label` を数十個から数百個単位で配置した画面では、以下の問題が発生しやすい。

- 初期表示が遅くなる。
- リサイズやスクロール時の再描画が重くなる。
- 同じ文字列表示用途でも `TextBlock` 構成よりメモリ使用量が増える。

フォーム見出し向けに `Label` を使う実装をそのまま一覧表示へ展開すると、表示性能が劣化しやすい。

---

## 原因・背景

`TextBlock` はテキスト描画を主目的とする軽量な要素であり、`FrameworkElement` を直接継承する。
一方で `Label` は `ContentControl` を継承し、文字列以外のコンテンツも扱える汎用 UI 部品として設計されている。

`Label` は内部で `ContentPresenter` を介して描画を行い、必要に応じてアクセスキー処理や `Target` 連携などの機能を提供する。
このため、単純な文字表示だけを大量に行う用途では、`TextBlock` よりもオーバーヘッドが大きくなる。

### visual ツリーの内訳

差の実体は、1 個あたりに構築される visual の数である。
既定テンプレートで文字列を与えたときの構成は次のとおりになる。

| 要素 | 構築される visual ツリー | visual 数 |
| --- | --- | --- |
| `TextBlock` | `TextBlock` | 1 |
| `ContentPresenter` | `ContentPresenter` → `TextBlock` | 2 |
| `Label` | `Label` → `Border` → `ContentPresenter` → `TextBlock` | 4 |

`Label` 1 個につき、`Border`・`ContentPresenter`・実際に文字を描く `TextBlock` が追加で構築される。
測定・配置・描画の各段階で処理対象が 4 倍になることが、性能差の直接の原因である。

### 非仮想化時の実測

同じ文字列を `StackPanel` に並べ、レイアウト完了までの時間と生成された visual の総数を測ると次のようになる。

<figure class="article-figure">
  <img src="/images/articles/wpf-label-vs-textblock-performance/label-vs-textblock-measurement.png" alt="計測結果の表。StackPanel に 250 個、1,000 個、4,000 個を並べた場合の visual 数とレイアウト時間を Label と TextBlock で比較している。Label の visual 数は常に TextBlock の 4 倍で、レイアウト時間はおよそ 2 倍である。" width="578" height="190" loading="lazy">
  <figcaption>.NET 10 / Windows 11、既定テーマ（Aero2）の <code>ControlTemplate</code> で、<code>Content</code> にアクセスキーを含まない文字列を与えた場合の実測値。仮想化されない <code>StackPanel</code> に並べている。visual 数はテーマや <code>ControlTemplate</code> の差し替えで変わり、所要時間は実行環境に依存するため、絶対値ではなく比率を目安として読む。</figcaption>
</figure>

visual 数は要素数に正確に比例し、`Label` は常に `TextBlock` の 4 倍になる。
一方でレイアウト時間の差はおよそ 2 倍にとどまる。
visual 1 個あたりの処理量が一定ではなく、`Border` や `ContentPresenter` のように文字を描画しない要素のコストが相対的に小さいためである。

メモリについても同様の傾向がある。
1,000 個を並べた直後のマネージドヒープの増分を比較すると、`Label` は `TextBlock` のおよそ 1.6 倍を消費する。
visual 数の比（4 倍）ほどには開かないが、要素数に比例して増える点は同じである。

### アンダーバーを含む文字列による追加コスト

`Label` のコストは `Content` の内容によっても変わる。
文字列にアンダーバー（`_`）が含まれると、`ContentPresenter` は `TextBlock` ではなく `AccessText` を生成し、visual が 1 段増えて 5 個になる。

1,000 個で固定し、構成を変えて比較した結果が次の表である。

<figure class="article-figure">
  <img src="/images/articles/wpf-label-vs-textblock-performance/label-vs-textblock-variants.png" alt="1,000 個を StackPanel に並べた場合の visual 数とレイアウト時間の表。Label、アンダーバーを含む Label、ContentTemplate を指定した Label、ContentPresenter、TextBlock を比較しており、アンダーバーを含む Label だけが突出して遅い。" width="405" height="251" loading="lazy">
  <figcaption>同一環境で 1,000 個を <code>StackPanel</code> に並べた場合の実測値。<code>Content</code> にアンダーバーを含む <code>Label</code> は <code>AccessText</code> が挟まり、visual が 1 個増えるだけでレイアウト時間はおよそ 3 倍になる。<code>ContentTemplate</code> に <code>TextBlock</code> を指定した <code>Label</code> は、アンダーバーを含まない <code>Label</code> とほぼ同じコストに収まる。</figcaption>
</figure>

visual 数は 4 個から 5 個へ 25% 増えるだけだが、レイアウト時間はおよそ 3 倍に達する。
`AccessText` はアクセスキーの解析と下線付き描画のために、単純な `TextBlock` より重い処理を行うためである。

この差は、ファイルパスや識別子のようにアンダーバーを含みやすいデータを表示する画面で実際に効く。
アンダーバーが表示から消える問題そのものは「[WPF の Label でアンダーバーが消える理由と回避方法](/ja/articles/wpf-label-underscore-issue/)」で扱う。

### 仮想化を有効にした場合

ここまでは `StackPanel` に直接並べた、仮想化されない構成での比較である。
UI 仮想化が有効な `ItemsControl` では前提が変わる。
画面に見えている範囲のコンテナだけが実体化されるため、コレクションの件数がいくら多くても、同時に存在する visual の数は一定に保たれる。

仮想化を有効にした `ListBox` に 10,000 件を流し、`ItemTemplate` の中身だけを入れ替えて比較した結果が次の表である。

<figure class="article-figure">
  <img src="/images/articles/wpf-label-vs-textblock-performance/label-vs-textblock-virtualized.png" alt="仮想化した ListBox に 10,000 件を流した場合の visual 数とレイアウト時間の表。Label と TextBlock で visual 数に差はあるが、レイアウト時間はほぼ同じである。" width="305" height="160" loading="lazy">
  <figcaption>同一環境で、<code>IsVirtualizing="True"</code>・<code>VirtualizationMode="Recycling"</code> の <code>ListBox</code> に 10,000 件をバインドした場合の実測値。実体化されるコンテナは表示範囲の分だけであるため、visual の総数は件数に依存しない。レイアウト時間の差は計測のばらつきと同程度で、非仮想化時に見えた約 2 倍の差は残らない。</figcaption>
</figure>

非仮想化の `StackPanel` で 4,000 個を並べた場合と比べると、件数が 2.5 倍でありながらレイアウト時間は 2 桁小さい。
`Label` と `TextBlock` の差も、この規模では計測のばらつきに埋もれる。

**「`Label` を大量配置すると遅い」という現象の主因は、`Label` そのものではなく仮想化が効いていないことにある。**
コントロールの選択で得られるのは約 2 倍の改善だが、仮想化の有無による差はそれよりはるかに大きい。

---

## 解決方法

改善は次の順序で検討する。

1. **仮想化が効いているかを先に確認する。** 一覧表示であれば、`ItemsControl` 系のコントロールを使い、UI 仮想化を有効に保つ。ここが崩れていると、コントロールを置き換えても効果は限定的である。
2. **表示専用テキストを `TextBlock` に寄せる。** 仮想化が使えない画面や、要素数が固定で多い画面で有効である。
3. **アンダーバーを含むデータには `ContentTemplate` を使う。** `Label` を維持しつつ `AccessText` の追加コストを避けられる。

用途の分離としては次のとおりである。

- 表示専用テキスト: 原則 `TextBlock` を採用する。
- 入力フォーム見出し: `Target` やアクセスキーが必要な箇所のみ `Label` を使用する。
- 既存画面の改修: `Label` を一括で置換せず、要件を満たす範囲で段階的に `TextBlock` 化する。

---

## 実装例

### 仮想化を保つ

一覧表示では、`ItemsControl` をそのまま使うのではなく、仮想化パネルを持つコントロールを選ぶ。
`ItemsControl` の既定の `ItemsPanel` は `StackPanel` であり、仮想化されない。

```xml
<!-- 仮想化されない。件数に比例してコンテナが作られる -->
<ItemsControl ItemsSource="{Binding Items}" />

<!-- 仮想化される。ListBox の既定は VirtualizingStackPanel -->
<ListBox ItemsSource="{Binding Items}"
         ScrollViewer.CanContentScroll="True"
         VirtualizingPanel.IsVirtualizing="True"
         VirtualizingPanel.VirtualizationMode="Recycling" />
```

`ItemsControl` のまま仮想化したい場合は、`ItemsPanel` に `VirtualizingStackPanel` を指定し、`ScrollViewer` を伴うテンプレートを与える必要がある。
選択機能が不要でも `ListBox` を使い、`Focusable` や選択の見た目をスタイルで無効化するほうが確実である。

`ScrollViewer.CanContentScroll` を `False` にすると、スクロール単位がアイテム単位からピクセル単位に変わり、仮想化が無効になる点に注意する。
この設定は既定値が `True` であり、滑らかなスクロールを求めて変更されることがある。

### 表示専用の Label を TextBlock に置き換える

単純表示用途の `Label` を `TextBlock` に置き換える。
この置き換えは、見た目を維持しつつ描画コストを削減する目的で実施する。

```xml
<!-- 置き換え前 -->
<Label Content="ステータス: 実行中" />

<!-- 置き換え後 -->
<TextBlock Text="ステータス: 実行中" />
```

この変更により、表示専用箇所から `Label` 固有機能を外し、軽量な描画構成に統一できる。
ただし `Label` の既定 `Padding` がなくなるため、必要に応じて `Margin` や `Padding` を明示する。

### Label が必要な箇所は残す

`Label` が必要な入力フォーム見出しの例を示す。
このパターンでは操作性とアクセシビリティを維持するため、`Label` を継続利用する。

```xml
<StackPanel Orientation="Horizontal">
    <Label Content="名前(_N):"
           Target="{Binding ElementName=NameTextBox}"
           VerticalAlignment="Center" />
    <TextBox x:Name="NameTextBox" Width="180" />
</StackPanel>
```

`Alt + N` によるフォーカス移動と、ラベルクリック時のフォーカス移動を利用する要件では `Label` が適する。
このため、性能改善では「全面禁止」ではなく「要件ベースの使い分け」が必要となる。

なお、この例のようにアクセスキーを意図して使う場合は `AccessText` が構築されるが、フォーム見出しの個数は通常わずかであり、コストは問題にならない。
`AccessText` のコストが効くのは、アンダーバーを含むデータを**大量に**表示する場合である。

### アンダーバーを含むデータを Label で表示する

`Label` の外観を保ったまま `AccessText` を避けるには、`ContentTemplate` に `TextBlock` を指定する。

```xml
<Label Content="{Binding FilePath}">
    <Label.ContentTemplate>
        <DataTemplate>
            <TextBlock Text="{Binding}" />
        </DataTemplate>
    </Label.ContentTemplate>
</Label>
```

`ContentPresenter` が `AccessText` を生成しなくなるため、アンダーバーがそのまま表示され、描画コストもアンダーバーを含まない `Label` と同等に戻る。
ただし `ContentTemplate` は `Content` が文字列以外のオブジェクトである場合にも適用されるため、対象は文字列表示に限定する。

---

## 注意点

- `Label` から `TextBlock` へ置き換えると、アクセスキー（`_`）と `Target` によるフォーカス連携は失われる。
- 長文表示では、`TextBlock` 側で `TextWrapping="Wrap"` や `TextTrimming` を明示しないと意図した表示にならない場合がある。
- 既存 UI では `Label` 既定余白に依存していることがあるため、置き換え後の行間と整列を確認する必要がある。
- `DataGrid` や `ItemsControl` のテンプレート内で多数描画する場合は、コントロールの選択よりも先に仮想化設定を評価する。仮想化が無効なまま `TextBlock` に置き換えても、得られる改善は限定的である。
- 本記事の計測は `Measure` から `UpdateLayout` までのレイアウト時間である。実際の描画（レンダリング）やスクロール時の再利用コストは含まない。スクロールの滑らかさを評価する場合は、別途スクロール操作を伴う計測が必要になる。
- 数十個規模であれば、`Label` と `TextBlock` の差は体感できない。置き換えは、実際に遅い画面を特定してから行う。

---

## 代替案・比較

| 方法 | メリット | デメリット | 適するケース |
| --- | --- | --- | --- |
| 仮想化を有効にする | 件数に依存しないコストにできる。効果が最も大きい | スクロール単位がアイテム単位になる。`CanContentScroll="False"` と併用できない | 一覧・グリッドなど件数が可変の画面 |
| すべて `Label` のまま維持 | アクセスキーや `Target` を既存仕様どおり保持できる | 大量表示時の描画コストが高い | 入力フォーム中心で要素数が少ない画面 |
| 表示専用箇所を `TextBlock` へ置換 | 描画とメモリ負荷を抑えやすい | 余白・折り返し設定の見直しが必要 | 仮想化できない、要素数の多い固定レイアウト |
| `Label` + `ContentTemplate` | 外観を保ったまま `AccessText` のコストを回避できる | XAML 量が増える | アンダーバーを含むデータを `Label` で表示する画面 |
| 画面全体を `TextBlock` 化 | 単純化しやすく軽量 | `Label` 固有の操作性を失う | 入力連携が不要な読み取り専用画面 |

---

## まとめ

WPF で `Label` を大量配置した場合の遅延要因は、`ContentControl` としての汎用機能に起因する描画オーバーヘッドである。
`Label` は 1 個あたり 4 個の visual を構築し、`TextBlock` の 4 倍になる。
非仮想化の構成では、レイアウト時間で約 2 倍、メモリで約 1.6 倍の差が出る。

ただし実務上の優先順位は次のとおりである。

- **まず仮想化を確認する。** 仮想化が有効なら、`Label` と `TextBlock` のレイアウト時間に実用上の差は残らない。コントロールの置き換えより効果が大きい。
- **仮想化できない画面では `TextBlock` に寄せる。** 要素数が固定で多い画面では、コントロール選択が効く。
- **アンダーバーを含むデータを `Label` で表示する場合は `ContentTemplate` を使う。** `AccessText` が挟まると、visual が 1 個増えるだけでレイアウト時間は約 3 倍になる。
- **`Target` とアクセスキーが必要な箇所は `Label` を維持する。** フォーム見出し程度の個数であればコストは問題にならない。

「`Label` は遅いから使わない」ではなく、「仮想化が効いているか」「その画面で本当に大量に描画しているか」を先に確認したうえで、要件に応じて選ぶ。

---

<!-- 関連記事 -->
- [WPF の Label でアンダーバーが消える理由と回避方法](/ja/articles/wpf-label-underscore-issue/)
- [WPF ListBox 仮想化環境での SelectedItems が消えたように見える問題とその解決法](/ja/articles/wpf-listbox-virtualization-selecteditems/)
