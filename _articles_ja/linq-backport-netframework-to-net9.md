---
layout: article-ja
title: "GroupBy を経由しないキー集計 — CountBy・AggregateBy・Index の辞書ベース実装"
date: 2026-07-16
category: C#
excerpt: "キーごとの集計で GroupBy が抱える中間グルーピングの割り当てコストを整理し、.NET 9 の CountBy・AggregateBy・Index を辞書への直接集計で .NET Framework に実装する方法と、GroupBy 委譲実装との挙動差を解説する。"
---

## 概要

キーごとの件数が欲しいだけなのに、`GroupBy` は各キーに対応する要素リストを丸ごとメモリ上に構築する。
集計結果として最終的に残るのは「キーと数値の対」だけであり、グルーピングされた要素本体は捨てられる。
.NET 9 で追加された `CountBy`・`AggregateBy` は、この中間グルーピングを省き、キーごとの状態だけを辞書に保持して集計する演算子である。
あわせて追加された `Index` は、`Select((x, i) => …)` で書いていたインデックス付き列挙を専用メソッド化したものである。

本記事では、これら 3 メソッドを .NET Framework 環境へバックポートする。
主題は「ポリフィルの内部を `GroupBy` への委譲で済ませず、本家と同じ辞書ベースの集計で実装する」ことの意味である。
委譲実装と辞書実装ではメモリ効率だけでなく `null` キーの挙動まで変わるため、両者の差を比較しながら実装を組み立てる。

---

## 前提・対象環境

- フレームワーク: .NET Framework 4.8（バックポート先）/ .NET 9+（将来の移行先）
- 対象: LINQ の 3 メソッド（CountBy / AggregateBy / Index）と各オーバーロード
- 方針: `#nullable enable` を適用し、`#if !NET9_0_OR_GREATER` で移行時に自動無効化する
- 言語バージョン: `#nullable enable` と `where TKey : notnull` は C# 8.0 以上を必要とする。.NET Framework 4.8 の既定は C# 7.3 のため、`.csproj` の `LangVersion` を `8.0` 以上に設定する

---

## 問題: `GroupBy` 集計の隠れた割り当て

.NET 9 で追加された以下のメソッドは、.NET Framework 環境では使用できない。

| メソッド | 追加されたバージョン | 概要 |
| --- | --- | --- |
| `CountBy` | .NET 9.0 | キーごとに要素数を数え、`KeyValuePair<TKey, int>` の列として返す |
| `AggregateBy` | .NET 9.0 | キーごとに累積計算を行い、`KeyValuePair<TKey, TAccumulate>` の列として返す |
| `Index` | .NET 9.0 | 各要素にインデックスを付与し、`(int Index, TSource Item)` タプルの列として返す |

これらが無い環境で同等の集計を書くと、`GroupBy` を経由することになる。

```csharp
// キーごとの件数を GroupBy で集計する
var counts = words.GroupBy(w => w)
                  .Select(g => new { g.Key, Count = g.Count() });
```

このコードの見えないコストが中間グルーピングである。
`GroupBy` は列挙開始時に全要素を走査し、キーごとに「そのキーに属する全要素への参照リスト」を構築する。
件数を数えるだけの用途では各グループの要素リストは最終的に捨てられるが、`Count()` を呼んだ直後に解放されるわけではなく、`GroupBy` が返すシーケンス全体の列挙が完了する（全キーのグループを構築し終える）まで内部に保持され続ける。
要素数 100 万件・キー数 10 件のデータであれば、10 件の結果を得る間ずっと、100 万件分の参照を保持するグルーピングがメモリ上に残る。

`Index` の代替も同様に、`Select((item, index) => (index, item))` という定型記述を毎回書くことになる。
こちらはコストの問題ではなく、意図（インデックスが欲しいだけ）が記述に埋もれる問題である。

---

## 原因・背景

`GroupBy` が要素リストを構築するのは、`IGrouping<TKey, TSource>` として「キーごとの要素列」を返す契約だからであり、`GroupBy` 自体の欠陥ではない。
問題は、集計しか必要ない場面でもこの契約のコストを払っていたことにある。

.NET 9 の `CountBy`・`AggregateBy` は「キーごとの状態（件数や累積値）だけを保持すればよい」という集計専用の契約を導入し、内部では `Dictionary` にキーごとの状態のみを積んで結果を返す。
この内部辞書の性質上、本家はいずれも `where TKey : notnull` 制約を課し、キーに `null` が渡ると列挙時に例外を投げる。

---

## 解決方法: `GroupBy` 委譲ではなく辞書への直接集計

ポリフィルの内部実装には 2 つの選択肢がある。

```csharp
// 選択肢 A: GroupBy へ委譲する素朴な実装
public static IEnumerable<KeyValuePair<TKey, int>> CountBy<TSource, TKey>(
    this IEnumerable<TSource> source, Func<TSource, TKey> keySelector)
    => source.GroupBy(keySelector)
             .Select(g => new KeyValuePair<TKey, int>(g.Key, g.Count()));
```

選択肢 A は短く書けるが、2 つの点で本家と一致しない。

| 観点 | GroupBy 委譲（A） | 辞書への直接集計（B・本記事） | 本家 .NET 9 |
| --- | --- | --- | --- |
| メモリ | キーごとの要素リストを構築 | キーごとの状態のみ保持 | キーごとの状態のみ保持 |
| `null` キー | 1 グループとして許容 | 列挙時に `ArgumentNullException` | 列挙時に `ArgumentNullException` |

`GroupBy` は `null` キーを 1 つのグループとして受け入れるため、委譲実装は本家なら例外になる入力を黙って通してしまう。
ポリフィルの目的が「移行時に挙動を変えないこと」である以上、メモリ効率だけでなく例外挙動の一致という点でも、本家と同じ辞書ベースの実装（選択肢 B)を採る。

あわせて、遅延評価を保ったまま引数検証を即時化するため、public メソッドと `yield return` を含むイテレータ本体を分離する（根拠は[シリーズ基礎編の設計原則 1](/ja/articles/linq-backport-netframework-to-net5/)を参照）。
`Index` だけは集計を伴わないため、`Select` のインデックス付きオーバーロードへの委譲で足りる。

---

## 実装例

以下は 3 メソッド（`AggregateBy` の `seed` 版・`seedSelector` 版を含む計 4 シグネチャ）のポリフィル実装一式である。
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
`AggregateBy` の `seed` 版は、初期値を返すだけの `seedSelector`（`key => seed`）に読み替えて共通の集計本体へ委譲している。

---

## 各メソッドの動作

### `CountBy`: キーごとの件数

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

結果の列挙順は保証されない。
内部で用いる `Dictionary<TKey, TValue>` の列挙順は API 契約として規定されていないため、特定の順序（初出順など）に依存してはならない。
決まった順序が必要な場合は、結果を明示的に並べ替えるか、順序を保持するコレクションへ変換する。
`IEqualityComparer<TKey>` を渡すオーバーロードでは、大文字・小文字を区別しない集計などに比較方法を差し替えられる。

### `AggregateBy`: キーごとの畳み込み

`AggregateBy` は `Aggregate` のキー別版であり、キーごとに独立した累積計算を行う。
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

### `Index`: インデックス付き列挙

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

### 遅延評価と例外のタイミング

3 メソッドはいずれも遅延評価であり、`foreach` や `.ToList()` の時点で初めて計算が実行される。
一方、`source` や各デリゲートが `null` の場合の `ArgumentNullException` は、呼び出し時点で即座に投げられる。

```csharp
IEnumerable<int> numbers = null!;

// 列挙する前に、この行で即座に ArgumentNullException が投げられる
var query = numbers.CountBy(n => n);
```

キーセレクタが列挙中に `null` キーを返した場合の `ArgumentNullException` は、内部辞書へそのキーで参照・格納しようとした時点（`TryGetValue` の呼び出し）で投げられる。
これは本家 .NET 9 の挙動（内部辞書が `null` キーを拒否する）と一致する。

---

## 移行ガード

本ポリフィルは `#if !NET9_0_OR_GREATER` で囲む。
`CountBy`・`AggregateBy`・`Index` は .NET 8 以前に存在しないため、`!NETCOREAPP` や `!NET8_0_OR_GREATER` を条件にすると .NET 8 向けビルドでポリフィルが無効化され、コンパイルエラーになる。
シンボル選択の一般規則（追加されたバージョン以上で無効化する）は[.NET 6 メソッドのバックポート記事](/ja/articles/linq-backport-netframework-to-net6/)で整理している。

---

## 注意点

- **結果は遅延列であり、列挙のたびに再集計される**: 戻り値は `Dictionary` ではなく遅延列である。同じ結果を複数回使う場合は `.ToList()` や `.ToDictionary()` で実体化しないと、そのつどソースの走査と集計をやり直す。
- **キー型は `notnull`**: 本家と同じく `where TKey : notnull` 制約を付けており、実行時に `null` キーが現れた場合は列挙時に `ArgumentNullException` を投げる。`null` キーを集計対象にしたいデータでは、キーを事前に変換（`?? "(none)"` など）してから渡す。
- **`CountBy` のオーバーフロー**: 計数は `checked(count + 1)` で行うため、あるキーの件数が `int.MaxValue` を超えると `OverflowException` を投げる。本家 .NET 9 も `checked` で加算するため、この挙動は一致する。
- **`Index` とタプル順序 / 型名の紛らわしさ**: `Index` が返すタプルは `(Index, Item)` の順であり、インデックスが先である。また、メソッド名 `Index` は C# 8 で導入された `System.Index` 型と名称が紛らわしいが、型とメソッドであり衝突はしない。

---

## 代替案・比較

| 方法 | メリット | デメリット | 適するケース |
| --- | --- | --- | --- |
| 辞書ベースのポリフィル（本記事） | 本家と同じメモリ特性・例外挙動 | 実装がやや長い | 移行時に挙動を変えたくない場合 |
| `GroupBy` への委譲ポリフィル | 実装が数行で済む | 中間グルーピングを割り当てる・`null` キーの挙動が本家と異なる | 挙動差を理解したうえで暫定利用する場合 |
| `GroupBy(...).Select(...)` を直書き | 追加コード不要 | 記述が冗長・移行時に一括置換が必要 | 使用箇所が少なく移行予定もない場合 |
| .NET 9 へのアップグレード | 根本的解決・割り当て削減の恩恵を受けられる | 移行コストが発生する | 移行が技術的・ビジネス的に許容できる場合 |

---

## まとめ

`CountBy`・`AggregateBy` のバックポートで問われるのは、「同じ結果を返すこと」ではなく「同じやり方で返すこと」である。
`GroupBy` へ委譲しても結果の値は一致するが、中間グルーピングの割り当てと `null` キーの許容という 2 点で本家から乖離する。
辞書への直接集計で実装すれば、メモリ特性・例外挙動・`where TKey : notnull` 制約のすべてが本家と揃い、`#if !NET9_0_OR_GREATER` による移行時の無修正切り替えが安心して成立する。

| メソッド | 実装方式 | 保持する状態 |
| --- | --- | --- |
| `CountBy` | 辞書への直接集計 | キーごとの件数のみ |
| `AggregateBy` | 辞書への直接集計 | キーごとの累積値のみ |
| `Index` | `Select` への委譲 | なし（パススルー） |

大量データのキー集計を .NET Framework で日常的に書いているなら、`GroupBy` 直書きの継続よりも本ポリフィルの導入が割り当て削減と移行準備を兼ねる選択になる。

---

## 関連記事

- [遅延評価を壊さない LINQ ポリフィルの設計原則 — Append・Prepend・TakeLast・SkipLast の実装](/ja/articles/linq-backport-netframework-to-net5/)
- [GroupBy と全件ソートによる回避コードをなくす — Chunk・MaxBy・MinBy・DistinctBy の実装](/ja/articles/linq-backport-netframework-to-net6/)
- [委譲だけで作る Order・OrderDescending — IOrderedEnumerable 互換の最小ポリフィル](/ja/articles/linq-backport-netframework-to-net7/)
- [セレクタなし ToDictionary の実現 — オーバーロード解決と notnull 制約の設計](/ja/articles/linq-backport-netframework-to-net8/)
- [SQL の外部結合を LINQ で表現する — LeftJoin・RightJoin・Shuffle の実装](/ja/articles/linq-backport-netframework-to-net10/)
