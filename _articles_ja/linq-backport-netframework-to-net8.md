---
layout: article-ja
title: ".NET Framework の不足 LINQ メソッドを .NET 8 相当にバックポートする"
date: 2026-07-15
category: C#
excerpt: ".NET 8 で追加されたデリゲート不要の ToDictionary オーバーロード（KeyValuePair 版・タプル版）を、#nullable enable と条件付きコンパイルを活用しながら .NET Framework 環境へ安全にバックポートする実装方法を解説する。"
---

## 概要

.NET Framework から新世代 .NET（.NET 8 以降）への移行を段階的に進めている場合や、諸事情で .NET Framework 環境のコードをメンテナンスし続けなければならない場合、ストレスの原因となるのが「新世代 .NET にはあるのに、.NET Framework には存在しない LINQ メソッド」の存在である。

本記事では、**.NET 8 で新たに追加された `ToDictionary` のオーバーロード**（キーセレクタ不要の `KeyValuePair` 版・タプル版）を整理し、**同一の使用感で動作する拡張メソッド（ポリフィル）を安全に実装する方法**を解説する。
`#nullable enable` を使った現代的な実装と、将来の .NET 8 以降への移行時にコードを無修正で切り替えるための条件付きコンパイル手法も合わせて紹介する。

---

## 前提・対象環境

- フレームワーク: .NET Framework 4.8 / .NET 8+
- 対象: LINQ `ToDictionary` のデリゲート不要オーバーロード 4 シグネチャ（`KeyValuePair` 版・タプル版、それぞれ `IEqualityComparer<TKey>` オーバーロードを含む）
- 方針: `#nullable enable` を適用し、`#if !NET8_0_OR_GREATER` による条件付きコンパイルで移行時に自動無効化する
- プロジェクト設定（`.csproj` の言語バージョンなど）は変更しない

---

## 問題

.NET 8 では、公開 `Enumerable` に新しい LINQ 演算子はほとんど追加されていない。
実質的な追加は、キーセレクタ・値セレクタを渡さずに済む `ToDictionary` のオーバーロード群であり、これらは .NET Framework 環境では使用できない。

| メソッド | 追加されたバージョン | 概要 |
| --- | --- | --- |
| `ToDictionary<TKey, TValue>(this IEnumerable<KeyValuePair<TKey, TValue>>)` | .NET 8.0 | `KeyValuePair` のシーケンスをそのまま辞書へ変換する |
| `ToDictionary<TKey, TValue>(this IEnumerable<(TKey, TValue)>)` | .NET 8.0 | 2 要素タプルのシーケンスをそのまま辞書へ変換する |

いずれも `IEqualityComparer<TKey>` を受け取るオーバーロードを持ち、合計 4 シグネチャとなる。

これらが存在しない .NET Framework 環境では、要素がすでに `KeyValuePair` やタプルの形になっている場合でも、恒等的なセレクタを明示する既存の `ToDictionary` を書く必要がある。

- `pairs.ToDictionary()` の代わりに、`pairs.ToDictionary(p => p.Key, p => p.Value)` と書く
- タプルのシーケンスに対しても `items.ToDictionary(t => t.Item1, t => t.Item2)` と書く

`p => p.Key` / `p => p.Value` は変換の本質ではない定型記述であり、キーと値のセレクタを取り違える軽微なミスの温床にもなる。

---

## 原因・背景

.NET 6 で `Chunk`・`MaxBy`・`MinBy`・`DistinctBy`、.NET 7 で `Order`・`OrderDescending` が追加された一方、.NET 8 では公開 `Enumerable` への新しい演算子追加はほぼ行われなかった。
唯一の実用的な追加が、`KeyValuePair` またはタプルのシーケンスをセレクタなしで辞書へ変換する `ToDictionary` オーバーロードである。

これらは .NET 8.0 で初めて追加されたものであり、.NET 7 以前の環境には存在しない。
`Dictionary` をフィルタリングして再構築する、あるいは `Select` が返したタプルを辞書化するといった場面では、要素がすでにキーと値の対になっている。
その場合に恒等的なセレクタを書かせられていた冗長さを解消するため、専用オーバーロードとして標準化された。

なお、.NET Framework から .NET 5 の間に追加されたメソッド（`Append`・`Prepend`・`TakeLast`・`SkipLast`）については[別記事](/ja/articles/linq-backport-netframework-to-net5/)で、.NET 6 で追加された 4 メソッドについては[別記事](/ja/articles/linq-backport-netframework-to-net6/)で、.NET 7 で追加された `Order`・`OrderDescending` については[別記事](/ja/articles/linq-backport-netframework-to-net7/)で解説している。

---

## 解決方法

本家 LINQ と同じ名前空間（`System.Linq`）に拡張メソッドを定義することで、既存のソースファイルに手を加えることなく透過的に利用できる。

条件付きコンパイル `#if !NET8_0_OR_GREATER` を使い、.NET 8 以降の環境ではこのファイルを丸ごとスキップするよう仕込む。
将来のフレームワークアップグレード時に、ファイルの削除やコードの書き換えを行わずに自動的に本家 LINQ へ切り替わる。

実装の要点は 2 つある。
1 つは戻り値型を `Dictionary<TKey, TValue>` にすること、もう 1 つは `where TKey : notnull` 制約を本家と一致させることである。
本家のオーバーロードはこの制約を持つため、ポリフィル側でも同じ制約を付けることで、移行前後で `null` 許容参照型の解析結果が一致する。

---

## 実装例

以下は 4 シグネチャ（`KeyValuePair` 版・タプル版、それぞれ `IEqualityComparer<TKey>` オーバーロードを含む）のポリフィル実装一式である。
`ICollection<T>` を実装するソースでは要素数から辞書の容量をあらかじめ確保し、本家と同様に不要な再ハッシュを避ける。
プロジェクトに `LinqExtensions.Net8.cs` などの名前でそのまま追加して使用できる。

```csharp
#nullable enable

using System;
using System.Collections.Generic;

#if !NET8_0_OR_GREATER // .NET 8.0 以降ではない環境（.NET Framework など）のみ有効化

namespace System.Linq
{
    /// <summary>
    /// .NET 8.0 で追加された LINQ メソッドを古いターゲットフレームワーク向けに補完する拡張メソッドを提供します。
    /// </summary>
    public static partial class LinqExtensions
    {
        // ==========================================
        // 1. IEnumerable<KeyValuePair<TKey, TValue>>.ToDictionary
        // ==========================================
        public static Dictionary<TKey, TValue> ToDictionary<TKey, TValue>(
            this IEnumerable<KeyValuePair<TKey, TValue>> source)
            where TKey : notnull
            => source.ToDictionary(comparer: null); // comparer 名前付き引数で本オーバーロードに解決させる

        public static Dictionary<TKey, TValue> ToDictionary<TKey, TValue>(
            this IEnumerable<KeyValuePair<TKey, TValue>> source,
            IEqualityComparer<TKey>? comparer)
            where TKey : notnull
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            var dictionary = source is ICollection<KeyValuePair<TKey, TValue>> collection
                ? new Dictionary<TKey, TValue>(collection.Count, comparer)
                : new Dictionary<TKey, TValue>(comparer);

            foreach (var pair in source)
            {
                dictionary.Add(pair.Key, pair.Value);
            }

            return dictionary;
        }

        // ==========================================
        // 2. IEnumerable<(TKey, TValue)>.ToDictionary
        // ==========================================
        public static Dictionary<TKey, TValue> ToDictionary<TKey, TValue>(
            this IEnumerable<(TKey Key, TValue Value)> source)
            where TKey : notnull
            => source.ToDictionary(comparer: null);

        public static Dictionary<TKey, TValue> ToDictionary<TKey, TValue>(
            this IEnumerable<(TKey Key, TValue Value)> source,
            IEqualityComparer<TKey>? comparer)
            where TKey : notnull
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            var dictionary = source is ICollection<(TKey Key, TValue Value)> collection
                ? new Dictionary<TKey, TValue>(collection.Count, comparer)
                : new Dictionary<TKey, TValue>(comparer);

            foreach (var pair in source)
            {
                dictionary.Add(pair.Key, pair.Value);
            }

            return dictionary;
        }
    }
}

#endif
```

コンパイル時に `NET8_0_OR_GREATER` シンボルが定義されていない環境（.NET Framework を含む .NET 8 未満の環境）でのみ、上記クラスが有効になる。

---

## 各メソッドの詳解

### `KeyValuePair` のシーケンスからの変換

既存の辞書をフィルタリングして新しい辞書を作る場面では、要素がそのまま `KeyValuePair` になっている。
セレクタを書かずに `ToDictionary()` を呼ぶだけで、キーと値の対応を保ったまま新しい辞書へ変換できる。

```csharp
var source = new Dictionary<string, int>
{
    ["apple"] = 3,
    ["banana"] = 5,
    ["cherry"] = 2,
};

// 値が 3 以上の要素だけを残して新しい辞書を作る
var filtered = source.Where(pair => pair.Value >= 3)
                     .ToDictionary();
// filtered: { "apple": 3, "banana": 5 }
```

`Dictionary<TKey, TValue>` は `IEnumerable<KeyValuePair<TKey, TValue>>` を実装するため、`Where` の戻り値は `KeyValuePair` のシーケンスになる。
そのセレクタなしの `ToDictionary()` は、`pair => pair.Key` / `pair => pair.Value` を明示する必要をなくす。

### 2 要素タプルのシーケンスからの変換

`Select` が 2 要素タプルを返す場合も、そのまま辞書化できる。

```csharp
var files = new[] { "report.pdf", "photo.jpg", "notes.txt" };

// ファイル名をキー、拡張子を値にする
var byName = files.Select(name => (name, System.IO.Path.GetExtension(name)))
                  .ToDictionary();
// byName: { "report.pdf": ".pdf", "photo.jpg": ".jpg", "notes.txt": ".txt" }
```

タプルの第 1 要素がキー、第 2 要素が値として扱われる。
タプルの要素名（`(name, ext)` のような命名）は型に影響しないため、名前の有無にかかわらず動作する。

### `IEqualityComparer<TKey>` による比較方法の指定

キーの等価比較を差し替えるオーバーロードでは、大文字・小文字を区別しない辞書などを構築できる。

```csharp
var pairs = new[] { ("Alpha", 1), ("BETA", 2) };

var dictionary = pairs.ToDictionary(StringComparer.OrdinalIgnoreCase);

bool found = dictionary.ContainsKey("alpha"); // true
```

比較子を渡さないオーバーロードは `EqualityComparer<TKey>.Default` を使う。
比較子を差し替えても戻り値型は `Dictionary<TKey, TValue>` のまま変わらない。

### 即時評価であること

`ToDictionary` は `Where` や `Order` のような遅延評価ではなく、**即時評価**である。
呼び出した時点でソースを最後まで列挙し、辞書を構築して返す。

```csharp
var query = Enumerable.Range(1, 3).Select(n => (n, n * n));

var dictionary = query.ToDictionary(); // ここでソースが列挙される
// dictionary: { 1: 1, 2: 4, 3: 9 }
```

このため、ソースが遅延評価のクエリであっても、`ToDictionary` の呼び出しで確定した辞書が得られる。
後続でソースの元データが変化しても、生成済みの辞書には影響しない。

---

## 条件付きコンパイルシンボルの選択

本実装では `#if !NET8_0_OR_GREATER` を採用している。
[.NET 5 相当のバックポート記事](/ja/articles/linq-backport-netframework-to-net5/)が `#if !NETCOREAPP` を採用しているのとは異なる。

これらの `ToDictionary` オーバーロードは .NET 7 以前には存在しないため、`NETCOREAPP` や `NET7_0_OR_GREATER` を条件に使うと、.NET 6 や .NET 7 向けビルドでポリフィルが無効化され、コンパイルエラーが発生する。

| シンボル | .NET Framework | .NET 7 | .NET 8+ |
| --- | --- | --- | --- |
| `!NETCOREAPP` | ポリフィル有効 | **ポリフィル無効（エラー）** | ポリフィル無効 |
| `!NET7_0_OR_GREATER` | ポリフィル有効 | **ポリフィル無効（エラー）** | ポリフィル無効 |
| `!NET8_0_OR_GREATER` | ポリフィル有効 | ポリフィル有効 | ポリフィル無効 |

`!NET8_0_OR_GREATER` を使うことで、.NET 7 を含めた .NET 8 未満の環境すべてでポリフィルが有効になり、.NET 8 以降では自動的に本家 LINQ へ切り替わる。

---

## 注意点

- **既存の `ToDictionary` との使い分け**: 本オーバーロードは、要素がすでに `KeyValuePair` またはタプルの形になっている場合にのみ有効である。任意の要素型 `TSource` から辞書を作る場合は、従来どおりキーセレクタ（必要なら値セレクタ）を渡す既存の `ToDictionary` を使う。要素が対になっていない場面で無理に本オーバーロードへ寄せる必要はない。
- **名前衝突は起きない**: 本ポリフィルは `Func<...>` を第 1 引数に取る既存の `ToDictionary` とはシグネチャが異なるため、オーバーロード解決で衝突しない。セレクタを渡す呼び出しは従来どおり本家の実装へ、要素が `KeyValuePair` またはタプルでセレクタを渡さない呼び出しは本ポリフィルへ解決される。実装内でパラメータなしのオーバーロードから比較子オーバーロードへ委譲する際は、`comparer:` の名前付き引数を用いて意図した先へ確実に解決させている。
- **即時評価である**: 前述のとおり `ToDictionary` は即時評価であり、`source` を最後まで列挙する。無限シーケンスや、列挙のたびに副作用を起こすソースには使わない。`source` が `null` の場合の `ArgumentNullException` は呼び出し時点で即座に投げられる。
- **キーの重複は例外になる**: 実装は `Dictionary<TKey, TValue>.Add` を用いるため、キーが重複すると `ArgumentException` が発生する。この挙動は本家の `ToDictionary` と一致する。重複を許容して後勝ちにしたい場合は、`ToDictionary` ではなく手動で `dictionary[key] = value` を用いる方法を検討する。
- **キーの `null` は例外になる**: キーに `null` が現れると `ArgumentNullException` が発生する。`where TKey : notnull` 制約により参照型のキーは非 `null` が前提となるが、実行時に `null` が混入した場合はこの例外で検出される。

---

## 代替案・比較

| 方法 | メリット | デメリット | 適するケース |
| --- | --- | --- | --- |
| 自作ポリフィル（本記事） | 外部依存なし・戻り値型と制約を本家に合わせられる | 実装・保守の手間がある | 依存を最小化したいプロジェクト |
| `ToDictionary(p => p.Key, p => p.Value)` を直書き | 追加コード不要 | 記述が冗長・移行時に一括置換が必要 | 使用箇所が少なく移行予定もない場合 |
| .NET 8 へのアップグレード | 根本的解決・言語機能も享受できる | 移行コストが発生する | 移行が技術的・ビジネス的に許容できる場合 |

セレクタの直書きは追加コードこそ不要だが、後日 .NET 8 へ移行してセレクタなしの `ToDictionary` に統一する際に、使用箇所を洗い出して置換する手間が残る。
本記事のポリフィルを導入しておけば、移行前からセレクタなしで記述でき、移行時にはファイルを残したまま条件付きコンパイルが自動で本家へ切り替える。

---

## まとめ

.NET 8 で追加されたデリゲート不要の `ToDictionary` オーバーロードと、.NET Framework 環境へのバックポート手法を解説した。

実装において重要なポイントは以下の 3 点である。

- **戻り値型と制約を本家に合わせる**: 戻り値型を `Dictionary<TKey, TValue>`、制約を `where TKey : notnull` とすることで、移行前後の `null` 許容解析を一致させる。
- **`#if !NET8_0_OR_GREATER` を選択する**: .NET 7 以前にはこれらのオーバーロードが存在しないため、`!NETCOREAPP` や `!NET7_0_OR_GREATER` では .NET 7 環境でエラーになる。
- **即時評価と重複キーの挙動を把握する**: `ToDictionary` は即時評価であり、キーの重複や `null` は例外になる。要素が対になっていない場合は従来のセレクタ付き `ToDictionary` を使う。

| メソッド | 評価戦略 | 戻り値型 | 適する入力 |
| --- | --- | --- | --- |
| `ToDictionary`（`KeyValuePair` 版） | 即時 | `Dictionary<TKey, TValue>` | `KeyValuePair` のシーケンス |
| `ToDictionary`（タプル版） | 即時 | `Dictionary<TKey, TValue>` | 2 要素タプルのシーケンス |

---

## 関連記事

- [.NET Framework の不足 LINQ メソッドを .NET 7 相当にバックポートする](/ja/articles/linq-backport-netframework-to-net7/)
- [.NET Framework の不足 LINQ メソッドを .NET 6 相当にバックポートする](/ja/articles/linq-backport-netframework-to-net6/)
- [.NET Framework の不足 LINQ メソッドを .NET 5 相当にバックポートする](/ja/articles/linq-backport-netframework-to-net5/)
