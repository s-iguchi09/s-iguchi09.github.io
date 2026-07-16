---
layout: article-ja
title: ".NET Framework の不足 LINQ メソッドを .NET 10 相当にバックポートする"
date: 2026-07-17
category: C#
excerpt: ".NET 10 で追加された LeftJoin・RightJoin・Shuffle を、#nullable enable と条件付きコンパイルを用いて .NET Framework へ拡張メソッドで実装し、本家と同じ使用感で利用する方法を解説する。"
---

## 概要

.NET Framework から新世代 .NET への移行を段階的に進めている場合や、諸事情で .NET Framework 環境のコードをメンテナンスし続けなければならない場合、ストレスの原因となるのが「新世代 .NET にはあるのに、.NET Framework には存在しない LINQ メソッド」の存在である。

本記事では、**.NET 10 で新たに追加された 3 つの LINQ 演算子**（外部結合を行う `LeftJoin`・`RightJoin`、およびシーケンスをランダム順に並べ替える `Shuffle`）を整理し、**本家と同じ使用感で動作する拡張メソッド（ポリフィル）を安全に実装する方法**を解説する。
`#nullable enable` を使った現代的な実装と、将来の .NET 10 以降への移行時にコードを無修正で切り替えるための条件付きコンパイル手法も合わせて紹介する。

---

## 前提・対象環境

- フレームワーク: .NET Framework 4.8 / .NET 10+
- 対象: LINQ の `LeftJoin`（2 シグネチャ）・`RightJoin`（2 シグネチャ）・`Shuffle`（1 シグネチャ）
- 方針: `#nullable enable` を適用し、`#if !NET10_0_OR_GREATER` による条件付きコンパイルで移行時に自動無効化する
- 言語バージョン: 制約なし型パラメータの `null` 許容注釈（`TInner?` / `TOuter?`）を用いるため、`LangVersion` を 9.0 以上（推奨: `latest`）に設定する。ターゲットフレームワークやその他のプロジェクト構成は変更しない

---

## 問題

.NET 10 では、公開 `Enumerable` へ久しぶりに実用的な演算子が追加された。
追加されたのは外部結合の `LeftJoin`・`RightJoin`、およびランダム並べ替えの `Shuffle` であり、これらは .NET Framework 環境では使用できない。

| メソッド | 追加バージョン | 概要 |
| --- | --- | --- |
| `LeftJoin<TOuter, TInner, TKey, TResult>` | .NET 10.0 | 外部シーケンスの全要素を残す左外部結合を行う |
| `RightJoin<TOuter, TInner, TKey, TResult>` | .NET 10.0 | 内部シーケンスの全要素を残す右外部結合を行う |
| `Shuffle<TSource>` | .NET 10.0 | シーケンスの要素をランダムな順序へ並べ替える |

`LeftJoin`・`RightJoin` はいずれも `IEqualityComparer<TKey>` を受け取るオーバーロードを持つ。

これらが存在しない .NET Framework 環境では、同じ結果を得るために定型的な回避コードを書く必要がある。

- 左外部結合は、`GroupJoin` の結果に `SelectMany` と `DefaultIfEmpty` を組み合わせて表現する
- ランダム並べ替えは、`OrderBy(_ => Guid.NewGuid())` のような擬似的なイディオムで代用する

前者は結合の本質ではない定型句であり、記述を誤ると意図しない結合結果を生む。
後者は要素数に対して安定した一様分布を保証せず、要素ごとにキーを生成・ソートするため非効率でもある。

---

## 原因・背景

新世代 .NET の各バージョンでは、公開 `Enumerable` に演算子が少しずつ追加されてきた。
.NET 6 で `Chunk`・`MaxBy`・`MinBy`・`DistinctBy`、.NET 7 で `Order`・`OrderDescending`、.NET 8 でデリゲート不要の `ToDictionary` オーバーロード、.NET 9 で `CountBy`・`AggregateBy`・`Index` が追加された。

.NET 10 では、長らく標準の LINQ に欠けていた外部結合演算子 `LeftJoin`・`RightJoin` が初めて標準化された。
従来は `GroupJoin` と `DefaultIfEmpty` を組み合わせて手動で表現していた左・右外部結合が、SQL の `LEFT JOIN` / `RIGHT JOIN` に対応する専用メソッドとして提供される。
併せて、シーケンスをランダム順に並べ替える `Shuffle` も追加された。
`Shuffle` は非暗号論的な乱数生成器を用い、`OrderBy` による擬似シャッフルの非効率さと分布の偏りを解消する。

これらは .NET 10.0 で初めて追加されたものであり、.NET 9 以前の環境には存在しない。

なお、.NET Framework から .NET 5 の間に追加されたメソッド（`Append`・`Prepend`・`TakeLast`・`SkipLast`）については[別記事](/ja/articles/linq-backport-netframework-to-net5/)で、.NET 6 で追加された 4 メソッドについては[別記事](/ja/articles/linq-backport-netframework-to-net6/)で、.NET 7 で追加された `Order`・`OrderDescending` については[別記事](/ja/articles/linq-backport-netframework-to-net7/)で、.NET 8 で追加された `ToDictionary` オーバーロードについては[別記事](/ja/articles/linq-backport-netframework-to-net8/)で、.NET 9 で追加された `CountBy`・`AggregateBy`・`Index` については[別記事](/ja/articles/linq-backport-netframework-to-net9/)で解説している。

---

## 解決方法

本家 LINQ と同じ名前空間（`System.Linq`）に拡張メソッドを定義することで、既存のソースファイルに手を加えることなく透過的に利用できる。
`using System.Linq;` を記述済みのファイルは、追記なしで不足メソッドを獲得する。

条件付きコンパイル `#if !NET10_0_OR_GREATER` を使い、.NET 10 以降の環境ではこのファイルを丸ごとスキップするよう仕込む。
将来のフレームワークアップグレード時に、ファイルの削除やコードの書き換えを行わずに自動的に本家 LINQ へ切り替わる。

実装の要点は 3 つある。
1 つは `LeftJoin`・`RightJoin` を本家と同じく `GroupJoin` + `SelectMany` + `DefaultIfEmpty` の合成で表現すること、2 つ目は結果セレクタの引数に本家と一致する `null` 許容注釈（`LeftJoin` は内部要素 `TInner?`、`RightJoin` は外部要素 `TOuter?`）を付けること、3 つ目は `Shuffle` を本家と同じく遅延実行の反復子として実装しつつ、`Random.Shared` の無い .NET Framework でもスレッド安全な乱数源を確保することである。

---

## 実装例

以下は `LeftJoin`（2 シグネチャ）・`RightJoin`（2 シグネチャ）・`Shuffle`（1 シグネチャ）のポリフィル実装一式である。
`LeftJoin`・`RightJoin` は本家と同じく `GroupJoin` を土台に構成し、`Shuffle` はソースを配列へバッファリングしてから Fisher–Yates 法で並べ替える。
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

コンパイル時に `NET10_0_OR_GREATER` シンボルが定義されていない環境（.NET Framework を含む .NET 10 未満の環境）でのみ、上記クラスが有効になる。

---

## 各メソッドの詳解

### `LeftJoin` による左外部結合

左外部結合は、外部（左）シーケンスの全要素を出力に残し、一致する内部（右）要素が無い場合は既定値を補う。
一致しない外部要素に対しては結果セレクタの第 2 引数へ `default(TInner)`（参照型なら `null`）が渡される。

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

一致する部署を持たない「高橋」も出力に残り、部署名は `null` になるため既定値へフォールバックできる。
結果セレクタの第 2 引数 `d` は `null` 許容であり、参照する前に `null` 検査が必要である。

### `RightJoin` による右外部結合

右外部結合は、内部（右）シーケンスの全要素を出力に残し、一致する外部（左）要素が無い場合は既定値を補う。
一致しない内部要素に対しては結果セレクタの第 1 引数へ `default(TOuter)`（参照型なら `null`）が渡される。

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

所属社員の居ない「総務部」も出力に残り、社員名側が `null` になる。
`RightJoin(outer, inner, ...)` は `inner` の全要素を保持する点で `LeftJoin` と対称であり、内部シーケンスを軸に結合する。

### `Shuffle` によるランダム並べ替え

`Shuffle` はシーケンスの要素をランダムな順序で列挙する。
Fisher–Yates 法により一様分布のシャッフルを行い、`OrderBy(_ => Guid.NewGuid())` のような擬似的なイディオムよりも効率的で偏りが無い。

```csharp
var deck = Enumerable.Range(1, 52);

var shuffled = deck.Shuffle().ToArray();
// 例: [17, 3, 50, 28, ...]（呼び出しごとに異なる）
```

要素数 `n` に対して 1 回の走査で並べ替えるため、キー生成とソートを伴う擬似シャッフルより計算量が小さい。
乱数は非暗号論的な生成器を用いるため、抽選やトークン生成などセキュリティ用途には使わない。

### 遅延実行とバッファリング

`LeftJoin`・`RightJoin`・`Shuffle` はいずれも **遅延実行** の演算子であり、結果を列挙するまでソースは走査されない。
ただし `Shuffle` は `OrderBy` と同様、最初の要素を返す前にソース全体をバッファリングする。

```csharp
var query = Enumerable.Range(1, 3).Shuffle();

var first = query.ToArray();  // ここでソースが列挙・並べ替えされる
var second = query.ToArray(); // 再列挙すると別の順序になる
```

遅延実行であるため、同じクエリを 2 回列挙すると `Shuffle` は毎回異なる順序を生成する。
順序を固定したい場合は、`ToArray` や `ToList` で一度実体化してから使い回す。

---

## 条件付きコンパイルシンボルの選択

本実装では `#if !NET10_0_OR_GREATER` を採用している。
[.NET 5 相当のバックポート記事](/ja/articles/linq-backport-netframework-to-net5/)が `#if !NETCOREAPP` を採用しているのとは異なる。

`LeftJoin`・`RightJoin`・`Shuffle` は .NET 9 以前には存在しないため、`NETCOREAPP` や `NET9_0_OR_GREATER` を条件に使うと、.NET 8 や .NET 9 向けビルドでポリフィルが無効化され、コンパイルエラーが発生する。

| シンボル | .NET Framework | .NET 9 | .NET 10+ |
| --- | --- | --- | --- |
| `!NETCOREAPP` | ポリフィル有効 | **ポリフィル無効（エラー）** | ポリフィル無効 |
| `!NET9_0_OR_GREATER` | ポリフィル有効 | **ポリフィル無効（エラー）** | ポリフィル無効 |
| `!NET10_0_OR_GREATER` | ポリフィル有効 | ポリフィル有効 | ポリフィル無効 |

`!NET10_0_OR_GREATER` を使うことで、.NET 9 を含めた .NET 10 未満の環境すべてでポリフィルが有効になり、.NET 10 以降では自動的に本家 LINQ へ切り替わる。
`Shuffle` の乱数源は入れ子の `#if NET6_0_OR_GREATER` でさらに分岐し、.NET 6〜9 では `Random.Shared` を、.NET Framework では `[ThreadStatic]` なインスタンスを使う。

---

## 注意点

- **結合方向の対応関係**: `LeftJoin` は第 1 引数（`outer`）の全要素を、`RightJoin` は第 2 引数（`inner`）の全要素を保持する。結果セレクタで `null` 許容になるのは、`LeftJoin` では内部要素、`RightJoin` では外部要素である。参照する前に `null` 検査を行う。
- **キーの等価比較**: `comparer` を渡さないオーバーロードは `EqualityComparer<TKey>.Default` を使う。大文字・小文字を区別しない結合などが必要な場合は `IEqualityComparer<TKey>` を渡すオーバーロードを用いる。パラメータの少ないオーバーロードから比較子オーバーロードへ委譲する際は、`comparer:` の名前付き引数で意図した先へ確実に解決させている。
- **`Shuffle` は遅延実行である**: `Shuffle` は列挙するたびに並べ替えを行うため、同一クエリを複数回列挙すると毎回異なる順序になる。順序を固定するには `ToArray` / `ToList` で実体化する。列挙開始時にソース全体をバッファリングするため、無限シーケンスには使えない。
- **`Shuffle` の乱数はセキュリティ用途に不適**: 非暗号論的な乱数生成器を使うため、抽選・シャッフルの再現不可能性が問われる用途には `System.Security.Cryptography` の乱数を別途用いる。
- **名前衝突は起きない**: これらのポリフィルは本家に存在しないシグネチャであり、既存の `Join` や `OrderBy` とはメソッド名・引数が異なるため、オーバーロード解決で衝突しない。移行後は同名の本家メソッドが優先され、条件付きコンパイルによりポリフィルは無効化される。
- **C# 9 以上でコンパイルする**: 本ポリフィルは制約なし型パラメータの `null` 許容注釈（`TInner?` / `TOuter?`）と `#nullable enable` を用いるため、コンパイルに C# 9 以上を要する。.NET Framework 4.8 の既定 `LangVersion`（7.3）のままでは `CS8627` などでコンパイルできないため、`.csproj` に `<LangVersion>9.0</LangVersion>`（または `latest`）を指定する。

---

## 代替案・比較

| 方法 | メリット | デメリット | 適するケース |
| --- | --- | --- | --- |
| 自作ポリフィル（本記事） | 外部依存なし・本家と同じ名前と使用感で書ける | 実装・保守の手間がある | 依存を最小化したいプロジェクト |
| `GroupJoin` + `DefaultIfEmpty` を直書き | 追加コード不要 | 記述が冗長・意図を誤りやすい | 結合の使用箇所が少ない場合 |
| `OrderBy(_ => Guid.NewGuid())` で代用 | 追加コード不要 | 非効率で分布が保証されない | 少数要素の並べ替えで厳密さを問わない場合 |
| MoreLINQ などのライブラリ導入 | 実装済みで検証されている | 外部依存が増える・API が本家と異なる | 既に依存を許容している場合 |
| .NET 10 へのアップグレード | 根本的解決・言語機能も享受できる | 移行コストが発生する | 移行が技術的・ビジネス的に許容できる場合 |

定型句の直書きは追加コードこそ不要だが、後日 .NET 10 へ移行して本家の `LeftJoin`・`Shuffle` へ統一する際に、使用箇所を洗い出して置換する手間が残る。
本記事のポリフィルを導入しておけば、移行前から本家と同じ名前で記述でき、移行時にはファイルを残したまま条件付きコンパイルが自動で本家へ切り替える。

---

## まとめ

.NET 10 で追加された `LeftJoin`・`RightJoin`・`Shuffle` と、.NET Framework 環境へのバックポート手法を解説した。

実装において重要なポイントは以下の 3 点である。

- **本家の合成と注釈に合わせる**: `LeftJoin`・`RightJoin` は `GroupJoin` + `SelectMany` + `DefaultIfEmpty` で表現し、結果セレクタの `null` 許容注釈を本家と一致させることで、移行前後の `null` 許容解析を一致させる。
- **`#if !NET10_0_OR_GREATER` を選択する**: .NET 9 以前にはこれらの演算子が存在しないため、`!NETCOREAPP` や `!NET9_0_OR_GREATER` では .NET 9 環境でエラーになる。
- **`Shuffle` の遅延実行と乱数源を把握する**: `Shuffle` は遅延実行かつ列挙開始時に全バッファリングし、乱数源はフレームワークに応じて `Random.Shared` と `[ThreadStatic]` を切り替える。

| メソッド | 評価戦略 | 保持される側 | `null` 許容になる引数 |
| --- | --- | --- | --- |
| `LeftJoin` | 遅延 | 外部（左） | 内部要素 `TInner?` |
| `RightJoin` | 遅延 | 内部（右） | 外部要素 `TOuter?` |
| `Shuffle` | 遅延（列挙時に全バッファリング） | — | — |

---

## 関連記事

- [.NET Framework の不足 LINQ メソッドを .NET 9 相当にバックポートする](/ja/articles/linq-backport-netframework-to-net9/)
- [.NET Framework の不足 LINQ メソッドを .NET 8 相当にバックポートする](/ja/articles/linq-backport-netframework-to-net8/)
- [.NET Framework の不足 LINQ メソッドを .NET 7 相当にバックポートする](/ja/articles/linq-backport-netframework-to-net7/)
- [.NET Framework の不足 LINQ メソッドを .NET 6 相当にバックポートする](/ja/articles/linq-backport-netframework-to-net6/)
- [.NET Framework の不足 LINQ メソッドを .NET 5 相当にバックポートする](/ja/articles/linq-backport-netframework-to-net5/)
