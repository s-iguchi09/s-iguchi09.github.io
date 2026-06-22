---
layout: article-ja
title: "C# バージョン別 演算子と初期化構文シンタックスシュガー一覧"
date: 2026-06-22
category: C#
excerpt: ".NET Framework 環境では ??= などの C# 新構文が使用できない場合がある。本記事では C# 各バージョンで追加された演算子と初期化の糖衣構文をバージョン対応表とコード例とともに網羅的に整理する。"
---

## 概要

本記事では、.NET Framework 環境で `??=`（ヌル合体割り当て演算子）が使用できないという問題を契機として、C# 各バージョンで追加された演算子および初期化の糖衣構文（シンタックスシュガー）をバージョン対応表とコード例を交えて網羅的に整理する。
対応するバージョンを事前に把握しておくことで、環境の制約に応じた適切な実装選択が可能となる。

---

## 前提・対象環境

- 言語: C# 2.0 〜 C# 12
- フレームワーク: .NET Framework 2.0〜4.8 / .NET Core / .NET 5〜8
- 対象機能: Null 安全演算子、インデックス・範囲演算子、型演算子、初期化の糖衣構文

---

## 問題

.NET Framework をターゲットにした C# 開発において、プロジェクトが C# 8.0 未満（例: 7.3）としてコンパイルされている場合、`??=`（ヌル合体割り当て演算子）を使用するとコンパイルエラーが発生する。

```csharp
private List<int>? _numbers;

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

| 演算子 / 構文 | C# バージョン | .NET バージョン | .NET Framework サポート |
| --- | --- | --- | --- |
| `??`（ヌル合体） | C# 2.0 | .NET Framework 2.0 | ✅ 2.0 以降 |
| `as`（型キャスト） | C# 1.0 | .NET Framework 1.0 | ✅ 全バージョン |
| `is`（型チェック） | C# 1.0 | .NET Framework 1.0 | ✅ 全バージョン |
| `=>`（ラムダ） | C# 3.0 | .NET Framework 3.5 | ✅ 3.5 以降 |
| `=>`（式形式のメンバー） | C# 6.0 | .NET Framework 4.6 | ✅ 4.6 以降 |
| `?.` `?[]`（ヌル条件） | C# 6.0 | .NET Framework 4.6 | ✅ 4.6 以降 |
| `nameof` | C# 6.0 | .NET Framework 4.6 | ✅ 4.6 以降 |
| `is` パターンマッチング | C# 7.0 | .NET Framework 4.7 | ✅ 4.7 以降 |
| `??=`（ヌル合体割り当て） | C# 8.0 | .NET Core 3.0 / .NET 5 | ⚠️ 部分サポート / 実質非推奨 |
| `!`（null 免除） | C# 8.0 | .NET Core 3.0 / .NET 5 | ⚠️ 部分サポート |
| `^`（末尾インデックス） | C# 8.0 | .NET Core 3.0 / .NET 5 | ⚠️ 部分サポート |
| `..`（範囲） | C# 8.0 | .NET Core 3.0 / .NET 5 | ⚠️ 部分サポート |
| `with`（with 式） | C# 9.0 | .NET 5 | ❌ 非対応 |
| Target-typed `new` | C# 9.0 | .NET 5 | ❌ 非対応 |
| `required` プロパティ | C# 11.0 | .NET 7 | ❌ 非対応 |
| コレクション式 | C# 12.0 | .NET 8 | ❌ 非対応 |
| プライマリコンストラクタ | C# 12.0 | .NET 8 | ❌ 非対応 |

---

## 実装例

### 1. Null 安全演算子

#### `?.` と `?[]`（ヌル条件演算子）— C# 6.0 以降

オブジェクトまたはコレクションが `null` でない場合のみメンバーや要素にアクセスする。
対象が `null` であれば評価を行わず `null` を返すため、事前の `null` チェックを省略できる。

```csharp
string? title = GetTitle();
int? length = title?.Length; // title が null なら length も null になる

List<string>? items = GetItems();
string? firstItem = items?[0]; // items が null なら firstItem も null になる
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
private List<int>? _numbers;

public void AddNumber(int val)
{
    _numbers ??= new List<int>(); // _numbers が null の時だけインスタンスを生成
    _numbers.Add(val);
}
```

.NET Framework では C# 8.0 のサポートが制限されるため、`??=` が使用できない場合は `??` を用いた以下の等価な記述で代替する。

```csharp
_numbers = _numbers ?? new List<int>();
```

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
int[] digits = [10, 20, 30, 40];
int last = digits[^1];         // 40（digits[digits.Length - 1] と等価）
int secondFromLast = digits[^2]; // 30
```

.NET Framework では `System.Index` 型を利用できないため、`^` 演算子は使用できない。
代替として `array[array.Length - 1]` のように明示的なインデックス計算を行う。

#### `..`（範囲演算子）— C# 8.0 以降

開始インデックスと終了インデックスを指定して部分範囲（`System.Range`）を生成し、配列や文字列のスライスを直感的に記述できる。
開始インデックスは含まれ、終了インデックスは含まれない（半開区間）。

```csharp
int[] dataset = [0, 1, 2, 3, 4, 5];
int[] sliced = dataset[1..4]; // [1, 2, 3]（インデックス 1 から 4 未満）

// 先頭・末尾の省略
int[] continuous = dataset[2..];  // インデックス 2 から末尾まで [2, 3, 4, 5]
int[] allButLast = dataset[..^1]; // 先頭から末尾の 1 つ前まで [0, 1, 2, 3, 4]
```

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
public class Rectangle(double width, double height)
{
    // 式形式のプロパティ
    public double Area => width * height;

    // 式形式のメソッド
    public void PrintArea() => Console.WriteLine($"Area: {Area}");
}
```

式形式のメンバーは、処理が単一の式で表現できる場合にブロック本体を省略でき、クラス定義の可読性が向上する。

#### `nameof`（ネームオブ演算子）— C# 6.0 以降

変数、型、プロパティ、メソッドなどの識別子名をコンパイル時に文字列として取得する。
識別子名を文字列としてハードコードしないため、リファクタリング時の追随が自動化され、タイポを防止できる。

```csharp
public void UpdateText(string newText)
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

`with` 式は .NET 5（C# 9.0）以降でのみ使用可能であり、.NET Framework では利用できない。

---

### 5. 初期化の糖衣構文

#### Target-typed `new` 式（ターゲット型の new 式）— C# 9.0 以降

代入先の型またはメソッドの引数の型からインスタンス化すべき型が推論できる場合、`new` の後ろの型名を省略して `new()` と記述できる。

```csharp
// 以前の記述
Dictionary<string, List<string>> map = new Dictionary<string, List<string>>();

// Target-typed new
Dictionary<string, List<string>> map = new();

// メソッド引数への適用
public void RegisterProfile(UserOptions options) { }
RegisterProfile(new() { IsActive = true });
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

コレクション式は .NET 8（C# 12.0）以降でのみ使用可能である。

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

`required` は .NET 7（C# 11.0）以降でのみ使用可能である。

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
プライマリコンストラクタは .NET 8（C# 12.0）以降でのみ使用可能である。

---

## 注意点

- .NET Framework では C# 8.0 のサポートが限定的であり、`??=`、`^`、`..`、`!` などの演算子は原則として使用できないものとして扱うことが安全である。
- `!`（null 免除演算子）はコンパイル時の警告を抑制するだけであり、実行時の `null` チェックを行わない。
  使用した箇所で `null` が渡された場合は `NullReferenceException` が発生するため、使用箇所を最小限に抑える。
- `with` 式、Target-typed `new`、コレクション式、`required`、プライマリコンストラクタはいずれも .NET Framework では使用できない。
  これらを使用する場合は .NET 5 以降への移行が前提となる。
- コレクション式の `..` スプレッド演算子は、C# 8.0 の範囲演算子 `..` と同じ記号を使用するが、用途が異なる（コレクション式の中での要素展開）。

---

## まとめ

C# の演算子と初期化構文は言語バージョンとともに段階的に追加されており、使用可能かどうかはターゲットフレームワークに依存する。

環境ごとの選択基準は以下のとおりである。

- **.NET Framework 環境**：`??`（C# 2.0）、`?.`（C# 6.0）、`nameof`（C# 6.0）、`is` パターンマッチング（C# 7.0）が上限の目安となる。
  `??=` は `_x = _x ?? value;` に、`^1` は `array[array.Length - 1]` に置き換えて対処する。
- **.NET 5〜6（C# 9〜10）**：`??=`、`with`、Target-typed `new` が利用可能になる。
- **.NET 7（C# 11）以降**：`required` プロパティが利用可能になる。
- **.NET 8（C# 12）以降**：コレクション式、プライマリコンストラクタが利用可能になる。

ターゲットフレームワークの制約を事前に確認したうえで、利用可能な構文を選択することが適切である。

---

<!-- 関連記事 -->
