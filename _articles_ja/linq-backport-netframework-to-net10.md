---
layout: article-ja
title: "SQL の外部結合を LINQ で表現する — LeftJoin・RightJoin・Shuffle の実装"
date: 2026-07-16
category: C#
excerpt: "SQL の LEFT JOIN / RIGHT JOIN に対応する .NET 10 の LeftJoin・RightJoin と Shuffle を .NET Framework へ実装し、GroupJoin イディオムとの対応関係、擬似シャッフルとの違い、IQueryable に適用した場合の落とし穴を解説する。"
---

## 概要

SQL では `LEFT JOIN` の 1 句で書ける外部結合が、LINQ では長らく `GroupJoin`・`SelectMany`・`DefaultIfEmpty` という 3 メソッドの合成を要した。
.NET 10 はこの長年のギャップをようやく埋め、`LeftJoin`・`RightJoin` を標準の演算子として追加した。
あわせて、`OrderBy(_ => Guid.NewGuid())` という擬似イディオムで代用されてきたランダム並べ替えも、`Shuffle` として標準化された。

本記事では、SQL の結合句と LINQ イディオムの対応関係を起点に、これら 3 演算子を .NET Framework で使えるようにするポリフィルを実装する。
さらに、外部結合という操作がデータベースクエリと地続きであることから生じる固有の論点 — `IQueryable<T>` にポリフィルを適用した場合にクエリ翻訳が壊れる問題 — を掘り下げる。

---

## 前提・対象環境

- フレームワーク: .NET Framework 4.8（バックポート先）/ .NET 10+（将来の移行先）
- 対象: LINQ の `LeftJoin`（2 シグネチャ）・`RightJoin`（2 シグネチャ）・`Shuffle`（1 シグネチャ）
- 方針: `#nullable enable` を適用し、`#if !NET10_0_OR_GREATER` で移行時に自動無効化する
- 言語バージョン: 制約なし型パラメータの `null` 許容注釈（`TInner?` / `TOuter?`）を用いるため、`LangVersion` を 9.0 以上（推奨: `latest`）に設定する。ターゲットフレームワークやその他のプロジェクト構成は変更しない

---

## SQL の結合句と LINQ イディオムの対応

.NET 10 で追加された 3 演算子は以下のとおりである。

| メソッド | 追加バージョン | 対応する操作 |
| --- | --- | --- |
| `LeftJoin<TOuter, TInner, TKey, TResult>` | .NET 10.0 | SQL の `LEFT OUTER JOIN`（外部シーケンスの全要素を残す） |
| `RightJoin<TOuter, TInner, TKey, TResult>` | .NET 10.0 | SQL の `RIGHT OUTER JOIN`（内部シーケンスの全要素を残す） |
| `Shuffle<TSource>` | .NET 10.0 | ランダムな順序への並べ替え |

.NET 9 以前の LINQ に外部結合の専用演算子はなく、`Join`（内部結合）しか存在しない。
SQL で書けば 1 句の操作が、LINQ では次の対応になっていた。

```sql
-- SQL: 一致しない社員も残す
SELECT e.Name, d.DeptName
FROM Employee e
LEFT JOIN Department d ON e.DeptId = d.DeptId
```

```csharp
// LINQ (.NET 9 以前): GroupJoin + SelectMany + DefaultIfEmpty の合成
var result = employees
    .GroupJoin(departments, e => e.DeptId, d => d.DeptId, (e, ds) => new { e, ds })
    .SelectMany(g => g.ds.DefaultIfEmpty(), (g, d) => new { g.e.Name, d?.DeptName });
```

この合成イディオムは「外部結合」という意図が構造に埋もれており、`SelectMany` と `DefaultIfEmpty` の位置を誤ると内部結合や交差結合に化ける。
ランダム並べ替えも同様で、`OrderBy(_ => Guid.NewGuid())` は全要素へのキー生成とソートを伴い、シャッフルとしての一様性も保証されない。

---

## 実装

以下は `LeftJoin`（2 シグネチャ）・`RightJoin`（2 シグネチャ）・`Shuffle`（1 シグネチャ）のポリフィル実装一式である。
`LeftJoin`・`RightJoin` は前述の合成イディオムを本家と同じ形で内包し、`Shuffle` はソースを配列へバッファリングしてから Fisher–Yates 法で並べ替える。
名前空間を `System.Linq` に置く理由と移行ガードの仕組みは[シリーズ基礎編](/ja/articles/linq-backport-netframework-to-net5/)のとおりである。
プロジェクトに `LinqExtensions.Net10.cs` などの名前でそのまま追加して使用できる。

```csharp
#nullable enable

using System;
using System.Collections.Generic;

#if !NET10_0_OR_GREATER // .NET 10.0 未満の環境（.NET Framework など）のみ有効化

namespace System.Linq
{
    /// <summary>
    /// .NET 10.0 で追加された LINQ メソッドを古いターゲットフレームワーク向けに補完する拡張メソッドを提供します。
    /// </summary>
    public static partial class LinqExtensions
    {
        // ==========================================
        // 1. LeftJoin（左外部結合）
        // ==========================================
        public static IEnumerable<TResult> LeftJoin<TOuter, TInner, TKey, TResult>(
            this IEnumerable<TOuter> outer,
            IEnumerable<TInner> inner,
            Func<TOuter, TKey> outerKeySelector,
            Func<TInner, TKey> innerKeySelector,
            Func<TOuter, TInner?, TResult> resultSelector)
            => outer.LeftJoin(inner, outerKeySelector, innerKeySelector, resultSelector, comparer: null);

        public static IEnumerable<TResult> LeftJoin<TOuter, TInner, TKey, TResult>(
            this IEnumerable<TOuter> outer,
            IEnumerable<TInner> inner,
            Func<TOuter, TKey> outerKeySelector,
            Func<TInner, TKey> innerKeySelector,
            Func<TOuter, TInner?, TResult> resultSelector,
            IEqualityComparer<TKey>? comparer)
        {
            if (outer == null) throw new ArgumentNullException(nameof(outer));
            if (inner == null) throw new ArgumentNullException(nameof(inner));
            if (outerKeySelector == null) throw new ArgumentNullException(nameof(outerKeySelector));
            if (innerKeySelector == null) throw new ArgumentNullException(nameof(innerKeySelector));
            if (resultSelector == null) throw new ArgumentNullException(nameof(resultSelector));

            // 外部要素ごとに一致する内部要素をまとめ、無ければ default(TInner) を 1 件補う。
            return outer
                .GroupJoin(inner, outerKeySelector, innerKeySelector, (o, inners) => new { o, inners }, comparer)
                .SelectMany(g => g.inners.DefaultIfEmpty(), (g, i) => resultSelector(g.o, i));
        }

        // ==========================================
        // 2. RightJoin（右外部結合）
        // ==========================================
        public static IEnumerable<TResult> RightJoin<TOuter, TInner, TKey, TResult>(
            this IEnumerable<TOuter> outer,
            IEnumerable<TInner> inner,
            Func<TOuter, TKey> outerKeySelector,
            Func<TInner, TKey> innerKeySelector,
            Func<TOuter?, TInner, TResult> resultSelector)
            => outer.RightJoin(inner, outerKeySelector, innerKeySelector, resultSelector, comparer: null);

        public static IEnumerable<TResult> RightJoin<TOuter, TInner, TKey, TResult>(
            this IEnumerable<TOuter> outer,
            IEnumerable<TInner> inner,
            Func<TOuter, TKey> outerKeySelector,
            Func<TInner, TKey> innerKeySelector,
            Func<TOuter?, TInner, TResult> resultSelector,
            IEqualityComparer<TKey>? comparer)
        {
            if (outer == null) throw new ArgumentNullException(nameof(outer));
            if (inner == null) throw new ArgumentNullException(nameof(inner));
            if (outerKeySelector == null) throw new ArgumentNullException(nameof(outerKeySelector));
            if (innerKeySelector == null) throw new ArgumentNullException(nameof(innerKeySelector));
            if (resultSelector == null) throw new ArgumentNullException(nameof(resultSelector));

            // 内部シーケンスを軸にして GroupJoin し、無ければ default(TOuter) を 1 件補う。
            return inner
                .GroupJoin(outer, innerKeySelector, outerKeySelector, (i, outers) => new { i, outers }, comparer)
                .SelectMany(g => g.outers.DefaultIfEmpty(), (g, o) => resultSelector(o, g.i));
        }

        // ==========================================
        // 3. Shuffle（ランダム並べ替え）
        // ==========================================
        public static IEnumerable<TSource> Shuffle<TSource>(this IEnumerable<TSource> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return ShuffleIterator(source);
        }

        private static IEnumerable<TSource> ShuffleIterator<TSource>(IEnumerable<TSource> source)
        {
            var buffer = source.ToArray();

            // Fisher–Yates 法で末尾から未確定要素と交換していく。
            for (int i = buffer.Length - 1; i > 0; i--)
            {
                int j = SharedRandom.Next(i + 1);
                if (j != i)
                {
                    (buffer[i], buffer[j]) = (buffer[j], buffer[i]);
                }
            }

            foreach (var item in buffer)
            {
                yield return item;
            }
        }

#if NET6_0_OR_GREATER
        // .NET 6 以降にはスレッド安全な共有インスタンスがある。
        private static Random SharedRandom => Random.Shared;
#else
        // .NET Framework には Random.Shared が無いため、スレッドごとにインスタンスを持つ。
        [ThreadStatic]
        private static Random? _threadRandom;
        private static Random SharedRandom => _threadRandom ??= new Random();
#endif
    }
}

#endif
```

`LeftJoin`・`RightJoin` の結果セレクタには、本家と一致する `null` 許容注釈（`LeftJoin` は内部要素 `TInner?`、`RightJoin` は外部要素 `TOuter?`）を付けている。
これにより「どちら側が欠けうるか」がシグネチャ上で表現され、移行前後の null 許容解析も一致する。
`Shuffle` の乱数源は入れ子の `#if NET6_0_OR_GREATER` でさらに分岐し、`Random.Shared` の無い .NET Framework では `[ThreadStatic]` なインスタンスでスレッド安全性を確保する。

---

## `LeftJoin` / `RightJoin` の使い方

### `LeftJoin`: 外部（左）の全要素を残す

一致する内部要素が無い外部要素に対しては、結果セレクタの第 2 引数へ `default(TInner)`（参照型なら `null`）が渡される。

```csharp
var employees = new[]
{
    new { Name = "佐藤", DeptId = 10 },
    new { Name = "鈴木", DeptId = 20 },
    new { Name = "高橋", DeptId = 99 }, // 対応する部署が無い
};

var departments = new[]
{
    new { DeptId = 10, DeptName = "営業部" },
    new { DeptId = 20, DeptName = "開発部" },
};

var result = employees.LeftJoin(
    departments,
    e => e.DeptId,
    d => d.DeptId,
    (e, d) => $"{e.Name}: {d?.DeptName ?? "(未所属)"}");
// 佐藤: 営業部
// 鈴木: 開発部
// 高橋: (未所属)
```

SQL の `LEFT JOIN ... ON e.DeptId = d.DeptId` に対応し、一致しない「高橋」も出力に残る。
結果セレクタの第 2 引数 `d` は `null` 許容であり、参照する前に `null` 検査が必要である。

### `RightJoin`: 内部（右）の全要素を残す

一致する外部要素が無い内部要素に対しては、結果セレクタの第 1 引数へ `default(TOuter)` が渡される。

```csharp
var employees = new[]
{
    new { Name = "佐藤", DeptId = 10 },
    new { Name = "鈴木", DeptId = 20 },
};

var departments = new[]
{
    new { DeptId = 10, DeptName = "営業部" },
    new { DeptId = 20, DeptName = "開発部" },
    new { DeptId = 30, DeptName = "総務部" }, // 所属する社員が居ない
};

var result = employees.RightJoin(
    departments,
    e => e.DeptId,
    d => d.DeptId,
    (e, d) => $"{d.DeptName}: {e?.Name ?? "(在籍なし)"}");
// 営業部: 佐藤
// 開発部: 鈴木
// 総務部: (在籍なし)
```

`RightJoin(outer, inner, ...)` は `inner` の全要素を保持する点で `LeftJoin` と対称であり、実装も `GroupJoin` の軸を内部シーケンス側に入れ替えただけである。

---

## `Shuffle` と擬似シャッフルの違い

`OrderBy(_ => Guid.NewGuid())` によるランダム並べ替えは広く使われてきたが、2 つの問題を抱える。
要素ごとに GUID を生成して $O(n \log n)$ のソートを行うため非効率であり、ソートキーとしての GUID の生成分布が並び替えの一様性を保証しない。

`Shuffle` は Fisher–Yates 法により、各順列が等確率で現れる一様なシャッフルを 1 回の走査（$O(n)$）で行う。

```csharp
var deck = Enumerable.Range(1, 52);

var shuffled = deck.Shuffle().ToArray();
// 例: [17, 3, 50, 28, ...]（呼び出しごとに異なる）
```

`Shuffle` は遅延実行だが、`OrderBy` と同様に最初の要素を返す前にソース全体をバッファリングする。

```csharp
var query = Enumerable.Range(1, 3).Shuffle();

var first = query.ToArray();  // ここでソースが列挙・並べ替えされる
var second = query.ToArray(); // 再列挙すると別の順序になる
```

同じクエリを複数回列挙すると毎回異なる順序になるため、順序を固定したい場合は `ToArray` / `ToList` で一度実体化してから使い回す。
乱数は非暗号論的な生成器を用いるため、抽選など再現不可能性が問われる用途には `System.Security.Cryptography` の乱数を別途用いる。

---

## `IQueryable<T>` に適用した場合の落とし穴

外部結合はデータベースクエリと地続きの操作であるため、このポリフィルには他のバックポートに無い固有のリスクがある。
本ポリフィルは `Enumerable`（`IEnumerable<T>`）の拡張メソッドだが、`IQueryable<T>` は `IEnumerable<T>` を継承するため、Entity Framework などの DB クエリにもコンパイル上は適用できてしまう。

その場合に起きるのは実行時エラーではなく、**静かな性能劣化**である。
`Queryable` 版が存在しないため `Enumerable` 版のポリフィルが選択され、結合が SQL へ翻訳されずにクライアント側で実行される。
テーブル全件が転送された後にメモリ上で結合される形になり、データ量が増えるまで問題が表面化しない。

.NET 10 未満で DB クエリにサーバー側の外部結合を求める場合は、プロバイダが翻訳できる `GroupJoin(...).SelectMany(..., DefaultIfEmpty())` の形で記述する。
`AsEnumerable` はクライアント評価の境界を引くだけで、結合をサーバー側に保つ効果はない。
なお .NET 10 では `Queryable` にも `LeftJoin`・`RightJoin` が追加されているが、その翻訳可否はプロバイダに依存し、本記事の対象は `Enumerable` に限る。

---

## 移行ガード

本ポリフィルは `#if !NET10_0_OR_GREATER` で囲む。
`LeftJoin`・`RightJoin`・`Shuffle` は .NET 9 以前に存在しないため、`!NETCOREAPP` や `!NET9_0_OR_GREATER` を条件にすると .NET 9 向けビルドでポリフィルが無効化され、コンパイルエラーになる（`NET9_0_OR_GREATER` は .NET 9 以降でのみ定義されるため、`.NET 8` では未定義となりポリフィルは有効なままとなる）。
シンボル選択の一般規則（追加されたバージョン以上で無効化する）は[.NET 6 メソッドのバックポート記事](/ja/articles/linq-backport-netframework-to-net6/)で整理している。

---

## 注意点

- **結合方向の対応関係**: `LeftJoin` は第 1 引数（`outer`）の全要素を、`RightJoin` は第 2 引数（`inner`）の全要素を保持する。結果セレクタで `null` 許容になるのは、`LeftJoin` では内部要素、`RightJoin` では外部要素である。参照する前に `null` 検査を行う。
- **キーの等価比較**: `comparer` を渡さないオーバーロードは `EqualityComparer<TKey>.Default` を使う。大文字・小文字を区別しない結合などが必要な場合は `IEqualityComparer<TKey>` を渡すオーバーロードを用いる。パラメータの少ないオーバーロードから委譲する際は、`comparer:` の名前付き引数で解決先を固定している（この技法は [ToDictionary のバックポート記事](/ja/articles/linq-backport-netframework-to-net8/)でも使っている）。
- **`Shuffle` は無限シーケンスに使えない**: 列挙開始時にソース全体をバッファリングするため、終端のないシーケンスに適用すると停止しない。
- **C# 9 以上でコンパイルする**: 制約なし型パラメータの `null` 許容注釈（`TInner?` / `TOuter?`）を用いるため、.NET Framework 4.8 の既定 `LangVersion`（7.3）のままでは `CS8627` などでコンパイルできない。`.csproj` に `<LangVersion>9.0</LangVersion>`（または `latest`）を指定する。
- **名前衝突は起きない**: これらは本家 .NET Framework に存在しないシグネチャであり、既存の `Join` や `OrderBy` とはメソッド名・引数が異なるため、オーバーロード解決で衝突しない。

---

## 代替案・比較

| 方法 | メリット | デメリット | 適するケース |
| --- | --- | --- | --- |
| 自作ポリフィル（本記事） | 外部依存なし・SQL に対応する意図が名前で伝わる | `IQueryable` への誤適用リスクの管理が必要 | インメモリの結合・乱択が中心の場合 |
| `GroupJoin` + `DefaultIfEmpty` を直書き | 追加コード不要・`IQueryable` でも翻訳される | 記述が冗長・意図を誤りやすい | DB クエリの外部結合 |
| `OrderBy(_ => Guid.NewGuid())` で代用 | 追加コード不要 | 非効率で一様性が保証されない | 少数要素の並べ替えで厳密さを問わない場合 |
| MoreLINQ などのライブラリ導入 | 実装済みで検証されている | 外部依存が増える・API が本家と異なる | 既に依存を許容している場合 |
| .NET 10 へのアップグレード | 根本的解決・`Queryable` 版も使える | 移行コストが発生する | 移行が技術的・ビジネス的に許容できる場合 |

インメモリのコレクション操作にはポリフィルを、DB クエリには従来の `GroupJoin` イディオムを使い分けるのが、.NET 10 移行までの現実的な構成である。

---

## まとめ

.NET 10 の `LeftJoin`・`RightJoin`・`Shuffle` は、SQL では標準だった操作と、擬似イディオムで代用されてきた操作を LINQ の第一級の演算子に引き上げた。
バックポートの要点は次の 3 つである。

- `LeftJoin` / `RightJoin` は従来の `GroupJoin` + `SelectMany` + `DefaultIfEmpty` イディオムを内包し、`null` 許容注釈で「欠けうる側」をシグネチャに表現する
- `Shuffle` は Fisher–Yates 法で一様なシャッフルを行い、`Guid.NewGuid()` ソートの非効率と偏りを解消する
- ポリフィルは `Enumerable` 専用であり、`IQueryable<T>` の DB クエリに適用するとクライアント評価に落ちる。DB クエリでは従来イディオムを維持する

| メソッド | 保持される側 | `null` 許容になる引数 | 評価 |
| --- | --- | --- | --- |
| `LeftJoin` | 外部（左） | 内部要素 `TInner?` | 遅延 |
| `RightJoin` | 内部（右） | 外部要素 `TOuter?` | 遅延 |
| `Shuffle` | — | — | 遅延（列挙時に全バッファリング） |

---

## 関連記事

- [遅延評価を壊さない LINQ ポリフィルの設計原則 — Append・Prepend・TakeLast・SkipLast の実装](/ja/articles/linq-backport-netframework-to-net5/)
- [GroupBy と全件ソートによる回避コードをなくす — Chunk・MaxBy・MinBy・DistinctBy の実装](/ja/articles/linq-backport-netframework-to-net6/)
- [委譲だけで作る Order・OrderDescending — IOrderedEnumerable 互換の最小ポリフィル](/ja/articles/linq-backport-netframework-to-net7/)
- [セレクタなし ToDictionary の実現 — オーバーロード解決と notnull 制約の設計](/ja/articles/linq-backport-netframework-to-net8/)
- [GroupBy を経由しないキー集計 — CountBy・AggregateBy・Index の辞書ベース実装](/ja/articles/linq-backport-netframework-to-net9/)
