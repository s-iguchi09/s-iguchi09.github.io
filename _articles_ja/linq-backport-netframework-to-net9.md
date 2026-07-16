---
layout: article-ja
title: "CountBy・AggregateBy・Index を .NET Framework にバックポートする"
date: 2026-07-16
category: C#
excerpt: ".NET 9 で追加された LINQ の CountBy・AggregateBy・Index を、#nullable enable と条件付きコンパイル、遅延評価の維持を意識しながら .NET Framework 環境へ安全にバックポートする実装方法を解説する。"
---

## 概要

.NET 9 で追加された `CountBy`・`AggregateBy`・`Index` は、`GroupBy` を経由しがちだった「キーごとの集計」と、`Select((x, i) => …)` で書いていた「インデックス付き列挙」を簡潔にするメソッドである。.NET Framework 環境ではこれらが存在せず、冗長な回避コードで代用することになる。

本記事では、**.NET 9 で新たに追加された 3 つの LINQ メソッド**（`CountBy`・`AggregateBy`・`Index`）を整理し、**同一の使用感で動作する拡張メソッド（ポリフィル）を安全に実装する方法**を解説する。
これらはいずれも遅延評価であるため、遅延と即時の境界を保った実装と、将来の .NET 9 以降への移行時にコードを無修正で切り替えるための条件付きコンパイル手法も合わせて紹介する。

---

## 前提・対象環境

- フレームワーク: .NET Framework 4.8 / .NET 9+
- 対象: LINQ の 3 メソッド（CountBy / AggregateBy / Index）と各オーバーロード
- 方針: `#nullable enable` を適用し、`#if !NET9_0_OR_GREATER` による条件付きコンパイルで移行時に自動無効化する
- 言語バージョン: `#nullable enable` と `where TKey : notnull` は C# 8.0 以上を必要とする。.NET Framework 4.8 の既定は C# 7.3 のため、`.csproj` の `LangVersion` を `8.0` 以上に設定する

---

## 問題

.NET 9 で追加された以下の LINQ メソッドは、.NET Framework 環境では使用できない。

| メソッド | 追加されたバージョン | 概要 |
| --- | --- | --- |
| `CountBy` | .NET 9.0 | キーごとに要素数を数え、`KeyValuePair<TKey, int>` の列として返す |
| `AggregateBy` | .NET 9.0 | キーごとに累積計算を行い、`KeyValuePair<TKey, TAccumulate>` の列として返す |
| `Index` | .NET 9.0 | 各要素にインデックスを付与し、`(int Index, TSource Item)` タプルの列として返す |

これらが存在しない .NET Framework 環境では、同等の集計を得るために `GroupBy` を経由した冗長な記述を強いられる。

- `CountBy(keySelector)` の代わりに、`GroupBy(keySelector).Select(g => new { g.Key, Count = g.Count() })` と書く
- `Index()` の代わりに、`Select((item, index) => (index, item))` と書く

これらの定型記述は集計の本質ではなく、記述量が増えるほど可読性を下げる。

---

## 原因・背景

.NET 6 で `Chunk`・`MaxBy`・`MinBy`・`DistinctBy`、.NET 7 で `Order`・`OrderDescending`、.NET 8 でデリゲート不要の `ToDictionary` が追加された流れに続き、.NET 9 では「キー単位の集計」と「インデックス付き列挙」を簡潔にするメソッドが追加された。
`CountBy`・`AggregateBy`・`Index` はいずれも .NET 9.0 で初めて追加されたものであり、.NET 8 以前の環境では存在しない。

`CountBy`・`AggregateBy` の狙いは、集計のためだけに `GroupBy` で中間的なグルーピング（各キーに対応する要素リスト）を割り当てる無駄を避けることにある。
本家の実装は内部で `Dictionary` を用いてキーごとの状態のみを保持して集計するため、要素そのものを保持し続けるグルーピングよりもメモリ効率が高い。
この内部辞書の性質上、本家 `CountBy`・`AggregateBy` は型引数に `where TKey : notnull` 制約を課し、キーに `null` が渡ると列挙時に例外を投げる。

なお、.NET Framework から .NET 5 の間に追加されたメソッド（`Append`・`Prepend`・`TakeLast`・`SkipLast`）については[別記事](/ja/articles/linq-backport-netframework-to-net5/)で、.NET 6 で追加された 4 メソッドについては[別記事](/ja/articles/linq-backport-netframework-to-net6/)で、.NET 7 の `Order`・`OrderDescending` については[別記事](/ja/articles/linq-backport-netframework-to-net7/)で、.NET 8 の `ToDictionary` オーバーロードについては[別記事](/ja/articles/linq-backport-netframework-to-net8/)で解説している。

---

## 解決方法

本家 LINQ と同じ名前空間（`System.Linq`）に拡張メソッドを定義することで、既存のソースファイルに手を加えることなく透過的に利用できる。

条件付きコンパイル `#if !NET9_0_OR_GREATER` を使い、.NET 9 以降の環境ではこのファイルを丸ごとスキップするよう仕込む。
将来のフレームワークアップグレード時に、ファイルの削除やコードの書き換えを行わずに自動的に本家 LINQ へ切り替わる。

`CountBy`・`AggregateBy` の実装方針は、本家と同じく内部 `Dictionary` でキーごとの状態を直接集計することである。
`GroupBy` へ委譲する素朴な方法もあるが、それでは本家が避けている中間グルーピングを割り当ててしまい、`null` キーの扱いも `GroupBy`（`null` を 1 グループとして許容する）と本家（例外）とで食い違う。
辞書に直接集計すれば、割り当てを抑えつつ `where TKey : notnull` 制約と `null` キーの挙動を本家に一致させられる。

重要なのは、遅延評価を保ったまま引数検証を即時に行うことである。
公開メソッドで引数を検証し、`yield return` を含む `private` なイテレータ本体へ委譲する 2 段構えにする。
本体全体をそのまま `yield` メソッドにすると、`source` などの `ArgumentNullException` が列挙時まで遅延し、本家と挙動がずれる。
`Index` は `Select` のインデックス付きオーバーロードへ委譲するだけでよい（`Select` 自身が同じ 2 段構えを備える）。

---

## 実装例

以下は 3 メソッド（`AggregateBy` の `seed` 版・`seedSelector` 版を含む計 4 シグネチャ）のポリフィル実装一式である。
各公開メソッドは引数を即時に検証し、遅延評価のイテレータ本体へ委譲する。
プロジェクトに `LinqExtensions.Net9.cs` などの名前でそのまま追加して使用できる。

```csharp
#nullable enable

using System;
using System.Collections.Generic;

#if !NET9_0_OR_GREATER // .NET 9.0 以降ではない環境（.NET Framework など）のみ有効化

namespace System.Linq
{
    /// <summary>
    /// .NET 9.0 で追加された LINQ メソッドを古いターゲットフレームワーク向けに補完する拡張メソッドを提供します。
    /// </summary>
    public static partial class LinqExtensions
    {
        // ==========================================
        // 1. CountBy
        // ==========================================
        public static IEnumerable<KeyValuePair<TKey, int>> CountBy<TSource, TKey>(
            this IEnumerable<TSource> source,
            Func<TSource, TKey> keySelector,
            IEqualityComparer<TKey>? keyComparer = null)
            where TKey : notnull
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));

            return CountByIterator(source, keySelector, keyComparer);
        }

        private static IEnumerable<KeyValuePair<TKey, int>> CountByIterator<TSource, TKey>(
            IEnumerable<TSource> source,
            Func<TSource, TKey> keySelector,
            IEqualityComparer<TKey>? keyComparer)
            where TKey : notnull
        {
            var counts = new Dictionary<TKey, int>(keyComparer);
            foreach (var item in source)
            {
                TKey key = keySelector(item); // key が null の場合、次行で ArgumentNullException
                counts.TryGetValue(key, out int count);
                counts[key] = checked(count + 1); // 本家 .NET 9 同様、int.MaxValue 超過で OverflowException
            }

            foreach (KeyValuePair<TKey, int> entry in counts)
            {
                yield return entry;
            }
        }

        // ==========================================
        // 2. AggregateBy（seed 版 / seedSelector 版）
        // ==========================================
        public static IEnumerable<KeyValuePair<TKey, TAccumulate>> AggregateBy<TSource, TKey, TAccumulate>(
            this IEnumerable<TSource> source,
            Func<TSource, TKey> keySelector,
            TAccumulate seed,
            Func<TAccumulate, TSource, TAccumulate> func,
            IEqualityComparer<TKey>? keyComparer = null)
            where TKey : notnull
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));
            if (func == null) throw new ArgumentNullException(nameof(func));

            return AggregateByIterator(source, keySelector, key => seed, func, keyComparer);
        }

        public static IEnumerable<KeyValuePair<TKey, TAccumulate>> AggregateBy<TSource, TKey, TAccumulate>(
            this IEnumerable<TSource> source,
            Func<TSource, TKey> keySelector,
            Func<TKey, TAccumulate> seedSelector,
            Func<TAccumulate, TSource, TAccumulate> func,
            IEqualityComparer<TKey>? keyComparer = null)
            where TKey : notnull
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));
            if (seedSelector == null) throw new ArgumentNullException(nameof(seedSelector));
            if (func == null) throw new ArgumentNullException(nameof(func));

            return AggregateByIterator(source, keySelector, seedSelector, func, keyComparer);
        }

        private static IEnumerable<KeyValuePair<TKey, TAccumulate>> AggregateByIterator<TSource, TKey, TAccumulate>(
            IEnumerable<TSource> source,
            Func<TSource, TKey> keySelector,
            Func<TKey, TAccumulate> seedSelector,
            Func<TAccumulate, TSource, TAccumulate> func,
            IEqualityComparer<TKey>? keyComparer)
            where TKey : notnull
        {
            var accumulators = new Dictionary<TKey, TAccumulate>(keyComparer);
            foreach (var item in source)
            {
                TKey key = keySelector(item); // key が null の場合、次行で ArgumentNullException
                if (!accumulators.TryGetValue(key, out var acc))
                {
                    acc = seedSelector(key);
                }
                accumulators[key] = func(acc!, item);
            }

            foreach (KeyValuePair<TKey, TAccumulate> entry in accumulators)
            {
                yield return entry;
            }
        }

        // ==========================================
        // 3. Index
        // ==========================================
        public static IEnumerable<(int Index, TSource Item)> Index<TSource>(
            this IEnumerable<TSource> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            return source.Select((item, index) => (index, item));
        }
    }
}

#endif
```

コンパイル時に `NET9_0_OR_GREATER` シンボルが定義されていない環境（.NET Framework を含む .NET 9 未満の環境）でのみ、上記クラスが有効になる。
`seed` 版は初期値を返すだけの `seedSelector`（`key => seed`）に読み替えて共通の集計本体へ委譲している。

---

## 各メソッドの詳解

### `CountBy`（キーごとの要素数）

`CountBy` はキーセレクタが返すキーごとに要素数を数え、`KeyValuePair<TKey, int>` の列として返す。

```csharp
var words = new[] { "apple", "banana", "apple", "cherry", "banana", "apple" };

foreach (var pair in words.CountBy(word => word))
{
    Console.WriteLine($"{pair.Key}: {pair.Value}");
}
// apple: 3
// banana: 2
// cherry: 1
```

結果はキーが初めて出現した順に返る（削除が発生しない限り、内部辞書は挿入順で列挙されるため初出順になる）。
`IEqualityComparer<TKey>` を渡すオーバーロードでは、大文字・小文字を区別しない集計などに比較方法を差し替えられる。

### `AggregateBy`（キーごとの畳み込み）

`AggregateBy` はキーごとに累積計算を行う。
以下は地域ごとの売上合計を求める例である。

```csharp
var sales = new[]
{
    ("Tokyo", 100),
    ("Osaka", 80),
    ("Tokyo", 120),
    ("Osaka", 60),
};

var totals = sales.AggregateBy(
    keySelector: s => s.Item1,
    seed: 0,
    func: (total, s) => total + s.Item2);

foreach (var pair in totals)
{
    Console.WriteLine($"{pair.Key}: {pair.Value}");
}
// Tokyo: 220
// Osaka: 140
```

初期値をキーごとに変えたい場合は、`seed` の代わりに `seedSelector`（`Func<TKey, TAccumulate>`）を受け取るオーバーロードを使う。
キーに応じて異なる初期状態から畳み込みを開始できる。

### `Index`（インデックス付き列挙）

`Index` は各要素に 0 始まりのインデックスを付与し、`(int Index, TSource Item)` タプルの列として返す。

```csharp
var items = new[] { "a", "b", "c" };

foreach (var (index, item) in items.Index())
{
    Console.WriteLine($"{index}: {item}");
}
// 0: a
// 1: b
// 2: c
```

タプルの第 1 要素がインデックス、第 2 要素が値である。
`Select((item, index) => ...)` のラムダとは引数の順序が逆になる点に注意する（`Index` はインデックスが先）。

### 遅延評価であること

`CountBy`・`AggregateBy`・`Index` はいずれも遅延評価であり、`foreach` や `.ToList()` の時点で初めて計算が実行される。
一方、`source` や各デリゲートが `null` の場合の `ArgumentNullException` は、呼び出し時点で即座に投げられる。
公開メソッドが引数検証を先に行い、そのうえで遅延イテレータを返すためである。

```csharp
IEnumerable<int> numbers = null!;

// 列挙する前に、この行で即座に ArgumentNullException が投げられる
var query = numbers.CountBy(n => n);
```

なお、キーセレクタが列挙中に `null` キーを返した場合の `ArgumentNullException` は、内部辞書へそのキーで参照・格納しようとした時点（`TryGetValue` の呼び出し）で投げられる。
これは本家 .NET 9 の挙動（内部辞書が `null` キーを拒否する）と一致する。

---

## 条件付きコンパイルシンボルの選択

本実装では `#if !NET9_0_OR_GREATER` を採用している。
[.NET 5 相当のバックポート記事](/ja/articles/linq-backport-netframework-to-net5/)が `#if !NETCOREAPP` を採用しているのとは異なる。

`CountBy`・`AggregateBy`・`Index` は .NET 8 以前には存在しないため、`NETCOREAPP` や `NET8_0_OR_GREATER` を条件に使うと、.NET 8 向けビルドでポリフィルが無効化され、コンパイルエラーが発生する。

| シンボル | .NET Framework | .NET 8 | .NET 9+ |
| --- | --- | --- | --- |
| `!NETCOREAPP` | ポリフィル有効 | **ポリフィル無効（エラー）** | ポリフィル無効 |
| `!NET8_0_OR_GREATER` | ポリフィル有効 | **ポリフィル無効（エラー）** | ポリフィル無効 |
| `!NET9_0_OR_GREATER` | ポリフィル有効 | ポリフィル有効 | ポリフィル無効 |

`!NET9_0_OR_GREATER` を使うことで、.NET 8 を含めた .NET 9 未満の環境すべてでポリフィルが有効になり、.NET 9 以降では自動的に本家 LINQ へ切り替わる。

---

## 注意点

- **遅延評価である**: 3 メソッドとも遅延評価であり、列挙するまで計算は走らない。戻り値は `Dictionary` ではなく遅延列であるため、同じ結果を複数回列挙するとそのつど再計算される。結果を確定・再利用したい場合は `.ToList()` や `.ToDictionary()` で即時に実体化する。`source` や各デリゲートが `null` の場合の `ArgumentNullException` は呼び出し時点で投げられる。
- **キー型は `notnull`**: 本家 `CountBy`・`AggregateBy` は `where TKey : notnull` 制約を持つため、ポリフィルも同じ制約を付ける。これにより移行前後で `null` 許容参照型の解析結果が一致する。キーセレクタが実行時に `null` キーを返した場合は、内部辞書が列挙時に `ArgumentNullException` を投げる。この挙動も本家と一致する。
- **メモリ効率**: 本ポリフィルは内部 `Dictionary` にキーごとの状態のみを集計するため、`GroupBy` を経由する素朴な実装のように要素そのものを保持し続けることはない。割り当てプロファイルは本家 .NET 9 の実装に近い。
- **`CountBy` のオーバーフロー**: 計数は `checked(count + 1)` で行うため、あるキーの件数が `int.MaxValue` を超えると `OverflowException` を投げる。本家 .NET 9 も `checked` で加算するため、この挙動は一致する。
- **`Index` とタプル順序 / 型名の紛らわしさ**: `Index` が返すタプルは `(Index, Item)` の順であり、インデックスが先である。また、メソッド名 `Index` は C# 8 で導入された `System.Index` 型（範囲・インデックス構文の要素型）と名称が紛らわしいが、型とメソッドであり衝突はしない。
- **名前衝突は起きない**: 本ポリフィルは本家と同じシグネチャで `System.Linq` に定義するため、`using System.Linq;` があるファイルで透過的に解決される。同名・同型のメソッドを別途自作している場合のみ衝突しうる。

---

## 代替案・比較

| 方法 | メリット | デメリット | 適するケース |
| --- | --- | --- | --- |
| 自作ポリフィル（本記事） | 外部依存なし・本家と同一の使用感と割り当てプロファイル | 実装・保守の手間がある | 依存を最小化したいプロジェクト |
| `GroupBy(...).Select(...)` を直書き | 追加コード不要 | 記述が冗長・中間グルーピングを割り当てる・移行時に一括置換が必要 | 使用箇所が少なく移行予定もない場合 |
| .NET 9 へのアップグレード | 根本的解決・割り当て削減の恩恵を受けられる | 移行コストが発生する | 移行が技術的・ビジネス的に許容できる場合 |

`GroupBy(...).Select(...)` の直書きは追加コードこそ不要だが、集計のためだけに中間グルーピングを割り当てるうえ、後日 .NET 9 へ移行して `CountBy` などに統一する際に使用箇所を洗い出して置換する手間が残る。
本記事のポリフィルを導入しておけば、移行前から本家と同じ記法で記述でき、移行時にはファイルを残したまま条件付きコンパイルが自動で本家へ切り替える。

---

## まとめ

.NET 9 で追加された `CountBy`・`AggregateBy`・`Index` の 3 メソッドと、.NET Framework 環境へのバックポート手法を解説した。

実装において重要なポイントは以下の 3 点である。

- **遅延評価を保つ**: 公開メソッドで引数を即時検証し、`yield return` を含む `private` イテレータへ委譲する 2 段構えにする。戻り値は `Dictionary` ではなく遅延列であり、確定したい場合は `.ToList()` などで実体化する。
- **`#if !NET9_0_OR_GREATER` を選択する**: .NET 8 以前にはこれらのメソッドが存在しないため、`!NETCOREAPP` や `!NET8_0_OR_GREATER` では .NET 8 環境でエラーになる。
- **`where TKey : notnull` と `null` キー挙動を本家に合わせる**: 内部 `Dictionary` で直接集計することで、制約・`null` キー例外・割り当てプロファイルを本家と一致させ、移行時の無修正切替を成立させる。

| メソッド | 評価戦略 | 戻り値型 | 実装の要点 |
| --- | --- | --- | --- |
| `CountBy` | 遅延 | `IEnumerable<KeyValuePair<TKey, int>>` | 内部 `Dictionary` でキーごとに計数 |
| `AggregateBy` | 遅延 | `IEnumerable<KeyValuePair<TKey, TAccumulate>>` | 内部 `Dictionary` でキーごとに畳み込み（seed / seedSelector） |
| `Index` | 遅延 | `IEnumerable<(int Index, TSource Item)>` | `Select((item, index) => (index, item))` に委譲 |

---

## 関連記事

- [KeyValuePair・タプル版 ToDictionary を .NET Framework にバックポートする](/ja/articles/linq-backport-netframework-to-net8/)
- [Order・OrderDescending を .NET Framework にバックポートする](/ja/articles/linq-backport-netframework-to-net7/)
- [Chunk・MaxBy・MinBy・DistinctBy を .NET Framework にバックポートする](/ja/articles/linq-backport-netframework-to-net6/)
- [Append・Prepend・TakeLast・SkipLast を .NET Framework にバックポートする](/ja/articles/linq-backport-netframework-to-net5/)
