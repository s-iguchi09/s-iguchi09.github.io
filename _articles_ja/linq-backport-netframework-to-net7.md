---
layout: article-ja
title: ".NET Framework の不足 LINQ メソッドを .NET 7 相当にバックポートする"
date: 2026-07-14
category: C#
excerpt: ".NET 7 で追加された LINQ の Order・OrderDescending を、条件付きコンパイルと IOrderedEnumerable の維持を意識しながら .NET Framework 環境へ安全にバックポートする実装方法を解説する。"
---

## 概要

.NET Framework から新世代 .NET（.NET 7 以降）への移行を段階的に進めている場合や、諸事情で .NET Framework 環境のコードをメンテナンスし続けなければならない場合、ストレスの原因となるのが「新世代 .NET にはあるのに、.NET Framework には存在しない LINQ メソッド」の存在である。

本記事では、**.NET 7 で新たに追加された 2 つの LINQ メソッド**（`Order`・`OrderDescending`）を整理し、**同一の使用感で動作する拡張メソッド（ポリフィル）を安全に実装する方法**を解説する。
戻り値型 `IOrderedEnumerable<T>` を保った実装と、将来の .NET 7 以降への移行時にコードを無修正で切り替えるための条件付きコンパイル手法も合わせて紹介する。

---

## 前提・対象環境

- フレームワーク: .NET Framework 4.8 / .NET 7+
- 対象: LINQ の 2 メソッド（Order / OrderDescending）とそれぞれの `IComparer<T>` オーバーロード
- 方針: `#nullable enable` を適用し、`#if !NET7_0_OR_GREATER` による条件付きコンパイルで移行時に自動無効化する
- プロジェクト設定（`.csproj` の言語バージョンなど）は変更しない

---

## 問題

.NET 7 で追加された以下の LINQ メソッドは、.NET Framework 環境では使用できない。

| メソッド名 | 追加されたバージョン | 概要 |
| --- | --- | --- |
| `Order<T>` | .NET 7.0 | シーケンスの要素自体を基準に昇順で並び替える |
| `OrderDescending<T>` | .NET 7.0 | シーケンスの要素自体を基準に降順で並び替える |

これらが存在しない .NET Framework 環境では、値そのもので並び替えるだけの場面でも、恒等ラムダを明示的に書く代替実装を強いられる。

- `Order()` の代わりに、`OrderBy(x => x)` と自分自身を返すラムダを書く
- `OrderDescending()` の代わりに、`OrderByDescending(x => x)` と書く

`x => x` はソートの本質ではない定型記述であり、記述量が増えるほど可読性を下げ、キーセレクタの取り違えといった軽微なミスの温床にもなる。

---

## 原因・背景

.NET 6 で `Chunk`・`MaxBy`・`MinBy`・`DistinctBy` といったコレクション操作メソッドが一挙に追加された後、続く .NET 7 では「要素自身を並び替え対象にする」メソッドが追加された。
`Order`・`OrderDescending` はいずれも .NET 7.0 で初めて追加されたものであり、.NET 6 以前の環境では存在しない。

`OrderBy(x => x)` という冗長なイディオムは長く定着していたが、キーセレクタが不要なケースが頻繁に存在することから、恒等関数を内包する専用メソッドとして標準化された。

なお、.NET Framework から .NET 5 の間に追加されたメソッド（`Append`・`Prepend`・`TakeLast`・`SkipLast`）については[別記事](/ja/articles/linq-backport-netframework-to-net5/)で、.NET 6 で追加された 4 メソッドについては[別記事](/ja/articles/linq-backport-netframework-to-net6/)で解説している。

---

## 解決方法

本家 LINQ と同じ名前空間（`System.Linq`）に拡張メソッドを定義することで、既存のソースファイルに手を加えることなく透過的に利用できる。

条件付きコンパイル `#if !NET7_0_OR_GREATER` を使い、.NET 7 以降の環境ではこのファイルを丸ごとスキップするよう仕込む。
将来のフレームワークアップグレード時に、ファイルの削除やコードの書き換えを行わずに自動的に本家 LINQ へ切り替わる。

実装の要点は、戻り値型を `IEnumerable<T>` ではなく `IOrderedEnumerable<T>` にすることである。
本家 `Order` は `IOrderedEnumerable<T>` を返すため、後続の `ThenBy` を連結できる。
戻り値を `IEnumerable<T>` に落とすと `ThenBy` が呼べなくなり、本家との互換性が失われる。

---

## 実装例

以下は 2 メソッド（各 `IComparer<T>` オーバーロードを含む計 4 シグネチャ）のポリフィル実装一式である。
実装は内部で `OrderBy` / `OrderByDescending` に恒等ラムダを渡すだけであり、並び替えの安定性やカルチャ依存の比較挙動も本家と同一になる。
プロジェクトに `LinqExtensions.Net7.cs` などの名前でそのまま追加して使用できる。

```csharp
#nullable enable

using System;
using System.Collections.Generic;

#if !NET7_0_OR_GREATER // .NET 7.0 以降ではない環境（.NET Framework など）のみ有効化

namespace System.Linq
{
    /// <summary>
    /// .NET 7.0 で追加された LINQ メソッドを古いターゲットフレームワーク向けに補完する拡張メソッドを提供します。
    /// </summary>
    public static partial class LinqExtensions
    {
        // ==========================================
        // 1. Order
        // ==========================================
        public static IOrderedEnumerable<TSource> Order<TSource>(this IEnumerable<TSource> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            return source.OrderBy(x => x);
        }

        public static IOrderedEnumerable<TSource> Order<TSource>(this IEnumerable<TSource> source, IComparer<TSource>? comparer)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            return source.OrderBy(x => x, comparer);
        }

        // ==========================================
        // 2. OrderDescending
        // ==========================================
        public static IOrderedEnumerable<TSource> OrderDescending<TSource>(this IEnumerable<TSource> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            return source.OrderByDescending(x => x);
        }

        public static IOrderedEnumerable<TSource> OrderDescending<TSource>(this IEnumerable<TSource> source, IComparer<TSource>? comparer)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            return source.OrderByDescending(x => x, comparer);
        }
    }
}

#endif
```

コンパイル時に `NET7_0_OR_GREATER` シンボルが定義されていない環境（.NET Framework を含む .NET 7 未満の環境）でのみ、上記クラスが有効になる。

---

## 各メソッドの詳解

### `Order<T>` / `OrderDescending<T>`（要素自体を基準にした並び替え）

`Order` はシーケンスの要素そのものを基準に昇順で並び替える。
`OrderBy(x => x)` と等価だが、キーセレクタを書かずに意図を表現できる。

```csharp
var numbers = new[] { 3, 1, 4, 1, 5, 9, 2, 6 };

var ascending = numbers.Order();
// ascending: 1, 1, 2, 3, 4, 5, 6, 9

var descending = numbers.OrderDescending();
// descending: 9, 6, 5, 4, 3, 2, 1, 1
```

引数なしオーバーロードは要素型の既定の比較子（`Comparer<T>.Default`）を用いるため、`int` や `string` のように順序が定義された型はそのまま並び替えられる。

`IComparer<T>` を受け取るオーバーロードでは、比較ロジックを差し替えられる。
以下は文字列を大文字・小文字を区別せずに並び替える例である。

```csharp
var words = new[] { "banana", "Apple", "cherry" };

var sorted = words.Order(StringComparer.OrdinalIgnoreCase);
// sorted: Apple, banana, cherry
```

比較子を差し替えても戻り値型は変わらないため、後述の `ThenBy` 連結と組み合わせられる。

### `IOrderedEnumerable<T>` を返すことによる `ThenBy` の連結

戻り値を `IOrderedEnumerable<T>` にすることで、第 2 ソートキーを `ThenBy` / `ThenByDescending` で追加できる。

```csharp
var words = new[] { "Banana", "apple", "banana", "Apple" };

// まず大文字・小文字を無視して昇順、綴りが同じ要素は序数比較で並び替える
var result = words.Order(StringComparer.OrdinalIgnoreCase)
                  .ThenBy(s => s, StringComparer.Ordinal);
// result: Apple, apple, Banana, banana
```

第 1 キー（大文字小文字を無視した比較）では `"Apple"` と `"apple"`、`"Banana"` と `"banana"` がそれぞれ同順になるため、第 2 キー（序数比較）が同順要素の並びを決める。
戻り値を `IEnumerable<T>` に落とした実装では、上記の `ThenBy` はコンパイルエラーになる。
本家 API との互換性を保つうえで、戻り値型の選択は重要である。

---

## 条件付きコンパイルシンボルの選択

本実装では `#if !NET7_0_OR_GREATER` を採用している。
[.NET 5 相当のバックポート記事](/ja/articles/linq-backport-netframework-to-net5/)が `#if !NETCOREAPP` を採用しているのとは異なる。

`Order`・`OrderDescending` は .NET 6 以前には存在しないため、`NETCOREAPP` や `NET6_0_OR_GREATER` を条件に使うと、.NET 5 や .NET 6 向けビルドでポリフィルが無効化され、コンパイルエラーが発生する。

| シンボル | .NET Framework | .NET 6 | .NET 7+ |
| --- | --- | --- | --- |
| `!NETCOREAPP` | ポリフィル有効 | **ポリフィル無効（エラー）** | ポリフィル無効 |
| `!NET6_0_OR_GREATER` | ポリフィル有効 | **ポリフィル無効（エラー）** | ポリフィル無効 |
| `!NET7_0_OR_GREATER` | ポリフィル有効 | ポリフィル有効 | ポリフィル無効 |

`!NET7_0_OR_GREATER` を使うことで、.NET 6 を含めた .NET 7 未満の環境すべてでポリフィルが有効になり、.NET 7 以降では自動的に本家 LINQ へ切り替わる。

---

## 注意点

- **戻り値型は `IOrderedEnumerable<T>` にする**: `IEnumerable<T>` を返す実装は動作こそするが、`ThenBy` / `ThenByDescending` を連結できず本家と非互換になる。恒等ラムダを渡す `OrderBy` / `OrderByDescending` はそのまま `IOrderedEnumerable<T>` を返すため、戻り値型を明示するだけで互換性を維持できる。
- **要素型に順序が定義されている必要がある**: 引数なしオーバーロードは `Comparer<T>.Default` を使う。要素型が `IComparable<T>` を実装しておらず、既定の比較子も解決できない場合、列挙時に `ArgumentException`（メッセージ: "At least one object must implement IComparable."）が発生する。任意の型を並び替える場合は `IComparer<T>` オーバーロードを使う。
- **遅延評価である**: `Order` / `OrderDescending` は `OrderBy` と同じく遅延評価であり、`foreach` や `.ToList()` の時点で初めてソートが実行される。`source` が `null` の場合の `ArgumentNullException` は呼び出し時点で即座に投げられる（本メソッドは `yield return` を含まないため）。
- **並び替えは安定ソート**: 内部の `OrderBy` が安定ソートであるため、同一キーの要素は入力順を保つ。この挙動は本家 `Order` と一致する。

---

## 代替案・比較

| 方法 | メリット | デメリット | 適するケース |
| --- | --- | --- | --- |
| 自作ポリフィル（本記事） | 外部依存なし・戻り値型を本家に合わせられる | 実装・保守の手間がある | 依存を最小化したいプロジェクト |
| `OrderBy(x => x)` を直書き | 追加コード不要 | 記述が冗長・`Order` への移行時に一括置換が必要 | 使用箇所が少なく移行予定もない場合 |
| .NET 7 へのアップグレード | 根本的解決・言語機能も享受できる | 移行コストが発生する | 移行が技術的・ビジネス的に許容できる場合 |

`OrderBy(x => x)` の直書きは追加コードこそ不要だが、後日 .NET 7 へ移行して `Order` に統一する際に使用箇所を洗い出して置換する手間が残る。
本記事のポリフィルを導入しておけば、移行前から `Order` で記述でき、移行時にはファイルを残したまま条件付きコンパイルが自動で本家へ切り替える。

---

## まとめ

.NET 7 で追加された `Order`・`OrderDescending` の 2 メソッドと、.NET Framework 環境へのバックポート手法を解説した。

実装において重要なポイントは以下の 3 点である。

- **戻り値型を `IOrderedEnumerable<T>` にする**: 本家と同じく `ThenBy` を連結できるようにするため、`IEnumerable<T>` ではなく `IOrderedEnumerable<T>` を返す。
- **`#if !NET7_0_OR_GREATER` を選択する**: .NET 6 以前にはこれらのメソッドが存在しないため、`!NETCOREAPP` や `!NET6_0_OR_GREATER` では .NET 6 環境でエラーになる。
- **既定の比較子に依存する場合の例外に注意する**: 引数なしオーバーロードは要素型の順序が定義されていることを前提とする。任意型では `IComparer<T>` オーバーロードを使う。

| メソッド | 評価戦略 | 戻り値型 | 実装の要点 |
| --- | --- | --- | --- |
| `Order` | 遅延 | `IOrderedEnumerable<T>` | `OrderBy(x => x)` に委譲 |
| `OrderDescending` | 遅延 | `IOrderedEnumerable<T>` | `OrderByDescending(x => x)` に委譲 |

---

## 関連記事

- [.NET Framework の不足 LINQ メソッドを .NET 6 相当にバックポートする](/ja/articles/linq-backport-netframework-to-net6/)
- [.NET Framework の不足 LINQ メソッドを .NET 5 相当にバックポートする](/ja/articles/linq-backport-netframework-to-net5/)
