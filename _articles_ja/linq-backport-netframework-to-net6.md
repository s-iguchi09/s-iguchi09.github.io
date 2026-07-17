---
layout: article-ja
title: "GroupBy と全件ソートによる回避コードをなくす — Chunk・MaxBy・MinBy・DistinctBy の実装"
date: 2026-07-13
category: C#
excerpt: ".NET Framework で Chunk・MaxBy・MinBy・DistinctBy を代用する GroupBy・全件ソートの回避イディオムを実行コストの面から検証し、計算量を改善するポリフィルの実装と、バージョン別シンボルによる移行ガードの選び方を解説する。"
---

## 概要

`OrderByDescending(x => x.Price).First()` や `GroupBy(x => x.Key).Select(g => g.First())` は、.NET Framework の現場コードで繰り返し見かける定型句である。
これらは「キー基準の最大要素」「キー基準の重複除去」を表現するための回避イディオムだが、目的に対して実行コストが釣り合っていない。
最大値が 1 つ欲しいだけなのに全件をソートし、重複を除きたいだけなのにキーごとの要素リストを構築している。

.NET 6 で追加された `Chunk`・`MaxBy`・`MinBy`・`DistinctBy` は、これらの操作を専用メソッドとして提供し、回避イディオムの冗長さと実行コストの両方を解消する。
本記事では、各回避イディオムのコストを確認したうえで、.NET Framework 環境で同じ改善を得るためのポリフィル実装を解説する。
あわせて、このバージョンから必要になる「バージョン別シンボルによる移行ガード」の選び方を整理する。

---

## 前提・対象環境

- フレームワーク: .NET Framework 4.8（バックポート先）/ .NET 6+（将来の移行先）
- 対象: LINQ の 4 メソッド（Chunk / MaxBy / MinBy / DistinctBy）
- 方針: `#nullable enable` を適用し、`#if !NET6_0_OR_GREATER` で移行時に自動無効化する
- 言語バージョン: 実装例は `#nullable enable` と `using var` を用いるため C# 8.0 以上を要する。.NET Framework 4.8 の既定は C# 7.3 のため、`.csproj` の `LangVersion` を `8.0` 以上に設定する（C# 7.3 のまま使う場合は `using var` を通常の `using` へ置き換え、`#nullable enable` を外す）

---

## 問題: 回避イディオムの実行コスト

.NET 6 で追加された以下のメソッドは、.NET Framework 環境では使用できない。

| メソッド名 | 追加されたバージョン | 概要 |
| --- | --- | --- |
| `Chunk<T>` | .NET 6.0 | シーケンスを指定した最大サイズの塊に分割する |
| `MaxBy<T, TKey>` | .NET 6.0 | 指定したキー基準で値が最大の要素を丸ごと取得する |
| `MinBy<T, TKey>` | .NET 6.0 | 指定したキー基準で値が最小の要素を丸ごと取得する |
| `DistinctBy<T, TKey>` | .NET 6.0 | 指定したキーの一意性に基づいて要素を抽出する |

そのため、同じ結果を得るには次のような回避イディオムを書くことになる。

| 目的 | 回避イディオム | 実行コスト |
| --- | --- | --- |
| 最大サイズで分割 | インデックス付き `Select` + `GroupBy(t => t.i / size)` | 初回列挙時に全要素をグルーピングし、中間タプルを割り当てる |
| キー基準の最大・最小 | `OrderByDescending(x => x.Key).First()` | $O(n \log n)$ の全件ソート |
| キー基準の重複除去 | `GroupBy(x => x.Key).Select(g => g.First())` | キーごとの要素リストを丸ごと構築 |

問題は記述の冗長さだけではない。
「1 要素が欲しいだけの操作に全件ソートを払う」「先頭要素しか使わないグループに全要素を保持させる」という、目的とコストの不一致が恒常化する点にある。

---

## 原因・背景

.NET Framework 4.8 の機能追加終了後、.NET Core から .NET 5 までの LINQ は内部性能の改善が中心で、演算子の追加はわずかだった。
これらの操作を 1 メソッドで表現する演算子が揃ったのは .NET 6 が最初であり、.NET 5 以前のどの環境にも存在しない。
`MaxBy`・`MinBy`・`DistinctBy` はキーを基準にした操作、`Chunk` は要素をサイズで分割する操作であり、基準は異なるが、いずれも従来は複数メソッドの組み合わせを要した。
「.NET 5 にも存在しない」という事実は、後述する移行ガードのシンボル選択に直接影響する。

---

## 解決方法

本家と同じ `System.Linq` 名前空間に拡張メソッドを定義し、既存コードに手を加えず透過的に利用できるようにする。
名前空間戦略と「引数検証とイテレータの分離」の根拠は[シリーズ基礎編](/ja/articles/linq-backport-netframework-to-net5/)で解説しているため、本記事では実装固有の論点に絞る。

実装固有の論点は 2 つある。
1 つは、4 メソッドの評価戦略が遅延（`Chunk`・`DistinctBy`）と先行（`MaxBy`・`MinBy`）に分かれるため、メソッド構成をそれぞれに合わせること。
もう 1 つは、移行ガードに `!NETCOREAPP` ではなく `!NET6_0_OR_GREATER` を使うことである。

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
                // 参照型・null 許容値型では default(= null) を返し、非 null 値型のみ例外を投げる（本家 .NET 6 と同一）
                if (default(TSource) is null) return default;
                throw new InvalidOperationException("Sequence contains no elements.");
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
                // 参照型・null 許容値型では default(= null) を返し、非 null 値型のみ例外を投げる（本家 .NET 6 と同一）
                if (default(TSource) is null) return default;
                throw new InvalidOperationException("Sequence contains no elements.");
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

## 回避イディオムとの置き換え効果

### `MaxBy` / `MinBy`: 全件ソートから 1 パス走査へ

回避イディオムの `OrderByDescending(...).First()` は、1 要素を得るために全件を並び替える。

```csharp
var products = new[]
{
    new { Name = "A", Price = 300 },
    new { Name = "B", Price = 100 },
    new { Name = "C", Price = 200 },
};

// 回避イディオム: O(n log n) の全件ソート
var before = products.OrderByDescending(p => p.Price).First();

// MaxBy: O(n) の 1 パス走査
var after = products.MaxBy(p => p.Price);
// after: { Name = "A", Price = 300 }
```

ポリフィルの内部は、最大のキーとその要素を更新しながら全件を 1 回だけ走査する。

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

ソートが不要になるため計算量は $O(n \log n)$ から $O(n)$ に下がり、ソート用の作業領域も不要になる。
`MinBy` は比較条件が `> 0` から `< 0` に変わるだけで構造は同一である。
なお `Max()` / `Min()` がキーの値そのものを返すのに対し、`MaxBy` / `MinBy` はキーに対応する**元の要素**を返す。

### `DistinctBy`: グループ構築から `HashSet` 判定へ

回避イディオムの `GroupBy(...).Select(g => g.First())` は、先頭しか使わないグループに全要素を保持させる。

```csharp
var products = new[]
{
    new { Name = "A", Category = "Food" },
    new { Name = "B", Category = "Tech" },
    new { Name = "C", Category = "Food" },
};

// 回避イディオム: キーごとの要素リストを構築してから先頭を取る
var before = products.GroupBy(p => p.Category).Select(g => g.First());

// DistinctBy: キーの既出判定だけで流す
var after = products.DistinctBy(p => p.Category);
// after: { Name = "A", Category = "Food" }, { Name = "B", Category = "Tech" }
```

ポリフィルの内部は `HashSet<TKey>` にキーを追加し、追加が成功した（未出現の）要素だけを流す。

```csharp
var knownKeys = new HashSet<TKey>(comparer); // O(1) でキーの登録・存在確認を行う

foreach (var item in source)
{
    if (knownKeys.Add(keySelector(item))) // 未登録キーなら true が返る
    {
        yield return item;
    }
}
```

保持するのはキーだけであり、要素本体をため込まない（空間計算量 $O(\text{ユニーク件数})$）。
`GroupBy` と違って全件を先読みせず、元の順序を保ったまま 1 件ずつ流れる遅延評価になる点も実用上の差である。

### `Chunk`: インデックス演算のグルーピングから逐次分割へ

インデックスを `size` で割ってグルーピングする回避イディオムは、中間タプルの割り当てを伴い、`GroupBy` が初回列挙時にソース全体を読み込む。
`Chunk` は列挙しながら「最大 `size` 個」の配列を順に切り出す。

```csharp
var result = new[] { 1, 2, 3, 4, 5 }.Chunk(2);
// result: [1, 2], [3, 4], [5]
```

ポリフィルの内部は、`size` 個分の配列を確保してデータを詰め、端数チャンクのみ `Array.Resize` で切り詰める。

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

チャンクを 1 つ返すたびに次のチャンクを構築するため、全件を先読みするグルーピングが不要になる。

---

## 評価戦略の違いによる実装差

4 メソッドは評価戦略が 2 種類に分かれ、メソッド構成もそれに従う。

**遅延評価（`Chunk` / `DistinctBy`）** は `yield return` を使うイテレータであり、列挙されるまでデータを 1 件も処理しない。
`yield return` を含むメソッドは引数チェックまで遅延してしまうため、public メソッドと `~Iterator` メソッドを分離して例外を即時化する。
この分離の根拠と失敗例は[シリーズ基礎編の設計原則 1](/ja/articles/linq-backport-netframework-to-net5/)で詳しく説明している。

**先行評価（`MaxBy` / `MinBy`）** は `yield return` を使わず、呼び出しと同時に全件を走査して単一の結果を返す。
引数チェックも呼び出し時点で実行されるため、メソッドの分離は不要である。

戻り値がシーケンスか単一要素かで評価戦略が決まり、評価戦略がメソッド構成を決める。
ポリフィルを自作する際は、この順で構成を判断する。

---

## 移行ガード: バージョン別シンボルが必要になる最初のケース

[シリーズ基礎編](/ja/articles/linq-backport-netframework-to-net5/)で使った `#if !NETCOREAPP` は、このバージョンのバックポートには使えない。
`NETCOREAPP` シンボルは .NET Core と .NET 5 でも定義されるため、`!NETCOREAPP` でガードすると .NET 5 向けビルドでポリフィルが無効化される。
`Chunk` 以下の 4 メソッドは .NET 5 に存在しないので、その時点でコンパイルエラーになる。

| シンボル | .NET Framework | .NET 5 | .NET 6+ |
| --- | --- | --- | --- |
| `!NETCOREAPP` | ポリフィル有効 | **ポリフィル無効（エラー）** | ポリフィル無効 |
| `!NET6_0_OR_GREATER` | ポリフィル有効 | ポリフィル有効 | ポリフィル無効 |

正しい条件は「対象メソッドが追加されたバージョン以上で無効化する」であり、本実装では `#if !NET6_0_OR_GREATER` を使う。
一般化すると、バックポート対象が .NET X で追加されたメソッドなら `#if !NETX_0_OR_GREATER` を選ぶ。
以降のバージョンのバックポート（.NET 7 の `Order` など）でも、この規則をそのまま適用する。

---

## 注意点

- **空のシーケンスに対する `MaxBy` / `MinBy`**: 空のシーケンスが渡された場合、要素型が参照型または null 許容値型なら `default`（= `null`）を返し、非 null 値型（`int` や `struct` など）でのみ `InvalidOperationException` を投げる。これは .NET 6 本家の挙動と同一である（戻り値型も `TSource?`）。非 null 値型で空になりうる場合は、事前に `.Any()` で要素の存在を確認するか、`try-catch` で対処すること。
- **`Chunk` の末尾チャンクのサイズ**: 入力要素数が `size` の倍数でない場合、最後のチャンクは `size` より小さくなる。チャンクが常に一定サイズであることを前提とした呼び出し元の実装は誤りである。
- **`DistinctBy` のキーと `null`**: キーに `null` が含まれる場合、`null` 同士は同一キーとして扱われ、最初に現れた `null` キー要素のみが出力される。
- **`#nullable enable` の適用範囲**: ファイル先頭の `#nullable enable` はファイルスコープで有効化する。プロジェクト全体で `<Nullable>enable</Nullable>` を設定している場合でも、重複して記述することに害はない。

---

## 代替案・比較

| 方法 | メリット | デメリット | 適するケース |
| --- | --- | --- | --- |
| 自作ポリフィル（本記事） | 外部依存なし・回避イディオムの計算量問題も解消 | 実装コストがある | 依存を最小化したいプロジェクト |
| 回避イディオムの継続 | 追加コード不要 | 計算量・割り当ての無駄が残り続ける | 対象データが常に少件数の場合 |
| MoreLINQ（NuGet） | 豊富なメソッド・テスト済み | 外部ライブラリへの依存が増える | 多数の追加メソッドが必要なプロジェクト |
| .NET 6 へのアップグレード | 根本的解決・性能改善も享受できる | 移行コストが発生する | 移行が技術的・ビジネス的に許容できる場合 |

`MoreLINQ`（NuGet パッケージ名: `morelinq`）は `Chunk` や `MaxBy` に相当するメソッドを含む充実したライブラリだが、必要なメソッドが本記事の 4 件に限られるなら、外部依存のない自作ポリフィルのほうが保守はシンプルである。

---

## まとめ

回避イディオムを使い続けるか、ポリフィルへ置き換えるかの判断基準は「そのコードがどれだけの件数を、どれだけの頻度で処理するか」である。
数十件のデータを画面表示のたびに処理する程度なら回避イディオムでも問題は表面化しないが、件数と頻度が増えるほど、全件ソートや中間グループ構築のコストは無視できなくなる。

| メソッド | 回避イディオムのコスト | ポリフィル後のコスト |
| --- | --- | --- |
| `MaxBy` / `MinBy` | $O(n \log n)$（全件ソート） | $O(n)$（1 パス走査） |
| `DistinctBy` | キーごとの要素リスト構築 | キー集合のみ保持（$O(\text{ユニーク件数})$） |
| `Chunk` | 全件を先読みするグルーピング | チャンク単位の逐次構築（$O(size)$） |

置き換え自体は本家と同名・同シグネチャのポリフィルを追加するだけで済み、`#if !NET6_0_OR_GREATER` のガードにより .NET 6 移行時には自動で本家実装に切り替わる。

---

## 関連記事

- [遅延評価を壊さない LINQ ポリフィルの設計原則 — Append・Prepend・TakeLast・SkipLast の実装](/ja/articles/linq-backport-netframework-to-net5/)
- [委譲だけで作る Order・OrderDescending — IOrderedEnumerable 互換の最小ポリフィル](/ja/articles/linq-backport-netframework-to-net7/)
- [セレクタなし ToDictionary の実現 — オーバーロード解決と notnull 制約の設計](/ja/articles/linq-backport-netframework-to-net8/)
- [GroupBy を経由しないキー集計 — CountBy・AggregateBy・Index の辞書ベース実装](/ja/articles/linq-backport-netframework-to-net9/)
- [SQL の外部結合を LINQ で表現する — LeftJoin・RightJoin・Shuffle の実装](/ja/articles/linq-backport-netframework-to-net10/)
