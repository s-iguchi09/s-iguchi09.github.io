---
layout: article-ja
title: "委譲だけで作る Order・OrderDescending — IOrderedEnumerable 互換の最小ポリフィル"
date: 2026-07-14
category: C#
excerpt: "既存の OrderBy への委譲だけで完結する Order・OrderDescending のポリフィルを題材に、戻り値型 IOrderedEnumerable が決める ThenBy 互換性と、環境によって異なるソート時例外という 2 つの互換性論点を解説する。"
---

## 概要

.NET 7 で追加された `Order`・`OrderDescending` のポリフィルは、実装本体が各メソッド 1 行で書ける。
イテレータも `Queue<T>` も不要で、既存の `OrderBy` / `OrderByDescending` に恒等ラムダを渡して委譲するだけである。

実装が単純なだけに、このポリフィルの正否を分けるのはアルゴリズムではなく**互換性の詰め**である。
本記事では、委譲型ポリフィルの実装を示したうえで、互換性を左右する 2 つの論点を解説する。

- 戻り値型を `IOrderedEnumerable<T>` にしないと `ThenBy` が連結できず、本家と非互換になる
- 既定比較子で並び替えられない型に対する例外は、.NET Framework と .NET Core 以降で型が異なる

「本家 API への委譲だけで作れるポリフィル」は、イテレータを自作する型（[基礎編](/ja/articles/linq-backport-netframework-to-net5/)参照）と並ぶもう 1 つの実装パターンであり、その代表例として読める構成にしている。

---

## 前提・対象環境

- フレームワーク: .NET Framework 4.8（バックポート先）/ .NET 7+（将来の移行先）
- 対象: LINQ の `Order` / `OrderDescending`（各 `IComparer<T>` オーバーロードを含む計 4 シグネチャ）
- 方針: `#nullable enable` を適用し、`#if !NET7_0_OR_GREATER` で移行時に自動無効化する
- プロジェクト設定（`.csproj` の言語バージョンなど）は変更しない

---

## 恒等ラムダ `x => x` の定型句

`Order` / `OrderDescending` は、シーケンスの要素そのものを基準に並び替える専用メソッドである。

| メソッド名 | 追加されたバージョン | 概要 |
| --- | --- | --- |
| `Order<T>` | .NET 7.0 | シーケンスの要素自体を基準に昇順で並び替える |
| `OrderDescending<T>` | .NET 7.0 | シーケンスの要素自体を基準に降順で並び替える |

これらが無い .NET Framework 環境では、値そのもので並び替えるだけの場面でも `OrderBy(x => x)` / `OrderByDescending(x => x)` と恒等ラムダを書き続けることになる。
`x => x` はソートの意図とは無関係な定型記述であり、キーセレクタの取り違えといった軽微なミスの混入点にもなる。
逆に言えば、恒等関数を内包した専用メソッドがあれば済む話であり、.NET 7 はそれを標準化した。

---

## 委譲による実装

以下は 4 シグネチャのポリフィル実装一式である。
実装は内部で `OrderBy` / `OrderByDescending` に恒等ラムダを渡すだけであり、並び替えの安定性やカルチャ依存の比較挙動は本家と同一になる。
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

委譲型では `yield return` を書かないため、イテレータ自作型で必要だった「引数検証とイテレータの分離」（[基礎編の設計原則 1](/ja/articles/linq-backport-netframework-to-net5/)）は考えなくてよい。
`source` の null チェックは呼び出し時点で即座に実行され、並び替え自体は委譲先の `OrderBy` が持つ遅延評価のまま動く。
検証済みの既存 API に処理を委ねるため、アルゴリズム起因のバグが入り込む余地がない。

基本の使い方は次のとおりである。

```csharp
var numbers = new[] { 3, 1, 4, 1, 5, 9, 2, 6 };

var ascending = numbers.Order();
// ascending: 1, 1, 2, 3, 4, 5, 6, 9

var descending = numbers.OrderDescending();
// descending: 9, 6, 5, 4, 3, 2, 1, 1
```

引数なしオーバーロードは要素型の既定比較子（`Comparer<T>.Default`）を使うため、`int` や `string` のように順序が定義された型はそのまま並び替えられる。
`IComparer<T>` を受け取るオーバーロードでは比較ロジックを差し替えられる。

```csharp
var words = new[] { "banana", "Apple", "cherry" };

var sorted = words.Order(StringComparer.OrdinalIgnoreCase);
// sorted: Apple, banana, cherry
```

---

## 互換性論点 1: 戻り値型 `IOrderedEnumerable<T>` が `ThenBy` 連結を決める

委譲型ポリフィルで唯一設計判断が要るのが戻り値型である。
`IEnumerable<T>` を返しても並び替え結果自体は同じだが、本家 `Order` は `IOrderedEnumerable<T>` を返すため、第 2 ソートキーを `ThenBy` / `ThenByDescending` で連結できる。

```csharp
var words = new[] { "Banana", "apple", "banana", "Apple" };

// まず大文字・小文字を無視して昇順、綴りが同じ要素は序数比較で並び替える
var result = words.Order(StringComparer.OrdinalIgnoreCase)
                  .ThenBy(s => s, StringComparer.Ordinal);
// result: Apple, apple, Banana, banana
```

第 1 キー（大文字小文字を無視した比較）では `"Apple"` と `"apple"`、`"Banana"` と `"banana"` がそれぞれ同順になるため、第 2 キー（序数比較）が同順要素の並びを決める。
戻り値を `IEnumerable<T>` に落とした実装では、この `ThenBy` がコンパイルエラーになる。

委譲先の `OrderBy` はもともと `IOrderedEnumerable<T>` を返すので、ポリフィル側ですべきことは「戻り値型を `IEnumerable<T>` に狭めない」ことだけである。
委譲型ポリフィル全般に言える教訓として、**委譲先が返す型をそのまま公開し、情報を落とさない**ことが本家互換の条件になる。

---

## 互換性論点 2: 環境によって異なるソート時例外

引数なしオーバーロードは `Comparer<T>.Default` に依存するため、要素型が `IComparable` / `IComparable<T>` を実装していない場合、列挙（ソート実行）時に例外が発生する。
このとき表面化する例外の型が、実行環境によって異なる。

- 既定比較子自体は `ArgumentException`（メッセージ: "At least one object must implement IComparable."）を投げる。
- **.NET Framework** の `OrderBy` は内部ソートで比較例外をラップしないため、この `ArgumentException` がそのまま伝播する。
- **.NET Core 3.0 以降**（本家 .NET 7 の `Order` を含む）は、内部ソートが比較例外を `InvalidOperationException`（メッセージ: "Failed to compare two elements in the array."、内側例外が上記 `ArgumentException`）にラップする。

つまり、本ポリフィルは .NET Framework 上で動く限り本家 .NET 7 と例外型が一致しない。
これは委譲型ポリフィルの構造的な限界である。
委譲先（.NET Framework の `OrderBy`）の挙動がそのまま出てくるため、委譲先自体が本家と異なる部分は再現できない。

例外型でハンドリングしているコードを移行する場合はこの差異に注意し、そもそも任意の型を並び替える場合は `IComparer<T>` オーバーロードで比較を明示しておくのが安全である。

---

## 移行ガード

本ポリフィルは `#if !NET7_0_OR_GREATER` で囲む。
`Order` / `OrderDescending` は .NET 6 以前に存在しないため、`!NETCOREAPP` や `!NET6_0_OR_GREATER` を条件にすると .NET 5 / .NET 6 向けビルドでポリフィルが無効化され、コンパイルエラーになる。
「対象メソッドが追加されたバージョン以上で無効化する」というシンボル選択の一般規則は、[.NET 6 メソッドのバックポート記事](/ja/articles/linq-backport-netframework-to-net6/)で整理している。

---

## 注意点

- **並び替えは安定ソート**: 内部の `OrderBy` が安定ソートであるため、同一キーの要素は入力順を保つ。この挙動は本家 `Order` と一致する。
- **遅延評価である**: `Order` / `OrderDescending` は `OrderBy` と同じく遅延評価であり、`foreach` や `.ToList()` の時点で初めてソートが実行される。`source` が `null` の場合の `ArgumentNullException` は呼び出し時点で即座に投げられる。
- **既定比較子に依存しない書き方を優先する**: 前述のとおり、順序が定義されていない型への引数なしオーバーロードは実行時例外になる。任意の型を扱う場面では `IComparer<T>` オーバーロードで比較を明示する。

---

## 代替案・比較

| 方法 | メリット | デメリット | 適するケース |
| --- | --- | --- | --- |
| 委譲型ポリフィル（本記事） | 実装が数行・アルゴリズム起因のバグが入らない | 委譲先と本家の挙動差（例外型）は再現できない | `Order` の記法を移行前から使いたい場合 |
| `OrderBy(x => x)` を直書き | 追加コード不要 | 記述が冗長・.NET 7 移行時に一括置換が必要 | 使用箇所が少なく移行予定もない場合 |
| .NET 7 へのアップグレード | 根本的解決・言語機能も享受できる | 移行コストが発生する | 移行が技術的・ビジネス的に許容できる場合 |

`OrderBy(x => x)` の直書きは動作上の問題こそないが、後日 .NET 7 へ移行して `Order` に統一する際、使用箇所を洗い出して置換する手間が残る。
ポリフィルを導入しておけば移行前から `Order` で記述でき、移行時には条件付きコンパイルが自動で本家へ切り替える。

---

## まとめ

`Order`・`OrderDescending` のポリフィルは、既存 API への委譲だけで完結する最小構成の実装である。
そのぶん品質を決めるのは互換性の詰めであり、押さえるべき点は 2 つに集約される。

| 論点 | 判断 |
| --- | --- |
| 戻り値型 | `IOrderedEnumerable<T>` を維持し、`ThenBy` 連結の互換性を確保する |
| ソート時例外 | .NET Framework 上では本家と例外型が異なる。比較を `IComparer<T>` で明示して回避する |

イテレータを自作するポリフィル（[基礎編](/ja/articles/linq-backport-netframework-to-net5/)）と本記事の委譲型のどちらを選ぶかは、バックポート対象と同じ処理が既存 API の組み合わせで表現できるかで決まる。
表現できるなら委譲型のほうが安全で、表現できないときだけイテレータを書く。

---

## 関連記事

- [遅延評価を壊さない LINQ ポリフィルの設計原則 — Append・Prepend・TakeLast・SkipLast の実装](/ja/articles/linq-backport-netframework-to-net5/)
- [GroupBy と全件ソートによる回避コードをなくす — Chunk・MaxBy・MinBy・DistinctBy の実装](/ja/articles/linq-backport-netframework-to-net6/)
- [セレクタなし ToDictionary の実現 — オーバーロード解決と notnull 制約の設計](/ja/articles/linq-backport-netframework-to-net8/)
- [GroupBy を経由しないキー集計 — CountBy・AggregateBy・Index の辞書ベース実装](/ja/articles/linq-backport-netframework-to-net9/)
- [SQL の外部結合を LINQ で表現する — LeftJoin・RightJoin・Shuffle の実装](/ja/articles/linq-backport-netframework-to-net10/)
