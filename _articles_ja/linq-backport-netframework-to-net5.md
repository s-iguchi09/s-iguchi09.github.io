---
layout: article-ja
title: ".NET Framework の不足 LINQ メソッドを .NET 5 相当にバックポートする"
date: 2026-07-10
category: C#
excerpt: ".NET Framework 4.8 から .NET 5 の間に追加された LINQ の 4 メソッド（Append・Prepend・TakeLast・SkipLast）を、遅延評価の落とし穴と移行時の名前衝突に対処しながら安全にバックポートする方法を解説する。"
---

## 概要

.NET Framework から .NET 5 への移行を段階的に進めている場合や、諸事情で .NET Framework 環境のコードをメンテナンスし続けなければならない場合、地味にストレスになるのが「新世代 .NET にはあるのに、.NET Framework には存在しない LINQ メソッド」の存在である。

`.Append()` が使えないために `Concat` に配列を渡して代用したり、`TakeLast` がないため一度 `Count` を計算して引き算したりする実装は、コードの可読性を下げ、将来的なバグの温床になる。

本記事では、.NET Framework から .NET 5 の間に LINQ に追加された 4 つのメソッドを整理し、**同一の使用感で動作する拡張メソッド（ポリフィル）を安全に実装する方法**を解説する。実装時の設計上の注意点（遅延評価とメソッド分離）、各アルゴリズムの解説、および将来の移行時にコードを無修正で切り替えるための条件付きコンパイルの使い方も合わせて紹介する。

---

## 前提・対象環境

- フレームワーク: .NET Framework 4.8 / .NET 5+
- 対象: LINQ の 4 メソッド（Append / Prepend / TakeLast / SkipLast）
- 方針: `yield return` の遅延評価を踏まえて public メソッドと iterator を分離し、移行時は `#if !NETCOREAPP` で自動的に無効化する

---

## .NET Framework から .NET 5 における LINQ の変遷

.NET Framework 最終版の「4.8」から統合された新世代の「.NET 5」までの期間（.NET Core 2.0 〜 .NET 5）は、目新しいメソッドの大量追加よりも、**内部ロジックの書き直しによる徹底的なパフォーマンス向上**に注力された移行期にあたる。

とはいえ、開発者にとって有益な 4 つのメソッドがこの期間に追加されている。

| メソッド名 | 追加されたバージョン | 概要 |
| --- | --- | --- |
| `Append<T>` | .NET Core 2.0 | シーケンスの末尾に要素を 1 つ追加する |
| `Prepend<T>` | .NET Core 2.0 | シーケンスの先頭に要素を 1 つ追加する |
| `TakeLast<T>` | .NET Core 3.0 | シーケンスの末尾から指定数の要素を取得する |
| `SkipLast<T>` | .NET Core 3.0 | シーケンスの末尾から指定数の要素を除外する |

`Chunk`（指定サイズで分割）や `MaxBy`（特定のキーが最大の要素を取得）といったメソッド群が追加されたのは **.NET 6** からである。.NET 5 の時点ではまだ存在していないため、混同しないよう注意が必要である。

---

## バックポートの実装コード

以下は、上記 4 メソッドを .NET Framework 環境でも同一の使用感で利用できるようにする拡張メソッドの実装である。

将来の .NET 5 以降への移行を見据えた条件付きコンパイル（`#if !NETCOREAPP`）を施してある。プロジェクトに `LinqExtensions.Net5.cs` などの名前でそのままコピーして利用できる。

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

---

## 技術解説：メソッドを分離する理由（遅延評価の罠）

上記のコードでは、パブリックなメソッドとプライベートな `~Iterator` メソッドの 2 つに分かれている。1 つにまとめたほうがシンプルに見えるが、ここには C# の遅延評価（Lazy Evaluation）に関わる重要な設計意図がある。

### `yield return` が持つ特殊な性質

`yield return`（イテレータブロック）を使用したメソッドは、**呼び出された瞬間には内部のコードが 1 行も実行されない**という特殊な挙動をする。実際にデータが必要になり、`foreach` で反復されたり `.ToList()` が呼ばれたりした瞬間に初めて動き出す。

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

メソッドを呼び出した時点では ① の null チェックすら実行されずにスルーされる。そして遥か後方で `foreach` が実行された瞬間にクラッシュする。バグ（null を渡した操作）の犯人は数行前（あるいは別クラス）にいるのに、全く関係のない `foreach` の場所でエラーが起きるため、原因究明が困難になる。

### メソッド分離による解決

`yield return` を使わない通常のメソッドは、呼び出された瞬間に即座に実行される。そのため引数チェックがその場で行われ、即座に例外を投げる。チェックを通過した後にイテレータメソッドを呼び出すことで、本来の遅延評価のメリットを維持しつつ、エラーを発生源のすぐ近くで検知できる。この構成は標準の LINQ 内部でも採用されているパターンである。

---

## 各メソッドの詳解

### `Append<T>`（末尾への要素追加）

元のシーケンスの末尾に要素を 1 つだけ付け足す。

```csharp
var result = new[] { 1, 2, 3 }.Append(4);
// result: 1, 2, 3, 4
```

#### Append の内部ロジック

```csharp
foreach (var item in source)
{
    yield return item; // 元のデータをそのまま通過させる
}
yield return element; // 元データが尽きたら末尾に追加する
```

元のデータをどこかに保存したり加工したりせず、ただ通過させているだけなのでメモリを消費しない（空間計算量 $O(1)$）。従来の `Concat(new[] { element })` のように要素 1 つのために配列インスタンスを生成する無駄がないため、非常に軽量である。

---

### `Prepend<T>`（先頭への要素追加）

`Append` の逆で、元のシーケンスが始まる前に要素を先頭に挿入する。

```csharp
var result = new[] { 1, 2, 3 }.Prepend(0);
// result: 0, 1, 2, 3
```

#### Prepend の内部ロジック

```csharp
yield return element; // 最初に追加したい要素を流す

foreach (var item in source)
{
    yield return item; // その後、元のデータを追いかける
}
```

`Append` と同様に、データを一時的にバッファリングする必要がないため、メモリ消費を最小限に抑えて動作する。

---

### `TakeLast<T>`（末尾から指定数を取り出す）

`IEnumerable` は「最後までデータを読み進めてみないと末尾がどこかわからない」という制約がある。そのため、`Queue<T>` を用いたスライディングウィンドウの仕組みを使う。

```csharp
var result = new[] { 1, 2, 3, 4, 5 }.TakeLast(3);
// result: 3, 4, 5
```

#### TakeLast の内部ロジック

```csharp
// 指定された個数だけを記憶できるキューを用意する
var queue = new Queue<TSource>(count);

foreach (var item in source)
{
    // キューが満杯になったら最も古いデータを 1 つ押し出す
    if (queue.Count == count)
    {
        queue.Dequeue();
    }
    queue.Enqueue(item); // 新しいデータを入れる
}

// 元のデータがすべて終わった時点でキューに残っているのが「最後の N 個」
foreach (var item in queue)
{
    yield return item;
}
```

元データが何個あっても、メモリ上に保持し続けるのは常に `count` 個の要素だけである（空間計算量 $O(count)$）。

---

### `SkipLast<T>`（末尾から指定数を除外する）

「最後の N 個以外のデータをすべて流す」という実装が必要になる。`TakeLast` と同じく、現在のデータが「末尾から N 個以内かどうか」は、次以降のデータが来るまで判断できない。

```csharp
var result = new[] { 1, 2, 3, 4, 5 }.SkipLast(2);
// result: 1, 2, 3
```

#### SkipLast の内部ロジック

「指定された数だけデータをキューにキープし、次のデータが来たら最も古いデータを解放して流す」という時間差の仕組みを使う。

```csharp
var queue = new Queue<TSource>(count);

foreach (var item in source)
{
    // キューが満杯の場合、先頭のデータを流す
    // （次のデータが来たということは、そのデータは末尾から N 個以内ではないことが確定する）
    if (queue.Count == count)
    {
        yield return queue.Dequeue();
    }

    // 新しいデータをキープする
    queue.Enqueue(item);
}
// ループ終了時、キューに残っている「最後の N 個」は流されずに終了する（= スキップされる）
```

データの出力が常に `count` 個分遅れて実行される点がこの関数の要点である。元データがすべて尽きたとき、キューに残されている `count` 個のデータは `yield return` される機会を得られないままメソッドが終了する。結果として、「末尾の N 個がスキップされた」状態が実現する。

---

## .NET 5 以降への移行時の名前衝突対策

今回実装した拡張メソッドは、名前空間をあえて本家と同じ `System.Linq` に設定している。これにより、既存のソースファイルに `using System.Linq;` が書いてあれば、コードを一切書き換えることなく自動的にこの拡張メソッドが適用される。

しかし、プロジェクトを将来 .NET 5 や .NET 6 にアップグレードした場合、本家の LINQ と自作の LINQ が衝突し、「**曖昧な呼び出しです（CS0121）**」というコンパイルエラーが発生する。

### プリプロセッサディレクティブによる解決

ソースコードの先頭と末尾に仕込んだ条件付きコンパイル命令がこの問題を自動的に解決する。

```csharp
#if !NETCOREAPP
namespace System.Linq
{
    // 拡張メソッドの実装...
}
#endif
```

`NETCOREAPP` というシンボルは、.NET Core や .NET 5 以降のモダンな環境でビルドするときにコンパイラが自動的に定義する。

| ビルド環境 | `#if !NETCOREAPP` の結果 | 動作 |
| --- | --- | --- |
| .NET Framework | 条件成立 | 自作の拡張メソッドがコンパイルされ、不足が補われる |
| .NET 5 / .NET 6 以降 | 条件不成立 | このファイルの中身は白紙として扱われ、本家 LINQ が使われる |

将来フレームワークをアップグレードした際、コードの修正やファイルの削除を一切行うことなく、**自動的かつ安全に本家 .NET のネイティブ LINQ へと切り替わる**。

---

## まとめ

.NET Framework から .NET 5 の間に追加された 4 つの LINQ メソッドと、.NET Framework 環境へのバックポート手法を解説した。

実装において重要なポイントは以下の 3 点である。

- **メソッドの分離**: C# の遅延評価の特性を理解し、引数チェックが呼び出し時点で実行されるよう通常メソッドとイテレータメソッドを分ける
- **`Queue<T>` の活用**: `TakeLast` / `SkipLast` のように末尾基準の操作では、固定サイズのキューによるスライディングウィンドウで省メモリを実現する
- **`#if !NETCOREAPP` の活用**: 将来の .NET 5 以降への移行時に名前衝突が起きないよう、自動的に無効化される条件付きコンパイルを仕込む

| メソッド | 空間計算量 | アルゴリズムの要点 |
| --- | --- | --- |
| `Append` | $O(1)$ | 元データを通過させた後に末尾要素を流す |
| `Prepend` | $O(1)$ | 先頭要素を流した後に元データを通過させる |
| `TakeLast` | $O(count)$ | キューで末尾 N 個を保持し、全走査後に出力する |
| `SkipLast` | $O(count)$ | キューで N 個遅延させ、溢れたデータを順次出力する |

---

## 関連記事

- [.NET Framework の不足 LINQ メソッドを .NET 6 相当にバックポートする](/ja/articles/linq-backport-netframework-to-net6/)
