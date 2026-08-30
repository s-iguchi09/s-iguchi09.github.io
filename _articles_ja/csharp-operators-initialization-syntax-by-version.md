---
layout: article-ja
title: "C# バージョン別 演算子と初期化構文シンタックスシュガー一覧"
date: 2026-06-22
category: C#
excerpt: ".NET Framework 環境では ??= などの C# 新構文が使用できない場合がある。各バージョンで追加された演算子と初期化の糖衣構文を、net48 に対して実際にコンパイルした結果とともに整理する。BCL 型を要する構文と、その補い方も実測で示す。"
---

## 概要

本記事では、.NET Framework 環境で `??=`（ヌル合体割り当て演算子）が使用できないという問題を契機として、C# 各バージョンで追加された演算子および初期化の糖衣構文（シンタックスシュガー）をバージョン対応表とコード例を交えて網羅的に整理する。
対応するバージョンを事前に把握しておくことで、環境の制約に応じた適切な実装選択が可能となる。

---

## 前提・対象環境

- 言語: C# 1.0 〜 C# 12
- フレームワーク: .NET Framework 2.0〜4.8 / .NET Core / .NET 5 以降
- 対象機能: Null 安全演算子、インデックス・範囲演算子、型演算子、初期化の糖衣構文
- 検証環境: .NET SDK 10.0.302 / .NET Framework 4.8.9337.0 / Windows 11

本記事の対応表は、各構文を**実際にコンパイルして確かめた結果**である。
`LangVersion` を下げながら `net10.0` へコンパイルして構文が受け付けられる下限を求め、`net48` へコンパイルして BCL 側の型が足りるかどうかを確かめている。
`^` と `..` については、`net48` 上で実行して戻り値も確認した。

その結果、**ドキュメントの記述だけでは分からない差異が 3 点見つかっている**。いずれも本文中で該当箇所に記す。

---

## 問題

.NET Framework をターゲットにした C# 開発において、プロジェクトが C# 8.0 未満（例: 7.3）としてコンパイルされている場合、`??=`（ヌル合体割り当て演算子）を使用するとコンパイルエラーが発生する。

```csharp
private List<int> _numbers;

public void AddNumber(int val)
{
    _numbers ??= new List<int>(); // C# 8.0 未満でコンパイルされる環境ではコンパイルエラー
    _numbers.Add(val);
}
```

このエラーは、`??=` が C# 8.0 で導入された構文である一方で、プロジェクトが C# 8.0 未満としてコンパイルされている（`LangVersion` が 7.3 以前など）ために発生する。

---

## 原因・背景

C# の言語バージョンはターゲットフレームワークとは独立しており、利用可否は主に (1) コンパイラ／`LangVersion` と、(2) 機能が要求するランタイム側の型・API の有無で決まる。
そのため .NET Framework をターゲットとしていても、ビルド環境が C# 8.0 に対応していれば `??=` や `!` のような言語機能は使用できる。

以下に、本記事で取り上げる演算子・構文と導入された C# バージョンの対応表を示す。
導入時期を時系列で見ると次のようになる。

<figure class="article-figure article-figure--wide">
  <img src="/images/articles/csharp-operators-initialization-syntax-by-version/csharp-operator-timeline.svg" alt="C# 1.0 から 12.0 までの時系列に、各バージョンで追加された演算子と構文を配置した図。^ と ..、init、with、required が BCL 側の型や属性を必要とすることが色分けされている。" width="900" height="300" loading="lazy">
  <figcaption>各構文が追加された C# のバージョン。枠が色付きのものは <code>LangVersion</code> を上げるだけでは足りず、BCL 側の型や属性を必要とする。<code>??=</code> は C# 8.0 の位置にある。色分けは次節のコンパイル結果に対応する。</figcaption>
</figure>

| 演算子 / 構文 | C# バージョン | .NET バージョン | .NET Framework 対応 |
| --- | --- | --- | --- |
| `??`（ヌル合体） | C# 2.0 | .NET Framework 2.0 | ✅ 2.0 以降 |
| `as`（型キャスト） | C# 1.0 | .NET Framework 1.0 | ✅ 全バージョン |
| `is`（型チェック） | C# 1.0 | .NET Framework 1.0 | ✅ 全バージョン |
| `=>`（ラムダ） | C# 3.0 | .NET Framework 3.5 | ✅ 言語機能のみ（†1） |
| `=>`（式形式のメンバー） | C# 6.0 | .NET Framework 4.6 | ✅ 言語機能のみ（†1） |
| `?.` `?[]`（ヌル条件） | C# 6.0 | .NET Framework 4.6 | ✅ 言語機能のみ（†1） |
| `nameof` | C# 6.0 | .NET Framework 4.6 | ✅ 言語機能のみ（†1） |
| `is` パターンマッチング | C# 7.0 | .NET Framework 4.7 | ✅ 言語機能のみ（†1） |
| `??=`（ヌル合体割り当て） | C# 8.0 | .NET Core 3.0 / .NET 5 | ✅ 言語機能のみ（†1） |
| `!`（null 免除） | C# 8.0 | .NET Core 3.0 / .NET 5 | ✅ 言語機能のみ（†1） |
| `^`（末尾インデックス） | C# 8.0 | .NET Core 3.0 / .NET 5 | ⚠️ BCL 型が必要（†2） |
| `..`（範囲） | C# 8.0 | .NET Core 3.0 / .NET 5 | ⚠️ BCL 型が必要（†2） |
| `init` アクセサ | C# 9.0 | .NET 5 | ⚠️ BCL 型が必要（†3） |
| `with`（with 式） | C# 9.0 | .NET 5 | ⚠️ 対象が `init` を持つ場合のみ BCL 型が必要（†3） |
| Target-typed `new` | C# 9.0 | .NET 5 | ✅ 言語機能のみ（†1） |
| `required` プロパティ | C# 11.0 | .NET 7 | ⚠️ BCL 属性が必要（†4） |
| コレクション式 | C# 12.0 | .NET 8 | ✅ 言語機能のみ（†1） |
| プライマリコンストラクタ | C# 12.0 | .NET 8 | ✅ 言語機能のみ（†1） |

- **†1**: 純粋な言語機能。`LangVersion` を対応する C# バージョンに設定（または Visual Studio / .NET SDK を更新）すれば .NET Framework 上でも使用できる。
- **†2**: `System.Index` / `System.Range`（.NET Core 3.0+ で追加された BCL 型）が必要。配列のスライス `a[1..3]` はさらに `System.Runtime.CompilerServices.RuntimeHelpers.GetSubArray` を要求する。
- **†3**: `System.Runtime.CompilerServices.IsExternalInit`（.NET 5+ で追加された BCL 型）が必要。`with` 式が要求するかどうかは**対象の型が `init` アクセサを持つか**で決まる。`record` クラスと `readonly record struct` は `init` を生成するため必要になる。一方、可変な `struct` と positional の `record struct` は `init` を生成しないため不要である。
- **†4**: `RequiredMemberAttribute` に加えて `CompilerFeatureRequiredAttribute` と `IsExternalInit`（いずれも `System.Runtime.CompilerServices`）が必要。

**`init` を †1（言語機能のみ）に分類している解説は誤りである。**
`init` アクセサを持つ型に対する `with` 式も同じ制約を受ける。`IsExternalInit` が存在しない .NET Framework では `LangVersion` を上げてもコンパイルできない。
次節に、この分類を実際のコンパイル結果で示す。

### .NET Framework 4.8 に対するコンパイル結果

各構文を `net48` へ `LangVersion=latest` でコンパイルした結果が次の表である。
不足する型を自前定義した場合に通るようになるかも、同じ手順で確かめている。

<figure class="article-figure">
  <img src="/images/articles/csharp-operators-initialization-syntax-by-version/csharp-net-framework-matrix.svg" alt="各構文を net48 へコンパイルした結果の表。??=、!、new()、コレクション式、プライマリコンストラクタ、可変な struct への with、record struct への with は OK。a[^1]、a[1..3]、init、record への with、required は NG で、不足する型名が示され、ポリフィルを足すといずれも OK になっている。" width="576" height="440" loading="lazy">
  <figcaption>.NET SDK 10.0.302 で <code>net48</code> を対象に <code>LangVersion=latest</code> でコンパイルした結果。<code>missing type</code> はコンパイラが不足を報告した型で、複数ある場合は先頭 1 件と残りの件数を示す。<code>+ polyfill</code> はその型を自前定義したうえで再コンパイルした結果である。</figcaption>
</figure>

読み取れることは 3 点ある。

**1. `??=` や `!` は `LangVersion` を上げるだけで .NET Framework でも使える。**
Target-typed `new`、コレクション式、プライマリコンストラクタも同様である。
これらは記事冒頭の問題（`??=` が使えない）に対して、`LangVersion` の引き上げだけで解決できることを意味する。

**2. `init` と、`init` を持つ型への `with` は `LangVersion` を上げても通らない。**
`System.Runtime.CompilerServices.IsExternalInit` が .NET Framework に存在しないためである。
ここで分かれ目になるのは `with` という構文ではなく、**対象の型が `init` アクセサを持つかどうか**である。
表のとおり、可変な `struct` と positional の `record struct` への `with` は `net48` でもそのまま通る。これらは `init` ではなく通常のセッターを生成するためである。
一方、`record` クラスと `readonly record struct` は `init` を生成するため `IsExternalInit` を要する。

**3. `required` が要求する型は 1 つではない。**
`RequiredMemberAttribute` だけでなく、`CompilerFeatureRequiredAttribute` と `IsExternalInit` も必要になる。
`RequiredMemberAttribute` だけを自前定義しても解決しない。

いずれも、不足する型を自前定義すればコンパイルは通る。具体的な定義は後掲の「不足する型を自前で補う」に示す。

---

## 解決方法

C# の新構文でコンパイルエラーが発生した場合の対処方法は、主に以下の 2 つである。

### 方法1：`LangVersion` を引き上げる（またはビルド環境を更新する）

プロジェクトファイル（`.csproj`）に `<LangVersion>` を明示的に指定することで、使用する C# バージョンを制御できる。

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net481</TargetFramework>
    <LangVersion>8.0</LangVersion>  <!-- ??= や ! が使用可能になる -->
  </PropertyGroup>
</Project>
```

Visual Studio または .NET SDK を最新バージョンに更新することでも、対応する C# バージョンのコンパイラが利用可能になる。

### 方法2：旧構文に書き換える

ビルド環境を変更できない場合や、BCL 型が不足している場合（`System.Index` / `System.Range` など）は、同等の意味を持つ古い構文で代替する。

```csharp
// ??= が使えない場合
_numbers = _numbers ?? new List<int>();

// ^ が使えない場合（System.Index が不足）
int last = array[array.Length - 1];

// .. が使えない場合（System.Range が不足）
int[] sliced = array.Skip(1).Take(3).ToArray();
```

上記はいずれも意味を保ったまま旧構文で表現した例である（LINQ 例は `using System.Linq;` が必要）。

### 方法3：不足する型を自前で補う

BCL 側の型が足りないだけであれば、その型を自分のプロジェクトに定義すればコンパイルは通る。
コンパイラは名前空間と形が一致する型を探すだけであり、それがどのアセンブリにあるかは問わない。

以下は `net48` に対して実際にコンパイルし、通ることを確認した定義である。

`init` と `with` を使う場合は、`IsExternalInit` を定義する。

```csharp
namespace System.Runtime.CompilerServices
{
    // init アクセサと record が要求するマーカー型。中身は不要である。
    internal static class IsExternalInit { }
}
```

`required` を使う場合は、さらに 2 つの属性が必要になる。

```csharp
using System;

namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct
        | AttributeTargets.Field | AttributeTargets.Property, Inherited = false)]
    internal sealed class RequiredMemberAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.All, AllowMultiple = true, Inherited = false)]
    internal sealed class CompilerFeatureRequiredAttribute : Attribute
    {
        public CompilerFeatureRequiredAttribute(string featureName) => FeatureName = featureName;

        public string FeatureName { get; }
    }
}
```

`^` と `..` については、`System.Index` と `System.Range` に加え、配列のスライスで呼ばれる `RuntimeHelpers.GetSubArray` も定義する必要がある。

```csharp
using System;

namespace System
{
    internal readonly struct Index
    {
        private readonly int _value;

        public Index(int value, bool fromEnd = false) => _value = fromEnd ? ~value : value;

        public int Value => _value < 0 ? ~_value : _value;

        public bool IsFromEnd => _value < 0;

        public int GetOffset(int length) => IsFromEnd ? length - Value : Value;

        public static implicit operator Index(int value) => new Index(value);
    }

    internal readonly struct Range
    {
        public Range(Index start, Index end) { Start = start; End = end; }

        public Index Start { get; }

        public Index End { get; }

        public (int Offset, int Length) GetOffsetAndLength(int length)
        {
            int start = Start.GetOffset(length);
            int end = End.GetOffset(length);

            // 検査を省くと a[3..1] が負の長さを返す。標準の System.Range は
            // ArgumentOutOfRangeException を送出するため、そこへ揃える。
            if ((uint)end > (uint)length || (uint)start > (uint)end)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }

            return (start, end - start);
        }
    }
}

namespace System.Runtime.CompilerServices
{
    // 配列に対する a[1..3] は、コンパイラがこのメソッドへ変換する。
    internal static class RuntimeHelpers
    {
        public static T[] GetSubArray<T>(T[] array, Range range)
        {
            (int offset, int length) = range.GetOffsetAndLength(array.Length);
            var result = new T[length];
            Array.Copy(array, offset, result, 0, length);
            return result;
        }
    }
}
```

この定義を含む `net48` のコンソールアプリを実行すると、`a[^1]` と `a[1..3]` はいずれも .NET 5 以降と同じ値を返す。
`int[] a = { 10, 20, 30, 40, 50 }` に対して `a[^1]` は `50`、`a[1..3]` は `[20, 30]` となることを実機で確認した。

#### NuGet パッケージを使う場合の注意

`Index` / `Range` を提供する NuGet パッケージとしては `IndexRange` がある。
**`System.Index` という名前の NuGet パッケージは存在しない**（`nuget.org` は 404 を返す）。

`IndexRange` 1.1.1 を `net48` に参照させて確かめたところ、`a[^1]` と `Index` / `Range` 型は使えるようになるが、**配列のスライス `a[1..3]` は依然としてコンパイルできない**。

```text
error CS0656: コンパイラが必要とするメンバー
'System.Runtime.CompilerServices.RuntimeHelpers.GetSubArray' がありません
```

このパッケージは `GetSubArray` を含まないためである。
配列のスライスまで使うのであれば、上の `RuntimeHelpers` を自前で定義する必要がある。

#### 自前定義の注意点

- `RuntimeHelpers` は `mscorlib` にも同名の型が存在する。自プロジェクトで定義すると、そのプロジェクト内ではこちらが優先される。`RuntimeHelpers` の他のメンバー（`InitializeArray` など）を使っている場合は、そちらも自前の型へ実装する必要がある。**完全修飾名で書いても BCL 側は呼べない**（`System.Runtime.CompilerServices.RuntimeHelpers.InitializeArray` と書いてもローカル型が優先され、`CS0117` になる。`net48` で確認した）。実装を足したくない場合は、配列スライスのポリフィルを使わず `Skip` / `Take` などで代替する。
- いずれの型も `internal` で定義する。`public` にすると、そのアセンブリを参照する側と型が衝突しうる。
- ターゲットを .NET 5 以降へ移行した際は、これらの定義を削除する。BCL 側の型と重複すると、自プロジェクトの定義が優先されて意図しない挙動になりうる。

---

## 実装例

### 1. Null 安全演算子

#### `?.` と `?[]`（ヌル条件演算子）— C# 6.0 以降

オブジェクトまたはコレクションが `null` でない場合のみメンバーや要素にアクセスする。
対象が `null` であれば評価を行わず `null` を返すため、事前の `null` チェックを省略できる。

```csharp
string title = GetTitle();
int? length = title?.Length; // title が null なら length も null になる

List<string> items = GetItems();
string firstItem = items?[0]; // items が null なら firstItem も null になる
```

`?.` と `?[]` はメソッドチェーン中に組み合わせて使用できる。
いずれかの箇所で `null` が評価された時点でチェーン全体が `null` を返す。

#### `??`（ヌル合体演算子）— C# 2.0 以降

左辺の値が `null` でない場合はその値を、`null` である場合は右辺の値を返す。
`null` 時のデフォルト値を指定する際に使用される。

```csharp
string typedName = GetName();
string displayName = typedName ?? "Anonymous"; // typedName が null なら "Anonymous" になる
```

`??` は .NET Framework 2.0 以降でサポートされており、バージョンを問わず使用できる。

#### `??=`（ヌル合体割り当て演算子）— C# 8.0 以降

左辺の変数が `null` の場合にのみ右辺の値を代入する。
遅延初期化（Lazy Initialization）パターンで多用される。

```csharp
private List<int> _numbers;

public void AddNumber(int val)
{
    _numbers ??= new List<int>(); // _numbers が null の時だけインスタンスを生成
    _numbers.Add(val);
}
```

ビルド環境が C# 8.0 未満（例: `LangVersion` が 7.3 以前）としてコンパイルされる場合、`??=` が使用できないため、`??` を用いた以下の等価な記述で代替する。

```csharp
_numbers = _numbers ?? new List<int>();
```

この書き方は `??=` と同じく「`null` のときだけ代入する」意図を表すが、演算子が使える環境では `??=` の方が簡潔である。

#### `!`（null 免除演算子）— C# 8.0 以降

C# の静的コード分析に対して「この変数はこの時点で絶対に `null` ではない」と宣言するための演算子である。
コンパイル時の Nullable 警告を抑制するためだけのものであり、実行時の挙動には一切影響を与えない。

```csharp
string? rawInput = GetValidatedInput();
// 事前チェックにより、ここでは null にならないことが確定していると仮定
string solidInput = rawInput!;
```

`!` を使用すると `null` チェックが省略されるため、実際に `null` が渡された場合は実行時例外が発生する。
静的分析への過信は危険であり、使用箇所を最小限に抑えることが望ましい。

---

### 2. インデックスと範囲

#### `^`（末尾からのインデックス演算子）— C# 8.0 以降

コレクションの末尾からの位置を表す `System.Index` 型を生成する。
`^1` は最後の要素（`Length - 1`）、`^0` はコレクションの要素数（`Length`）と同じ位置を指す。

```csharp
int[] digits = new[] { 10, 20, 30, 40 };
int last = digits[^1];         // 40（digits[digits.Length - 1] と等価）
int secondFromLast = digits[^2]; // 30
```

.NET Framework では `System.Index` 型が標準では提供されないため、追加参照／ポリフィルなしでは `^` 演算子を使用できない。
代替として `array[array.Length - 1]` のように明示的なインデックス計算を行う。

#### `..`（範囲演算子）— C# 8.0 以降

開始インデックスと終了インデックスを指定して部分範囲（`System.Range`）を生成し、配列や文字列のスライスを直感的に記述できる。
開始インデックスは含まれ、終了インデックスは含まれない（半開区間）。

```csharp
int[] dataset = new[] { 0, 1, 2, 3, 4, 5 };
int[] sliced = dataset[1..4]; // [1, 2, 3]（インデックス 1 から 4 未満）

// 先頭・末尾の省略
int[] continuous = dataset[2..];  // インデックス 2 から末尾まで [2, 3, 4, 5]
int[] allButLast = dataset[..^1]; // 先頭から末尾の 1 つ前まで [0, 1, 2, 3, 4]
```

`..` 演算子には `System.Range` 型が必要であり、.NET Framework では追加参照またはポリフィルなしに使用できない。
代替として `array.Skip(start).Take(count).ToArray()` のような LINQ を使用する方法がある。

---

### 3. 型演算子

#### `is` と `as`（型チェック・型キャスト演算子）

`is` はオブジェクトが特定の型と互換性があるかを判定する。
C# 7.0 以降ではパターンマッチングと組み合わせて、型が一致した場合に変数を宣言して代入できる。

`as` は型変換を試みて成功すればキャストされたオブジェクトを返し、失敗した場合は例外を発生させずに `null` を返す。

```csharp
object element = "Hello WPF";

// is パターンマッチング（C# 7.0 以降）
if (element is string message)
{
    Console.WriteLine(message.Length); // このブロック内では string 型として扱える
}

// as 演算子
var stream = element as System.IO.Stream; // 変換できないため stream は null になる
```

`(型名)obj` の強制キャストは変換不可時に `InvalidCastException` を投げるのに対し、`as` は `null` を返すだけであるため、型の互換性が不確実な場合は `as` または `is` パターンマッチングを優先する。

---

### 4. データ操作・その他の演算子

#### `=>`（ラムダ演算子 / 式形式のメンバー）— C# 3.0 / C# 6.0 以降

C# 3.0 でラムダ式の構文として導入され、C# 6.0 ではプロパティやメソッドの定義を 1 行で記述する「式形式のメンバー（Expression-bodied members）」としても使用できるようになった。

```csharp
public class Rectangle
{
    private readonly double _width;
    private readonly double _height;

    public Rectangle(double width, double height)
    {
        _width = width;
        _height = height;
    }

    // 式形式のプロパティ（C# 6.0）
    public double Area => _width * _height;

    // 式形式のメソッド（C# 6.0）
    public void PrintArea() => Console.WriteLine($"Area: {Area}");
}
```

式形式のメンバーは、処理が単一の式で表現できる場合にブロック本体を省略でき、クラス定義の可読性が向上する。

#### `nameof`（ネームオブ演算子）— C# 6.0 以降

変数、型、プロパティ、メソッドなどの識別子名をコンパイル時に文字列として取得する。
識別子名を文字列としてハードコードしないため、リファクタリング時の追随が自動化され、タイポを防止できる。

```csharp
public void UpdateText(string? newText)
{
    if (newText == null)
    {
        // 引数名が変わっても nameof が自動的に追随する
        throw new ArgumentNullException(nameof(newText));
    }
}
```

`nameof` の戻り値はコンパイル時定数であるため、`switch` 文の `case` ラベルや属性の引数としても使用できる。

#### `with`（with 式）— C# 9.0 以降

レコード型（`record`）や構造体などの不変オブジェクトをベースに、一部のプロパティのみを変更した新しいコピーインスタンスを生成する。
元のオブジェクトは変更されない。

```csharp
public record WindowSettings(string Title, double Width, double Height);

var defaultSettings = new WindowSettings("Main", 800, 600);
// Title と Width はそのまま引き継ぎ、Height のみ変更した新しいインスタンスを生成
var tallSettings = defaultSettings with { Height = 1000 };
```

`with` 式は C# 9.0 で導入された言語機能であり、`LangVersion` を C# 9.0 以上に設定することで .NET Framework 上でも使用可能である。
ただし `record` や `init` アクセサーを利用する場合、.NET Framework では `System.Runtime.CompilerServices.IsExternalInit` の追加定義（ポリフィル）が必要になることがある。

---

### 5. 初期化の糖衣構文

#### Target-typed `new` 式（ターゲット型の new 式）— C# 9.0 以降

代入先の型またはメソッドの引数の型からインスタンス化すべき型が推論できる場合、`new` の後ろの型名を省略して `new()` と記述できる。

```csharp
public class Example
{
    public void Run()
    {
        // 以前の記述
        Dictionary<string, List<string>> map1 = new Dictionary<string, List<string>>();

        // Target-typed new
        Dictionary<string, List<string>> map2 = new();

        // メソッド引数への適用（型は引数から推論される）
        RegisterNumbers(new() { 1, 2, 3 });
    }

    private void RegisterNumbers(List<int> numbers) { }
}
```

型が左辺または引数の型宣言から明確に推論できる場合のみ省略可能である。
`var` と組み合わせると右辺の型が推論できなくなるため、`var map = new();` のような記述はコンパイルエラーとなる。

#### コレクション式（Collection Expressions）— C# 12.0 以降

配列、`List<T>`、`Span<T>`、その他カスタムコレクションなど、あらゆるコレクションの初期化を `[...]` の統一した記法で記述できる。

```csharp
int[] row = [1, 2, 3];                     // 配列
List<string> tags = ["C#", "WPF", ".NET"]; // List<T>
ReadOnlySpan<byte> data = [0x00, 0x01];    // Span<T>
```

コレクション式の中では `..` スプレッド演算子を使用して、別のコレクションの要素をフラットに展開して結合できる。

```csharp
int[] left = [1, 2];
int[] right = [5, 6];

int[] result = [.. left, 3, 4, .. right]; // [1, 2, 3, 4, 5, 6] が生成される
```

コレクション式は C# 12.0 で導入された構文であり、利用には C# 12.0 に対応したコンパイラ（`LangVersion` 12.0 以上）／SDK が必要である。

#### `required` プロパティ — C# 11.0 以降

`required` 修飾子を付与したプロパティは、オブジェクト初期化子によるインスタンス生成時に値の設定がコンパイルレベルで強制される。
コンストラクタの引数を追加せずに初期化漏れを防止できる。

```csharp
public class AppTheme
{
    public required string ThemeName { get; init; } // 初期化時の指定が必須
    public string Author { get; init; } = "Unknown"; // 必須ではない（デフォルト値あり）
}

// コンパイル成功
var lightTheme = new AppTheme { ThemeName = "Light Mode" };

// コンパイルエラー（ThemeName が指定されていないため）
// var invalidTheme = new AppTheme { Author = "s-iguchi" };
```

`required` は C# 11.0 で導入された構文であり、.NET 7 以降では必要な属性が標準で提供される。

.NET Framework では 3 つの型が不足する。`net48` へコンパイルすると、次の 3 件がまとめて報告される。

- `System.Runtime.CompilerServices.RequiredMemberAttribute`
- `System.Runtime.CompilerServices.CompilerFeatureRequiredAttribute`
- `System.Runtime.CompilerServices.IsExternalInit`（上の例が `init` アクセサを使うため）

`RequiredMemberAttribute` だけを自前定義しても解決しない点に注意する。
3 つすべての定義は「方法3：不足する型を自前で補う」に示した。

#### プライマリコンストラクタ — C# 12.0 以降

C# 12 から `class` や `struct` でもクラス名の後ろに直接コンストラクタの引数を定義できるようになった。
コンストラクタ本体の記述や、引数をプライベートフィールドに代入するだけの定型コードが不要になる。

```csharp
public class LogWriter(string logFilePath, LogLevel minimumLevel)
{
    // 引数はクラス内の全メンバーから直接参照できる
    public void WriteLog(string message, LogLevel level)
    {
        if (level >= minimumLevel)
        {
            System.IO.File.AppendAllText(logFilePath, $"[{level}] {message}\n");
        }
    }
}
```

引数として渡された値（`logFilePath` や `minimumLevel`）は、クラス内のどのメンバーからも直接参照でき、パラメータの状態がそのまま維持される。
プライマリコンストラクタは C# 12.0 で導入された構文であり、利用には C# 12.0 に対応したコンパイラ（`LangVersion` 12.0 以上）／SDK が必要である。

---

## 注意点

- .NET Framework をターゲットにする場合でも、`??=` や `!` のような言語機能はビルド環境（コンパイラ／`LangVersion`）が対応していれば使用できる。一方 `^` / `..`（Index / Range）は必要な型・API（`System.Index` / `System.Range` など）の有無に依存するため、追加参照／ポリフィルが必要になるか、利用できない場合がある。
- `!`（null 免除演算子）はコンパイル時の警告を抑制するだけであり、実行時の `null` チェックを行わない。
  使用した箇所で `null` が渡された場合は `NullReferenceException` が発生するため、使用箇所を最小限に抑える。
- Target-typed `new`、コレクション式、プライマリコンストラクタは純粋な言語機能であり、`LangVersion` を対応バージョンに設定すれば .NET Framework 上でも使用できる（`net48` で確認済み）。一方 **`init` は純粋な言語機能ではない**。`init` を持つ型への `with` も同様である。`IsExternalInit` を必要とするため、`LangVersion` を上げただけでは .NET Framework でコンパイルできない。可変な `struct` と positional の `record struct` への `with` は、通常のセッターを生成するためそのまま通る。`required` はさらに `RequiredMemberAttribute` と `CompilerFeatureRequiredAttribute` を要する。
- コレクション式の `..` スプレッド演算子は、C# 8.0 の範囲演算子 `..` と同じ記号を使用するが、用途が異なる（コレクション式の中での要素展開）。

---

## 代替案・比較

C# 新構文でコンパイルエラーが発生した場合の対処アプローチを比較する。

| 方法 | メリット | デメリット | 適するケース |
| --- | --- | --- | --- |
| `LangVersion` を引き上げる | 新構文をそのまま使用できる。コードの簡潔さが維持される。 | ビルド環境（VS / SDK）のアップデートが必要。 | 開発環境を更新できる場合。長期的に保守するプロジェクト。 |
| ビルド環境（VS / .NET SDK）を更新する | 最新の言語機能・ツールサポートを得られる。 | 既存プロジェクトへの影響範囲が広い場合がある。 | 新規または移行可能なプロジェクト。 |
| 旧構文に書き換える | 環境を一切変更せずに対応できる。 | コードが冗長になる。新機能の恩恵を受けられない。 | レガシー環境で環境変更が許可されない場合。 |
| 不足する型を自前で定義する | `^` / `..` / `init` / `with` / `required` を .NET Framework で使用できる。追加パッケージが要らない。 | 定義の管理が必要。.NET 5 以降へ移行する際は削除する。`RuntimeHelpers` は BCL の同名型を隠す。 | BCL 型が不足する構文を .NET Framework で利用したい場合。 |
| `IndexRange` パッケージを参照する | `Index` / `Range` 型と `a[^1]` が使えるようになる。 | 配列のスライス `a[1..3]` には対応しない（`GetSubArray` を含まないため、別途自前定義が必要）。 | `^` だけを使えれば足りる場合。 |

---

## まとめ

C# の演算子と初期化構文は言語バージョンとともに段階的に追加されており、使用可能かどうかは主にコンパイラ（`LangVersion`）と、機能が要求するランタイム側の型・API の有無に依存する。

環境ごとの選択基準は以下のとおりである。

- **.NET Framework 環境（コンパイラを更新しない場合）**：`??`（C# 2.0）、`?.`（C# 6.0）、`nameof`（C# 6.0）、`is` パターンマッチング（C# 7.0）が上限の目安となる。`^1` は `array[array.Length - 1]` に、`..` は LINQ に置き換えて対処する。
- **.NET Framework 環境（LangVersion を引き上げた場合）**：`??=`、`!`、Target-typed `new`、コレクション式、プライマリコンストラクタが追加で利用可能になる（`net48` で確認済み）。一方 `^` / `..` / `init` / `with` / `required` は BCL 側の型が不足するため、自前定義するまで使えない。
- **.NET 5〜6（C# 9〜10）**：BCL 型も含めてすべての C# 9〜10 機能が使用可能になる。
- **.NET 7（C# 11）以降**：`required` プロパティが利用可能になる。
- **.NET 8（C# 12）以降**：コレクション式、プライマリコンストラクタが利用可能になる。

実際に `net48` へコンパイルして確かめたところ、**「純粋な言語機能かどうか」は構文の見た目からは判断できない**ことが分かった。
`with` は演算子のように見えるが `IsExternalInit` を要し、逆にプライマリコンストラクタやコレクション式のような大きな構文追加が BCL 型を要さない。

したがって、`LangVersion` を上げれば済むのか、型を補う必要があるのかは、**対象フレームワークへ実際にコンパイルして確認する**のが確実である。
不足する型はコンパイラが `CS0518` / `CS0656` で名指しするため、そのまま自前定義すればよい。

---

<!-- 関連記事 -->
