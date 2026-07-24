---
layout: article-ja
title: "C# で Windows エクスプローラーと同じ並び順を実装する（StrCmpLogicalW と IComparer）"
date: 2026-07-24
category: C#
excerpt: "既定の文字列ソートでは \"item10\" が \"item2\" より前に並んでしまう。本記事では Win32 API の StrCmpLogicalW を P/Invoke で呼び出し、IComparer を実装したクラスとしてエクスプローラーと同じ自然順ソートを実現する方法を、メリット・デメリットとともに解説する。"
---

## 概要

Windows エクスプローラーでファイル名を並べると、`item1` `item2` `item10` のように数字部分が数値として扱われた「自然順（logical order）」で表示される。
一方、.NET の既定の文字列ソートでは `item1` `item10` `item2` の順になり、エクスプローラーと並びが一致しない。
本記事では、Win32 API の `StrCmpLogicalW` を P/Invoke で呼び出し、`IComparer<string>`（および非ジェネリックの `IComparer`）を実装したクラスとして、エクスプローラーと同じ並び順を実現する方法を扱う。
実装手順に加え、この方式のメリット・デメリットと代替案との比較も整理する。

---

## 前提・対象環境

- 言語: C# 9.0 以降（本記事の実行例はトップレベルステートメントを使用する。比較クラス自体は C# 8.0 以前でも動作し、P/Invoke はどのバージョンでも可）
- フレームワーク: .NET Framework 4.x / .NET 5 以降
- 実行環境: Windows 専用（`shlwapi.dll` に依存するため）
- 用途: `List<T>.Sort` / LINQ の `OrderBy` など、`IComparer<string>` を受け取る API

---

## 問題

ファイル名やキー文字列を昇順で並べたいだけなのに、既定のソートではエクスプローラーと異なる並びになる。

```csharp
var files = new List<string> { "item10", "item2", "item1", "item20", "item3" };
files.Sort();
// 実際の結果: item1, item10, item2, item20, item3
// 期待する結果: item1, item2, item3, item10, item20
```

`List<T>.Sort()` は `Comparer<string>.Default` を用いるが、これは数字を「文字の並び」として比較する。
そのため `item10` と `item2` を比べると、5 文字目の `1` と `2` の大小で `item10` が先に来てしまう。

---

## 原因・背景

通常の文字列比較は、この例のような ASCII の数字を含む文字列では、序数比較・カルチャ比較のいずれでも先頭から順に文字を突き合わせて大小を決める。
`item10` と `item2` は先頭の `item` まで等しく、次の文字が `1`（U+0031）と `2`（U+0032）である。
`1` は `2` より小さいため、後続に何桁続こうと `item10` が `item2` より前と判定される。

これに対しエクスプローラーは、文字列中の連続した数字を 1 つの数値としてまとめて比較する。
この「数字を数値として扱う」比較を提供するのが、シェルが内部で使う Win32 API の `StrCmpLogicalW`（`shlwapi.dll`）である。
`StrCmpLogicalW` は 2 つの Unicode 文字列を比較し、等しければ 0、第 1 引数が大きければ 1、小さければ -1 を返す。
比較は大文字小文字を区別しない。

---

## 解決方法

`StrCmpLogicalW` を `DllImport` で P/Invoke 宣言し、`IComparer<string>` を実装したクラスの `Compare` メソッド内から呼び出す。
`IComparer<string>` を実装しておけば、`List<T>.Sort` にも LINQ の `OrderBy` にもそのまま渡せる。
非ジェネリックの `IComparer` も併せて実装しておくと、`ListCollectionView` など古い API からも利用できる。

---

## 実装例

以下は `StrCmpLogicalW` を包んだ比較クラスである。
`shlwapi.dll` の関数を Unicode 版として厳密に束縛するため、`CharSet.Unicode` と `ExactSpelling = true` を指定する。
`null` を渡すと未定義動作となり得るため、比較前に `null` を明示的に処理する。

```csharp
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

public sealed class NaturalStringComparer : IComparer<string>, IComparer
{
    // shlwapi.dll の StrCmpLogicalW を P/Invoke で束縛する。
    // 数字の並びを数値として扱い、エクスプローラーと同じ論理順で比較する。
    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int StrCmpLogicalW(string psz1, string psz2);

    // 状態を持たないため、共有インスタンスを 1 つ使い回せば十分である。
    public static NaturalStringComparer Instance { get; } = new NaturalStringComparer();

    public int Compare(string x, string y)
    {
        // null を API へ渡さないよう、事前に順序を確定させる。
        if (ReferenceEquals(x, y)) return 0;
        if (x is null) return -1;
        if (y is null) return 1;
        return StrCmpLogicalW(x, y);
    }

    // 非ジェネリック版。ListCollectionView など古い API 向け。
    // 型不一致は as で null に潰さず、契約どおり ArgumentException とする。
    int IComparer.Compare(object x, object y)
    {
        if (x != null && !(x is string)) throw new ArgumentException("string 型が必要である。", nameof(x));
        if (y != null && !(y is string)) throw new ArgumentException("string 型が必要である。", nameof(y));
        return Compare((string)x, (string)y);
    }
}
```

このクラスは状態を持たないため、`Instance` を使い回してよい。
実際のソートでは、`List<T>.Sort` と LINQ の `OrderBy` のいずれにも同じインスタンスを渡せる。

```csharp
var files = new List<string> { "item10", "item2", "item1", "item20", "item3" };

// List<T>.Sort に IComparer<string> を渡してその場で並べ替える。
files.Sort(NaturalStringComparer.Instance);
// 結果: item1, item2, item3, item10, item20

// LINQ で新しいシーケンスとして並べ替える場合。
var ordered = files.OrderBy(f => f, NaturalStringComparer.Instance).ToList();
// 結果: item1, item2, item3, item10, item20
```

`OrderBy` の第 2 引数は `IComparer<TKey>` を受け取るため、キーセレクターで対象の文字列を返せば同じ比較器を再利用できる。
なお `OrderBy` や `ToList` を使うには `using System.Linq;` が必要である。

---

## 注意点

- **Windows 専用である。** `shlwapi.dll` は Windows のライブラリであり、Linux / macOS では `DllNotFoundException` となる。クロスプラットフォームで動かす場合は使えない。
- **ロケールに基づく言語的照合ではない。** `StrCmpLogicalW` はロケールを考慮した言語的な並び替えを行わない。公式ドキュメントも「正規のソート（canonical sorting）用途には使用すべきでない」「戻り値はリリース間で変わり得る」と明記している。永続化するキーの順序付けや、厳密な再現性が必要な照合には向かない。
- **大文字小文字を区別しない。** `Item2` と `item2` は同一として扱われる。ケースを区別したい場合は別途タイブレークが必要になる。
- **`null` の扱いに注意する。** `StrCmpLogicalW` は NULL 終端文字列を前提とするため、`null` をそのまま渡すと未定義動作となる。上記実装のように呼び出し前に `null` を処理する。
- **埋め込み NUL 文字を含む文字列は正しく比較できない。** マーシャリングは文字列を NULL 終端として渡すため、`"\0"` を含む文字列は最初の `\0` までしか比較されない。本比較器はファイル名など通常の文字列を対象とし、埋め込み NUL を含む文字列は想定しない。
- **P/Invoke のコストがある。** 比較 1 回ごとにネイティブ呼び出しが発生する。要素数 n のソートでは比較が O(n log n) 回呼ばれるため、極端に大量の要素では純粋なマネージド実装よりオーバーヘッドが目立つ場合がある。

---

## 代替案・比較

| 方法 | メリット | デメリット | 適するケース |
|---|---|---|---|
| `StrCmpLogicalW`（本記事） | エクスプローラーと同じ並びに近づく・実装が数行で済む | Windows 専用・ロケールに基づく言語的照合ではない・大文字小文字非区別・挙動がリリース間で変わり得る | Windows デスクトップアプリでシェルと並びを揃えたい |
| 自前の自然順比較（数字トークンを分割して数値比較） | クロスプラットフォーム・挙動を完全に制御できる | 実装量が多く、桁あふれや先頭ゼロなどの考慮が必要 | .NET 5+ のクロスプラットフォーム環境 |
| `StringComparer.Ordinal` | 標準・高速でカルチャに依存しない安定した順序 | 数字を数値として扱わず自然順にならない | 安定した順序が必要な内部データの照合 |
| `StringComparer.CurrentCulture` | 言語的に自然な照合が得られる | カルチャに依存し順序が環境で変わる・数字を数値として扱わない | ユーザー向け表示テキストの照合 |

---

## まとめ

Windows デスクトップアプリで、ファイル一覧などをエクスプローラーに近い並び順にしたい場合は、`StrCmpLogicalW` を `IComparer<string>` 実装で包む方式が最も手軽である。
数行のコードでシェルと同等の並びが得られ、`List<T>.Sort` にも `OrderBy` にもそのまま渡せる。
ただし公式には canonical sorting 用途は非推奨とされ、挙動がリリース間で変わり得る点は前提として押さえておく。
一方、クロスプラットフォームでの動作や、大文字小文字の区別、永続化するキーの安定した順序が必要な場合は、この API の制約（Windows 専用・ロケールに基づく言語的照合ではない・リリース間で挙動が変わり得る）が問題になる。
その場合は数字トークンを自前で分割して数値比較する実装を選ぶ。
エクスプローラーとの見た目の一致を最優先するなら `StrCmpLogicalW`、移植性と制御性を優先するなら自前実装、という基準で選択する。
