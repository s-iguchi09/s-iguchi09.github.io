---
layout: article-ja
title: "WPF で Label を大量配置すると遅い原因と TextBlock への置き換え指針"
date: 2026-06-10
category: WPF
excerpt: "WPF で Label を大量配置した際に描画が遅くなる原因を整理し、TextBlock との違い、使い分け、置き換え時の注意点を実装例付きでまとめる。"
image: /images/articles/wpf-label-vs-textblock-performance/label-vs-textblock-measurement.png
---

## 概要

WPF で `Label` を大量に配置した画面において、初期描画とスクロール時の応答が低下する事象を確認した。  
本記事では、`Label` と `TextBlock` の構造差に基づいて原因を整理し、性能を優先する画面での実装方針を示す。  

---

## 前提・対象環境

- フレームワーク／言語: .NET 8 / C# 12 / WPF
- 対象コントロール: `Label`、`TextBlock`
- アーキテクチャ: MVVM（コードビハインドでも同様）
- 想定画面: 一覧・ダッシュボードなどテキスト要素を多数表示する画面

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

この差は実際に計測できる。  
同じ文字列を 1,000 個並べた `StackPanel` について、レイアウト完了後の visual ツリーの要素数と、`Measure` から `UpdateLayout` までに要した時間を測ると次のようになる。  

<figure class="article-figure">
  <img src="/images/articles/wpf-label-vs-textblock-performance/label-vs-textblock-measurement.png" alt="計測結果の表。Label を 1000 個並べた場合は visual 要素が 4,002 個でレイアウトに 385.3 ミリ秒、TextBlock では 1,002 個で 153.9 ミリ秒。" width="415" height="160" loading="lazy">
  <figcaption>.NET 10 / Windows 11、既定テーマ（Aero2）の <code>ControlTemplate</code> で、<code>Content</code> に文字列を与えた場合の実測値。この条件では <code>Label</code> 1 個あたり 4 個の visual が構築されるのに対し、<code>TextBlock</code> は 1 個で済む。visual 数はテーマや <code>ControlTemplate</code> の差し替えで変わり、所要時間は実行環境に依存するため、絶対値ではなく比率を目安として読む。</figcaption>
</figure>

要素数がおよそ 4 倍になる点が本質である。  
既定テンプレートでは `Label` 1 個につき `Border`・`ContentPresenter`・実際に文字を描く `TextBlock` が追加で構築されるため、測定・配置・描画の各段階で処理対象が増える。  
なお `Content` にアクセスキー（`_`）を含む文字列を与えた場合は、`ContentPresenter` が `AccessText` を挟むため visual は 5 個になる。  
上の計測はアクセスキーを含まない文字列で行っている。  

---

## 解決方法

解決方針は、用途を「表示専用」か「入力連携付きラベル」かで分離することである。  

- 表示専用テキスト: 原則 `TextBlock` を採用する。  
- 入力フォーム見出し: `Target` やアクセスキーが必要な箇所のみ `Label` を使用する。  
- 既存画面の改修: `Label` を一括で置換せず、要件を満たす範囲で段階的に `TextBlock` 化する。  

---

## 実装例

まず、単純表示用途の `Label` を `TextBlock` に置き換える例を示す。  
この置き換えは、見た目を維持しつつ描画コストを削減する目的で実施する。  

```xml
<!-- 置き換え前 -->
<Label Content="ステータス: 実行中" />

<!-- 置き換え後 -->
<TextBlock Text="ステータス: 実行中" />
```

この変更により、表示専用箇所から `Label` 固有機能を外し、軽量な描画構成に統一できる。  
ただし `Label` の既定 `Padding` がなくなるため、必要に応じて `Margin` や `Padding` を明示する。  

次に、`Label` が必要な入力フォーム見出しの例を示す。  
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

---

## 注意点

- `Label` から `TextBlock` へ置き換えると、アクセスキー（`_`）と `Target` によるフォーカス連携は失われる。  
- 長文表示では、`TextBlock` 側で `TextWrapping="Wrap"` や `TextTrimming` を明示しないと意図した表示にならない場合がある。  
- 既存 UI では `Label` 既定余白に依存していることがあるため、置き換え後の行間と整列を確認する必要がある。  
- `DataGrid` や `ItemsControl` のテンプレート内で多数描画する場合は、仮想化設定と併せて評価することが重要である。  

---

## 代替案・比較

| 方法 | メリット | デメリット | 適するケース |
| --- | --- | --- | --- |
| すべて `Label` のまま維持 | アクセスキーや `Target` を既存仕様どおり保持できる | 大量表示時の描画コストが高い | 入力フォーム中心で要素数が少ない画面 |
| 表示専用箇所を `TextBlock` へ置換 | 描画とメモリ負荷を抑えやすい | 余白・折り返し設定の見直しが必要 | 一覧・監視画面などテキスト表示が多い画面 |
| 画面全体を `TextBlock` 化 | 単純化しやすく軽量 | `Label` 固有の操作性を失う | 入力連携が不要な読み取り専用画面 |

---

## まとめ

WPF で `Label` を大量配置した場合の遅延要因は、`ContentControl` としての汎用機能に起因する描画オーバーヘッドである。  
性能を優先する画面では、表示専用テキストを `TextBlock` に寄せる構成が有効である。  

実装判断の基準は以下となる。  

- `Target` とアクセスキーが必要な箇所は `Label` を維持する。  
- 単純な文字表示は `TextBlock` を採用する。  
- 大量描画領域では、コントロール選択に加えて仮想化とレイアウトの再評価を実施する。  

この基準により、操作性を維持しながら描画性能の劣化を抑制できる。  

---

<!-- 関連記事 -->
- [WPF の Label でアンダーバーが消える理由と回避方法](/ja/articles/wpf-label-underscore-issue)
