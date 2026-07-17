---
layout: article-ja
title: "セレクタなし ToDictionary の実現 — オーバーロード解決と notnull 制約の設計"
date: 2026-07-15
category: C#
excerpt: ".NET 8 の ToDictionary 新オーバーロード（KeyValuePair 版・タプル版）を .NET Framework で再現する際に問われる、既存オーバーロードとの共存・名前付き引数による解決先の固定・notnull 制約の一致というシグネチャ設計を解説する。"
---

## 概要

.NET 8 の LINQ には、新しい演算子がほとんど追加されていない。
実質的な追加は、`KeyValuePair` やタプルのシーケンスをセレクタなしで辞書化する `ToDictionary` の**オーバーロード**である。
つまりこのバージョンのバックポートで問われるのは、新しいアルゴリズムではなく「既存のメソッド群に新しいシグネチャを違和感なく同居させる」というシグネチャ設計である。

本記事では、セレクタ不要の `ToDictionary`（`KeyValuePair` 版・タプル版）のポリフィル実装を示し、その設計判断を 3 つに分けて解説する。

1. 既存の `ToDictionary(keySelector, valueSelector)` とのオーバーロード解決を衝突させない
2. 実装内部の委譲では名前付き引数 `comparer:` で解決先を固定する
3. `where TKey : notnull` 制約と戻り値型を本家に一致させ、null 許容解析の結果を揃える

---

## 前提・対象環境

- フレームワーク: .NET Framework 4.8（バックポート先）/ .NET 8+（将来の移行先）
- 対象: LINQ `ToDictionary` のセレクタ不要オーバーロード 4 シグネチャ（`KeyValuePair` 版・タプル版、それぞれ `IEqualityComparer<TKey>` オーバーロードを含む）
- 方針: `#nullable enable` を適用し、`#if !NET8_0_OR_GREATER` で移行時に自動無効化する
- 言語バージョン: `#nullable enable`・nullable 参照型注釈・`where TKey : notnull` 制約は C# 8.0 以上を要する。.NET Framework 4.8 の既定は C# 7.3 のため、`.csproj` の `LangVersion` を `8.0` 以上に設定する（`LangVersion` を明示しない古い非 SDK 形式プロジェクトでは特に注意する）

---

## 問題

.NET 8 で追加された以下のオーバーロードは、.NET Framework 環境では使用できない。

| メソッド | 追加されたバージョン | 概要 |
| --- | --- | --- |
| `ToDictionary<TKey, TValue>(this IEnumerable<KeyValuePair<TKey, TValue>>)` | .NET 8.0 | `KeyValuePair` のシーケンスをそのまま辞書へ変換する |
| `ToDictionary<TKey, TValue>(this IEnumerable<(TKey, TValue)>)` | .NET 8.0 | 2 要素タプルのシーケンスをそのまま辞書へ変換する |

いずれも `IEqualityComparer<TKey>` を受け取るオーバーロードを持ち、合計 4 シグネチャとなる。

これらが無い環境では、要素がすでにキーと値の対になっている場合でも、恒等的なセレクタを明示する既存の `ToDictionary` を書く必要がある。

- `pairs.ToDictionary()` の代わりに `pairs.ToDictionary(p => p.Key, p => p.Value)` と書く
- タプルのシーケンスに対しても `items.ToDictionary(t => t.Item1, t => t.Item2)` と書く

`p => p.Key` / `p => p.Value` は変換の本質ではない定型記述であり、キーと値のセレクタを取り違える軽微なミスの温床にもなる。

---

## 原因・背景

`Dictionary` をフィルタリングして再構築する、`Select` が返したタプルを辞書化する、といった場面では要素がすでにキーと値の対になっている。
その場合に恒等セレクタを書かせられていた冗長さを解消するため、.NET 8 でセレクタ不要のオーバーロードが標準化された。

既存メソッドへの「シグネチャ追加」という性格上、本家自身も既存オーバーロードとの解決関係を壊さないよう設計されている。
ポリフィルも同じ制約の下で設計する必要があり、それが本記事の主題である。

---

## 解決方法

本家と同じ `System.Linq` 名前空間に拡張メソッドを定義し、`#if !NET8_0_OR_GREATER` で移行時に自動無効化する（この基本方針の根拠は[シリーズ基礎編](/ja/articles/linq-backport-netframework-to-net5/)を参照）。

そのうえで、シグネチャ設計として次の 3 点を本家に揃える。

- 第 1 引数の型を `IEnumerable<KeyValuePair<TKey, TValue>>` / `IEnumerable<(TKey, TValue)>` に限定し、既存オーバーロードと衝突させない
- 内部委譲は `comparer:` の名前付き引数で解決先を固定する
- 戻り値型を `Dictionary<TKey, TValue>`、制約を `where TKey : notnull` にする

---

## 実装例

以下は 4 シグネチャのポリフィル実装一式である。
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

## シグネチャ設計の 3 つの判断

### 判断 1: 既存オーバーロードと衝突しない第 1 引数型

`ToDictionary` という名前は .NET Framework にも既に存在するため、無条件に追加すればオーバーロード解決を壊しかねない。
本ポリフィルが安全なのは、第 1 引数（拡張対象）の型で棲み分けているからである。

- セレクタを渡す呼び出し（`ToDictionary(x => x.Id)` など）は、`Func<...>` を引数に取る既存の本家実装へ解決される
- 要素が `KeyValuePair` またはタプルで、セレクタを渡さない呼び出しだけが本ポリフィルへ解決される

既存メソッドへシグネチャを追加するタイプのポリフィルでは、「新しいシグネチャが既存のどの呼び出しにもマッチしない」ことを最初に確認する。
これが崩れていると、コンパイルは通っても意図しないオーバーロードが選ばれる事故につながる。

### 判断 2: 名前付き引数 `comparer:` による解決先の固定

パラメータなしのオーバーロードは、比較子オーバーロードへ `null` を渡して委譲する。
このとき単に `source.ToDictionary(null)` と書くと、`null` リテラルは `IEqualityComparer<TKey>` にも `Func<...>` にも変換可能なため、コンパイラがどのオーバーロードを選ぶか曖昧になる。

```csharp
public static Dictionary<TKey, TValue> ToDictionary<TKey, TValue>(
    this IEnumerable<KeyValuePair<TKey, TValue>> source)
    where TKey : notnull
    => source.ToDictionary(comparer: null); // 名前付き引数で比較子オーバーロードに固定
```

`comparer:` の名前付き引数を使えば、その名前のパラメータを持つオーバーロードだけが候補になり、解決先が一意に固定される。
オーバーロードが密集した API へ委譲する際の定石である。

### 判断 3: `where TKey : notnull` と戻り値型の一致

本家のオーバーロードは戻り値型が `Dictionary<TKey, TValue>`、型制約が `where TKey : notnull` である。
ポリフィルがこの 2 つを一致させておくと、`#nullable enable` なコードベースで移行前後の null 許容解析の結果が変わらない。

制約を省いてもコンパイルは通るが、その場合、ポリフィル利用中は許容されていた「null 許容キー型」の呼び出しが .NET 8 移行後に警告へ変わる。
移行ガードで自動的に本家へ切り替える設計（後述）を成立させるには、シグネチャの互換はアルゴリズムの互換と同じだけ重要である。

---

## 利用場面別の動作

### `KeyValuePair` のシーケンスからの変換

既存の辞書をフィルタリングして新しい辞書を作る場面では、要素がそのまま `KeyValuePair` になっている。

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

`Dictionary<TKey, TValue>` は `IEnumerable<KeyValuePair<TKey, TValue>>` を実装するため、`Where` の戻り値は `KeyValuePair` のシーケンスになり、そのままセレクタなしで辞書へ戻せる。

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

### 即時評価であること

`ToDictionary` は `Where` や `Order` のような遅延評価ではなく、**即時評価**である。
呼び出した時点でソースを最後まで列挙し、辞書を構築して返す。

```csharp
var query = Enumerable.Range(1, 3).Select(n => (n, n * n));

var dictionary = query.ToDictionary(); // ここでソースが列挙される
// dictionary: { 1: 1, 2: 4, 3: 9 }
```

ソースが遅延評価のクエリであっても、`ToDictionary` の呼び出しで確定した辞書が得られる。
後続でソースの元データが変化しても、生成済みの辞書には影響しない。

---

## 移行ガード

本ポリフィルは `#if !NET8_0_OR_GREATER` で囲む。
これらのオーバーロードは .NET 7 以前に存在しないため、誤ったガードはコンパイルエラーを招く。`!NETCOREAPP` は `NETCOREAPP` が定義される全ターゲット（.NET Core・.NET 5〜7）でポリフィルを無効化し、`!NET7_0_OR_GREATER` は `net7.0` 以降でのみ無効化する（`.NET 6` では `NET7_0_OR_GREATER` が未定義のため有効なまま、`.NET 7` で無効化されエラーになる）。いずれもオーバーロードを持たない環境でポリフィルを外してしまうため、正しくは対象バージョンで無効化する `#if !NET8_0_OR_GREATER` を用いる。
シンボル選択の一般規則（追加されたバージョン以上で無効化する）は[.NET 6 メソッドのバックポート記事](/ja/articles/linq-backport-netframework-to-net6/)で整理している。

---

## 注意点

- **既存の `ToDictionary` との使い分け**: 本オーバーロードは、要素がすでに `KeyValuePair` またはタプルの形になっている場合にのみ有効である。任意の要素型 `TSource` から辞書を作る場合は、従来どおりキーセレクタ（必要なら値セレクタ）を渡す既存の `ToDictionary` を使う。
- **キーの重複は例外になる**: 実装は `Dictionary<TKey, TValue>.Add` を用いるため、キーが重複すると `ArgumentException` が発生する。この挙動は本家の `ToDictionary` と一致する。重複を許容して後勝ちにしたい場合は、`ToDictionary` ではなく手動で `dictionary[key] = value` を用いる。
- **キーの `null` は例外になる**: キーに `null` が現れると `ArgumentNullException` が発生する。`where TKey : notnull` 制約により参照型のキーは非 `null` が前提となるが、実行時に `null` が混入した場合はこの例外で検出される。
- **即時評価である**: `source` を最後まで列挙するため、無限シーケンスや、列挙のたびに副作用を起こすソースには使わない。`source` が `null` の場合の `ArgumentNullException` は呼び出し時点で即座に投げられる。

---

## 代替案・比較

| 方法 | メリット | デメリット | 適するケース |
| --- | --- | --- | --- |
| 自作ポリフィル（本記事） | 外部依存なし・シグネチャを本家と完全一致させられる | オーバーロード設計の検討が必要 | `#nullable enable` なコードベースで移行を見据える場合 |
| `ToDictionary(p => p.Key, p => p.Value)` を直書き | 追加コード不要 | 記述が冗長・移行時に一括置換が必要 | 使用箇所が少なく移行予定もない場合 |
| .NET 8 へのアップグレード | 根本的解決・言語機能も享受できる | 移行コストが発生する | 移行が技術的・ビジネス的に許容できる場合 |

セレクタの直書きは追加コードこそ不要だが、後日 .NET 8 へ移行してセレクタなしの記法へ統一する際に、使用箇所を洗い出して置換する手間が残る。

---

## まとめ

.NET 8 のセレクタなし `ToDictionary` は「新しいアルゴリズム」ではなく「新しいシグネチャ」であり、バックポートの品質はシグネチャ設計で決まる。

| 設計判断 | 内容 |
| --- | --- |
| 第 1 引数型の限定 | `KeyValuePair` / タプルのシーケンスに限定し、既存オーバーロードと衝突させない |
| 名前付き引数 `comparer:` | 内部委譲の解決先を比較子オーバーロードに固定する |
| `notnull` 制約と戻り値型 | 本家と一致させ、移行前後の null 許容解析を揃える |

要素が対になっていない入力にはセレクタ付きの既存 `ToDictionary` を使い、対になっている入力にだけ本オーバーロードを使う。
この使い分けを守れば、`#if !NET8_0_OR_GREATER` の移行ガードによって .NET 8 移行時にコード無修正で本家実装へ切り替わる。

---

## 関連記事

- [遅延評価を壊さない LINQ ポリフィルの設計原則 — Append・Prepend・TakeLast・SkipLast の実装](/ja/articles/linq-backport-netframework-to-net5/)
- [GroupBy と全件ソートによる回避コードをなくす — Chunk・MaxBy・MinBy・DistinctBy の実装](/ja/articles/linq-backport-netframework-to-net6/)
- [委譲だけで作る Order・OrderDescending — IOrderedEnumerable 互換の最小ポリフィル](/ja/articles/linq-backport-netframework-to-net7/)
- [GroupBy を経由しないキー集計 — CountBy・AggregateBy・Index の辞書ベース実装](/ja/articles/linq-backport-netframework-to-net9/)
- [SQL の外部結合を LINQ で表現する — LeftJoin・RightJoin・Shuffle の実装](/ja/articles/linq-backport-netframework-to-net10/)
