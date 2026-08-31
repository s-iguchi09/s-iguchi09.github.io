---
layout: article-ja
title: "C# 14 の拡張メンバーで静的クラスに追加できるのは静的メンバーだけ"
date: 2026-06-17
category: C#
excerpt: "C# 14 の extension ブロックで Directory のような静的クラスを対象にすると、静的メンバーは追加できるがインスタンスメンバーは CS0721 / CS9303 で拒否される。レシーバーの書き方による境界と、インスタンス形式で呼びたい場合の代替手段を整理する。"
---

## 概要

本記事では、C# 14（.NET 10）で導入された `extension` ブロック構文で `System.IO.Directory` のような静的クラスを対象にしたときに、何ができて何ができないのかを整理する。

結論から書くと、静的クラスは `extension` ブロックの対象にできる。ただし追加できるのは静的メンバーだけであり、インスタンスメンバーを追加しようとするとコンパイルエラーになる。
この境界はレシーバーの書き方（型だけを書くか、パラメーター名まで書くか）で決まる。

合わせて、インスタンス形式で呼び出したい場合の代替手段と、C# 3.0 から C# 14 にかけての拡張メンバー機能の変遷についても述べる。

<figure class="article-figure article-figure--wide">
  <img src="/images/articles/csharp14-extension-members-static-class-limitation/extension-receiver-form-matrix.svg" alt="静的クラス Directory に対する extension ブロックの可否を示す表。レシーバーが型だけの場合、静的メンバーはコンパイルが通り、インスタンスメンバーは CS9303 になる。レシーバーにパラメーター名を付けた場合はブロックの時点で CS0721 になる。" width="880" height="322" loading="lazy">
  <figcaption>静的クラスを対象にした <code>extension</code> ブロックの可否。.NET 10 SDK 10.0.302 / <code>LangVersion 14.0</code> で実際にビルドして確認したもの。レシーバーに名前を付けた時点で、後続のメンバーの種類にかかわらず <code>CS0721</code> になる。</figcaption>
</figure>

---

## 前提・対象環境

- 言語: C# 14（`LangVersion` は `14.0`）
- フレームワーク: .NET 10（確認に使用した SDK は 10.0.302）
- 対象機能: 拡張メンバー（`extension` ブロック構文）
- 比較対象: 従来の拡張メソッド（`this` 引数構文）
- 検証環境: .NET 10 SDK 10.0.302 / Windows 11（本文のコンパイル結果はこの環境で取得した）

---

## 問題

`extension` ブロックで静的クラスを扱おうとすると、書き方によってコンパイルが通ったり通らなかったりする。
まず、通らない方の例を挙げる。

```csharp
using System.IO;

public static class DirectoryExtensions
{
    // error CS0721: 'Directory': 静的型はパラメーターとして使用することはできません
    extension(Directory directory)
    {
        public void DeleteIfExists(string path)
        {
            try
            {
                Directory.Delete(path, true);
            }
            catch (DirectoryNotFoundException)
            {
                // 既に存在しない場合は何もしない。
            }
        }
    }
}
```

エラーは `extension(Directory directory)` の `Directory` の位置で報告される。
つまり、メンバーの中身に関係なく、レシーバーの宣言そのものが拒否されている。

一方、レシーバーからパラメーター名 `directory` を取り除き、メンバーを `static` にすると、同じ `Directory` を対象にしていてもコンパイルが通る。

```csharp
using System.IO;

public static class DirectoryExtensions
{
    // こちらはコンパイルが通る
    extension(Directory)
    {
        public static void DeleteIfExists(string path)
        {
            try
            {
                Directory.Delete(path, true);
            }
            catch (DirectoryNotFoundException)
            {
                // 既に存在しない場合は何もしない。
            }
        }
    }
}
```

呼び出し側でも、あたかも `Directory` 本来の静的メソッドであるかのように解決される。

```csharp
Directory.DeleteIfExists(@"C:\Temp\TargetDir");
```

「静的クラスには拡張メンバーを追加できない」と一括りにされることがあるが、実際に追加できないのはインスタンスメンバーだけである。

---

## 原因

### レシーバー指定には 2 つの形がある

`extension` ブロックのレシーバー指定は、**型だけを書く形**と**パラメーターとして書く形**の 2 通りある。
この 2 つは書き方の好みの違いではなく、宣言できるメンバーの種類が変わる。

| レシーバーの書き方 | 宣言できるメンバー | 静的クラスを対象にできるか |
|---|---|---|
| `extension(Directory)` | 静的メンバーのみ | できる |
| `extension(Directory directory)` | インスタンスメンバー（および静的メンバー） | できない |

言語仕様では、レシーバーパラメーターに名前を付ける場合、レシーバー型は静的であってはならないと定められている。
名前を付けた時点でそれは通常のパラメーターと同じ扱いになるため、静的クラスをパラメーターの型に使えないという既存の規則がそのまま適用される。

### 静的クラスをパラメーターの型に使えない理由

C# では `static class` の名前を、値を保持する場所の型として使うことが禁止されている。
以下はいずれもコンパイルエラーになる。

| 記述例 | エラー |
|---|---|
| `Directory myDir;` | `CS0723`: 静的型の変数を宣言することはできない |
| `List<Directory> list;` | `CS0718`: 静的型を型引数として使用することはできない |
| `void M(Directory d)` | `CS0721`: 静的型はパラメーターとして使用することはできない |

`extension(Directory directory)` は最後の行と同じ状況であり、報告されるエラーも `CS0721` で一致する。

なお、静的クラスの名前がまったく型として扱えないわけではない。
`typeof(Directory)` は合法であり、`Directory.Exists(path)` のようなメンバーアクセスも当然できる。
禁止されているのは、値を入れる場所（変数・パラメーター・型引数）の型として使うことである。
`extension(Directory)` の形が通るのは、ここにはレシーバーの値を受け取る場所が存在しないためである。

### 名前の無いレシーバーにインスタンスメンバーは置けない

逆に、`extension(Directory)` の形でインスタンスメンバーを宣言しようとすると、別のエラーになる。

```csharp
extension(Directory)
{
    // error CS9303: 名前のないレシーバーパラメーターを持つ拡張ブロックで
    //               インスタンスメンバーを宣言することはできません
    public void DeleteIfExists(string path) { }
}
```

インスタンスメンバーの本体はレシーバーの値を参照するため、その値を指す名前が必要になる。
名前が無い以上、インスタンスメンバーは宣言できない。

この 2 つのエラーが挟み撃ちになる結果、静的クラスに対してインスタンス形式の拡張メンバーを書く方法は存在しないことになる。

### 実測: 組み合わせごとのコンパイル結果

レシーバーの書き方とメンバーの種類をすべて組み合わせて、実際にコンパイルした結果が次の表である。

<figure class="article-figure">
  <img src="/images/articles/csharp14-extension-members-static-class-limitation/extension-receiver-matrix.svg" alt="レシーバーの書き方とメンバーの種類を組み合わせてコンパイルした結果の表。extension(Directory) に静的メンバーはコンパイルが通り、インスタンスメンバーは CS9303 になる。extension(Directory directory) はメンバーの種類によらず CS0721 になる。対照として extension(DirectoryInfo info) にインスタンスメンバーを置いた場合はコンパイルが通る。" width="524" height="230" loading="lazy">
  <figcaption>.NET 10 SDK 10.0.302 / <code>LangVersion 14.0</code> で <code>net10.0</code> を対象にコンパイルした結果。最終行は対照で、静的でない型（<code>DirectoryInfo</code>）であれば名前付きレシーバーにインスタンスメンバーを置けることを示す。</figcaption>
</figure>

**`extension(Directory directory)` の行は、メンバーの種類によらず `CS0721` になる。**
レシーバーに名前を付けた時点でエラーが確定するため、ブロックの中身は評価されない。
静的クラスに対して書ける拡張メンバーが静的メンバーだけに限られるのは、この 2 つのエラーの組み合わせによる。

最終行が示すとおり、同じ「名前付きレシーバー＋インスタンスメンバー」でも、対象が静的クラスでなければ問題なくコンパイルできる。
制限は `extension` ブロックそのものではなく、**静的クラスをパラメーターの型に使えない**という C# の既存の規則から来ている。

---

## 3 つの解決策

やりたいことに応じて選択肢が変わる。

- **解決策 A**: `Directory.Xxx(...)` の形で呼びたい → 型だけのレシーバーで静的拡張メンバーとして書く
- **解決策 B**: `xxx.Yyy()` のインスタンス形式で呼びたい → 対応するインスタンス型（`DirectoryInfo`）に拡張メンバーを定義する
- **解決策 C**: C# 13 以前も対象に含める必要がある → 通常の静的ヘルパークラスにする

### 解決策 A：静的拡張メンバーとして定義する

C# 14 以降であれば、これがもっとも素直な方法である。
`Directory` に本来存在するかのようなメソッドを追加できる。

```csharp
using System.IO;

namespace MyLib;

public static class DirectoryExtensions
{
    extension(Directory)
    {
        /// <summary>
        /// 指定されたパスにディレクトリが存在する場合、安全に削除します。
        /// </summary>
        public static void DeleteIfExists(string path)
        {
            try
            {
                Directory.Delete(path, true);
            }
            catch (DirectoryNotFoundException)
            {
                // 既に存在しない場合は何もしない。
            }
        }

        /// <summary>既定の作業ディレクトリ。</summary>
        public static string DefaultRoot => @"C:\Temp";
    }
}
```

呼び出し側では以下のように使用する。

```csharp
using System;
using System.IO;
using MyLib; // ← これを忘れると CS0117 になる

Directory.DeleteIfExists(@"C:\Temp\TargetDir");
Console.WriteLine(Directory.DefaultRoot);
```

上のコメントのとおり、拡張メンバーを定義した名前空間を `using` していないと、`Directory` に `DeleteIfExists` の定義が無いという `CS0117` になる。
これは従来の拡張メソッドと同じ制約であり、静的拡張メンバー特有の問題ではない。

### 解決策 B：`DirectoryInfo` に対して拡張メンバーを定義する

`DirectoryInfo` はインスタンス化可能な通常の型であるため、レシーバーにパラメーター名を付けられる。
インスタンス形式の呼び出しに揃えたい場合はこちらになる。

```csharp
using System.IO;

public static class DirectoryInfoExtensions
{
    // DirectoryInfo は静的クラスではないのでパラメーター形式で書ける
    extension(DirectoryInfo directoryInfo)
    {
        /// <summary>
        /// ディレクトリが存在する場合に安全に削除します。
        /// </summary>
        public void DeleteIfExists()
        {
            try
            {
                directoryInfo.Delete(true);
            }
            catch (DirectoryNotFoundException)
            {
                // 既に存在しない場合は何もしない。
            }
        }
    }
}
```

呼び出し側では以下のように使用する。

```csharp
var dir = new DirectoryInfo(@"C:\Temp\TargetDir");
dir.DeleteIfExists();
```

`DirectoryInfo` のインスタンス生成が必要になるが、パスを 1 度だけ解決して複数の操作を続ける場合はむしろ扱いやすい。

### 解決策 C：静的ヘルパークラスとして定義する

C# 13 以前を対象に含める場合は、`extension` ブロックを使えないため従来どおりの静的クラスにする。

```csharp
using System.IO;

public static class DirectoryHelper
{
    /// <summary>
    /// 指定されたパスにディレクトリが存在する場合、安全に削除します。
    /// </summary>
    public static void DeleteIfExists(string path)
    {
        try
        {
            Directory.Delete(path, true);
        }
        catch (DirectoryNotFoundException)
        {
            // 既に存在しない場合は何もしない。
        }
    }
}
```

```csharp
DirectoryHelper.DeleteIfExists(@"C:\Temp\TargetDir");
```

`Directory.DeleteIfExists(...)` の形にはならないが、メソッドの所在が明確であるという利点はある。

---

## 選択の分岐点

3 つのどれを使うかは、呼び出し形式と対象バージョンで決まる。

**`Directory.Xxx(path)` の形で呼びたいなら解決策 A。**
標準 API と同じ見た目に揃うため、既存コードへ自然に混ざる。型だけのレシーバーに静的メンバーを置く形になり、インスタンスメンバーは書けない。

**`xxx.Yyy()` のインスタンス形式で揃えたいなら解決策 B。**
`DirectoryInfo` のような対応するインスタンス型を拡張する。パスを 1 度解決して複数の操作を続ける場合は、インスタンスを保持できるぶん有利になる。ただし `Directory.Exists(path)` に相当するのが `directoryInfo.Exists` プロパティであるように、API の形そのものが違う点は移し替えのときに効いてくる。

**C# 13 以前を対象に含めるなら解決策 C。**
`extension` ブロックは C# 14（.NET 10）以降でしか使えない。それ以前を含むなら、従来どおりの静的ヘルパークラスにする。呼び出し形式は標準 API と揃わないが、所在は最も明確である。

A と B はいずれも、呼び出し側で定義元の名前空間を `using` する必要がある。忘れると `CS0117` になり、拡張メンバーの解決に失敗したことが読み取りにくい。

---

## 解決策の比較

| 方法 | 呼び出し形式 | メリット | デメリット | 適するケース |
|---|---|---|---|---|
| 静的拡張メンバー（解決策 A） | `Directory.DeleteIfExists(path)` | 標準 API と同じ見た目で呼べる | C# 14 以降限定、`using` 忘れが分かりにくい | C# 14 以降で、静的クラスの API を補完したい場合 |
| `DirectoryInfo` への拡張メンバー（解決策 B） | `dir.DeleteIfExists()` | インスタンス形式に統一できる | インスタンス生成が必要、C# 14 以降限定 | パスを 1 度解決して複数操作を続ける場合 |
| 静的ヘルパークラス（解決策 C） | `DirectoryHelper.DeleteIfExists(path)` | バージョン依存なし、所在が明確 | 標準 API と呼び出し形式が揃わない | C# 13 以前も対象に含める場合 |

---

## 注意点

- 静的クラスを拡張する `extension` ブロックには、ユーザー定義演算子を置けない（`CS9321`）。
  演算子は拡張対象の型を引数に取る必要があり、静的クラスはその型になれないためである。
- `this` 引数構文で書ける従来の拡張メソッドはインスタンスメソッドだけであり、静的メンバーの外付けはできない。
  解決策 A が使えるのは `extension` ブロックが入った C# 14 以降に限られる。

---

## 補足：C# 3.0 から C# 14 までの拡張メンバーの変遷

C# における外付けメンバー機能は段階的に拡張されてきた。以下にその主な変遷を示す。

| バージョン | 対象プラットフォーム | 主な変更内容 |
|---|---|---|
| C# 3.0 | .NET Framework 3.5 | **拡張メソッドの導入**。`public static class` 内で第一引数に `this` キーワードを付与することで、既存の型にインスタンスメソッドを外付けできるようになった。LINQ の実現基盤でもある。 |
| C# 7.2 | .NET Core 2.0 / .NET Framework 4.7.2 | **値型への対応強化**。`ref this` や `in this` 修飾子が利用可能になり、大きな構造体をコピーせず参照渡しで拡張できるようになった。 |
| C# 14 | .NET 10 | **拡張メンバー構文（`extension` ブロック）の導入**。`this` 引数による記述に加えて `extension(型)` ブロックによる宣言が可能になり、メソッドに加えてプロパティ・インデクサー・演算子、および静的メンバーの外付けができるようになった。 |

`this` 引数構文で書けるのはインスタンス拡張メソッドだけであり、静的メンバーを外付けする手段は C# 13 以前には無かった。
静的クラスに対して `Directory.DeleteIfExists(...)` のようなメンバーを足せるようになったのは、C# 14 の `extension` ブロックが初めてである。

---

## まとめ

静的クラスを `extension` ブロックの対象にすること自体は可能である。
できないのはインスタンスメンバーの追加であり、それはレシーバーに名前を付けた時点で静的クラスがパラメーターの型として扱われ、`CS0721` になるためである。

分岐点は呼び出し形式と対象バージョンにある。
標準 API と同じ形で呼びたいなら解決策 A、インスタンス形式に揃えたいなら解決策 B、C# 13 以前を含むなら解決策 C を選ぶ。

---

<!-- 関連記事 -->
