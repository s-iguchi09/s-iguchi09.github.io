---
layout: article-ja
title: "WPFで編集不可のままテキストを選択・コピー可能に表示する方法"
date: 2026-05-14
category: WPF
excerpt: "WPFでTextBlockの代わりにTextBoxを使い、編集不可のままテキストを選択・コピー可能に表示する方法を解説します。"
---

## 概要

本記事では、WPF においてエラーメッセージやログなどの表示専用テキストを、編集不可のまま選択・コピー可能に表示する方法を扱う。
`TextBlock` は表示用途に適している一方、標準では部分選択やコピーを前提としたコントロールではない。
この要件に対しては、`TextBox` を読み取り専用で使用し、見た目を調整する構成が有効である。

---

## 前提・対象環境

- フレームワーク／言語: WPF / C# / XAML
- 対象コントロール・機能: `TextBlock`, `TextBox`
- アーキテクチャ: MVVM / コードビハインドのいずれでも適用可能
- 想定用途: エラーメッセージ、ログ、詳細情報の表示

---

## 問題

エラーメッセージや詳細情報を画面に表示する場面では、内容をユーザーに編集させたくない一方で、表示中のテキストをコピーしたい要件がある。
全文コピー用のボタンを別途配置する方法もあるが、実際には一部だけを選択してコピーしたいケースも多い。
このため、表示専用でありながら、テキストの選択とコピーに対応した UI が必要となる。

---

## 原因・背景

`TextBlock` は軽量な表示専用コントロールであり、静的なテキスト表示には適している。
ただし、標準の `TextBlock` は入力コントロールではないため、`TextBox` のような操作感でテキストを選択・コピーする用途には向いていない。

一方、`TextBox` は本来入力用のコントロールであるが、`IsReadOnly="True"` を設定することで編集を禁止しつつ、テキストの選択とコピーを可能にできる。
さらに、背景や枠線、キャレット表示を調整することで、見た目を `TextBlock` に近づけられる。
このため、表示用途では `TextBlock` の代替として同じように扱うことができる。

---

## 解決方法

表示専用のテキストを選択可能にしたい場合は、`TextBlock` の代わりに `TextBox` を利用する。
そのうえで、以下の設定を適用する。

- `IsReadOnly="True"`
  編集を禁止しつつ、選択とコピーを可能にする
- `IsReadOnlyCaretVisible="False"`
  読み取り専用時にキャレットを表示しない
- `Background="Transparent"`
  背景を透明にする
- `BorderThickness="0"`
  枠線を消す
- `TextWrapping="Wrap"`
  長文を折り返して表示する

この構成により、見た目は `TextBlock` に近いまま、`TextBox` の選択・コピー機能を利用できる。

---

## 実装例

以下の XAML は、エラーメッセージを編集不可かつ選択可能な状態で表示する最小構成である。
`TextBlock` の置き換え先として利用しやすいように、背景と枠線を除去し、読み取り専用時のキャレットも非表示としている。

```xml
<TextBox
    Text="{Binding ErrorMessage}"
    IsReadOnly="True"
    IsReadOnlyCaretVisible="False"
    Background="Transparent"
    BorderThickness="0"
    TextWrapping="Wrap" />
```

この設定により、表示内容は編集できないが、ユーザーは必要な範囲を選択してコピーできる。
また、見た目も `TextBlock` に近いため、既存の表示用テキストを `TextBox` に置き換える形で適用しやすい。

長文や複数行の表示を前提とする場合は、改行入力の扱いやスクロール表示も加えると実用性が高まる。
以下はその例である。

```xml
<TextBox
    Text="{Binding ErrorMessage}"
    IsReadOnly="True"
    IsReadOnlyCaretVisible="False"
    Background="Transparent"
    BorderThickness="0"
    TextWrapping="Wrap"
    AcceptsReturn="True"
    VerticalScrollBarVisibility="Auto" />
```

`AcceptsReturn="True"` を設定することで複数行テキストを自然に扱いやすくなり、`VerticalScrollBarVisibility="Auto"` により表示領域を超えた内容も確認しやすくなる。

---

## 注意点

- `TextBox` は `TextBlock` よりも入力コントロールとしての性質が強いため、既定スタイルによっては余白やフォーカス時の見た目が異なる場合がある
- `IsReadOnly="True"` のみでは、フォーカス時にキャレットが表示され、入力可能なコントロールに見えることがある
- `IsReadOnlyCaretVisible="False"` を設定すると、読み取り専用時のキャレット表示を抑制でき、表示専用の印象に近づけやすい
- デザイン上の統一が必要な場合は、`Padding` や `Focusable`、スタイル定義もあわせて調整する構成が適する

---

## 代替案・比較

| 方法 | メリット | デメリット | 適するケース |
|---|---|---|---|
| `TextBlock` をそのまま使う | 軽量で表示用途に適する | 標準では選択・コピーを前提にしにくい | 単純なラベル表示 |
| `TextBox` を読み取り専用で使う | 選択・コピーに対応でき、`TextBlock` の代替として扱いやすい | 見た目調整が必要になる | エラー表示、ログ表示、共有用テキスト表示 |
| コピーボタンを別途配置する | ワンクリックで全文コピーできる | 一部分だけのコピーには向かない | 定型メッセージや識別子の全文コピー |

---

## まとめ

WPF で、編集は不可としつつテキストの選択・コピーを可能にしたい場合は、`TextBlock` の代わりに `TextBox` を読み取り専用で利用する方法が有効である。
`IsReadOnly="True"` により編集を防ぎ、`IsReadOnlyCaretVisible="False"` により読み取り専用時のキャレット表示を抑制できる。
さらに、背景や枠線を調整することで、`TextBlock` と同じような表示用途で扱いながら、選択・コピーに対応できる。

表示専用でありながらコピー性が求められる場面では、この構成を基本パターンとして採用するのが適切である。

---

<!-- 関連記事 -->
<!-- - [WPF DataGrid の並び替えを実装する方法](/ja/articles/wpf-datagrid-sorting.html) -->
