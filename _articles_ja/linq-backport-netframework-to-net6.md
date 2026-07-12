---
layout: article-ja
title: ".NET Framework の不足 LINQ メソッドを .NET 6 相当にバックポートする"
date: 2026-07-13
category: C#
excerpt: ".NET 6 で追加された LINQ の 4 メソッド（Chunk・MaxBy・MinBy・DistinctBy）を、#nullable enable と条件付きコンパイルを活用しながら .NET Framework 環境へ安全にバックポートする実装方法を解説する。"
---

## 概要

.NET Framework から新世代 .NET（.NET 6 以降）への移行を段階的に進めている場合や、諸事情で .NET Framework 環境のコードをメンテナンスし続けなければならない場合、ストレスの原因となるのが「新世代 .NET にはあるのに、.NET Framework には存在しない LINQ メソッド」の存在である。

本記事では、**.NET 6 で新たに追加された 4 つの LINQ メソッド**（`Chunk`・`MaxBy`・`MinBy`・`DistinctBy`）を整理し、**同一の使用感で動作する拡張メソッド（ポリフィル）を安全に実装する方法**を解説する。
`#nullable enable` を使った現代的な実装と、将来の .NET 6 以降への移行時にコードを無修正で切り替えるための条件付きコンパイル手法も合わせて紹介する。

---

## 前提・対象環境

- フレームワーク: .NET Framework 4.8 / .NET 6+
- 対象: LINQ の 4 メソッド（Chunk / MaxBy / MinBy / DistinctBy）
- 方針: `#nullable enable` を適用し、`#if !NET6_0_OR_GREATER` による条件付きコンパイルで移行時に自動無効化する

---

## 問題

.NET 6 で追加された以下の LINQ メソッドは、.NET Framework 環境では使用できない。

| メソッド名 | 追加されたバージョン | 概要 |
| --- | --- | --- |
| `Chunk<T>` | .NET 6.0 | シーケンスを指定した最大サイズの塊に分割する |
| `MaxBy<T, TKey>` | .NET 6.0 | 指定したキー基準で値が最大の要素を丸ごと取得する |
| `MinBy<T, TKey>` | .NET 6.0 | 指定したキー基準で値が最小の要素を丸ごと取得する |
| `DistinctBy<T, TKey>` | .NET 6.0 | 指定したキーの一意性に基づいて要素を抽出する |

これらが存在しない .NET Framework 環境では、下記のような代替実装を強いられる。

- `Chunk` の代わりに、インデックスを使った `GroupBy` で手動グループ化する
- `MaxBy` の代わりに、`OrderByDescending(x => x.Key).FirstOrDefault()` で全件ソートする
- `DistinctBy` の代わりに、`GroupBy(x => x.Key).Select(g => g.First())` と書く

いずれもコードの可読性を下げ、`MaxBy` の代替に見られる全件ソートのようなパフォーマンス上の無駄も生じる。

---

## 原因・背景

.NET Framework 4.8 が最終バージョンとなった後、.NET Core から .NET 5 までの移行期は、LINQ の内部ロジックを書き直すパフォーマンス向上が中心であり、目立ったメソッドの大量追加は行われなかった。

.NET 6 のリリースで、待望の便利メソッド群が一挙に公開された。
`Chunk`・`MaxBy`・`MinBy`・`DistinctBy` はいずれも .NET 6.0 で初めて追加されたものであり、.NET 5 以前の環境では存在しない。

なお、.NET Framework から .NET 5 の間に追加されたメソッド（`Append`・`Prepend`・`TakeLast`・`SkipLast`）については[別記事](/ja/articles/linq-backport-netframework-to-net5/)で解説している。

---

## 解決方法

本家 LINQ と同じ名前空間（`System.Linq`）に拡張メソッドを定義することで、既存のソースファイルに手を加えることなく透過的に利用できる。

条件付きコンパイル `#if !NET6_0_OR_GREATER` を使い、.NET 6 以降の環境ではこのファイルを丸ごとスキップするよう仕込む。
将来のフレームワークアップグレード時に、ファイルの削除やコードの書き換えを行わずに自動的に本家 LINQ へ切り替わる。

---

## 実装例

以下は 4 メソッドのポリフィル実装一式である。
プロジェクトに `LinqExtensions.Net6.cs` などの名前でそのまま追加して使用できる。

```csharp
#nullable enable

using System;
using System.Collections.Generic;

#if !NET6_0_OR_GREATER // .NET 6.0 以降ではない環境（.NET Framework など）のみ有効化

namespace System.Linq
{
    /// <summary>
    /// .NET 6.0 で追加された LINQ メソッドを古いターゲットフレームワーク向けに補完する拡張メソッドを提供します。
    /// </summary>
    public static partial class LinqExtensions
    {
        // ==========================================
        // 1. Chunk
        // ==========================================
        public static IEnumerable<TSource[]> Chunk<TSource>(this IEnumerable<TSource> source, int size)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (size <= 0) throw new ArgumentOutOfRangeException(nameof(size), "Size must be greater than 0.");

            return ChunkIterator(source, size);
        }

        private static IEnumerable<TSource[]> ChunkIterator<TSource>(IEnumerable<TSource> source, int size)
        {
            using var enumerator = source.GetEnumerator();
            while (enumerator.MoveNext())
            {
                var chunk = new TSource[size];
                chunk[0] = enumerator.Current;
                int count = 1;

                while (count < size && enumerator.MoveNext())
                {
                    chunk[count++] = enumerator.Current;
                }

                if (count < size)
                {
                    Array.Resize(ref chunk, count);
                }

                yield return chunk;
            }
        }

        // ==========================================
        // 2. MaxBy
        // ==========================================
        public static TSource? MaxBy<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));

            return MaxBy(source, keySelector, comparer: null);
        }

        public static TSource? MaxBy<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector, IComparer<TKey>? comparer)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));

            comparer ??= Comparer<TKey>.Default;

            using var enumerator = source.GetEnumerator();
            if (!enumerator.MoveNext())
            {
                return default;
            }

            var maxElement = enumerator.Current;
            var maxKey = keySelector(maxElement);

            while (enumerator.MoveNext())
            {
                var currentElement = enumerator.Current;
                var currentKey = keySelector(currentElement);

                if (comparer.Compare(currentKey, maxKey) > 0)
                {
                    maxElement = currentElement;
                    maxKey = currentKey;
                }
            }

            return maxElement;
        }

        // ==========================================
        // 3. MinBy
        // ==========================================
        public static TSource? MinBy<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));

            return MinBy(source, keySelector, comparer: null);
        }

        public static TSource? MinBy<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector, IComparer<TKey>? comparer)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));

            comparer ??= Comparer<TKey>.Default;

            using var enumerator = source.GetEnumerator();
            if (!enumerator.MoveNext())
            {
                return default;
            }

            var minElement = enumerator.Current;
            var minKey = keySelector(minElement);

            while (enumerator.MoveNext())
            {
                var currentElement = enumerator.Current;
                var currentKey = keySelector(currentElement);

                if (comparer.Compare(currentKey, minKey) < 0)
                {
                    minElement = currentElement;
                    minKey = currentKey;
                }
            }

            return minElement;
        }

        // ==========================================
        // 4. DistinctBy
        // ==========================================
        public static IEnumerable<TSource> DistinctBy<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));

            return DistinctByIterator(source, keySelector, comparer: null);
        }

        public static IEnumerable<TSource> DistinctBy<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector, IEqualityComparer<TKey>? comparer)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));

            return DistinctByIterator(source, keySelector, comparer);
        }

        private static IEnumerable<TSource> DistinctByIterator<TSource, TKey>(IEnumerable<TSource> source, Func<TSource, TKey> keySelector, IEqualityComparer<TKey>? comparer)
        {
            var knownKeys = new HashSet<TKey>(comparer);
            foreach (var item in source)
            {
                if (knownKeys.Add(keySelector(item)))
                {
                    yield return item;
                }
            }
        }
    }
}

#endif
```

コンパイル時に `NET6_0_OR_GREATER` シンボルが定義されていない環境（.NET Framework を含む .NET 6 未満の環境）でのみ、上記クラスが有効になる。

---

## 技術解説：遅延評価と先行評価のメソッド設計

4 つのメソッドは評価戦略が 2 種類に分かれており、それぞれでメソッドの設計が異なる。

**遅延評価メソッド（`Chunk` / `DistinctBy`）** は `yield return` を使うイテレータで実装されており、`foreach` や `.ToList()` が呼ばれるまでデータを 1 件も処理しない。
引数チェックも同様に遅延されるため、`public` メソッドと `~Iterator` メソッドを分離する必要がある。

**先行評価メソッド（`MaxBy` / `MinBy`）** は `yield return` を一切使わず、呼び出しと同時に全件を走査して結果を返す。
引数チェックも呼び出し時点で即時実行されるため、メソッドの分離は不要である。

遅延評価のメソッドを誤って 1 つのメソッドにまとめた場合に何が起きるかを以下に示す。

```csharp
// 悪い設計の例（DistinctBy のパブリックメソッドとイテレータをまとめた場合）
public static IEnumerable<TSource> DistinctBy<TSource, TKey>(
    this IEnumerable<TSource> source, Func<TSource, TKey> keySelector)
{
    if (source == null) throw new ArgumentNullException(nameof(source)); // ①

    var knownKeys = new HashSet<TKey>();
    foreach (var item in source) // ② yield return が存在するため、① はここで初めて実行される
    {
        if (knownKeys.Add(keySelector(item)))
        {
            yield return item;
        }
    }
}
```

`yield return` を含むメソッドは呼び出された瞬間にコードが一切実行されない。
上記の悪い設計に `null` を渡して呼び出すと、`①` の null チェックをスルーして正常終了したかのように見え、実際に列挙した時点で初めて `ArgumentNullException` が発生する。
バグの発生箇所と例外の発生箇所が離れるため、デバッグが困難になる。

メソッドを 2 つに分けることで、引数チェックが呼び出しの瞬間に即時実行されるようになる。
この構成は標準の LINQ 内部でも採用されているパターンである。

---

## 各メソッドの詳解

### `Chunk<T>`（指定サイズで分割）

シーケンスを「最大 `size` 個」のチャンク（配列）に分割する。

```csharp
var result = new[] { 1, 2, 3, 4, 5 }.Chunk(2);
// result: [1, 2], [3, 4], [5]
```

#### Chunk の内部ロジック

最初に `size` 個分の配列を確保し、データを順番に詰めていく。

```csharp
var chunk = new TSource[size]; // size 個分の配列を事前に確保する
chunk[0] = enumerator.Current;
int count = 1;

while (count < size && enumerator.MoveNext())
{
    chunk[count++] = enumerator.Current;
}

if (count < size)
{
    Array.Resize(ref chunk, count); // 末尾チャンクが端数の場合のみリサイズする
}

yield return chunk;
```

末尾チャンクが端数（例: 5 個を 2 ずつ分割した最後の 1 個）になる場合のみ `Array.Resize` が走る。
それ以外のチャンクは事前確保の配列をそのまま返すため、不要なアロケーションが最小限に抑えられる。

---

### `MaxBy<T, TKey>` / `MinBy<T, TKey>`（キー基準での最大・最小要素取得）

キーを指定して、そのキーが最大・最小の要素をオブジェクトごと取得する。
`Max()` や `Min()` がキーの値そのものを返すのに対し、`MaxBy` / `MinBy` は**キーに対応する元の要素**を返す点が異なる。

```csharp
var products = new[]
{
    new { Name = "A", Price = 300 },
    new { Name = "B", Price = 100 },
    new { Name = "C", Price = 200 },
};

var mostExpensive = products.MaxBy(p => p.Price);
// mostExpensive: { Name = "A", Price = 300 }
```

#### MaxBy の内部ロジック

全件を 1 パスで走査し、最大のキーとその要素を随時更新する。

```csharp
var maxElement = enumerator.Current;
var maxKey = keySelector(maxElement);

while (enumerator.MoveNext())
{
    var currentElement = enumerator.Current;
    var currentKey = keySelector(currentElement);

    if (comparer.Compare(currentKey, maxKey) > 0) // 現在のキーが最大値を更新するか
    {
        maxElement = currentElement;
        maxKey = currentKey;
    }
}

return maxElement;
```

`OrderByDescending(...).FirstOrDefault()` のような全件ソート（$O(n \log n)$）が不要で、1 パスの線形探索（$O(n)$）で済む。
`MinBy` は比較条件が `> 0` から `< 0` に変わるだけで、構造は同一である。

---

### `DistinctBy<T, TKey>`（キー基準の重複除去）

元の `Distinct()` が要素そのものの一意性を基準にするのに対し、`DistinctBy` は**指定したキーの一意性**を基準に絞り込む。

```csharp
var products = new[]
{
    new { Name = "A", Category = "Food" },
    new { Name = "B", Category = "Tech" },
    new { Name = "C", Category = "Food" },
};

var result = products.DistinctBy(p => p.Category);
// result: { Name = "A", Category = "Food" }, { Name = "B", Category = "Tech" }
```

#### DistinctBy の内部ロジック

`HashSet<TKey>` に対してキーを追加し、追加が成功した（まだ見ていないキーだった）要素のみを流す。

```csharp
var knownKeys = new HashSet<TKey>(comparer); // O(1) でキーの登録・存在確認を行う

foreach (var item in source)
{
    if (knownKeys.Add(keySelector(item))) // 未登録キーなら true が返る
    {
        yield return item; // 最初に現れたものだけを流す
    }
}
```

`HashSet.Add()` は追加に成功した場合 `true`、すでに存在する場合 `false` を返す。
この性質を使い、1 パスの走査で元の順序を保ちながら重複を除去する（空間計算量 $O(\text{ユニーク件数})$）。

---

## 条件付きコンパイルシンボルの選択

本実装では `#if !NET6_0_OR_GREATER` を採用している。
[.NET 5 相当のバックポート記事](/ja/articles/linq-backport-netframework-to-net5/)が `#if !NETCOREAPP` を採用しているのとは異なる。

`NETCOREAPP` シンボルは .NET Core および .NET 5 以降でも定義されるため、.NET 5 向けビルドでポリフィルが無効化されてしまう。
`Chunk`・`MaxBy`・`MinBy`・`DistinctBy` の 4 メソッドは .NET 5 にも存在しないため、`.NET 5` でポリフィルが無効になるとコンパイルエラーが発生する。

| シンボル | .NET Framework | .NET 5 | .NET 6+ |
| --- | --- | --- | --- |
| `!NETCOREAPP` | ポリフィル有効 | **ポリフィル無効（エラー）** | ポリフィル無効 |
| `!NET6_0_OR_GREATER` | ポリフィル有効 | ポリフィル有効 | ポリフィル無効 |

`!NET6_0_OR_GREATER` を使うことで、.NET 5 を含めた .NET 6 未満の環境すべてでポリフィルが有効になり、.NET 6 以降では自動的に本家 LINQ へ切り替わる。

---

## 注意点

- **空のシーケンスに対する `MaxBy` / `MinBy`**: 空のシーケンスが渡された場合、`default` を返す。参照型では `null`、値型（`int` など）では `0` などの既定値になる。呼び出し元で null の可能性がある場合は `?.` で扱うこと。
- **`Chunk` の末尾チャンクのサイズ**: 入力要素数が `size` の倍数でない場合、最後のチャンクは `size` より小さくなる。チャンクが常に一定サイズであることを前提とした呼び出し元の実装は誤りである。
- **`DistinctBy` のキーと `null`**: キーに `null` が含まれる場合、`null` 同士は同一キーとして扱われ、最初に現れた `null` キー要素のみが出力される。
- **`#nullable enable` の適用範囲**: ファイル先頭の `#nullable enable` はファイルスコープで有効化する。プロジェクト全体で `<Nullable>enable</Nullable>` を設定している場合でも、重複して記述することに害はない。

---

## 代替案・比較

| 方法 | メリット | デメリット | 適するケース |
| --- | --- | --- | --- |
| 自作ポリフィル（本記事） | 外部依存なし・コード全体を把握できる | 実装コストがある | 依存を最小化したいプロジェクト |
| MoreLINQ（NuGet） | 豊富なメソッド・テスト済み | 外部ライブラリへの依存が増える | 多数の追加メソッドが必要なプロジェクト |
| .NET 6 へのアップグレード | 根本的解決・パフォーマンス向上も享受できる | 移行コストが発生する | 移行が技術的・ビジネス的に許容できる場合 |

`MoreLINQ`（NuGet パッケージ名: `morelinq`）は `Chunk` や `MaxBy` に相当するメソッドを含む充実したライブラリだが、.NET Framework 環境ではパッケージ管理が複雑になるケースがある。
必要なメソッドが上記 4 件に限定されるなら、外部依存のない自作ポリフィルが保守面でシンプルである。

---

## まとめ

.NET 6 で追加された `Chunk`・`MaxBy`・`MinBy`・`DistinctBy` の 4 メソッドと、.NET Framework 環境へのバックポート手法を解説した。

実装において重要なポイントは以下の 3 点である。

- **メソッドの評価戦略を区別する**: `Chunk`・`DistinctBy`（遅延評価）はパブリックメソッドとイテレータを分離する。`MaxBy`・`MinBy`（先行評価）は分離不要である。
- **`#if !NET6_0_OR_GREATER` を選択する**: .NET 5 にもこれらのメソッドは存在しないため、`!NETCOREAPP` では .NET 5 環境でエラーになる。
- **空シーケンスの戻り値に注意する**: `MaxBy`・`MinBy` は空のシーケンスに対して `default`（参照型は `null`）を返す。

| メソッド | 評価戦略 | 空間計算量 | アルゴリズムの要点 |
| --- | --- | --- | --- |
| `Chunk` | 遅延 | $O(size)$ | チャンクサイズの配列を逐次構築し、端数は `Array.Resize` |
| `MaxBy` / `MinBy` | 先行 | $O(1)$ | 1 パスの線形探索で最大・最小要素を更新 |
| `DistinctBy` | 遅延 | $O(\text{ユニーク件数})$ | `HashSet` でキーを管理し、順序を保って重複除去 |

---

## 関連記事

- [.NET Framework の不足 LINQ メソッドを .NET 5 相当にバックポートする](/ja/articles/linq-backport-netframework-to-net5/)
