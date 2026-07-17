---
layout: article-ja
title: "遅延評価を壊さない LINQ ポリフィルの設計原則 — Append・Prepend・TakeLast・SkipLast の実装"
date: 2026-07-10
category: C#
excerpt: "LINQ ポリフィル自作時の 3 つの設計原則（引数検証とイテレータの分離・バッファリングの最小化・条件付きコンパイルによる移行ガード）を、.NET Core 期に追加された Append・Prepend・TakeLast・SkipLast の実装を題材に解説する。"
---

## 概要

.NET Framework 4.8 は Windows に同梱され続ける一方で機能追加は 2019 年に止まっており、それ以降に LINQ へ加わったメソッドは利用できない。
不足分は拡張メソッド（ポリフィル）として自作できるが、標準 LINQ と同じ使用感を再現するには、シグネチャを揃えるだけでは足りない設計上の配慮がある。

本記事では、.NET Core 2.0〜3.0 期に追加された `Append`・`Prepend`・`TakeLast`・`SkipLast` の 4 メソッドを題材に、LINQ ポリフィルを自作するうえで守るべき 3 つの設計原則を解説する。

1. **引数検証とイテレータ本体を分離する** — 遅延評価を保ったまま、例外を呼び出し時点で即時化する
2. **バッファリングを最小化する** — パススルー型とスライディングウィンドウ型を使い分ける
3. **条件付きコンパイルで移行をガードする** — フレームワーク更新時にコード無修正で本家へ切り替える

この 3 原則は本記事の 4 メソッドに限らず、.NET 6 以降で追加されたメソッド群のバックポート（文末の関連記事）にも共通して適用できる。
シリーズの基礎編として、原則そのものの根拠から説明する。

---

## 前提・対象環境

- フレームワーク: .NET Framework 4.8（バックポート先）/ .NET 5+（将来の移行先）
- 対象: LINQ の 4 メソッド（Append / Prepend / TakeLast / SkipLast）
- 方針: public メソッドと iterator を分離し、`#if !NETCOREAPP` で移行時に自動無効化する

---

## ポリフィルを System.Linq 名前空間に置く理由

自作の拡張メソッドは、あえて本家と同じ `System.Linq` 名前空間に定義する。
既存のソースファイルには `using System.Linq;` がすでに書かれているため、独自名前空間を追加で `using` する必要がなく、コードを 1 行も書き換えずに不足メソッドが使えるようになる。

この配置は「将来フレームワークを更新したとき、呼び出し側を修正せずに本家実装へ切り替える」という後述の移行ガード（設計原則 3）とセットで機能する。
独自名前空間に置いた場合、移行時にすべての `using` と呼び出し箇所を洗い出して削除する作業が発生し、透過的な切り替えが成立しない。

---

## 対象メソッド: .NET Core 期に追加された 4 つの演算子

.NET Framework 4.8 から .NET 5 に至る期間（.NET Core 2.0〜3.1）の LINQ は、内部実装の書き直しによる性能改善が中心で、メソッドの大量追加は行われていない。
この期間に追加されたメソッドには `ToHashSet`（.NET Core 2.0）なども含まれるが、本記事で題材として扱うのは以下の 4 つである。

| メソッド名 | 追加されたバージョン | 概要 |
| --- | --- | --- |
| `Append<T>` | .NET Core 2.0 | シーケンスの末尾に要素を 1 つ追加する |
| `Prepend<T>` | .NET Core 2.0 | シーケンスの先頭に要素を 1 つ追加する |
| `TakeLast<T>` | .NET Core 3.0 | シーケンスの末尾から指定数の要素を取得する |
| `SkipLast<T>` | .NET Core 3.0 | シーケンスの末尾から指定数の要素を除外する |

.NET Framework 環境でこれらが無いと、`Append` の代わりに `Concat(new[] { element })` と配列を確保したり、`TakeLast` の代わりに `Count` を先に数えて `Skip` したりする回避コードを書き続けることになる。
いずれも意図が読み取りにくく、不要な割り当てや二重列挙の温床になる。

なお、`Chunk` や `MaxBy` などのメソッド群が追加されたのは .NET 6 からであり、この時点では存在しない。
それらの実装は[別記事](/ja/articles/linq-backport-netframework-to-net6/)で扱う。

---

## 実装コード

以下は 4 メソッドを .NET Framework 環境でも同一の使用感で利用できるようにするポリフィルの全体である。
`LinqExtensions.Net5.cs` などの名前でプロジェクトにそのままコピーして利用できる。

```csharp
using System;
using System.Collections.Generic;

#if !NETCOREAPP // .NET Core / .NET 5 以降ではない環境（.NET Framework など）のみ有効化

namespace System.Linq
{
    /// <summary>
    /// .NET Framework 環境に .NET 5 相当の LINQ メソッドをバックポートする拡張クラス
    /// </summary>
    public static partial class LinqExtensions
    {
        // ==========================================
        // 1. Append
        // ==========================================
        public static IEnumerable<TSource> Append<TSource>(this IEnumerable<TSource> source, TSource element)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return AppendIterator(source, element);
        }

        private static IEnumerable<TSource> AppendIterator<TSource>(IEnumerable<TSource> source, TSource element)
        {
            foreach (var item in source)
            {
                yield return item;
            }
            yield return element;
        }

        // ==========================================
        // 2. Prepend
        // ==========================================
        public static IEnumerable<TSource> Prepend<TSource>(this IEnumerable<TSource> source, TSource element)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return PrependIterator(source, element);
        }

        private static IEnumerable<TSource> PrependIterator<TSource>(IEnumerable<TSource> source, TSource element)
        {
            yield return element;
            foreach (var item in source)
            {
                yield return item;
            }
        }

        // ==========================================
        // 3. TakeLast
        // ==========================================
        public static IEnumerable<TSource> TakeLast<TSource>(this IEnumerable<TSource> source, int count)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (count <= 0) return Enumerable.Empty<TSource>();

            return TakeLastIterator(source, count);
        }

        private static IEnumerable<TSource> TakeLastIterator<TSource>(IEnumerable<TSource> source, int count)
        {
            var queue = new Queue<TSource>(count);
            foreach (var item in source)
            {
                if (queue.Count == count)
                {
                    queue.Dequeue();
                }
                queue.Enqueue(item);
            }

            foreach (var item in queue)
            {
                yield return item;
            }
        }

        // ==========================================
        // 4. SkipLast
        // ==========================================
        public static IEnumerable<TSource> SkipLast<TSource>(this IEnumerable<TSource> source, int count)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (count <= 0) return source;

            return SkipLastIterator(source, count);
        }

        private static IEnumerable<TSource> SkipLastIterator<TSource>(IEnumerable<TSource> source, int count)
        {
            var queue = new Queue<TSource>(count);
            foreach (var item in source)
            {
                if (queue.Count == count)
                {
                    yield return queue.Dequeue();
                }
                queue.Enqueue(item);
            }
        }
    }
}

#endif
```

すべてのメソッドが「引数検証を行う public メソッド」と「`yield return` を含む private イテレータ」の 2 段構えになっている点、および `TakeLast` / `SkipLast` が `Queue<T>` を使っている点が、以降で説明する設計原則の実体である。

---

## 設計原則 1: 引数検証とイテレータの分離

public メソッドと private な `~Iterator` メソッドは、1 つにまとめたほうが一見シンプルである。
それでも分離するのは、C# の遅延評価（Lazy Evaluation）に関わる例外発生タイミングの問題があるためである。

### `yield return` が持つ特殊な性質

`yield return`（イテレータブロック）を使用したメソッドは、**呼び出された瞬間には内部のコードが 1 行も実行されない**。
実際にデータが必要になり、`foreach` で反復されたり `.ToList()` が呼ばれたりした瞬間に初めて動き出す。

引数チェックとループ処理を 1 つのメソッドにまとめた場合、以下の問題が発生する。

```csharp
// 悪い設計の例（1 つにまとめた場合）
public static IEnumerable<TSource> Append<TSource>(this IEnumerable<TSource> source, TSource element)
{
    if (source == null) throw new ArgumentNullException(nameof(source)); // ①

    foreach (var item in source)
    {
        yield return item;
    }
    yield return element;
}
```

この実装に `null` を渡して呼び出した場合の挙動は次のようになる。

```csharp
IEnumerable<int> numbers = null;

var result = numbers.Append(5); // ここではエラーが起きない

Console.WriteLine("後続のロジックが走る...");

foreach (var item in result) // ここで初めて ArgumentNullException が発生する
```

メソッドを呼び出した時点では ① の null チェックすら実行されない。
そして遥か後方で `foreach` が実行された瞬間にクラッシュする。
バグの原因（null を渡した操作）は数行前、あるいは別クラスにあるのに、無関係に見える `foreach` の位置で例外が起きるため、原因究明が難しくなる。

### 分離による即時検証

`yield return` を使わない通常のメソッドは、呼び出された瞬間に実行される。
そこで引数チェックだけを通常メソッドに置き、チェック通過後にイテレータメソッドを呼び出す構成にすると、遅延評価のメリットを維持しつつ、例外を発生源の直近で検知できる。
この 2 段構えは標準 LINQ の内部実装でも一貫して採用されているパターンであり、ポリフィルが本家と同じ例外タイミングを再現するための必須条件である。

---

## 設計原則 2: バッファリングの最小化

4 メソッドはいずれも `IEnumerable<T>` を入力とするため、「シーケンスの長さは最後まで列挙しないとわからない」という制約の下でアルゴリズムを選ぶ必要がある。
その戦略は 2 種類に分かれる。

### パススルー型: Append・Prepend

`Append` は元のデータをそのまま通過させ、尽きたところで末尾要素を 1 つ流す。

```csharp
foreach (var item in source)
{
    yield return item; // 元のデータをそのまま通過させる
}
yield return element; // 元データが尽きたら末尾に追加する
```

`Prepend` はその逆で、先頭要素を流してから元データを追いかける。

```csharp
yield return element; // 最初に追加したい要素を流す

foreach (var item in source)
{
    yield return item; // その後、元のデータを追いかける
}
```

どちらも要素をどこにも保存しないため、空間計算量は $O(1)$ である。
`Concat(new[] { element })` のように要素 1 つのために配列を確保する回避コードと違い、要素 1 つ分の配列割り当ては発生しない（遅延列挙のためのイテレータオブジェクト自体は生成される）。

```csharp
var appended = new[] { 1, 2, 3 }.Append(4);   // 1, 2, 3, 4
var prepended = new[] { 1, 2, 3 }.Prepend(0); // 0, 1, 2, 3
```

### スライディングウィンドウ型: TakeLast・SkipLast

「末尾から N 個」を扱う 2 メソッドは、パススルーでは実装できない。
現在の要素が末尾から N 個以内かどうかは、後続の要素が来るまで確定しないためである。
そこで、直近 `count` 個だけを保持する `Queue<T>` のスライディングウィンドウを使う。

`TakeLast` は、満杯になったら古いものを捨てながらキューを更新し、元データが尽きた時点でキューに残った「最後の N 個」を出力する。

```csharp
var queue = new Queue<TSource>(count);

foreach (var item in source)
{
    if (queue.Count == count)
    {
        queue.Dequeue(); // 最も古いデータを押し出す
    }
    queue.Enqueue(item);
}

foreach (var item in queue)
{
    yield return item; // 残っているのが「最後の N 個」
}
```

`SkipLast` は同じウィンドウを逆向きに使う。
キューが満杯の状態で次の要素が来たとき、押し出される先頭要素は「末尾から N 個以内ではない」ことが確定しているので、そのタイミングで出力する。

```csharp
var queue = new Queue<TSource>(count);

foreach (var item in source)
{
    if (queue.Count == count)
    {
        yield return queue.Dequeue(); // 末尾 N 個以内でないことが確定した要素を流す
    }
    queue.Enqueue(item);
}
// ループ終了時にキューへ残った「最後の N 個」は出力されずに終わる（= スキップ）
```

出力が常に `count` 個分遅れて進む点が要点である。
元データの総数に関係なく、保持するのは常に `count` 個だけなので、空間計算量は $O(count)$ に抑えられる。
`Count()` を先に数えてから `Skip` / `Take` する回避コードのような二重列挙も発生しない。

```csharp
var taken = new[] { 1, 2, 3, 4, 5 }.TakeLast(3);  // 3, 4, 5
var skipped = new[] { 1, 2, 3, 4, 5 }.SkipLast(2); // 1, 2, 3
```

---

## 設計原則 3: 条件付きコンパイルによる移行ガード

`System.Linq` 名前空間に自作メソッドを置いたままプロジェクトを .NET 5 以降へアップグレードすると、本家の同名メソッドと衝突して「曖昧な呼び出しです（CS0121）」のコンパイルエラーが発生する。
これを防ぐのが、ファイル全体を包む条件付きコンパイルである。

```csharp
#if !NETCOREAPP
namespace System.Linq
{
    // 拡張メソッドの実装...
}
#endif
```

`NETCOREAPP` シンボルは .NET Core および .NET 5 以降でビルドするときにコンパイラが自動的に定義する。

| ビルド環境 | `#if !NETCOREAPP` の結果 | 動作 |
| --- | --- | --- |
| .NET Framework | 条件成立 | ポリフィルがコンパイルされ、不足が補われる |
| .NET 5 / .NET 6 以降 | 条件不成立 | ファイルの中身は空として扱われ、本家 LINQ が使われる |

フレームワークを更新した際、ファイルの削除もコードの修正も行うことなく、自動的に本家実装へ切り替わる。
これが名前空間戦略（前述）と対になる移行ガードである。

なお、`!NETCOREAPP` が使えるのは「バックポート対象が .NET Core 2.0〜3.0 で追加されたメソッド」だからである。
.NET 6 以降で追加されたメソッドをこの条件でガードすると、.NET 5 環境でポリフィルが無効化されてコンパイルエラーになる。
バージョン別シンボル（`NET6_0_OR_GREATER` など）の使い分けは[.NET 6 メソッドのバックポート記事](/ja/articles/linq-backport-netframework-to-net6/)で詳しく扱う。

---

## 注意点

- **`TakeLast` / `SkipLast` は `count` 個分のメモリを消費する**: スライディングウィンドウは省メモリだが、`count` に巨大な値を渡せばその分のバッファが確保される。全件保持が前提になるような使い方では効果がない。
- **`count <= 0` の扱い**: `TakeLast` は空シーケンス、`SkipLast` は元シーケンスそのものを返す。例外にはならず、本家 .NET の挙動と一致する。
- **列挙のたびに再実行される**: 4 メソッドとも遅延評価であるため、同じクエリを複数回列挙するとソースの走査も毎回やり直される。結果を再利用する場合は `.ToList()` などで実体化する。
- **性能の最適化は本家より簡素である**: 本家 .NET の実装はソースが `IList<T>` の場合の特殊化などを持つが、本ポリフィルは汎用実装のみである。返す結果と例外の挙動は一致するが、コレクション型によっては本家より遅くなる余地がある。

---

## まとめ

`Append`・`Prepend`・`TakeLast`・`SkipLast` の 4 メソッドを題材に、LINQ ポリフィルの 3 つの設計原則を解説した。

| 設計原則 | 目的 |
| --- | --- |
| 引数検証とイテレータの分離 | 遅延評価を保ちつつ、例外を呼び出し時点で発生させる |
| バッファリングの最小化 | パススルー型は $O(1)$、末尾基準の操作は $O(count)$ に抑える |
| 条件付きコンパイルによる移行ガード | フレームワーク更新時にコード無修正で本家へ切り替える |

| メソッド | 空間計算量 | アルゴリズム |
| --- | --- | --- |
| `Append` / `Prepend` | $O(1)$ | パススルー |
| `TakeLast` / `SkipLast` | $O(count)$ | `Queue<T>` によるスライディングウィンドウ |

バックポート対象がこの 4 メソッドに限られるなら、外部ライブラリを導入するより本記事のポリフィルのほうが依存を増やさずに済む。
.NET 6 以降で追加されたメソッドも必要な場合は、同じ原則の上にバージョン別のガードを重ねていく（関連記事参照）。

---

## 関連記事

- [GroupBy と全件ソートによる回避コードをなくす — Chunk・MaxBy・MinBy・DistinctBy の実装](/ja/articles/linq-backport-netframework-to-net6/)
- [委譲だけで作る Order・OrderDescending — IOrderedEnumerable 互換の最小ポリフィル](/ja/articles/linq-backport-netframework-to-net7/)
- [セレクタなし ToDictionary の実現 — オーバーロード解決と notnull 制約の設計](/ja/articles/linq-backport-netframework-to-net8/)
- [GroupBy を経由しないキー集計 — CountBy・AggregateBy・Index の辞書ベース実装](/ja/articles/linq-backport-netframework-to-net9/)
- [SQL の外部結合を LINQ で表現する — LeftJoin・RightJoin・Shuffle の実装](/ja/articles/linq-backport-netframework-to-net10/)
